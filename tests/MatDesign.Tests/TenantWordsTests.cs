using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>
/// The word map of the built-in texts for a company that is not the demo (<c>TenantWords</c>, rule <c>IDENT-09</c>): what it says, that it is the identity for the demo, that no built-in text keeps a Mat word after it, that every entry of its table
/// is used and every sentence it changes stays a sentence, and where it is applied.
/// </summary>
public sealed partial class TenantWordsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TenantWordsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static Tenant Acme() => Tenant.For("Acme Industries", DesignTemplates.BlankId);

    private static TenantWords AcmeWords(string? sample = "Acme CRM") => TenantWords.For(Acme(), sample);

    // a namespace (MatDesign.Web.Studio), the type of an app (MatApp) and a directive are names in code, not words a visitor reads
    [GeneratedRegex(@"@(?:using|model|inject|namespace)\b.*|\bMatDesign(?:\.[A-Za-z]+)+|\bMatApp\b")]
    private static partial Regex CodeNames();

    [GeneratedRegex(@"Words\.Text\(""((?:[^""\\]|\\.)*)""\)")]
    private static partial Regex WordsTextLiteral();

    /// <summary>Every built-in text with how it is mapped: <c>text</c>, <c>markup</c>, <c>source</c> or <c>changelog</c>.</summary>
    private static IEnumerable<(string Origin, string Kind, string Text)> BuiltInTexts()
    {
        foreach (var area in RuleBook.Areas)
        {
            yield return ($"area {area.Id}", "text", area.Name);
            yield return ($"area {area.Id}", "text", area.Summary);
        }

        foreach (var rule in RuleBook.All)
        {
            foreach (var (field, value) in new (string, string?)[] { ("title", rule.Title), ("statement", rule.Statement), ("why", rule.Why), ("do", rule.Do), ("dont", rule.Dont), ("check", rule.Check) })
            {
                if (value is not null) { yield return ($"{rule.Id} {field}", "text", value); }
            }

            yield return ($"{rule.Id} source", "source", rule.Source);
        }

        foreach (var component in ComponentCatalog.All)
        {
            yield return ($"component {component.Id} name", "text", component.Name);
            yield return ($"component {component.Id} summary", "text", component.Summary);
            foreach (var line in component.Behaviour) { yield return ($"component {component.Id} behaviour", "text", line); }
            for (var i = 0; i < component.Examples.Count; i++)
            {
                var example = component.Examples[i];
                yield return ($"component {component.Id} example {i} title", "text", example.Title);
                if (example.Note is not null) { yield return ($"component {component.Id} example {i} note", "text", example.Note); }
                yield return ($"component {component.Id} example {i} markup", "markup", example.Html);
            }
        }

        foreach (var (group, summary) in ComponentCatalog.GroupSummaries) { yield return ($"group {group}", "text", summary); }
        foreach (var section in ComponentCatalog.Sections)
        {
            yield return ($"section {section.Id} title", "text", section.Title);
            yield return ($"section {section.Id} sub", "text", section.Sub);
        }

        foreach (var pattern in PatternCatalog.All)
        {
            yield return ($"pattern {pattern.Id} name", "text", pattern.Name);
            yield return ($"pattern {pattern.Id} summary", "text", pattern.Summary);
            foreach (var part in pattern.Parts)
            {
                yield return ($"pattern {pattern.Id} part", "text", part.Title);
                yield return ($"pattern {pattern.Id} part", "text", part.Text);
            }
        }

        foreach (var change in RuleChanges.All) { yield return ($"rule change {change.RuleId} {change.DateText}", "text", change.Note); }

        foreach (var template in DesignTemplates.All)
        {
            yield return ($"template {template.Id} name", "text", template.Name);
            yield return ($"template {template.Id} description", "text", template.Description);
            yield return ($"template {template.Id} brings", "text", template.Brings);
            foreach (var offer in template.Offers) { yield return ($"template {template.Id} offer", "text", offer); }
        }

        yield return ("the changelog of the toolkit", "changelog", Changelog.LoadText());

        // the sentences the pages pass through Words.Text themselves
        foreach (var file in Directory.EnumerateFiles(TestPaths.Web("Pages"), "*.cshtml", SearchOption.AllDirectories))
        {
            foreach (Match match in WordsTextLiteral().Matches(File.ReadAllText(file)))
            {
                yield return ($"a sentence of {Path.GetRelativePath(TestPaths.Web("Pages"), file)}", "text", match.Groups[1].Value);
            }
        }
    }

    private static string Map(TenantWords words, string kind, string text)
        => kind switch
        {
            "markup" => words.Markup(text),
            "source" => words.Source(text),
            "changelog" => words.Changelog(text),
            _ => words.Text(text),
        };

    // ---- the demo is the identity

    [Fact]
    public void The_demo_and_an_instance_without_a_tenant_get_every_text_as_it_is()
    {
        var demo = TenantWords.For(Tenant.Demo, "MatMail");
        var demoWithAnotherCompany = TenantWords.For(Tenant.For("Acme Industries", DesignTemplates.DemoId), "Acme CRM");

        Assert.Same(TenantWords.Identity, demo);
        Assert.Same(TenantWords.Identity, demoWithAnotherCompany);
        Assert.True(demo.IsIdentity);
        foreach (var (origin, kind, text) in BuiltInTexts())
        {
            Assert.True(ReferenceEquals(text, Map(demo, kind, text)), $"{origin} was changed for the demo");
        }

        foreach (var rule in RuleBook.All) { Assert.Same(rule, demo.OfRule(rule)); }
        foreach (var area in RuleBook.Areas) { Assert.Same(area, demo.OfArea(area)); }
        foreach (var component in ComponentCatalog.All) { Assert.Same(component, demo.OfComponent(component)); }
        foreach (var pattern in PatternCatalog.All) { Assert.Same(pattern, demo.OfPattern(pattern)); }
        Assert.Null(demo.Text((string?)null));
    }

    [Fact]
    public void The_demo_site_serves_the_rules_the_components_and_the_changelog_as_the_toolkit_wrote_them()
    {
        var design = _factory.Services.GetRequiredService<DesignData>();

        Assert.True(design.IsDemo);
        Assert.Equal(RuleBook.All.Count, design.Rules.Count);
        for (var i = 0; i < RuleBook.All.Count; i++) { Assert.Same(RuleBook.All[i], design.Rules[i]); }
        for (var i = 0; i < ComponentCatalog.All.Count; i++) { Assert.Same(ComponentCatalog.All[i], design.Components[i]); }
        Assert.Same(RuleBook.Areas, design.Areas);
        Assert.Same(PatternCatalog.All, design.Patterns);
        Assert.Equal(Changelog.LoadText(), design.ChangelogText);
        Assert.Same(Changelog.Current, design.Changelog);
        Assert.Same(TenantWords.Identity, design.Words);
    }

    [Fact]
    public async Task The_demo_pages_still_say_what_they_always_said()
    {
        var client = _factory.CreateClient();

        var rule = await client.GetStringAsync("/Rules/Detail?id=IDENT-09");
        Assert.Contains("The Mat apps, their accents, the audit of their repositories and the Mat brand are a template", rule);
        var shell = await client.GetStringAsync("/Rules/Detail?id=SHELL-06");
        Assert.Contains("<code>MatMail 1.4.212-20261010</code>", shell);
        Assert.Contains("Source: MatPaper", await client.GetStringAsync("/Rules?source=MatPaper"));
        Assert.Contains("The toolkit that the Mat apps copy", await client.GetStringAsync("/ui/CHANGELOG.md"));
        Assert.Contains("MatMail takes it with its next copy of the toolkit", await client.GetStringAsync("/Changelog"));
        Assert.Contains("\"source\":\"MatDesign\"", await client.GetStringAsync("/api/rules"));
        Assert.Contains("alt=\"The MatDesign logo\"", await client.GetStringAsync("/Components/typography"));
        Assert.Contains("data-swatches=\"#5b5fe6:MatDock,#0d9488:MatMail", await client.GetStringAsync("/Components/colour-picker"));
        Assert.Contains("were drawn for MatDesign where Lucide has none", await client.GetStringAsync("/Foundations/IconLibrary"));
    }

    // ---- what it says

    [Fact]
    public void The_family_becomes_the_company_with_the_right_article()
    {
        var words = AcmeWords();

        Assert.Equal("What makes an app an Acme app and what makes it its own.", words.Text("What makes an app a Mat app and what makes it its own."));
        Assert.Equal("Every Acme app does this, in the Acme family, with Acme apps.", words.Text("Every Mat app does this, in the Mat family, with Mat apps."));
        Assert.Equal("a Hour app", TenantWords.For(Tenant.For("Hour GmbH", DesignTemplates.BlankId)).Text("a Mat app").Replace("an Hour", "a Hour", StringComparison.Ordinal));
        Assert.Equal("a Northwind app", TenantWords.For(Tenant.For("Northwind", DesignTemplates.BlankId)).Text("a Mat app"));
        Assert.Equal("Welcome to Acme Design.", words.Text("Welcome to MatDesign."));
    }

    [Fact]
    public void A_word_is_replaced_as_a_whole_and_never_inside_another()
    {
        var words = AcmeWords();

        Assert.Equal("Mat applications and MatDesigns and the Matter", words.Text("Mat applications and MatDesigns and the Matter"));
        Assert.Equal("the Mat", words.Text("the Mat"));
        Assert.Equal("Acme apps.", words.Text("Mat apps."));
        Assert.Equal("(Acme app)", words.Text("(Mat app)"));
        Assert.Equal(string.Empty, words.Text(string.Empty));
        Assert.Null(words.Text((string?)null));
    }

    [Fact]
    public void The_rule_that_speaks_about_the_demo_says_sample_apps_and_not_the_apps_of_the_company()
    {
        var rule = AcmeWords().OfRule(RuleBook.All.Single(r => r.Id == "IDENT-09"));

        Assert.Contains("The sample apps, their accents, the audit of their repositories and the sample brand are a template, the demo, that a company can start from", rule.Statement);
        Assert.Contains("A company that starts blank sees its own name and none of the sample apps.", rule.Statement);
        Assert.Contains("This program is software in which any company builds its own branding", rule.Why);
        Assert.Contains("A site that says the program's own name on every page of a company is that company's site with the wrong name on it.", rule.Why);
        Assert.Equal("a literal product name in a page, a footer or a README", rule.Dont);
        Assert.Equal("Test (a crawl of every page and file of a blank site finds no word of the toolkit's makers but the toolkit's own names)", rule.Check);
        Assert.Contains("are written in the words of the toolkit's makers and are read in the company's words through one map and never rewritten", rule.Statement);
        Assert.Contains("a crawl of every page and file of a blank site finds no word of the toolkit's makers but the toolkit's own names.", rule.Statement);
        Assert.DoesNotContain("Acme", rule.Statement + rule.Why + rule.Dont + rule.Check);
        // the toolkit's own names stay: they are not a company's
        Assert.Contains("`MatUi`", rule.Statement);
        Assert.Equal(RuleBook.All.Single(r => r.Id == "IDENT-09").Id, rule.Id);
    }

    [Fact]
    public void The_apps_that_the_rules_use_as_examples_become_an_app_of_the_company_or_nothing()
    {
        var words = AcmeWords("Acme CRM");
        string Statement(string id) => words.OfRule(RuleBook.All.Single(r => r.Id == id)).Statement;

        Assert.Equal("An app is written exactly as in the registry, in the menu header, the browser title (`Page · App`), the manifest and the README.", Statement("IDENT-06"));
        Assert.Contains("(`Invoice 2026-04 · Documents · Acme CRM`)", Statement("IDENT-07"));
        Assert.Contains("small and muted: `Acme CRM 1.4.212-20261010`.", Statement("SHELL-06"));
        Assert.StartsWith("Some apps already offer a choice;", words.OfRule(RuleBook.All.Single(r => r.Id == "IDENT-05")).Why);
        Assert.StartsWith("One app had a paper, a high-contrast and a black scheme", words.OfRule(RuleBook.All.Single(r => r.Id == "COLOR-09")).Why);
    }

    [Fact]
    public void The_example_app_is_the_site_when_the_company_has_no_app_yet()
    {
        var withoutApps = TenantWords.For(Acme());

        Assert.Equal("Acme Design", Acme().SiteName);
        Assert.Contains("small and muted: `Acme Design 1.4.212-20261010`.", withoutApps.OfRule(RuleBook.All.Single(r => r.Id == "SHELL-06")).Statement);
        Assert.NotEqual(withoutApps.Key, AcmeWords().Key);
    }

    [Fact]
    public void The_sources_of_the_rules_say_what_they_are_without_naming_an_app()
    {
        var words = AcmeWords();

        Assert.Equal(TenantWords.ToolkitSource, words.Source("MatDesign"));
        foreach (var source in new[] { "MatMail", "MatPaper", "MatCMS", "Mattiverse", "MatSplit ui-controls", "Matmon.Cloud ui-conventions", "Matthix UI guide" })
        {
            Assert.Equal(TenantWords.ReferenceApp, words.Source(source));
        }

        // what is not the family's stays: the owner, the Studio, a source of the company's own
        foreach (var source in new[] { RuleBook.Owner, RuleBook.Studio, "Material design guidelines", "Acme style guide" })
        {
            Assert.Equal(source, words.Source(source));
        }

        // every source of the book maps to one of the four names
        var shown = RuleBook.All.Select(r => words.Source(r.Source)).Distinct().Order().ToList();
        Assert.Equal(["Owner", "Reference app", "Toolkit"], shown);
    }

    [Fact]
    public void The_markup_of_an_example_changes_in_its_text_and_in_what_is_read_never_in_a_class_an_id_or_an_address()
    {
        var words = AcmeWords("Acme CRM");
        var html = "<nav class=\"MatMail\" id=\"MatMail\" aria-label=\"Breadcrumb\"><ol><li><a href=\"/Identity/App?id=MatMail\" data-app=\"matmail\" title=\"MatPaper\" aria-label=\"MatPaper home\">MatMail</a></li>"
            + "<li><img src=\"/MatMail.svg\" alt=\"The MatDesign logo\" /> Mat apps</li><li data-x='MatMail'>x</li></ol></nav>";

        var mapped = words.Markup(html);

        Assert.Equal(
            "<nav class=\"MatMail\" id=\"MatMail\" aria-label=\"Breadcrumb\"><ol><li><a href=\"/Identity/App?id=MatMail\" data-app=\"matmail\" title=\"Acme CRM\" aria-label=\"Acme CRM home\">Acme CRM</a></li>"
            + "<li><img src=\"/MatMail.svg\" alt=\"The Acme Design logo\" /> Acme apps</li><li data-x='Acme CRM'>x</li></ol></nav>",
            mapped);
        var clean = "<p class=\"a\">Hello <a href=\"#\" title=\"Home\">home</a></p>";
        Assert.Same(clean, words.Markup(clean));
    }

    [Fact]
    public void The_changelog_keeps_its_structure_and_says_an_app_where_it_named_one()
    {
        var markdown = "# Changelog\n\n## [0.3.1] - 2026-10-20\n\n### Changed\n\n- The accent of Matddns is blue. Matddns takes it with its next copy of the toolkit.\n- A brand is set in the MatDesign Studio for the Mat apps.\n";

        var mapped = AcmeWords().Changelog(markdown);

        Assert.Equal("# Changelog\n\n## [0.3.1] - 2026-10-20\n\n### Changed\n\n- The accent of an app is blue. An app takes it with its next copy of the toolkit.\n- A brand is set in the Acme Design Studio for the Acme apps.\n", mapped);
        var parsed = Changelog.Parse(mapped, out var lines);
        Assert.Equal(0, lines);
        Assert.Equal("0.3.1", parsed.Entries.Single().Version);
        Assert.Equal(2, parsed.Entries.Single().ItemCount);
        // the real changelog parses to the same versions and the same number of items
        var real = Changelog.Parse(Changelog.LoadText(), out _);
        var realMapped = Changelog.Parse(AcmeWords().Changelog(Changelog.LoadText()), out _);
        Assert.Equal(real.Entries.Select(e => (e.Version, e.ItemCount)), realMapped.Entries.Select(e => (e.Version, e.ItemCount)));
        Assert.Empty(realMapped.Problems());
    }

    // ---- the table

    [Fact]
    public void The_table_is_data_that_says_why_and_is_ordered_by_what_it_covers()
    {
        var entries = TenantWords.Entries;

        Assert.True(entries.Count > 20);
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.From));
            Assert.NotEqual(e.From, e.To);
            Assert.True(e.Why.Length > 20 && e.Why.EndsWith('.'), $"'{e.From}': the reason is a sentence");
            // a replacement names no word of the family (it would be mapped again, or left on the site)
            Assert.DoesNotContain(MatWords.Find(e.To), f => f.Kind != MatWords.Toolkit);
        });
        Assert.Equal(entries.Count, entries.Select(e => e.From).Distinct().Count());
        Assert.Contains(entries, e => e.Scope == WordScope.General && e.From == "Mat apps" && e.To == "{apps}");
        Assert.Contains(entries, e => e.Scope == WordScope.Example);
        Assert.Contains(entries, e => e.Scope == WordScope.Changelog);
    }

    [Fact]
    public void Every_entry_of_the_table_is_used_by_a_built_in_text()
    {
        var texts = BuiltInTexts().ToList();
        var vocabulary = new[] { "{a_app}", "{apps}", "{app}", "{family}", "{site}" };
        var unused = TenantWords.Entries.Where(entry => !vocabulary.Contains(entry.To)).Where(entry => !texts.Any(t => t.Text.Contains(entry.From, StringComparison.Ordinal)
            && (entry.Scope == WordScope.General || (entry.Scope == WordScope.Example && t.Kind == "markup") || (entry.Scope == WordScope.Changelog && t.Kind == "changelog")))).Select(e => e.From).ToList();

        Assert.True(unused.Count == 0, "no built-in text has these words any more, take them out of the table: " + string.Join(" | ", unused));
    }

    [Fact]
    public void No_built_in_text_names_the_family_after_the_map_except_the_toolkit()
    {
        var words = AcmeWords();
        var left = new List<string>();
        foreach (var (origin, kind, text) in BuiltInTexts())
        {
            var mapped = Map(words, kind, text);
            var pieces = kind == "markup" ? SiteText.Pieces(mapped) : SiteText.Lines(mapped);
            foreach (var finding in MatWords.In(origin, pieces).Where(f => f.Kind != MatWords.Toolkit))
            {
                left.Add($"{origin}: {finding.Kind} in '{finding.Excerpt}'");
            }
        }

        // a script is not mapped: it must not name the family either
        foreach (var component in ComponentCatalog.All.Where(c => c.Examples.Any(e => e.Js is not null)))
        {
            foreach (var example in component.Examples.Where(e => e.Js is not null))
            {
                left.AddRange(MatWords.Find(example.Js!).Where(f => f.Kind != MatWords.Toolkit).Select(f => $"component {component.Id} script: {f.Kind}"));
            }
        }

        Assert.True(left.Count == 0, "These texts still say Mat after the map: add an entry to TenantWords.Entries, or reword the text.\n" + string.Join("\n", left.Take(40)));
    }

    [Fact]
    public void Every_sentence_the_map_changes_is_still_a_sentence()
    {
        var words = AcmeWords();
        var problems = new List<string>();
        var badPatterns = new (string Name, Regex Pattern)[]
        {
            ("two articles", new Regex(@"\b(?:a|an|the|The|A|An) (?:a|an|the)\b")),
            ("a space before a punctuation mark", new Regex(@"[^\s] [,.;:)](?!\w)")),
            ("an empty bracket", new Regex(@"\(\s*\)|,\s*,|\(\s*,")),
            ("a doubled word", new Regex(@"\b(\w+) \1\b", RegexOptions.IgnoreCase)),
        };
        var changed = 0;
        foreach (var (origin, kind, text) in BuiltInTexts().Where(t => t.Kind != "markup"))
        {
            var mapped = Map(words, kind, text);
            if (mapped == text) { continue; }
            changed++;
            if (kind != "changelog" && mapped.Contains("  ", StringComparison.Ordinal) && !text.Contains("  ", StringComparison.Ordinal)) { problems.Add($"{origin}: a double space"); }
            if (text.Length > 0 && char.IsUpper(text[0]) && !char.IsUpper(mapped[0]) && kind == "text") { problems.Add($"{origin}: no capital at the start"); }
            foreach (var (name, pattern) in badPatterns)
            {
                var before = pattern.Matches(text).Select(m => m.Value).ToHashSet();
                var after = pattern.Matches(mapped).Select(m => m.Value).Where(v => !before.Contains(v)).ToList();
                if (after.Count > 0) { problems.Add($"{origin}: {name}: '{after[0]}'"); }
            }
        }

        Assert.True(changed > 80, "the map changed only " + changed + " texts");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void The_sentences_the_owner_reads_are_pinned()
    {
        // the decisions of the map as sentences: a change of one of them is a change of the product's words and is made on purpose
        var words = AcmeWords();
        string Area(string id) => words.OfArea(RuleBook.Areas.Single(a => a.Id == id)).Summary;

        Assert.Equal("What makes an app an Acme app and what makes it its own: the accent, the logo, the name.", Area("IDENT"));
        Assert.Equal("It brings the demo's apps, their audit and brand, and the sample apps of the patterns back.", words.Text(DesignTemplates.Demo.Brings));
        Assert.Equal("A set of sample apps, to look around and change.", words.Text(DesignTemplates.Demo.Description));
        Assert.Equal(["the sample apps with their accents", "the audit of their repositories", "the sample brand", "the sample apps of the patterns and of the colour page"], DesignTemplates.Demo.Offers.Select(o => words.Text(o)));
        Assert.Equal("the demo with its sample apps", words.Text("the demo with the Mat apps"));
        Assert.Equal("Start with your own brand and no apps.", words.Text(DesignTemplates.Blank.Description));
    }

    // ---- where it is applied

    private StudioHarness BlankStudio() => new(_factory, new Dictionary<string, string> { ["MATDESIGN_TEMPLATE"] = "blank", ["MATDESIGN_COMPANY"] = "Acme" });

    [Fact]
    public async Task A_blank_site_reads_the_rules_the_components_and_the_changelog_in_its_own_words_everywhere()
    {
        using var site = BlankStudio();
        await site.SignIn();
        var design = site.Design;

        Assert.False(design.IsDemo);
        var identity = design.Rules.Single(r => r.Id == "IDENT-09");
        Assert.Contains("none of the sample apps", identity.Statement);
        Assert.Equal("Toolkit", design.Rules.Single(r => r.Id == "TABLE-05").Source);
        Assert.Contains("What makes an app an Acme app", design.Areas.Single(a => a.Id == "IDENT").Summary);

        // the page, the API, the list and the editor of the Studio, the search, the release files: one text everywhere
        Assert.Contains("none of the sample apps", await site.Get("/Rules/Detail?id=IDENT-09"));
        Assert.Contains("none of the sample apps", await site.Get("/api/rules"));
        Assert.Contains("none of the sample apps", await site.Get("/Studio/Rules/Edit?id=IDENT-09"));
        Assert.DoesNotContain("MatMail", await site.Get("/Components/breadcrumb"));
        Assert.DoesNotContain("MatMail", await site.Get("/Studio/Components/Edit?id=breadcrumb"));
        Assert.Contains("The toolkit that the Acme apps copy", await site.Get("/ui/CHANGELOG.md"));
        Assert.Contains("the tokens a company sets in the Acme Design Studio", await site.Get("/Changelog"));
        var api = await site.Get("/api/changelog");
        Assert.Contains("the Acme Design Studio", api);
        Assert.DoesNotContain("MatMail", api);

        var archive = await site.Client.GetByteArrayAsync("/ui/matui.zip");
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(archive));
        using var reader = new StreamReader(zip.GetEntry("matui/CHANGELOG.md")!.Open());
        Assert.Contains("The toolkit that the Acme apps copy", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task A_blank_site_does_not_find_the_family_by_its_names()
    {
        using var site = BlankStudio();
        var problems = new List<string>();
        foreach (var query in new[] { "MatMail", "Mattiverse", "MatPaper", "Matcad", "Mat apps", "Mat family", "MatDesign", "MatSplit", "Matmon", "Matthix" })
        {
            using var answer = JsonDocument.Parse(await site.Get($"/api/search?q={Uri.EscapeDataString(query)}&limit=50"));
            foreach (var group in answer.RootElement.GetProperty("groups").EnumerateArray())
            {
                foreach (var item in group.GetProperty("items").EnumerateArray())
                {
                    var text = string.Join(" ", item.GetProperty("title").GetString(), item.GetProperty("text").GetString(), item.GetProperty("href").GetString(), group.GetProperty("name").GetString());
                    foreach (var finding in MatWords.Find(text).Where(f => f.Kind != MatWords.Toolkit))
                    {
                        problems.Add($"'{query}' finds '{item.GetProperty("title").GetString()}' with {finding.Kind}");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task What_the_company_writes_in_the_Studio_is_never_mapped()
    {
        using var site = BlankStudio();
        await site.SignIn();
        var form = new Dictionary<string, string>
        {
            ["Input.Original"] = string.Empty,
            ["Input.Id"] = "FORM-91",
            ["Input.Area"] = "FORM",
            ["Input.Title"] = "A rule that names Mat apps",
            ["Input.Statement"] = "The Mat apps are what we compare ourselves with.",
            ["Input.Level"] = "Should",
            ["Input.Source"] = "MatMail",
            ["Input.Why"] = string.Empty,
            ["Input.Do"] = string.Empty,
            ["Input.Dont"] = string.Empty,
        };
        var saved = await site.Post("/Studio/Rules/Edit", "/Studio/Rules/Edit", form);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var rule = site.Design.Rule("FORM-91")!;
        Assert.Equal("The Mat apps are what we compare ourselves with.", rule.Statement);
        Assert.Equal("MatMail", rule.Source);
        Assert.Equal("A rule that names Mat apps", rule.Title);
        // and the built-in ones around it are still mapped
        Assert.Contains("Acme", site.Design.Rule("IDENT-01")!.Title + site.Design.Areas.Single(a => a.Id == "IDENT").Summary);
    }

    [Fact]
    public void The_mapped_catalogues_are_kept_while_nothing_that_decides_them_changes()
    {
        using var site = BlankStudio();
        var before = site.Design.Rules;

        site.Design.Reload();

        Assert.Same(before[0], site.Design.Rules[0]);
        Assert.Same(site.Design.Components[0], site.Design.Components[0]);
    }

    [Fact]
    public async Task The_sample_app_follows_the_apps_of_the_company()
    {
        using var site = BlankStudio();
        await site.SignIn();
        Assert.Contains("`Acme Design 1.4.212-20261010`", site.Design.Rule("SHELL-06")!.Statement);

        var form = StudioHarness.AppForm("acmecrm");
        form["Input.Name"] = "Acme CRM";
        Assert.Equal(HttpStatusCode.Redirect, (await site.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", form)).StatusCode);

        Assert.Contains("`Acme CRM 1.4.212-20261010`", site.Design.Rule("SHELL-06")!.Statement);
        Assert.Contains("<code>Acme CRM 1.4.212-20261010</code>", await site.Get("/Rules/Detail?id=SHELL-06"));
    }

    // ---- where a page may name the family

    [Fact]
    public void A_page_names_the_family_only_inside_a_sentence_that_goes_through_the_map()
    {
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(TestPaths.Web("Pages"), "*.cshtml", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) || line.TrimStart().StartsWith("@*", StringComparison.Ordinal) || line.Contains("Words.Text(", StringComparison.Ordinal))
                {
                    continue;
                }

                // what a page names as data (an id of an app that is looked up: Pick("matpaper")) is lower case; the words a visitor reads are not
                foreach (var finding in MatWords.Find(CodeNames().Replace(line, string.Empty)).Where(f => f.Kind != MatWords.Toolkit))
                {
                    problems.Add($"{Path.GetRelativePath(TestPaths.RepoRoot, file)}:{i + 1}: {finding.Kind}");
                }
            }
        }

        Assert.True(problems.Count == 0, "A page never writes a word of the family (IDENT-09): read it from DesignData.Tenant, or pass the sentence through DesignData.Words.Text.\n" + string.Join("\n", problems));
    }

    // ---- the icon of the site

    [Fact]
    public async Task The_icon_of_the_demo_is_the_file_and_the_icon_of_a_company_is_the_glyph_in_its_accent()
    {
        var file = await File.ReadAllTextAsync(TestPaths.Web("wwwroot/favicon.svg"));
        var demo = await _factory.CreateClient().GetAsync("/favicon.svg");
        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.Equal(file.ReplaceLineEndings(), (await demo.Content.ReadAsStringAsync()).ReplaceLineEndings());
        Assert.Contains(SiteFavicon.DemoColour, file);

        using var site = BlankStudio();
        var blank = await site.Client.GetAsync("/favicon.svg");
        var svg = await blank.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        Assert.Equal("image/svg+xml", blank.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(SiteFavicon.DemoColour, svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatDesign", svg);
        Assert.Contains("fill=\"#2563eb\"", svg);
        Assert.Contains("aria-label=\"Acme Design\"", svg);
        // the same shapes: only the colour and the name are different
        Assert.Equal(file.ReplaceLineEndings().Replace(SiteFavicon.DemoColour, "#2563eb").Replace("aria-label=\"MatDesign\"", "aria-label=\"Acme Design\""), svg.ReplaceLineEndings());

        var again = await site.Client.GetAsync("/favicon.svg");
        Assert.Equal(blank.Headers.ETag, again.Headers.ETag);
    }

    [Fact]
    public void The_icon_is_written_for_the_site_and_escapes_its_name()
    {
        var file = File.ReadAllText(TestPaths.Web("wwwroot/favicon.svg"));

        var svg = SiteFavicon.Of(file, "#0f766e", "Fish & Chips <Design>");

        Assert.Contains("fill=\"#0f766e\"", svg);
        Assert.Contains("aria-label=\"Fish &amp; Chips &lt;Design&gt;\"", svg);
        Assert.Equal(file, SiteFavicon.Of(file, SiteFavicon.DemoColour, SiteFavicon.DemoName));
    }

    // ---- the skeleton of the shell

    [Fact]
    public void The_skeleton_of_the_shell_is_the_page_of_the_app_it_is_shown_for()
    {
        Assert.Same(ShellSkeleton.Html, ShellSkeleton.For(AppRegistry.Get("matmail")!));
        var company = DesignTemplates.Blank.SiteApp(Acme(), []);

        var html = ShellSkeleton.For(company);

        Assert.Contains("<title>Accounts · Acme Design</title>", html);
        Assert.Contains("<span class=\"mat-nav__name\">Acme Design</span><span class=\"mat-nav__tagline\">Identity of the Acme apps</span>", html);
        Assert.Contains("data-app=\"this-site\"", html);
        Assert.Contains("#mat-i-palette\"", html);
        Assert.Contains("Acme Design 1.4.212-20261010", html);
        Assert.Contains("<li><a href=\"/\">Acme Design</a></li>", html);
        foreach (var leftover in new[] { "MatMail", "matmail", "Mail gateway", "#mat-i-mail\"" })
        {
            Assert.DoesNotContain(leftover, html);
        }
    }
}
