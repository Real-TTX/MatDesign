using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

internal static class Mat
{
    /// <summary>The colour of MatDesign's own glyph (the accent of the app <c>matdesign</c>): on a blank site no file may draw it.</summary>
    public const string Pink = "#d6336c";
}

/// <summary>What the crawl of a site found: the pages it read, and every Mat word on them.</summary>
public sealed class SiteCrawl
{
    public List<string> Pages { get; } = [];

    public List<string> Files { get; } = [];

    public List<MatFinding> Findings { get; } = [];

    public List<string> Problems { get; } = [];
}

/// <summary>
/// A blank instance of the site (company Acme, one app of its own added in the Studio, a release made) that a crawl reads from end to end: every page a visitor and an administrator can reach by a link
/// (from the start page, the Studio and a few seeds for the pages that only have a form or an id), the JSON of the API, the files of the toolkit that are words (not the code), and the files of a release.
/// </summary>
public sealed partial class BlankSiteFixture : IAsyncLifetime
{
    private StudioHarness? _studio;
    private WebApplicationFactory<Program>? _plain;

    public SiteCrawl Crawl { get; } = new();

    public StudioHarness Studio => _studio ?? throw new InvalidOperationException("Not started.");

    /// <summary>The app of its own that the company added: pages that list apps have one.</summary>
    public const string AppId = "acmecrm";

    [GeneratedRegex(@"\.(?:css|js|zip|svg|png|ico|json|md|txt|woff2?)$")]
    private static partial Regex StaticFile();

    [GeneratedRegex(@"\bhref=""([^""#]*)""")]
    private static partial Regex Href();

    // addresses a crawl does not follow: files of the toolkit (read separately), the API (read separately), an end of the session, static files
    private static readonly string[] NotFollowed = ["/ui/", "/api/", "/releases/", "/js/", "/css/", "/img/", "/lib/", "/favicon", "/health", "/Studio/Logout", "/_framework", "/_content", "/Preview"];

    public async Task InitializeAsync()
    {
        _plain = new WebApplicationFactory<Program>();
        _studio = new StudioHarness(_plain, new Dictionary<string, string> { ["MATDESIGN_TEMPLATE"] = "blank", ["MATDESIGN_COMPANY"] = "Acme" });
        await _studio.SignIn();
        var form = StudioHarness.AppForm(AppId);
        form["Input.Name"] = "Acme CRM";
        form["Input.Tagline"] = "Customer records for Acme";
        var saved = await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", form);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var released = await _studio.Post("/Studio/Releases/New", "/Studio/Releases/New", new Dictionary<string, string> { ["Version"] = Ver.First, ["Note"] = "The first release." });
        Assert.Equal(HttpStatusCode.Redirect, released.StatusCode);

        await CrawlPages();
        await CrawlFiles();
        await CrawlSetup();
        await CrawlStates();
    }

    public Task DisposeAsync()
    {
        _studio?.Dispose();
        _plain?.Dispose();
        return Task.CompletedTask;
    }

    private static IEnumerable<string> Seeds()
    {
        yield return "/";
        yield return "/Studio";
        yield return $"/Identity/App?id={AppId}";
        yield return "/Identity?q=nothing-matches-this";
        yield return "/Rules/Detail?id=SHELL-01";
        yield return "/Rules/Detail?id=IDENT-01";
        yield return "/Rules/Detail?id=IDENT-09";
        yield return "/Rules/Changes";
        yield return "/Rules?q=accent";
        yield return "/Components/All";
        yield return "/Components/buttons";
        yield return "/Foundations/Tokens?group=Schemes";
        yield return "/Builder?accent=%23ff6600";
        yield return "/Builder?scheme=paper";
        yield return "/Search?q=button";
        yield return "/Search?q=rule";
        yield return "/Search?q=colour";
        yield return "/Search?q=family";
        yield return "/Studio/Apps/Edit";
        yield return $"/Studio/Apps/Edit?id={AppId}";
        yield return "/Studio/Rules/Edit";
        yield return "/Studio/Rules/Edit?id=SHELL-01";
        yield return "/Studio/Components/Edit";
        yield return "/Studio/Components/Edit?id=buttons";
        yield return "/Studio/Releases/New";
        yield return "/Studio/Releases/WhiteLabel";
        yield return "/Studio/Brand";
        yield return "/Studio/Brand/Components";
        yield return "/Studio/Brand/Assets";
        yield return "/Studio/Company";
        yield return "/Studio/Account";
        yield return "/Studio/Users";
        yield return "/Studio/Users/Edit";
        yield return "/Studio/Versions";
        yield return $"/Studio/Versions/Edit?id={AppId}";
        yield return "/Studio/Checks";
        yield return "/Studio/Activity";
        yield return "/Preview?view=list";
        yield return "/Preview?view=auth";
        yield return "/Preview?app=matmail&view=dashboard";
    }

