using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using MatDesign.Core.Studio;

namespace MatDesign.Core;

/// <summary>Where a word of <see cref="TenantWords.Entries"/> is used.</summary>
public enum WordScope
{
    /// <summary>Every text that is data: the statements of the rules, the notes of the components, the template cards, the changelog.</summary>
    General,

    /// <summary>The markup of the examples of a component only (a breadcrumb that names an app, the labels of the swatches of a picker).</summary>
    Example,

    /// <summary>The changelog of the toolkit only: its sentences say which app of the family needed a change; a company's site says "an app".</summary>
    Changelog,
}

/// <summary>One thing the word map says differently for a company that is not the demo: the words as the built-in data have them, what a company reads instead, and why.</summary>
/// <param name="Scope">Where it applies.</param>
/// <param name="From">The words as the built-in text has them, case sensitive, replaced as a whole (a letter next to the first or last character stops the match).</param>
/// <param name="To">What is read instead: with the tokens <c>{apps}</c> (Acme apps), <c>{app}</c> (Acme app), <c>{a_app}</c> (an Acme app), <c>{family}</c>, <c>{site}</c> (the site name), <c>{company}</c> and <c>{sample}</c> (the name of an app of the company, or of the site when it has none).</param>
/// <param name="Why">Why this is the reading: the sentence is about the Mat family, not about the company that runs the site.</param>
public sealed record WordEntry(WordScope Scope, string From, string To, string Why);

/// <summary>
/// The words of the built-in texts, for a company that is not the demo (docs/STUDIO.md, "Company and templates"; rule <c>IDENT-09</c>). The rules, the documentation of the components, the patterns and the changelog of the toolkit
/// are the toolkit's own data: they are not edited per company, and the text of a rule is what its hash in the rulebook changelog covers. They were written in the words of the Mat family ("Mat apps", "MatMail"), and a visitor of
/// another company's site must not read them. This map is applied at one place per kind of text (<c>DesignData</c> for the rules, the areas, the components and the patterns; the changelog and the template cards where they are served) and
/// is the identity for the demo: <see cref="Identity"/> returns every text as it is, the same reference. It never changes code (the class names, an address, a script): <see cref="Markup"/> only touches the text of an element and
/// the attributes that are read.
/// A word that is not in <see cref="Entries"/> is left alone, and a test lists every built-in text that still has a Mat word after the map (the data is what the owner decides about, never a guess of the map): the one fallback is the
/// changelog, a history that names apps in passing, where an app of the family becomes "an app".
/// </summary>
public sealed partial class TenantWords
{
    /// <summary>The name a company reads for the source of a rule that came from the documentation of an app of the family.</summary>
    public const string ReferenceApp = "Reference app";

    /// <summary>The name a company reads for the source of a rule that was decided for the toolkit itself.</summary>
    public const string ToolkitSource = "Toolkit";

    /// <summary>The family's own names that are not an app of the registry: a source of a rule, a product of the owner.</summary>
    private static readonly string[] OtherFamilyNames = ["Matthix"];

