using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The whole site in memory: every page answers, follows the shell rules and links only to pages that exist. A link that leads nowhere or a page
/// without a breadcrumb is found here before anybody sees it.
/// </summary>
public partial class SiteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SiteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [GeneratedRegex(@"\b(?:href|src)=""(/[^""#]*)""")]
    private static partial Regex LocalLink();

    [GeneratedRegex(@"<pre\b.*?</pre>", RegexOptions.Singleline)]
    private static partial Regex PreBlock();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"<title>([^<]*)</title>")]
    private static partial Regex Title();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"<img\b(?![^>]*\balt=)")]
    private static partial Regex ImageWithoutAlt();

    private static IEnumerable<string> Pages()
    {
        yield return "/";
        yield return "/Rules";
        yield return "/Rules/Detail?id=SHELL-01";
        yield return "/Identity";
        yield return "/Identity/App?id=matmail";
        yield return "/Identity/Palette";
        yield return "/Identity/Audit";
        yield return "/Foundations";
        yield return "/Foundations/Type";
        yield return "/Foundations/Space";
        yield return "/Foundations/Motion";
        yield return "/Foundations/Icons";
        yield return "/Foundations/Tokens";
        yield return "/Shell";
        yield return "/Components";
        yield return "/Patterns";
        yield return "/Builder";
        yield return "/Builder?app=matmail&accent=%23ff6600";
        yield return "/Download";
    }

    private async Task<(HttpStatusCode Status, string Html)> Get(string url)
    {
        var response = await _client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Every_page_answers_and_follows_the_shell_rules()
    {
        var problems = new List<string>();
        foreach (var url in Pages())
        {
            var (status, html) = await Get(url);
            if (status != HttpStatusCode.OK)
            {
                problems.Add($"{url}: {(int)status}");
                continue;
            }

            if (!html.Contains("<html lang=\"en\"")) { problems.Add($"{url}: no lang on html"); }
            if (Title().Match(html) is not { Success: true } title || title.Groups[1].Value.Trim().Length < 3) { problems.Add($"{url}: no title"); }
            if (Heading().Matches(html).Count != 1) { problems.Add($"{url}: {Heading().Matches(html).Count} h1"); }
            if (!html.Contains("class=\"mat-nav\"")) { problems.Add($"{url}: no menu"); }
            if (!html.Contains("class=\"mat-nav__brand\"")) { problems.Add($"{url}: no logo above the menu"); }
            if (!html.Contains("class=\"mat-crumbs\"")) { problems.Add($"{url}: no breadcrumb (SHELL-08)"); }
            if (!html.Contains("data-theme-toggle")) { problems.Add($"{url}: no theme button (SHELL-04)"); }
            if (!html.Contains("class=\"mat-user\"")) { problems.Add($"{url}: no user (SHELL-05)"); }
            if (!html.Contains("class=\"mat-nav__version\"")) { problems.Add($"{url}: no version (SHELL-06)"); }
            if (!html.Contains("<main class=\"mat-page")) { problems.Add($"{url}: no main landmark"); }
            if (InlineStyle().IsMatch(html)) { problems.Add($"{url}: an inline style (CODE-02)"); }
            if (ImageWithoutAlt().IsMatch(html)) { problems.Add($"{url}: an image without alt"); }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task The_html_tag_has_only_the_attributes_that_have_a_value()
    {
        // An empty data-theme counts as present: the boot script would never apply the theme the user saved in the browser.
        var (_, html) = await Get("/");
        Assert.Matches(@"<html lang=""en"" data-app=""matdesign""\s*>", html);
        Assert.DoesNotContain("data-theme=\"\"", html);
        Assert.DoesNotContain("data-accent=\"\"", html);
        Assert.DoesNotContain("data-preview", html);

        var (_, preview) = await Get("/Preview?app=matmail&theme=dark&view=list");
        Assert.Contains("data-theme=\"dark\"", preview);
        Assert.Contains("data-preview", preview);
        Assert.DoesNotContain("data-accent=", preview);
    }

    [Fact]
    public async Task Every_list_page_has_its_toolbar_above_the_list_and_the_three_dialogs()
    {
        foreach (var url in new[] { "/Identity", "/Identity/Palette", "/Identity/Audit", "/Rules", "/Foundations/Icons", "/Foundations/Tokens", "/Components", "/Patterns" })
        {
            var (_, html) = await Get(url);
            var toolbar = html.IndexOf("class=\"mat-toolbar\"", StringComparison.Ordinal);
            Assert.True(toolbar > 0, $"{url}: no toolbar (LIST-02)");
            Assert.True(toolbar < html.IndexOf("<table class=\"mat-table", StringComparison.Ordinal) || !html.Contains("<table class=\"mat-table"), $"{url}: the table is above the toolbar");
            foreach (var dialog in new[] { "-search\"", "-filter\"", "-sort\"" })
            {
                Assert.True(Regex.IsMatch(html, "<dialog class=\"mat-dialog[^\"]*\" id=\"[a-z]+" + dialog), $"{url}: no {dialog.Trim('-', '"')} dialog (LIST-03)");
            }
        }
    }

    [Fact]
    public async Task Every_component_and_pattern_page_answers()
    {
        var urls = ComponentCatalog.All.Select(c => $"/Components/Detail?id={c.Id}")
            .Concat(PatternCatalog.All.Select(p => $"/Patterns/Detail?id={p.Id}"))
            .Concat(RuleBook.All.Select(r => $"/Rules/Detail?id={r.Id}"))
            .Concat(AppRegistry.All.Select(a => $"/Identity/App?id={a.Id}"));
        var problems = new List<string>();
        foreach (var url in urls)
        {
            var (status, html) = await Get(url);
            if (status != HttpStatusCode.OK) { problems.Add($"{url}: {(int)status}"); }
            else if (!html.Contains("class=\"mat-crumbs\"")) { problems.Add($"{url}: no breadcrumb"); }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Theory]
    [InlineData("/Rules/Detail?id=NOPE-99")]
    [InlineData("/Components/Detail?id=nope")]
    [InlineData("/Patterns/Detail?id=nope")]
    [InlineData("/Identity/App?id=nope")]
    public async Task An_unknown_item_is_a_404(string url)
        => Assert.Equal(HttpStatusCode.NotFound, (await Get(url)).Status);

    [Theory]
    [InlineData("list")]
    [InlineData("gallery")]
    [InlineData("form")]
    [InlineData("settings")]
    [InlineData("dashboard")]
    [InlineData("auth")]
    public async Task The_sample_app_draws_every_view_in_every_accent_and_theme(string view)
    {
        foreach (var app in new[] { "matmail", "matsplit", "matarchive" })
        {
            foreach (var theme in new[] { "light", "dark" })
            {
                var (status, html) = await Get($"/Preview?app={app}&view={view}&theme={theme}");
                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Contains($"data-app=\"{app}\"", html);
                Assert.Contains($"data-theme=\"{theme}\"", html);
                Assert.Contains("data-preview", html);
            }
        }
    }

    [Fact]
    public async Task The_sample_app_takes_a_free_accent_and_ignores_rubbish()
    {
        var (_, custom) = await Get("/Preview?app=matmail&accent=%23ff6600&view=list");
        Assert.Contains("data-accent=\"custom\"", custom);
        Assert.Contains("data-custom-accent=\"#ff6600\"", custom);

        var (status, rubbish) = await Get("/Preview?app=%3Cscript%3E&accent=red&view=nonsense&theme=blue");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain("data-accent=\"custom\"", rubbish);
        Assert.DoesNotContain("<script>", rubbish);
    }

    [Fact]
    public async Task No_internal_link_or_frame_leads_nowhere()
    {
        // A crawl from the start page: every local href and src. The addresses of a kind differ in their values only (an app, a theme, a colour,
        // a page number), so each shape of address is followed with a few values: the crawl ends by itself and still reaches every kind of page.
        var seen = new HashSet<string>();
        var perShape = new Dictionary<string, int>();
        var queue = new Queue<string>(["/"]);
        var broken = new List<string>();
        var fetched = 0;

        while (queue.Count > 0)
        {
            Assert.True(seen.Count < 5000, "the crawl does not end: something generates endless addresses");
            var url = queue.Dequeue();
            if (!seen.Add(url))
            {
                continue;
            }

            var shape = Shape(url);
            if (perShape.GetValueOrDefault(shape) >= 4)
            {
                continue;
            }

            perShape[shape] = perShape.GetValueOrDefault(shape) + 1;
            fetched++;
            var response = await _client.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"{url}: {(int)response.StatusCode}");
                continue;
            }

            if (response.Content.Headers.ContentType?.MediaType != "text/html")
            {
                continue;
            }

            // addresses in code samples (inside <pre>) are examples, not links
            var html = PreBlock().Replace(await response.Content.ReadAsStringAsync(), string.Empty);
            foreach (Match link in LocalLink().Matches(html))
            {
                var target = WebUtility.HtmlDecode(link.Groups[1].Value);
                if (!seen.Contains(target) && !target.StartsWith("//", StringComparison.Ordinal))
                {
                    queue.Enqueue(target);
                }
            }
        }

        Assert.True(broken.Count == 0, "Broken links:\n" + string.Join("\n", broken));
        Assert.True(fetched > 60, $"the crawl fetched only {fetched} addresses");
    }

    /// <summary>The kind of an address: the path and the names of its parameters, without their values.</summary>
    private static string Shape(string url)
    {
        var question = url.IndexOf('?');
        return question < 0 ? url : url[..question] + "?" + string.Join("&", url[(question + 1)..].Split('&').Select(p => p.Split('=')[0]).Order());
    }

    [Fact]
    public async Task The_toolkit_is_served_as_one_bundle_with_its_version_in_the_first_line()
    {
        var css = await _client.GetAsync("/ui/matui.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal("*", css.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var text = await css.Content.ReadAsStringAsync();
        Assert.StartsWith($"/* MatUi {AppVersion.Current}", text);
        foreach (var part in ToolkitFiles.CssParts)
        {
            Assert.Contains(File.ReadAllText(TestPaths.Toolkit($"css/{part}")).Split('\n')[^2].Trim(), text);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/ui/css/nothing.css")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/ui/matui.js")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/ui/matui.boot.js")).StatusCode);
        Assert.Contains("<symbol id=\"mat-i-star\"", await _client.GetStringAsync("/ui/matui-icons.svg"));
    }

    [Fact]
    public async Task The_bundle_answers_a_repeated_request_with_304()
    {
        var first = await _client.GetAsync("/ui/matui.css");
        var request = new HttpRequestMessage(HttpMethod.Get, "/ui/matui.css");
        request.Headers.TryAddWithoutValidation("If-None-Match", first.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NotModified, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task The_archive_holds_every_file_and_a_readme()
    {
        var bytes = await _client.GetByteArrayAsync("/ui/matui.zip");
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var names = zip.Entries.Select(e => e.FullName).ToList();

        foreach (var name in new[] { "matui/matui.css", "matui/matui.js", "matui/matui.boot.js", "matui/matui-icons.svg", "matui/README.txt" })
        {
            Assert.Contains(name, names);
        }

        Assert.All(ToolkitFiles.CssParts, p => Assert.Contains($"matui/css/{p}", names));
        using var readme = new StreamReader(zip.GetEntry("matui/README.txt")!.Open());
        var text = await readme.ReadToEndAsync();
        Assert.Contains(AppVersion.Current, text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public async Task The_design_system_is_available_as_data()
    {
        async Task<JsonElement> Json(string url) => JsonDocument.Parse(await _client.GetStringAsync(url)).RootElement;

        Assert.Equal(AppRegistry.All.Count, (await Json("/api/apps")).GetArrayLength());
        Assert.Equal(RuleBook.All.Count, (await Json("/api/rules")).GetArrayLength());
        Assert.Equal(AccentPresets.All.Count, (await Json("/api/accents")).GetArrayLength());
        Assert.True((await Json("/api/tokens")).GetArrayLength() > 80);
        Assert.True((await Json("/api/icons")).GetArrayLength() > 100);
        Assert.Equal(AppVersion.Current, (await Json("/api/version")).GetProperty("toolkit").GetString());
        Assert.Equal("ok", await _client.GetStringAsync("/health"));
    }

    [Fact]
    public async Task The_site_formats_numbers_the_same_way_on_every_machine()
    {
        var (_, html) = await Get("/Download");
        Assert.Matches(@"Download the archive \(\d+\.\d KB\)", html);
    }
}
