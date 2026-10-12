using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-19: the code input, its documentation, its texts, its CSS contract and what its script promises.</summary>
public partial class CodeInputTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.forms.css");

    [GeneratedRegex(@"(?s)var OTP_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"<div class=""mat-otp([^""]*)""([^>]*)>\s*<input class=""mat-otp__input"" id=""([^""]+)""([^>]*)>", RegexOptions.Singleline)]
    private static partial Regex Wrapper();

    [GeneratedRegex(@"otpText\('([A-Za-z]+)'")]
    private static partial Regex TextCall();

    [GeneratedRegex(@"\b(?:name|only): '([A-Za-z]+)'")]
    private static partial Regex KindText();

    private static ComponentDoc Component => ComponentCatalog.All.Single(c => c.Id == "code-input");

    private static IEnumerable<(string Classes, string Attributes, string Id, string Input)> Wrappers() =>
        Component.Examples.SelectMany(e => Wrapper().Matches(e.Html).Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value)));

    /// <summary>The code of the section of the script that draws the code input.</summary>
    private static string Section
    {
        get
        {
            var from = Js.IndexOf("// ---- code input (FORM-19", StringComparison.Ordinal);
            if (from < 0)
            {
                from = Js.IndexOf("code input (FORM-19, matui.forms.css)", StringComparison.Ordinal);
            }

            var to = Js.IndexOf("// ---- the events of the pickers", StringComparison.Ordinal);
            Assert.True(from > 0 && to > from, "the section of the code input was not found");
            return Js[from..to];
        }
    }

    private static string Block(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{selector} is not in the stylesheet");
        return css[start..css.IndexOf('}', start)];
    }

    [Fact]
    public void The_code_input_is_documented_among_the_forms_and_follows_its_rule()
    {
        Assert.Equal("Forms", Component.Group);
        Assert.Contains("FORM-19", Component.Rules);
        Assert.True(Component.Examples.Count >= 6, $"only {Component.Examples.Count} examples");
        Assert.True(Component.Behaviour.Count >= 8);
        var rule = RuleBook.Get("FORM-19");
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.False(string.IsNullOrWhiteSpace(rule.Do));
        Assert.False(string.IsNullOrWhiteSpace(rule.Dont));
    }

    [Fact]
    public void Every_code_input_of_an_example_is_a_wrapper_around_a_real_input_that_a_label_points_to()
    {
        var wrappers = Wrappers().ToList();
        Assert.True(wrappers.Count >= 10, $"only {wrappers.Count} code inputs in the examples");
        Assert.All(wrappers, w =>
        {
            Assert.Contains("data-otp", w.Attributes);
            Assert.Contains(Component.Examples, e => e.Html.Contains($"for=\"{w.Id}\"", StringComparison.Ordinal));
            Assert.Contains("name=\"", w.Input);
        });
    }

    [Fact]
    public void The_options_of_the_examples_are_the_ones_the_script_knows()
    {
        var names = new[] { "digits", "letters", "alphanumeric", "hex" };
        foreach (var w in Wrappers())
        {
            var length = Regex.Match(w.Attributes, @"data-length=""(\d+)""");
            Assert.True(length.Success && int.Parse(length.Groups[1].Value) is >= 3 and <= 12, $"{w.Id}: data-length is 3 to 12");
            var chars = Regex.Match(w.Attributes, @"data-chars=""([^""]+)""");
            Assert.True(!chars.Success || names.Contains(chars.Groups[1].Value) || Regex.IsMatch(chars.Groups[1].Value, @"^\[.+\]$"), $"{w.Id}: data-chars");
            var casing = Regex.Match(w.Attributes, @"data-case=""([^""]+)""");
            Assert.True(!casing.Success || casing.Groups[1].Value is "upper" or "lower", $"{w.Id}: data-case");
            var group = Regex.Match(w.Attributes, @"data-group=""([^""]+)""");
            Assert.True(!group.Success || Regex.IsMatch(group.Groups[1].Value, @"^\d+([- ]\d+)*$"), $"{w.Id}: data-group");
        }
    }

    [Fact]
    public void The_examples_show_the_kinds_of_code_and_the_states()
    {
        var html = string.Join("\n", Component.Examples.Select(e => e.Html));
        foreach (var part in new[]
                 {
                     "data-chars=\"digits\"", "data-chars=\"alphanumeric\"", "data-chars=\"[A-Z2-7]\"", "data-mask", "data-group=\"4-4\"", "data-group=\"4\"", "data-case=\"upper\"",
                     "data-submit>", "data-submit=\"#otp-signin-go\"", "mat-otp--sm", "mat-otp--lg", "mat-otp--center", "disabled", "readonly", "mat-field is-invalid", "aria-invalid=\"true\"",
                     "autocomplete=\"one-time-code\"", "required", "pattern=\"[0-9]{6}\"",
                 })
        {
            Assert.Contains(part, html);
        }
    }

    [Fact]
    public void A_form_that_sends_itself_is_the_authors_decision_and_a_pin_never_does()
    {
        var examples = Component.Examples;
        var totp = examples.Single(e => e.Html.Contains("id=\"otp-totp\"", StringComparison.Ordinal));
        Assert.Contains("data-submit", totp.Html);
        Assert.Contains("type=\"submit\"", totp.Html);
        Assert.DoesNotContain("data-submit", examples.Single(e => e.Html.Contains("id=\"otp-pin\"", StringComparison.Ordinal)).Html);
        // every example that sends by itself also has the button, for the one who does not wait or whose script is off
        Assert.All(examples.Where(e => e.Html.Contains("data-submit", StringComparison.Ordinal)), e => Assert.Contains("type=\"submit\"", e.Html));
    }

    [Fact]
    public void A_code_that_is_offered_by_sms_asks_for_the_right_keyboard()
    {
        foreach (var w in Wrappers().Where(w => w.Input.Contains("one-time-code", StringComparison.Ordinal)))
        {
            Assert.Contains("inputmode=\"numeric\"", w.Input);
        }

        // a PIN is a password in the real input too: the case without the script does not show it
        var pin = Wrappers().Single(w => w.Id == "otp-pin");
        Assert.Contains("type=\"password\"", pin.Input);
        Assert.Contains("data-mask", pin.Attributes);
    }

    [Fact]
    public void The_code_input_has_every_text_in_both_languages()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the code input were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 10, $"only {en.Count} texts");
        Assert.Equal(en, de);
    }

    [Fact]
    public void Every_text_the_script_asks_for_is_in_the_table_and_a_box_is_named_by_its_place()
    {
        var match = Texts().Match(Js);
        var keys = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).ToHashSet();
        var asked = TextCall().Matches(Section).Select(m => m.Groups[1].Value).Concat(KindText().Matches(Section).Select(m => m.Groups[1].Value)).Distinct().ToList();
        Assert.True(asked.Count >= 6, "the script asks for too few texts: the check no longer looks at anything");
        Assert.All(asked, key => Assert.Contains(key, keys));
        // "Digit 3 of 6" / "Ziffer 3 von 6": the place and the length
        foreach (var language in new[] { match.Groups[1].Value, match.Groups[2].Value })
        {
            Assert.Matches(@"boxDigit: '[^']*\{0\}[^']*\{1\}[^']*'", language);
            Assert.Matches(@"boxChar: '[^']*\{0\}[^']*\{1\}[^']*'", language);
        }
    }

    [Fact]
    public void The_section_of_the_script_sits_between_the_tags_and_the_events_of_the_pickers()
    {
        var tags = Js.IndexOf("KINDS.tags = {", StringComparison.Ordinal);
        var otp = Js.IndexOf("code input (FORM-19, matui.forms.css)", StringComparison.Ordinal);
        var events = Js.IndexOf("// ---- the events of the pickers", StringComparison.Ordinal);
        Assert.True(tags > 0 && tags < otp && otp < events);
    }

    [Fact]
    public void The_header_of_the_script_documents_the_attributes_and_the_event()
    {
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        foreach (var part in new[] { "data-otp", "data-length", "data-chars", "data-case", "data-mask", "data-group", "data-submit", "mat:otp-complete" })
        {
            Assert.Contains(part, header);
        }
    }

    [Fact]
    public void The_script_draws_a_named_group_of_boxes_and_says_what_happens()
    {
        var section = Section;
        // the boxes are a group named by the label, each box has a name, a live region tells what was refused and that the code is complete
        Assert.Contains("setAttribute('role', 'group')", section);
        Assert.Contains("aria-labelledby", section);
        Assert.Contains("aria-label", section);
        Assert.Contains("role', 'status'", section);
        Assert.Contains("aria-live", section);
        Assert.Contains("aria-invalid", section);
        // one tab stop that the arrow keys walk (A11Y-02)
        Assert.Contains("b.tabIndex = k === i ? 0 : -1;", section);
        foreach (var key in new[] { "ArrowLeft", "ArrowRight", "Home", "End", "Backspace", "Delete" })
        {
            Assert.Contains($"'{key}'", section);
        }

        // a pasted code, SMS autofill on the first box, a keyboard that fits the characters, no zoom, the real input out of the way
        Assert.Contains("'paste'", section);
        Assert.Contains("'beforeinput'", section);
        Assert.Contains("'compositionend'", section);
        Assert.Contains("one-time-code", section);
        Assert.Contains("'inputmode'", section);
        Assert.Contains("OTP_SEPARATOR", section);
        Assert.Contains("box.type = o.mask ? 'password' : 'text';", section);
        Assert.Contains("input.setAttribute('aria-hidden', 'true');", section);
    }

    [Fact]
    public void The_boxes_take_no_maxlength_so_that_an_autofilled_code_is_not_cut_and_never_post_a_name_of_their_own()
    {
        var section = Section;
        Assert.DoesNotContain("maxlength", section, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("box.name", section);
        Assert.DoesNotContain("setAttribute('name'", section);
    }

    [Fact]
    public void The_form_is_sent_only_when_the_author_asks_and_through_requestSubmit()
    {
        var section = Section;
        Assert.Contains("hasAttribute('data-submit')", section);
        Assert.Contains("requestSubmit()", section);
        // a button of the page can be named instead, and the event can be cancelled by an app that takes over
        Assert.Contains("button.click()", section);
        Assert.Contains("cancelable: true", section);
        Assert.Contains("new CustomEvent('mat:otp-complete'", section);
        Assert.DoesNotContain(".submit()", section.Replace("requestSubmit()", string.Empty, StringComparison.Ordinal));
        // the code is complete only when a person made it so, not when the page draws it or a script sets it
        Assert.Contains("full && changed && byUser", section);
    }

    [Fact]
    public void An_incomplete_code_is_not_valid_so_the_validation_of_the_form_works_on_the_real_input()
    {
        var section = Section;
        Assert.Contains("setCustomValidity(", section);
        Assert.Contains("data-msg-incomplete", section);
        Assert.Contains("data-msg-chars", section);
        // the red mark is the one of the form validation: showError, which also puts aria-invalid on the real input that the boxes follow
        Assert.Contains("showError(o.input, text)", section);
        Assert.Contains("'aria-invalid'", section);
        Assert.Contains("MutationObserver", section);
    }

    [Fact]
    public void The_public_api_and_the_refresh_know_the_code_input()
    {
        var api = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains("otp: { init: initOtps, clear: otpClear, focus: otpFocusFirst, setInvalid: otpSetInvalid, value: otpValue }", api);
        Assert.Contains("initOtps(scope)", api);
        var init = Js[Js.IndexOf("function init() {", StringComparison.Ordinal)..];
        Assert.Contains("initOtps();", init[..init.IndexOf("applyTheme", StringComparison.Ordinal)]);
        // a form that is reset shows empty boxes again
        Assert.Contains("addEventListener('reset'", Section);
    }

    [Fact]
    public void The_css_sits_before_the_floating_labels_and_hides_the_real_input_only_when_the_script_has_drawn_the_boxes()
    {
        Assert.True(Css.IndexOf("Code input (FORM-19)", StringComparison.Ordinal) is var otp and > 0 && otp < Css.IndexOf("Floating labels", StringComparison.Ordinal));
        Assert.Contains(".mat-otp.is-ready > .mat-otp__input {", Css);
        var hidden = Block(Css, ".mat-otp.is-ready > .mat-otp__input");
        Assert.Contains("opacity: 0;", hidden);
        Assert.Contains("pointer-events: none;", hidden);
        // without the script it is a field
        var plain = Block(Css, ".mat-otp__input");
        Assert.Contains("border: 1px solid var(--mat-border-strong);", plain);
        Assert.DoesNotContain("opacity", plain);
    }

    [Fact]
    public void The_boxes_keep_16_px_type_are_a_touch_target_and_shrink_instead_of_overflowing()
    {
        // MOBILE-04: 44 px high on a touch screen (the tokens of the controls), and 16 px or more so that a phone does not zoom in
        var sizes = Regex.Matches(Css, @"--mat-otp-fs: ([^;]+);").Select(m => m.Groups[1].Value).ToList();
        Assert.True(sizes.Count >= 3);
        Assert.All(sizes, s => Assert.Contains(s, new[] { "1rem", "var(--mat-fs-lg)", "var(--mat-fs-xl)", "var(--mat-fs-2xl)", "var(--mat-fs-3xl)" }));
        Assert.Contains("--mat-otp-h: var(--mat-control-h-lg);", Css);
        Assert.Contains("--mat-otp-h: var(--mat-control-h);", Css);
        var tokens = TestPaths.ReadToolkit("css/matui.tokens.css");
        var coarse = tokens[tokens.IndexOf("@media (pointer: coarse)", StringComparison.Ordinal)..];
        Assert.Contains("--mat-control-h: 44px;", coarse);
        Assert.Contains("--mat-control-h-lg: 52px;", coarse);

        var box = Block(Css, ".mat-otp__box");
        Assert.Contains("font-size: var(--mat-otp-fs);", box);
        Assert.Contains("height: var(--mat-otp-h);", box);
        Assert.Contains("flex: 0 1 var(--mat-otp-w);", box);
        Assert.Contains("min-width: 0;", box);
        Assert.Contains("max-width: 100%;", Block(Css, ".mat-otp__boxes"));
        var phone = Css[Css.IndexOf("@media (max-width: 640px)", StringComparison.Ordinal)..];
        Assert.Contains(".mat-otp__boxes { gap: var(--mat-space-1); }", phone);
    }

    [Fact]
    public void The_boxes_show_the_focus_the_error_the_disabled_and_the_read_only_state_with_the_tokens()
    {
        // A11Y-01, FORM-03
        Assert.Contains("box-shadow: var(--mat-focus-ring)", Block(Css, ".mat-otp__box:focus"));
        Assert.Contains("border-color: var(--mat-danger);", Block(Css, ".mat-field.is-invalid .mat-otp__input, .mat-field.is-invalid .mat-otp__box, .mat-otp.is-invalid .mat-otp__box"));
        Assert.Contains("cursor: not-allowed;", Block(Css, ".mat-otp__box:disabled"));
        Assert.Contains("background: var(--mat-surface-2);", Block(Css, ".mat-otp__box[readonly]"));
        Assert.Contains(".mat-otp__sep--dash::before {", Css);
        // a dot box is a password field: the buttons of the browser must not sit on top of the dot
        Assert.Contains(".mat-otp__box::-ms-reveal", Css);
        Assert.Contains("-webkit-credentials-auto-fill-button", Css);
    }
}