    /// <summary>Every name of the family: the apps of the registry (MatDesign among them) and the few others.</summary>
    public static IReadOnlyList<string> FamilyNames { get; } = AppRegistry.All.Select(a => a.Name).Concat(OtherFamilyNames).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The map, in the order it is written; the longest words are tried first at each place of a text.</summary>
    public static IReadOnlyList<WordEntry> Entries { get; } =
    [
        // ---- the words of the family: the apps, the app, the family, the product
        new(WordScope.General, "a Mat app", "{a_app}", "The article agrees with the new word (an Acme app)."),
        new(WordScope.General, "Mat apps", "{apps}", "The apps of the family are the apps of the company."),
        new(WordScope.General, "Mat app", "{app}", "An app of the family is an app of the company."),
        new(WordScope.General, "Mat family", "{family}", "The family is the company's."),
        new(WordScope.General, "MatDesign", "{site}", "The product is the site of the company (its name is the site name)."),

        new(WordScope.General, "Drawn for MatDesign", "Drawn for the toolkit", "The dialog of an icon of the icon library, where the origin of the icon is named (the sentence of the page says the same)."),
        new(WordScope.General, "were drawn for MatDesign where Lucide has none", "were drawn for the toolkit where Lucide has none", "The page of the icon library: the icons that are not Lucide's were drawn for the toolkit, which is the same for every company."),

        // ---- the rule IDENT-09 speaks about the demo itself: the demo's apps are sample apps for a company, never its own
        new(WordScope.General, "The Mat apps, their accents, the audit of their repositories and the Mat brand", "The sample apps, their accents, the audit of their repositories and the sample brand", "IDENT-09 says what the demo is made of: for a company that is not the demo these are sample data, not its apps."),
        new(WordScope.General, "written in the words of the Mat family", "written in the words of the toolkit's makers", "IDENT-09: the toolkit's own texts are in the words of the Mat family, which for a company are the words of the toolkit's makers, not its own."),
        new(WordScope.General, "finds no word of the Mat family but", "finds no word of the toolkit's makers but", "IDENT-09 and its check: the crawl of a blank site looks for the words of the toolkit's makers."),
        new(WordScope.General, "none of the Mat apps", "none of the sample apps", "IDENT-09 and its check: a blank start shows none of the sample apps."),
        new(WordScope.General, "MatDesign is software in which", "This program is software in which", "IDENT-09 (why): the product, not the company's site."),
        new(WordScope.General, "A site that says Mat on every page of another company", "A site that says the program's own name on every page of a company", "IDENT-09 (why): the same sentence without naming the maker."),
        new(WordScope.General, "a literal MatDesign in a page", "a literal product name in a page", "IDENT-09 (don't): the same warning without the maker's name."),
        new(WordScope.General, "with MatDesign (docs/PRODUCT.md", "with this program (docs/PRODUCT.md", "COLOR-11 and CODE-09 (why): the product, not the company's site."),

        // ---- the examples of the rules: an app of the registry used as an example
        new(WordScope.General, " (MatMail, Matcad, Matddns ...)", string.Empty, "IDENT-06: the names of three apps of the registry as an example; without them the sentence says the same."),
        new(WordScope.General, "MatPaper, Matdo and MatMail already offer a choice", "Some apps already offer a choice", "IDENT-05 (why): three apps of the family that had a choice of accents."),
        new(WordScope.General, "MatPaper had a paper, a high-contrast and a black scheme before it moved onto the toolkit", "One app had a paper, a high-contrast and a black scheme before it moved onto the toolkit", "COLOR-09 (why): one app of the family that had colour schemes."),
        new(WordScope.General, "`Invoice 2026-04 · Documents · MatPaper`", "`Invoice 2026-04 · Documents · {sample}`", "IDENT-07: the title of a page, with an app as the last part."),
        new(WordScope.General, "`MatMail 1.4.212-20261010`", "`{sample} 1.4.212-20261010`", "SHELL-06: the name and the version of an app."),

        // ---- the template cards of the setup assistant and of the Company page: what the demo is, read by a company that is not the demo
        new(WordScope.General, "It brings the Mat apps, the audit, the Mat brand and the sample apps back.", "It brings the demo's apps, their audit and brand, and the sample apps of the patterns back.", "The question of the Company page when a company switches to the demo."),
        new(WordScope.General, "The Mat apps as a sample", "A set of sample apps", "The card of the demo template."),
        new(WordScope.General, "the Mat apps with their accents", "the sample apps with their accents", "What the demo template offers."),
        new(WordScope.General, "the demo with the Mat apps", "the demo with its sample apps", "The overview of the Studio, on the Company card."),
        new(WordScope.General, "the Mat brand", "the sample brand", "What the demo template offers."),

        // ---- the examples of the components: an app of the registry as the name in a breadcrumb or a footer, the names of the swatches of a picker
        new(WordScope.Example, "#5b5fe6:MatDock,#0d9488:MatMail,#c2410c:MatFile,#7c3aed:MatPaper,#0284c7:Matmon", "#5b5fe6:Indigo,#0d9488:Teal,#c2410c:Orange,#7c3aed:Violet,#0284c7:Blue", "The swatches of the colour picker are named after the accents of five apps; a colour is named by its colour."),
        new(WordScope.Example, "MatMail", "{sample}", "The first crumb of a breadcrumb, the name in a footer: the app the page belongs to."),
        new(WordScope.Example, "MatPaper", "{sample}", "The first crumb of a breadcrumb: the app the page belongs to."),

        // ---- the changelog of the toolkit: the sentences that name an app of the family
        new(WordScope.Changelog, "a new app, MatIdentity (the single sign-on of the family)", "a new app of the family (the single sign-on)", "The changelog names the app that needed the new colour."),
        new(WordScope.Changelog, "a new app, MatSMTP (the mail relay of the family)", "a new app of the family (the mail relay)", "The changelog names the app that needed the new colour."),
        new(WordScope.Changelog, "the glyph of the MatMail logo", "the glyph of an app's logo", "The changelog names the app whose logo needed the new icon; with the fallback alone the sentence would have two articles."),
        new(WordScope.Changelog, "the glyph of the MatPaper logo", "the glyph of an app's logo", "The same sentence for another app."),
        new(WordScope.Changelog, "the glyph of the MatTrakr logo", "the glyph of an app's logo", "The same sentence for another app."),
        new(WordScope.Changelog, "like `whale` for MatDock", "like `whale` for another app", "The example of an earlier app that has its own glyph."),
    ];

