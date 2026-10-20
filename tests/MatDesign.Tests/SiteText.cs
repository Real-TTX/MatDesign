using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;

namespace MatDesign.Tests;

/// <summary>A place on a page where a person can read something: a text, or an attribute that is read (a title, an alt text, a label, a placeholder, a tip, a description).</summary>
public sealed record SitePiece(string Where, string Text);

/// <summary>A Mat word found on a page: the kind it was counted under, where it stood and the words around it.</summary>
public sealed record MatFinding(string Page, string Kind, string Where, string Excerpt);

/// <summary>
/// What a visitor can read on a page, taken out of the markup (no HTML parser is referenced by the tests, and a page of this site is plain, well formed markup): the text of every element except
/// the code blocks (<c>pre</c>), scripts, styles and drawings, and the attributes that are read aloud or shown (everything but the names of classes and ids, addresses and the like). The same
/// extraction reads the text values of a JSON answer and the lines of a text file, so that one list of words holds for everything a site says.
/// </summary>
public static partial class SiteText
{
    /// <summary>Elements whose content is not text a visitor reads: code blocks (the toolkit's own identifiers are in them), scripts, styles, the icon sprite and other drawings, templates.</summary>
    private static readonly HashSet<string> Skipped = ["script", "style", "pre", "svg", "template", "noscript"];

    private static readonly HashSet<string> Void = ["meta", "link", "img", "input", "br", "hr", "source", "area", "base", "col", "embed", "wbr", "param", "track"];

    /// <summary>Attributes that are names, addresses or layout and never something a person reads.</summary>
    private static readonly HashSet<string> NotRead =
    [
        "class", "id", "for", "name", "type", "rel", "href", "src", "action", "method", "role", "form", "list", "target", "d", "viewbox", "xmlns", "points", "tabindex", "autocomplete", "inputmode",
        "aria-controls", "aria-labelledby", "aria-describedby", "aria-owns", "aria-activedescendant", "aria-haspopup", "aria-expanded", "aria-current", "aria-selected", "aria-checked",
        "aria-hidden", "aria-modal", "aria-live", "aria-sort", "enctype", "novalidate", "charset", "http-equiv", "sizes", "crossorigin", "referrerpolicy", "nonce", "integrity",
    ];

    [GeneratedRegex(@"<!--.*?-->|<![^>]*>|<(/?)([A-Za-z][A-Za-z0-9:-]*)((?:""[^""]*""|'[^']*'|[^>""'])*)>", RegexOptions.Singleline)]
    private static partial Regex Tag();

