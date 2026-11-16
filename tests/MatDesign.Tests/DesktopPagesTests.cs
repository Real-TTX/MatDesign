using System.Buffers.Binary;
using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// Desktop and services (docs/DESKTOP.md): the four areas of the rulebook, the five pages, the pictures as data with their files, and the neutrality of the pages on a blank site, where the
/// pictures of the demo's programs are left out (IDENT-09).
/// </summary>
public sealed partial class DesktopPagesTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly string[] Areas = ["DESK", "TRAY", "SETUP", "AGENT"];

    private static readonly (string Url, string Page, string Area)[] PageList =
    [
        ("/Desktop", "overview", ""),
        ("/Desktop/App", "app", "DESK"),
        ("/Desktop/Tray", "tray", "TRAY"),
        ("/Desktop/Setup", "setup", "SETUP"),
        ("/Desktop/Agents", "agents", "AGENT"),
    ];

    private readonly List<WebApplicationFactory<Program>> _sites = [];

    [GeneratedRegex(@"<img\b[^>]*\bsrc=""(/img/desktop/[^""]+)""")]
    private static partial Regex DesktopImage();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    private static string Folder => TestPaths.Web("wwwroot/img/desktop");

    public void Dispose()
    {
        foreach (var site in _sites)
        {
            site.Dispose();
        }
    }

    private static async Task<string> Html(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode}");
        return await response.Content.ReadAsStringAsync();
    }

    private HttpClient Blank()
    {
        var site = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_TEMPLATE", "blank");
            builder.UseSetting("MATDESIGN_COMPANY", "Acme Industries");
        });
        _sites.Add(site);
        return site.CreateClient();
    }

    // ---- the rules

    [Fact]
    public void The_four_areas_stand_after_the_areas_of_the_web_and_their_rules_are_numbered_from_one()
    {
        Assert.Equal(Areas, RuleBook.Areas.TakeLast(4).Select(a => a.Id));
        foreach (var area in Areas)
        {
            var ids = RuleBook.In(area).Select(r => r.Id).ToList();
            Assert.Equal(Enumerable.Range(1, ids.Count).Select(n => $"{area}-{n:00}"), ids);
        }
    }

    [Fact]
    public void The_rules_of_desktop_programs_are_the_owners_checked_by_a_reviewer_and_name_no_app_of_the_family()
    {
        var blank = TenantWords.For(Tenant.For("Acme Industries", DesignTemplates.BlankId), "Acme CRM");
        foreach (var rule in Areas.SelectMany(RuleBook.In))
        {
            // the owner decided them (2026-11-15); a May is never the owner's, so the one optional rule is the design's
            Assert.True(rule.Source == RuleBook.Owner || (rule.Level == RuleLevel.May && rule.Source == RuleBook.Design), rule.Id);
            // this site cannot run a desktop program: a reviewer checks them
            Assert.StartsWith("Review", rule.Check, StringComparison.Ordinal);
            Assert.NotNull(rule.Why);
            // a blank site reads them through the word map: no word of the family in them (DESK-02 and DESK-09 name the library for desktop programs, which the map says in words)
            var read = blank.OfRule(rule);
            foreach (var text in new[] { read.Title, read.Statement, read.Why, read.Do, read.Dont, read.Check })
            {
                Assert.DoesNotContain(MatWords.Find(text ?? string.Empty), f => f.Kind != MatWords.Toolkit);
            }

            // added on the day the owner decided them; a later line only changes them
            var lines = RuleChanges.For(rule.Id);
            Assert.Equal(RuleChangeKind.Added, lines[0].Kind);
            Assert.Equal("2026-11-15", lines[0].Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            Assert.All(lines.Skip(1), l => Assert.Equal(RuleChangeKind.Changed, l.Kind));
        }
    }

    [Fact]
    public void The_owners_decisions_are_in_the_rules()
    {
        Assert.Contains("There is no Uninstall in a tray", RuleBook.Get("TRAY-01")!.Statement);
        Assert.Contains("`Quit` as the last item, always", RuleBook.Get("TRAY-01")!.Statement);
        Assert.Contains("Only Quit", RuleBook.Get("DESK-06")!.Statement);
        Assert.Contains("\"Also delete my data\"", RuleBook.Get("SETUP-04")!.Statement);
        Assert.Contains("`Version <version> · <channel>`", RuleBook.Get("SETUP-07")!.Statement);
        Assert.Contains("never travels over the local status channel", RuleBook.Get("AGENT-03")!.Statement);
        Assert.Contains("Avalonia", RuleBook.Get("DESK-01")!.Statement);
        Assert.Equal(RuleLevel.Should, RuleBook.Get("AGENT-05")!.Level);
    }

    // ---- the pictures

    [Fact]
    public void Every_picture_has_its_files_with_the_size_it_says_and_every_file_is_a_picture_of_the_list()
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var figure in DesktopFigures.All)
        {
            Assert.Contains(figure.Page, DesktopFigures.Pages);
            Assert.All(figure.Rules, id => Assert.True(RuleBook.Get(id) is not null, $"{figure.File}: {id}"));
            Assert.True(figure.Alt.Length > 20 && figure.Caption.Length > 10, figure.File);
            foreach (var file in figure.Files)
            {
                Assert.True(named.Add(file), $"{file} is named twice");
                var path = Path.Combine(Folder, file);
                Assert.True(File.Exists(path), $"{file} is not in wwwroot/img/desktop");
                var bytes = File.ReadAllBytes(path);
                Assert.True(bytes.Length > 24 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G', $"{file} is not a PNG");
                Assert.Equal((figure.Width, figure.Height), (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20))));
                Assert.True(bytes.Length < 200_000, $"{file} has {bytes.Length} bytes");
            }
        }

        var files = Directory.GetFiles(Folder).Select(Path.GetFileName).ToList();
        Assert.All(files, f => Assert.True(named.Contains(f!), $"{f} is in wwwroot/img/desktop but no picture of DesktopFigures"));
    }

    // ---- the pages

    [Fact]
    public void The_menu_has_the_group_with_its_five_pages()
    {
        var group = Navigation.Sets.SelectMany(s => s.Groups).Single(g => g.Title == "Desktop and services");
        Assert.Equal(PageList.Select(p => p.Url), group.Items.Select(i => i.Href));
    }

    [Fact]
    public async Task On_the_demo_every_page_shows_its_rules_and_pictures_that_exist()
    {
        var client = factory.CreateClient();
        foreach (var (url, page, area) in PageList)
        {
            var html = await Html(client, url);
            Assert.Single(Heading().Matches(html));
            if (area.Length > 0)
            {
                Assert.All(RuleBook.In(area), r => Assert.Contains($"href=\"/Rules/Detail?id={r.Id}\"", html));
            }

            var shown = DesktopImage().Matches(html).Select(m => m.Groups[1].Value).ToHashSet();
            var expected = DesktopFigures.On(page).SelectMany(f => f.Files).Select(f => DesktopFigures.Folder + f).ToHashSet();
            Assert.Equal(expected, shown);
            foreach (var src in shown)
            {
                var response = await client.GetAsync(src);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
            }
        }
    }

    [Fact]
    public async Task A_pair_of_pictures_is_shown_by_the_theme_of_the_site()
    {
        var html = await Html(factory.CreateClient(), "/Desktop/App");
        var pair = DesktopFigures.On("app").First(f => f.Dark is not null);
        Assert.Contains($"class=\"doc-shot__img mat-brandmark--light\" src=\"{pair.Src}\"", html);
        Assert.Contains($"class=\"doc-shot__img mat-brandmark--dark\" src=\"{pair.DarkSrc}\"", html);
        Assert.Contains("loading=\"lazy\"", html);
    }

    [Fact]
    public async Task On_a_blank_site_the_pages_draw_the_specimens_with_the_companys_names_and_show_no_picture_of_the_demo()
    {
        var client = Blank();
        foreach (var (url, _, _) in PageList)
        {
            var html = await Html(client, url);
            Assert.DoesNotContain("/img/desktop/", html);
            Assert.DoesNotContain("ThixClip", html);
            var findings = MatWords.In(url, SiteText.Pieces(html)).Where(f => f.Kind != MatWords.Toolkit).ToList();
            Assert.True(findings.Count == 0, string.Join("\n", findings.Select(f => $"{f.Kind}: {f.Excerpt}")));
        }

        var agents = await Html(client, "/Desktop/Agents");
        Assert.Contains("Acme Agent", agents);
        Assert.Contains("acme-agent.exe", agents);
    }
}
