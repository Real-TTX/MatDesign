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
        foreach (var url in new[] { "/Identity", "/Identity/Palette", "/Identity/Audit", "/Rules", "/Foundations/Icons", "/Foundations/Tokens", "/Patterns" })
        {
            var (_, html) = await Get(url);
            var toolbar = html.IndexOf("class=\"mat-toolbar\"", StringComparison.Ordinal);
            Assert.True(toolbar > 0, $"{url}: no toolbar (LIST-02)");
            Assert.True(toolbar < html.IndexOf("<table class=\"mat-table", StringComparison.Ordinal) || !html.Contains("<table class=\"mat-table"), $"{url}: the table is above the toolbar");
            // The rulebook is searched all the time: its search is a field in the toolbar, Filter and Sort stand at the right end (LIST-03).
            var searchField = url == "/Rules";
            foreach (var dialog in new[] { "-search\"", "-filter\"", "-sort\"" }.Where(d => !searchField || d != "-search\""))
            {
                Assert.True(Regex.IsMatch(html, "<dialog class=\"mat-dialog[^\"]*\" id=\"[a-z]+" + dialog), $"{url}: no {dialog.Trim('-', '"')} dialog (LIST-03)");
            }

            Assert.Equal(searchField, html.Contains("class=\"mat-input-icon mat-toolbar__search\"", StringComparison.Ordinal));
            Assert.Equal(searchField, html.Contains("class=\"mat-toolbar__end\"", StringComparison.Ordinal));
            Assert.Equal(!searchField, Regex.IsMatch(html, "<dialog class=\"mat-dialog[^\"]*\" id=\"[a-z]+-search\""));
        }
    }

    [Fact]
    public async Task Every_pattern_rule_and_app_page_answers()
    {
        var urls = PatternCatalog.All.Select(p => $"/Patterns/Detail?id={p.Id}")
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

    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"\shref=""#([^""]+)""")]
    private static partial Regex FragmentLink();

    [Theory]
    [InlineData("components")]
    [InlineData("layout")]
    [InlineData("utilities")]
    public async Task A_documentation_page_has_every_component_under_its_group_and_in_the_index(string sectionId)
    {
        // SHELL-16: one long page, an index beside it (and in a dialog for small screens), both marked by the scroll spy.
        var section = ComponentCatalog.Sections.Single(s => s.Id == sectionId);
        var (status, html) = await Get(section.Path);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Single(Heading().Matches(html));
        Assert.Contains("class=\"mat-toc-aside\"", html);
        Assert.Equal(2, Regex.Matches(html, "<nav class=\"mat-toc\"[^>]*data-scrollspy").Count);
        Assert.Contains("data-dialog-open=\"toc-dialog\"", html);
        Assert.Contains("id=\"toc-dialog\"", html);

        foreach (var group in section.Groups)
        {
            var anchor = ComponentCatalog.GroupAnchor(group);
            Assert.Contains($"<section class=\"doc-group mat-anchor\" id=\"{anchor}\"", html);
            Assert.True(Regex.Matches(html, $"href=\"#{anchor}\"").Count >= 2, $"{group} is in the index beside the content and in the dialog");
        }

        foreach (var component in ComponentCatalog.In(section))
        {
            Assert.Contains($"<section class=\"doc-component mat-anchor\" id=\"{component.Id}\"", html);
            Assert.True(Regex.Matches(html, $"mat-toc__link( is-active)?\" href=\"#{component.Id}\"").Count >= 2, $"{component.Id} is in the index beside the content and in the dialog");
        }

        // a component is on the page of its own section only
        foreach (var other in ComponentCatalog.All.Except(ComponentCatalog.In(section)))
        {
            Assert.DoesNotContain($"<section class=\"doc-component mat-anchor\" id=\"{other.Id}\"", html);
        }
    }

    [Theory]
    [InlineData("/Components")]
    [InlineData("/Layout")]
    [InlineData("/Utilities")]
    public async Task The_ids_of_a_documentation_page_are_unique_and_every_fragment_link_leads_somewhere(string url)
    {
        // All the examples share one page: two controls with the same id, or a link to a section that is not there, would break it.
        var (_, html) = await Get(url);
        var ids = IdAttribute().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        var twice = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(twice.Count == 0, "ids that appear twice on the page: " + string.Join(", ", twice));

        var known = ids.ToHashSet();
        var lost = FragmentLink().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Where(f => !known.Contains(f)).Distinct().ToList();
        Assert.True(lost.Count == 0, "links to sections that are not on the page: " + string.Join(", ", lost));
    }

    [Theory]
    [InlineData("/Foundations/Type", "design", "toolkit")]
    [InlineData("/Identity", "design", "toolkit")]
    [InlineData("/Shell", "toolkit", "design")]
    [InlineData("/Components", "toolkit", "design")]
    public async Task The_menu_has_a_switcher_and_shows_only_the_menu_of_the_current_page(string url, string shown, string hidden)
    {
        var (_, html) = await Get(url);
        Assert.Contains("class=\"mat-switcher\"", html);
        Assert.Contains("id=\"nav-switch-menu\"", html);
        Assert.Contains($"data-nav-set=\"{shown}\"", html);
        Assert.DoesNotContain(" hidden", Regex.Match(html, $"<nav[^>]*data-nav-panel=\"{shown}\"[^>]*>").Value);
        Assert.Contains(" hidden", Regex.Match(html, $"<nav[^>]*data-nav-panel=\"{hidden}\"[^>]*>").Value);
        Assert.Contains($"aria-checked=\"true\" data-nav-set=\"{shown}\"", html);
    }

    [Fact]
    public async Task The_branch_with_the_current_page_is_open_and_the_child_is_marked()
    {
        var (_, html) = await Get("/Foundations/Type");
        var branch = Regex.Match(html, "<li class=\"mat-nav__branch([^\"]*)\">");
        Assert.True(branch.Success, "no submenu in the menu");
        Assert.Contains("is-open", branch.Groups[1].Value);
        Assert.Contains("is-current", branch.Groups[1].Value);
        Assert.Contains("aria-expanded=\"true\" aria-controls=\"nav-sub-design-foundations\" data-nav-branch=\"foundations\"", html);
        Assert.Matches(@"<a class=""mat-nav__item mat-nav__item--sub is-active"" href=""/Foundations/Type"" aria-current=""page"">", html);

        // on a page of another section the branch is closed
        var (_, other) = await Get("/Identity");
        Assert.Contains("aria-expanded=\"false\" aria-controls=\"nav-sub-design-foundations\"", other);
        Assert.DoesNotContain("is-open", Regex.Match(other, "<li class=\"mat-nav__branch([^\"]*)\">").Groups[1].Value);
    }

    [Fact]
    public async Task Every_submenu_is_controlled_by_a_button_that_names_it_and_badges_are_readable()
    {
        foreach (var url in new[] { "/", "/Shell", "/Preview?app=matmail&view=list", "/Preview?app=matmail&view=list&area=backups" })
        {
            var (_, html) = await Get(url);
            var ids = IdAttribute().Matches(html).Select(m => m.Groups[1].Value).ToHashSet();
            foreach (Match button in Regex.Matches(html, "data-nav-branch=\"[^\"]+\""))
            {
                Assert.True(button.Success);
            }

            foreach (Match controls in Regex.Matches(html, "aria-controls=\"(nav-sub-[^\"]+)\""))
            {
                Assert.True(ids.Contains(controls.Groups[1].Value), $"{url}: {controls.Groups[1].Value} is not in the page");
            }

            // a badge that is a count or a dot says in words what it counts
            foreach (Match badge in Regex.Matches(html, "<span class=\"mat-nav__badge[^\"]*\">(.*?)</span></span>|<span class=\"mat-nav__badge[^\"]*\">[^<]*<span class=\"mat-sr-only\">", RegexOptions.Singleline))
            {
                Assert.Contains("mat-sr-only", badge.Value);
            }
        }
    }

    [Fact]
    public async Task The_sample_app_has_an_administration_behind_the_switcher_with_badges_and_a_submenu()
    {
        var (_, work) = await Get("/Preview?app=matmail&view=list");
        Assert.Contains("<span class=\"mat-switcher__name\">Work</span>", work);
        Assert.Contains("mat-nav__badge mat-nav__badge--warn", work);

        var (_, admin) = await Get("/Preview?app=matmail&view=list&area=users");
        Assert.Contains("<span class=\"mat-switcher__name\">Administration</span>", admin);
        Assert.DoesNotContain(" hidden", Regex.Match(admin, "<nav[^>]*data-nav-panel=\"admin\"[^>]*>").Value);
        Assert.Contains("<li>Users</li>", admin.Replace("<li aria-current=\"page\">Users</li>", "<li>Users</li>"));

        var (_, backups) = await Get("/Preview?app=matmail&view=dashboard&area=backups");
        Assert.Matches(@"<li class=""mat-nav__branch is-open is-current"">\s*<button[^>]*data-nav-branch=""settings""", backups);
        Assert.Contains("mat-nav__badge mat-nav__badge--error", backups);
    }

    [Theory]
    [InlineData("/Rules/Detail?id=NOPE-99")]
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
        // "0.#": a size that is a whole number of kilobytes has no decimal at all, so both forms are fine; a comma never is.
        Assert.Matches(@"Download the archive \(\d+(\.\d)? KB\)", html);
        Assert.DoesNotMatch(@"\(\d+,\d+ KB\)", html);
    }
}
