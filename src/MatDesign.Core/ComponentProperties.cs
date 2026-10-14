using System.Text.RegularExpressions;

namespace MatDesign.Core;

/// <summary>
/// A custom property that a control of the toolkit offers for its shape: <c>--mat-btn-radius</c>. It has no value of its own in the CSS: wherever the control uses it, it is
/// <c>var(--mat-btn-radius, var(--mat-radius))</c>, so the <see cref="Fallback"/> (a global token or, where the toolkit has none, a literal) is the default and an app that does not set it sees nothing change.
/// </summary>
/// <param name="Component">The id of the component group (<see cref="ComponentProperties.Components"/>): <c>btn</c>.</param>
/// <param name="Name">The property, <c>--mat-&lt;component&gt;-&lt;aspect&gt;</c>.</param>
/// <param name="Type">colour, length, number, duration or font: what the property takes (a brand editor offers a matching input).</param>
/// <param name="Fallback">What the control uses when the property is not set: <c>var(--mat-radius)</c> or a literal such as <c>1px</c>.</param>
/// <param name="Description">What the property changes, in one sentence.</param>
public sealed record ComponentProperty(string Component, string Name, string Type, string Fallback, string Description);

/// <summary>A place in the CSS where a property is used with a fallback: <c>var(--mat-btn-radius, var(--mat-radius))</c>.</summary>
public sealed record PropertyUse(string Part, string Name, string Fallback);

/// <summary>A group of properties: its id (the prefix of the names), its title and the components of the documentation that show the properties.</summary>
public sealed record PropertyComponent(string Id, string Title, IReadOnlyList<string> Docs);

/// <summary>
/// The documented component properties of <c>MatUi</c>, read from the CSS. Each property is declared by one line above the rule that uses it, in one strict format:
/// <code>/* @prop btn | --mat-btn-radius | length | var(--mat-radius) | Corner radius of every button. */</code>
/// (component, name, type, fallback, description; no <c>|</c> in a field). The CSS stays the single source: the site, the documentation and the tests read the list from it, and
/// <see cref="Problems"/> says what is wrong when a property is used and not declared, declared and not used, or has a fallback that is neither an existing token nor a literal of its type.
/// </summary>
public static partial class ComponentProperties
{
    /// <summary>What a property can take. A colour or a font is always a token (the toolkit has no literal colour outside the token files).</summary>
    public static readonly IReadOnlyList<string> Types = ["colour", "length", "number", "duration", "font"];

    /// <summary>
    /// The component groups that have properties and the components of the catalogue that document them. The id is the prefix of a name (<c>--mat-btn-*</c>); a custom property
    /// that starts with one of these and is used with a fallback must be declared, so a new property of a control cannot stay undocumented.
    /// </summary>
    public static readonly IReadOnlyList<PropertyComponent> Components =
    [
        new("btn", "Button", ["buttons", "button-group"]),
        new("input", "Field, select and text area", ["fields", "input-types", "selects", "input-groups", "special-inputs", "floating-labels"]),
        new("card", "Card", ["cards"]),
        new("dialog", "Dialog", ["dialogs"]),
        new("menu", "Context menu", ["menu", "dropdown"]),
        new("tag", "Tag", ["pills"]),
        new("pill", "Status pill", ["pills"]),
        new("badge", "Badge", ["pills"]),
        new("tabs", "Tabs", ["tabs"]),
        new("table", "Table", ["table"]),
        new("alert", "Alert", ["alerts"]),
        new("toast", "Toast", ["toasts"]),
        new("switch", "Switch", ["choices"]),
        new("check", "Checkbox and radio button", ["choices", "checks"]),
        new("nav", "Menu item", ["navmenu"]),
        new("topbar", "Header bar", ["topbar"]),
    ];

    [GeneratedRegex(@"^[ \t]*/\*[ \t]*@prop[ \t]+([a-z][a-z0-9]*)[ \t]*\|[ \t]*(--mat-[a-z0-9-]+)[ \t]*\|[ \t]*([a-z]+)[ \t]*\|[ \t]*([^|]+?)[ \t]*\|[ \t]*([^|]+?)[ \t]*\*/[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex PropLine();

    [GeneratedRegex(@"@prop\b")]
    private static partial Regex PropMark();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"var\(\s*(--mat-[a-z0-9-]+)\s*,\s*")]
    private static partial Regex VarWithFallback();