    [GeneratedRegex(@"([^\s=/>""']+)(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+)))?")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>The pieces of readable text of a page, in order. <c>Where</c> is <c>text</c>, <c>title</c>, or <c>@attribute</c>.</summary>
    public static IEnumerable<SitePiece> Pieces(string html)
    {
        var skip = new Stack<string>();
        var title = false;
        var textarea = false;
        var last = 0;
        foreach (Match tag in Tag().Matches(html))
        {
            if (tag.Index > last && skip.Count == 0)
            {
                var text = Clean(html[last..tag.Index]);
                if (text.Length > 0)
                {
                    yield return new SitePiece(title ? "title" : textarea ? "textarea" : "text", text);
                }
            }

            last = tag.Index + tag.Length;
            if (!tag.Groups[2].Success)
            {
                continue;
            }

            var name = tag.Groups[2].Value.ToLowerInvariant();
            var closing = tag.Groups[1].Value == "/";
            if (closing)
            {
                if (skip.Count > 0 && skip.Peek() == name) { skip.Pop(); }
                if (name == "title") { title = false; }
                if (name == "textarea") { textarea = false; }
                continue;
            }

            if (skip.Count > 0)
            {
                if (Skipped.Contains(name) && !tag.Value.EndsWith("/>", StringComparison.Ordinal) && !Void.Contains(name)) { skip.Push(name); }
                continue;
            }

            if (Skipped.Contains(name) && !tag.Value.EndsWith("/>", StringComparison.Ordinal))
            {
                skip.Push(name);
                continue;
            }

            if (name == "title") { title = true; }
            if (name == "textarea") { textarea = true; }
            foreach (var piece in Attributes(name, tag.Groups[3].Value))
            {
                yield return piece;
            }
        }
    }

    private static IEnumerable<SitePiece> Attributes(string element, string attributes)
    {
        foreach (Match attribute in Attribute().Matches(attributes))
        {
            var name = attribute.Groups[1].Value.ToLowerInvariant();
            var value = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Success ? attribute.Groups[3].Value : attribute.Groups[4].Value;
            if (value.Length == 0 || NotRead.Contains(name))
            {
                continue;
            }

            if (element == "meta" && name == "content" && attributes.Contains("viewport", StringComparison.Ordinal))
            {
                continue;
            }

            var text = Clean(value);
            if (text.Length > 0)
            {
                yield return new SitePiece("@" + name, text);
            }
        }
    }

    /// <summary>The addresses on a page (links, images, frames, the icon of the site): what the markup points to, for the checks that say a blank site points to no app of the family.</summary>
    public static IEnumerable<(string Attribute, string Value)> Addresses(string html)
    {
        foreach (Match tag in Tag().Matches(html))
        {
            if (!tag.Groups[2].Success || tag.Groups[1].Value == "/")
            {
                continue;
            }

            foreach (Match attribute in Attribute().Matches(tag.Groups[3].Value))
            {
                var name = attribute.Groups[1].Value.ToLowerInvariant();
                if (name is "href" or "src" or "action" or "data-app" or "data-src" or "data-search-url" or "data-create-url" or "data-suggest-url" or "data-results-url")
                {
                    var value = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Success ? attribute.Groups[3].Value : attribute.Groups[4].Value;
                    if (value.Length > 0)
                    {
                        yield return (name, WebUtility.HtmlDecode(value));
                    }
                }
            }
        }
    }

    /// <summary>The text values of a JSON document (property names are the contract of an API, not words of a site).</summary>
    public static IEnumerable<SitePiece> JsonStrings(string json)
    {
        using var document = JsonDocument.Parse(json);
        var stack = new Stack<(JsonElement Element, string Path)>();
        stack.Push((document.RootElement, "$"));
        var found = new List<SitePiece>();
        while (stack.Count > 0)
        {
            var (element, path) = stack.Pop();
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    if (Clean(element.GetString() ?? string.Empty) is { Length: > 0 } text) { found.Add(new SitePiece(path, text)); }
                    break;
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in element.EnumerateArray()) { stack.Push((item, $"{path}[{index++}]")); }
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject()) { stack.Push((property.Value, $"{path}.{property.Name}")); }
                    break;
            }
        }

        // the path is for a person who reads the report; the order of the document is kept
        return found.AsEnumerable().Reverse();
    }

    /// <summary>The lines of a text file.</summary>
    public static IEnumerable<SitePiece> Lines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(l => Clean(l)).Where(l => l.Length > 0).Select(l => new SitePiece("line", l));

    private static string Clean(string text) => Spaces().Replace(WebUtility.HtmlDecode(text), " ").Trim();
}

/// <summary>
/// The words that say Mat on a site that is not the Mat family's: the family and its apps ("Mat apps", "Mat family", "MatMail"), the product MatDesign, any other word that starts with Mat in
/// capitals (MatPlatform, MatIdentity), and the bare name. <c>MatUi</c> is the toolkit's own product name: it is counted, under its own kind, and allowed (a white label build renames it).
/// </summary>
public static partial class MatWords
{
    /// <summary>The kind of the toolkit's own product name; the one kind that is allowed everywhere.</summary>
    public const string Toolkit = "MatUi";

    /// <summary>Names of the family that are not an app of the registry (a source of a rule, a product of the owner).</summary>
    private static readonly string[] OtherNames = ["Matthix"];