    // the pages that exist once for every rule, component and pattern: every one is read
    private static readonly HashSet<string> OnePageEach = ["/Rules/Detail", "/Studio/Rules/Edit", "/Studio/Components/Edit", "/Patterns/Detail"];

    /// <summary>A page for every rule, component and pattern: the texts that are data are on them.</summary>
    private IEnumerable<string> DataSeeds()
    {
        foreach (var rule in _studio!.Design.Rules)
        {
            yield return $"/Rules/Detail?id={rule.Id}";
            yield return $"/Studio/Rules/Edit?id={rule.Id}";
        }

        foreach (var component in _studio.Design.Components)
        {
            yield return $"/Components/{component.Id}";
            yield return $"/Studio/Components/Edit?id={component.Id}";
        }

        foreach (var pattern in PatternCatalog.All)
        {
            yield return $"/Patterns/Detail?id={pattern.Id}";
        }
    }

    private async Task CrawlPages()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(Seeds().Concat(DataSeeds()));
        var perPath = new Dictionary<string, int>(StringComparer.Ordinal);
        while (queue.Count > 0 && seen.Count < 2500)
        {
            var url = queue.Dequeue();
            if (!seen.Add(url))
            {
                continue;
            }

            var path = url.Split('?')[0];
            perPath[path] = perPath.GetValueOrDefault(path) + 1;
            // a rule has a page each and every one is read; the pages that only vary a filter, a page number or a colour are read up to a number
            if (perPath[path] > (OnePageEach.Contains(path) ? 5000 : 40))
            {
                continue;
            }

            var response = await _studio!.Client.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is { } location && location.OriginalString.StartsWith('/'))
            {
                queue.Enqueue(location.OriginalString.Split('#')[0]);
                continue;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Crawl.Problems.Add($"{url}: {(int)response.StatusCode}");
                continue;
            }

