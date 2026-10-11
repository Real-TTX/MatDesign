using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The toolbar with its search as a field and its dialog buttons at the right end (LIST-03), and the index of a long page as an icon in the header bar (SHELL-16, SHELL-17).</summary>
public sealed class ToolbarAndIndexTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static string Css(string part) => TestPaths.ReadToolkit("css/" + part);

    private static string Example(string component, int index) => ComponentCatalog.All.Single(c => c.Id == component).Examples[index].Html;

    [Fact]
    public void The_rule_allows_a_search_field_in_the_toolbar_with_the_dialog_buttons_at_the_right_end()
    {
        var rule = RuleBook.All.Single(r => r.Id == "LIST-03");

        Assert.Contains("search field directly in the toolbar", rule.Statement);
        Assert.Contains("right end", rule.Statement);
        Assert.Contains("mat-toolbar__search", rule.Do);
        Assert.Contains("mat-toolbar__end", rule.Do);
        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.Contains("selects", rule.Dont);
    }

    [Fact]
    public void The_toolkit_draws_the_search_field_to_grow_and_the_dialog_buttons_to_sit_at_the_right_end()
    {
        var css = Css("matui.lists.css");

        Assert.Contains(".mat-toolbar__search { flex: 1 1 16rem; min-width: 0;", css);
        Assert.Contains("margin-left: auto", css[css.IndexOf(".mat-toolbar__end {", StringComparison.Ordinal)..]);
        // on a phone the field takes the whole first row
        Assert.Matches(@"@media \(max-width: 480px\) \{[^@]*\.mat-toolbar__search \{ flex-basis: 100%", css);
        Assert.Contains("mat-input-icon", Css("matui.forms.css"));
    }

    [Fact]
    public void The_second_example_of_the_toolbar_has_the_field_in_the_bar_and_filter_and_sort_in_the_end_group()
    {
        var html = Example("toolbar", 1);

        Assert.Equal(2, ComponentCatalog.All.Single(c => c.Id == "toolbar").Examples.Count);
        var field = Regex.Match(html, @"<div class=""mat-input-icon mat-toolbar__search"">.*?<input class=""mat-input"" type=""search"" id=""tb2-q"" name=""q"" form=""tb2-form""[^>]*aria-label=""[^""]+""", RegexOptions.Singleline);
        Assert.True(field.Success, "the search field belongs to the one GET form and has a name");
        var end = Regex.Match(html, @"<div class=""mat-toolbar__end"">(.*?)</div>\s*</div>", RegexOptions.Singleline);
        Assert.True(end.Success);
        Assert.Contains("data-dialog-open=\"tb2-filter\"", end.Groups[1].Value);
        Assert.Contains("data-dialog-open=\"tb2-sort\"", end.Groups[1].Value);
        Assert.DoesNotContain("tb2-search", html);
        Assert.True(html.IndexOf("mat-toolbar__search", StringComparison.Ordinal) < html.IndexOf("mat-toolbar__end", StringComparison.Ordinal), "the field is at the left, the buttons at the right");
    }

    [Fact]
    public async Task The_rulebook_is_searched_in_a_field_of_the_toolbar_that_belongs_to_the_get_form_and_filter_and_sort_stand_at_its_right_end()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules");

        var toolbar = Regex.Match(html, @"<div class=""mat-toolbar"".*?</div>\s*</div>\s*(?=<(?:div|form|table)\b)", RegexOptions.Singleline).Value;
        Assert.Contains("<input class=\"mat-input\" type=\"search\" id=\"rules-q\" name=\"q\" form=\"rules-form\"", toolbar);
        Assert.Contains("<form id=\"rules-form\" method=\"get\" action=\"/Rules\" hidden>", html);
        var end = Regex.Match(toolbar, @"<div class=""mat-toolbar__end"">(.*)", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains("data-dialog-open=\"rules-filter\"", end);
        Assert.Contains("data-dialog-open=\"rules-sort\"", end);
        Assert.DoesNotContain("rules-search", html);
    }

    [Fact]
    public async Task A_search_in_the_field_keeps_the_filter_and_the_sort_in_effect_and_needs_no_chip()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules?q=toolbar&area=LIST&sort=title");

        Assert.Contains("value=\"toolbar\"", html);
        Assert.DoesNotContain("Search: toolbar", html);
        Assert.Contains("Remove the filter", html);
    }

    [Fact]
    public void The_script_puts_the_cursor_in_a_search_field_when_the_slash_is_pressed()
    {
        var script = TestPaths.ReadToolkit("js/matui.js");

        Assert.Contains("if (sc.matches('input, textarea')) { sc.focus(); sc.select(); } else { sc.click(); }", script);
    }

    [Theory]
    [InlineData("/Components")]
    [InlineData("/Layout")]
    [InlineData("/Utilities")]
    public async Task A_page_with_an_index_offers_it_as_an_icon_in_the_header_bar_and_not_as_a_button_in_the_content(string url)
    {
        var html = await factory.CreateClient().GetStringAsync(url);

        var header = html[html.IndexOf("<header class=\"mat-topbar\"", StringComparison.Ordinal)..html.IndexOf("</header>", StringComparison.Ordinal)];
        Assert.Contains("<div class=\"mat-topbar__actions\">", header);
        Assert.Matches(@"<button type=""button"" class=""mat-btn mat-btn--ghost mat-btn--icon mat-topbar__index"" data-dialog-open=""toc-dialog"" data-tip=""On this page"" aria-label=""On this page, jump to a section""", header);
        Assert.DoesNotContain("mat-toc-bar", html);
        Assert.DoesNotContain("Jump to a section</span>", html);
        Assert.Contains("id=\"toc-dialog\"", html);
    }

    [Fact]
    public async Task A_page_without_an_index_has_no_icon_for_it()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules");

        Assert.DoesNotContain("mat-topbar__index", html);
    }

    [Fact]
    public void The_toolkit_shows_the_index_icon_only_where_the_index_is_not_beside_the_content()
    {
        var css = Css("matui.lists.css");

        Assert.Matches(@"@media \(min-width: 1100px\) \{\s*\.mat-topbar__actions \.mat-topbar__index \{ display: none; \}", css);
        Assert.DoesNotContain("mat-toc-bar", css);
        Assert.DoesNotContain("mat-toc-bar", string.Concat(ComponentCatalog.All.SelectMany(c => c.Examples).Select(e => e.Html)));
    }

    [Fact]
    public void The_rules_name_the_index_icon_as_the_one_thing_of_a_page_that_belongs_in_the_header_bar()
    {
        Assert.Contains("icon at the right end of the header bar", RuleBook.All.Single(r => r.Id == "SHELL-16").Statement);
        Assert.Contains("its index (SHELL-16)", RuleBook.All.Single(r => r.Id == "SHELL-17").Statement);
    }
}
