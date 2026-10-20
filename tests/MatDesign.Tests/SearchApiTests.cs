using System.Diagnostics;
using System.Net;
using System.Text.Json;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The addresses of the search: <c>/api/search</c> for the popup of the toolkit and <c>/api/demo/search</c> for the examples of the catalogue.</summary>
public sealed class SearchApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SearchApiTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(HttpResponseMessage Response, JsonElement Body)> Get(string url)
    {
        var response = await _client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        return (response, JsonDocument.Parse(text).RootElement);
    }

    [Fact]
    public async Task The_answer_is_json_with_groups_of_items_that_have_what_the_script_of_the_toolkit_reads()
    {
        var (response, body) = await Get("/api/search?q=button&limit=8");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("button", body.GetProperty("query").GetString());
        Assert.True(body.GetProperty("total").GetInt32() > 50);
        var groups = body.GetProperty("groups").EnumerateArray().ToList();
        Assert.NotEmpty(groups);
        Assert.Equal("Components", groups[0].GetProperty("name").GetString());
        Assert.Equal("component", groups[0].GetProperty("kind").GetString());
        Assert.True(groups[0].GetProperty("count").GetInt32() > 5);
        var items = groups.SelectMany(g => g.GetProperty("items").EnumerateArray()).ToList();
        Assert.Equal(8, items.Count);
        foreach (var item in items)
        {
            Assert.False(string.IsNullOrEmpty(item.GetProperty("title").GetString()));
            Assert.False(string.IsNullOrEmpty(item.GetProperty("text").GetString()));
            Assert.StartsWith("/", item.GetProperty("href").GetString());
            Assert.Matches("^[a-z0-9-]+$", item.GetProperty("icon").GetString()!);
            Assert.Contains(item.GetProperty("kind").GetString(), SearchKinds.All.Select(SearchKinds.Name));
            Assert.False(string.IsNullOrEmpty(item.GetProperty("id").GetString()));
        }

        Assert.Equal("buttons", groups[0].GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("/Components/buttons", groups[0].GetProperty("items")[0].GetProperty("href").GetString());
    }

    [Fact]
    public async Task The_limit_the_kind_and_an_empty_text_are_handled()
    {
        Assert.Equal(3, (await Get("/api/search?q=button&limit=3")).Body.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("items").GetArrayLength()));
        // a limit that is not a number, is below one or above fifty is the default or the nearest
        Assert.Equal(8, (await Get("/api/search?q=a&limit=abc")).Body.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("items").GetArrayLength()));
        Assert.Equal(1, (await Get("/api/search?q=a&limit=0")).Body.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("items").GetArrayLength()));
        Assert.Equal(50, (await Get("/api/search?q=a&limit=9999")).Body.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("items").GetArrayLength()));

        var rules = (await Get("/api/search?q=button&kind=rule&limit=4")).Body;
        Assert.Equal(1, rules.GetProperty("groups").GetArrayLength());
        Assert.Equal("rule", rules.GetProperty("groups")[0].GetProperty("kind").GetString());
        Assert.Equal(rules.GetProperty("total").GetInt32(), rules.GetProperty("groups")[0].GetProperty("count").GetInt32());
        Assert.Equal(4, rules.GetProperty("groups")[0].GetProperty("items").GetArrayLength());
        Assert.Equal("Rules", (await Get("/api/search?q=button&kind=Rules")).Body.GetProperty("groups")[0].GetProperty("name").GetString());

        var empty = (await Get("/api/search?q=")).Body;
        Assert.Equal(0, empty.GetProperty("total").GetInt32());
        Assert.Equal(0, empty.GetProperty("groups").GetArrayLength());
        Assert.Equal(0, (await Get("/api/search")).Body.GetProperty("groups").GetArrayLength());
        Assert.Equal(0, (await Get("/api/search?q=zzzzzzzzzzzz")).Body.GetProperty("groups").GetArrayLength());

        var (bad, error) = await Get("/api/search?q=button&kind=nothing");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("kind is one of rule, component, token", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_query_is_cut_at_a_hundred_characters_and_a_hostile_one_comes_back_as_data_only()
    {
        var (_, long1) = await Get("/api/search?q=" + new string('x', 400));
        Assert.Equal(SearchIndex.MaxQueryLength, long1.GetProperty("query").GetString()!.Length);

        var hostile = "<script>alert(1)</script><img src=x onerror=alert(2)>";
        var response = await _client.GetAsync("/api/search?q=" + Uri.EscapeDataString(hostile));
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        // JSON escapes what could open a tag: the answer is never HTML, and a browser is told not to guess
        Assert.DoesNotContain("<script>", raw);
        Assert.DoesNotContain("<img", raw);
        Assert.Contains("\\u003Cscript\\u003E", raw);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(hostile.Length, JsonDocument.Parse(raw).RootElement.GetProperty("query").GetString()!.Length);
        // the page that is asked for HTML does not turn it into some
        Assert.Equal("application/json", (await _client.GetAsync("/api/search?q=%3Cb%3E")).Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_answer_comes_with_an_etag_for_the_version_of_the_data_and_the_same_question_is_answered_with_304()
    {
        var first = await _client.GetAsync("/api/search?q=dropdown&limit=5");
        var etag = first.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrEmpty(etag));
        Assert.Equal("no-cache", first.Headers.CacheControl?.ToString());

        var again = new HttpRequestMessage(HttpMethod.Get, "/api/search?q=dropdown&limit=5");
        again.Headers.TryAddWithoutValidation("If-None-Match", etag);
        Assert.Equal(HttpStatusCode.NotModified, (await _client.SendAsync(again)).StatusCode);

        // another question, another limit and another kind are other answers
        Assert.NotEqual(etag, (await _client.GetAsync("/api/search?q=dropdown&limit=6")).Headers.ETag?.Tag);
        Assert.NotEqual(etag, (await _client.GetAsync("/api/search?q=dropdowns&limit=5")).Headers.ETag?.Tag);
        Assert.NotEqual(etag, (await _client.GetAsync("/api/search?q=dropdown&limit=5&kind=rule")).Headers.ETag?.Tag);
        // the case of the text is not a question of its own
        Assert.Equal(etag, (await _client.GetAsync("/api/search?q=DROPDOWN&limit=5")).Headers.ETag?.Tag);
    }

    [Fact]
    public async Task A_query_of_the_api_is_answered_fast_from_the_cache_and_without_one()
    {
        _ = await _client.GetAsync("/api/search?q=tooltip");
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 30; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/search?q=tooltip")).StatusCode);
        }

        Assert.True(watch.Elapsed.TotalMilliseconds / 30 < 50, $"{watch.Elapsed.TotalMilliseconds / 30:0.0} ms for a request in memory");
    }

    [Fact]
    public async Task The_search_is_public_and_only_reads()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/search?q=button")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _client.PostAsync("/api/search?q=button", new StringContent(string.Empty))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _client.PostAsync("/api/demo/search?q=button", new StringContent(string.Empty))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/Search?q=button")).StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------- the demo of the catalogue

    [Fact]
    public async Task The_demo_finds_documents_people_and_settings_by_every_word_and_keeps_nothing()
    {
        var (response, body) = await Get("/api/demo/search?q=report&limit=6");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var groups = body.GetProperty("groups").EnumerateArray().ToList();
        Assert.Contains(groups, g => g.GetProperty("name").GetString() == "Documents");
        var titles = groups.SelectMany(g => g.GetProperty("items").EnumerateArray()).Select(i => i.GetProperty("title").GetString()).ToList();
        Assert.Contains("Quarterly report", titles);
        Assert.True(titles.Count <= 6);
        Assert.All(groups.SelectMany(g => g.GetProperty("items").EnumerateArray()), i => Assert.Equal("#sr-remote", i.GetProperty("href").GetString()));

        var two = (await Get("/api/demo/search?q=annual%20report")).Body.GetProperty("groups").EnumerateArray().SelectMany(g => g.GetProperty("items").EnumerateArray()).ToList();
        Assert.Single(two);
        Assert.Equal("Annual report 2025", two[0].GetProperty("title").GetString());
        Assert.Equal(0, (await Get("/api/demo/search?q=nothingofthekind")).Body.GetProperty("groups").GetArrayLength());
        // accents and case do not matter here either
        Assert.NotEqual(0, (await Get("/api/demo/search?q=QUARTERLY")).Body.GetProperty("groups").GetArrayLength());
    }

    [Fact]
    public async Task The_demo_fails_on_purpose_for_error_and_waits_a_moment_for_slow()
    {
        var (failed, error) = await Get("/api/demo/search?q=error");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Contains("on purpose", error.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.InternalServerError, (await _client.GetAsync("/api/demo/search?q=report%20error")).StatusCode);

        var watch = Stopwatch.StartNew();
        var (slow, body) = await Get("/api/demo/search?q=slow%20report");
        Assert.Equal(HttpStatusCode.OK, slow.StatusCode);
        Assert.True(watch.ElapsedMilliseconds >= 1200, $"{watch.ElapsedMilliseconds} ms");
        Assert.NotEqual(0, body.GetProperty("groups").GetArrayLength());
        // "slow" alone waits and then shows everything
        Assert.True((await Get("/api/demo/search?q=slow&limit=50")).Body.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("items").GetArrayLength()) >= 10);
    }

    [Fact]
    public async Task Every_address_that_an_example_of_the_search_asks_leads_to_an_endpoint_that_answers_json()
    {
        var addresses = ComponentCatalog.All.SelectMany(c => c.Examples)
            .SelectMany(e => System.Text.RegularExpressions.Regex.Matches(e.Html, @"data-search-url=""([^""]+)""").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.Contains("/api/demo/search", addresses);
        Assert.Contains("/api/search", addresses);
        foreach (var url in addresses)
        {
            var response = await _client.GetAsync(url + "?q=report&limit=5");
            Assert.True(response.IsSuccessStatusCode, $"{url}: {(int)response.StatusCode}");
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.TryGetProperty("groups", out _));
        }
    }
}