            var html = await response.Content.ReadAsStringAsync();
            Crawl.Pages.Add(url);
            Crawl.Findings.AddRange(MatWords.In(url, SiteText.Pieces(html)));
            Crawl.Findings.AddRange(MatWords.InAddresses(url, html));
            foreach (Match link in Href().Matches(html))
            {
                var target = WebUtility.HtmlDecode(link.Groups[1].Value);
                if (target.StartsWith('/') && !target.StartsWith("//", StringComparison.Ordinal) && !NotFollowed.Any(p => target.StartsWith(p, StringComparison.Ordinal)) && !StaticFile().IsMatch(target.Split('?')[0]))
                {
                    queue.Enqueue(target);
                }
            }
        }
    }

    private async Task CrawlFiles()
    {
        var client = _studio!.Client;
        async Task<string> Text(string url)
        {
            var response = await client.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Crawl.Problems.Add($"{url}: {(int)response.StatusCode}");
                return string.Empty;
            }

            Crawl.Files.Add(url);
            return await response.Content.ReadAsStringAsync();
        }

        foreach (var url in new[]
        {
            "/api/apps", "/api/rules", "/api/rules/changes", "/api/tokens", "/api/properties", "/api/changelog", "/api/iconlib", "/api/schemes", "/api/releases",
            "/api/search?q=rule&limit=50", "/api/search?q=family&limit=50", "/api/search?q=accent&limit=50", "/api/search?q=app&limit=50", "/api/search?q=version&limit=50",
            "/ui/manifest.json",
        })
        {
            var json = await Text(url);
            if (json.Length > 0)
            {
                Crawl.Findings.AddRange(MatWords.In(url, SiteText.JsonStrings(json)));
            }
        }

        foreach (var url in new[] { "/ui/CHANGELOG.md", "/ui/iconlib/README.txt", "/ui/iconlib/LICENSE.txt" })
        {
            Crawl.Findings.AddRange(MatWords.In(url, SiteText.Lines(await Text(url))));
        }

        // the favicon of the site is a file of its own: what it draws and what it says
        var favicon = await Text("/favicon.svg");
        Crawl.Findings.AddRange(MatWords.In("/favicon.svg", SiteText.Lines(Regex.Replace(favicon, @"\s(aria-label|title)=""([^""]*)""|<[^>]*>", m => "\n" + m.Groups[2].Value + "\n"))));
        if (favicon.Contains(Mat.Pink, StringComparison.OrdinalIgnoreCase))
        {
            Crawl.Findings.Add(new MatFinding("/favicon.svg", "file:favicon", "@fill", "the colour of the Mat design glyph, " + Mat.Pink));
        }

        // the archive of the toolkit: its README and manifest are words
        var bytes = await client.GetByteArrayAsync("/ui/matui.zip");
        using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes)))
        {
            foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".txt", StringComparison.Ordinal) || e.FullName.EndsWith(".md", StringComparison.Ordinal) || e.FullName.EndsWith(".json", StringComparison.Ordinal)))
            {
                using var reader = new StreamReader(entry.Open());
                var content = await reader.ReadToEndAsync();
                var name = "/ui/matui.zip!" + entry.FullName;
                Crawl.Files.Add(name);
                Crawl.Findings.AddRange(MatWords.In(name, entry.FullName.EndsWith(".json", StringComparison.Ordinal) ? SiteText.JsonStrings(content) : SiteText.Lines(content)));
            }
        }

        // the files of the release the Studio made: the text of them (the styleguide, README, changelog, design.json)
        var latest = await Text("/api/releases/latest");
        if (latest.Length > 0)
        {
            using var manifest = JsonDocument.Parse(latest);
            var version = manifest.RootElement.GetProperty("version").GetString()!;
            Crawl.Findings.AddRange(MatWords.In("/api/releases/latest", SiteText.JsonStrings(latest)));
            foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()!))
            {
                var extension = Path.GetExtension(file);
                if (extension is not (".html" or ".json" or ".md" or ".txt"))
                {
                    continue;
                }

                var url = $"/releases/{version}/{file}";
                var content = await Text(url);
                var pieces = extension switch
                {
                    ".html" => SiteText.Pieces(content),
                    ".json" => SiteText.JsonStrings(content),
                    _ => SiteText.Lines(content),
                };
                Crawl.Findings.AddRange(MatWords.In(url, pieces));
                if (extension == ".html")
                {
                    Crawl.Findings.AddRange(MatWords.InAddresses(url, content));
                }
            }
        }
    }

    /// <summary>The setup assistant of a blank start: its own instance (the page is only there while nobody has been set up).</summary>
    private async Task CrawlSetup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "matdesign-neutral-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = _plain!.WithWebHostBuilder(b =>
            {
                b.UseSetting("MATDESIGN_DATA", directory);
                b.UseSetting("MATDESIGN_TEMPLATE", "blank");
                b.UseSetting("MATDESIGN_COMPANY", "Acme");
            });
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await Read(_studio!.NewClient(), "(signed out) /Studio/Login", "/Studio/Login");
            foreach (var url in new[] { "/Studio/Setup", "/Studio/Login", "/" })
            {
                await Read(client, "(setup) " + url, url);
            }

            // what the assistant says to a form that is not right
            var page = await client.GetStringAsync("/Studio/Setup");
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var wrong = await client.PostAsync("/Studio/Setup", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Company"] = string.Empty,
                ["ShortName"] = string.Empty,
                ["Template"] = "blank",
                ["Name"] = string.Empty,
                ["Password"] = "short",
                ["Repeat"] = "other",
                ["__RequestVerificationToken"] = token,
            }));
            Scan("(setup, wrong input) /Studio/Setup", await wrong.Content.ReadAsStringAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>The pages a visitor meets when something is missing, refused or wrong: not found, forbidden, read-only, a wrong password.</summary>
    private async Task CrawlStates()
    {
        foreach (var url in new[] { "/nothing-is-here", "/Rules/Detail?id=NOPE-99", "/Components/nope", "/Patterns/Detail?id=nope", "/Identity/App?id=nope", "/Studio/Apps/Edit?id=nope", "/Studio/Rules/Edit?id=NOPE-99", "/Studio/Releases/Run?id=r999999" })
        {
            var response = await _studio!.Client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url}: {(int)response.StatusCode}");
            Scan("(not found) " + url, await response.Content.ReadAsStringAsync());
        }

        // a viewer reads everything and changes nothing: the read-only notices, and the page that says no
        _studio!.AddUser("Vera", "viewer");
        var viewer = await _studio.ClientOf("Vera");
        foreach (var url in new[] { "/Studio", "/Studio/Apps", "/Studio/Apps/Edit", "/Studio/Rules/Edit?id=SHELL-01", "/Studio/Components/Edit?id=buttons", "/Studio/Brand", "/Studio/Brand/Assets", "/Studio/Company", "/Studio/Releases/New", "/Studio/Releases/WhiteLabel", "/Studio/Versions", "/Studio/Account", "/Studio/Users", "/Studio/Users/Edit" })
        {
            var response = await viewer.GetAsync(url);
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden, $"{url} as a viewer: {(int)response.StatusCode}");
            Scan("(viewer) " + url, await response.Content.ReadAsStringAsync());
        }

        // a wrong password
        var wrong = await _studio.SignInAs(_studio.NewClient(), "admin", "not the password");
        Scan("(wrong password) /Studio/Login", await wrong.Content.ReadAsStringAsync());
    }

    private void Scan(string name, string html)
    {
        Crawl.Pages.Add(name);
        Crawl.Findings.AddRange(MatWords.In(name, SiteText.Pieces(html)));
        Crawl.Findings.AddRange(MatWords.InAddresses(name, html));
    }

    private async Task Read(HttpClient client, string name, string url)
    {
        var response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is { } location)
        {
            response = await client.GetAsync(location.OriginalString);
        }

        var html = await response.Content.ReadAsStringAsync();
        Crawl.Pages.Add(name);
        Crawl.Findings.AddRange(MatWords.In(name, SiteText.Pieces(html)));
        Crawl.Findings.AddRange(MatWords.InAddresses(name, html));
    }
}

