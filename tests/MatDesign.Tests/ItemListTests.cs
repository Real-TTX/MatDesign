using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>LIST-15: the list of rows, its documentation, its texts, its CSS contract and its script.</summary>
public sealed partial class ItemListTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.lists.css");

    private static readonly string[] Ids = ["itemlist", "itemlist-contents", "itemlist-choices", "itemlist-actions"];

    private static IReadOnlyList<ComponentDoc> Docs => [.. ComponentCatalog.All.Where(c => Ids.Contains(c.Id))];

    private static string Html(string id) => string.Join("\n", ComponentCatalog.Get(id)!.Examples.Select(e => e.Html));

    private static string AllHtml => string.Join("\n", Docs.SelectMany(d => d.Examples).Select(e => e.Html));

    /// <summary>The section of the stylesheet that this rule adds: from its heading to the carousel.</summary>
    private static string Section => Css[Css.IndexOf("/* ---- Lists of rows (LIST-15)", StringComparison.Ordinal)..Css.IndexOf("/* ---- Carousel (LIST-13)", StringComparison.Ordinal)];

    /// <summary>The section of the script: from its heading to the index of a long page.</summary>
    private static string Script => Js[Js.IndexOf("lists of rows (LIST-15, matui.lists.css)", StringComparison.Ordinal)..Js.IndexOf("on this page: scrollspy (SHELL-16)", StringComparison.Ordinal)];

    [GeneratedRegex(@"var IL_TEXT = \{\s*'en': \{(.*?)\},\s*'de': \{(.*?)\}\s*\};", RegexOptions.Singleline)]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '((?:[^'\\]|\\.)*)'")]
    private static partial Regex TextEntry();

    [GeneratedRegex(@"<a class=""mat-item__main""[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex LinkRow();

    [GeneratedRegex(@"<button\b[^>]*class=""mat-item__main""[^>]*>(.*?)</button>", RegexOptions.Singleline)]
    private static partial Regex ButtonRow();

    [GeneratedRegex(@"<label class=""mat-item__main""[^>]*>(.*?)</label>", RegexOptions.Singleline)]
    private static partial Regex LabelRow();

    [GeneratedRegex(@"<ul class=""mat-itemlist[ ""][^>]*>")]
    private static partial Regex ListStart();

    // ---------------------------------------------------------------------------------------------------- the rule and its documentation

    [Fact]
    public void The_rule_is_in_the_rulebook_and_says_what_a_list_of_rows_is()
    {
        var rule = RuleBook.Get("LIST-15");
        Assert.NotNull(rule);
        Assert.Equal("LIST", rule.Area);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.Equal("Lists of rows", rule.Title);
        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.False(string.IsNullOrWhiteSpace(rule.Do));
        Assert.False(string.IsNullOrWhiteSpace(rule.Dont));
        foreach (var word in new[] { "mat-itemlist", "48 px", "More button", "arrow keys", "handle", "tint", "COLOR-04", "COLOR-08", "LIST-02" })
        {
            Assert.Contains(word, rule.Statement);
        }
    }

    [Fact]
    public void The_components_are_documented_among_the_lists_and_cite_the_rule()
    {
        Assert.Equal(Ids.Length, Docs.Count);
        Assert.All(Docs, d =>
        {
            Assert.Equal("Lists and data", d.Group);
            Assert.Contains("LIST-15", d.Rules);
            Assert.NotEmpty(d.Behaviour);
            Assert.True(d.Examples.Count >= 3, d.Id);
        });
        Assert.All(Docs.SelectMany(d => d.Rules), r => Assert.NotNull(RuleBook.Get(r)));
    }

    [Fact]
    public void The_documentation_keeps_to_what_the_studio_accepts_for_a_component()
    {
        // 12 lines of 300 characters of behaviour, 12 examples of 20000 characters, a title of 80 and a note of 400 (ComponentValidator)
        Assert.All(Docs, d =>
        {
            Assert.InRange(d.Name.Length, 2, 60);
            Assert.InRange(d.Summary.Length, 10, 300);
            Assert.True(d.Behaviour.Count <= 12, d.Id);
            Assert.All(d.Behaviour, b => Assert.True(b.Length <= 300, $"{d.Id}: {b.Length} characters: {b}"));
            Assert.True(d.Examples.Count <= 12, d.Id);
            Assert.All(d.Examples, e =>
            {
                Assert.InRange(e.Title.Length, 2, 80);
                Assert.True(e.Html.Length <= 20000, $"{d.Id}/{e.Title}");
                Assert.True(e.Note is null || e.Note.Length <= 400, $"{d.Id}/{e.Title}: the note has {e.Note?.Length} characters");
            });
        });
    }

    [Fact]
    public void Every_kind_of_content_the_rule_asks_for_has_a_live_example()
    {
        var contents = Html("itemlist-contents");
        // contacts under letters that stay, with a filter
        Assert.Contains("mat-itemlist__group", contents);
        Assert.Contains("mat-itemlist--sticky", contents);
        Assert.Contains("data-item-filter=\"il-contacts\"", contents);
        Assert.Contains("id=\"il-contacts\"", contents);
        Assert.Contains("class=\"mat-itemlist__empty\" hidden", contents);
        // an inbox: unread is a bold title, a dot and a word
        Assert.Contains("is-unread", contents);
        Assert.Contains("mat-item__dot", contents);
        Assert.Contains("<span class=\"mat-sr-only\">Unread: </span>", contents);
        Assert.Contains("mat-item__text--clamp", contents);
        // settings: an icon tile, a switch, a value and the arrow
        Assert.Contains("mat-item__tile", contents);
        Assert.Contains("mat-switch--bare", contents);
        Assert.Contains("mat-item__chevron", contents);
        // files with a thumbnail, tasks with a check, a skeleton, an empty list
        Assert.Contains("mat-item__thumb", contents);
        Assert.Contains("mat-itemlist--tasks", contents);
        Assert.Contains("aria-busy=\"true\"", contents);
        Assert.Contains("mat-skeleton--circle", contents);
        Assert.Contains("mat-itemlist__empty", contents);
        Assert.Contains("mat-empty__title", contents);
        // products: a larger lead and a price
        Assert.Contains("mat-itemlist--lead-lg", contents);
        Assert.Contains("mat-item__note--strong", contents);
        // choosing and typing
        var choices = Html("itemlist-choices");
        Assert.Contains("type=\"radio\"", choices);
        Assert.Contains("type=\"checkbox\"", choices);
        Assert.Contains("mat-itemlist--choices", choices);
        Assert.Contains("data-select-all", choices);
        Assert.Contains("data-select-row", choices);
        Assert.Contains("data-bulk-bar", choices);
        Assert.Contains("mat-item__input", choices);
        Assert.Contains("mat-item__label", choices);
        // sliding actions and reordering
        var actions = Html("itemlist-actions");
        Assert.Contains("mat-item--sliding", actions);
        Assert.Contains("mat-item__options--start", actions);
        Assert.Contains("mat-item__options--end", actions);
        Assert.Contains("data-reorder", actions);
        Assert.Contains("mat-item__handle", actions);
        // the basics: lines, the grouped card, headers and dividers, the states
        var basics = Html("itemlist");
        foreach (var word in new[] { "mat-itemlist--lines-inset", "mat-itemlist--lines-none", "mat-itemlist--inset", "mat-itemlist__header", "mat-itemlist__divider", "is-selected", "is-disabled", "aria-current=\"page\"", "mat-item__aside" })
        {
            Assert.Contains(word, basics);
        }
    }

    [Fact]
    public void The_four_tones_of_a_notification_have_an_icon_of_their_own_and_a_word_for_a_screen_reader()
    {
        var notifications = Html("itemlist-contents");
        foreach (var (tone, icon, word) in new[] { ("ok", "check-circle", "Success"), ("warn", "alert", "Warning"), ("error", "x-circle", "Error"), ("info", "info", "Information") })
        {
            Assert.Matches($@"mat-item__tile--{tone}""><svg[^>]*><use href=""#mat-i-{icon}""", notifications);
            Assert.Contains($"<span class=\"mat-sr-only\">{word}: </span>", notifications);
        }
    }

    // ---------------------------------------------------------------------------------------------------- the markup contract

    [Fact]
    public void Every_list_is_named_so_that_a_screen_reader_says_what_it_lists()
    {
        var lists = ListStart().Matches(AllHtml).ToList();
        Assert.True(lists.Count >= 20, $"only {lists.Count} lists");
        foreach (Match list in lists)
        {
            var before = AllHtml[Math.Max(0, list.Index - 12)..list.Index];
            Assert.True(list.Value.Contains("aria-label", StringComparison.Ordinal) || list.Value.Contains("aria-labelledby", StringComparison.Ordinal) || before.Contains("</legend>", StringComparison.Ordinal), $"a list without a name: {list.Value}");
        }
    }

    [Fact]
    public void A_target_never_holds_another_target()
    {
        // LIST-15: what has to be pressed as well stands beside the row (mat-item__aside), never inside a link, a button or a label
        var rows = LinkRow().Matches(AllHtml).Concat(ButtonRow().Matches(AllHtml)).ToList();
        Assert.True(rows.Count >= 25, $"only {rows.Count} rows that are links or buttons");
        Assert.All(rows, r => Assert.DoesNotMatch(@"<(button|input|select|textarea|a)\b", r.Groups[1].Value));

        var labels = LabelRow().Matches(AllHtml).ToList();
        Assert.True(labels.Count >= 12, $"only {labels.Count} rows that are labels");
        Assert.All(labels, r =>
        {
            Assert.DoesNotMatch(@"<(button|select|textarea|a)\b", r.Groups[1].Value);
            Assert.All(Regex.Matches(r.Groups[1].Value, @"<input\b[^>]*>").Select(m => m.Value), i => Assert.Contains("mat-item__check", i));
        });
    }

    [Fact]
    public void An_option_is_a_typed_button_with_an_icon_and_a_word_and_a_destructive_one_asks_first()
    {
        var options = Regex.Matches(AllHtml, @"<button type=""button"" class=""mat-item__option[^""]*""[^>]*>(.*?)</button>", RegexOptions.Singleline).ToList();
        Assert.True(options.Count >= 8, $"only {options.Count} options");
        Assert.All(options, o =>
        {
            Assert.Contains("<svg", o.Groups[1].Value);
            Assert.Matches(@"<span>[A-Z][a-z]+</span>", o.Groups[1].Value);
            Assert.Contains("data-item-action=\"", o.Value);
        });
        // LIST-08: a delete asks
        foreach (var delete in options.Where(o => o.Value.Contains("data-item-action=\"delete\"", StringComparison.Ordinal)))
        {
            Assert.Contains("data-confirm=\"", delete.Value);
        }
    }

    [Fact]
    public void A_row_that_slides_holds_its_main_and_its_options_in_one_row_and_its_list_is_taken_over_by_the_script()
    {
        var actions = Html("itemlist-actions");
        var sliding = Regex.Matches(actions, @"<li class=""mat-item mat-item--sliding[^""]*""[^>]*>\s*<div class=""mat-item__row"">(.*?)</div>\s*</li>", RegexOptions.Singleline).ToList();
        Assert.True(sliding.Count >= 5, $"only {sliding.Count} rows that slide");
        Assert.All(sliding, s =>
        {
            Assert.Contains("mat-item__main", s.Groups[1].Value);
            Assert.Contains("mat-item__options mat-item__options--", s.Groups[1].Value);
        });
        foreach (var list in Regex.Matches(actions, @"<ul class=""mat-itemlist[^""]*""[^>]*>").Select(m => m.Value))
        {
            Assert.Contains("data-itemlist", list);
        }
    }

    [Fact]
    public void A_handle_is_a_typed_icon_button_with_a_name_and_a_row_has_a_key_that_the_event_can_name()
    {
        var actions = Html("itemlist-actions");
        var handles = Regex.Matches(actions, @"<button\b[^>]*class=""mat-item__handle""[^>]*>").Select(m => m.Value).ToList();
        Assert.True(handles.Count >= 5, $"only {handles.Count} handles");
        Assert.All(handles, h =>
        {
            Assert.Contains("type=\"button\"", h);
            Assert.Matches(@"aria-label=""Reorder [A-Z][a-z]+""", h);
        });
        var keys = Regex.Matches(actions, @"data-item-key=""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
        Assert.True(keys.Count >= 10);
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void The_selected_check_and_the_row_fields_follow_the_rules_of_the_forms()
    {
        // FORM-08: a control has an id and a label that points to it
        var choices = Html("itemlist-choices");
        foreach (Match input in Regex.Matches(choices, @"<(input|select)\b([^>]*)>"))
        {
            var id = Regex.Match(input.Groups[2].Value, @"\bid=""([^""]+)""").Groups[1].Value;
            Assert.NotEmpty(id);
            Assert.Contains($"for=\"{id}\"", choices);
        }
    }

    [Fact]
    public void The_classes_of_the_lists_do_not_collide_with_the_list_block_of_a_page_or_the_list_group()
    {
        // .mat-list is the list block of a page (toolbar, table, pager) and .mat-listgroup the group of links: a list of rows has names of its own
        Assert.DoesNotMatch(@"\.mat-list[ ,{.:\[]", Section);
        Assert.DoesNotMatch(@"\.mat-listgroup", Section);
        Assert.DoesNotContain("mat-list\"", AllHtml.Replace("mat-listgroup", string.Empty));
    }

    // ---------------------------------------------------------------------------------------------------- the texts

    [Fact]
    public void The_lists_have_every_text_in_both_languages_and_the_same_places_for_the_words_that_are_filled_in()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the lists were not found");
        var en = TextEntry().Matches(match.Groups[1].Value).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var de = TextEntry().Matches(match.Groups[2].Value).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        Assert.True(en.Count >= 8, $"only {en.Count} texts");
        Assert.Equal(en.Keys.Order(), de.Keys.Order());
        foreach (var key in en.Keys)
        {
            Assert.NotEqual(en[key], de[key]);
            Assert.Equal(Regex.Matches(en[key], @"\{\d\}").Select(m => m.Value).Order(), Regex.Matches(de[key], @"\{\d\}").Select(m => m.Value).Order());
        }
    }

    [Fact]
    public void The_texts_are_found_by_the_key_in_the_language_of_the_page_and_can_be_changed_by_an_attribute_of_the_page()
    {
        Assert.Contains("root.getAttribute('data-il-' + key.toLowerCase())", Script);
        Assert.Contains("(root.lang || 'en')", Script);
        // the one place where a phrase is not a text of the table is a name that the author wrote
        Assert.Contains("ilText('more', ilTitle(item))", Script);
        Assert.Contains("ilText('reorder', ilTitle(", Script);
    }

    // ---------------------------------------------------------------------------------------------------- the CSS

    [Fact]
    public void The_section_of_the_stylesheet_stands_before_the_carousel_and_has_no_important()
    {
        Assert.True(Css.IndexOf("Lists of rows (LIST-15)", StringComparison.Ordinal) < Css.IndexOf("Carousel (LIST-13)", StringComparison.Ordinal));
        Assert.DoesNotContain("!important", Section);
        Assert.DoesNotContain("radial-gradient", Section);
    }

    [Fact]
    public void What_the_script_hides_stays_hidden_in_spite_of_the_display_of_its_class()
    {
        // a display in the CSS beats the attribute: the elements that carry one need a rule of their own
        foreach (var selector in new[] { ".mat-item[hidden]", ".mat-itemlist__group[hidden]", ".mat-itemlist__empty[hidden]" })
        {
            Assert.Contains(selector + " { display: none; }", Section);
        }
    }

    [Fact]
    public void A_row_and_what_is_pressed_in_it_are_touch_targets()
    {
        // MOBILE-04: 44 px at least (the large control is 48 px, 52 on a touch screen); the handle is as large as a control (40 px, 44 px on a touch screen)
        Assert.Contains("--mat-item-h: max(44px, var(--mat-control-h-lg));", Section);
        Assert.Matches(@"\.mat-item__main \{[^}]*min-height: var\(--mat-item-h\);", Section);
        Assert.Matches(@"\.mat-item__option \{[^}]*min-height: var\(--mat-item-h\);", Section);
        Assert.Matches(@"\.mat-item__handle \{[^}]*width: var\(--mat-control-h\); height: var\(--mat-control-h\);", Section);
        // an option is wide enough for a finger and a word
        Assert.Contains("min-width: 76px;", Section);
        // the More button that the script adds is a button of the toolkit: an icon button, 40 px and 44 px on a touch screen
        Assert.Contains("mat-btn mat-btn--ghost mat-btn--icon mat-item__more", Script);
    }

    [Fact]
    public void A_selected_row_is_a_tint_behind_the_whole_row_and_the_focus_ring_is_inside_the_row()
    {
        // COLOR-08 and A11Y-01: no bar at a side, a ring that no neighbour clips
        Assert.Matches(@"\.mat-item\.is-selected, [^{]*\{ background: var\(--mat-accent-soft-strong\); \}", Section);
        Assert.Matches(@"\.mat-item__main:focus-visible[^{]*\{[^}]*box-shadow: inset 0 0 0 2px var\(--mat-accent\);", Section);
        Assert.Matches(@"\.mat-item\.is-selected \.mat-item__title[^{]*\{ font-weight: 650; \}", Section);
        Assert.DoesNotMatch(@"\.mat-item[^{]*\{[^}]*border-left", Section);
    }

    [Fact]
    public void The_sticky_dividers_stay_because_the_card_clips_instead_of_scrolling()
    {
        Assert.Contains(".mat-itemlist--sticky .mat-itemlist__divider { position: sticky;", Section);
        // overflow: hidden would make a scroll container of the card and the divider would stick to that
        Assert.Contains(".mat-itemlist--inset { border: 1px solid var(--mat-border); border-radius: var(--mat-radius-lg); overflow: hidden; overflow: clip; }", Section);
        Assert.Contains(".mat-itemlist--scroll { max-height: 22rem; overflow-y: auto;", Section);
    }

    [Fact]
    public void The_options_are_hidden_until_the_row_is_open_and_the_row_leaves_the_vertical_pan_to_the_page()
    {
        Assert.Matches(@"\.mat-itemlist\.is-ready \.mat-item--sliding \.mat-item__options \{[^}]*visibility: hidden;", Section);
        Assert.Matches(@"\.mat-item--sliding\.is-swiping \.mat-item__options, \.mat-itemlist\.is-ready \.mat-item--sliding\.is-open \.mat-item__options \{ visibility: visible;", Section);
        Assert.Matches(@"\.mat-item--sliding > \.mat-item__row \{[^}]*touch-action: pan-y;", Section);
        Assert.Matches(@"\.mat-item__handle \{[^}]*touch-action: none;", Section);
        Assert.Contains("transform: translateX(var(--mat-item-x, 0px));", Section);
    }

    [Fact]
    public void The_options_stay_where_they_are_without_the_script_and_the_held_row_floats_with_the_shadow_of_what_floats()
    {
        // without the script the options are buttons at the end of the row: the absolute position is only for a list that is ready
        Assert.DoesNotMatch(@"(?m)^\.mat-item__options[^{]*\{[^}]*position: absolute", Section);
        Assert.Contains(".mat-item.is-dragging, .mat-item.is-picked { z-index: 2; border-radius: var(--mat-radius); background: var(--mat-surface); box-shadow: var(--mat-shadow-2); }", Section);
    }

    [Fact]
    public void With_reduced_motion_nothing_slides()
    {
        // A11Y-05
        var motion = Section[Section.IndexOf("@media (prefers-reduced-motion: reduce)", StringComparison.Ordinal)..];
        Assert.Contains(".mat-item__row", motion);
        Assert.Contains("transition: none;", motion);
        Assert.Contains("reducedMotion()", Script[Script.IndexOf("function ilFlip", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void Colours_in_the_section_are_tokens()
    {
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(", Regex.Replace(Section, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline));
    }

    // ---------------------------------------------------------------------------------------------------- the script

    [Fact]
    public void The_script_section_stands_before_the_index_of_a_page_and_is_documented_in_the_header()
    {
        Assert.True(Js.IndexOf("lists of rows (LIST-15, matui.lists.css)", StringComparison.Ordinal) < Js.IndexOf("on this page: scrollspy (SHELL-16)", StringComparison.Ordinal));
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        foreach (var attribute in new[] { "data-itemlist", "data-reorder", "data-item-filter", "data-item-action", "data-item-key", "mat:item-action", "mat:reorder", "data-il-<name>" })
        {
            Assert.Contains(attribute, header);
        }

        var api = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains("itemlist: {", api);
        Assert.Contains("initItemLists(scope)", api);
    }

    [Fact]
    public void The_options_of_a_closed_row_are_inert_and_the_actions_do_not_need_a_swipe()
    {
        Assert.Contains("toggleAttribute('inert'", Script);
        Assert.Contains("setAttribute('inert', '')", Script);
        // the More button and the arrow keys
        Assert.Contains("mat-item__more", Script);
        Assert.Contains("aria-expanded", Script);
        Assert.Contains("'ArrowLeft'", Script);
        Assert.Contains("'ArrowRight'", Script);
        Assert.Contains("'Escape'", Script);
        // an option that asks first is done when the question is answered
        Assert.Contains("opt.hasAttribute('data-confirm') && !confirmed.has(opt)", Script);
        Assert.DoesNotContain(".disabled =", Script);
    }

    [Fact]
    public void A_swipe_is_a_pointer_gesture_that_leaves_a_vertical_move_to_the_page_and_does_not_follow_the_link_it_ends_on()
    {
        foreach (var name in new[] { "pointerdown", "pointermove", "pointerup", "pointercancel", "setPointerCapture", "dragstart" })
        {
            Assert.Contains(name, Script);
        }

        Assert.Contains("Math.abs(dx) < Math.abs(dy) * 1.2", Script);
        Assert.Contains("ilSuppress", Script);
        Assert.Contains("e.stopPropagation()", Script);
    }

    [Fact]
    public void Reordering_works_with_the_keyboard_and_says_what_it_does()
    {
        foreach (var key in new[] { "' '", "'Enter'", "'ArrowUp'", "'ArrowDown'", "'Home'", "'End'", "'Escape'", "'Tab'" })
        {
            Assert.Contains($"e.key === {key}", Script);
        }

        Assert.Contains("aria-pressed", Script);
        Assert.Contains("aria-live", Script);
        Assert.Contains("'mat:reorder'", Script);
        Assert.Contains("ilText('pickedUp'", Script);
        Assert.Contains("ilText('moved'", Script);
        Assert.Contains("ilText('dropped'", Script);
        Assert.Contains("ilText('cancelled'", Script);
        // the DOM is changed when a row is dropped, so that the pointer capture of the handle lives as long as the drag
        Assert.Contains("the DOM changes when it is dropped", Script);
    }

    [Fact]
    public void The_filter_ignores_case_and_accents_and_the_selection_counts_only_the_rows_that_are_shown()
    {
        Assert.Contains("normalize('NFD')", Script);
        Assert.Contains("toLowerCase()", Script);
        Assert.Contains("'mat:item-filter'", Script);
        Assert.Contains("e.stopImmediatePropagation()", Script);
        Assert.Contains("is-selected", Script);
    }

    // ---------------------------------------------------------------------------------------------------- the page

    [Fact]
    public async Task The_components_page_shows_the_lists_with_their_code_and_a_name_in_the_index()
    {
        var html = await factory.CreateClient().GetStringAsync("/Components");
        foreach (var d in Docs)
        {
            Assert.Contains($"<section class=\"doc-component mat-anchor\" id=\"{d.Id}\"", html);
            Assert.Contains($"href=\"#{d.Id}\"", html);
        }

        Assert.Contains("data-item-filter=\"il-contacts\"", html);
        Assert.Contains("mat-item__handle", html);
        Assert.Contains("href=\"/Rules/Detail?id=LIST-15\"", html);
    }
}
