using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>SHELL-20: the search in the header bar of every page of the site and of the Studio, not of the sample apps and not of the pages without a menu.</summary>
public sealed partial class HeaderSearchTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Field = "<div class=\"mat-topbar__search\">";

    [GeneratedRegex(@"<header class=""mat-topbar"">(.*?)</header>", RegexOptions.Singleline)]
    private static partial Regex Bar();

    private static string BarOf(string html)
    {
        var match = Bar().Match(html);
        Assert.True(match.Success, "no header bar");
        return match.Groups[1].Value;
    }

    private static void AssertSearchInBar(string url, string html)
    {
        var bar = BarOf(html);
        Assert.True(bar.Contains(Field, StringComparison.Ordinal), $"{url}: no search in the header bar (SHELL-20)");
        // after the breadcrumb, before the icons, one field
        Assert.True(bar.IndexOf("</nav>", StringComparison.Ordinal) < bar.IndexOf(Field, StringComparison.Ordinal), $"{url}: the search is before the breadcrumb");
        if (bar.Contains("mat-topbar__actions", StringComparison.Ordinal))
        {
            Assert.True(bar.IndexOf(Field, StringComparison.Ordinal) < bar.IndexOf("mat-topbar__actions", StringComparison.Ordinal), $"{url}: the search is after the icons");
        }

        Assert.Single(Regex.Matches(html, "id=\"site-search\""));
        Assert.Matches(@"<form role=""search"" action=""/Search"" method=""get"">\s*<label class=""mat-sr-only"" for=""site-search"">", bar);
        Assert.Matches(@"<div class=""mat-search"" data-autocomplete data-search-url=""/api/search"" data-results-url=""/Search"" data-recent=""site"" data-key=""/,mod\+k"">", bar);
        Assert.Matches(@"<input class=""mat-search__input"" type=""search"" id=""site-search"" name=""q"" placeholder=""Search""", bar);
        Assert.DoesNotContain(" style=", bar);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Rules")]
    [InlineData("/Rules/Detail?id=SHELL-20")]
    [InlineData("/Identity")]
    [InlineData("/Identity/Palette")]
    [InlineData("/Foundations")]
    [InlineData("/Foundations/Tokens")]
    [InlineData("/Foundations/Icons")]
    [InlineData("/Shell")]
    [InlineData("/Components")]
    [InlineData("/Layout")]
    [InlineData("/Utilities")]
    [InlineData("/Patterns")]
    [InlineData("/Builder")]
    [InlineData("/Download")]
    [InlineData("/Changelog")]
    [InlineData("/Search")]
    [InlineData("/Search?q=button")]
    public async Task Every_page_of_the_site_has_the_search_in_its_header_bar(string url)
    {
        var html = await factory.CreateClient().GetStringAsync(url);

        AssertSearchInBar(url, html);
    }

    [Fact]
    public async Task A_page_with_an_index_icon_has_the_search_before_the_icons_and_the_order_is_the_toolkits_whatever_the_markup()
    {
        var html = await factory.CreateClient().GetStringAsync("/Components");
        var bar = BarOf(html);

        Assert.Contains("mat-topbar__index", bar);
        Assert.True(bar.IndexOf(Field, StringComparison.Ordinal) < bar.IndexOf("mat-topbar__actions", StringComparison.Ordinal));
        var css = TestPaths.ReadToolkit("css/matui.shell.css");
        // the burger and the breadcrumb keep the order 0 they have always had; the search is 1 and the icons 2, whatever the markup says
        Assert.DoesNotContain(".mat-topbar > .mat-crumbs { order", css);
        Assert.Contains(".mat-topbar > .mat-topbar__search { order: 1;", css);
        Assert.Contains(".mat-topbar > .mat-topbar__actions { order: 2; }", css);
    }

    [Fact]
    public async Task The_sample_apps_and_the_pages_without_a_menu_have_no_search_of_the_site()
    {
        var client = factory.CreateClient();
        foreach (var url in new[] { "/Preview?app=matmail&view=list", "/Preview?app=matpaper&view=dashboard", "/Studio/Login" })
        {
            var html = await client.GetStringAsync(url);
            Assert.DoesNotContain("mat-topbar__search", html);
            Assert.DoesNotContain("id=\"site-search\"", html);
        }

        // a sample app keeps its own header bar, with its own icons
        Assert.Contains("class=\"mat-topbar\"", await client.GetStringAsync("/Preview?app=matmail&view=list"));
    }

    [Fact]
    public async Task Every_page_of_the_studio_has_it_too()
    {
        using var studio = new StudioHarness(factory);
        await studio.SignIn();
        var urls = new[]
        {
            "/Studio", "/Studio/Apps", "/Studio/Apps/Edit", "/Studio/Rules", "/Studio/Components", "/Studio/Brand", "/Studio/Brand/Components", "/Studio/Brand/Assets", "/Studio/Company",
            "/Studio/Checks", "/Studio/Releases", "/Studio/Releases/WhiteLabel", "/Studio/Versions", "/Studio/Activity", "/Studio/Users", "/Studio/Account",
        };
        foreach (var url in urls)
        {
            AssertSearchInBar(url, await studio.Get(url));
        }
    }

    [Fact]
    public async Task A_blank_site_has_it_too_and_names_nothing_of_the_family_in_it()
    {
        using var blank = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_TEMPLATE", "blank");
            builder.UseSetting("MATDESIGN_COMPANY", "Acme Industries");
        });
        var client = blank.CreateClient();
        foreach (var url in new[] { "/", "/Rules", "/Components", "/Identity", "/Search?q=button", "/Search" })
        {
            var html = await client.GetStringAsync(url);
            AssertSearchInBar(url, html);
            Assert.DoesNotContain("MatDesign", html.Substring(html.IndexOf("<header class=\"mat-topbar\">", StringComparison.Ordinal), html.IndexOf("</header>", StringComparison.Ordinal) - html.IndexOf("<header class=\"mat-topbar\">", StringComparison.Ordinal)));
        }

        var search = await client.GetStringAsync("/Search?q=matpaper");
        Assert.DoesNotContain("href=\"/Identity/App?id=matpaper\"", search);
        var api = await client.GetStringAsync("/api/search?q=matpaper");
        Assert.DoesNotContain("\"kind\":\"app\"", api);
    }

    [Fact]
    public void The_rule_is_there_with_what_it_says_and_what_it_does_not()
    {
        var rule = RuleBook.Get("SHELL-20")!;

        Assert.Equal("SHELL", rule.Area);
        Assert.Equal("Search in the header bar", rule.Title);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.InRange(rule.Statement.Length, 10, 1200);
        Assert.Contains("mat-topbar__search", rule.Statement);
        Assert.Contains("SHELL-17", rule.Statement);
        Assert.Contains("FORM-23", rule.Statement);
        Assert.Contains("640 px", rule.Statement);
        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.Contains("mat-topbar__search", rule.Do);
        Assert.False(string.IsNullOrWhiteSpace(rule.Dont));
        Assert.StartsWith("Test", rule.Check);
        // the component that documents the header bar names the rule, and shows the search in an example
        var topbar = ComponentCatalog.All.Single(c => c.Id == "topbar");
        Assert.Contains("SHELL-20", topbar.Rules);
        Assert.Contains(topbar.Examples, e => e.Html.Contains("mat-topbar__search", StringComparison.Ordinal) && e.Frame == "bar");
    }
}
