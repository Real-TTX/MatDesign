using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-20: the number stepper, its documentation, its texts, its script and its CSS contract.</summary>
public partial class NumberStepperTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.forms.css");

    [GeneratedRegex(@"(?s)var STEP_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"<div class=""(mat-stepper[^""]*)""([^>]*)>\s*<input class=""([^""]*)""([^>]*)>")]
    private static partial Regex Stepper();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    private static ComponentDoc Component => ComponentCatalog.All.Single(c => c.Id == "number-stepper");

    private static IEnumerable<(string Wrapper, string WrapperAttributes, string InputClass, string InputAttributes)> Steppers() =>
        Component.Examples.SelectMany(e => Stepper().Matches(e.Html).Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value)));

    /// <summary>The script from the stepper's text table to the start of the form helpers that use it.</summary>
    private static string StepperScript()
    {
        var start = Js.IndexOf("var STEP_TEXT", StringComparison.Ordinal);
        Assert.True(start > 0, "the stepper section of the script was not found");
        var end = Js.IndexOf("function initForms(scope)", start, StringComparison.Ordinal);
        return Js[start..end];
    }

    [Fact]
    public void The_stepper_is_documented_among_the_forms_next_to_the_range_and_follows_its_rule()
    {
        Assert.Equal("Forms", Component.Group);
        Assert.Contains("FORM-20", Component.Rules);
        Assert.True(Component.Examples.Count >= 4, "a stepper needs several examples");
        Assert.True(Component.Behaviour.Count >= 4);

        var forms = ComponentCatalog.All.Where(c => c.Group == "Forms").Select(c => c.Id).ToList();
        // the range and the slider come first, then the stepper: the controls for a number
        Assert.True(forms.IndexOf("number-stepper") > forms.IndexOf("range"), "the stepper is before the range");
        Assert.True(forms.IndexOf("number-stepper") - forms.IndexOf("range") <= 2, "the stepper is far from the range");

        var rule = RuleBook.Get("FORM-20");
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.Equal("FORM", rule.Area);
    }

    [Fact]
    public void Every_stepper_of_an_example_is_a_wrapper_around_one_number_input_that_has_a_label_and_limits()
    {
        var steppers = Steppers().ToList();
        Assert.True(steppers.Count >= 8, $"only {steppers.Count} steppers in the examples");
        Assert.All(steppers, s =>
        {
            Assert.Contains("data-stepper", s.WrapperAttributes);
            Assert.Contains("type=\"number\"", s.InputAttributes);
            Assert.Contains("mat-input", s.InputClass.Split(' '));
            Assert.Matches(@"\bid=""[^""]+""", s.InputAttributes);
            Assert.Matches(@"\bname=""[^""]+""", s.InputAttributes);
            Assert.Contains("step=\"", s.InputAttributes);
        });

        foreach (var example in Component.Examples)
        {
            foreach (Match input in Regex.Matches(example.Html, @"<input class=""mat-input[^""]*"" type=""number"" id=""([^""]+)"""))
            {
                Assert.Contains($"for=\"{input.Groups[1].Value}\"", example.Html);
            }
        }
    }

    [Fact]
    public void The_examples_show_the_default_the_stacked_variant_the_sizes_the_unit_and_the_states()
    {
        var steppers = Steppers().ToList();
        Assert.Contains(steppers, s => s.Wrapper == "mat-stepper");
        Assert.Contains(steppers, s => s.Wrapper.Contains("mat-stepper--stacked", StringComparison.Ordinal));
        Assert.Contains(steppers, s => s.InputClass.Contains("mat-input--sm", StringComparison.Ordinal));
        Assert.Contains(steppers, s => s.InputClass.Contains("mat-input--lg", StringComparison.Ordinal));
        Assert.Contains(steppers, s => s.WrapperAttributes.Contains("data-unit=\"", StringComparison.Ordinal));
        Assert.Contains(steppers, s => s.WrapperAttributes.Contains("data-precision=\"", StringComparison.Ordinal));
        Assert.Contains(steppers, s => s.InputAttributes.Contains("disabled", StringComparison.Ordinal));
        Assert.Contains(steppers, s => s.InputAttributes.Contains("readonly", StringComparison.Ordinal));
        Assert.Contains(steppers, s => s.InputAttributes.Contains("required", StringComparison.Ordinal) && s.InputAttributes.Contains("data-msg-required", StringComparison.Ordinal));
        // a step with decimals, and one without an upper limit
        Assert.Contains(steppers, s => Regex.IsMatch(s.InputAttributes, @"step=""0\.\d+"""));
        Assert.Contains(steppers, s => !s.InputAttributes.Contains("max=", StringComparison.Ordinal));
    }

    [Fact]
    public void An_example_with_a_form_has_the_message_line_at_the_field()
    {
        var form = Component.Examples.Single(e => e.Html.Contains("<form", StringComparison.Ordinal)).Html;
        Assert.Contains("novalidate", form);
        Assert.Contains("class=\"mat-error\"", form);
        Assert.Contains("type=\"submit\"", form);
    }

    [Fact]
    public void The_stepper_has_every_text_in_both_languages()
    {
        // The buttons are named for a screen reader: a text that exists in English only would show as its key on a German page.
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the stepper were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 4, $"only {en.Count} texts");
        Assert.Equal(en, de);
        Assert.Equal(["decrease", "decreaseField", "increase", "increaseField"], en);
    }

    [Fact]
    public void The_script_is_documented_in_its_header_and_found_by_refresh()
    {
        Assert.Contains("data-stepper", Js);
        Assert.Contains("data-unit=\"GB\"", Js);
        Assert.Contains("data-precision=\"2\"", Js);
        Assert.Contains("data-stp-<name>", Js);
        // MatUi.refresh calls initForms, and initForms makes the buttons of the steppers that are new
        var forms = Js[Js.IndexOf("function initForms(scope)", StringComparison.Ordinal)..];
        Assert.Contains("initSteppers(scope);", forms[..forms.IndexOf("\n  }", StringComparison.Ordinal)]);
        Assert.Contains("refresh: function (scope) { refreshDependents(scope); fillMeters(scope); initForms(scope);", Js);
    }

    [Fact]
    public void A_button_that_cannot_go_further_is_aria_disabled_and_the_focus_stays()
    {
        var script = StepperScript();
        Assert.Contains("setAttribute('aria-disabled'", script);
        // the only disabled attribute is the one of a disabled field; a limit never sets it
        var assignments = Regex.Matches(script, @"[\w\[\]]+\.disabled = [^;]+;").Select(m => m.Value).ToList();
        Assert.Equal(["b[0].disabled = off;"], assignments);
        // not a stop of the Tab key: the field has the keys
        Assert.Contains("b.tabIndex = -1;", script);
        // a press keeps the focus where it was
        Assert.Contains("addEventListener('mousedown'", script);
    }

    [Fact]
    public void The_buttons_change_the_value_like_the_browser_does_with_the_events_a_form_listens_for()
    {
        var script = StepperScript();
        Assert.Contains("dispatchEvent(new Event('input', { bubbles: true }))", script);
        Assert.Contains("dispatchEvent(new Event('change', { bubbles: true }))", script);
        // the keys of the field
        foreach (var key in new[] { "ArrowUp", "ArrowDown", "PageUp", "PageDown" })
        {
            Assert.Contains($"'{key}'", script);
        }

        // Shift is ten steps, with the keys and with a press
        Assert.Contains("e.shiftKey", script);
        // what is typed is put in order before other listeners hear of it
        Assert.Contains("}, true);", script[script.IndexOf("stepperCommit(s); stepperSync(s);", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void A_held_button_repeats_faster_and_faster_and_stops_when_the_press_ends()
    {
        var script = StepperScript();
        var hold = script[script.IndexOf("function stepperHold", StringComparison.Ordinal)..];
        hold = hold[..hold.IndexOf("function stepperButton", StringComparison.Ordinal)];
        Assert.Contains("Math.pow(", hold);
        Assert.Contains("Math.max(", hold);
        Assert.Contains("setTimeout(tick", hold);
        foreach (var end in new[] { "pointerup", "pointercancel", "contextmenu" })
        {
            Assert.Contains($"'{end}'", script);
        }
    }

    [Fact]
    public void The_arithmetic_is_done_in_whole_numbers_so_that_decimals_have_no_float_errors()
    {
        var script = StepperScript();
        Assert.Contains("Math.pow(10, p)", script);
        Assert.Contains("Math.round(step * k)", script);
        Assert.Contains("Math.round(cur * k)", script);
        // the step base is the one of the browser: min, then the value attribute, then 0
        Assert.Contains("min !== null ? min : (stepNumber(i.getAttribute('value')) || 0)", script);
        // data-precision decides the decimals that are shown
        Assert.Contains("n.toFixed(fixed)", script);
    }

    [Fact]
    public void The_unit_is_read_with_the_field_and_a_changed_value_is_said()
    {
        var script = StepperScript();
        Assert.Contains("aria-describedby", script);
        Assert.Contains("'aria-live', 'polite'", script);
        Assert.Contains("setAttribute('aria-controls'", script);
    }

    [Fact]
    public void Without_the_script_it_is_the_native_number_field_the_spinners_go_only_when_the_buttons_are_there()
    {
        // FORM-20: the class that the script sets is what hides the spinners and squares the corners
        Assert.Contains(".mat-stepper.is-ready > .mat-input::-webkit-inner-spin-button", Css);
        Assert.Contains(".mat-stepper.is-ready > .mat-input::-webkit-outer-spin-button", Css);
        Assert.Contains("appearance: textfield;", Css);
        Assert.Contains("classList.add('is-ready')", StepperScript());
        Assert.DoesNotMatch(@"\n\.mat-stepper > \.mat-input", Css);
    }

    [Fact]
    public void The_sizes_follow_the_input_and_use_the_tokens_of_the_controls()
    {
        Assert.Contains(".mat-stepper:has(> .mat-input--sm) { --mat-stepper-h: var(--mat-control-h-sm);", Css);
        Assert.Contains(".mat-stepper:has(> .mat-input--lg) { --mat-stepper-h: var(--mat-control-h-lg);", Css);
        Assert.Contains("--mat-stepper-h: var(--mat-control-h);", Css);
        Assert.Matches(@"\.mat-stepper__btn \{[^}]*width: var\(--mat-stepper-h\); min-height: var\(--mat-stepper-h\);", Css);
    }

    [Fact]
    public void The_stacked_variant_is_for_a_mouse_and_a_touch_screen_gets_the_default_buttons()
    {
        // MOBILE-04: two arrows that share the height of one control are too small to hit with a finger
        var css = Comment().Replace(Css, string.Empty);
        var at = css.IndexOf("@media (pointer: fine) {", StringComparison.Ordinal);
        Assert.True(at > 0, "the stacked variant has no media query for a mouse");
        Assert.Contains(".mat-stepper--stacked {", css[at..]);
        Assert.Contains(".mat-stepper.mat-stepper--stacked .mat-stepper__btn--inc", css[at..]);
        // outside that query no rule knows the stacked variant, so on a touch screen it is the default
        Assert.DoesNotContain("mat-stepper--stacked", css[..at]);
    }

    [Fact]
    public void A_stepper_in_a_field_with_an_error_or_in_a_disabled_field_looks_like_the_field()
    {
        Assert.Contains(".mat-field.is-invalid .mat-stepper__btn { border-color: var(--mat-danger); }", Css);
        Assert.Contains(".mat-stepper:has(> .mat-input:disabled) .mat-stepper__btn", Css);
        Assert.Contains(".mat-stepper__btn[aria-disabled=\"true\"]", Css);
    }
}
