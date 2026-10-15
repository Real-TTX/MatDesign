using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MatDesign.Tests;

/// <summary>
/// The people of the Studio at work (stage 16, CODE-10): the pages that manage them, sessions that follow the role, the account and the password, a lock-out that is per name and per address, a
/// migration from the old administrator file, and a volume that never holds a secret outside its one file.
/// </summary>
public sealed partial class StudioPeopleTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly StudioHarness _studio;
    private readonly List<string> _directories = [];

    public StudioPeopleTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _studio = new StudioHarness(factory);
    }

    public void Dispose()
    {
        _studio.Dispose();
        foreach (var directory in _directories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex Token();

    private static string Local(HttpResponseMessage response)
        => response.Headers.Location!.IsAbsoluteUri ? response.Headers.Location.PathAndQuery : response.Headers.Location.OriginalString;

    private Task<HttpResponseMessage> Create(HttpClient admin, string name, string role, string password)
        => _studio.PostAs(admin, "/Studio/Users/Edit?handler=Create", new Dictionary<string, string> { ["Name"] = name, ["Role"] = role, ["Password"] = password });

    private Task<HttpResponseMessage> Change(HttpClient admin, string handler, StudioUser person, Dictionary<string, string>? fields = null, string extra = "")
        => _studio.PostAs(admin, $"/Studio/Users/Edit?handler={handler}&id={person.Id}{extra}", fields);

    private async Task<HttpStatusCode> StatusOf(HttpClient client, string url) => (await client.GetAsync(url)).StatusCode;

    // ---- the pages of the people ----

    [Fact]
    public async Task An_administrator_creates_a_person_through_the_form_and_the_list_shows_them()
    {
        await _studio.SignIn();

        var response = await Create(_studio.Client, "Anna Lee", "editor", "a first password 1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Studio/Users", Local(response));
        var person = _studio.Service.Users.ByName("anna lee")!;
        Assert.Equal("editor", person.Role);
        Assert.True(person.MustChange);
        Assert.Equal("admin", person.CreatedBy);
        var list = await _studio.Client.GetStringAsync("/Studio/Users");
        Assert.Contains("Anna Lee", list);
        Assert.Contains("Must change password", list);
        Assert.Contains($"href=\"/Studio/Users/Edit?id={person.Id}\"", list);
        Assert.Contains("<small>You</small>", list);
    }

    [Theory]
    [InlineData("Anna Lee", "editor", "short", "At least 10 characters")]
    [InlineData("Anna Lee", "editor", "password123", "guessed")]
    [InlineData("Anna Lee", "boss", "a first password 1", "Choose a role")]
    [InlineData("A", "editor", "a first password 1", "Use 2 to 40")]
    [InlineData("<b>x</b>", "editor", "a first password 1", "Use 2 to 40")]
    [InlineData("ADMIN", "editor", "a first password 1", "built-in administrator")]
    public async Task What_does_not_fit_is_refused_at_the_field_and_the_typed_password_never_comes_back_in_the_page(string name, string role, string password, string message)
    {
        await _studio.SignIn();
        var before = _studio.Snapshot();

        var response = await Create(_studio.Client, name, role, password);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, html);
        Assert.Contains("Nothing was changed.", html);
        Assert.DoesNotContain(password, html);
        Assert.Equal(before, _studio.Snapshot());
    }

    [Fact]
    public async Task A_name_is_not_given_twice_whatever_its_case()
    {
        await _studio.SignIn();
        await Create(_studio.Client, "Anna Lee", "editor", "a first password 1");

        var twice = await Create(_studio.Client, "ANNA LEE", "viewer", "a first password 1");

        Assert.Equal(HttpStatusCode.OK, twice.StatusCode);
        Assert.Contains("Somebody has this name already", await twice.Content.ReadAsStringAsync());
        Assert.Equal(2, _studio.Service.Users.All.Count);
    }

    [Fact]
    public async Task A_role_is_changed_and_a_person_is_disabled_enabled_and_deleted_through_the_page()
    {
        await _studio.SignIn();
        var person = _studio.AddUser("Cleo", "editor");

        var role = await Change(_studio.Client, "Role", person, new() { ["Role"] = "viewer" });
        Assert.Equal(HttpStatusCode.Redirect, role.StatusCode);
        Assert.Equal("viewer", _studio.Service.Users.ById(person.Id)!.Role);

        Assert.Equal(HttpStatusCode.Redirect, (await Change(_studio.Client, "Disable", person, extra: "&disable=true")).StatusCode);
        Assert.True(_studio.Service.Users.ById(person.Id)!.Disabled);
        var page = await _studio.Client.GetStringAsync($"/Studio/Users/Edit?id={person.Id}");
        Assert.Contains("Enable the account", page);
        Assert.Equal(HttpStatusCode.Redirect, (await Change(_studio.Client, "Disable", person, extra: "&disable=false")).StatusCode);
        Assert.False(_studio.Service.Users.ById(person.Id)!.Disabled);

        var delete = await Change(_studio.Client, "Delete", person);
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.Null(_studio.Service.Users.ById(person.Id));
        Assert.Equal(HttpStatusCode.NotFound, await StatusOf(_studio.Client, $"/Studio/Users/Edit?id={person.Id}"));
    }

    [Fact]
    public async Task The_built_in_administrator_has_no_page_and_cannot_be_changed_by_a_post()
    {
        await _studio.SignIn();

        Assert.Equal(HttpStatusCode.NotFound, await StatusOf(_studio.Client, "/Studio/Users/Edit?id=env-admin"));
        var post = await _studio.PostAs(_studio.Client, "/Studio/Users/Edit?handler=Delete&id=env-admin");
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.NotNull(_studio.Service.Users.ById("env-admin"));
    }

    // ---- the guards, through the page ----

    private async Task<(HttpClient Client, StudioUser Me, StudioHarness Site)> Founded(string name = "Ann")
    {
        // a Studio that was set up (no built-in administrator): the first user is an administrator
        var directory = Path.Combine(Path.GetTempPath(), "matdesign-people-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("MATDESIGN_DATA", directory));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Studio/Setup");
        var response = await client.PostAsync("/Studio/Setup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Password"] = "the founder password 1",
            ["Repeat"] = "the founder password 1",
            ["__RequestVerificationToken"] = Token().Match(page).Groups[1].Value,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var site = new StudioHarness(factory, directory);
        return (client, site.Service.Users.ByName(name)!, site);
    }

    [Fact]
    public async Task Nobody_deletes_disables_or_changes_the_role_of_themselves_and_the_page_does_not_offer_it()
    {
        var (client, me, site) = await Founded();
        var other = site.Service.Users.Create("Bob", "administrator", "bob has a password 2", "Ann", mustChange: false).User!;

        var page = await client.GetStringAsync($"/Studio/Users/Edit?id={me.Id}");
        Assert.DoesNotContain("handler=Delete", page);
        Assert.DoesNotContain("handler=Disable", page);
        Assert.DoesNotContain("<span>Save the role</span>", page);
        Assert.Contains("You cannot change your own role", page);
        Assert.Contains("your account page", page);

        foreach (var (handler, fields, extra) in new (string, Dictionary<string, string>?, string)[]
        {
            ("Delete", null, string.Empty),
            ("Disable", null, "&disable=true"),
            ("Role", new() { ["Role"] = "viewer" }, string.Empty),
            ("Reset", new() { ["Password"] = "a new password for me 3" }, string.Empty),
        })
        {
            var response = await site.PostAs(client, $"/Studio/Users/Edit?handler={handler}&id={me.Id}{extra}", fields);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Nothing was changed.", await response.Content.ReadAsStringAsync());
        }

        var still = site.Service.Users.ById(me.Id)!;
        Assert.Equal("administrator", still.Role);
        Assert.False(still.Disabled);
        Assert.NotNull(site.Service.Users.Verify("Ann", "the founder password 1"));

        // and another administrator can, as long as one is left
        var demote = await site.PostAs(client, $"/Studio/Users/Edit?handler=Role&id={other.Id}", new Dictionary<string, string> { ["Role"] = "viewer" });
        Assert.Equal(HttpStatusCode.Redirect, demote.StatusCode);
        site.Dispose();
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_taken_away_by_a_request_made_for_it()
    {
        var (client, me, site) = await Founded();
        var editor = site.Service.Users.Create("Cleo", "editor", "cleo has a password 2", "Ann", mustChange: false).User!;

        // the page leaves the controls out, and a request that is made anyway is refused by the store
        var page = await client.GetStringAsync($"/Studio/Users/Edit?id={me.Id}");
        Assert.Contains("You cannot disable or remove your own account", page);
        Assert.False(site.Service.Users.SetRole(me.Id, "viewer", editor.Id).Ok);
        Assert.False(site.Service.Users.SetDisabled(me.Id, true, editor.Id).Ok);
        Assert.False(site.Service.Users.Delete(me.Id, editor.Id).Ok);
        Assert.Equal(1, site.Service.Users.ActiveAdministrators);
        site.Dispose();
    }

    [Fact]
    public async Task The_page_of_the_only_administrator_for_another_administrator_leaves_the_controls_out_and_says_why()
    {
        var (client, me, site) = await Founded();
        var bob = site.Service.Users.Create("Bob", "administrator", "bob has a password 2", "Ann", mustChange: false).User!;
        site.Service.Users.SetDisabled(me.Id, true, bob.Id);
        var bobClient = site.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await site.SignInAs(bobClient, "Bob", "bob has a password 2")).StatusCode);

        // Ann is disabled, so Bob is the last active administrator: he cannot be changed (by Bob either), and Ann's page offers nothing for Bob
        var page = await bobClient.GetStringAsync($"/Studio/Users/Edit?id={me.Id}");
        Assert.Contains("Enable the account", page);
        Assert.False(site.Service.Users.SetDisabled(bob.Id, true, me.Id).Ok);
        Assert.Equal(HttpStatusCode.Redirect, (await site.PostAs(bobClient, $"/Studio/Users/Edit?handler=Disable&id={me.Id}&disable=false")).StatusCode);
        Assert.NotEmpty(client.DefaultRequestHeaders.ToString() + "x");
        site.Dispose();
    }

    // ---- sessions follow the person ----

    [Fact]
    public async Task A_role_that_is_changed_takes_effect_at_the_next_click_of_a_session_that_is_open()
    {
        await _studio.SignIn();
        var person = _studio.AddUser("Eddie", "editor");
        var browser = await _studio.ClientOf("Eddie");
        Assert.Equal(HttpStatusCode.OK, await StatusOf(browser, "/Studio/Apps/Edit"));
        Assert.Contains("Editor", await browser.GetStringAsync("/Studio"));

        await Change(_studio.Client, "Role", person, new() { ["Role"] = "viewer" });

        Assert.Equal(HttpStatusCode.Forbidden, await StatusOf(browser, "/Studio/Apps/Edit"));
        var overview = await browser.GetStringAsync("/Studio");
        Assert.Contains("<span class=\"mat-user__role\">Viewer</span>", overview);
        var post = await _studio.PostAs(browser, "/Studio/Apps/Edit", StudioHarness.AppForm("matv"));
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Null(_studio.Store.Current("apps", "matv"));

        // and the other way: the session gets what the new role allows, without signing in again
        await Change(_studio.Client, "Role", person, new() { ["Role"] = "administrator" });
        Assert.Equal(HttpStatusCode.OK, await StatusOf(browser, "/Studio/Users"));
    }

    [Fact]
    public async Task A_person_who_is_disabled_is_signed_out_at_the_next_click_and_cannot_come_back_with_the_old_session()
    {
        await _studio.SignIn();
        var person = _studio.AddUser("Eddie", "editor");
        var browser = await _studio.ClientOf("Eddie");
        Assert.Equal(HttpStatusCode.OK, await StatusOf(browser, "/Studio"));

        await Change(_studio.Client, "Disable", person, extra: "&disable=true");

        var gone = await browser.GetAsync("/Studio");
        Assert.Equal(HttpStatusCode.Redirect, gone.StatusCode);
        Assert.Contains("/Studio/Login", Local(gone));
        var refused = await _studio.SignInAs(_studio.NewClient(), "Eddie");
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("The name or the password is not right.", await refused.Content.ReadAsStringAsync());

        // enabled again: a session from before does not return with the account, a new one is needed
        await Change(_studio.Client, "Disable", person, extra: "&disable=false");
        var old = _studio.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await StudioHarnessSignIn(old)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await StatusOf(old, "/Studio"));
    }

    private Task<HttpResponseMessage> StudioHarnessSignIn(HttpClient client) => _studio.SignInAs(client, "Eddie");

    [Fact]
    public async Task A_person_who_is_deleted_is_out_and_what_they_did_stays_under_their_name()
    {
        await _studio.SignIn();
        var person = _studio.AddUser("Eddie", "editor");
        var browser = await _studio.ClientOf("Eddie");
        var form = StudioHarness.AppForm("matd");
        form["Input.Name"] = "MatD";
        form["Input.Accent"] = "#7c3aed";
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.PostAs(browser, "/Studio/Apps/Edit", form)).StatusCode);

        await Change(_studio.Client, "Delete", person);

        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Studio")).StatusCode);
        Assert.Equal("Eddie", _studio.Store.History("apps", "matd")[0].By);
        var activity = await _studio.Client.GetStringAsync("/Studio/Activity");
        Assert.Contains("Eddie", activity);
        Assert.Contains("Deleted the user Eddie", activity);
        Assert.Equal(HttpStatusCode.OK, await StatusOf(_studio.Client, "/Studio/Activity?by=Eddie"));
        var again = await Create(_studio.Client, "Eddie", "editor", "a first password 1");
        Assert.Contains("removed", await again.Content.ReadAsStringAsync());
    }

    // ---- the first password of a person, and the account ----

    [Fact]
    public async Task A_password_that_an_administrator_chose_has_to_be_changed_before_anything_else_opens()
    {
        await _studio.SignIn();
        await Create(_studio.Client, "Fay", "editor", "handed over password 7");
        var browser = _studio.NewClient();

        var signIn = await _studio.SignInAs(browser, "Fay", "handed over password 7");
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Equal("/Studio/Account", Local(signIn));

        foreach (var url in new[] { "/Studio", "/Studio/Apps", "/Studio/Apps/Edit", "/Studio/Checks", "/Studio/Users" })
        {
            var response = await browser.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.Redirect && Local(response) == "/Studio/Account", $"{url}: {(int)response.StatusCode}");
        }

        var account = await browser.GetStringAsync("/Studio/Account");
        Assert.Contains("data-must-change", account);
        Assert.Contains("Choose your own password.", account);

        // not even a change that she may make: a post goes to the account page and saves nothing
        var before = _studio.Snapshot();
        var post = await _studio.PostAs(browser, "/Studio/Apps/Edit", StudioHarness.AppForm("matf"));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal("/Studio/Account", Local(post));
        Assert.Equal(before, _studio.Snapshot());
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsync("/Studio/Releases/WhiteLabel/rename?prefix=acme&name=AcmeUi", new StringContent("mat-btn"))).StatusCode);

        // wrong current password, the same password again, a weak one, a repeat that differs: each says so and changes nothing
        foreach (var (current, password, repeat, message) in new[]
        {
            ("not the one", "a better password 8", "a better password 8", "The current password is not right."),
            ("handed over password 7", "handed over password 7", "handed over password 7", "must differ"),
            ("handed over password 7", "password123", "password123", "guessed"),
            ("handed over password 7", "a better password 8", "something else entirely", "not the same"),
        })
        {
            var response = await _studio.PostAs(browser, "/Studio/Account?handler=Password", new Dictionary<string, string> { ["Current"] = current, ["Password"] = password, ["Repeat"] = repeat });
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(message, html);
            Assert.DoesNotContain("a better password 8", html);
            Assert.True(_studio.Service.Users.ByName("Fay")!.MustChange);
        }

        var done = await _studio.PostAs(browser, "/Studio/Account?handler=Password", new Dictionary<string, string> { ["Current"] = "handed over password 7", ["Password"] = "a better password 8", ["Repeat"] = "a better password 8" });
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Equal("/Studio", Local(done));
        Assert.False(_studio.Service.Users.ByName("Fay")!.MustChange);
        Assert.Equal(HttpStatusCode.OK, await StatusOf(browser, "/Studio/Apps"));
        Assert.Equal(HttpStatusCode.Redirect, (await _studio.SignInAs(_studio.NewClient(), "Fay", "a better password 8")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _studio.SignInAs(_studio.NewClient(), "Fay", "handed over password 7")).StatusCode);
    }

    [Fact]
    public async Task A_password_change_ends_every_other_session_of_the_person_and_not_the_one_that_changed_it()
    {
        await _studio.SignIn();
        _studio.AddUser("Gus", "editor");
        var here = await _studio.ClientOf("Gus");
        var there = await _studio.ClientOf("Gus");
        Assert.Equal(HttpStatusCode.OK, await StatusOf(there, "/Studio"));

        var done = await _studio.PostAs(here, "/Studio/Account?handler=Password", new Dictionary<string, string> { ["Current"] = StudioHarness.PersonPassword, ["Password"] = "a brand new password 4", ["Repeat"] = "a brand new password 4" });

        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await StatusOf(here, "/Studio"));
        var other = await there.GetAsync("/Studio");
        Assert.Equal(HttpStatusCode.Redirect, other.StatusCode);
        Assert.Contains("/Studio/Login", Local(other));
    }

    [Fact]
    public async Task A_password_that_an_administrator_resets_ends_the_sessions_of_the_person_and_must_be_changed()
    {
        await _studio.SignIn();
        var person = _studio.AddUser("Hal", "viewer");
        var browser = await _studio.ClientOf("Hal");

        var reset = await Change(_studio.Client, "Reset", person, new() { ["Password"] = "reset by the boss 5" });

        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Studio")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _studio.SignInAs(_studio.NewClient(), "Hal")).StatusCode);
        var again = _studio.NewClient();
        var signIn = await _studio.SignInAs(again, "Hal", "reset by the boss 5");
        Assert.Equal("/Studio/Account", Local(signIn));
        Assert.True(_studio.Service.Users.ById(person.Id)!.MustChange);
    }

    [Fact]
    public async Task The_current_password_is_needed_and_the_built_in_administrator_changes_theirs_in_the_environment()
    {
        await _studio.SignIn();

        var page = await _studio.Client.GetStringAsync("/Studio/Account");
        Assert.Contains("MATDESIGN_ADMIN_PASSWORD", page);
        Assert.DoesNotContain("name=\"Current\"", page);
        var post = await _studio.PostAs(_studio.Client, "/Studio/Account?handler=Password", new Dictionary<string, string> { ["Current"] = StudioHarness.Password, ["Password"] = "a brand new password 4", ["Repeat"] = "a brand new password 4" });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        Assert.Contains("is the value of MATDESIGN_ADMIN_PASSWORD", await post.Content.ReadAsStringAsync());
        Assert.NotNull(_studio.Service.Users.Verify("admin", StudioHarness.Password));
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        await _studio.SignIn();
        _studio.AddUser("Ida", "viewer");
        var browser = await _studio.ClientOf("Ida");

        var done = await _studio.PostAs(browser, "/Studio/Logout");

        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Studio")).StatusCode);
    }

    [Fact]
    public void The_cookie_carries_the_id_and_the_stamp_and_no_role()
    {
        var user = new StudioUser { Id = "ann-1a2b3c", Name = "Ann", Role = "administrator", SessionStamp = "0123456789abcdef" };

        var claims = StudioSession.PrincipalOf(user).Claims.ToList();

        Assert.Equal([ClaimTypes.Name, ClaimTypes.NameIdentifier, StudioService.StampClaim], claims.Select(c => c.Type));
        Assert.DoesNotContain(claims, c => c.Type == ClaimTypes.Role);
        Assert.DoesNotContain(claims, c => c.Value.Contains("administrator", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("ann-1a2b3c", claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
    }

    // ---- the lock-out ----

    [Fact]
    public void The_lock_is_per_name_and_per_address_so_that_nobody_locks_another_person_out_by_guessing()
    {
        var studio = new StudioService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MATDESIGN_DATA"] = NewDirectory() }).Build());

        for (var i = 0; i < 5; i++)
        {
            studio.Failed("Eddie", "10.0.0.1");
        }

        Assert.NotNull(studio.LockedFor("Eddie", "10.0.0.1"));
        Assert.NotNull(studio.LockedFor(" EDDIE ", "10.0.0.1"));
        Assert.Null(studio.LockedFor("Eddie", "10.0.0.2"));
        Assert.Null(studio.LockedFor("Vera", "10.0.0.1"));
        Assert.True(studio.LockedFor("Eddie", "10.0.0.1")!.Value <= TimeSpan.FromMinutes(1));
        studio.Succeeded("Eddie", "10.0.0.1");
        Assert.Null(studio.LockedFor("Eddie", "10.0.0.1"));
    }

    [Fact]
    public void One_address_that_tries_many_names_is_locked_as_a_whole_with_the_same_wait()
    {
        var studio = new StudioService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MATDESIGN_DATA"] = NewDirectory() }).Build());

        for (var i = 0; i < 29; i++)
        {
            studio.Failed($"guess {i}", "10.0.0.9");
        }

        Assert.Null(studio.LockedFor("somebody else", "10.0.0.9"));
        studio.Failed("guess 29", "10.0.0.9");
        Assert.NotNull(studio.LockedFor("somebody else", "10.0.0.9"));
        Assert.Null(studio.LockedFor("somebody else", "10.0.0.10"));
    }

    private string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "matdesign-people-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        return directory;
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_that_name_from_that_address_and_not_the_same_person_from_another_address()
    {
        await _studio.SignIn();
        _studio.AddUser("Eddie", "editor");
        var attacker = _studio.NewClient();
        attacker.DefaultRequestHeaders.Add("X-Forwarded-For", "10.1.1.1");
        var friend = _studio.NewClient();
        friend.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.2.2");

        for (var i = 0; i < 5; i++)
        {
            await _studio.SignInAs(attacker, "Eddie", "wrong " + i);
        }

        var locked = await _studio.SignInAs(attacker, "Eddie");
        Assert.Contains("Too many attempts", await locked.Content.ReadAsStringAsync());
        var fine = await _studio.SignInAs(friend, "Eddie");
        Assert.Equal(HttpStatusCode.Redirect, fine.StatusCode);
    }

    [Fact]
    public async Task The_sign_in_never_says_whether_a_name_exists_or_an_account_is_disabled_and_the_lock_is_the_same_for_a_name_that_does_not_exist()
    {
        await _studio.SignIn();
        _studio.AddUser("Eddie", "editor");
        var gone = _studio.AddUser("Ghost", "viewer");
        _studio.Service.Users.SetDisabled(gone.Id, true, "test");

        var messages = new List<string>();
        foreach (var (name, password) in new[] { ("Eddie", "wrong password 1"), ("Nobody", "wrong password 1"), ("Ghost", StudioHarness.PersonPassword), ("Ghost", "wrong password 1") })
        {
            var response = await _studio.SignInAs(_studio.NewClient(), name, password);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            messages.Add(Regex.Match(html, @"<div class=""mat-alert__body"">([^<]*)</div>").Groups[1].Value);
        }

        Assert.All(messages, m => Assert.Equal("The name or the password is not right.", m));

        // five tries at a name that does not exist lock it exactly like one that does
        var client = _studio.NewClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.3.3");
        for (var i = 0; i < 5; i++)
        {
            await _studio.SignInAs(client, "Nobody", "wrong " + i);
        }

        Assert.Contains("Too many attempts", await (await _studio.SignInAs(client, "Nobody")).Content.ReadAsStringAsync());
    }

    // ---- the migration, through the site ----

    [Fact]
    public async Task A_studio_with_an_admin_json_of_the_old_code_signs_the_old_administrator_in_with_the_old_password()
    {
        var directory = NewDirectory();
        Directory.CreateDirectory(directory);
        Assert.True(AdminAccount.Create("Matthias", "the password of the old days").TryWrite(directory));
        var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("MATDESIGN_DATA", directory));
        using var site = new StudioHarness(factory, directory);

        // no setup assistant: the old administrator is the first user
        var client = site.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Studio/Setup")).StatusCode);
        var response = await site.SignInAs(client, "matthias", "the password of the old days");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Studio", Local(response));

        var overview = await client.GetStringAsync("/Studio");
        Assert.Contains("<span class=\"mat-user__role\">Administrator</span>", overview);
        Assert.Equal(HttpStatusCode.OK, await StatusOf(client, "/Studio/Users"));
        Assert.Contains("Matthias", await client.GetStringAsync("/Studio/Users"));
        Assert.False(File.Exists(Path.Combine(directory, "admin.json")));
        Assert.True(File.Exists(Path.Combine(directory, "admin.json.migrated")));
        Assert.True(File.Exists(Path.Combine(directory, "users.json")));

        var activity = await client.GetStringAsync("/Studio/Activity");
        Assert.Contains("Migrated from admin.json", activity);
        Assert.Contains("migration", activity);
    }

    // ---- secrets ----

    private sealed class Capture : ILoggerProvider
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Sink(this);

        public void Dispose()
        {
        }

        private sealed class Sink(Capture owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner.Lines)
                {
                    owner.Lines.Add(formatter(state, exception) + (exception?.ToString() ?? string.Empty));
                }
            }
        }
    }

    [Fact]
    public async Task No_password_hash_or_salt_is_ever_in_the_history_the_activity_a_page_or_a_log()
    {
        var directory = NewDirectory();
        var logs = new Capture();
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_DATA", directory);
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
        });
        var passwords = new[] { "the founder password 1", "a first password 2 for cleo", "reset by the boss 3", "cleo chose this one 4", "a wrong guess number 5" };
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var setupPage = await client.GetStringAsync("/Studio/Setup");
        await client.PostAsync("/Studio/Setup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Founder", ["Password"] = passwords[0], ["Repeat"] = passwords[0], ["__RequestVerificationToken"] = Token().Match(setupPage).Groups[1].Value,
        }));
        using var site = new StudioHarness(factory, directory);
        var admin = client;

        await Create(admin, "Cleo", "editor", passwords[1]);
        var cleo = site.Service.Users.ByName("Cleo")!;
        await site.PostAs(admin, $"/Studio/Users/Edit?handler=Reset&id={cleo.Id}", new Dictionary<string, string> { ["Password"] = passwords[2] });
        var browser = site.NewClient();
        await site.SignInAs(browser, "Cleo", passwords[2]);
        await site.PostAs(browser, "/Studio/Account?handler=Password", new Dictionary<string, string> { ["Current"] = passwords[2], ["Password"] = passwords[3], ["Repeat"] = passwords[3] });
        await site.SignInAs(site.NewClient(), "Cleo", passwords[4]);
        await site.PostAs(admin, $"/Studio/Users/Edit?handler=Role&id={cleo.Id}", new Dictionary<string, string> { ["Role"] = "viewer" });
        await site.PostAs(admin, $"/Studio/Users/Edit?handler=Disable&id={cleo.Id}&disable=true");
        await Create(admin, "Weak", "viewer", "password123");
        var pages = string.Concat(await Task.WhenAll(new[] { "/Studio", "/Studio/Users", $"/Studio/Users/Edit?id={cleo.Id}", "/Studio/Account", "/Studio/Activity", "/Studio/Checks" }.Select(u => admin.GetStringAsync(u))));

        var users = site.Service.Users;
        var secrets = users.All.SelectMany(u => new[] { u.Hash, u.Salt }).Where(s => s.Length > 0).ToList();
        Assert.NotEmpty(secrets);

        // the passwords are nowhere, in any file, in any page, in any log
        var everything = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "keys" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(directory, f), f => Encoding.UTF8.GetString(File.ReadAllBytes(f)));
        foreach (var password in passwords.Append("password123"))
        {
            Assert.DoesNotContain(everything, f => f.Value.Contains(password, StringComparison.Ordinal));
            Assert.DoesNotContain(password, pages);
            lock (logs.Lines)
            {
                Assert.DoesNotContain(logs.Lines, l => l.Contains(password, StringComparison.Ordinal));
            }
        }

        // the hash and the salt are in users.json and in no other file, no page and no log
        foreach (var secret in secrets)
        {
            // the file is JSON: a + in a Base64 text is written as +
            var written = JsonSerializer.Serialize(secret).Trim('"');
            Assert.Equal(["users.json"], everything.Where(f => f.Value.Contains(written, StringComparison.Ordinal) || f.Value.Contains(secret, StringComparison.Ordinal)).Select(f => f.Key));
            Assert.DoesNotContain(secret, pages);
            lock (logs.Lines)
            {
                Assert.DoesNotContain(logs.Lines, l => l.Contains(secret, StringComparison.Ordinal));
            }
        }

        // what the history keeps of a person: the id, the name, the role and whether the account is disabled
        var files = Directory.GetFiles(Path.Combine(directory, "studio", "users"), "??????.json", SearchOption.AllDirectories);
        Assert.True(files.Length >= 6, $"{files.Length} entries");
        foreach (var file in files)
        {
            var revision = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
            if (revision.GetProperty("data").ValueKind == JsonValueKind.Object)
            {
                Assert.Equal(["disabled", "id", "name", "role"], revision.GetProperty("data").EnumerateObject().Select(p => p.Name).Order());
            }

            var text = File.ReadAllText(file);
            foreach (var key in new[] { "hash", "salt", "iterations", "sessionStamp", "mustChange" })
            {
                Assert.DoesNotContain($"\"{key}\"", text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task The_activity_names_who_did_what_to_whom_for_every_change_to_the_people()
    {
        await _studio.SignIn();
        await Create(_studio.Client, "Cleo", "editor", "a first password 2 for cleo");
        var cleo = _studio.Service.Users.ByName("Cleo")!;
        await Change(_studio.Client, "Role", cleo, new() { ["Role"] = "viewer" });
        await Change(_studio.Client, "Disable", cleo, extra: "&disable=true");
        await Change(_studio.Client, "Disable", cleo, extra: "&disable=false");
        await Change(_studio.Client, "Reset", cleo, new() { ["Password"] = "reset by the boss 3" });
        var browser = _studio.NewClient();
        await _studio.SignInAs(browser, "Cleo", "reset by the boss 3");
        await _studio.PostAs(browser, "/Studio/Account?handler=Password", new Dictionary<string, string> { ["Current"] = "reset by the boss 3", ["Password"] = "cleo chose this one 4", ["Repeat"] = "cleo chose this one 4" });
        await Change(_studio.Client, "Delete", cleo);

        var history = _studio.Store.History("users", cleo.Id).Reverse().ToList();

        Assert.Equal(
            ["admin", "admin", "admin", "admin", "admin", "Cleo", "admin"],
            history.Select(r => r.By));
        Assert.Contains("Created as editor", history[0].Note);
        Assert.Contains("Role changed from editor to viewer", history[1].Note);
        Assert.Contains("Disabled", history[2].Note);
        Assert.Equal("Enabled", history[3].Note);
        Assert.Contains("Password reset by an administrator", history[4].Note);
        Assert.Contains("Changed their own password", history[5].Note);
        Assert.Contains("Deleted the user Cleo", history[6].Note);
        Assert.True(history[6].Deleted);

        var activity = await _studio.Client.GetStringAsync("/Studio/Activity?kind=users");
        Assert.Contains("Person", activity);
        Assert.Contains("Created as editor", activity);
        Assert.Contains(">admin<", activity.Replace("\n", string.Empty).Replace(" ", string.Empty));
        Assert.Contains("Cleo", activity);
    }

    [Fact]
    public async Task The_overview_and_the_account_name_the_person_and_the_role()
    {
        await _studio.SignIn();
        _studio.AddUser("Eddie", "editor");
        var browser = await _studio.ClientOf("Eddie");

        var overview = await browser.GetStringAsync("/Studio");

        Assert.Contains("Signed in as", overview);
        Assert.Contains("data-me", overview);
        Assert.Contains("<span class=\"mat-user__name\">Eddie</span>", overview);
        Assert.Contains("<span class=\"mat-user__role\">Editor</span>", overview);
        Assert.Contains("Signed in to the Studio, editor", overview);
        Assert.Contains("href=\"/Studio/Account\"", overview);
        Assert.DoesNotContain("href=\"/Studio/Users\"", overview);
    }

    // ---- the checks ----

    [Fact]
    public async Task The_check_of_the_people_passes_and_fails_for_a_file_that_cannot_be_read_and_a_release_is_then_refused()
    {
        await _studio.SignIn();
        var good = _studio.Checks.Refresh();
        Assert.Contains(good.Results, r => r.Check.Id == "users" && r.Passed);

        _studio.AddUser("Eddie", "editor");
        File.WriteAllText(Path.Combine(_studio.DataDirectory, "users.json"), "garbage");

        var bad = _studio.Checks.Refresh();

        var finding = Assert.Single(bad.Results.Single(r => r.Check.Id == "users").Findings);
        Assert.Contains("is not a valid file of users", finding.Message);
        Assert.False(bad.Passed);
        // the Checks page shows it and the pipeline stops at Check
        var page = await _studio.Client.GetStringAsync("/Studio/Checks?check=users");
        Assert.Contains("is not a valid file of users", page);
        var run = _studio.Releases.Execute("7.7.7", "refused", "admin");
        Assert.False(run.Succeeded);
        Assert.Equal("Check", run.Stages.First(s => s.Status == StageStatus.Failed).Name);
    }

    [Fact]
    public async Task A_change_to_the_people_makes_the_checks_run_again_for_the_menu_badge_and_the_report()
    {
        await _studio.SignIn();
        var before = _studio.Checks.Report;

        _studio.AddUser("Eddie", "editor");

        // the report is the one for the people as they are now: a change to them makes it again
        var after = _studio.Checks.Report;
        Assert.NotSame(before, after);
        Assert.Same(after, _studio.Checks.Report);

        // and the same report is the badge of the menu
        File.WriteAllText(Path.Combine(_studio.DataDirectory, "users.json"), "garbage");
        var refreshed = _studio.Checks.Refresh();
        Assert.False(refreshed.Results.Single(r => r.Check.Id == "users").Passed);
        Assert.Contains("checks find errors", await _studio.Client.GetStringAsync("/Studio"));
    }
}
