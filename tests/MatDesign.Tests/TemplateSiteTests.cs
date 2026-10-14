using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>
/// The site for the two templates (docs/PRODUCT.md, stage 14). The demo is the site exactly as it was before there were tenants (the pages are compared with the ones that main served, up to the
/// numbers that change with the rulebook); a blank start shows the company's own names and none of the Mat apps, on every page, with the shell rules kept.
/// </summary>
public sealed partial class TemplateSiteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TemplateSiteTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [GeneratedRegex(@"<svg xmlns=""http://www\.w3\.org/2000/svg"" class=""mat-sprite"".*?</svg>", RegexOptions.Singleline)]
    private static partial Regex Sprite();

    [GeneratedRegex(@"\?v=[0-9a-f]+")]
    private static partial Regex AssetVersion();

    [GeneratedRegex(@"(?:local|nightly)-[\w-]*\d{8}|\d+\.\d+\.\d+-\d{8}")]
    private static partial Regex Build();

    [GeneratedRegex(@"https?://[^/""'<\s]+")]
    private static partial Regex Host();

    [GeneratedRegex(@"\b[0-9a-f]{10}\b")]
    private static partial Regex Hash();

    [GeneratedRegex(@"(?<![\w#.\-/])\d+(?:\.\d+)?(?![\w.\-/])")]
    private static partial Regex Number();

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""[^""]*""")]
    private static partial Regex Token();

    [GeneratedRegex(@">\s+<")]
    private static partial Regex BetweenTags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"<title>([^<]*)</title>")]
    private static partial Regex Title();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    /// <summary>
    /// A page without what changes for a reason that has nothing to do with a tenant: the icon sprite, the hash of the files, the build, the host, the numbers (a rule more in the rulebook
    /// changes a count), the anti-forgery token, and the white space between tags (the indentation of the source).
    /// </summary>
    public static string Normalize(string html)
    {
        html = html.Replace("\r\n", "\n");
        html = Sprite().Replace(html, "<!-- sprite -->");
        html = html.Replace("<!-- sprite -->\n", "<!-- sprite -->");
        html = AssetVersion().Replace(html, "?v=HASH");
        html = Build().Replace(html, "BUILD");
        html = Host().Replace(html, "http://HOST");
        html = Hash().Replace(html, "HASH");
        html = Number().Replace(html, "N");
        html = Token().Replace(html, "name=\"__RequestVerificationToken\" value=\"TOKEN\"");
        html = BetweenTags().Replace(html, "><");
        return Spaces().Replace(html, " ").Trim();
    }

    private static string Golden(string name) => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "tests", "MatDesign.Tests", "Golden", name + ".html"));

    private WebApplicationFactory<Program> Blank(string company = "Acme Industries")
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_TEMPLATE", "blank");
            builder.UseSetting("MATDESIGN_COMPANY", company);
        });

    private static async Task<string> Html(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode}");
        return await response.Content.ReadAsStringAsync();
    }

    // ---- the demo is the site as it was

    [Theory]
    [InlineData("/", "home")]
    [InlineData("/Identity", "identity")]
    [InlineData("/Identity/Palette", "palette")]
    [InlineData("/Identity/App?id=matmail", "app-matmail")]
    [InlineData("/Studio/Login", "login")]
    [InlineData("/Download", "download")]
    public async Task The_site_of_an_instance_without_a_tenant_is_the_page_main_served(string url, string golden)
    {
        var html = await Html(_factory.CreateClient(), url);

        var expected = Normalize(Golden(golden));
        var actual = Normalize(html);
        Assert.True(expected == actual, FirstDifference(expected, actual));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Identity")]
    public async Task The_demo_template_chosen_in_the_environment_is_the_same_site_as_no_tenant_at_all(string url)
    {
        var plain = await Html(_factory.CreateClient(), url);
        var chosen = await Html(_factory.WithWebHostBuilder(b => b.UseSetting("MATDESIGN_TEMPLATE", "demo")).CreateClient(), url);

        Assert.Equal(Normalize(plain), Normalize(chosen));
    }

    [Fact]
    public async Task The_menu_of_the_demo_is_the_menu_main_served_and_lists_the_audit()
    {
        var html = await Html(_factory.CreateClient(), "/");

        var menu = Normalize(Regex.Match(html, @"<aside class=""mat-nav"".*?</aside>", RegexOptions.Singleline).Value);
        var expected = Normalize(Regex.Match(Golden("home"), @"<aside class=""mat-nav"".*?</aside>", RegexOptions.Singleline).Value);

        Assert.NotEmpty(menu);
        Assert.Equal(expected, menu);
        Assert.Contains("href=\"/Identity/Audit\"", menu);
    }

    private static string FirstDifference(string expected, string actual)
    {
        var at = 0;
        while (at < expected.Length && at < actual.Length && expected[at] == actual[at])
        {
            at++;
        }

        string Around(string text) => text.Length == 0 ? string.Empty : text[Math.Max(0, at - 80)..Math.Min(text.Length, at + 160)];
        return $"The page differs from what main served at {at}:\nexpected: ...{Around(expected)}\nactual:   ...{Around(actual)}";
    }

    [Fact]
    public async Task The_demo_says_Mat_where_it_always_said_it()
    {
        var home = await Html(_factory.CreateClient(), "/");

        Assert.Contains("<h1>MatDesign</h1>", home);
        Assert.Contains("How the Mat apps are meant to look.", home);
        Assert.Contains("MatDesign · the design and identity of the Mat apps", home);
        Assert.Contains("class=\"mat-nav__tagline\">Identity of the Mat apps<", home);
        Assert.Contains($"class=\"mat-nav__version\">MatDesign {AppVersion.Current}<", home);
        Assert.Matches(@"<html lang=""en"" data-app=""matdesign""\s*>", home);
        Assert.Contains("Rules of the Mat family", await Html(_factory.CreateClient(), "/Rules"));
        Assert.Contains("Apps of the Mat family", await Html(_factory.CreateClient(), "/Identity"));
    }

    // ---- a blank start says the company

    [Fact]
    public async Task A_blank_site_names_itself_after_the_company_in_the_shell()
    {
        var client = Blank().CreateClient();
        var home = await Html(client, "/");

        Assert.Contains("<h1>Acme Design</h1>", home);
        Assert.Contains("How the Acme apps are meant to look.", home);
        Assert.Contains("Acme Design · the design and identity of the Acme apps", home);
        Assert.Contains("class=\"mat-nav__name\">Acme Design<", home);
        Assert.Contains("class=\"mat-nav__tagline\">Identity of the Acme apps<", home);
        Assert.Contains($"class=\"mat-nav__version\">Acme Design {AppVersion.Current}<", home);
        Assert.Contains("<title>Overview · Acme Design</title>", home);
        Assert.Matches(@"<html lang=""en"" data-app=""this-site""\s*>", home);
        Assert.Contains("<meta name=\"theme-color\" content=\"#2563eb\" />", home);
        Assert.Contains("Rules of the Acme family", await Html(client, "/Rules"));
    }

    [Fact]
    public async Task A_blank_site_lists_no_app_of_the_mat_family_and_offers_to_add_the_first()
    {
        var client = Blank().CreateClient();
        var home = await Html(client, "/");
        var identity = await Html(client, "/Identity");

        foreach (var app in AppRegistry.All)
        {
            Assert.DoesNotContain($"data-app=\"{app.Id}\"", home);
            Assert.DoesNotContain($"data-app=\"{app.Id}\"", identity);
            Assert.DoesNotContain($">{app.Name}<", home);
        }

        Assert.Contains("No apps yet", home);
        Assert.Contains("Acme Industries has no apps on this site", home);
        Assert.Contains("href=\"/Studio/Apps/Edit\"", home);
        Assert.Contains("0 apps</a>", home);
        Assert.Contains("No apps yet", identity);
        Assert.DoesNotContain("<table", identity);
    }

    [Fact]
    public async Task A_blank_menu_has_no_audit_and_the_audit_page_is_not_there()
    {
        var client = Blank().CreateClient();
        var home = await Html(client, "/");

        Assert.DoesNotContain("/Identity/Audit", home);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Identity/Audit")).StatusCode);
        Assert.DoesNotContain("The apps are the", home);
        Assert.Contains("href=\"/Identity/Palette\"", home);
        Assert.Contains("href=\"/Identity\"", home);
    }

    [Fact]
    public async Task A_blank_site_has_the_rules_and_the_components_of_the_toolkit()
    {
        var client = Blank().CreateClient();

        Assert.Contains("SHELL-01", await Html(client, "/Rules"));
        Assert.Contains("Buttons", await Html(client, "/Components"));
        var api = await Html(client, "/api/rules");
        Assert.Contains("\"SHELL-01\"", api);
        Assert.Equal("[]", (await Html(client, "/api/apps")).Trim());
    }

    [Fact]
    public async Task The_colour_of_a_blank_site_is_in_the_apps_part_of_the_bundle_and_the_demo_has_none_of_it()
    {
        var blank = await Html(Blank().CreateClient(), "/ui/matui.css");
        var demo = await Html(_factory.CreateClient(), "/ui/matui.css");

        Assert.Contains("[data-app=\"this-site\"] { --mat-app-accent: #2563eb; }", blank);
        Assert.DoesNotContain("this-site", demo);
        Assert.Contains("[data-app=\"matdesign\"]", blank);
        Assert.Contains("[data-app=\"matdesign\"]", demo);
        Assert.Equal(blank.Split('\n').Length - 3, demo.Split('\n').Length);
    }

    [Fact]
    public async Task The_archive_of_the_toolkit_names_the_company_that_made_it()
    {
        var blank = Blank();
        var bytes = await blank.CreateClient().GetByteArrayAsync("/ui/matui.zip");

        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("matui/README.txt")!.Open());
        var readme = await reader.ReadToEndAsync();

        Assert.Contains($"Made by Acme Design {AppVersion.Current}.", readme);
        Assert.Contains("The UI toolkit of the Acme apps. Rules, documentation and live examples: the Acme Design site.", readme);
        Assert.Contains("(docs/VERSIONING.md on the Acme Design site)", readme);
        Assert.DoesNotContain("MatDesign", readme);

        var demoBytes = await _factory.CreateClient().GetByteArrayAsync("/ui/matui.zip");
        using var demoZip = new System.IO.Compression.ZipArchive(new MemoryStream(demoBytes));
        using var demoReader = new StreamReader(demoZip.GetEntry("matui/README.txt")!.Open());
        var demoReadme = await demoReader.ReadToEndAsync();
        Assert.Contains($"Made by MatDesign {AppVersion.Current}.", demoReadme);
        Assert.Contains("The UI toolkit of the Mat apps. Rules, documentation and live examples: the MatDesign site.", demoReadme);
    }

    [Fact]
    public async Task The_download_page_of_a_blank_site_says_who_built_the_files()
    {
        var html = await Html(Blank().CreateClient(), "/Download");

        Assert.Contains("built by Acme Design", html);
        Assert.DoesNotContain("built by MatDesign", html);
    }

    // ---- every page, in both templates

    private static IEnumerable<string> VisitorPages()
    {
        yield return "/";
        yield return "/Rules";
        yield return "/Rules/Detail?id=SHELL-01";
        yield return "/Identity";
        yield return "/Identity?q=nothing-matches-this";
        yield return "/Identity/Palette";
        yield return "/Foundations";
        yield return "/Foundations/Type";
        yield return "/Foundations/Space";
        yield return "/Foundations/Motion";
        yield return "/Foundations/Icons";
        yield return "/Foundations/Tokens";
        yield return "/Foundations/Tokens?group=Schemes";
        yield return "/Shell";
        yield return "/Components";
        yield return "/Layout";
        yield return "/Utilities";
        yield return "/Patterns";
        yield return "/Patterns/Detail?id=list";
        yield return "/Patterns/Detail?id=sign-in";
        yield return "/Builder";
        yield return "/Builder?accent=%23ff6600";
        yield return "/Builder?scheme=paper";
        yield return "/Builder?accent=%23ff6600&scheme=oled";
        yield return "/Download";
        yield return "/Changelog";
        yield return "/Preview?view=list";
        yield return "/Preview?view=gallery";
        yield return "/Preview?view=form";
        yield return "/Preview?view=dashboard";
        yield return "/Preview?view=settings";
        yield return "/Studio/Login";
    }

    [Fact]
    public async Task Every_page_of_a_blank_site_answers_and_follows_the_shell_rules()
    {
        var client = Blank().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var problems = new List<string>();
        foreach (var url in VisitorPages())
        {
            var response = await client.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();
            if (url == "/Studio/Login")
            {
                // without a data directory the Studio is off and the page says so
                if (response.StatusCode != HttpStatusCode.OK || !html.Contains("The Studio is off")) { problems.Add($"{url}: {(int)response.StatusCode}"); }
                continue;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                problems.Add($"{url}: {(int)response.StatusCode}");
                continue;
            }

            if (Title().Match(html) is not { Success: true } title || title.Groups[1].Value.Trim().Length < 3) { problems.Add($"{url}: no title"); }
            if (Heading().Matches(html).Count != 1) { problems.Add($"{url}: {Heading().Matches(html).Count} h1"); }
            if (!html.Contains("class=\"mat-nav\"")) { problems.Add($"{url}: no menu"); }
            if (!html.Contains("class=\"mat-nav__brand\"")) { problems.Add($"{url}: no logo above the menu"); }
            if (!html.Contains("class=\"mat-crumbs\"")) { problems.Add($"{url}: no breadcrumb (SHELL-08)"); }
            if (!html.Contains("data-theme-toggle")) { problems.Add($"{url}: no theme button (SHELL-04)"); }
            if (!html.Contains("class=\"mat-user\"")) { problems.Add($"{url}: no user (SHELL-05)"); }
            if (!html.Contains("class=\"mat-nav__version\"")) { problems.Add($"{url}: no version (SHELL-06)"); }
            if (InlineStyle().IsMatch(html)) { problems.Add($"{url}: an inline style (CODE-02)"); }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task The_pages_that_speak_for_the_company_say_nothing_of_another_one()
    {
        var client = Blank().CreateClient();
        var problems = new List<string>();
        // the pages the visitor reads about the company; the documentation of the toolkit (rules, components) is the toolkit's and stays as it is
        foreach (var url in new[] { "/", "/Identity", "/Identity/Palette", "/Foundations", "/Shell", "/Patterns", "/Patterns/Detail?id=list", "/Builder", "/Download", "/Preview?view=list", "/Preview?view=auth" })
        {
            var html = await Html(client, url);
            var text = Regex.Replace(Sprite().Replace(html, string.Empty), @"<[^>]*>", " ");
            foreach (var needle in new[] { "MatDesign", "Mat apps", "Mat family", "Mat app ", "MatMail", "Mattiverse", "MatPaper", "Matdo", "data-app=\"mat" })
            {
                // an app of the family named in the text of a frame or a link is the visible sign of the demo
                if ((needle.StartsWith("data-app") ? html : text).Contains(needle, StringComparison.Ordinal)) { problems.Add($"{url}: says {needle}"); }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task The_frames_of_a_blank_site_open_the_site_app_and_not_an_app_that_does_not_exist()
    {
        var client = Blank().CreateClient();

        var shell = await Html(client, "/Shell");
        Assert.Contains("src=\"/Preview?app=this-site&amp;view=list\"", shell);
        Assert.Contains("here in the accent of Acme Design.", shell);

        var preview = await Html(client, "/Preview?app=matmail&view=list");
        Assert.Contains("data-app=\"this-site\"", preview);
        Assert.DoesNotContain("data-app=\"matmail\"", preview);

        var pattern = await Html(client, "/Patterns/Detail?id=list");
        Assert.Contains("The pattern in Acme Design", pattern);
        Assert.Contains("src=\"/Preview?app=this-site&amp;view=list&amp;theme=light\"", pattern);
    }

    [Fact]
    public async Task The_builder_and_the_colour_page_work_without_an_app_of_the_family()
    {
        var client = Blank().CreateClient();

        var builder = await Html(client, "/Builder?accent=%23ff6600");
        Assert.Contains("Acme Design", builder);

        var foundations = await Html(client, "/Foundations");
        Assert.Contains("Shown here for Acme Design's own accent, #2563eb.", foundations);
        Assert.Contains("href=\"/Builder?app=this-site&amp;scheme=", foundations);
    }

    [Fact]
    public async Task The_palette_of_a_blank_site_is_an_empty_state_and_its_menu_badge_counts_nothing()
    {
        var client = Blank().CreateClient();
        var palette = await Html(client, "/Identity/Palette");
        var home = await Html(client, "/");

        Assert.Contains("<h1>Palette</h1>", palette);
        // no pair of accents is too close, so the badge of the menu counts nothing
        var item = Regex.Match(home, @"<a class=""mat-nav__item[^""]*"" href=""/Identity/Palette"".*?</a>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(item);
        Assert.DoesNotContain("mat-nav__badge", item);
        Assert.Contains("data-app=\"this-site\"", palette);
    }
}
