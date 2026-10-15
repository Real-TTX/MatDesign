using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The styleguide that every release carries (the rules, every component with its examples drawn live and as markup, the apps) and the design data next to it, samples included.</summary>
public sealed partial class StyleguideTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    private (string Html, DesignData Design) Page()
    {
        var design = factory.Services.GetRequiredService<DesignData>();
        var toolkit = factory.Services.GetRequiredService<ToolkitFiles>();
        return (Styleguide.Html(design, toolkit, "1.2.3", toolkit.Hash), design);
    }

    [Fact]
    public void The_page_names_the_release_and_uses_the_files_of_the_release_next_to_it()
    {
        var (html, _) = Page();

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<title>MatUi 1.2.3 – styleguide</title>", html);
        Assert.Contains("src=\"matui.boot.js\"", html);
        Assert.Contains("href=\"matui.css\"", html);
        Assert.Contains("href=\"styleguide.css\"", html);
        Assert.Contains("src=\"matui.js\"", html);
        Assert.DoesNotContain("href=\"/", html);
        Assert.DoesNotContain("src=\"/", html);
    }

    [Fact]
    public void Every_rule_every_component_and_every_app_is_on_the_page()
    {
        var (html, design) = Page();

        foreach (var rule in design.Rules)
        {
            Assert.Contains($"id=\"rule-{rule.Id}\"", html);
        }

        foreach (var component in design.Components)
        {
            Assert.Contains($"id=\"{component.Id}\"", html);
        }

        foreach (var app in design.Apps)
        {
            Assert.Contains(app.Name, html);
        }
    }

    [Fact]
    public void A_live_example_is_drawn_as_it_is_and_every_example_has_its_markup_to_copy()
    {
        var (html, design) = Page();

        foreach (var ex in design.Components.SelectMany(c => c.Examples))
        {
            if (ex.Live && ex.Html.Length > 0)
            {
                Assert.Contains(Styleguide.Live(ex.Html), html);
            }

            Assert.Contains(MatDesign.Web.Studio.CodeHighlighter.Of(ex.Js ?? ex.Html, ex.Js is not null), html);
        }
    }

    [Fact]
    public void The_page_follows_the_contract_of_the_documentation()
    {
        var (html, _) = Page();

        Assert.DoesNotContain(" style=\"", html);
        Assert.DoesNotContain("<style", html);
        Assert.Single(Regex.Matches(html, "<main "));
        Assert.Single(Regex.Matches(html, "<h1"));
        Assert.Single(Regex.Matches(html, "<symbol id=\"mat-i-book\""));

        var ids = IdAttribute().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        var twice = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(twice.Count == 0, "ids that appear twice: " + string.Join(", ", twice));
    }

    [Fact]
    public void The_stylesheet_of_the_page_uses_the_tokens_and_no_colour_of_its_own()
    {
        var css = Styleguide.Css();

        Assert.DoesNotContain("#", css.Replace("#main", string.Empty));
        Assert.DoesNotContain("!important", css);
        Assert.DoesNotContain("rgb(", css);
        Assert.Contains("var(--mat-border)", css);
    }

    [Fact]
    public async Task A_release_carries_the_styleguide_and_the_design_data_with_the_samples()
    {
        using var studio = new StudioHarness(factory);
        await studio.SignIn();
        var response = await studio.Post("/Studio/Releases/New", "/Studio/Releases/New", new Dictionary<string, string> { ["Version"] = Ver.First, ["Note"] = "" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var page = await studio.Client.GetAsync($"/releases/{Ver.First}/styleguide.html");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType!.MediaType);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains($"MatUi {Ver.First} – styleguide", html);
        Assert.Contains("id=\"tabs\"", html);
        Assert.Equal(HttpStatusCode.OK, (await studio.Client.GetAsync($"/releases/{Ver.First}/styleguide.css")).StatusCode);

        using var data = JsonDocument.Parse(await studio.Get($"/releases/{Ver.First}/design.json"));
        var tabs = data.RootElement.GetProperty("components").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "tabs");
        Assert.True(tabs.GetProperty("examples").GetArrayLength() >= 2);
        Assert.Contains("role=\"tablist\"", tabs.GetProperty("examples")[0].GetProperty("html").GetString());
        Assert.True(tabs.GetProperty("behaviour").GetArrayLength() > 0);
        var rule = data.RootElement.GetProperty("rules").EnumerateArray().First(r => r.GetProperty("why").ValueKind == JsonValueKind.String);
        Assert.NotEmpty(rule.GetProperty("why").GetString()!);
    }
}