/// <summary>
/// A blank site must really be neutral (docs/PRODUCT.md, <c>IDENT-09</c>): a visitor of an instance whose template is <c>blank</c> must not read Mat. The crawl above reads every page and file of such an instance and
/// counts every Mat word; what is left after the allowlist is a finding. The allowlist holds only what the owner accepted, each entry with its reason.
/// </summary>
public sealed class BlankNeutralityTests : IClassFixture<BlankSiteFixture>
{
    private readonly BlankSiteFixture _site;

    public BlankNeutralityTests(BlankSiteFixture site) => _site = site;

    /// <summary>One thing that may stay on a blank site, and why.</summary>
    private sealed record Allowed(string Kind, string Reason, string? Page = null, string? Contains = null)
    {
        public bool Covers(MatFinding finding)
            => (finding.Kind == Kind || (Kind.EndsWith(':') && finding.Kind.StartsWith(Kind, StringComparison.Ordinal)))
               && (Page is null || Regex.IsMatch(finding.Page, Page))
               && (Contains is null || finding.Excerpt.Contains(Contains, StringComparison.Ordinal));
    }

    private static readonly Allowed[] Allowlist =
    [
        new(MatWords.Toolkit, "The toolkit's own product name: MatUi, the `mat-` classes and the `MatUi.` API are its identifiers until a white label build renames them (CODE-09)."),

        // OWNER DECISION: the notice of the icon library says who drew and owns the 51 icons that are not Lucide's. A licence names its author, and a word map that rewrote it into the company's name would be a false statement of
        // ownership, so it is left as the file is (it is a file of src/MatUi, which this change does not touch). To make it neutral, change the wording of the notice at its source (tools/iconlib/, "the authors of the toolkit").
        new("MatDesign", "The legal notice of the icon library (LICENSE.txt) names the author and owner of the icons that were drawn for the toolkit; it is a licence and keeps the name of its author.", Page: @"/iconlib/LICENSE\.txt$"),
    ];

