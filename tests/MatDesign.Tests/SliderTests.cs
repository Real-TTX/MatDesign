using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>FORM-17: the slider and the range slider, their documentation, their texts, their CSS contract and their script.</summary>
public partial class SliderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.forms.css");

    private readonly HttpClient _client;

    public SliderTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // the wrapper of a slider with what is inside it: nothing but the native inputs
    [GeneratedRegex(@"<div class=""(mat-slider[^""]*)""([^>]*)>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex SliderMarkup();

    [GeneratedRegex(@"<input\b([^>]*)>")]
    private static partial Regex InputTag();

    [GeneratedRegex(@"\b(data-[a-z]+(?:-[a-z]+)*)")]
    private static partial Regex DataAttribute();

    [GeneratedRegex(@"(?s)var SL_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    private static ComponentDoc Doc => ComponentCatalog.All.Single(c => c.Id == "slider");

    private sealed record Slider(string Example, bool Range, string Attributes, IReadOnlyList<string> Inputs, string Html);

    private static IEnumerable<Slider> Sliders() =>
        Doc.Examples.SelectMany(e => SliderMarkup().Matches(e.Html).Select(m => new Slider(
            e.Title,
            m.Groups[1].Value.Contains("mat-slider--range", StringComparison.Ordinal),
            m.Groups[2].Value,
            InputTag().Matches(m.Groups[3].Value).Select(i => i.Groups[1].Value).ToList(),
            e.Html)));

    private static string Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $@"(?<![\w-]){Regex.Escape(name)}=""([^""]*)""");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    private static string Section(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{from}' was not found");
        var rest = text[start..];
        var end = rest.IndexOf(to, StringComparison.Ordinal);
        Assert.True(end > 0, $"'{to}' was not found after '{from}'");
        return rest[..end];
    }

    private static string ScriptSection => Section(Js, "var SL_TEXT", "carousel (LIST-13, matui.lists.css)");

    private static string StyleSection => Css[Css.IndexOf("Slider and range slider (FORM-17)", StringComparison.Ordinal)..];

    // ---------------------------------------------------------------------------------------------------- the documentation

    [Fact]
    public void The_slider_is_documented_among_the_forms_next_to_the_range_and_follows_its_rule()
    {
        Assert.Equal("Forms", Doc.Group);
        foreach (var rule in new[] { "FORM-17", "FORM-12", "A11Y-02", "MOBILE-04" })
        {
            Assert.Contains(rule, Doc.Rules);
        }

        Assert.True(Doc.Examples.Count >= 6, $"only {Doc.Examples.Count} examples");
        Assert.True(Doc.Behaviour.Count >= 5, $"only {Doc.Behaviour.Count} lines of behaviour");

        var all = ComponentCatalog.All.Select(c => c.Id).ToList();
        Assert.Equal(all.IndexOf("range") + 1, all.IndexOf("slider"));

        var rule17 = RuleBook.Get("FORM-17");
        Assert.NotNull(rule17);
        Assert.Equal(RuleLevel.Should, rule17.Level);
        Assert.Equal(RuleBook.Design, rule17.Source);
        Assert.Equal("FORM", rule17.Area);
    }

    [Fact]
    public void The_examples_show_a_slider_a_range_slider_a_time_of_day_a_price_a_form_a_disabled_one_and_a_dialog()
    {
        var sliders = Sliders().ToList();
        Assert.True(sliders.Count(s => !s.Range) >= 6, "single sliders");
        Assert.True(sliders.Count(s => s.Range) >= 4, "range sliders");
        Assert.Contains(Doc.Examples, e => e.Title.Contains("Time of day", StringComparison.Ordinal));
        Assert.Contains(sliders, s => s.Attributes.Contains("data-format=\"{0} €\"", StringComparison.Ordinal) && s.Attributes.Contains("data-range-format=\"From {0} to {1}\"", StringComparison.Ordinal));
        Assert.Contains(sliders, s => s.Inputs.Any(i => i.Contains(" disabled", StringComparison.Ordinal)));
        Assert.Contains(sliders, s => s.Attributes.Contains("data-bubble=\"always\"", StringComparison.Ordinal));
        Assert.Contains(sliders, s => s.Attributes.Contains("data-ticks", StringComparison.Ordinal));
        Assert.Contains(Doc.Examples, e => e.Html.Contains("<form ", StringComparison.Ordinal) && e.Html.Contains("type=\"reset\"", StringComparison.Ordinal));
        Assert.Contains(Doc.Examples, e => e.Html.Contains("<dialog ", StringComparison.Ordinal) && SliderMarkup().IsMatch(e.Html));
        Assert.Contains(sliders, s => s.Html.Contains("mat-slider--sm", StringComparison.Ordinal));
        Assert.Contains(sliders, s => s.Html.Contains("mat-slider--lg", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_slider_is_a_wrapper_around_native_range_inputs_that_post_their_value()
    {
        var sliders = Sliders().ToList();
        Assert.True(sliders.Count >= 10, $"only {sliders.Count} sliders in the examples");
        Assert.All(sliders, s =>
        {
            Assert.Contains("data-slider", s.Attributes);
            Assert.Equal(s.Range ? 2 : 1, s.Inputs.Count);
            Assert.All(s.Inputs, input =>
            {
                Assert.Contains("class=\"mat-slider__input\"", input);
                Assert.Contains("type=\"range\"", input);
                foreach (var name in new[] { "id", "name", "min", "max", "value" })
                {
                    Assert.True(Attribute(input, name).Length > 0, $"{s.Example}: an input without {name}: {input}");
                }
            });
        });
    }

    [Fact]
    public void The_values_of_the_examples_are_on_the_grid_inside_the_scale_and_a_range_keeps_its_gap()
    {
        foreach (var s in Sliders())
        {
            var gap = s.Attributes.Contains("data-min-gap", StringComparison.Ordinal) ? Number(Attribute(s.Attributes, "data-min-gap")) : 0;
            var values = new List<double>();
            foreach (var input in s.Inputs)
            {
                var (min, max, value) = (Number(Attribute(input, "min")), Number(Attribute(input, "max")), Number(Attribute(input, "value")));
                var step = Attribute(input, "step").Length > 0 ? Number(Attribute(input, "step")) : 1;
                Assert.True(value >= min && value <= max, $"{s.Example}: {value} is outside {min}..{max}");
                Assert.True(Math.Abs((value - min) / step - Math.Round((value - min) / step)) < 1e-9, $"{s.Example}: {value} is not on the grid of {step} from {min}");
                values.Add(value);
            }

            if (s.Range)
            {
                Assert.True(values[1] - values[0] >= gap, $"{s.Example}: from {values[0]} to {values[1]} is closer than the gap {gap}");
                Assert.Equal(Attribute(s.Inputs[0], "min"), Attribute(s.Inputs[1], "min"));
                Assert.Equal(Attribute(s.Inputs[0], "max"), Attribute(s.Inputs[1], "max"));
                Assert.Equal(Attribute(s.Inputs[0], "step"), Attribute(s.Inputs[1], "step"));
                Assert.NotEqual(Attribute(s.Inputs[0], "name"), Attribute(s.Inputs[1], "name"));
            }
        }
    }

    [Fact]
    public void A_slider_has_a_visible_label_that_points_to_it_or_a_name_of_its_own_and_a_range_slider_points_from_its_first_input()
    {
        foreach (var s in Sliders())
        {
            var first = s.Inputs[0];
            var id = Attribute(first, "id");
            Assert.True(s.Html.Contains($"<label class=\"mat-label\" for=\"{id}\"", StringComparison.Ordinal) || first.Contains("aria-label=\"", StringComparison.Ordinal),
                $"{s.Example}: {id} has neither a label nor an aria-label");
            if (s.Range)
            {
                // the script names the two inputs from this label ("Price from", "Price to")
                Assert.Contains($"<label class=\"mat-label\" for=\"{id}\"", s.Html);
                Assert.DoesNotContain("aria-label=", first);
            }
        }
    }

    [Fact]
    public void Every_template_has_the_number_in_it_and_every_output_is_an_element_of_the_example()
    {
        foreach (var s in Sliders())
        {
            if (s.Attributes.Contains("data-format=", StringComparison.Ordinal))
            {
                Assert.Contains("{0}", Attribute(s.Attributes, "data-format"));
            }

            if (s.Attributes.Contains("data-range-format=", StringComparison.Ordinal))
            {
                Assert.True(s.Range, s.Example + ": data-range-format is for a range slider");
                Assert.Contains("{0}", Attribute(s.Attributes, "data-range-format"));
                Assert.Contains("{1}", Attribute(s.Attributes, "data-range-format"));
            }

            if (s.Attributes.Contains("data-output=", StringComparison.Ordinal))
            {
                var output = Attribute(s.Attributes, "data-output");
                Assert.Contains($"<output class=\"mat-range-out\" id=\"{output}\"", s.Html);
            }

            // the labels under the marks need marks
            Assert.True(!s.Attributes.Contains("data-tick-labels", StringComparison.Ordinal) || s.Attributes.Contains("data-ticks", StringComparison.Ordinal), s.Example);
        }
    }

    [Fact]
    public void Every_attribute_an_example_uses_is_documented_in_the_header_of_the_script()
    {
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        var used = Sliders().SelectMany(s => DataAttribute().Matches(s.Attributes).Select(m => m.Groups[1].Value)).Distinct().ToList();
        Assert.True(used.Count >= 8, $"only {used.Count} attributes");
        Assert.All(used, a => Assert.Contains(a, header));
    }

    [Fact]
    public async Task The_page_draws_the_examples_and_the_rule_has_its_own_page()
    {
        var page = await _client.GetAsync("/Components/slider");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("id=\"slider\"", html);
        Assert.Contains("class=\"mat-slider mat-slider--range\"", html);
        Assert.Contains("href=\"/Rules/Detail?id=FORM-17\"", html);

        var rule = await _client.GetAsync("/Rules/Detail?id=FORM-17");
        Assert.Equal(HttpStatusCode.OK, rule.StatusCode);
        Assert.Contains("Slider and range slider", await rule.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------------------------- the texts

    [Fact]
    public void The_slider_has_every_text_in_both_languages()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the slider were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.Equal(["from", "range", "to"], en);
        Assert.Equal(en, de);
        // the names are made from the label and these words: "Price from", "Preis von"
        Assert.Contains("from: 'von', to: 'bis'", match.Groups[2].Value);
    }

    // ---------------------------------------------------------------------------------------------------- the script

    [Fact]
    public void The_script_builds_a_thumb_and_a_bubble_per_input_and_names_them_for_a_screen_reader()
    {
        var script = ScriptSection;
        Assert.Contains("aria-valuetext", script);
        Assert.Contains("'mat-slider__thumb'", script);
        Assert.Contains("'mat-slider__bubble'", script);
        Assert.Contains("setAttribute('aria-label', name + ' ' + slText(i === 0 ? 'from' : 'to'))", script);
        Assert.Contains("el.setAttribute('role', 'group')", script);
        // what is drawn is for the eyes: the native inputs are what a screen reader reads
        Assert.Contains("s.rail.setAttribute('aria-hidden', 'true')", script);
        Assert.Contains("scale.setAttribute('aria-hidden', 'true')", script);
        // the values are text, never markup
        Assert.DoesNotContain("innerHTML", script);
        Assert.Contains("textContent", script);
    }

    [Fact]
    public void The_thumbs_of_a_range_slider_cannot_cross_and_the_keys_and_the_pointer_are_handled()
    {
        var script = ScriptSection;
        Assert.Contains("other - s.gap", script);
        Assert.Contains("other + s.gap", script);
        foreach (var key in new[] { "ArrowRight", "ArrowLeft", "ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End" })
        {
            Assert.Contains($"'{key}'", script);
        }

        // a tenth of the scale, at least one step
        Assert.Contains("Math.max(s.step, (s.max - s.min) / 10)", script);
        Assert.Contains("setPointerCapture", script);
        Assert.Contains("'pointercancel'", script);
        Assert.Contains("is-top", script);
        // input while it moves and change when it is over, like a native input (a bubbling event, so a form and a delegated listener see it)
        Assert.Contains("new Event('input', { bubbles: true })", script);
        Assert.Contains("new Event('change', { bubbles: true })", script);
    }

    [Fact]
    public void A_value_that_breaks_the_gap_is_put_right_before_anybody_reads_it_and_a_reset_is_picked_up()
    {
        var script = ScriptSection;
        // the capture phase of the document runs before the listeners of the input and of the page
        Assert.Matches(@"doc\.addEventListener\('input', function \(e\) \{.*?\}, true\);", script.Replace("\n", " "));
        Assert.Contains("doc.addEventListener('reset'", script);
        Assert.Contains("setTimeout(function ()", script);
        Assert.Contains("slNormalize(s)", script);
    }

    [Fact]
    public void The_script_hides_nothing_with_the_hidden_attribute_and_looks_at_a_slider_in_a_closed_dialog_again()
    {
        var script = ScriptSection;
        Assert.DoesNotContain(".hidden =", script);
        Assert.DoesNotContain("setAttribute('hidden'", script);
        Assert.Contains("new ResizeObserver", script);
    }

    [Fact]
    public void New_sliders_are_picked_up_by_initForms_and_refresh_and_the_api_has_init_and_update()
    {
        var forms = Section(Js, "function initForms(scope) {", "doc.addEventListener('input'");
        Assert.Contains("initSliders(scope);", forms);
        var api = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains("slider: { init: initSliders, update: slUpdate }", api);
        Assert.Contains("initForms(scope)", api);
    }

    // ---------------------------------------------------------------------------------------------------- the style

    [Fact]
    public void The_thumb_is_a_target_of_44_px_and_a_small_slider_is_a_compact_control_of_36_px()
    {
        // MOBILE-04
        Assert.Matches(@"\.mat-slider \{[^}]*--mat-slider-hit: 44px;", Css);
        Assert.Matches(@"\.mat-slider--sm \{[^}]*--mat-slider-hit: 36px;", Css);
        Assert.Matches(@"\.mat-slider--lg \{[^}]*--mat-slider-hit: 52px;", Css);
        Assert.Matches(@"\.mat-slider__thumb \{[^}]*width: var\(--mat-slider-hit\); height: var\(--mat-slider-hit\);", Css);
        Assert.Matches(@"\.mat-slider__rail \{[^}]*height: var\(--mat-slider-hit\);", Css);
        // what is seen is a knob, smaller than the target
        Assert.Matches(@"\.mat-slider \{[^}]*--mat-slider-knob: 20px;", Css);
    }

    [Fact]
    public void A_press_on_the_rail_is_the_scripts_and_the_page_can_still_be_scrolled_up_and_down()
    {
        Assert.Matches(@"\.mat-slider__rail \{[^}]*touch-action: pan-y;", Css);
        Assert.Matches(@"\.mat-slider__rail \{[^}]*user-select: none;", Css);
        // the bubble and the labels never take a press
        Assert.Matches(@"\.mat-slider__bubble \{[^}]*pointer-events: none;", Css);
        Assert.Matches(@"\.mat-slider__scale \{[^}]*pointer-events: none;", Css);
    }

    [Fact]
    public void Without_the_script_the_native_control_is_drawn_and_with_it_the_inputs_are_invisible_but_not_hidden()
    {
        Assert.Contains(".mat-slider__input::-webkit-slider-thumb", Css);
        Assert.Contains(".mat-slider__input::-moz-range-thumb", Css);
        Assert.Contains(".mat-slider--range:not(.is-ready) { display: flex; flex-direction: column;", Css);

        // display: none and visibility: hidden would take the input out of the Tab order and away from a screen reader
        var rule = Regex.Match(Css, @"\.mat-slider\.is-ready \.mat-slider__input \{([^}]*)\}");
        Assert.True(rule.Success);
        Assert.Contains("opacity: 0;", rule.Groups[1].Value);
        Assert.Contains("pointer-events: none;", rule.Groups[1].Value);
        Assert.DoesNotContain("display: none", rule.Groups[1].Value);
        Assert.DoesNotContain("visibility", rule.Groups[1].Value);
        Assert.DoesNotContain("display: none", StyleSection);
        Assert.DoesNotContain("visibility: hidden", StyleSection);
    }

    [Fact]
    public void A_bubble_that_stays_has_room_above_the_slider_and_the_bubbles_show_on_drag_and_focus()
    {
        Assert.Contains(".mat-slider.is-ready[data-bubble=\"always\"] { padding-top: 20px; }", Css);
        Assert.Contains(".mat-slider__thumb.is-focus > .mat-slider__bubble", Css);
        Assert.Contains(".mat-slider__thumb.is-drag > .mat-slider__bubble", Css);
        // two near thumbs share one bubble
        Assert.Contains(".mat-slider.is-merged .mat-slider__thumb--from > .mat-slider__bubble { opacity: 0; }", Css);
        Assert.Matches(@"\.mat-slider__bubble \{[^}]*background: var\(--mat-text\); color: var\(--mat-bg\);", Css);
    }

    [Fact]
    public void The_thumb_on_top_is_the_one_moved_last_and_a_disabled_slider_is_dimmed()
    {
        Assert.Contains(".mat-slider__thumb.is-top { z-index: 2; }", Css);
        // every input disabled (also by a fieldset): the wrapper is dimmed and its rail says so
        Assert.Contains(".mat-slider:not(:has(.mat-slider__input:enabled)) { opacity: .5; }", Css);
        Assert.Contains("cursor: not-allowed;", Css[Css.IndexOf(".mat-slider:not(:has(.mat-slider__input:enabled)) .mat-slider__rail", StringComparison.Ordinal)..]);
        // the focus is a ring from the token, on the keyboard and while dragging
        Assert.Contains(".mat-slider__thumb.is-key::before, .mat-slider__thumb.is-drag::before { box-shadow: var(--mat-focus-ring); }", Css);
    }

    [Fact]
    public void The_slider_is_flat_and_made_of_tokens_only()
    {
        var style = StyleSection;
        Assert.DoesNotContain("!important", style);
        Assert.DoesNotContain("gradient", style);
        Assert.DoesNotContain("blur", style);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(", style[style.IndexOf("*/", StringComparison.Ordinal)..]);
        Assert.Contains("var(--mat-accent)", style);
        Assert.Contains("var(--mat-surface-3)", style);
        Assert.Contains("var(--mat-thumb)", style);
        // the old range stays where it was, with its contract
        Assert.Contains(".mat-range { --mat-range: 50%;", Css);
        Assert.True(Css.IndexOf(".mat-range {", StringComparison.Ordinal) < Css.IndexOf("Slider and range slider (FORM-17)", StringComparison.Ordinal));
    }
}