    [GeneratedRegex(@"^var\((--mat-[a-z0-9-]+)\)$")]
    private static partial Regex TokenVar();

    [GeneratedRegex(@"^(0|-?(\d+\.?\d*|\.\d+)(px|rem|em|%|vh|vw|dvh|ch))$")]
    private static partial Regex LengthLiteral();

    [GeneratedRegex(@"^-?(\d+\.?\d*|\.\d+)$")]
    private static partial Regex NumberLiteral();

    [GeneratedRegex(@"^(\d+\.?\d*|\.\d+)(ms|s)$")]
    private static partial Regex DurationLiteral();

    /// <summary>The properties declared in a stylesheet, in the order of the file.</summary>
    public static IReadOnlyList<ComponentProperty> Parse(string css)
        => PropLine().Matches(css)
            .Select(m => new ComponentProperty(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, Squash(m.Groups[4].Value), Squash(m.Groups[5].Value)))
            .ToList();

    /// <summary>The properties declared in the parts of the toolkit, in the order of the bundle.</summary>
    public static IReadOnlyList<ComponentProperty> Parse(IEnumerable<string> parts) => parts.SelectMany(Parse).ToList();

    /// <summary>The properties of one component group.</summary>
    public static IEnumerable<ComponentProperty> Of(IEnumerable<ComponentProperty> all, string component) => all.Where(p => p.Component == component);

    /// <summary>The properties that the component of the documentation <paramref name="docId"/> shows (grouped by component group, in the order of <see cref="Components"/>).</summary>
    public static IReadOnlyList<ComponentProperty> ForDoc(IEnumerable<ComponentProperty> all, string docId)
    {
        var ids = Components.Where(c => c.Docs.Contains(docId, StringComparer.OrdinalIgnoreCase)).Select(c => c.Id).ToList();
        return all.Where(p => ids.Contains(p.Component)).OrderBy(p => ids.IndexOf(p.Component)).ToList();
    }

    /// <summary>Every use of a custom property with a fallback in a stylesheet, comments left out.</summary>
    public static IReadOnlyList<PropertyUse> Uses(string part, string css)
    {
        var text = Comment().Replace(css, " ");
        var uses = new List<PropertyUse>();
        foreach (Match m in VarWithFallback().Matches(text))
        {
            var start = m.Index + m.Length;
            var depth = 1;
            var i = start;
            while (i < text.Length && depth > 0)
            {
                depth += text[i] == '(' ? 1 : text[i] == ')' ? -1 : 0;
                i++;
            }

            if (depth == 0)
            {
                uses.Add(new PropertyUse(part, m.Groups[1].Value, Squash(text[start..(i - 1)])));
            }
        }

        return uses;
    }

    /// <summary>
    /// True when <paramref name="fallback"/> is something a property of <paramref name="type"/> may fall back to: <c>var(--mat-token)</c> of a token that exists, or, for a length,
    /// a number or a duration where the toolkit has no token, a literal of that type. The reason is given when it is not.
    /// </summary>
    public static bool ValidFallback(string type, string fallback, IReadOnlySet<string> tokens, out string reason)
    {
        reason = string.Empty;
        var token = TokenVar().Match(fallback);
        if (token.Success)
        {
            if (tokens.Contains(token.Groups[1].Value))
            {
                return true;
            }

            reason = $"{token.Groups[1].Value} is not a token of matui.tokens.css";
            return false;
        }

        var literal = type switch
        {
            "length" => LengthLiteral().IsMatch(fallback),
            "number" => NumberLiteral().IsMatch(fallback),
            "duration" => DurationLiteral().IsMatch(fallback),
            _ => false,
        };
        if (!literal)
        {
            reason = type is "colour" or "font"
                ? $"a {type} falls back to a token, not to '{fallback}'"
                : $"'{fallback}' is neither a token nor a literal {type}";
        }

        return literal;
    }