    private IEnumerable<MatFinding> Unallowed() => _site.Crawl.Findings.Where(f => !Allowlist.Any(a => a.Covers(f)));

    [Fact]
    public void The_crawl_reads_every_kind_of_page_of_a_blank_site()
    {
        var pages = _site.Crawl.Pages;

        Assert.True(pages.Count > 150, $"only {pages.Count} pages were read");
        foreach (var expected in new[]
        {
            "/", "/Rules", "/Rules/Detail?id=SHELL-01", "/Rules/Changes", "/Identity", $"/Identity/App?id={BlankSiteFixture.AppId}", "/Identity/Palette", "/Foundations", "/Foundations/Type", "/Shell",
            "/Components", "/Components/buttons", "/Components/All", "/Layout", "/Utilities", "/Patterns", "/Builder", "/Download", "/Changelog", "/Search?q=button",
            "/Studio", "/Studio/Apps", "/Studio/Rules", "/Studio/Components", "/Studio/Brand", "/Studio/Company", "/Studio/Releases", "/Studio/Releases/WhiteLabel", "/Studio/Versions", "/Studio/Checks", "/Studio/Users", "/Studio/Account", "(signed out) /Studio/Login", "(setup) /Studio/Setup",
            "(setup, wrong input) /Studio/Setup", "(wrong password) /Studio/Login", "(viewer) /Studio/Users", "(viewer) /Studio/Apps", "(not found) /Components/nope", "(not found) /nothing-is-here",
        })
        {
            Assert.Contains(expected, pages);
        }

        // every rule, component and pattern has been read on its own page, in the site and in the Studio's editor
        var design = _site.Studio.Design;
        var unread = design.Rules.Select(r => $"/Rules/Detail?id={r.Id}").Concat(design.Rules.Select(r => $"/Studio/Rules/Edit?id={r.Id}"))
            .Concat(design.Components.Select(ComponentCatalog.PageOf).Where(url => url.StartsWith("/Components/", StringComparison.Ordinal))).Concat(design.Components.Select(c => $"/Studio/Components/Edit?id={c.Id}"))
            .Concat(PatternCatalog.All.Select(p => $"/Patterns/Detail?id={p.Id}"))
            .Where(url => !pages.Contains(url)).ToList();
        Assert.True(unread.Count == 0, "not read: " + string.Join(", ", unread.Take(10)));

        Assert.DoesNotContain("/Identity/Audit", pages);
        Assert.Empty(_site.Crawl.Problems);
    }

    [Fact]
    public void The_crawl_reads_the_files_that_are_words()
    {
        foreach (var expected in new[] { "/api/rules", "/api/search?q=rule&limit=50", "/ui/CHANGELOG.md", "/favicon.svg" })
        {
            Assert.Contains(expected, _site.Crawl.Files);
        }

        Assert.Contains(_site.Crawl.Files, f => f.StartsWith("/ui/matui.zip!matui/README", StringComparison.Ordinal));
        Assert.Contains(_site.Crawl.Files, f => f.Contains("/styleguide", StringComparison.Ordinal) && f.EndsWith(".html", StringComparison.Ordinal));
        Assert.Contains(_site.Crawl.Files, f => f.EndsWith("/design.json", StringComparison.Ordinal));
    }