    /// <summary>The sources of a rule that name the documentation an app of the family was written from, and what a company reads instead.</summary>
    private static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MatDesign"] = ToolkitSource,
    };

    private static readonly Regex GeneralPattern = Build(WordScope.General);
    private static readonly Regex MarkupPattern = Build(WordScope.General, WordScope.Example);
    private static readonly Regex ChangelogPattern = Build(WordScope.General, WordScope.Changelog);

    private static readonly Regex FamilyName = new(@"(?<![A-Za-z0-9])(?:" + string.Join("|", FamilyNames.Select(Regex.Escape).OrderByDescending(n => n.Length)) + @")(?![A-Za-z0-9])", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"<!--.*?-->|<(?<end>/?)(?<name>[A-Za-z][A-Za-z0-9:-]*)(?<attributes>(?:""[^""]*""|'[^']*'|[^>""'])*)>", RegexOptions.Singleline)]
    private static partial Regex Tag();

    [GeneratedRegex(@"(?<name>[^\s=/>""']+)(?:\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'))")]
    private static partial Regex QuotedAttribute();

    /// <summary>Attributes whose value is a name, an address or a layout and never a text that is read: the map leaves them alone.</summary>
    private static readonly HashSet<string> NotRead =
    [
        "class", "id", "for", "name", "type", "rel", "href", "src", "action", "method", "role", "form", "list", "target", "d", "viewbox", "xmlns", "points", "tabindex", "autocomplete", "inputmode",
        "aria-controls", "aria-labelledby", "aria-describedby", "aria-owns", "aria-activedescendant", "style",
    ];

    private readonly Tenant? _tenant;
    private readonly string _sample;

    private TenantWords(Tenant? tenant, string sample)
    {
        _tenant = tenant;
        _sample = sample;
    }

    /// <summary>The map for the demo and for an instance without a tenant: every text is returned as it is.</summary>
    public static TenantWords Identity { get; } = new(null, string.Empty);

    /// <summary>
    /// The map for a tenant. <paramref name="sampleApp"/> is the name an example uses where the built-in text names one app of the family: an app of the company, or its site when it has none. The demo is always
    /// <see cref="Identity"/>, whatever the company is called.
    /// </summary>
    public static TenantWords For(Tenant tenant, string? sampleApp = null)
        => tenant.IsDemo ? Identity : new TenantWords(tenant, string.IsNullOrWhiteSpace(sampleApp) ? tenant.SiteName : sampleApp.Trim());

    /// <summary>True for the demo: nothing is changed.</summary>
    public bool IsIdentity => _tenant is null;

    /// <summary>What decides the result: two maps with the same key map every text the same. Used to keep the mapped catalogues while nothing that matters changed.</summary>
    public string Key => _tenant is null ? string.Empty : string.Join('\u001f', _tenant.ShortName, _tenant.SiteName, _tenant.Company, _sample);

    /// <summary>A sentence, a title, a note: the words of the family become the company's. Null stays null.</summary>
    [return: NotNullIfNotNull(nameof(text))]
    public string? Text(string? text) => text is null || _tenant is null || text.Length == 0 ? text : Apply(GeneralPattern, text);

    /// <summary>The same for a list of texts.</summary>
    public IReadOnlyList<string> Texts(IReadOnlyList<string> texts) => _tenant is null ? texts : texts.Select(t => Text(t)!).ToList();

    /// <summary>
    /// The markup of an example: the text of its elements and the attributes that are read (a label, a title, an alt text, a placeholder, the swatches of a picker), never a class, an id or an address. A markup without a word of the
    /// family is returned as it is.
    /// </summary>
    public string Markup(string html)
    {
        if (_tenant is null || html.Length == 0 || !MayHaveWords(html))
        {
            return html;
        }

        var result = new StringBuilder(html.Length);
        var last = 0;
        foreach (Match tag in Tag().Matches(html))
        {
            result.Append(Apply(MarkupPattern, html[last..tag.Index]));
            last = tag.Index + tag.Length;
            if (!tag.Groups["name"].Success)
            {
                result.Append(tag.Value);
                continue;
            }

            var attributes = tag.Groups["attributes"].Value;
            result.Append('<').Append(tag.Groups["end"].Value).Append(tag.Groups["name"].Value)
                .Append(MayHaveWords(attributes) ? QuotedAttribute().Replace(attributes, a => Attribute(a)) : attributes)
                .Append('>');
        }

        result.Append(Apply(MarkupPattern, html[last..]));
        return result.ToString();
    }

    private string Attribute(Match attribute)
    {
        var name = attribute.Groups["name"].Value;
        if (NotRead.Contains(name.ToLowerInvariant()))
        {
            return attribute.Value;
        }

        var quote = attribute.Groups["double"].Success ? '"' : '\'';
        var value = attribute.Groups["double"].Success ? attribute.Groups["double"].Value : attribute.Groups["single"].Value;
        return $"{name}={quote}{Apply(MarkupPattern, value)}{quote}";
    }

    /// <summary>A quick test before the markup is taken apart: the words that can change are all in this list.</summary>
    private static bool MayHaveWords(string text) => text.Contains("Mat", StringComparison.Ordinal);

    /// <summary>The source of a rule: the documentation of an app of the family becomes a reference, the toolkit's own decisions the toolkit's. Owner and Studio are what they are.</summary>
    public string Source(string source)
    {
        if (_tenant is null)
        {
            return source;
        }

        if (Sources.TryGetValue(source, out var known))
        {
            return known;
        }

        return FamilyName.IsMatch(source) ? ReferenceApp : source;
    }

    /// <summary>A rule as a company reads it: the text and the source mapped; id, area, level and the check are the rule's.</summary>
    public Rule OfRule(Rule rule)
        => _tenant is null
            ? rule
            : rule with { Title = Text(rule.Title), Statement = Text(rule.Statement), Source = Source(rule.Source), Why = Text(rule.Why), Do = Text(rule.Do), Dont = Text(rule.Dont), Check = Text(rule.Check) };

    /// <summary>A line of the changelog of the rulebook as a company reads it: the note mapped; the rule, the kind, the date and the hash are the line's.</summary>
    public RuleChange OfChange(RuleChange change) => _tenant is null ? change : change with { Note = Text(change.Note) };

    /// <summary>An area of the rulebook as a company reads it.</summary>
    public RuleArea OfArea(RuleArea area) => _tenant is null ? area : area with { Name = Text(area.Name), Summary = Text(area.Summary) };

    /// <summary>
    /// The markdown of the changelog of the toolkit as a company reads it: the same words, and where a sentence names an app of the family that the map has no words for, "an app" (a history names apps in passing,
    /// and its structure, the headings and the bullets, is what tools read).
    /// </summary>
    public string Changelog(string markdown)
    {
        if (_tenant is null || markdown.Length == 0)
        {
            return markdown;
        }

        var mapped = Apply(ChangelogPattern, markdown);
        return FamilyName.Replace(mapped, m => StartsSentence(mapped, m.Index) ? "An app" : "an app");
    }

    // ---- the work

    private string Apply(Regex pattern, string text)
        => text.Length == 0 || _tenant is null ? text : pattern.Replace(text, match => Expand(match.Value));

    private string Expand(string matched)
    {
        var entry = Lookup[matched];
        var replacement = entry.To
            .Replace("{a_app}", Article(_tenant!.App) + " " + _tenant.App, StringComparison.Ordinal)
            .Replace("{apps}", _tenant.Apps, StringComparison.Ordinal)
            .Replace("{app}", _tenant.App, StringComparison.Ordinal)
            .Replace("{family}", _tenant.Family, StringComparison.Ordinal)
            .Replace("{site}", _tenant.SiteName, StringComparison.Ordinal)
            .Replace("{company}", _tenant.Company, StringComparison.Ordinal)
            .Replace("{sample}", _sample, StringComparison.Ordinal);
        return replacement;
    }

    /// <summary>"a" or "an" for the word that follows.</summary>
    private static string Article(string word) => word.Length > 0 && "AEIOUaeiou".Contains(word[0], StringComparison.Ordinal) ? "an" : "a";

    /// <summary>True when the word at <paramref name="at"/> is the first of a sentence, a line or a bullet.</summary>
    private static bool StartsSentence(string text, int at)
    {
        var before = at - 1;
        while (before >= 0 && text[before] is ' ' or '\t')
        {
            before--;
        }

        if (before < 0)
        {
            return true;
        }

        if (text[before] is '.' or ':' or '!' or '?' or '\n')
        {
            return true;
        }

        // a bullet: "- an app" at the start of a line
        return text[before] == '-' && (before == 0 || text[before - 1] == '\n');
    }

    private static readonly Dictionary<string, WordEntry> Lookup = Entries.GroupBy(e => e.From, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    private static Regex Build(params WordScope[] scopes)
    {
        var parts = Entries.Where(e => scopes.Contains(e.Scope)).Select(e => e.From).Distinct(StringComparer.Ordinal).OrderByDescending(f => f.Length)
            .Select(f => (char.IsLetterOrDigit(f[0]) ? "(?<![A-Za-z0-9])" : string.Empty) + Regex.Escape(f) + (char.IsLetterOrDigit(f[^1]) ? "(?![A-Za-z0-9])" : string.Empty));
        return new Regex(string.Join("|", parts), RegexOptions.CultureInvariant);
    }
}
