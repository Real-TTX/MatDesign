using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The whole Studio, signed in, with something in every list: every page answers and follows the shell rules, and no link of the Studio leads nowhere.</summary>
public sealed partial class StudioPagesTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    public StudioPagesTests(WebApplicationFactory<Program> factory) => _studio = new StudioHarness(factory);

    public void Dispose() => _studio.Dispose();

    [GeneratedRegex(@"\b(?:href|src)=""(/[^""#]*)""")]
    private static partial Regex LocalLink();

    // code that is shown, not links that are drawn: the markup in a <pre> or in the box of the editor
    [GeneratedRegex(@"<(pre|textarea)\b.*?</\1>", RegexOptions.Singleline)]
    private static partial Regex ShownCode();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"<title>([^<]*)</title>")]
    private static partial Regex Title();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"<td\b([^>]*)>")]
    private static partial Regex TableCell();

    /// <summary>One thing in every list: an app, a rule, a component, a release, a version, and a run that stopped.</summary>
    private async Task Fill()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", StudioHarness.AppForm());
        var rule = new Rule("FORM-90", "FORM", "A rule for the tests", "Every test rule is deleted again when the test ends.", RuleLevel.Should, RuleBook.Studio);
        _studio.Store.Save("rules", rule.Id, rule, "admin", "Created");
        _studio.Store.Save("components", "qa-pages", new ComponentDoc("qa-pages", "QA pages", "Content", "toggle", "A component for the page tests.", ["BUTTON-01"],
            [new ComponentExample("Pill", """<span class="mat-pill">x</span>""")], []), "admin", "Created");
        _studio.Store.Save("brand", "brand", MatDesign.Core.Studio.Brand.BuiltIn with { Company = "Acme", Light = new Dictionary<string, string> { ["--mat-radius"] = "8px" } }, "admin", "Created");
        _studio.Design.Reload();
        Assert.True(_studio.Releases.Execute("0.1.0", "For the page tests.", "admin").Succeeded);
        _studio.Factory.Services.GetRequiredService<VersionTracker>().Set("matmail", "0.1.0", "A note.", "admin");
        // a second app and a second release, so that something is behind
        Assert.True(_studio.Releases.Execute("0.1.1", string.Empty, "admin").Succeeded);
        // the white label setting (stage 15), after the releases so that they stay what the other tests expect
        _studio.Store.Save("whitelabel", "whitelabel", new MatDesign.Core.Studio.WhiteLabelSetting("acme", "AcmeUi"), "admin", "Created");
        _studio.Design.Reload();
    }

    private IEnumerable<string> Pages()
    {
        yield return "/Studio";
        yield return "/Studio/Apps";
        yield return "/Studio/Apps/Edit";
        yield return "/Studio/Apps/Edit?id=matx";
        yield return "/Studio/Apps/Edit?id=matmail";
        yield return "/Studio/Rules";
        yield return "/Studio/Rules/Edit";
        yield return "/Studio/Rules/Edit?id=FORM-90";
        yield return "/Studio/Rules/Edit?id=SHELL-01";
        yield return "/Studio/Components";
        yield return "/Studio/Components/Edit";
        yield return "/Studio/Components/Edit?id=qa-pages";
        yield return "/Studio/Components/Edit?id=buttons";
        yield return "/Studio/Activity";
        yield return "/Studio/History?kind=apps&id=matx";
        yield return "/Studio/History?kind=rules&id=FORM-90";
        yield return "/Studio/History?kind=components&id=qa-pages";
        yield return "/Studio/Checks";
        yield return "/Studio/Releases";
        yield return "/Studio/Releases/New";
        yield return $"/Studio/Releases/Run?id={_studio.Releases.Runs(1)[0].Id}";
        yield return "/Studio/Versions";
        yield return "/Studio/Versions/Edit?id=matmail";
        yield return "/Studio/Versions/Edit?id=matdock";
        yield return "/Studio/Brand";
        yield return "/Studio/History?kind=brand&id=brand";
        yield return "/Studio/Releases/WhiteLabel";
        yield return "/Studio/History?kind=whitelabel&id=whitelabel";
    }

    [Fact]
    public async Task Every_page_of_the_studio_answers_and_follows_the_shell_rules()
    {
        await Fill();
        var problems = new List<string>();
        foreach (var url in Pages())
        {
            var response = await _studio.Client.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                problems.Add($"{url}: {(int)response.StatusCode}");
                continue;
            }

            if (Title().Match(html) is not { Success: true } title || title.Groups[1].Value.Trim().Length < 3) { problems.Add($"{url}: no title"); }
            if (Heading().Matches(html).Count != 1) { problems.Add($"{url}: {Heading().Matches(html).Count} h1"); }
            if (!html.Contains("class=\"mat-nav\"")) { problems.Add($"{url}: no menu"); }
            if (!html.Contains("data-nav-panel=\"studio\"")) { problems.Add($"{url}: no Studio menu"); }
            if (!html.Contains("class=\"mat-nav__brand\"")) { problems.Add($"{url}: no logo above the menu"); }
            if (!html.Contains("class=\"mat-crumbs\"")) { problems.Add($"{url}: no breadcrumb (SHELL-08)"); }
            if (!html.Contains("data-theme-toggle")) { problems.Add($"{url}: no theme button (SHELL-04)"); }
            if (!html.Contains("class=\"mat-user\"")) { problems.Add($"{url}: no user (SHELL-05)"); }
            if (!html.Contains("class=\"mat-nav__version\"")) { problems.Add($"{url}: no version (SHELL-06)"); }
            if (!html.Contains("<main class=\"mat-page")) { problems.Add($"{url}: no main landmark"); }
            if (InlineStyle().IsMatch(html)) { problems.Add($"{url}: an inline style (CODE-02)"); }

            // the same ids twice would break a label or a link; a cell of a table without a label is nothing on a phone (TABLE-02)
            var ids = IdAttribute().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
            foreach (var twice in ids.GroupBy(i => i).Where(g => g.Count() > 1))
            {
                problems.Add($"{url}: the id {twice.Key} is on the page twice");
            }

            foreach (Match cell in TableCell().Matches(html))
            {
                if (!cell.Groups[1].Value.Contains("data-label=") && !cell.Groups[1].Value.Contains("mat-table__cell-actions"))
                {
                    problems.Add($"{url}: a cell without data-label: {cell.Value}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task No_link_of_the_studio_leads_nowhere()
    {
        await Fill();

        // from the overview, every local address of the Studio and what it links to: the shapes of an address (a path and the names of its query) are followed with a few values each
        var queue = new Queue<string>(["/Studio", .. Pages()]);
        var seen = new HashSet<string>();
        var shapes = new Dictionary<string, int>();
        var broken = new List<string>();
        var fetched = 0;
        while (queue.Count > 0 && fetched < 600)
        {
            var url = WebUtility.HtmlDecode(queue.Dequeue());
            if (!seen.Add(url) || url.Contains("handler=", StringComparison.Ordinal) || url.StartsWith("/Studio/Logout", StringComparison.Ordinal) || url.StartsWith("/ui/", StringComparison.Ordinal) || url.StartsWith("/js/", StringComparison.Ordinal) || url.StartsWith("/css/", StringComparison.Ordinal) || url.Contains("favicon", StringComparison.Ordinal))
            {
                continue;
            }

            var query = url.Contains('?') ? string.Join("&", url[(url.IndexOf('?') + 1)..].Split('&').Select(p => p.Split('=')[0]).Order()) : string.Empty;
            var shape = url.Split('?')[0] + "?" + query;
            if (shapes.GetValueOrDefault(shape) >= 4)
            {
                continue;
            }

            shapes[shape] = shapes.GetValueOrDefault(shape) + 1;
            var response = await _studio.Client.GetAsync(url);
            fetched++;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"{url}: {(int)response.StatusCode}");
                continue;
            }

            if (response.Content.Headers.ContentType?.MediaType != "text/html")
            {
                continue;
            }

            foreach (Match link in LocalLink().Matches(ShownCode().Replace(await response.Content.ReadAsStringAsync(), string.Empty)))
            {
                queue.Enqueue(link.Groups[1].Value);
            }
        }

        Assert.True(fetched > 80, $"the crawl fetched only {fetched} addresses");
        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    [Fact]
    public async Task The_studio_is_a_start_page_in_the_switcher_and_its_menu_is_only_in_its_own_pages()
    {
        await _studio.SignIn();

        // on a page of the site the entry of the Studio is a link to its overview, the other two are buttons, and the Studio has no hidden panel
        var site = await _studio.Get("/Rules");
        Assert.Matches(@"<a class=""mat-menu__item"" href=""/Studio"" role=""menuitemradio"" aria-checked=""false"" data-nav-set=""studio""", site);
        Assert.Matches(@"<button type=""button"" class=""mat-menu__item"" role=""menuitemradio"" aria-checked=""false"" data-nav-set=""toolkit""", site);
        Assert.DoesNotContain("data-nav-panel=\"studio\"", site);
        Assert.Contains("data-nav-panel=\"toolkit\"", site);

        // on a page of the Studio the entry is checked and its menu is drawn, shown
        var studio = await _studio.Get("/Studio/Apps");
        Assert.Matches(@"<a class=""mat-menu__item"" href=""/Studio"" role=""menuitemradio"" aria-checked=""true"" data-nav-set=""studio""", studio);
        Assert.Matches(@"data-nav-panel=""studio""(?![^>]*hidden)", studio);
    }

    [Fact]
    public async Task The_menu_of_the_studio_has_every_page_and_marks_the_one_that_is_open()
    {
        await Fill();
        var menu = Navigation.StudioSet.Pages.Select(i => i.Href!).ToList();

        foreach (var href in menu)
        {
            var html = await _studio.Get(href);
            Assert.Contains($"<a class=\"mat-nav__item is-active\" href=\"{href}\" aria-current=\"page\">", html);
        }

        // a page that is not in the menu belongs to a page that is
        Assert.Contains("href=\"/Studio/Apps\" aria-current=\"page\"", await _studio.Get("/Studio/History?kind=apps&id=matx"));
        Assert.Contains("href=\"/Studio/Releases\" aria-current=\"page\"", await _studio.Get($"/Studio/Releases/Run?id={_studio.Releases.Runs(1)[0].Id}"));
        Assert.Contains("href=\"/Studio/Versions\" aria-current=\"page\"", await _studio.Get("/Studio/Versions/Edit?id=matmail"));
    }
}
