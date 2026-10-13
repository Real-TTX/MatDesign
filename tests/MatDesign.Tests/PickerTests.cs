using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-15: the pickers, their documentation, their texts and their CSS contract.</summary>
public partial class PickerTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");

    private static readonly string[] Kinds = ["select", "date", "daterange", "time", "timerange", "colour"];

    [GeneratedRegex(@"<div class=""mat-picker""([^>]*)>\s*<input class=""mat-picker__input"" id=""([^""]+)""([^>]*)>", RegexOptions.Singleline)]
    private static partial Regex PickerMarkup();

    [GeneratedRegex(@"\bvalue=""([^""]*)""")]
    private static partial Regex ValueAttribute();

    [GeneratedRegex(@"data-picker=""([a-z]+)""")]
    private static partial Regex KindAttribute();

    [GeneratedRegex(@"(?s)\ben: \{(.*?)\n    \},\n    de: \{(.*?)\n    \}\n  \};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    private static IEnumerable<(string Component, string Kind, string Id, string Value, string Html)> Pickers() =>
        ComponentCatalog.All.SelectMany(c => c.Examples.SelectMany(e => PickerMarkup().Matches(e.Html).Select(m =>
            (c.Id, KindAttribute().Match(m.Groups[1].Value).Groups[1].Value, m.Groups[2].Value, ValueAttribute().Match(m.Groups[3].Value).Groups[1].Value, e.Html))));

    [Theory]
    [InlineData("rich-select")]
    [InlineData("date-picker")]
    [InlineData("time-picker")]
    [InlineData("colour-picker")]
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
        // FORM-15: 2026-10-10, 2026-10-10..2026-10-14(~2), 09:30, 09:00..17:30, #b4235f
        var formats = new Dictionary<string, string>
        {
            ["date"] = @"^(\d{4}-\d\d-\d\d)?$",
            ["daterange"] = @"^(\d{4}-\d\d-\d\d\.\.\d{4}-\d\d-\d\d(~\d+)?)?$",
            ["time"] = @"^(\d\d:\d\d)?$",
            ["timerange"] = @"^(\d\d:\d\d\.\.\d\d:\d\d)?$",
            ["colour"] = @"^(#[0-9a-f]{6})?$",
            ["select"] = @"^[a-z0-9, ]*$",
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
        Assert.Contains("data-picker=\"select|date|daterange|time|timerange|colour\"", Js);
        Assert.Contains("2026-10-10..2026-10-14", Js);
    }

    [Theory]
    [InlineData(".mat-picker__panel[hidden]")]
    [InlineData(".mat-picker__foot[hidden]")]
    [InlineData(".mat-picker__options[hidden]")]
    [InlineData(".mat-picker__option[hidden]")]
    [InlineData(".mat-picker__empty[hidden]")]
    [InlineData(".mat-picker__clear[hidden]")]
    [InlineData(".mat-picker__status[hidden]")]
    [InlineData(".mat-picker__chips[hidden]")]
    [InlineData(".mat-picker__create[hidden]")]
    [InlineData(".mat-picker__create-msg[hidden]")]
    [InlineData(".mat-picker__group[hidden]")]
    [InlineData(".mat-picker__group-head[hidden]")]
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
    public void A_select_that_creates_and_suggests_is_documented_with_its_rule_and_is_in_the_header_and_the_api_of_the_script()
    {
        // FORM-16: create what is missing, suggestions
        var select = ComponentCatalog.All.Single(c => c.Id == "rich-select");
        Assert.Contains("FORM-16", select.Rules);
        Assert.NotNull(RuleBook.Get("FORM-16"));
        Assert.Contains(select.Examples, e => e.Html.Contains("data-create-url=", StringComparison.Ordinal) && !e.Html.Contains("data-suggest-url=", StringComparison.Ordinal));
        Assert.Contains(select.Examples, e => e.Html.Contains("data-create-url=", StringComparison.Ordinal) && e.Html.Contains("data-suggest-url=", StringComparison.Ordinal) && e.Html.Contains("data-multiple", StringComparison.Ordinal));
        Assert.Contains(select.Examples, e => e.Html.Contains("data-suggest-url=", StringComparison.Ordinal) && !e.Html.Contains("data-create-url=", StringComparison.Ordinal));
        Assert.Contains(select.Examples, e => e.Html.Contains("required", StringComparison.Ordinal) && e.Html.Contains("data-create-url=", StringComparison.Ordinal) && e.Html.Contains("data-create-label=", StringComparison.Ordinal));

        var header = Js[..Js.IndexOf("*/", StringComparison.Ordinal)];
        foreach (var documented in new[] { "data-create-url", "data-create-label", "data-suggest-url", "MatUi.picker.set(", ".addOption(", ".suggest(" })
        {
            Assert.Contains(documented, header);
        }

        Assert.Contains("picker: { init: initPickers, close: closePicker, set: pickerSet, addOption: pickerAddOption, suggest: pickerSuggest }", Js);

        var texts = Texts().Match(Js);
        foreach (var language in new[] { texts.Groups[1].Value, texts.Groups[2].Value })
        {
            foreach (var key in new[] { "create", "creating", "created", "createFailed", "createHint", "suggestions", "all", "suggestionCount", "suggestionOne" })
            {
                Assert.Matches($@"\b{key}: '", language);
            }
        }
    }

    [Fact]
    public void An_option_that_is_created_is_made_with_textContent_because_what_a_person_typed_is_never_markup()
    {
        var body = Js[Js.IndexOf("function pickerAddOption", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("// A new option is chosen", StringComparison.Ordinal)];
        Assert.DoesNotContain("innerHTML", body);
        Assert.True(Regex.Count(body, @"\.textContent = ") >= 3, "name, description and tags are set with textContent");
        // and so is the name of a suggestion and the text of the create row
        var rows = Js[Js.IndexOf("function suggestionRow", StringComparison.Ordinal)..];
        Assert.Contains("$('.mat-picker__name', row).textContent = s.name;", rows[..rows.IndexOf("function renderSuggestions", StringComparison.Ordinal)]);
        Assert.Contains("$('.mat-picker__name', p.createRow).textContent = pkTextOf(p, 'create', q);", Js);
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
