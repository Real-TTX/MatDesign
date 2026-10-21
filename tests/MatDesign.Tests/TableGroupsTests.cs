using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>LIST-16: groups in a table, their documentation, their markup contract, their CSS and their script.</summary>
public partial class TableGroupsTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.lists.css");

    [GeneratedRegex(@"(?s)<tbody class=""mat-group([^""]*)""([^>]*)>(.*?)</tbody>")]
    private static partial Regex Group();

    [GeneratedRegex(@"<tr class=""([^""]*)""")]
    private static partial Regex RowClass();

    private static IEnumerable<(string Example, string Classes, string Attributes, string Rows)> GroupsOfExamples()
        => ComponentCatalog.All.Single(c => c.Id == "table-groups").Examples
            .SelectMany(e => Group().Matches(e.Html).Select(m => (e.Title, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)));

    [Fact]
    public void Groups_are_documented_among_the_lists_and_follow_their_rule()
    {
        var component = ComponentCatalog.All.Single(c => c.Id == "table-groups");

        Assert.Equal("Lists and data", component.Group);
        Assert.Contains("LIST-16", component.Rules);
        Assert.Contains("TABLE-02", component.Rules);
        Assert.True(component.Examples.Count >= 2);
        Assert.NotEmpty(component.Behaviour);
        var rule = RuleBook.Get("LIST-16");
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Owner, rule.Source);
    }

    [Fact]
    public void Every_table_of_an_example_has_a_key_and_every_group_a_name_and_a_toggle_that_says_what_it_does()
    {
        var examples = ComponentCatalog.All.Single(c => c.Id == "table-groups").Examples;
        Assert.All(examples, e =>
        {
            Assert.Matches(@"<table[^>]*data-groups=""[^""]+""", e.Html);
            Assert.True(Regex.Count(e.Html, @"<tbody class=""mat-group") >= 2, e.Title);
        });

        var groups = GroupsOfExamples().ToList();
        Assert.True(groups.Count >= 4);
        Assert.All(groups, g =>
        {
            Assert.Matches(@"data-group=""[^""]+""", g.Attributes);
            var collapsed = g.Classes.Contains("is-collapsed", StringComparison.Ordinal);
            Assert.Matches(@"<button type=""button"" class=""mat-group__toggle"" data-group-toggle aria-expanded=""" + (collapsed ? "false" : "true") + @"""", g.Rows);
            Assert.Contains("class=\"mat-group__title\"", g.Rows);
            // the first row is the heading, and it spans the table
            Assert.Equal("mat-group__head", RowClass().Match(g.Rows).Groups[1].Value);
            Assert.Matches(@"<tr class=""mat-group__head""><td class=""mat-group__cell"" colspan=""\d+"">", g.Rows);
        });
    }

    [Fact]
    public void The_action_that_adds_a_row_is_the_last_row_of_its_group_and_never_in_the_heading()
    {
        var withAdd = GroupsOfExamples().Where(g => g.Rows.Contains("mat-group__add", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(withAdd);
        Assert.All(withAdd, g =>
        {
            Assert.Equal("mat-group__add", RowClass().Matches(g.Rows).Last().Groups[1].Value);
            Assert.Single(RowClass().Matches(g.Rows), m => m.Groups[1].Value == "mat-group__add");
        });
        var heading = Regex.Match(string.Concat(GroupsOfExamples().Select(g => g.Rows)), @"(?s)<tr class=""mat-group__head"">.*?</tr>").Value;
        Assert.DoesNotContain("Add a", heading);
    }

    [Fact]
    public void A_group_that_is_closed_in_the_markup_is_closed_by_the_class_the_script_uses()
    {
        var closed = GroupsOfExamples().Where(g => g.Classes.Contains("is-collapsed", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(closed);
        Assert.Contains(GroupsOfExamples(), g => g.Attributes.Contains("data-group-fixed", StringComparison.Ordinal));
    }

    [Fact]
    public void The_css_hides_the_rows_of_a_closed_group_and_draws_the_line_that_the_last_row_ends()
    {
        Assert.Contains(".mat-group.is-collapsed > tr:not(.mat-group__head) { display: none; }", Css);
        Assert.Contains(".mat-group.is-collapsed .mat-group__chev { transform: rotate(-90deg); }", Css);
        Assert.Contains(".mat-group > tr:last-child > td:first-child::before { bottom: 50%; }", Css);
        // the muted rest of a name (.example.org after app) is not bold like the name
        Assert.Contains(".mat-table__name .mat-muted { font-weight: 400; }", Css);
        // COLOR-08 does not forbid a line of a tree, but the heading is tinted, never marked by a bar at its side
        Assert.DoesNotContain("border-left: 3px", Css[Css.IndexOf(".mat-group", StringComparison.Ordinal)..Css.IndexOf("/* ---- Pagination", StringComparison.Ordinal)]);
    }

    [Fact]
    public void On_a_phone_the_label_of_a_cell_is_in_the_type_of_the_page_even_in_a_monospace_cell()
    {
        // TABLE-02: the label is text of the page, not a value; a mat-mono cell (a date, a number) must not lend it its monospace
        Assert.Matches(@"\.mat-table td::before \{ content: attr\(data-label\);[^}]*font-family: var\(--mat-font\);", Css);
    }

    [Fact]
    public void On_a_phone_a_row_with_actions_is_a_card_with_the_name_as_its_title_and_the_actions_beside_it()
    {
        const string row = ".mat-table > tbody:not(.mat-group) > tr:has(> td.mat-table__cell-actions):not(:has(> td.mat-table__select, > td.mat-table__primary))";
        Assert.Contains(row + " {\n    display: grid; grid-template-columns: minmax(0, 1fr) auto;", Css);
        Assert.Contains(row + " > td:first-child::before { content: none; }", Css);
        Assert.Contains(row + " > td.mat-table__cell-actions { grid-column: 2; grid-row: 1; width: auto; padding: 0; }", Css);
        Assert.Contains("its first cell (the name) is the title of the card", RuleBook.All.Single(r => r.Id == "TABLE-02").Statement);
    }

    [Fact]
    public void On_a_phone_a_row_with_a_primary_cell_is_a_record_card_that_opens_from_anywhere()
    {
        const string row = ".mat-table > tbody:not(.mat-group) > tr:has(> td.mat-table__primary):not(:has(> td.mat-table__select))";
        // the old card with actions beside the title leaves such a row alone
        Assert.Contains("tr:has(> td.mat-table__cell-actions):not(:has(> td.mat-table__select, > td.mat-table__primary)) {", Css);
        Assert.Contains(row + " {\n    position: relative; display: flex; flex-wrap: wrap;", Css);
        // the labels are for a screen reader only, the link of the title covers the card, the other controls stay above it
        Assert.Contains(row + " > td::before {\n    position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%);", Css);
        Assert.Contains(row + " > td.mat-table__primary a[href]::after { content: \"\"; position: absolute; inset: 0; }", Css);
        Assert.Contains(row + " > td:not(.mat-table__primary) :is(a[href], button, input, select, textarea, label, summary) {\n    position: relative; z-index: 1;", Css);
        // the actions are a row of their own, an icon button shows its text
        Assert.Contains(row + " > td.mat-table__cell-actions { order: 1; flex: 1 1 100%;", Css);
        Assert.Contains(row + " .mat-table__actions .mat-btn--icon[data-tip]::after {\n    content: attr(data-tip); display: inline; position: static;", Css);
        Assert.Contains("is a record card", RuleBook.All.Single(r => r.Id == "TABLE-02").Statement);
    }

    [Fact]
    public void On_a_phone_the_line_is_gone_and_the_heading_is_a_bar_over_the_cards()
    {
        var phone = Css[Css.IndexOf("@media (max-width: 720px) {\n  /* A row is a card here: no line", StringComparison.Ordinal)..];
        phone = phone[..phone.IndexOf("/* ---- Pagination", StringComparison.Ordinal)];
        // the pseudo element of the line is the label of the cell again (TABLE-02)
        Assert.Contains("{ position: static; border: 0; content: none; }", phone);
        Assert.Contains("td:first-child[data-label]:not([data-label=\"\"])::before { content: attr(data-label); }", phone);
        Assert.Contains("td:first-child::after { content: none; }", phone);
        Assert.Contains(".mat-table .mat-group__head { padding: 0; }", phone);
        Assert.Contains(".mat-group__add > .mat-group__cell { justify-content: flex-start;", phone);
    }

    [Fact]
    public void The_toggle_is_a_real_button_that_fills_the_heading_and_shows_where_the_focus_is()
    {
        Assert.Matches(@"\.mat-group__toggle \{[^}]*flex: 1 1 auto;[^}]*min-height: var\(--mat-control-h\);", Css);
        Assert.Contains(".mat-group__toggle:focus-visible { box-shadow: inset 0 0 0 2px var(--mat-accent);", Css);
        Assert.Contains(".mat-group__toggle:hover { background: var(--mat-accent-soft); }", Css);
    }

    [Fact]
    public void The_script_opens_and_closes_by_class_and_aria_remembers_the_choice_and_leaves_a_fixed_group_alone()
    {
        var section = Js[Js.IndexOf("var GROUP_KEY", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("function selectTab", StringComparison.Ordinal)];
        Assert.Contains("classList.toggle('is-collapsed', !open)", section);
        Assert.Contains("setAttribute('aria-expanded'", section);
        Assert.Contains("localStorage.setItem(GROUP_KEY", section);
        Assert.Contains("!group.hasAttribute('data-group-fixed')", section);
        Assert.Contains("mat:group-toggle", section);
        // what is saved belongs to the app, the table and the group
        Assert.Contains("root.getAttribute('data-app')", section);
        Assert.Contains("closest('[data-groups]')", section);
        // the saved state is applied when the page loads and after a refresh
        Assert.Contains("restoreGroups();", Js);
        Assert.Contains("restoreGroups(scope);", Js);
        Assert.Contains("groups: { set:", Js);
    }

    [Fact]
    public void The_attributes_of_the_script_are_in_its_header()
    {
        var header = Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)];
        Assert.Contains("data-groups=\"key\"", header);
        Assert.Contains("data-group-toggle", header);
        Assert.Contains("data-group-fixed", header);
        Assert.Contains("data-groups-set=\"open|closed|toggle\"", header);
    }
}
