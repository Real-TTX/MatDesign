using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>
/// MOBILE-04 and MOBILE-06 on the CSS and the script: what a touch screen gets is a hit area of 44 px, not a bigger mark. The browser check (tests/e2e/run.js, "Touch targets", and
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

    // ------------------------------------------------------------------------------------------------------ MOBILE-06: the type of a field

    [GeneratedRegex(@"font-size:\s*(?:1rem|max\(1rem,[^;]+\))\s*;")]
    private static partial Regex FieldSize();

    [Fact]
    public void Every_text_field_the_toolkit_draws_gets_16_px_type_for_a_touch_screen_and_a_mouse_sees_no_change()
    {
        // The fields take their size from what they stand in (15 px in the body) and `font: inherit` is set on some of them after the rule of the forms part, so the pieces are in three parts:
        // matui.forms.css (the field, the select, the text area, the small and the large size, the stepper), matui.pickers.css (the search field, the tags) and matui.lists.css (the field of a row).
        var coarse = new List<(string Part, string Selector, string Body)>();
        foreach (var part in CssParts)
        {
            foreach (var block in CoarseBlocks(Css(part)))
            {
                foreach (Match rule in RuleRegex().Matches(block))
                {
                    if (rule.Groups["body"].Value.Contains("font-size", StringComparison.Ordinal))
                    {
                        coarse.Add((part, rule.Groups["selector"].Value.Trim(), rule.Groups["body"].Value.Trim()));
                    }
                }
            }
        }

        foreach (var (part, field) in new[]
        {
            ("forms", ".mat-input"), ("forms", ".mat-select"), ("forms", ".mat-textarea"), ("forms", ".mat-input--sm"), ("forms", ".mat-select--sm"), ("forms", ".mat-input--lg"), ("forms", ".mat-select--lg"),
            ("forms", ".mat-input-group--sm .mat-input"), ("forms", ".mat-input-group--lg .mat-select"), ("pickers", ".mat-search__input"), ("pickers", ".mat-tagfield__input"), ("lists", ".mat-item__input"),
        })
        {
            Assert.True(coarse.Any(r => r.Part == part && r.Selector.Split(',').Select(s => s.Trim()).Contains(field)), $"matui.{part}.css has no `font-size` for {field} under @media (pointer: coarse)");
        }

        // none of them gives less than 16 px: the value is 1rem or max(1rem, ...), and the large size keeps its own token
        var fields = new Regex(@"\.mat-(?:input|select|textarea|search__input|tagfield__input|item__input|stepper)");
        foreach (var rule in coarse.Where(r => fields.IsMatch(r.Selector)))
        {
            Assert.True(FieldSize().IsMatch(rule.Body), $"{rule.Selector} sets a type of less than 16 px for a touch screen: {rule.Body}");
        }

        Assert.Contains(coarse, r => r.Selector.Contains(".mat-input--lg", StringComparison.Ordinal) && r.Body.Contains("var(--mat-fs-lg)", StringComparison.Ordinal));

        // the rules that give a field its type outside of a touch screen have the same weight as these, so the later one wins: each of these stands after them in its part
        // (the input group set the type of its fields after the sizes of the field: a first version stood before it and left `grp-sm` at 13 px)
        var forms = Css("forms");
        var formsBlock = forms.IndexOf(".mat-input, .mat-select, .mat-textarea { font-size: max(1rem, 1em); }", StringComparison.Ordinal);
        Assert.True(formsBlock > 0, "the touch rule of the fields is not in matui.forms.css");
        foreach (var before in new[] { ".mat-input--sm, .mat-select--sm {", ".mat-input--lg, .mat-select--lg {", ".mat-input-group--sm .mat-input,", ".mat-input-group--lg .mat-input,", ".mat-stepper:has(> .mat-input--sm) {", ".mat-stepper:has(> .mat-input--lg) {" })
        {
            var at = forms.IndexOf(before, StringComparison.Ordinal);
            Assert.True(at >= 0, $"matui.forms.css has no `{before}`");
            Assert.True(formsBlock > at, $"the touch rule of the fields stands before `{before}` in matui.forms.css, which then has the last word");
        }

        var pickers = Css("pickers");
        var pickersBlock = pickers.IndexOf(".mat-search__input, .mat-tagfield__input { font-size: max(1rem, 1em); }", StringComparison.Ordinal);
        Assert.True(pickersBlock > pickers.IndexOf(".mat-tagfield__input {", StringComparison.Ordinal) && pickersBlock > pickers.LastIndexOf(".mat-search__input { font-size: 1rem; }", StringComparison.Ordinal) && pickersBlock > pickers.LastIndexOf("  font: inherit;\n  appearance: none;", StringComparison.Ordinal), "the touch rule of the search field and the tags does not stand after their own rules in matui.pickers.css");
        var lists = Css("lists");
        Assert.True(lists.IndexOf(".mat-item__input { font-size: max(1rem, 1em); }", StringComparison.Ordinal) > lists.IndexOf(".mat-item__input {", StringComparison.Ordinal), "the touch rule of the field of a row does not stand after its own rule in matui.lists.css");

        // and never outside of a touch screen: with a mouse the type is what it was (the 1rem of the search field on a phone below 640 px was there before)
        foreach (var part in CssParts)
        {
            var outside = Css(part);
            foreach (var block in CoarseBlocks(outside))
            {
                outside = outside.Replace(block, string.Empty, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("max(1rem, 1em)", outside);
        }
    }

    [Fact]
    public void The_rule_says_16_px_for_a_touch_screen_no_page_stops_the_zoom_and_the_catalogue_says_what_is_true()
    {
        var rule = RuleBook.Get("MOBILE-06")!;
        Assert.True(rule.Statement.Length <= 1200, $"MOBILE-06 is {rule.Statement.Length} characters");
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.Contains("16 px", rule.Statement);
        Assert.Contains("iOS", rule.Statement);
        Assert.Contains("@media (pointer: coarse)", rule.Statement);
        Assert.Contains("max(1rem, 1em)", rule.Statement);
        Assert.Contains("touch-audit.js", rule.Check);

        // the way not to do it, which would take the zoom away from the user (WCAG 1.4.4), is not on any page of the site or of a sample
        foreach (var file in new[] { Path.Combine("Pages", "Shared", "_Head.cshtml"), Path.Combine("Services", "ShellSkeleton.cs") })
        {
            var text = File.ReadAllText(TestPaths.Web(file));
            Assert.Contains("name=\"viewport\"", text);
            Assert.DoesNotContain("maximum-scale", text);
            Assert.DoesNotContain("user-scalable", text);
        }

        Assert.Contains(ComponentCatalog.Get("fields")!.Behaviour, b => b.Contains("16 px type at least", StringComparison.Ordinal));
    }

    [Fact]
    public void The_audit_measures_the_type_of_every_text_field_and_the_browser_checks_look_at_the_components_page()
    {
        var e2e = Path.Combine(TestPaths.RepoRoot, "tests", "e2e");
        var audit = File.ReadAllText(Path.Combine(e2e, "touch-audit.js"));
        var run = File.ReadAllText(Path.Combine(e2e, "run.js"));
        Assert.Contains("const MIN_TYPE = 16;", audit);
        Assert.Contains("function measureType(", audit);
        Assert.Contains("parseFloat(cs.fontSize)", audit);
        Assert.Contains("const ALLOW_TYPE = {", audit);
        Assert.Contains("input:not([type=\"hidden\"])", audit);
        // every exception has its reason, and the audit fails when it saw hardly a field
        foreach (Match m in Regex.Matches(audit, @"\? '(?<id>[a-z-]+)' : ''"))
        {
            Assert.True(Regex.IsMatch(audit, $@"'{m.Groups["id"].Value}':\s*'"), $"the exception '{m.Groups["id"].Value}' of the type measure in touch-audit.js has no reason");
        }

        Assert.Contains("fieldsLooked < 50", audit);
        Assert.Contains("The type of fields on a touch screen, and toasts that wait", run);
        Assert.Contains("every text field of the components page has 16 px type or more", run);
        Assert.Contains("with a mouse a field keeps the type it had", run);
    }
}
