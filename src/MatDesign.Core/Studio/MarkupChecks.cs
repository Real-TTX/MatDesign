using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>A fault in the markup of an example. <see cref="Check"/> names the kind, <see cref="Example"/> is the index of the example (-1 for a fault between examples).</summary>
public sealed record MarkupProblem(string Check, int Example, string Message);

/// <summary>What the toolkit ships: the classes its stylesheets define and the icons of its sprite. The markup of an example may use nothing else.</summary>
public sealed record MarkupFacts(IReadOnlySet<string> Classes, IReadOnlySet<string> Icons);

/// <summary>
/// The contract of the markup of the documentation: the examples of the components may use only what the toolkit ships (classes, icons), carry no inline style, handler or
/// script, load nothing from another site, label their table cells, type their buttons, give their controls an id, keep their ids unique and their references resolved. The unit
/// tests hold the built-in examples to it and the Studio holds the typed ones to it, with this one piece of code, so the two can never disagree.
/// </summary>
public static partial class MarkupChecks
{
    public const string Classes = "classes";
    public const string Inline = "inline";
    public const string Elements = "elements";
    public const string Outside = "outside";
    public const string Icons = "icons";
    public const string CellLabel = "cell-label";
    public const string ButtonType = "button-type";
    public const string Controls = "controls";
    public const string Dialog = "dialog";
    public const string IconButton = "icon-button";
    public const string Balanced = "balanced";
    public const string Ids = "ids";
    public const string References = "references";

    /// <summary>The checks that keep a page safe: what must never be rendered, whatever else is wrong.</summary>
    public static readonly IReadOnlySet<string> Dangerous = new HashSet<string> { Inline, Elements, Outside };

    private static readonly string[] BalancedTags = ["div", "span", "section", "table", "tbody", "tr", "td", "ul", "ol", "li", "form", "dialog", "nav", "button", "label", "dl", "pre"];

    private static readonly string[] ReferencePatterns =
    [
        @"\sfor=""([^""]+)""", @"aria-labelledby=""([^""]+)""", @"aria-controls=""([^""]+)""", @"data-dialog-open=""([^""]+)""", @"data-menu-toggle=""([^""]+)""", @"\sform=""([^""]+)""", @"data-copy=""#([^""]+)""",
    ];

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex AttributeSelector();

    [GeneratedRegex(@"\.([A-Za-z_][\w-]*)")]
    private static partial Regex ClassSelector();