    [Fact]
    public void A_blank_site_says_nothing_of_Mat()
    {
        var left = Unallowed().ToList();

        // MATDESIGN_NEUTRALITY_REPORT=<file> writes the whole measurement (what the report of a change quotes)
        var report = Environment.GetEnvironmentVariable("MATDESIGN_NEUTRALITY_REPORT");
        if (!string.IsNullOrEmpty(report))
        {
            File.WriteAllText(report, $"read: {_site.Crawl.Pages.Count} pages and {_site.Crawl.Files.Count} files\nALL FINDINGS (before the allowlist)\n" + Summary(_site.Crawl, _site.Crawl.Findings) + "\n----- left after the allowlist -----\n" + Summary(_site.Crawl, left) + "\n" + MatWords.Describe(left)
                + "\n----- every distinct excerpt that is left -----\n"
                + string.Join("\n", left.GroupBy(f => (Kind: KindOf(f), f.Excerpt)).OrderBy(g => g.Key.Kind, StringComparer.Ordinal).ThenBy(g => g.Key.Excerpt, StringComparer.Ordinal)
                    .Select(g => $"{g.Count(),4}x {g.Key.Kind} [{g.First().Where}] {g.Key.Excerpt}   <{g.First().Page}>")));
        }

        var shown = string.Concat(MatWords.Describe(left).Split('\n').Take(40).Select(l => l + "\n"));
        Assert.True(left.Count == 0, $"{left.Count} Mat words on {left.Select(f => f.Page).Distinct().Count()} of {_site.Crawl.Pages.Count + _site.Crawl.Files.Count} pages and files of a blank site:\n{Summary(_site.Crawl, left)}\n{shown}");
    }

    /// <summary>The page group of a page: the pages made of data (a rule, a component) are one group each.</summary>
    internal static string GroupOf(string page)
    {
        var path = page.Split('?')[0];
        return path.StartsWith("/Components/", StringComparison.Ordinal) && path != "/Components/All" ? "/Components/{id}" : path;
    }

    private static string KindOf(MatFinding f)
        => f.Kind.StartsWith("app:", StringComparison.Ordinal) ? "app:*"
            : f.Kind.StartsWith("other:", StringComparison.Ordinal) ? "other:*"
            : f.Kind.StartsWith("app-address:", StringComparison.Ordinal) ? "app-address:*"
            : f.Kind;

    /// <summary>The totals by kind, the names found, and by group of pages: how many pages there are and what is found on them.</summary>
    internal static string Summary(SiteCrawl crawl, IReadOnlyCollection<MatFinding> findings)
    {
        var kinds = string.Join("\n", findings.GroupBy(KindOf).OrderByDescending(g => g.Count()).Select(g => $"  {g.Key,-18} {g.Count(),6}   on {g.Select(f => f.Page).Distinct().Count()} pages"));
        var names = string.Join(", ", findings.Where(f => f.Kind.StartsWith("app:", StringComparison.Ordinal) || f.Kind.StartsWith("other:", StringComparison.Ordinal)).GroupBy(f => f.Kind[(f.Kind.IndexOf(':') + 1)..]).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} x{g.Count()}"));
        var visited = crawl.Pages.Concat(crawl.Files).GroupBy(GroupOf).ToDictionary(g => g.Key, g => g.Count());
        var groups = string.Join("\n", findings.GroupBy(f => GroupOf(f.Page)).OrderByDescending(g => g.Count()).Select(g =>
            $"  {g.Key,-40} {g.Select(f => f.Page).Distinct().Count(),4}/{visited.GetValueOrDefault(g.Key),-4} pages  {string.Join("  ", g.GroupBy(KindOf).OrderByDescending(k => k.Count()).Select(k => $"{k.Key} x{k.Count()}"))}"));
        return $"by kind:\n{kinds}\nnames: {names}\nby group (pages with findings/pages read):\n{groups}\n";
    }
}
