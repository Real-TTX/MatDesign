using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Web.Pages;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The page of the results (<c>/Search</c>): a list page like the others, grouped by kind with counts, a filter by kind, a pager, marks that cannot be markup, an empty state with a way on.</summary>
public sealed partial class SearchPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SearchPageTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"<li class=""mat-item"">")]
    private static partial Regex Row();

    [GeneratedRegex(@"<h2 id=""results-([a-z]+)"">(\w+) <span class=""mat-badge mat-badge--soft"">(\d+)</span></h2>")]
    private static partial Regex GroupHeading();

    private async Task<string> Page(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Theory]
    [InlineData("/Search")]
    [InlineData("/Search?q=button")]
    [InlineData("/Search?q=button&kind=rule")]
    [InlineData("/Search?q=zzzzzzzz")]
    [InlineData("/Search?q=%3Cscript%3Ealert(1)%3C%2Fscript%3E")]
    [InlineData("/Search?q=a&kind=icon&page=999")]
    [InlineData("/Search?q=a&kind=nothing&page=x")]
    public async Task The_page_follows_the_rules_of_a_page_of_the_shell(string url)
    {
        var html = await Page(url);

        Assert.Single(Heading().Matches(html));
        Assert.Contains("class=\"mat-nav\"", html);
        Assert.Contains("class=\"mat-crumbs\"", html);
        Assert.Contains("<main class=\"mat-page", html);
        Assert.Matches(@"<title>[^<]*Search", html);
        Assert.DoesNotMatch(InlineStyle(), html);
        Assert.DoesNotContain("<script>alert", html);
        // the toolbar is above the list, and the search of the page is a field in it (LIST-03)
        Assert.Contains("class=\"mat-input-icon mat-toolbar__search\"", html);
        Assert.True(html.IndexOf("class=\"mat-toolbar\"", StringComparison.Ordinal) < html.IndexOf("class=\"mat-empty\"", StringComparison.Ordinal) || !html.Contains("class=\"mat-empty\"", StringComparison.Ordinal));
        Assert.Contains("name=\"kind\"", html);
    }

    [Fact]
    public async Task Without_a_kind_the_results_are_groups_with_their_counts_the_best_few_of_each_and_a_link_to_all_of_one()
    {
        var html = await Page("/Search?q=button");
        var groups = GroupHeading().Matches(html).Select(m => (Kind: m.Groups[1].Value, Name: m.Groups[2].Value, Count: int.Parse(m.Groups[3].Value))).ToList();

        Assert.True(groups.Count >= 3, "components, rules and more");
        Assert.Equal(("component", "Components"), (groups[0].Kind, groups[0].Name));
        Assert.True(groups[0].Count > SearchModel.PerKind);
        Assert.Contains($"Show all {groups[0].Count} components", html);
        Assert.Contains("href=\"/Search?q=button&amp;kind=component\"", html);
        // at most PerKind rows of each kind, in all at most that many times the groups
        Assert.True(Row().Matches(html).Count <= groups.Count * SearchModel.PerKind);
        Assert.Equal(groups.Sum(g => Math.Min(g.Count, SearchModel.PerKind)), Row().Matches(html).Count);
        Assert.Contains("href=\"/Components/buttons\"", html);
        Assert.DoesNotContain("class=\"mat-pager\"", html);
        // what the toolbar says: all the results, and the filter has the kinds with their counts
        Assert.Matches(@"\d+ results", html);
        foreach (var g in groups)
        {
            Assert.Contains($"<option value=\"{g.Kind}\">{g.Name} ({g.Count})</option>", html);
        }
    }

    [Fact]
    public async Task With_a_kind_the_results_are_one_list_of_that_kind_in_pages_with_the_pager_under_it()
    {
        var html = await Page("/Search?q=a&kind=icon");

        Assert.Contains("<h2 id=\"results-kind\">Icons ", html);
        Assert.Equal(SearchModel.PageSize, Row().Matches(html).Count);
        Assert.Contains("class=\"mat-pager\"", html);
        Assert.True(html.IndexOf("class=\"mat-itemlist", StringComparison.Ordinal) < html.IndexOf("class=\"mat-pager\"", StringComparison.Ordinal));
        Assert.Contains("href=\"/Search?q=a&amp;kind=icon&amp;page=2\"", html);
        Assert.Contains("All kinds of results", html);
        Assert.DoesNotContain("results-component", html);

        var second = await Page("/Search?q=a&kind=icon&page=2");
        Assert.Equal(SearchModel.PageSize, Row().Matches(second).Count);
        Assert.NotEqual(Regex.Matches(html, @"href=""(/Foundations/[^""]+)""").Select(m => m.Groups[1].Value), Regex.Matches(second, @"href=""(/Foundations/[^""]+)""").Select(m => m.Groups[1].Value));
        // a page that is too far is the last one
        var last = await Page("/Search?q=a&kind=icon&page=9999");
        Assert.True(Row().Matches(last).Count is > 0 and <= SearchModel.PageSize);
        // a short list needs no pager (LIST-06)
        Assert.DoesNotContain("class=\"mat-pager\"", await Page("/Search?q=otp&kind=component"));
    }

    [Fact]
    public async Task The_words_that_were_found_are_marked_in_the_titles_and_a_title_cannot_be_markup()
    {
        var html = await Page("/Search?q=dropdown&kind=component");
        Assert.Contains("<mark class=\"mat-search__hit\">Dropdown</mark>", html);

        // the query is echoed encoded in the field, the title, the heading and the empty state
        var hostile = await Page("/Search?q=" + Uri.EscapeDataString("<img src=x onerror=alert(1)>\"'&"));
        Assert.DoesNotContain("<img src=x", hostile);
        Assert.DoesNotContain("onerror=alert(1)>", hostile);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", hostile);
        Assert.Contains("class=\"mat-empty\"", hostile);
        Assert.Contains("value=\"&lt;img", hostile);
    }

    [Fact]
    public async Task An_empty_search_and_a_search_without_a_result_have_a_way_on_with_a_rule_id_a_component_and_a_token()
    {
        foreach (var url in new[] { "/Search", "/Search?q=zzzzzzzz", "/Search?q=---" })
        {
            var html = await Page(url);

            Assert.Contains("class=\"mat-empty\"", html);
            Assert.Contains("Try a rule id such as", html);
            Assert.Contains("href=\"/Search?q=FORM-15\"", html);
            Assert.Contains("href=\"/Search?q=button\"", html);
            Assert.Contains("href=\"/Search?q=--mat-accent\"", html);
            Assert.DoesNotContain("class=\"mat-itemlist", html);
        }

        var none = WebUtility.HtmlDecode(await Page("/Search?q=zzzzzzzz"));
        Assert.Contains("No results for “zzzzzzzz”", none);
        Assert.Contains("Every word you type has to be found", none);
        Assert.Contains("Clear the search", none);
        Assert.Contains("Search the design system", await Page("/Search"));
    }

    [Fact]
    public async Task The_kinds_of_a_query_are_in_the_filter_and_a_kind_without_a_hit_is_not()
    {
        var html = await Page("/Search?q=otp");
        Assert.Contains("name=\"kind\"", html);
        var options = Regex.Matches(html, @"<option value=""([a-z]+)""[^>]*>[A-Za-z]+ \((\d+)\)</option>").Select(m => m.Groups[1].Value).ToList();

        Assert.Contains("component", options);
        Assert.DoesNotContain("app", options);
        Assert.Equal(options.Distinct().Count(), options.Count);
    }

    [Fact]
    public async Task The_page_is_in_the_menu_of_no_one_and_the_field_of_the_header_is_on_it_too()
    {
        var html = await Page("/Search?q=button");

        Assert.DoesNotContain("href=\"/Search\"", html.Substring(html.IndexOf("<aside class=\"mat-nav\"", StringComparison.Ordinal), html.IndexOf("</aside>", StringComparison.Ordinal) - html.IndexOf("<aside class=\"mat-nav\"", StringComparison.Ordinal)));
        Assert.Contains("class=\"mat-topbar__search\"", html);
        // two search fields on the page, two ids: the header's and the toolbar's
        Assert.Contains("id=\"site-search\"", html);
        Assert.Contains("id=\"results-q\"", html);
        Assert.Equal(1, html.Split("id=\"site-search\"").Length - 1);
    }
}