    /// <summary>
    /// What is wrong with the documented properties of a set of stylesheets (name and content of each part); empty when the list is right. The checks: every <c>@prop</c> line has the
    /// format; the component exists and starts the name; the type is known; a name is declared once; the fallback is a token that exists (or a literal of the type); the property is used;
    /// its uses have a valid fallback, and one of them the declared one; no property of a component group is used with a fallback without being declared; and no part gives a documented
    /// property a value (the fallback is the default). <paramref name="tokens"/> are the names of the tokens.
    /// </summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<(string Name, string Css)> parts, IReadOnlySet<string> tokens)
    {
        var problems = new List<string>();
        var declared = new List<ComponentProperty>();
        foreach (var (name, css) in parts)
        {
            var lines = css.Split('\n');
            for (var n = 0; n < lines.Length; n++)
            {
                if (PropMark().IsMatch(lines[n]) && !PropLine().IsMatch(lines[n]))
                {
                    problems.Add($"{name}:{n + 1}: a line with @prop is not in the format '/* @prop component | --mat-component-aspect | type | fallback | Description. */'");
                }
            }

            declared.AddRange(Parse(css));
        }

        foreach (var dup in declared.GroupBy(p => p.Name).Where(g => g.Count() > 1))
        {
            problems.Add($"{dup.Key} is declared {dup.Count()} times");
        }

        var uses = parts.SelectMany(p => Uses(p.Name, p.Css)).ToList();
        foreach (var p in declared)
        {
            if (!Components.Any(c => c.Id == p.Component))
            {
                problems.Add($"{p.Name}: the component '{p.Component}' is not in ComponentProperties.Components");
            }
            else if (!p.Name.StartsWith($"--mat-{p.Component}-", StringComparison.Ordinal))
            {
                problems.Add($"{p.Name}: the name of a property of '{p.Component}' starts with --mat-{p.Component}-");
            }

            if (!Types.Contains(p.Type))
            {
                problems.Add($"{p.Name}: the type '{p.Type}' is none of {string.Join(", ", Types)}");
            }
            else if (!ValidFallback(p.Type, p.Fallback, tokens, out var why))
            {
                problems.Add($"{p.Name}: the fallback is wrong: {why}");
            }

            if (p.Description.Length < 10 || !p.Description.EndsWith('.'))
            {
                problems.Add($"{p.Name}: the description is a sentence that ends with a full stop");
            }

            var own = uses.Where(u => u.Name == p.Name).ToList();
            if (own.Count == 0)
            {
                problems.Add($"{p.Name} is declared with @prop but no rule uses it as var({p.Name}, ...)");
            }
            else
            {
                if (!own.Any(u => u.Fallback == p.Fallback))
                {
                    problems.Add($"{p.Name}: no use has the declared fallback '{p.Fallback}' (uses: {string.Join(" | ", own.Select(u => u.Fallback).Distinct())})");
                }

                foreach (var u in own.Where(u => Types.Contains(p.Type) && !ValidFallback(p.Type, u.Fallback, tokens, out _)))
                {
                    ValidFallback(p.Type, u.Fallback, tokens, out var why);
                    problems.Add($"{u.Part}: {p.Name} is used with the fallback '{u.Fallback}': {why}");
                }
            }
        }

        var names = declared.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var u in uses.Where(u => !names.Contains(u.Name)))
        {
            var component = Components.FirstOrDefault(c => u.Name.StartsWith($"--mat-{c.Id}-", StringComparison.Ordinal));
            if (component is not null && !tokens.Contains(u.Name))
            {
                problems.Add($"{u.Part}: {u.Name} is used with a fallback but has no @prop line (a property of {component.Title} is documented)");
            }
        }

        foreach (var (name, css) in parts.Where(p => p.Name != "matui.apps.css"))
        {
            var text = Comment().Replace(css, " ");
            foreach (var p in declared)
            {
                if (Regex.IsMatch(text, @"(?<![\w-])" + Regex.Escape(p.Name) + @"\s*:"))
                {
                    problems.Add($"{name}: {p.Name} is given a value; the fallback of its uses is the default");
                }
            }
        }

        return problems;
    }

    private static string Squash(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
