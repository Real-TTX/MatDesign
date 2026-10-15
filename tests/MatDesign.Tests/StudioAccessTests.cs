using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The Studio as each role meets it (stage 16, CODE-10): every page is read as an administrator, an editor and a viewer, and answers or is refused as the matrix says (a refusal is a page of the shell with
/// the role that would do, status 403); every changing address is posted by each role and is refused for the roles that lack its capability, and the volume is byte for byte what it was; the menu and the
/// buttons are drawn only for what the role may use. The anti-forgery check stays.
/// </summary>
public sealed partial class StudioAccessTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly StudioHarness _studio;

    private readonly WebApplicationFactory<Program> _factory;

    public StudioAccessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _studio = new StudioHarness(factory);
    }

    public void Dispose() => _studio.Dispose();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"<span class=""mat-user__role"">([^<]*)</span>")]
    private static partial Regex UserRole();

    [GeneratedRegex(@"<span class=""mat-user__name"">([^<]*)</span>")]
    private static partial Regex UserName();

    private sealed record Person(string Name, string Role, HttpClient Client);

    /// <summary>True when the page has the primary submit button of a form of the Studio (Save, Add, Create): the controls a role without the capability does not get.</summary>
    private static bool Saves(string html) => Regex.IsMatch(html, "<button type=\"submit\" class=\"mat-btn mat-btn--primary\"[^>]*>");

    private string _runId = string.Empty;

    /// <summary>An app, a release and the three kinds of person, each signed in in a browser of their own.</summary>
    private async Task<(Person Admin, Person Editor, Person Viewer)> Fill()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", StudioHarness.AppForm());
        // a second revision, so that there is something to restore
        var again = StudioHarness.AppForm("matx", "matx");
        again["Input.Tagline"] = "A tagline that changed";
        await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", again);
        Assert.True(_studio.Releases.Execute(Ver.First, "For the access tests.", "admin").Succeeded);
        _runId = _studio.Releases.Runs(1)[0].Id;
        _studio.AddUser("Eddie", "editor");
        _studio.AddUser("Vera", "viewer");
        return (new Person("admin", "administrator", _studio.Client), new Person("Eddie", "editor", await _studio.ClientOf("Eddie")), new Person("Vera", "viewer", await _studio.ClientOf("Vera")));
    }

    private IEnumerable<string> Pages(StudioUser? someone = null)
    {
        yield return "/Studio";
        yield return "/Studio/Account";
        yield return "/Studio/Apps";
        yield return "/Studio/Apps/Edit";
        yield return "/Studio/Apps/Edit?id=matx";
        yield return "/Studio/Apps/Edit?id=matmail";
        yield return "/Studio/Rules";
        yield return "/Studio/Rules/Edit";
        yield return "/Studio/Rules/Edit?id=SHELL-01";
        yield return "/Studio/Components";
        yield return "/Studio/Components/Edit";
        yield return "/Studio/Components/Edit?id=buttons";
        yield return "/Studio/Activity";
        yield return "/Studio/History?kind=apps&id=matx";
        yield return "/Studio/Checks";
        yield return "/Studio/Company";
        yield return "/Studio/Brand";
        yield return "/Studio/Brand/Components";
        yield return "/Studio/Brand/Assets";
        yield return "/Studio/Releases";
        yield return "/Studio/Releases/New";
        yield return $"/Studio/Releases/Run?id={_runId}";
        yield return "/Studio/Releases/WhiteLabel";
        yield return "/Studio/Versions";
        yield return "/Studio/Versions/Edit?id=matmail";
        yield return "/Studio/Users";
        yield return "/Studio/Users/Edit";
        if (someone is not null)
        {
            yield return $"/Studio/Users/Edit?id={someone.Id}";
        }
    }

    /// <summary>What a role gets for an address, by the matrix: the page that needs more than the role has is refused; an empty form for a new item needs the right to create.</summary>
    private static bool Allowed(string role, string url)
    {
        var path = url.Split('?')[0];
        if (path.StartsWith("/Studio/Users", StringComparison.Ordinal))
        {
            return StudioRole.Can(role, Capability.ManageUsers);
        }

        if (path == "/Studio/Releases/New")
        {
            return StudioRole.Can(role, Capability.ManageReleases);
        }

        if (path is "/Studio/Apps/Edit" or "/Studio/Rules/Edit" or "/Studio/Components/Edit" && !url.Contains("id=", StringComparison.Ordinal))
        {
            return StudioRole.Can(role, Capability.EditDesign);
        }

        return StudioRole.Can(role, Capability.ViewStudio);
    }

    private static void FollowsTheShell(string url, string html, Person person, bool refused)
    {
        Assert.True(Heading().Matches(html).Count == 1, $"{url}: {Heading().Matches(html).Count} h1");
        Assert.True(html.Contains("class=\"mat-nav\"") && html.Contains("class=\"mat-crumbs\"") && html.Contains("data-theme-toggle") && html.Contains("class=\"mat-nav__version\""), $"{url}: no shell");
        Assert.True(html.Contains("<main class=\"mat-page"), $"{url}: no main landmark");
        Assert.False(InlineStyle().IsMatch(html), $"{url}: an inline style");
        Assert.Equal(person.Name, UserName().Match(html).Groups[1].Value);
        Assert.Equal(StudioRole.Find(person.Role)!.Name, UserRole().Match(html).Groups[1].Value);
        Assert.Equal(refused, html.Contains("data-forbidden=", StringComparison.Ordinal));
    }

    // ---- reading ----

    [Fact]
    public async Task Every_page_of_the_studio_answers_or_is_refused_to_each_role_as_the_matrix_says()
    {
        var (admin, editor, viewer) = await Fill();
        var someone = _studio.Service.Users.ByName("Eddie")!;
        var problems = new List<string>();

        foreach (var person in new[] { admin, editor, viewer })
        {
            foreach (var url in Pages(someone))
            {
                var response = await person.Client.GetAsync(url);
                var html = await response.Content.ReadAsStringAsync();
                var expected = Allowed(person.Role, url) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                if (response.StatusCode != expected)
                {
                    problems.Add($"{person.Name} {url}: {(int)response.StatusCode}, expected {(int)expected}");
                    continue;
                }

                FollowsTheShell(url, html, person, refused: expected == HttpStatusCode.Forbidden);
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task A_refusal_is_a_page_that_names_the_role_that_would_do_and_leaves_the_way_out()
    {
        var (_, editor, viewer) = await Fill();

        var html = await (await editor.Client.GetAsync("/Studio/Users")).Content.ReadAsStringAsync();
        Assert.Contains("This needs the role administrator", html);
        Assert.Contains("You are signed in as <strong>Eddie</strong>, editor", html);
        Assert.Contains("href=\"/Studio/Account\"", html);
        Assert.Contains("href=\"/Studio\"", html);

        var viewerHtml = await (await viewer.Client.GetAsync("/Studio/Apps/Edit")).Content.ReadAsStringAsync();
        Assert.Contains("This needs the role editor", viewerHtml);
        Assert.Contains("create, change and delete apps", viewerHtml);
    }

    [Fact]
    public async Task Somebody_who_is_not_signed_in_is_sent_to_the_sign_in_from_every_page_but_the_three_open_ones()
    {
        var (_, _, _) = await Fill();
        var stranger = _studio.NewClient();

        foreach (var url in Pages())
        {
            var response = await stranger.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.OriginalString.Contains("/Studio/Login", StringComparison.Ordinal), $"{url}: {(int)response.StatusCode}");
        }

        foreach (var open in new[] { "/Studio/Login" })
        {
            Assert.Equal(HttpStatusCode.OK, (await stranger.GetAsync(open)).StatusCode);
        }

        var rename = await stranger.PostAsync("/Studio/Releases/WhiteLabel/rename?prefix=acme&name=AcmeUi", new StringContent("mat-btn"));
        Assert.Equal(HttpStatusCode.Redirect, rename.StatusCode);
    }

    [Fact]
    public async Task The_menu_shows_the_people_to_administrators_only_and_every_other_page_to_every_role()
    {
        var (admin, editor, viewer) = await Fill();

        foreach (var person in new[] { admin, editor, viewer })
        {
            var html = await (await person.Client.GetAsync("/Studio")).Content.ReadAsStringAsync();
            foreach (var href in new[] { "/Studio", "/Studio/Apps", "/Studio/Company", "/Studio/Brand", "/Studio/Brand/Assets", "/Studio/Rules", "/Studio/Components", "/Studio/Activity", "/Studio/Checks", "/Studio/Releases", "/Studio/Releases/WhiteLabel", "/Studio/Versions" })
            {
                Assert.Contains($"href=\"{href}\"", html);
            }

            Assert.Equal(person.Role == "administrator", html.Contains("href=\"/Studio/Users\"", StringComparison.Ordinal));
            Assert.Contains("href=\"/Studio/Account\"", html);
        }
    }

    [Fact]
    public async Task The_buttons_that_change_something_are_drawn_only_for_the_roles_that_may_use_them()
    {
        var (admin, editor, viewer) = await Fill();
        var read = new Dictionary<(string, string), string>();
        foreach (var person in new[] { admin, editor, viewer })
        {
            foreach (var url in Pages())
            {
                read[(person.Name, url)] = await (await person.Client.GetAsync(url)).Content.ReadAsStringAsync();
            }
        }

        string Html(Person p, string url) => read[(p.Name, url)];

        // the design: apps, rules, components, brand, files
        foreach (var url in new[] { "/Studio/Apps", "/Studio/Rules", "/Studio/Components" })
        {
            var noun = url.Split('/').Last().ToLowerInvariant().TrimEnd('s');
            Assert.Contains($"href=\"{url}/Edit\"", Html(admin, url));
            Assert.Contains($"href=\"{url}/Edit\"", Html(editor, url));
            Assert.DoesNotContain($"href=\"{url}/Edit\"", Html(viewer, url));
            Assert.NotEmpty(noun);
        }

        foreach (var url in new[] { "/Studio/Apps/Edit?id=matx", "/Studio/Rules/Edit?id=SHELL-01", "/Studio/Components/Edit?id=buttons", "/Studio/Brand", "/Studio/Brand/Components" })
        {
            Assert.True(Saves(Html(admin, url)), admin.Name + " " + url);
            Assert.True(Saves(Html(editor, url)), editor.Name + " " + url);
            Assert.False(Saves(Html(viewer, url)), url);
            Assert.DoesNotContain("data-read-only", Html(admin, url));
            Assert.DoesNotContain("data-read-only", Html(editor, url));
            var readOnly = Html(viewer, url);
            Assert.Contains("data-read-only", readOnly);
            Assert.Contains("Read only.", readOnly);
            Assert.Contains("This needs the role editor", readOnly);
            Assert.Contains("You are signed in as Vera, viewer.", readOnly);
            Assert.DoesNotContain("formaction=\"?handler=Delete\"", readOnly);
        }

        // the viewer's fields are turned off, not hidden: the page can be read
        var form = Html(viewer, "/Studio/Apps/Edit?id=matx");
        Assert.Matches(@"<fieldset class=""mat-fieldset"" disabled", form);
        Assert.Matches(@"<fieldset class=""mat-fieldset"">", Html(editor, "/Studio/Apps/Edit?id=matx"));

        // what only an administrator may change
        foreach (var url in new[] { "/Studio/Company", "/Studio/Releases/WhiteLabel", "/Studio/Versions/Edit?id=matmail" })
        {
            Assert.True(Saves(Html(admin, url)), admin.Name + " " + url);
            Assert.False(Saves(Html(editor, url)), editor.Name + " " + url);
            Assert.False(Saves(Html(viewer, url)), url);
            Assert.Contains("This needs the role administrator", Html(editor, url));
        }

        Assert.Contains("Switch to the", Html(admin, "/Studio/Company"));
        Assert.DoesNotContain("Switch to the", Html(editor, "/Studio/Company"));
        Assert.Contains("href=\"/Studio/Releases/New\"", Html(admin, "/Studio/Releases"));
        Assert.DoesNotContain("href=\"/Studio/Releases/New\"", Html(editor, "/Studio/Releases"));
        Assert.DoesNotContain("href=\"/Studio/Releases/New\"", Html(viewer, "/Studio/Releases"));
        Assert.Contains("Check a trial build", Html(admin, "/Studio/Releases/WhiteLabel"));
        Assert.DoesNotContain("Check a trial build", Html(editor, "/Studio/Releases/WhiteLabel"));
        Assert.DoesNotContain("<span>Use the latest", Html(editor, "/Studio/Versions/Edit?id=matmail"));

        // the checks and the history
        Assert.Contains("Run the checks again", Html(admin, "/Studio/Checks"));
        Assert.Contains("Run the checks again", Html(editor, "/Studio/Checks"));
        Assert.DoesNotContain("Run the checks again", Html(viewer, "/Studio/Checks"));
        Assert.Contains("handler=Restore", Html(editor, "/Studio/History?kind=apps&id=matx") + Html(admin, "/Studio/History?kind=apps&id=matx"));
        Assert.DoesNotContain("handler=Restore", Html(viewer, "/Studio/History?kind=apps&id=matx"));
        Assert.Contains("data-read-only", Html(viewer, "/Studio/History?kind=apps&id=matx"));
    }

    [Fact]
    public async Task The_account_page_lists_what_the_role_allows_in_words_for_every_role()
    {
        var (admin, editor, viewer) = await Fill();

        foreach (var person in new[] { admin, editor, viewer })
        {
            var html = await person.Client.GetStringAsync("/Studio/Account");
            foreach (var capability in Enum.GetValues<Capability>())
            {
                var allowed = StudioRole.Can(person.Role, capability);
                Assert.Matches($"data-capability=\"{capability}\" data-allowed=\"{(allowed ? "true" : "false")}\"", html);
                Assert.Contains(StudioRole.Describe(capability), html);
            }

            Assert.Contains(StudioRole.Find(person.Role)!.Summary, html);
            Assert.Contains($"<dd>{person.Name}</dd>", html);
        }
    }

    // ---- changing ----

    private sealed record Change(string Url, Capability Needs, IReadOnlyDictionary<string, string>? Fields = null);

    private IEnumerable<Change> Changes(StudioUser target)
    {
        var none = new Dictionary<string, string>();
        yield return new("/Studio/Apps/Edit", Capability.EditDesign, StudioHarness.AppForm("matz"));
        yield return new("/Studio/Apps/Edit?handler=Delete", Capability.EditDesign, new Dictionary<string, string> { ["Input.Original"] = "matx" });
        yield return new("/Studio/Rules/Edit", Capability.EditDesign, new Dictionary<string, string> { ["Input.Id"] = "FORM-91", ["Input.Area"] = "FORM", ["Input.Title"] = "A rule", ["Input.Statement"] = "It holds.", ["Input.Level"] = "Should", ["Input.Source"] = "Studio" });
        yield return new("/Studio/Rules/Edit?handler=Delete", Capability.EditDesign, new Dictionary<string, string> { ["Input.Original"] = "FORM-91" });
        yield return new("/Studio/Components/Edit", Capability.EditDesign, new Dictionary<string, string> { ["Input.Id"] = "qa-roles", ["Input.Name"] = "QA roles", ["Input.Group"] = "Content", ["Input.Summary"] = "A component." });
        yield return new("/Studio/Components/Edit?handler=AddExample", Capability.EditDesign, none);
        yield return new("/Studio/Components/Edit?handler=RemoveExample&index=0", Capability.EditDesign, none);
        yield return new("/Studio/Components/Edit?handler=Delete", Capability.EditDesign, new Dictionary<string, string> { ["Input.Original"] = "qa-roles" });
        yield return new("/Studio/Brand", Capability.EditDesign, new Dictionary<string, string> { ["Input.Company"] = "Roles Inc", ["Input.ShortName"] = "Roles" });
        yield return new("/Studio/Brand?handler=Reset", Capability.EditDesign, none);
        yield return new("/Studio/Brand/Components", Capability.EditDesign, none);
        yield return new("/Studio/Brand/Assets?handler=Save", Capability.EditDesign, new Dictionary<string, string> { ["slot"] = "logo" });
        yield return new("/Studio/Brand/Assets?handler=Remove", Capability.EditDesign, new Dictionary<string, string> { ["slot"] = "logo" });
        yield return new("/Studio/Brand/Assets?handler=AddFont", Capability.EditDesign, none);
        yield return new("/Studio/Brand/Assets?handler=RemoveFont", Capability.EditDesign, new Dictionary<string, string> { ["family"] = "Acme", ["weight"] = "400", ["style"] = "normal" });
        yield return new("/Studio/Brand/Assets?handler=Reset", Capability.EditDesign, none);
        yield return new("/Studio/History?handler=Restore", Capability.RestoreHistory, new Dictionary<string, string> { ["kind"] = "apps", ["id"] = "matx", ["revision"] = "1" });
        yield return new("/Studio/Checks?handler=Run", Capability.RunChecks, none);
        yield return new("/Studio/Company", Capability.ManageCompany, new Dictionary<string, string> { ["Input.Company"] = "Roles Inc", ["Input.ShortName"] = "Roles", ["Input.SiteName"] = "Roles Design" });
        yield return new("/Studio/Company?handler=Switch", Capability.ManageCompany, none);
        yield return new("/Studio/Releases/WhiteLabel", Capability.ManageWhiteLabel, new Dictionary<string, string> { ["Input.Prefix"] = "acme", ["Input.Name"] = "AcmeUi" });
        yield return new("/Studio/Releases/WhiteLabel?handler=Check", Capability.ManageWhiteLabel, new Dictionary<string, string> { ["Input.Prefix"] = "acme", ["Input.Name"] = "AcmeUi" });
        yield return new("/Studio/Releases/WhiteLabel?handler=Reset", Capability.ManageWhiteLabel, none);
        yield return new("/Studio/Releases/New", Capability.ManageReleases, new Dictionary<string, string> { ["Version"] = "9.9.9", ["Note"] = "refused" });
        yield return new("/Studio/Versions/Edit", Capability.ManageVersions, new Dictionary<string, string> { ["Input.AppId"] = "matmail", ["Input.Version"] = Ver.First });
        yield return new("/Studio/Versions/Edit?handler=Latest", Capability.ManageVersions, new Dictionary<string, string> { ["Input.AppId"] = "matmail" });
        yield return new("/Studio/Versions/Edit?handler=Forget", Capability.ManageVersions, new Dictionary<string, string> { ["Input.AppId"] = "matmail" });
        yield return new("/Studio/Users/Edit?handler=Create", Capability.ManageUsers, new Dictionary<string, string> { ["Name"] = "Mallory", ["Role"] = "administrator", ["Password"] = "mallory takes over 1" });
        yield return new($"/Studio/Users/Edit?handler=Role&id={target.Id}", Capability.ManageUsers, new Dictionary<string, string> { ["Role"] = "administrator" });
        yield return new($"/Studio/Users/Edit?handler=Disable&id={target.Id}&disable=true", Capability.ManageUsers, none);
        yield return new($"/Studio/Users/Edit?handler=Reset&id={target.Id}", Capability.ManageUsers, new Dictionary<string, string> { ["Password"] = "mallory resets it 22" });
        yield return new($"/Studio/Users/Edit?handler=Delete&id={target.Id}", Capability.ManageUsers, none);
    }

    [Fact]
    public async Task Every_change_is_refused_with_403_to_the_roles_that_lack_its_capability_and_nothing_is_saved()
    {
        var (_, editor, viewer) = await Fill();
        var target = _studio.AddUser("Target", "viewer");
        var refused = 0;

        foreach (var person in new[] { editor, viewer })
        {
            var before = _studio.Snapshot();
            foreach (var change in Changes(target).Where(c => !StudioRole.Can(person.Role, c.Needs)))
            {
                var response = await _studio.PostAs(person.Client, change.Url, change.Fields);
                var html = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{person.Name} {change.Url}: {(int)response.StatusCode}");
                Assert.Contains("data-forbidden=", html);
                Assert.Contains($"This needs the role {StudioRole.Needed(change.Needs).Name.ToLowerInvariant()}", html);
                refused++;
            }

            Assert.Equal(before, _studio.Snapshot());
        }

        Assert.True(refused > 40, $"only {refused} changes were tried");
        Assert.Equal("viewer", _studio.Service.Users.ById(target.Id)!.Role);
        Assert.Null(_studio.Service.Users.ByName("Mallory"));
    }

    [Fact]
    public async Task What_a_role_may_do_is_not_refused_and_the_editor_really_edits_and_restores_and_runs_the_checks()
    {
        var (admin, editor, _) = await Fill();
        var target = _studio.AddUser("Target", "viewer");

        foreach (var person in new[] { editor, admin })
        {
            foreach (var change in Changes(target).Where(c => StudioRole.Can(person.Role, c.Needs) && !c.Url.Contains("Users", StringComparison.Ordinal)))
            {
                var response = await _studio.PostAs(person.Client, change.Url, change.Fields);
                Assert.True(response.StatusCode != HttpStatusCode.Forbidden, $"{person.Name} {change.Url} was refused");
            }
        }

        // the editor's own work is in the history under their name
        var form = StudioHarness.AppForm("matq");
        form["Input.Name"] = "MatQ";
        form["Input.Accent"] = "#7c3aed";
        var made = await _studio.PostAs(editor.Client, "/Studio/Apps/Edit", form);
        Assert.Equal(HttpStatusCode.Redirect, made.StatusCode);
        Assert.Equal("Eddie", _studio.Store.History("apps", "matq")[0].By);
        var restore = await _studio.PostAs(editor.Client, "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "apps", ["id"] = "matq", ["revision"] = "1" });
        Assert.NotEqual(HttpStatusCode.Forbidden, restore.StatusCode);
        var run = await _studio.PostAs(editor.Client, "/Studio/Checks?handler=Run");
        Assert.Equal(HttpStatusCode.Redirect, run.StatusCode);
    }

    [Fact]
    public async Task An_editor_cannot_restore_the_company_or_the_white_label_setting_though_they_may_restore_history()
    {
        var (admin, editor, viewer) = await Fill();
        _studio.Store.Save("whitelabel", "whitelabel", new WhiteLabelSetting("acme", "AcmeUi"), "admin", "Created");
        _studio.Store.Save("whitelabel", "whitelabel", new WhiteLabelSetting("acme", "AcmeTwoUi"), "admin", "Changed");
        _studio.Design.Reload();
        var before = _studio.Snapshot();

        foreach (var kind in new[] { "tenant", "whitelabel" })
        {
            var id = kind == "tenant" ? "tenant" : "whitelabel";
            var response = await _studio.PostAs(editor.Client, "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = kind, ["id"] = id, ["revision"] = "1" });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _studio.PostAs(viewer.Client, "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = kind, ["id"] = id, ["revision"] = "1" })).StatusCode);
        }

        Assert.Equal(before, _studio.Snapshot());
        Assert.NotEqual(HttpStatusCode.Forbidden, (await _studio.PostAs(admin.Client, "/Studio/History?handler=Restore", new Dictionary<string, string> { ["kind"] = "whitelabel", ["id"] = "whitelabel", ["revision"] = "1" })).StatusCode);
        // an editor sees the history of the white label setting without the button
        Assert.DoesNotContain("handler=Restore", await editor.Client.GetStringAsync("/Studio/History?kind=whitelabel&id=whitelabel"));
    }

    [Fact]
    public async Task The_endpoint_that_renames_for_the_white_label_build_is_for_administrators()
    {
        var (admin, editor, viewer) = await Fill();

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.Client.PostAsync("/Studio/Releases/WhiteLabel/rename?prefix=acme&name=AcmeUi", new StringContent("mat-btn"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Client.PostAsync("/Studio/Releases/WhiteLabel/rename?prefix=acme&name=AcmeUi", new StringContent("mat-btn"))).StatusCode);
        var renamed = await admin.Client.PostAsync("/Studio/Releases/WhiteLabel/rename?prefix=acme&name=AcmeUi", new StringContent("mat-btn"));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("acme-btn", await renamed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_anti_forgery_check_stays_and_comes_before_everything_else()
    {
        var (_, editor, viewer) = await Fill();

        foreach (var person in new[] { editor, viewer })
        {
            foreach (var url in new[] { "/Studio/Apps/Edit", "/Studio/Users/Edit?handler=Create", "/Studio/Checks?handler=Run" })
            {
                var response = await person.Client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>()));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }

        var before = _studio.Snapshot();
        Assert.Equal(HttpStatusCode.BadRequest, (await _studio.Client.PostAsync("/Studio/Apps/Edit", new FormUrlEncodedContent(StudioHarness.AppForm("matw")))).StatusCode);
        Assert.Equal(before, _studio.Snapshot());
    }

    // ---- the pages in both templates ----

    [Fact]
    public async Task The_new_pages_follow_the_shell_rules_with_the_blank_template_too()
    {
        using var blank = new StudioHarness(_factory, new Dictionary<string, string> { ["MATDESIGN_TEMPLATE"] = "blank", ["MATDESIGN_COMPANY"] = "Acme" });
        await blank.SignIn();
        blank.AddUser("Eddie", "editor");
        var editor = await blank.ClientOf("Eddie");
        var someone = blank.Service.Users.ByName("Eddie")!;

        foreach (var url in new[] { "/Studio/Users", "/Studio/Users/Edit", $"/Studio/Users/Edit?id={someone.Id}", "/Studio/Account" })
        {
            var html = await blank.Html(url);
            Assert.True(Heading().Matches(html).Count == 1, url);
            Assert.Contains("class=\"mat-nav\"", html);
            Assert.Contains("Acme", html);
            Assert.DoesNotContain("MatDesign ", html.Replace("MatDesign.", string.Empty, StringComparison.Ordinal));
            Assert.False(InlineStyle().IsMatch(html), url);
        }

        var refused = await editor.GetAsync("/Studio/Users");
        var page = await refused.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("Acme", page);
        Assert.True(Heading().Matches(page).Count == 1);
    }

}
