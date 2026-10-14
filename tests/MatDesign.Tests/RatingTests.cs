using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-22: the rating, its documentation, its texts, its script and its CSS contract.</summary>
public partial class RatingTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.forms.css");
    private static readonly string Sprite = TestPaths.ReadToolkit("img/matui-icons.svg");

    [GeneratedRegex(@"(?s)var RT_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"(?s)<fieldset class=""([^""]*mat-rating[^""]*)""([^>]*)>(.*?)</fieldset>")]
    private static partial Regex Choice();

    [GeneratedRegex(@"<span class=""(mat-rating[^""]*)""([^>]*)>([^<]*)</span>")]
    private static partial Regex Shown();

    [GeneratedRegex(@"<input class=""mat-rating__input"" type=""radio"" id=""([^""]+)"" name=""([^""]+)"" value=""([^""]+)""([^>]*)/>")]
    private static partial Regex Radio();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    private static ComponentDoc Component => ComponentCatalog.All.Single(c => c.Id == "rating");

    private static IEnumerable<(string Classes, string Attributes, string Inner)> Choices() =>
        Component.Examples.SelectMany(e => Choice().Matches(e.Html).Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)));

    private static IEnumerable<(string Classes, string Attributes, string Text)> Displays() =>
        Component.Examples.SelectMany(e => Shown().Matches(e.Html).Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)));

    /// <summary>The script of the rating, from its text table to the start of the init section.</summary>
    private static string RatingScript()
    {
        var start = Js.IndexOf("var RT_TEXT", StringComparison.Ordinal);
        Assert.True(start > 0, "the rating section of the script was not found");
        // to the heading of the next section (other controls follow it in the file)
        var end = Js.IndexOf("\n  // ------", start, StringComparison.Ordinal);
        Assert.True(end > start, "the end of the rating section of the script was not found");
        return Js[start..end];
    }

    /// <summary>The rules of the rating in the stylesheet (from its heading to the end of the file).</summary>
    private static string RatingCss()
    {
        var at = Css.IndexOf("/* ---- Rating (FORM-22)", StringComparison.Ordinal);
        Assert.True(at > 0, "the rating section of the stylesheet was not found");
        return Css[at..];
    }

    [Fact]
    public void The_rating_is_documented_among_the_forms_and_follows_its_rule()
    {
        Assert.Equal("Forms", Component.Group);
        Assert.Contains("FORM-22", Component.Rules);
        Assert.True(Component.Examples.Count >= 6, "a rating needs several examples");
        Assert.True(Component.Behaviour.Count >= 6);

        var rule = RuleBook.Get("FORM-22");
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.Equal("FORM", rule.Area);
        Assert.True(rule.Statement.Length <= 1200, $"the statement has {rule.Statement.Length} characters");
    }

    [Fact]
    public void Every_choice_of_an_example_is_a_fieldset_with_a_legend_and_radios_that_share_a_name_and_have_labels()
    {
        var choices = Choices().ToList();
        Assert.True(choices.Count >= 7, $"only {choices.Count} ratings to choose from in the examples");
        Assert.All(choices, c =>
        {
            Assert.Contains("data-rating", c.Attributes);
            Assert.Contains("<legend class=\"mat-legend\">", c.Inner);
            var radios = Radio().Matches(c.Inner).ToList();
            Assert.True(radios.Count is >= 3 and <= 21, $"{radios.Count} radios");
            Assert.Single(radios.Select(r => r.Groups[2].Value).Distinct());
            foreach (var radio in radios)
            {
                Assert.Contains($"<label class=\"mat-rating__label\" for=\"{radio.Groups[1].Value}\">", c.Inner);
            }

            // at most one is checked
            Assert.True(radios.Count(r => r.Groups[4].Value.Contains("checked", StringComparison.Ordinal)) <= 1);
            // the values are numbers, in order
            var values = radios.Select(r => double.Parse(r.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(values.Order().ToList(), values);
        });
    }

    [Fact]
    public void The_examples_show_half_steps_the_shapes_the_sizes_clearing_a_disabled_group_and_a_required_one()
    {
        var choices = Choices().ToList();
        Assert.Contains(choices, c => Regex.IsMatch(c.Inner, @"value=""0\.5"""));
        Assert.Contains(choices, c => c.Classes.Contains("mat-rating--heart", StringComparison.Ordinal));
        Assert.Contains(choices, c => c.Classes.Contains("mat-rating--dot", StringComparison.Ordinal));
        Assert.Contains(choices, c => c.Classes.Contains("mat-rating--sm", StringComparison.Ordinal));
        Assert.Contains(choices, c => c.Classes.Contains("mat-rating--lg", StringComparison.Ordinal));
        Assert.Contains(choices, c => c.Attributes.Contains("data-clearable", StringComparison.Ordinal) && c.Inner.Contains("value=\"0\"", StringComparison.Ordinal));
        Assert.Contains(choices, c => c.Attributes.Contains("disabled", StringComparison.Ordinal));
        Assert.Contains(choices, c => c.Inner.Contains(" required", StringComparison.Ordinal) && c.Inner.Contains("data-msg-required=", StringComparison.Ordinal) && c.Inner.Contains("class=\"mat-error\"", StringComparison.Ordinal));
        // a scale of ten and one of three
        Assert.Contains(choices, c => Radio().Matches(c.Inner).Count == 10);
        Assert.Contains(choices, c => Radio().Matches(c.Inner).Count == 3);
        Assert.Contains(choices, c => c.Attributes.Contains("data-show-value", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_display_has_a_value_the_text_that_is_seen_without_the_script_and_no_inline_style()
    {
        var displays = Displays().Where(d => d.Classes.Contains("mat-rating--readonly", StringComparison.Ordinal)).ToList();
        Assert.True(displays.Count >= 8, $"only {displays.Count} read-only ratings in the examples");
        Assert.All(displays, d =>
        {
            Assert.Contains("data-rating", d.Attributes);
            Assert.Matches(@"data-value=""[0-9.]+""", d.Attributes);
            Assert.False(string.IsNullOrWhiteSpace(d.Text), "the content that is seen without the script");
            Assert.DoesNotContain("style=", d.Attributes);
        });
        // a value that is not a half, a count, the value as text, no value, ten dots
        Assert.Contains(displays, d => Regex.IsMatch(d.Attributes, @"data-value=""3\.7"""));
        Assert.Contains(displays, d => d.Attributes.Contains("data-count=", StringComparison.Ordinal));
        Assert.Contains(displays, d => d.Attributes.Contains("data-show-value", StringComparison.Ordinal));
        Assert.Contains(displays, d => d.Attributes.Contains("data-value=\"0\"", StringComparison.Ordinal));
        Assert.Contains(displays, d => d.Attributes.Contains("data-max=\"10\"", StringComparison.Ordinal));
        Assert.Contains(displays, d => d.Classes.Contains("mat-rating--heart", StringComparison.Ordinal));
        Assert.Contains(displays, d => d.Attributes.Contains("data-count=\"1\"", StringComparison.Ordinal));
    }

    [Fact]
    public void The_rating_has_every_text_in_both_languages()
    {
        // The label of a read-only rating is said by a screen reader: a text that exists in English only would show as its key on a German page.
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the rating were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.Equal(en, de);
        Assert.Equal(["notRated", "rated", "ratedCount", "ratedOne"], en);
        // the placeholders agree as well
        foreach (var key in en)
        {
            var enText = Regex.Match(match.Groups[1].Value, key + @": '([^']*)'").Groups[1].Value;
            var deText = Regex.Match(match.Groups[2].Value, key + @": '([^']*)'").Groups[1].Value;
            Assert.Equal(Regex.Matches(enText, @"\{\d\}").Select(m => m.Value).Order(), Regex.Matches(deText, @"\{\d\}").Select(m => m.Value).Order());
        }
    }

    [Fact]
    public void The_script_is_documented_in_its_header_started_with_the_page_and_found_by_refresh()
    {
        Assert.Contains("data-rating ", Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)]);
        Assert.Contains("data-rt-<name>", Js);
        Assert.Contains("mat:rating-change", Js);
        var init = Js[Js.IndexOf("  function init() {", StringComparison.Ordinal)..];
        Assert.Contains("initRatings();", init[..init.IndexOf("\n  }", StringComparison.Ordinal)]);
        Assert.Contains("initRatings(scope);", Js[Js.IndexOf("    refresh: function (scope)", StringComparison.Ordinal)..]);
        Assert.Contains("rating: { init: initRatings, get: ratGet, set: ratSet },", Js);
    }

    [Fact]
    public void The_radios_stay_the_truth_a_choice_is_a_native_change_and_the_page_is_told_after_it()
    {
        var script = RatingScript();
        // the radio is checked and the browser's events follow, then the toolkit's own
        Assert.Contains("hit.el.checked = true;", script);
        Assert.Contains("new Event('input', { bubbles: true })", script);
        Assert.Contains("new Event('change', { bubbles: true })", script);
        Assert.Contains("new CustomEvent('mat:rating-change', { bubbles: true, detail: { value: ratValue(r), rating: r.root } })", script);
        // "no rating" is the radio with the value 0, or no radio at all
        Assert.Contains("r.none.el.checked = true;", script);
        Assert.Contains("on.el.checked = false;", script);
        // a group that a fieldset disables is not changed by the pointer
        Assert.Contains("matches(':disabled')", script);
        // the radios are never taken out of the page: the keyboard and the screen reader need them
        Assert.DoesNotContain(".hidden = ", script);
        Assert.DoesNotContain("tabIndex", script);
    }

    [Fact]
    public void The_pointer_previews_only_with_a_mouse_or_a_pen_and_a_click_chooses_where_it_is()
    {
        var script = RatingScript();
        Assert.Contains("e.pointerType !== 'touch'", script);
        Assert.Contains("addEventListener('pointerleave'", script);
        Assert.Contains("addEventListener('click'", script);
        // the value under the pointer is the first value that can be chosen at or above its place: half values exist where there are radios for them
        Assert.Contains("r.values[i] >= raw - 1e-9", script);
        Assert.Contains("getBoundingClientRect()", script);
        // a click on the chosen value clears it only when the author said so
        Assert.Contains("v === ratValue(r) && r.clearable", script);
        Assert.Contains("el.hasAttribute('data-clearable')", script);
    }

    [Fact]
    public void A_read_only_rating_is_an_image_with_a_label_in_words_that_the_author_can_replace()
    {
        var script = RatingScript();
        Assert.Contains("setAttribute('role', 'img')", script);
        Assert.Contains("setAttribute('aria-label'", script);
        Assert.Contains("!el.hasAttribute('aria-label')", script);
        Assert.Contains("ratText('ratedCount'", script);
        // the number of shapes is 3 to 10
        Assert.Contains("Math.max(3, Math.min(10, max))", script);
        // the shapes are hidden from a screen reader, the label says it all
        Assert.Contains("setAttribute('aria-hidden', 'true')", script);
        // the fill is data: a custom property written by the script, no style text
        Assert.Contains("style.setProperty('--mat-rating-fill'", script);
        Assert.DoesNotContain(".style.cssText", script);
        Assert.DoesNotContain("style.width", script);
    }

    [Fact]
    public void The_shapes_come_from_the_sprite_and_the_sprite_has_them()
    {
        var script = RatingScript();
        Assert.Contains("heart: 'heart', dot: 'circle'", script);
        Assert.Contains("var name = 'star'", script);
        foreach (var name in new[] { "star", "heart", "circle" })
        {
            Assert.Contains($"id=\"mat-i-{name}\"", Sprite);
        }
    }

    [Fact]
    public void Without_the_script_it_is_a_row_of_radio_buttons_and_the_script_hides_them_without_taking_them_out()
    {
        var css = Comment().Replace(RatingCss(), string.Empty);
        Assert.Contains("fieldset.mat-rating:not(.is-ready) { display: flex;", css);
        Assert.Contains(".mat-rating.is-ready > .mat-rating__input, .mat-rating.is-ready > .mat-rating__label {", css);
        // invisible, never display: none (a radio that is not displayed has no focus)
        Assert.DoesNotContain("display: none", css);
        Assert.DoesNotContain("visibility: hidden", css);
        Assert.Contains("classList.add('is-ready')", RatingScript());
    }

    [Fact]
    public void The_filled_shapes_are_clipped_to_a_share_of_the_row_and_every_cell_has_the_same_width()
    {
        var css = Comment().Replace(RatingCss(), string.Empty);
        Assert.Contains("clip-path: inset(0 calc(100% - var(--mat-rating-fill)) 0 0);", css);
        Assert.Contains("grid-template-columns: repeat(var(--mat-rating-max), minmax(0, var(--mat-rating-w)));", css);
        // a shape that is filled is not only another colour: the empty one is an outline
        Assert.Contains(".mat-rating__layer--fill .mat-rating__icon { fill: currentColor; }", css);
        Assert.Contains("color: var(--mat-accent);", css);
    }

    [Fact]
    public void The_targets_are_as_high_as_a_control_and_shrink_in_width_rather_than_overflow()
    {
        var css = Comment().Replace(RatingCss(), string.Empty);
        // 40 px on a mouse and 44 px on a touch screen (the token changes), 36 px for a small one (MOBILE-04)
        Assert.Contains("--mat-rating-h: var(--mat-control-h);", css);
        Assert.Contains(".mat-rating--sm { --mat-rating-icon: 16px; --mat-rating-h: var(--mat-control-h-sm); }", css);
        Assert.Contains(".mat-rating--lg { --mat-rating-icon: 30px; --mat-rating-h: var(--mat-control-h-lg); }", css);
        Assert.Contains("minmax(0, var(--mat-rating-w))", css);
        Assert.Contains("max-width: 100%", css);
    }

    [Fact]
    public void It_is_drawn_from_the_tokens_with_the_focus_ring_the_error_and_the_disabled_state_of_a_field()
    {
        var css = Comment().Replace(RatingCss(), string.Empty);
        Assert.Contains(".mat-rating:has(.mat-rating__input:focus-visible) .mat-rating__stars { box-shadow: var(--mat-focus-ring); }", css);
        Assert.Contains(".mat-field.is-invalid .mat-rating__layer--empty { color: var(--mat-danger); }", css);
        Assert.Contains(".mat-rating:disabled .mat-rating__stars", css);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", css);
        Assert.DoesNotMatch(@"\brgba?\(", css);
        Assert.DoesNotContain("!important", css);
        Assert.DoesNotContain("gradient", css);
        Assert.DoesNotContain("blur", css);
        // flat: nothing lifts or glows
        Assert.DoesNotContain("transform", css);
    }
}
