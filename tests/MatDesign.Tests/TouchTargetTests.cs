using System.Text.RegularExpressions;
using MatDesign.Core;

namespace MatDesign.Tests;

/// <summary>
/// MOBILE-04 on the CSS and the script: what a touch screen gets is a hit area of 44 px, not a bigger mark. The browser check (tests/e2e/run.js, "Touch targets", and
/// tests/e2e/touch-audit.js) measures the real thing; these hold the pieces it relies on.
/// </summary>
public partial class TouchTargetTests
{
    private static readonly string[] CssParts = ["shell", "controls", "forms", "pickers", "lists", "dialogs", "layout"];

    private static string Css(string part) => Regex.Replace(TestPaths.ReadToolkit($"css/matui.{part}.css"), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    /// <summary>The text of every block that is written for a touch screen: `@media (pointer: coarse) { ... }`, also with more conditions after it.</summary>
    private static List<string> CoarseBlocks(string css)
    {
        var blocks = new List<string>();
        foreach (Match m in CoarseMediaRegex().Matches(css))
        {
            var depth = 1;
            var i = m.Index + m.Length;
            var start = i;
            while (i < css.Length && depth > 0)
            {
                depth += css[i] == '{' ? 1 : css[i] == '}' ? -1 : 0;
                i++;
            }

            blocks.Add(css[start..(i - 1)]);
        }

        return blocks;
    }

    [GeneratedRegex(@"@media[^{]*\(pointer:\s*coarse\)[^{]*\{")]
    private static partial Regex CoarseMediaRegex();

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"(?<![-\w])(?<prop>min-height|min-width|height|width)\s*:\s*(?<value>\d+(?:\.\d+)?)px")]
    private static partial Regex SizeRegex();

    [Fact]
    public void A_rule_for_a_touch_screen_never_makes_a_target_smaller_than_44_px()
    {
        // A rule under (pointer: coarse) that sets a height or a width in px sets it for a target: it is at least 44 px (the mark inside a 44 px target, the ::after of a
        // target, is not a target and is not set here: it is a ::before or ::after, a swatch's inset or a drawn size).
        var small = new List<string>();
        foreach (var part in CssParts)
        {
            foreach (var block in CoarseBlocks(Css(part)))
            {
                foreach (Match rule in RuleRegex().Matches(block))
                {
                    var selector = rule.Groups["selector"].Value.Trim();
                    foreach (Match size in SizeRegex().Matches(rule.Groups["body"].Value))
                    {
                        if (double.Parse(size.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture) < 44 && !selector.Contains("--sm", StringComparison.Ordinal) && !selector.Contains("--compact", StringComparison.Ordinal))
                        {
                            small.Add($"{part}: {selector} {{ {size.Value} }}");
                        }
                    }
                }
            }
        }

        Assert.True(small.Count == 0, "A touch screen gets a target under 44 px: " + string.Join("; ", small));
    }

    [Theory]
    [InlineData("forms", ".mat-check { min-height: 44px")]
    [InlineData("forms", ".mat-switch { min-height: 44px")]
    [InlineData("forms", ".mat-switch--bare { min-width: 44px")]
    [InlineData("forms", ".mat-seg__btn { position: relative; min-width: 44px")]
    [InlineData("lists", ".mat-pager__btn { min-width: 44px; height: 44px")]
    [InlineData("lists", ".mat-carousel__dot { width: 44px; height: 44px")]
    [InlineData("lists", ".mat-toc__link { min-height: 44px")]
    [InlineData("lists", ".mat-table__sort { min-width: 44px")]
    [InlineData("lists", ".mat-tabs--pills .mat-tab { min-height: 44px")]
    [InlineData("shell", ".mat-nav__item--sub, .mat-menu .mat-menu__item--sub { min-height: 44px")]
    [InlineData("shell", ".mat-crumbs a { display: block; min-width: 44px")]
    [InlineData("shell", ".mat-footer__links a { display: inline-flex; align-items: center; justify-content: center; min-width: 44px; min-height: 44px")]
    [InlineData("pickers", ".mat-picker__option, .mat-picker__all { min-height: 44px")]
    public void The_pieces_of_the_touch_targets_are_in_the_toolkit_for_a_touch_screen_only(string part, string rule)
    {
        var css = Css(part);
        var inCoarse = CoarseBlocks(css).Any(b => b.Contains(rule, StringComparison.Ordinal));
        Assert.True(inCoarse, $"matui.{part}.css has no `{rule} ...` under @media (pointer: coarse)");
        // and never outside of it: with a mouse nothing changes
        var outside = css;
        foreach (var block in CoarseBlocks(css))
        {
            outside = outside.Replace(block, string.Empty, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(rule, outside);
    }

    [Theory]
    [InlineData("pickers", ".mat-cal__day { height: 44px")]
    [InlineData("pickers", ".mat-timecol__item { height: 44px")]
    [InlineData("pickers", ".mat-cal__title--button { min-height: 44px")]
    [InlineData("pickers", ".mat-colour__swatches { grid-template-columns: repeat(auto-fill, minmax(44px, 1fr))")]
    [InlineData("pickers", ".mat-colour__hue { box-sizing: border-box; height: 44px")]
    public void The_pickers_have_44_px_cells_on_a_touch_screen(string part, string rule)
    {
        Assert.True(CoarseBlocks(Css(part)).Any(b => b.Contains(rule, StringComparison.Ordinal)), $"matui.{part}.css has no `{rule} ...` under @media (pointer: coarse)");
    }

    [Fact]
    public void A_small_button_inside_something_larger_keeps_its_look_and_gets_a_centred_target()
    {
        // The X of a chip or an alert, the clear button of a picker, a button of the segmented control: a ::before of 44 px centred on the button, so that nothing around it moves
        // (a ::before: the ::after of an element with data-tip is its tooltip).
        foreach (var (part, selector) in new[] { ("controls", ".mat-chip__x::before"), ("controls", ".mat-alert__close::before"), ("pickers", ".mat-picker__clear::before") })
        {
            var rule = CoarseBlocks(Css(part)).SelectMany(b => RuleRegex().Matches(b).Select(m => (Selector: m.Groups["selector"].Value, Body: m.Groups["body"].Value)))
                .FirstOrDefault(r => r.Selector.Contains(selector, StringComparison.Ordinal));
            Assert.True(rule.Body is not null, $"{selector} is not set under (pointer: coarse)");
            Assert.Contains("width: 44px", rule.Body);
            Assert.Contains("height: 44px", rule.Body);
            Assert.Contains("translate(-50%, -50%)", rule.Body);
        }
    }

    [Fact]
    public void The_marks_of_a_check_and_a_switch_are_not_set_by_the_touch_rules()
    {
        // --mat-check-size and --mat-switch-* change the drawn mark and the target never depends on them: the touch rules do not mention the properties.
        var forms = string.Concat(CoarseBlocks(Css("forms")));
        Assert.DoesNotContain("--mat-check-size", forms);
        Assert.DoesNotContain("--mat-switch-", forms);
    }

    [Fact]
    public void A_rating_that_has_no_room_for_44_px_shapes_is_set_in_two_rows_by_the_script_and_the_filled_part_follows()
    {
        var css = string.Concat(CoarseBlocks(Css("forms")));
        Assert.Contains(".mat-rating.is-wrapped .mat-rating__layer { grid-template-columns: repeat(var(--mat-rating-cols)", css);
        Assert.Contains("polygon(", css);
        Assert.Contains("--mat-rating-top", css);
        Assert.Contains("--mat-rating-bottom", css);
        var script = TestPaths.ReadToolkit("js/matui.js");
        Assert.Contains("function ratFit(", script);
        Assert.Contains("'is-wrapped'", script);
        Assert.Contains("(pointer: coarse)", script);
        Assert.Contains("--mat-rating-cols", script);
    }

    [Fact]
    public void A_tap_on_the_cell_around_a_check_and_on_a_row_that_is_a_field_is_forwarded_by_the_script()
    {
        var script = TestPaths.ReadToolkit("js/matui.js");
        Assert.Contains("closest('.mat-table__select')", script);
        Assert.Contains("closest('.mat-item__main')", script);
    }

    [Fact]
    public void The_rule_says_that_the_target_not_the_mark_is_44_px()
    {
        var rule = RuleBook.Get("MOBILE-04")!;
        Assert.True(rule.Statement.Length <= 1200, $"MOBILE-04 is {rule.Statement.Length} characters");
        Assert.Contains("hit area", rule.Statement);
        Assert.Contains("44 px", rule.Statement);
        Assert.Contains("36 px", rule.Statement);
    }

    [Fact]
    public void The_touch_checks_are_in_the_browser_tests_and_share_one_way_of_measuring()
    {
        var e2e = Path.Combine(TestPaths.RepoRoot, "tests", "e2e");
        var hit = File.ReadAllText(Path.Combine(e2e, "touch-hit.js"));
        var audit = File.ReadAllText(Path.Combine(e2e, "touch-audit.js"));
        var run = File.ReadAllText(Path.Combine(e2e, "run.js"));
        Assert.Contains("document.elementFromPoint", hit);
        Assert.Contains("require('./touch-hit')", audit);
        Assert.Contains("require('./touch-hit')", run);
        Assert.Contains("Touch targets (MOBILE-04)", run);
        // every exception of the audit has its reason
        foreach (Match m in Regex.Matches(audit, @"return '(?<id>[a-z-]+)';"))
        {
            Assert.True(Regex.IsMatch(audit, $@"'{m.Groups["id"].Value}':\s*'"), $"the exception '{m.Groups["id"].Value}' of touch-audit.js has no reason");
        }
    }
}
