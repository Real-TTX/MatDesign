using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-26: the wizard, its documentation, its texts, its markup contract, its script and its CSS (the behaviour is held by tests/e2e/run.js in a browser).</summary>
public partial class WizardTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.controls.css");
    private static readonly string ShellCss = TestPaths.ReadToolkit("css/matui.shell.css");
    private static readonly string Adopting = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md"));

    [GeneratedRegex(@"(?s)var WZ_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"wzText\('([A-Za-z]+)'")]
    private static partial Regex TextCall();

    [GeneratedRegex(@"<li[^>]*>(?:<span class=""mat-sr-only"">[^<]*</span>)</li>")]
    private static partial Regex BarItem();

    [GeneratedRegex(@"<section class=""mat-wizard__step""([^>]*)>\s*<h4 class=""mat-wizard__title"" id=""([^""]+)"">")]
    private static partial Regex Step();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    private static ComponentDoc Component => ComponentCatalog.All.Single(c => c.Id == "wizard");

    private static string Server => Component.Examples[0].Html;

    private static string AllInOne => Component.Examples[1].Html;

    /// <summary>The script of the wizard, from its text table to the heading of the init section.</summary>
    private static string Script
    {
        get
        {
            var from = Js.IndexOf("wizard (FORM-26, matui.controls.css)", StringComparison.Ordinal);
            var to = Js.IndexOf("// a meter is filled by data", StringComparison.Ordinal);
            Assert.True(from > 0 && to > from, "the section of the wizard was not found");
            return Js[from..to];
        }
    }

    /// <summary>The rules of the wizard in the stylesheet: from its heading to the next heading.</summary>
    private static string Style
    {
        get
        {
            var from = Css.IndexOf("/* ---- Wizard (FORM-26)", StringComparison.Ordinal);
            Assert.True(from > 0, "the CSS of the wizard was not found");
            var to = Css.IndexOf("/* Tooltip for icon buttons", from, StringComparison.Ordinal);
            Assert.True(to > from, "the end of the CSS of the wizard was not found");
            return Css[from..to];
        }
    }

    [Fact]
    public void The_wizard_is_documented_among_the_navigation_and_follows_its_rule()
    {
        Assert.Equal("Navigation", Component.Group);
        Assert.Contains("FORM-26", Component.Rules);
        Assert.Equal(2, Component.Examples.Count);
        Assert.True(Component.Behaviour.Count >= 8);
        // it comes right after the steps, which it builds on
        var order = ComponentCatalog.All.Where(c => c.Group == "Navigation").Select(c => c.Id).ToList();
        Assert.Equal(order.IndexOf("steps") + 1, order.IndexOf("wizard"));

        var rule = RuleBook.Get("FORM-26");
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Must, rule.Level);
        Assert.Equal(RuleBook.Owner, rule.Source);
        Assert.Equal("FORM", rule.Area);
        Assert.Contains("mat-wizard", rule.Statement);
        Assert.True(rule.Statement.Length <= 1200, $"the statement has {rule.Statement.Length} characters");
        // the rule cites what exists
        Assert.All(Regex.Matches(rule.Statement, @"\b[A-Z]+-\d\d\b").Select(m => m.Value), id => Assert.NotNull(RuleBook.Get(id)));
    }

    [Fact]
    public void The_server_driven_example_is_plain_markup_with_links_one_current_step_and_no_script_hooks()
    {
        Assert.DoesNotContain("data-wizard", Server);
        Assert.Contains("class=\"mat-auth__card mat-auth__card--wide mat-wizard\"", Server);
        Assert.Contains("<div class=\"mat-wizard__head\">", Server);
        Assert.Single(Regex.Matches(Server, @"aria-current=""step"""));
        var items = BarItem().Matches(Server).Count;
        Assert.Equal(5, items);
        Assert.Equal(2, Regex.Matches(Server, @"<li class=""is-done"">").Count);
        Assert.Contains("<li class=\"is-active\" aria-current=\"step\">", Server);
        // the buttons are links, in the one row of the toolkit with its spacer and the way out at the end
        Assert.Contains("<div class=\"mat-btn-row mat-wizard__actions\">", Server);
        Assert.Contains("mat-btn mat-btn--primary\" href=", Server);
        Assert.Contains("mat-btn-row__spacer", Server);
        Assert.Contains("mat-btn mat-btn--ghost mat-btn--end", Server);
        Assert.DoesNotContain("<button type=\"button\"", Server);
        // the step is named by its title, which the section points at
        Assert.Matches(@"aria-labelledby=""(wz-server-title)""[\s\S]*id=""\1""", Server);
    }

    [Fact]
    public void The_all_in_one_example_is_a_form_with_steps_a_bar_of_the_same_length_and_the_buttons_the_script_looks_for()
    {
        Assert.StartsWith("<form class=\"mat-auth__card mat-auth__card--wide mat-wizard mat-form\" data-wizard", AllInOne);
        var steps = Step().Matches(AllInOne).ToList();
        Assert.Equal(4, steps.Count);
        Assert.Equal(4, BarItem().Matches(AllInOne).Count);
        // every step is named by its title (aria-labelledby) and by a label for the bar
        foreach (Match step in steps)
        {
            Assert.Contains($"aria-labelledby=\"{step.Groups[2].Value}\"", step.Groups[1].Value);
            Assert.Contains("data-label=\"", step.Groups[1].Value);
        }

        // without the script it shows every step, so none is hidden in the markup; the buttons that need the script are
        Assert.DoesNotMatch(@"<section class=""mat-wizard__step""[^>]*\shidden", AllInOne);
        foreach (var hook in new[] { "data-wizard-next", "data-wizard-back", "data-wizard-skip" })
        {
            Assert.Matches($@"<button type=""button""[^>]*{hook}[^>]* hidden>", AllInOne);
        }

        Assert.Matches(@"<button type=""submit""[^>]*data-wizard-submit>", AllInOne);
        Assert.Contains("data-wizard-status>", AllInOne);
        Assert.Contains("data-optional", AllInOne);
        // a required field with its message, so that Continue has something to refuse
        Assert.Matches(@"required data-msg-required=""[^""]+""", AllInOne);
        // the form is checked by the script of the forms
        Assert.Contains("novalidate", AllInOne);
        Assert.DoesNotContain("style=", AllInOne);
        Assert.DoesNotContain("style=", Server);
        // the first step is the one that the bar shows
        Assert.Contains("<li class=\"is-active\" aria-current=\"step\">", AllInOne);
    }

    [Fact]
    public void The_wizard_has_every_text_in_both_languages_and_asks_only_for_texts_that_exist()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the wizard were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.Equal(en, de);
        var asked = TextCall().Matches(Script).Select(m => m.Groups[1].Value).Distinct().Order().ToList();
        Assert.Equal(asked, en);
        // the placeholders agree
        foreach (var key in en)
        {
            var enText = Regex.Match(match.Groups[1].Value, key + @": '([^']*)'").Groups[1].Value;
            var deText = Regex.Match(match.Groups[2].Value, key + @": '([^']*)'").Groups[1].Value;
            Assert.Equal(Regex.Matches(enText, @"\{\d\}").Select(m => m.Value).Order(), Regex.Matches(deText, @"\{\d\}").Select(m => m.Value).Order());
        }

        Assert.Single(Regex.Matches(Js, @"var WZ_TEXT = "));
        // a page can change a text with data-wz-<name> on <html>, and ADOPTING.md says so
        Assert.Contains("'data-wz-' + key.toLowerCase()", Script);
        Assert.Contains("| `data-wz-<name>` |", Adopting);
        Assert.Contains("`WZ_TEXT`", Adopting);
    }

    [Fact]
    public void The_script_is_documented_in_its_header_started_with_the_page_and_found_by_refresh()
    {
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        Assert.Contains("data-wizard ", header);
        Assert.Contains("data-wz-<name>", header);
        Assert.Contains("mat:wizard-step", header);
        Assert.Contains("MatUi.wizard.init / go / next / back / current", header);
        var init = Js[Js.IndexOf("  function init() {", StringComparison.Ordinal)..];
        Assert.Contains("initWizards();", init[..init.IndexOf("\n  }", StringComparison.Ordinal)]);
        Assert.Contains("initWizards(scope);", Js[Js.IndexOf("    refresh: function (scope)", StringComparison.Ordinal)..]);
        Assert.Contains("wizard: wizard,", Js);
        Assert.Contains("var wizard = { init: initWizards, go: wzGo, next: wzNext, back: wzBack, current:", Script);
    }

    [Fact]
    public void The_script_keeps_the_rules_of_the_slice_of_the_file_it_is_in_and_builds_no_name_from_parts()
    {
        // the upload tests read the script to the init section and strip block comments with a regex: no block comment in here, not even in a string
        Assert.DoesNotContain("/*", Script);
        // the white label renames mat- names on token boundaries: a name written from two strings would be missed
        Assert.DoesNotMatch(@"['""]mat['""]\s*\+", Script);
        Assert.DoesNotMatch(@"['""]mat-['""]\s*\+", Script);
        Assert.DoesNotMatch(@"['""]mat:['""]\s*\+", Script);
        Assert.DoesNotContain(".style.", Script);
        Assert.DoesNotContain("innerHTML", Script);
    }

    [Fact]
    public void A_step_is_shown_by_the_hidden_attribute_and_the_bar_says_where_the_person_is_in_words()
    {
        var script = Script;
        Assert.Contains("s.hidden = i !== cur;", script);
        Assert.Contains("li.classList.toggle('is-done', i < cur);", script);
        Assert.Contains("li.classList.toggle('is-active', i === cur);", script);
        Assert.Contains("li.setAttribute('aria-current', 'step')", script);
        Assert.Contains("li.removeAttribute('aria-current')", script);
        // every item has a hidden text and the state is in words (COLOR-04)
        Assert.Contains("hidden.className = 'mat-sr-only'", script);
        Assert.Contains("wzText('done')", script);
        Assert.Contains("wzText('current')", script);
        // the line under the title and the live region say "Step n of N"
        Assert.Contains("w.status.textContent = status;", script);
        Assert.Contains("w.live.setAttribute('aria-live', 'polite');", script);
        // the buttons: Continue and the submit swap on the last step, Back is not on the first, Skip only on an optional step
        Assert.Contains("w.next.hidden = last;", script);
        Assert.Contains("w.submit.hidden = !last;", script);
        Assert.Contains("w.back.hidden = cur === 0;", script);
        Assert.Contains("w.skip.hidden = last || !w.steps[cur].hasAttribute('data-optional');", script);
    }

    [Fact]
    public void A_new_step_gets_the_focus_on_its_title_and_the_event_can_be_cancelled()
    {
        var script = Script;
        Assert.Contains("$('.mat-wizard__title', w.steps[to]) || w.steps[to]", script);
        Assert.Contains("title.setAttribute('tabindex', '-1');", script);
        Assert.Contains("title.focus();", script);
        Assert.Contains("w.root.dispatchEvent(new CustomEvent('mat:wizard-step', { bubbles: true, cancelable: true, detail: { wizard: w.root, from: w.index + 1, to: to + 1, direction: to > w.index ? 'forward' : 'back' } }))", script);
        // a cancelled step stays: nothing moved before the event was answered, and silent = true sends no event
        Assert.True(script.IndexOf("mat:wizard-step", StringComparison.Ordinal) < script.IndexOf("w.index = to;", StringComparison.Ordinal));
        Assert.Contains("silent !== true", script);
    }

    [Fact]
    public void Continue_checks_the_step_with_the_check_of_the_form_and_Back_and_Skip_check_nothing()
    {
        var script = Script;
        var next = script[script.IndexOf("function wzNext(", StringComparison.Ordinal)..script.IndexOf("function wzBack(", StringComparison.Ordinal)];
        Assert.Contains("validateFields(w.form || w.root, w.steps[w.index])", next);
        Assert.Contains("bad[0].focus()", next);
        var back = script[script.IndexOf("function wzBack(", StringComparison.Ordinal)..script.IndexOf("function wzInit(", StringComparison.Ordinal)];
        Assert.DoesNotContain("validateFields", back);
        // the check of a form is the one function that the submit handler uses too (FORM-05): the wizard does not write its own
        var submit = Js[Js.IndexOf("function validateFields(", StringComparison.Ordinal)..];
        Assert.Contains("var bad = validateFields(form, form);", submit);
        Assert.Single(Regex.Matches(Js, @"function validateFields\("));
        Assert.Contains("checkValidity()", submit[..submit.IndexOf("doc.addEventListener('submit'", StringComparison.Ordinal)]);
    }

    [Fact]
    public void Enter_before_the_last_step_is_Continue_and_a_send_that_finds_an_earlier_field_wrong_shows_that_step()
    {
        var script = Script;
        // on the window, in the capture phase: before the check of the form, which must not run for Continue
        var early = script[script.IndexOf("window.addEventListener('submit'", StringComparison.Ordinal)..];
        Assert.Contains("e.preventDefault();", early);
        Assert.Contains("e.stopPropagation();", early);
        Assert.Contains("}, true);", early);
        Assert.Contains("e.submitter.hasAttribute('formnovalidate')", early);
        // after the check (this listener was added later on the same target): the step of the first wrong field is shown
        var late = script[script.IndexOf("doc.addEventListener('submit'", StringComparison.Ordinal)..];
        Assert.Contains("!e.defaultPrevented", late);
        Assert.Contains("i.getAttribute('aria-invalid') === 'true'", late);
        Assert.Contains("field.focus();", late);
        Assert.True(Js.IndexOf("function validateFields(", StringComparison.Ordinal) < Js.IndexOf("doc.addEventListener('submit', function (e) {\n    var w = e.target && e.target._matWizard;", StringComparison.Ordinal));
    }

    [Fact]
    public void A_step_with_an_error_is_the_first_one_shown_and_a_wizard_that_is_started_twice_is_one()
    {
        var script = Script;
        Assert.Contains("$('[aria-invalid=\"true\"], .is-invalid', s)", script);
        Assert.Contains("data-start", script);
        Assert.Contains("var w = el._matWizard;", script);
        Assert.Contains("var first = !w;", script);
        // the live region and the starting step are made once
        Assert.Contains("if (first) {", script);
    }

    [Fact]
    public void The_stylesheet_draws_the_parts_flat_from_tokens_and_hides_what_the_script_hides()
    {
        var style = Style;
        Assert.DoesNotContain("!important", style);
        Assert.DoesNotContain("gradient", style);
        Assert.DoesNotContain("box-shadow", style);
        Assert.DoesNotContain("blur", style);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", BlockComment().Replace(style, string.Empty));
        Assert.DoesNotMatch(@"\brgba?\(", style);
        foreach (var part in new[] { ".mat-wizard {", ".mat-wizard__head {", ".mat-wizard__step {", ".mat-wizard__title {", ".mat-wizard__text {", ".mat-wizard__actions {" })
        {
            Assert.Contains(part, style);
        }

        // a hidden step and a hidden button have a rule of their own, as the CSS gives them a display
        Assert.Contains(".mat-wizard__step[hidden] { display: none; }", style);
        Assert.Contains(".mat-wizard__actions > [hidden] { display: none; }", style);
        // the title has the accent icon; the air between the parts is a token, and the card wins the gap of the card
        Assert.Contains(".mat-wizard__title > .mat-icon { flex: none; color: var(--mat-accent-text); }", style);
        Assert.Contains(".mat-auth__card.mat-wizard { gap: var(--mat-space-6); }", style);
        // a phone: Continue on a line of its own, the way out on the last line, no spacer
        var phone = style[style.IndexOf("@media (max-width: 600px)", StringComparison.Ordinal)..];
        Assert.Contains(".mat-wizard__actions > .mat-btn--primary, .mat-wizard__actions > .mat-btn--end { flex-basis: 100%; }", phone);
        Assert.Contains(".mat-wizard__actions > .mat-btn-row__spacer { display: none; }", phone);
        // nothing here makes a control smaller than the toolkit makes it (MOBILE-04, MOBILE-06)
        Assert.DoesNotMatch(@"(min-)?height:\s*\d+px", style);
        Assert.DoesNotContain("font-size: 1", style.Replace("font-size: var(--mat-fs-lg)", string.Empty));
    }

    [Fact]
    public void The_steps_bar_is_what_it_was_and_the_wide_card_is_a_variant_of_the_card()
    {
        Assert.Contains(".mat-steps { display: flex; gap: var(--mat-space-2); list-style: none; }", Css);
        Assert.Contains(".mat-steps li.is-done { background: color-mix(in srgb, var(--mat-accent) 55%, transparent); }", Css);
        Assert.Contains(".mat-steps li.is-active { background: var(--mat-accent); }", Css);
        Assert.Contains(".mat-auth__card--wide { width: min(760px, 100%); }", ShellCss);
        // the wide card is defined after the card it varies, so it wins at equal weight
        Assert.True(ShellCss.IndexOf(".mat-auth__card {", StringComparison.Ordinal) < ShellCss.IndexOf(".mat-auth__card--wide {", StringComparison.Ordinal));
    }

    [Fact]
    public void The_event_and_the_texts_are_written_down_for_the_apps()
    {
        Assert.Contains("| `mat:wizard-step` | the `.mat-wizard` |", Adopting);
        Assert.Contains("`mat:wizard-step` are;", Adopting);
        // the changelog says what is new in this version
        var changelog = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "CHANGELOG.md"));
        Assert.Contains("mat-wizard", changelog);
    }
}
