using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using Microsoft.AspNetCore.Html;

namespace MatDesign.Web.Services;

/// <summary>Small helpers for the markup of the toolkit: icons, logos and the escaped source of an example.</summary>
public static partial class Ui
{
    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex NameRegex();

    /// <summary>An icon from the page's inline sprite. The name must exist in <c>matui-icons.svg</c> (a unit test checks the pages).</summary>
    public static IHtmlContent Icon(string name, string? extraClass = null)
    {
        if (!NameRegex().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not an icon name.", nameof(name));
        }

        var cls = string.IsNullOrEmpty(extraClass) ? "mat-icon" : $"mat-icon {extraClass}";
        return new HtmlString($"<svg class=\"{cls}\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-{name}\"></use></svg>");
    }

    /// <summary>The logo tile of an app: its glyph on the accent gradient. The tile carries <c>data-app</c>, so it has the app's colour wherever it is.</summary>
    public static IHtmlContent Logo(MatApp app, string size = "md")
        => new HtmlString(
            $"<span class=\"mat-logo mat-logo--{size}\" data-app=\"{WebUtility.HtmlEncode(app.Id)}\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(app.Name)}\">" +
            $"<svg class=\"mat-icon\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#mat-i-{app.Glyph}\"></use></svg></span>");

    /// <summary>Plain text with `code` spans: the text is encoded, a pair of backticks becomes a code element.</summary>
    public static IHtmlContent Inline(string text)
        => new HtmlString(CodeRegex().Replace(WebUtility.HtmlEncode(text), "<code>$1</code>"));

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeRegex();

    // class names of the toolkit, its data- and aria- attributes and the state classes
    [GeneratedRegex(@"(?<![\w>-])((?:mat|data|aria)-[a-z0-9_-]+|is-(?:invalid|selected|active|current|done))(?![\w-])")]
    private static partial Regex IdentifierRegex();

    /// <summary>
    /// Plain text in which a pair of backticks and the toolkit's own identifiers (<c>mat-btn-row</c>, <c>data-confirm</c>, <c>aria-label</c>) are set as code.
    /// Everything is encoded; an identifier inside backticks is set as code once.
    /// </summary>
    public static IHtmlContent Rich(string text)
    {
        var parts = Regex.Split(text, "(`[^`]+`)").Select(part => part.Length > 1 && part[0] == '`' && part[^1] == '`'
            ? "<code>" + WebUtility.HtmlEncode(part[1..^1]) + "</code>"
            : IdentifierRegex().Replace(WebUtility.HtmlEncode(part), "<code>$1</code>"));
        return new HtmlString(string.Concat(parts));
    }

    /// <summary>A colour as an SVG rectangle: <c>fill</c> is an attribute, not an inline style, so a page can show any colour without breaking the no-inline-style rule.</summary>
    public static IHtmlContent Swatch(string hex, string kind = "dot")
    {
        var h = WebUtility.HtmlEncode(hex);
        return new HtmlString($"<svg class=\"doc-sw doc-sw--{kind}\" viewBox=\"0 0 4 4\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"{h}\"><rect width=\"4\" height=\"4\" fill=\"{h}\"></rect></svg>");
    }

    public static IHtmlContent Swatch(Rgb colour, string kind = "dot") => Swatch(colour.ToHex(), kind);

    /// <summary>The first letters of a name, for an avatar.</summary>
    public static string Initials(string name)
        => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    public static string RuleLevelClass(RuleLevel level) => level switch
    {
        RuleLevel.Must => "mat-pill--accent",
        RuleLevel.Should => string.Empty,
        _ => "mat-pill--plain",
    };

    public static string Kb(int bytes) => bytes < 1024 ? $"{bytes} B" : string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB");
}
