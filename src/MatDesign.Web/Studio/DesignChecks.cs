using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Web.Studio;

/// <summary>How serious a finding is: an error stops a release, advice is for a person to look at.</summary>
public enum Severity
{
    Error,
    Advice,
}

/// <summary>A thing a check found. <see cref="Kind"/> and <see cref="Id"/> name the item it is about (apps, rules, components), so the report can link to it; a <see cref="Link"/> leads somewhere else.</summary>
public sealed record Finding(string Check, Severity Severity, string Message, string? Kind = null, string? Id = null, string? Link = null);

/// <summary>What a check looks at: the design data as it is now (built in, with what the Studio saved), the toolkit and the Studio's storage.</summary>
public sealed record CheckContext(DesignData Design, ToolkitFiles Toolkit, StudioService Studio, Func<VersionTracker> VersionsOf, Func<ReleaseService>? ReleasesOf = null)
{
    /// <summary>The pipeline, for a check that needs to try the build (the white label setting): asked for when needed, because the pipeline needs the checks.</summary>
    public ReleaseService? Releases => ReleasesOf?.Invoke();

    /// <summary>Which version of MatUi each app uses (asked for when a check needs it: the tracker needs the releases, which need the checks).</summary>
    public VersionTracker Versions => VersionsOf();
}

/// <summary>One check of the design system: what it is called, which area it belongs to, what it holds to, and the code that finds what breaks it.</summary>
public sealed record DesignCheck(string Id, string Area, string Name, string What, Func<CheckContext, IEnumerable<Finding>> Run);

/// <summary>The findings of one check.</summary>
public sealed record CheckResult(DesignCheck Check, IReadOnlyList<Finding> Findings)
{
    public int Errors => Findings.Count(f => f.Severity == Severity.Error);

    public int Advice => Findings.Count(f => f.Severity == Severity.Advice);

    /// <summary>Advice does not fail a check.</summary>
    public bool Passed => Errors == 0;
}

/// <summary>The result of running every check against the data of one moment (<see cref="Version"/> is the version of the design data it was run on).</summary>
public sealed record CheckReport(long Version, DateTimeOffset At, IReadOnlyList<CheckResult> Results)
{
    public int Errors => Results.Sum(r => r.Errors);

    public int Advice => Results.Sum(r => r.Advice);

    public bool Passed => Errors == 0;

    public IEnumerable<Finding> Findings => Results.SelectMany(r => r.Findings);
}

/// <summary>
/// The checks of the design system in one place: the validators on every app, rule and component as they stand now (the built-in ones too), the contrast of every accent, the rules
/// that patterns cite, the ids of the pages of the documentation, the tokens, the naming of the toolkit, the menu, and the items on the volume that the site had to leave out. The
/// report is the same for the page that shows it and for the pipeline that decides whether a release may be made; it is computed again when the design data changes.
/// </summary>
public sealed partial class DesignChecks(DesignData design, ToolkitFiles toolkit, StudioService studio, IServiceProvider services)
{
    private readonly object _gate = new();
    private CheckReport? _report;

    [GeneratedRegex(@"^(?:mat|is)-")]
    private static partial Regex ToolkitClassName();

    /// <summary>The report for the design data as it is now. It is computed once for each version of the data.</summary>
    public CheckReport Report
    {
        get
        {
            var current = _report;
            if (current is not null && current.Version == design.Version)
            {
                return current;
            }

            lock (_gate)
            {
                if (_report is null || _report.Version != design.Version)
                {
                    _report = Run();
                }

                return _report;
            }
        }
    }

    /// <summary>Runs every check now, whatever was computed before.</summary>
    public CheckReport Run()
    {
        var version = design.Version;
        var context = new CheckContext(design, toolkit, studio, () => services.GetRequiredService<VersionTracker>(), () => services.GetRequiredService<ReleaseService>());
        var results = All.Select(check => new CheckResult(check, check.Run(context).ToList())).ToList();
        return new CheckReport(version, DateTimeOffset.UtcNow, results);
    }