    private static readonly string AppNames = string.Join("|", AppRegistry.All.Select(a => a.Name).Concat(OtherNames).Select(Regex.Escape).OrderByDescending(n => n.Length));

    private static readonly Regex Pattern = new(
        @"(?<family>\bMat family\b)"
        + @"|(?<apps>\bMat apps?\b)"
        + @"|(?<design>\bMatDesign\b)"
        + @"|(?<app>\b(?:" + AppNames + @")\b)"
        + @"|(?<ui>\bMat[Uu][Ii]\b)"
        + @"|(?<other>\bMat(?:[A-Z][a-z0-9]+)+\b)"
        + @"|(?<bare>\bMat\b)",
        RegexOptions.CultureInvariant);

    [GeneratedRegex(@"[?&](?:id|app|apps)=(" + @"[a-z]+" + @")(?=&|$)")]
    private static partial Regex AppParameter();

    private static readonly HashSet<string> AppIds = AppRegistry.All.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>Every Mat word in a text, with its kind: <c>Mat family</c>, <c>Mat apps</c> (also the singular), <c>app:MatMail</c>, <c>MatDesign</c>, <c>MatUi</c>, <c>other:MatPlatform</c> and <c>Mat</c>.</summary>
    public static IEnumerable<(string Kind, string Excerpt)> Find(string text)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            var kind = match.Groups["family"].Success ? "Mat family"
                : match.Groups["apps"].Success ? "Mat apps"
                : match.Groups["app"].Success ? "app:" + match.Value
                : match.Groups["design"].Success ? "MatDesign"
                : match.Groups["ui"].Success ? Toolkit
                : match.Groups["other"].Success ? "other:" + match.Value
                : "Mat";
            var from = Math.Max(0, match.Index - 70);
            var to = Math.Min(text.Length, match.Index + match.Length + 70);
            yield return (kind, text[from..to]);
        }
    }

    /// <summary>The apps of the family that an address points to (<c>?id=matmail</c>, <c>data-app="matpaper"</c>): a blank site points to none.</summary>
    public static IEnumerable<string> AppsInAddress(string attribute, string value)
    {
        if (attribute == "data-app")
        {
            if (AppIds.Contains(value)) { yield return value; }
            yield break;
        }

        foreach (Match match in AppParameter().Matches(value))
        {
            if (AppIds.Contains(match.Groups[1].Value)) { yield return match.Groups[1].Value; }
        }
    }

    /// <summary>The findings of a page: every Mat word in the pieces and every address that names an app of the family.</summary>
    public static IEnumerable<MatFinding> In(string page, IEnumerable<SitePiece> pieces)
    {
        foreach (var piece in pieces)
        {
            foreach (var (kind, excerpt) in Find(piece.Text))
            {
                yield return new MatFinding(page, kind, piece.Where, excerpt);
            }
        }
    }

    /// <summary>The same for the addresses of a page.</summary>
    public static IEnumerable<MatFinding> InAddresses(string page, string html)
    {
        foreach (var (attribute, value) in SiteText.Addresses(html))
        {
            foreach (var app in AppsInAddress(attribute, value))
            {
                yield return new MatFinding(page, "app-address:" + app, "@" + attribute, value);
            }
        }
    }

    /// <summary>A report of findings as lines: per page and kind a count, with the first excerpt.</summary>
    public static string Describe(IEnumerable<MatFinding> findings)
    {
        var text = new StringBuilder();
        foreach (var page in findings.GroupBy(f => f.Page).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            text.Append(page.Key).Append(':');
            foreach (var kind in page.GroupBy(f => f.Kind).OrderByDescending(g => g.Count()))
            {
                text.Append("  ").Append(kind.Key).Append(" x").Append(kind.Count());
            }

            text.AppendLine();
            foreach (var first in page.GroupBy(f => (f.Kind, f.Excerpt)).Take(6))
            {
                text.Append("    ").Append(first.Key.Kind).Append(" [").Append(first.First().Where).Append("] ").AppendLine(first.Key.Excerpt);
            }
        }

        return text.ToString();
    }
}
