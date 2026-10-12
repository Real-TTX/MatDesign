using System.Text.RegularExpressions;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-15: the pickers, their documentation, their texts and their CSS contract.</summary>
public partial class PickerTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");

    private static readonly string[] Kinds = ["select", "date", "daterange", "time", "timerange", "colour", "tags", "slot"];

    [GeneratedRegex(@"<div class=""mat-picker""([^>]*)>\s*<input class=""mat-picker__input"" id=""([^""]+)""([^>]*)>", RegexOptions.Singleline)]
    private static partial Regex PickerMarkup();

    [GeneratedRegex(@"\bvalue=""([^""]*)""")]
    private static partial Regex ValueAttribute();

    [GeneratedRegex(@"data-picker=""([a-z]+)""")]
    private static partial Regex KindAttribute();

    // (anchored on the name: other parts of the script have text tables of their own, and one of them stands before this one)
    [GeneratedRegex(@"(?s)var PK_TEXT = \{\s*en: \{(.*?)\n    \},\n    de: \{(.*?)\n    \}\n  \};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"(?s)\bkeys: function \(p, e\) \{(.*?)\n    \}\n  \};")]
    private static partial Regex SelectKeys();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    private static IEnumerable<(string Component, string Kind, string Id, string Value, string Html)> Pickers() =>
        ComponentCatalog.All.SelectMany(c => c.Examples.SelectMany(e => PickerMarkup().Matches(e.Html).Select(m =>
            (c.Id, KindAttribute().Match(m.Groups[1].Value).Groups[1].Value, m.Groups[2].Value, ValueAttribute().Match(m.Groups[3].Value).Groups[1].Value, e.Html))));

    [Theory]
    [InlineData("rich-select")]
    [InlineData("checkbox-select")]
    [InlineData("tag-input")]
    [InlineData("date-picker")]
    [InlineData("time-picker")]
    [InlineData("colour-picker")]
    [InlineData("slot-picker")]
    public void The_pickers_are_documented_among_the_forms(string id)
    {
        var component = ComponentCatalog.All.Single(c => c.Id == id);
        Assert.Equal("Forms", component.Group);
        Assert.Contains("FORM-15", component.Rules);
        Assert.NotEmpty(component.Behaviour);
    }

    [Fact]
    public void Every_kind_of_picker_is_shown_and_the_script_knows_it()
    {
        var shown = Pickers().Select(p => p.Kind).Distinct().Order().ToList();
        Assert.Equal(Kinds.Order().ToList(), shown);
        Assert.All(Kinds, kind => Assert.Contains($"KINDS.{kind} =", Js));
    }

    [Fact]
    public void Every_picker_of_an_example_has_a_label_that_points_to_its_input()
    {
        var pickers = Pickers().ToList();
        Assert.True(pickers.Count >= 12, $"only {pickers.Count} pickers in the examples");
        Assert.All(pickers, p => Assert.Contains($"for=\"{p.Id}\"", p.Html));
    }

    [Fact]
    public void The_examples_post_the_fixed_formats_whatever_the_language()
    {
        // FORM-15: 2026-10-10, 2026-10-10..2026-10-14(~2), 09:30, 09:00..17:30, #b4235f, 2026-10-14T09:30 (..09:45 with a duration)
        var formats = new Dictionary<string, string>
        {
            ["date"] = @"^(\d{4}-\d\d-\d\d)?$",
            ["daterange"] = @"^(\d{4}-\d\d-\d\d\.\.\d{4}-\d\d-\d\d(~\d+)?)?$",
            ["time"] = @"^(\d\d:\d\d)?$",
            ["timerange"] = @"^(\d\d:\d\d\.\.\d\d:\d\d)?$",
            ["colour"] = @"^(#[0-9a-f]{6})?$",
            ["select"] = @"^[a-z0-9, ]*$",
            ["tags"] = @"^[A-Za-z0-9 \-]+(, [A-Za-z0-9 \-]+)*$|^$",
            ["slot"] = @"^(\d{4}-\d\d-\d\dT\d\d:\d\d(\.\.\d\d:\d\d)?)?$",
        };
        Assert.All(Pickers(), p => Assert.Matches(formats[p.Kind], p.Value));
    }

    [Fact]
    public void The_pickers_have_every_text_in_both_languages()
    {
        // A text that exists in English only would show as its key on a German page.
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the pickers were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count > 40, $"only {en.Count} texts");
        Assert.Equal(en, de);
    }

    [Fact]
    public void The_pickers_are_documented_in_the_header_of_the_script()
    {
        Assert.Contains("data-picker=\"select|date|daterange|time|timerange|colour|tags|slot\"", Js);
        Assert.Contains("2026-10-10..2026-10-14", Js);
        Assert.Contains("2026-10-14T09:30", Js);
    }

    [Theory]
    [InlineData(".mat-picker__panel[hidden]")]
    [InlineData(".mat-picker__foot[hidden]")]
    [InlineData(".mat-picker__options[hidden]")]
    [InlineData(".mat-picker__option[hidden]")]
    [InlineData(".mat-picker__all[hidden]")]
    [InlineData(".mat-picker__empty[hidden]")]
    [InlineData(".mat-picker__clear[hidden]")]
    [InlineData(".mat-picker__status[hidden]")]
    [InlineData(".mat-picker__chips[hidden]")]
    public void What_the_script_hides_stays_hidden_where_a_rule_sets_display(string selector)
        => Assert.Contains(selector, TestPaths.ReadToolkit("css/matui.pickers.css"));

    [Fact]
    public void A_panel_fills_a_phone_with_the_close_button_like_a_dialog_and_is_a_popover_on_a_wide_screen()
    {
        // FORM-15, DIALOG-03
        var css = TestPaths.ReadToolkit("css/matui.pickers.css");
        Assert.Matches(@"\.mat-picker__head \{ display: none;", css);
        var phone = css[css.IndexOf("@media (max-width: 640px)", StringComparison.Ordinal)..];
        Assert.Contains("inset: 0; width: 100%; height: 100%;", phone);
        Assert.Contains(".mat-picker__head { display: flex; }", phone);
        Assert.Contains("position: fixed; z-index: var(--mat-z-menu);", css);
    }

    [Fact]
    public void The_tag_input_is_a_field_of_its_own_and_its_suggestions_are_never_a_full_screen_sheet()
    {
        // FORM-15: no button and no panel that is opened, and the keyboard stays in the field on a phone
        Assert.Contains("inline: true", Js);
        foreach (var option in new[] { "data-max", "data-max-length", "data-duplicates", "data-strict" })
        {
            Assert.Contains(option, Js);
        }

        var css = TestPaths.ReadToolkit("css/matui.pickers.css");
        var phone = css[css.LastIndexOf("@media (max-width: 640px)", StringComparison.Ordinal)..];
        Assert.Contains(".mat-picker__panel.mat-picker__panel--suggest {", phone);
        Assert.Contains("inset: auto;", phone);
    }

    // ---------------------------------------------------------------------------------------- a dropdown with a list of checkboxes

    private static IReadOnlyList<ComponentExample> CheckboxExamples => ComponentCatalog.All.Single(c => c.Id == "checkbox-select").Examples;

    [Fact]
    public void The_checkbox_dropdown_shows_select_all_with_and_without_a_search_and_follows_its_rule()
    {
        var examples = CheckboxExamples;
        Assert.True(examples.Count >= 3, "a dropdown with a list of checkboxes needs examples");
        Assert.Contains(examples, e => e.Title == "A dropdown with a list of checkboxes");
        Assert.All(examples, e => Assert.Contains("data-multiple", e.Html));
        Assert.Contains(examples, e => e.Html.Contains("data-select-all", StringComparison.Ordinal) && !e.Html.Contains("data-search", StringComparison.Ordinal));
        Assert.Contains(examples, e => e.Html.Contains("data-select-all", StringComparison.Ordinal) && e.Html.Contains("data-search", StringComparison.Ordinal));
        Assert.Contains(examples, e => Regex.IsMatch(e.Html, @"class=""mat-picker__option""[^>]* disabled"));
        Assert.Contains("FORM-15", ComponentCatalog.All.Single(c => c.Id == "checkbox-select").Rules);
        Assert.Contains("data-select-all", MatDesign.Core.RuleBook.Get("FORM-15")!.Statement);
    }

    [Fact]
    public void The_entries_of_the_checkbox_dropdown_are_plain_a_label_and_nothing_else()
    {
        // no picture, no description and no tags: a label and a key
        foreach (Match option in Regex.Matches(string.Concat(CheckboxExamples.Select(e => e.Html)), @"<button type=""button"" class=""mat-picker__option""[^>]*>(.*?)</button>"))
        {
            Assert.DoesNotContain("<", option.Groups[1].Value);
            Assert.True(option.Groups[1].Value.Length > 2, option.Value);
        }
    }

    [Fact]
    public void Select_all_is_a_checkbox_in_three_states_that_acts_on_what_is_shown()
    {
        // a button with role=checkbox (a role=option has no state in between), aria-checked true, false or mixed, the options that are shown and can be chosen
        Assert.Contains("data-select-all", Js);
        Assert.Contains("p.all.setAttribute('role', 'checkbox')", Js);
        Assert.Contains("'mixed'", Js);
        Assert.Contains("function visibleOptions(p) { return $$('.mat-picker__option:not([hidden]):not(:disabled)', p.list); }", Js);
        Assert.Contains("var keys = visibleOptions(p).map", Js);

        var css = TestPaths.ReadToolkit("css/matui.pickers.css");
        Assert.Contains(".mat-picker__all[aria-checked=\"mixed\"] .mat-picker__check::after", css);
        // the in-between state is a dash, not only another colour (COLOR-04)
        Assert.Matches(@"\.mat-picker__all\[aria-checked=""mixed""\] \.mat-picker__check::after \{[^}]*width: 10px; height: 2px;", css);
    }

    [Fact]
    public void The_field_says_the_names_then_the_count_then_that_all_are_selected()
    {
        Assert.Contains("pkText('allSelected')", Js);
        Assert.Contains("chosen.length <= 2 ? names.join(', ') : pkText('selectedCount', chosen.length)", Js);
        // "All selected" is for a list of more than two entries: two entries that are both ticked are still named
        Assert.Contains("chosen.length === all.length && all.length > 2", Js);
    }

    [Fact]
    public void The_arrow_keys_walk_select_all_and_the_entries_and_space_is_left_to_the_button()
    {
        var keys = SelectKeys().Match(Js);
        Assert.True(keys.Success, "the keys of the select were not found");
        var body = keys.Groups[1].Value;
        Assert.Contains("selRows(p)", body);
        Assert.Contains("'Home'", body);
        Assert.Contains("'End'", body);
        Assert.Contains("e.key === ' ' && !p.typed", body);
    }

    [Fact]
    public void The_texts_of_select_all_exist_in_both_languages()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success);
        foreach (var key in new[] { "selectAll", "selectFound", "allSelected" })
        {
            Assert.Contains($"{key}: '", match.Groups[1].Value);
            Assert.Contains($"{key}: '", match.Groups[2].Value);
        }
    }

    [Fact]
    public void The_colour_picker_takes_its_white_and_black_from_tokens()
    {
        var css = TestPaths.ReadToolkit("css/matui.pickers.css");
        Assert.Contains("var(--mat-picker-light)", css);
        Assert.Contains("var(--mat-picker-dark)", css);
        Assert.Contains("var(--mat-picker-hues)", css);
    }
}