    [GeneratedRegex(@"class=""([^""]*)""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    // any attribute that starts with "on" is a handler (onclick, onerror, onpointerdown ...): none is allowed
    [GeneratedRegex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandler();

    [GeneratedRegex(@"<script\b", RegexOptions.IgnoreCase)]
    private static partial Regex Script();

    [GeneratedRegex(@"<(iframe|object|embed|link|meta|base|style|frame|frameset|applet)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenElement();

    [GeneratedRegex(@"\b(?:javascript|vbscript)\s*:|\bdata\s*:\s*text/html|\bsrcdoc\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptUrl();

    [GeneratedRegex(@"\b(?:src|href|action|formaction|poster|srcset|data)\s*=\s*""\s*(?:https?:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex OutsideResource();

    [GeneratedRegex(@"<td\b([^>]*)>")]
    private static partial Regex TableCell();

    [GeneratedRegex(@"<button\b([^>]*)>")]
    private static partial Regex Button();

    [GeneratedRegex(@"<(input|select|textarea)\b([^>]*)>")]
    private static partial Regex Control();

    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"#mat-i-([a-z0-9-]+)")]
    private static partial Regex SpriteReference();

    /// <summary>The classes that stylesheets define: every class of every selector (what is inside an attribute selector does not count).</summary>
    public static IReadOnlySet<string> ClassesIn(IEnumerable<string> stylesheets)
        => stylesheets
            .SelectMany(css => CssTokens.ParseBlocks(css).Select(b => b.Selector))
            .SelectMany(selector => ClassSelector().Matches(AttributeSelector().Replace(selector, string.Empty)).Select(m => m.Groups[1].Value))
            .ToHashSet();

    /// <summary>Everything that is wrong with the markup of the examples of one component. The ids and the references are looked at across all of them, as they share a page.</summary>
    public static IReadOnlyList<MarkupProblem> Check(IReadOnlyList<string> examples, MarkupFacts facts)
    {
        var problems = new List<MarkupProblem>();
        for (var i = 0; i < examples.Count; i++)
        {
            problems.AddRange(CheckOne(examples[i], i, facts));
        }

        var html = string.Join("\n", examples);
        var ids = IdAttribute().Matches(html).Select(m => m.Groups[1].Value).ToList();
        var twice = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (twice.Count > 0)
        {
            problems.Add(new(Ids, -1, "ids that appear twice: " + string.Join(", ", twice)));
        }

        var known = ids.ToHashSet();
        foreach (var pattern in ReferencePatterns)
        {
            foreach (Match reference in Regex.Matches(html, pattern))
            {
                if (!known.Contains(reference.Groups[1].Value))
                {
                    problems.Add(new(References, -1, $"{reference.Value.Trim()} points at nothing"));
                }
            }
        }

        return problems;
    }

    private static IEnumerable<MarkupProblem> CheckOne(string html, int index, MarkupFacts facts)
    {
        foreach (var token in ClassAttribute().Matches(html).SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct())
        {
            if (!facts.Classes.Contains(token))
            {
                yield return new(Classes, index, $"the toolkit has no class '{token}'");
            }
        }

        if (InlineStyle().IsMatch(html))
        {
            yield return new(Inline, index, "an inline style: use the classes of the toolkit (CODE-02)");
        }

        if (InlineHandler().IsMatch(html))
        {
            yield return new(Inline, index, "an inline event handler: behaviour comes from the data-attributes of the script (CODE-02)");
        }

        if (Script().IsMatch(html) || ScriptUrl().IsMatch(html))
        {
            yield return new(Inline, index, "a script or a script address: an example is markup only (CODE-02)");
        }

        foreach (Match element in ForbiddenElement().Matches(html))
        {
            yield return new(Elements, index, $"<{element.Groups[1].Value.ToLowerInvariant()}> is not allowed in an example");
        }

        if (OutsideResource().IsMatch(html))
        {
            yield return new(Outside, index, "an address of another site: an example loads nothing from outside (CODE-03)");
        }

        foreach (var name in SpriteReference().Matches(html).Select(m => m.Groups[1].Value).Distinct())
        {
            if (!facts.Icons.Contains(name))
            {
                yield return new(Icons, index, $"the icon '{name}' is not in the sprite");
            }
        }

        foreach (Match cell in TableCell().Matches(html))
        {
            var attributes = cell.Groups[1].Value;
            if (!attributes.Contains("data-label=", StringComparison.Ordinal) && !attributes.Contains("mat-table__cell-actions", StringComparison.Ordinal) && !attributes.Contains("mat-group__cell", StringComparison.Ordinal))
            {
                yield return new(CellLabel, index, $"a <td> without data-label (TABLE-02): {cell.Value}");
            }
        }

        foreach (Match button in Button().Matches(html))
        {
            if (!button.Groups[1].Value.Contains("type=\"", StringComparison.Ordinal))
            {
                yield return new(ButtonType, index, $"a button without a type submits the form it sits in: {button.Value}");
            }
        }

        foreach (Match control in Control().Matches(html))
        {
            if (control.Groups[2].Value.Contains("type=\"hidden\"", StringComparison.Ordinal))
            {
                continue;
            }

            if (!control.Groups[2].Value.Contains(" id=\"", StringComparison.Ordinal) && !control.Groups[2].Value.StartsWith(" id=\"", StringComparison.Ordinal))
            {
                yield return new(Controls, index, $"a control without an id (FORM-08): {control.Value}");
            }
        }

        if (html.Contains("<dialog", StringComparison.Ordinal))
        {
            var dialogs = Regex.Matches(html, @"<dialog\b").Count;
            if (Regex.Matches(html, @"<dialog\b[^>]*aria-labelledby=""[^""]+""[^>]*>").Count != dialogs
                || Regex.Matches(html, @"class=""mat-dialog__title""").Count != dialogs
                || Regex.Matches(html, @"mat-dialog__close[^>]*aria-label=""Close""").Count != dialogs)
            {
                yield return new(Dialog, index, "every dialog is named (aria-labelledby, a title) and has the X with aria-label=\"Close\" (DIALOG-03, DIALOG-04)");
            }
        }

        foreach (Match button in Regex.Matches(html, @"<(?:button|a)\b([^>]*)>\s*<svg[^>]*><use[^>]*></use></svg>\s*</(?:button|a)>"))
        {
            if (!button.Groups[1].Value.Contains("aria-label=", StringComparison.Ordinal))
            {
                yield return new(IconButton, index, "a button that is only an icon needs an aria-label (ICON-03)");
            }
        }

        foreach (var tag in BalancedTags)
        {
            var open = Regex.Matches(html, $@"<{tag}\b").Count;
            var close = Regex.Matches(html, $@"</{tag}>").Count;
            if (open != close)
            {
                yield return new(Balanced, index, $"{open} <{tag}> but {close} </{tag}>");
            }
        }
    }

    /// <summary>The ids that the markup defines.</summary>
    public static IReadOnlyList<string> IdsIn(string html) => IdAttribute().Matches(html).Select(m => m.Groups[1].Value).ToList();

    /// <summary>The number of buttons and links that are only an icon (for a test that wants to know the check looks at something).</summary>
    public static int IconOnlyButtons(string html) => Regex.Matches(html, @"<(?:button|a)\b([^>]*)>\s*<svg[^>]*><use[^>]*></use></svg>\s*</(?:button|a)>").Count;
}
