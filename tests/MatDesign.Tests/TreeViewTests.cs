using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>LIST-17: the tree view, its documentation, its markup contract, its texts, its CSS and its script.</summary>
public partial class TreeViewTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.lists.css");

    [GeneratedRegex(@"var TR_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};", RegexOptions.Singleline)]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    private static IReadOnlyList<ComponentExample> Examples => ComponentCatalog.All.Single(c => c.Id == "tree").Examples;

    private static string TreeCss()
    {
        var start = Css.IndexOf("/* ---- Tree view (LIST-17)", StringComparison.Ordinal);
        Assert.True(start >= 0, "the tree part of the stylesheet was not found");
        // (the part ends where the carousel begins; the schedule part is the last one of the file and its test reads to the end, so nothing is added after it)
        return Css[start..Css.IndexOf("/* ---- Carousel (LIST-13)", StringComparison.Ordinal)];
    }

    private static string TreeScript()
    {
        var start = Js.IndexOf("var TR_KEY", StringComparison.Ordinal);
        Assert.True(start >= 0, "the tree part of the script was not found");
        return Js[start..Js.IndexOf("function fillMeters", StringComparison.Ordinal)];
    }

    [Fact]
    public void The_tree_is_documented_among_the_lists_and_follows_its_rule()
    {
        var component = ComponentCatalog.All.Single(c => c.Id == "tree");
        Assert.Equal("Lists and data", component.Group);
        Assert.Contains("LIST-17", component.Rules);
        Assert.Contains("MOBILE-04", component.Rules);
        Assert.True(component.Examples.Count >= 4);
        Assert.NotEmpty(component.Behaviour);
        var rule = RuleBook.Get("LIST-17");
        Assert.NotNull(rule);
        Assert.Equal("LIST", rule.Area);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.True(rule.Statement.Length <= 1200);
        // a hierarchy of one level is a grouped table: the rule of that points here, and this one points back to it
        Assert.Contains("LIST-16", rule.Statement);
        Assert.Contains("a tree, not a table", RuleBook.Get("LIST-16")!.Statement);
    }

    [Fact]
    public void Every_tree_of_an_example_is_a_named_list_of_items_that_each_have_a_row_with_a_label()
    {
        Assert.All(Examples, e =>
        {
            Assert.Matches(@"<ul class=""mat-tree[^""]*""[^>]*\bdata-tree\b[^>]*aria-label=""[^""]+""", e.Html);
            var items = Regex.Count(e.Html, @"<li class=""mat-tree__item""");
            var rows = Regex.Count(e.Html, @"<li class=""mat-tree__item""[^>]*>\s*<div class=""mat-tree__row"">");
            Assert.True(items >= 3, e.Title);
            Assert.Equal(items, rows);
            // one label per row: a span, or the link of a tree of links
            Assert.Equal(items, Regex.Count(e.Html, @"class=""mat-tree__label"""));
            // a group stands inside an item and holds items
            Assert.Equal(Regex.Count(e.Html, @"<ul class=""mat-tree__group"">"), Regex.Count(e.Html, @"</ul>") - 1);
        });
    }

    [Fact]
    public void A_folder_has_the_closed_and_the_open_icon_and_a_button_of_a_row_has_a_name()
    {
        var html = string.Concat(Examples.Select(e => e.Html));
        Assert.Matches(@"<span class=""mat-tree__icon mat-tree__icon--folder""><svg[^>]*><use href=""#mat-i-folder""></use></svg><svg[^>]*><use href=""#mat-i-folder-open""></use></svg></span>", html);
        // the buttons in the actions of a row are icons: they need a name (ICON-03)
        Assert.All(Regex.Matches(html, @"<button type=""button"" class=""mat-btn[^""]*mat-btn--icon[^""]*""([^>]*)>"), m => Assert.Contains("aria-label=\"", m.Groups[1].Value));
    }

    [Fact]
    public void Every_kind_of_tree_that_the_script_knows_is_shown()
    {
        var all = string.Concat(Examples.Select(e => e.Html));
        foreach (var attribute in new[] { "data-multiple", "data-expand-all", "data-expanded", "data-lazy", "data-tree-key", "data-tree-filter", "data-tree-set", "data-tree-toggle=\"row\"", "aria-selected=\"true\"", "aria-checked=\"true\"", "mat-tree__actions", "mat-tree__count", "mat-tree--compact", "mat-tree--no-lines", "mat-tree__empty" })
        {
            Assert.Contains(attribute, all);
        }

        // what a field filters and what a button opens exists in the same example
        Assert.All(Examples, e =>
        {
            foreach (Match m in Regex.Matches(e.Html, @"data-tree-(?:filter|target)=""#?([^""]+)"""))
            {
                Assert.Contains($"id=\"{m.Groups[1].Value}\"", e.Html);
            }
        });
    }

    [Fact]
    public void A_lazy_item_has_no_children_yet_and_is_a_leaf_in_the_markup()
    {
        var lazy = Regex.Matches(string.Concat(Examples.Select(e => e.Html)), @"<li class=""mat-tree__item""[^>]*data-lazy[^>]*>(.*?)</li>", RegexOptions.Singleline).ToList();
        Assert.True(lazy.Count >= 2);
        Assert.All(lazy, m => Assert.DoesNotContain("mat-tree__group", m.Groups[1].Value));
    }

    [Fact]
    public void The_tree_has_every_text_in_both_languages()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the tree were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 8, $"only {en.Count} texts");
        Assert.Equal(en, de);
        // the texts that the script asks for exist
        foreach (Match asked in Regex.Matches(TreeScript(), @"trText\('([A-Za-z]+)'"))
        {
            Assert.Contains(asked.Groups[1].Value, en);
        }
    }

    [Fact]
    public void The_script_gives_the_roles_and_the_numbers_of_the_tree_pattern()
    {
        var script = TreeScript();
        foreach (var part in new[]
        {
            "setAttribute('role', 'tree')", "setAttribute('role', 'treeitem')", "setAttribute('role', 'group')",
            "'aria-level'", "'aria-setsize'", "'aria-posinset'", "'aria-expanded'", "'aria-selected'", "'aria-checked'", "'aria-labelledby'", "'aria-busy'",
            "'mixed'",
        })
        {
            Assert.Contains(part, script);
        }

        // the level is data for the CSS: set by the script through the CSSOM, never an inline style in the markup
        Assert.Contains("style.setProperty('--mat-tree-level'", script);
        // a live region says the matches and what is chosen
        Assert.Contains("aria-live", script);
    }

    [Fact]
    public void The_whole_tree_is_one_tab_stop_that_moves_with_the_focus()
    {
        var script = TreeScript();
        Assert.Contains("item.tabIndex = -1;", script);
        Assert.Contains("item.tabIndex = 0;", script);
        Assert.Contains("function trRove", script);
        Assert.Contains("addEventListener('focusin'", script);
        // the buttons of the other rows are not in the Tab order
        Assert.Contains("c.closest('.mat-tree__actions') ? 0 : -1", script);
    }

    [Fact]
    public void Every_key_of_the_pattern_is_handled()
    {
        var script = TreeScript();
        var keys = script[script.IndexOf("addEventListener('keydown'", StringComparison.Ordinal)..];
        foreach (var key in new[] { "'ArrowDown'", "'ArrowUp'", "'ArrowRight'", "'ArrowLeft'", "'Home'", "'End'", "'Enter'", "'*'", "' '" })
        {
            Assert.Contains(key, keys);
        }

        // type-ahead: the letters typed within a moment are one word
        Assert.Contains("t.typed", keys);
        // the keys work only on the item itself: a button in its actions has its own
        Assert.Contains("!item.classList.contains('mat-tree__item')", keys);
        Assert.Contains("e.preventDefault();", keys);
    }

    [Fact]
    public void A_parent_follows_its_children_and_checking_it_checks_them_all()
    {
        var script = TreeScript();
        Assert.Contains("function trCheckDown", script);
        Assert.Contains("function trCheckUp", script);
        Assert.Contains("on === kids.length ? 'true' : on > 0 || mixed ? 'mixed' : 'false'", script);
    }

    [Fact]
    public void The_events_the_api_and_the_remembered_state_are_there()
    {
        var script = TreeScript();
        foreach (var name in new[] { "mat:tree-select", "mat:tree-toggle", "mat:tree-load", "mat:tree-filter" })
        {
            Assert.Contains(name, script);
        }

        // what is remembered belongs to the app, the tree and the item, like the groups of a table
        Assert.Contains("localStorage.setItem(TR_KEY", script);
        Assert.Contains("root.getAttribute('data-app')", script);
        Assert.Contains("!item.hasAttribute('data-tree-fixed')", script);
        // a lazy item is not opened by what was remembered: nobody listens yet
        Assert.Contains("!item.hasAttribute('data-lazy') && !item.hasAttribute('data-tree-fixed')", script);
        // set up when the page loads and after a refresh, and the api
        Assert.Contains("initTrees();", Js);
        Assert.Contains("initTrees(scope);", Js[Js.IndexOf("refresh: function (scope)", StringComparison.Ordinal)..]);
        Assert.Matches(@"tree: tree,", Js);
        Assert.Matches(@"var tree = \{\s*init: initTrees,\s*expand:[^\n]*\n\s*collapse:[^\n]*\n\s*select:[^\n]*\n\s*setChildren: trSetChildren,", Js);
    }

    [Fact]
    public void The_attributes_of_the_script_are_in_its_header()
    {
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        foreach (var part in new[] { "data-tree ", "data-multiple", "data-expand-all", "data-tree-key", "data-tree-toggle=\"row\"", "data-lazy", "mat:tree-load", "data-tree-filter=\"id\"", "data-tree-set=\"open|closed|toggle\"", "MatUi.tree.init" })
        {
            Assert.Contains(part, header);
        }
    }

    [Fact]
    public void The_css_reads_the_state_from_the_attributes_so_the_markup_and_the_state_cannot_disagree()
    {
        var css = TreeCss();
        Assert.Contains(".mat-tree__item[aria-expanded=\"false\"] > .mat-tree__group { display: none; }", css);
        Assert.Contains(".mat-tree__item[aria-expanded=\"true\"] > .mat-tree__row > .mat-tree__toggle > .mat-icon { transform: rotate(90deg); }", css);
        Assert.Contains(".mat-tree__item[hidden] { display: none; }", css);
        Assert.Contains(".mat-tree__empty[hidden] { display: none; }", css);
        // a leaf has the room of a chevron, not the chevron
        Assert.Contains(".mat-tree__item:not([aria-expanded]) > .mat-tree__row > .mat-tree__toggle > .mat-icon { visibility: hidden; }", css);
    }

    [Fact]
    public void The_level_is_the_indentation_and_the_guides_draw_a_line_under_every_ancestor()
    {
        var css = TreeCss();
        Assert.Matches(@"\.mat-tree__row \{[^}]*padding: [^;]*var\(--mat-tree-level, 0\) \* var\(--mat-tree-indent\)", css);
        Assert.Matches(@"\.mat-tree__row::before \{[^}]*repeating-linear-gradient\(to right, var\(--mat-tree-line\) 0 1px, transparent 1px var\(--mat-tree-indent\)\)", css);
        Assert.Contains("pointer-events: none;", css);
    }

    [Fact]
    public void A_chosen_item_is_a_tint_behind_the_whole_row_and_never_a_bar_at_one_side()
    {
        var css = TreeCss();
        Assert.Contains(".mat-tree__item[aria-selected=\"true\"] > .mat-tree__row, .mat-tree__item[aria-checked=\"true\"] > .mat-tree__row { background: var(--mat-accent-soft-strong); }", css);
        Assert.Contains(".mat-tree__item[aria-selected=\"true\"] > .mat-tree__row .mat-tree__label { font-weight: 650; }", css);
        Assert.DoesNotContain("border-left", css);
        Assert.DoesNotContain("border-inline-start", css);
        Assert.DoesNotContain("!important", css);
        // the box of several shows its state as a shape too (COLOR-04): a check and a dash
        Assert.Contains("mat-tree__check-on", css);
        Assert.Contains("mat-tree__check-mixed", css);
        // the focus is a ring inside the row, which nothing clips
        Assert.Contains(".mat-tree__item:focus-visible > .mat-tree__row { box-shadow: inset 0 0 0 2px var(--mat-accent); }", css);
    }

    [Fact]
    public void The_tree_uses_tokens_only_and_stops_its_motion_for_reduced_motion()
    {
        var css = TreeCss();
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", css);
        Assert.DoesNotMatch(@"\brgba?\(", css);
        Assert.DoesNotContain("linear-gradient(to bottom", css);
        var reduced = css[css.IndexOf("@media (prefers-reduced-motion: reduce)", StringComparison.Ordinal)..];
        Assert.Contains("transition: none;", reduced);
    }

    [Fact]
    public void On_a_phone_or_a_touch_screen_a_row_is_44_px_and_a_label_wraps()
    {
        var css = TreeCss();
        Assert.Contains("@media (max-width: 720px), (pointer: coarse) {", css);
        Assert.Matches(@"\.mat-tree \{ --mat-tree-indent: 28px; --mat-tree-row-h: 44px; \}", css);
        Assert.Matches(@"\.mat-tree__label \{[^}]*min-width: 0;[^}]*overflow-wrap: anywhere;", css);
        // the actions that a pointer shows on hover are also there for the focus and for the chosen row
        Assert.Contains("@media (hover: hover) {", css);
        Assert.Contains(".mat-tree.is-ready .mat-tree__item:focus > .mat-tree__row > .mat-tree__actions", css);
    }

    [Fact]
    public void The_demo_of_the_site_answers_the_load_of_its_lazy_tree_and_the_open_folder_is_in_the_sprite()
    {
        var doc = File.ReadAllText(TestPaths.Web("wwwroot/js/doc.js"));
        Assert.Contains("mat:tree-load", doc);
        Assert.Contains("MatUi.tree.setChildren", doc);
        Assert.Contains("id=\"mat-i-folder-open\"", TestPaths.ReadToolkit("img/matui-icons.svg"));
    }
}
