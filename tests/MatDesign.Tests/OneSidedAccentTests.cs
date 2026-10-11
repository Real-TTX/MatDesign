using System.Text.RegularExpressions;
using MatDesign.Core;

namespace MatDesign.Tests;

/// <summary>COLOR-08: what is selected is a tint behind the whole item, never a bar or a border at one side of a rounded item. The scan reads the CSS of the toolkit and of the site.</summary>
public sealed partial class OneSidedAccentTests
{
    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}")]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"^border-(left|right|top|bottom|inline-start|inline-end|block-start|block-end)(-width)?$")]
    private static partial Regex OneSidedName();

    [GeneratedRegex(@"(?<![\w.])(\d+(?:\.\d+)?)px")]
    private static partial Regex Pixels();

    [GeneratedRegex(@"is-active|is-selected|aria-current|aria-selected|:checked")]
    private static partial Regex SelectedSelector();

    /// <summary>The wide stylesheets of the toolkit and the one of the site, as (name, text).</summary>
    private static IEnumerable<(string Name, string Css)> Sheets()
    {
        foreach (var file in Directory.GetFiles(TestPaths.Toolkit("css"), "*.css"))
        {
            yield return (Path.GetFileName(file), File.ReadAllText(file));
        }

        yield return ("site.css", File.ReadAllText(TestPaths.Web("wwwroot/css/site.css")));
    }

    private static bool Rounded(IReadOnlyDictionary<string, string> declarations)
        => declarations.TryGetValue("border-radius", out var value) && Regex.Matches(value, @"[\d.]+").Any(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) > 0);

    /// <summary>True when a width of two pixels or more is written in the value (a hairline of 1 px is a divider, not a mark).</summary>
    private static bool Thick(string value) => Pixels().Matches(value).Any(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 2);

    /// <summary>An inset shadow that is a bar at one side: exactly one offset, no blur, no spread (<c>inset 2px 0 0 red</c>); the focus ring <c>inset 0 0 0 2px</c> is not one.</summary>
    private static bool InsetBar(string value)
        => Regex.Matches(value, @"inset\s+(-?[\d.]+)(?:px)?\s+(-?[\d.]+)(?:px)?\s+(-?[\d.]+)(?:px)?\s+(?:-?[\d.]+(?:px)?\s+)?")
            .Any(m =>
            {
                var x = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var y = double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var blur = double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                return blur == 0 && (Math.Abs(x) >= 2 && y == 0 || Math.Abs(y) >= 2 && x == 0);
            });

    /// <summary>The places in a stylesheet where an accent sits at one side of an item: a thick border or an inset shadow on a rounded item, or in any selected state, or a bar drawn as a pseudo-element of a selected state.</summary>
    internal static IReadOnlyList<string> Violations(string name, string css)
    {
        var found = new List<string>();
        foreach (Match rule in RuleRegex().Matches(CommentRegex().Replace(css, string.Empty)))
        {
            var selector = Regex.Replace(rule.Groups[1].Value, @"\s+", " ").Trim();
            if (selector.StartsWith('@'))
            {
                continue;
            }

            var declarations = new Dictionary<string, string>();
            foreach (var part in rule.Groups[2].Value.Split(';'))
            {
                var colon = part.IndexOf(':');
                if (colon > 0)
                {
                    declarations[part[..colon].Trim().ToLowerInvariant()] = part[(colon + 1)..].Trim();
                }
            }

            var selected = SelectedSelector().IsMatch(selector);
            var rounded = Rounded(declarations);
            foreach (var (property, value) in declarations)
            {
                var side = OneSidedName().IsMatch(property) && Thick(value) && !value.StartsWith("0 ", StringComparison.Ordinal);
                var bar = property == "box-shadow" && InsetBar(value);
                if ((side || bar) && (rounded || selected))
                {
                    found.Add($"{name}: {selector} {{ {property}: {value} }} is {(rounded ? "a rounded item" : "a selected state")} with an accent at one side");
                }
            }

            if (selected && Regex.IsMatch(selector, "::?(before|after)$") && declarations.TryGetValue("position", out var position) && position == "absolute"
                && declarations.TryGetValue("width", out var width) && Pixels().IsMatch(width) && !Regex.IsMatch(selector, "mat-switch"))
            {
                found.Add($"{name}: {selector} draws a bar for a selected state");
            }
        }

        return found;
    }

    [Fact]
    public void No_stylesheet_marks_what_is_selected_with_a_bar_at_one_side_of_a_rounded_item()
    {
        var all = Sheets().SelectMany(s => Violations(s.Name, s.Css)).ToList();

        Assert.True(all.Count == 0, "COLOR-08 is broken:\n" + string.Join("\n", all));
    }

    [Theory]
    [InlineData(".a { border-radius: 6px; } .a.is-active { box-shadow: inset 2px 0 0 red; }", 1)]
    [InlineData(".b { border-left: 3px solid red; border-radius: 4px; }", 1)]
    [InlineData(".b { border-radius: 4px 0 0 4px; border-right: 2px solid red; }", 1)]
    [InlineData(".c[aria-selected=\"true\"] { box-shadow: inset 0 -3px 0 red; }", 1)]
    [InlineData(".d { position: relative; } .d[aria-current=\"page\"]::before { position: absolute; left: 0; width: 3px; background: red; }", 1)]
    [InlineData(".e { border: 0; border-bottom: 2px solid transparent; border-radius: 0; } .e[aria-selected=\"true\"] { border-bottom-color: red; }", 0)]
    [InlineData(".f { border-top: 1px solid grey; border-radius: 8px; }", 0)]
    [InlineData(".g:focus-visible { box-shadow: inset 0 0 0 2px red; border-radius: 6px; }", 0)]
    [InlineData(".h { border-radius: 6px; background: red; } .h.is-active { background: blue; }", 0)]
    [InlineData("@media (min-width: 1px) { .i.is-active { box-shadow: inset 3px 0 0 red; } }", 1)]
    public void The_scan_finds_a_bar_at_one_side_and_leaves_an_underline_a_hairline_and_a_ring_alone(string css, int expected)
    {
        Assert.Equal(expected, Violations("sample", css).Count);
    }

    [Fact]
    public void The_rule_is_in_the_rulebook()
    {
        var rule = RuleBook.All.Single(r => r.Id == "COLOR-08");

        Assert.Equal("COLOR", rule.Area);
        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.False(string.IsNullOrWhiteSpace(rule.Do));
        Assert.False(string.IsNullOrWhiteSpace(rule.Dont));
    }

    [Fact]
    public void A_selected_menu_item_and_index_entry_and_vertical_tab_are_a_tint_of_the_accent()
    {
        var shell = TestPaths.ReadToolkit("css/matui.shell.css");
        var lists = TestPaths.ReadToolkit("css/matui.lists.css");

        Assert.Contains(".mat-nav__item[aria-current=\"page\"] { background: var(--mat-accent-soft-strong)", shell);
        Assert.Contains(".mat-toc__link.is-active { background: var(--mat-accent-soft-strong)", lists);
        Assert.Contains(".mat-tabs--vertical .mat-tab.is-active { background: var(--mat-accent-soft-strong)", lists);
    }
}