    /// <summary>Where a finding leads: the edit page of the item, or its history when it is gone from the site; null when it is about nothing in particular.</summary>
    public string? UrlOf(Finding finding)
    {
        if (finding.Link is not null)
        {
            return finding.Link;
        }

        if (finding.Kind is null || finding.Id is null || !StudioHistory.IsKind(finding.Kind))
        {
            return null;
        }

        var exists = finding.Kind switch
        {
            "apps" => design.App(finding.Id) is not null,
            "rules" => design.Rule(finding.Id) is not null,
            Brand.Kind => finding.Id == Brand.ItemId,
            WhiteLabelSetting.Kind => finding.Id == WhiteLabelSetting.ItemId,
            Tenant.Kind => finding.Id == Tenant.ItemId,
            _ => design.Component(finding.Id) is not null,
        };
        if (exists)
        {
            return StudioHistory.EditUrl(finding.Kind, finding.Id);
        }

        return studio.Enabled && StudioStore.IsId(finding.Id) && studio.Store.Current(finding.Kind, finding.Id) is not null ? StudioHistory.HistoryUrl(finding.Kind, finding.Id) : null;
    }

    /// <summary>Every check, in the order the report shows them.</summary>
    public static IReadOnlyList<DesignCheck> All { get; } =
    [
        new("tenant", "Company", "The company is valid", "The names of the company, its template and, for the blank template, the accent of the site (it reaches AA in light and dark) pass what the Company page holds them to (IDENT-09).", TenantValid),
        new("apps", "Apps", "Every app is valid", "Name, id, tagline, category, glyph, port and colours of every app pass what a save holds them to, and every accent reaches AA in light and dark with its text; the tokens and component properties an app sets for itself are within the limits of the brand's editor.", AppsValid),
        new("brand", "Brand", "The brand is valid", "The names of the brand, every token value it sets (its kind and its limits), its component properties (their type, their limits, and no touch target under 44 px for a touch screen), what it sets inside each colour scheme, and the contrast of its surfaces and of the accent of every app, in light and dark and in every colour scheme (COLOR-11, SPACE-06). The apps' own overrides are held to the same limits under the apps check.", BrandValid),
        new("versions", "Apps", "The apps use the latest release", "Every app whose version of MatUi is written down uses the latest release. Advice: updating an app is the work of its own repository.", Versions),
        new("palette", "Apps", "No two accents are alike", "No two accents are closer than the palette check allows (IDENT-04). Advice: changing an app's colour is a decision for a person.", Palette),
        new("rules", "Rules", "Every rule is valid", "Id of its area, title, statement, level and source of every rule pass what a save holds them to.", RulesValid),
        new("components", "Components", "Every component is valid", "Every example uses only what the toolkit ships, with no inline style, handler or script, nothing from outside, labelled cells, typed buttons and unique ids; the rules a component names exist.", ComponentsValid),
        new("page-ids", "Components", "The ids of a page are unique", "The components, the layout and the utilities pages draw many examples on one page: no id is on a page twice, and no id is one the page makes itself.", PageIds),
        new("groups", "Components", "Every group has components", "Each group of the documentation has at least one component, so no heading stands over nothing.", Groups),
        new("patterns", "Components", "Patterns cite what exists", "The rules and the apps that the page patterns name are in the rulebook and in the family.", Patterns),
        new("tokens", "Toolkit", "Every token is described", "Every custom property says what it is for, and every colour token has a dark value or is derived from one that has.", Tokens),
        new("naming", "Toolkit", "The toolkit follows its naming", "Every class of the stylesheets starts with mat- (or is a state such as is-active) (CODE-04).", Naming),
        new("menu", "Site", "The menus are well formed", "Every menu has at most one level of submenus, its ids are unique, and so are its addresses.", Menus),
        new("volume", "Studio", "Nothing on the volume is left out", "Every item the Studio saved is read back by the site; an item that was edited by hand into something unsafe or wrong is not, and is reported here.", Volume),
        new("whitelabel", "Release", "The white label setting builds", "The prefix and the name of the white label setting pass what a save holds them to, and a trial build with them (the renamer and the checks of the toolkit on the renamed copy) finds nothing: a release with a setting that fails is refused (CODE-09).", WhiteLabelBuilds),
    ];

    private static IEnumerable<Finding> WhiteLabelBuilds(CheckContext c)
    {
        if (!c.Studio.Enabled)
        {
            yield break;
        }

        var stored = c.Studio.Store.Current(WhiteLabelSetting.Kind, WhiteLabelSetting.ItemId);
        if (stored is null or { Deleted: true })
        {
            yield break;
        }

        var saved = WhiteLabelStore.Saved(c.Studio);
        if (saved is null)
        {
            yield return new Finding("whitelabel", Severity.Error, "The white label setting on the volume is not valid (a prefix or a name the build cannot use): a release would not carry it.", WhiteLabelSetting.Kind, WhiteLabelSetting.ItemId);
            yield break;
        }

        if (saved.IsNoOp || c.Releases is not { } releases)
        {
            yield break;
        }

        var trial = releases.TrialBuild(saved);
        foreach (var finding in trial.Findings.Take(20))
        {
            yield return new Finding("whitelabel", Severity.Error, $"With the prefix {saved.Prefix}- and the name {saved.Name}: [{finding.Check}] {(finding.Path is null ? string.Empty : finding.Path + ": ")}{finding.Message}", WhiteLabelSetting.Kind, WhiteLabelSetting.ItemId);
        }
    }

    private static IEnumerable<Finding> TenantValid(CheckContext c)
    {
        foreach (var problem in TenantValidator.Validate(TenantValidator.Normalize(c.Design.Tenant)))
        {
            yield return new Finding("tenant", Severity.Error, $"{problem.Field}: {problem.Message}", Tenant.Kind, Tenant.ItemId);
        }

        // advice: a company that kept the names of the demo would put another company's name on its own releases (IDENT-09)
        if (!c.Design.IsDemo && c.Design.Tenant.SiteName.Contains("MatDesign", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Finding("tenant", Severity.Advice, $"The site is called {c.Design.Tenant.SiteName}, the name of the demo. Give it a name of the company.", Tenant.Kind, Tenant.ItemId);
        }
    }

    private static IEnumerable<Finding> AppsValid(CheckContext c)
    {
        foreach (var app in c.Design.Apps)
        {
            var others = c.Design.Apps.Where(a => a.Id != app.Id);
            foreach (var problem in AppValidator.Validate(app, others, c.Toolkit.IconNames))
            {
                yield return new Finding("apps", Severity.Error, $"{app.Name}: {problem.Message}", "apps", app.Id);
            }

            // what the app sets for itself on top of the brand (stage 12): its tokens and component properties, within the same limits
            var overrides = AppOverrides.Normalize(app, c.Toolkit.EditableTokens, c.Toolkit.EditableProperties);
            foreach (var problem in AppOverrides.Validate(overrides, c.Toolkit.EditableTokens, c.Toolkit.EditableProperties, c.Design.Brand))
            {
                yield return new Finding("apps", Severity.Error, $"{app.Name}, {problem.Field.Replace(":", " ")}: {problem.Message}", "apps", app.Id);
            }
        }
    }

    private static IEnumerable<Finding> BrandValid(CheckContext c)
    {
        var brand = BrandValidator.Normalize(c.Design.Brand, c.Toolkit.EditableTokens, c.Toolkit.EditableProperties, c.Toolkit.Schemes);
        foreach (var problem in BrandValidator.Validate(brand, c.Toolkit.Tokens, c.Toolkit.Schemes, c.Design.Apps, c.Toolkit.EditableProperties))
        {
            yield return new Finding("brand", Severity.Error, $"{problem.Field}: {problem.Message}", Brand.Kind, Brand.ItemId);
        }
    }

    private static IEnumerable<Finding> Versions(CheckContext c)
    {
        foreach (var behind in c.Versions.All().Where(v => v.State == VersionState.Behind))
        {
            yield return new Finding("versions", Severity.Advice, $"{behind.App.Name} uses MatUi {behind.Version}, {StudioFormat.Count(behind.Behind, "release")} behind the latest ({c.Versions.LatestVersion}).", "apps", behind.App.Id, $"/Studio/Versions/Edit?id={behind.App.Id}");
        }
    }

    private static IEnumerable<Finding> Palette(CheckContext c)
    {
        foreach (var pair in PaletteAdvisor.Collisions(c.Design.Apps))
        {
            yield return new Finding("palette", Severity.Advice, $"{pair.A.Name} and {pair.B.Name} are close ({pair.Distance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}): they can be mistaken for one another.", "apps", pair.A.Id);
        }
    }

    private static IEnumerable<Finding> RulesValid(CheckContext c)
    {
        foreach (var rule in c.Design.Rules)
        {
            foreach (var problem in RuleValidator.Validate(rule, c.Design.Rules.Where(r => r.Id != rule.Id)))
            {
                yield return new Finding("rules", Severity.Error, $"{rule.Id}: {problem.Message}", "rules", rule.Id);
            }
        }
    }

    private static IEnumerable<Finding> ComponentsValid(CheckContext c)
    {
        foreach (var doc in c.Design.Components)
        {
            var others = c.Design.Components.Where(d => d.Id != doc.Id);
            foreach (var problem in ComponentValidator.Validate(doc, others, rule => c.Design.Rule(rule) is not null, c.Toolkit.Facts))
            {
                yield return new Finding("components", Severity.Error, $"{doc.Name}: {problem.Message}", "components", doc.Id);
            }
        }
    }

    private static IEnumerable<Finding> PageIds(CheckContext c)
    {
        // what the layout and the section draw themselves, on every documentation page
        string[] own = ["main", "toc-dialog", "toc-dialog-title", "user-menu", "mat-nav", "nav-switch-menu"];
        foreach (var section in ComponentCatalog.Sections)
        {
            var ids = new List<(string Id, string Owner, string? Component)>(own.Select(i => (i, "the page", (string?)null)));
            foreach (var group in section.Groups)
            {
                ids.Add((ComponentCatalog.GroupAnchor(group), "the group " + group, null));
                ids.Add(("h-" + ComponentCatalog.GroupAnchor(group), "the group " + group, null));
            }

            foreach (var doc in c.Design.ComponentsIn(section))
            {
                ids.Add((doc.Id, doc.Name, doc.Id));
                ids.Add(("h-" + doc.Id, doc.Name, doc.Id));
                for (var i = 1; i <= doc.Examples.Count; i++)
                {
                    ids.Add(($"src-{doc.Id}-{i}", doc.Name, doc.Id));
                    ids.Add(($"ex-{doc.Id}-{i}", doc.Name, doc.Id));
                }

                foreach (var example in doc.Examples.Where(e => e.Live && e.Html.Length > 0))
                {
                    ids.AddRange(MarkupChecks.IdsIn(example.Html).Select(i => (i, doc.Name, (string?)doc.Id)));
                }
            }

            foreach (var twice in ids.GroupBy(i => i.Id).Where(g => g.Count() > 1))
            {
                var owners = twice.Select(i => i.Owner).Distinct().ToList();
                var component = twice.Select(i => i.Component).FirstOrDefault(i => i is not null);
                yield return new Finding("page-ids", Severity.Error, $"On the page {section.Title}, the id '{twice.Key}' is used {twice.Count()} times ({string.Join(", ", owners)}).", component is null ? null : "components", component);
            }
        }
    }

    private static IEnumerable<Finding> Groups(CheckContext c)
    {
        foreach (var group in ComponentCatalog.Groups)
        {
            if (!c.Design.ComponentsOf(group).Any())
            {
                yield return new Finding("groups", Severity.Error, $"The group {group} has no component.");
            }

            if (!ComponentCatalog.GroupSummaries.ContainsKey(group))
            {
                yield return new Finding("groups", Severity.Error, $"The group {group} has no summary line.");
            }
        }
    }

    private static IEnumerable<Finding> Patterns(CheckContext c)
    {
        foreach (var pattern in PatternCatalog.All)
        {
            foreach (var rule in pattern.Rules.Concat(pattern.Parts.Where(p => p.Rule is not null).Select(p => p.Rule!)).Distinct().Where(r => c.Design.Rule(r) is null))
            {
                yield return new Finding("patterns", Severity.Error, $"The pattern {pattern.Name} cites {rule}, which is not in the rulebook.");
            }
        }

        // the patterns are shown in the accent of five apps of the demo; a site without them shows them in its own apps
        foreach (var app in PatternCatalog.Apps.Where(a => c.Design.Template.OffersSampleApps && c.Design.App(a) is null))
        {
            yield return new Finding("patterns", Severity.Error, $"The patterns show the app {app}, which is not in the family.");
        }
    }

    private static IEnumerable<Finding> Tokens(CheckContext c)
    {
        foreach (var token in c.Toolkit.Tokens)
        {
            if (token.Description.Length == 0)
            {
                yield return new Finding("tokens", Severity.Error, $"{token.Name} has no comment that says what it is for.");
            }

            // --mat-picker-*: the white and the black of the colour picker's model are the same in light and dark on purpose
            if (token.IsColor && token.Dark is null && !token.Name.StartsWith("--mat-picker-", StringComparison.Ordinal) && !token.Light.StartsWith("color-mix", StringComparison.Ordinal) && !token.Light.StartsWith("var(", StringComparison.Ordinal))
            {
                yield return new Finding("tokens", Severity.Error, $"{token.Name} is a colour without a dark value.");
            }
        }
    }

    private static IEnumerable<Finding> Naming(CheckContext c)
    {
        foreach (var name in c.Toolkit.Facts.Classes.Where(n => !ToolkitClassName().IsMatch(n)).Order(StringComparer.Ordinal))
        {
            yield return new Finding("naming", Severity.Error, $"The class .{name} does not start with mat- and is not a state class.");
        }
    }

    private static IEnumerable<Finding> Menus(CheckContext c)
    {
        var sets = Navigation.For(c.Design.NavFacts, studio: true);
        foreach (var set in sets.Where(s => !s.IsValid))
        {
            yield return new Finding("menu", Severity.Error, $"The menu {set.Title} has a submenu that is more than one level deep, or a child without an address.");
        }

        foreach (var twice in sets.GroupBy(s => s.Id).Where(g => g.Count() > 1))
        {
            yield return new Finding("menu", Severity.Error, $"Two menus have the id {twice.Key}.");
        }

        foreach (var twice in sets.SelectMany(s => s.Pages).GroupBy(i => i.Href).Where(g => g.Count() > 1))
        {
            yield return new Finding("menu", Severity.Error, $"The address {twice.Key} is in the menus {twice.Count()} times.");
        }
    }

    private static IEnumerable<Finding> Volume(CheckContext c)
    {
        if (!c.Studio.Enabled)
        {
            yield break;
        }

        foreach (var app in c.Studio.Store.List("apps").Where(i => c.Design.StudioApps.All(a => !a.Id.Equals(i.Id, StringComparison.OrdinalIgnoreCase))))
        {
            yield return new Finding("volume", Severity.Error, $"The app {app.Id} on the volume is left out: it is not a valid app, or its id is not the name of its folder.", "apps", app.Id);
        }

        foreach (var rule in c.Studio.Store.List("rules").Where(i => c.Design.StudioRules.All(r => !r.Id.Equals(i.Id, StringComparison.OrdinalIgnoreCase))))
        {
            yield return new Finding("volume", Severity.Error, $"The rule {rule.Id} on the volume is left out: it is not a valid rule, or its id is not the name of its folder.", "rules", rule.Id);
        }

        foreach (var doc in c.Studio.Store.List("components").Where(i => c.Design.StudioComponents.All(d => !d.Id.Equals(i.Id, StringComparison.OrdinalIgnoreCase))))
        {
            yield return new Finding("volume", Severity.Error, $"The component {doc.Id} on the volume is left out: it is not valid, or breaks a check that keeps the page safe.", "components", doc.Id);
        }

        if (c.Studio.Store.List(Tenant.Kind).Any(i => i.Id == Tenant.ItemId) && c.Design.StudioTenant is null)
        {
            yield return new Finding("volume", Severity.Error, "The company on the volume is left out: it is not a valid company (a name that is empty or too long, a template that does not exist, an accent that is not a colour).", Tenant.Kind, Tenant.ItemId);
        }

        // the file of the setup assistant: a file that cannot be read leaves the site as the demo, which would turn it into another company's site
        string? fileProblem = null;
        try
        {
            Tenant.ReadFile(c.Studio.DataDirectory);
        }
        catch (InvalidDataException ex)
        {
            fileProblem = ex.Message;
        }

        if (fileProblem is not null)
        {
            yield return new Finding("volume", Severity.Error, fileProblem);
        }

        if (c.Studio.Store.List(Brand.Kind).Any(i => i.Id == Brand.ItemId) && c.Design.StudioBrand is null)
        {
            yield return new Finding("volume", Severity.Error, "The brand on the volume is left out: it is not a valid brand (a name that is empty or too long, a token name or a value that is not safe to write into a stylesheet).", Brand.Kind, Brand.ItemId);
        }
    }
}
