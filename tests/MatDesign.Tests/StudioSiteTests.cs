using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The Studio as a visitor and as the administrator meets it: off by default, a sign-in with one password, pages only after it.</summary>
public sealed partial class StudioSiteTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string Password = "correct horse battery staple";

    private readonly WebApplicationFactory<Program> _plain;
    private readonly List<string> _directories = [];

    public StudioSiteTests(WebApplicationFactory<Program> factory) => _plain = factory;

    public void Dispose()
    {
        foreach (var directory in _directories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex Token();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    private WebApplicationFactory<Program> StudioOn(string? password = Password)
    {
        var directory = Path.Combine(Path.GetTempPath(), "matdesign-studio-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        return _plain.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_DATA", directory);
            if (password is not null)
            {
                builder.UseSetting("MATDESIGN_ADMIN_PASSWORD", password);
            }
        });
    }

    /// <summary>The address of a redirect as path and query: the sign-in challenge of the cookie scheme is absolute, the page redirects are not.</summary>
    private static string Local(HttpResponseMessage response)
        => response.Headers.Location!.IsAbsoluteUri ? response.Headers.Location.PathAndQuery : response.Headers.Location.OriginalString;

    private static HttpClient Client(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> Html(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> SignIn(HttpClient client, string password = Password, string returnUrl = "", string name = "admin")
    {
        var page = await Html(client, "/Studio/Login");
        var token = Token().Match(page).Groups[1].Value;
        return await client.PostAsync("/Studio/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Fact]
    public async Task Without_a_data_directory_the_studio_is_off_and_the_sign_in_page_says_what_is_missing()
    {
        var client = Client(_plain);

        var login = await client.GetAsync("/Studio/Login");
        var html = await login.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("The Studio is off", html);
        Assert.Contains("MATDESIGN_DATA", html);
        Assert.DoesNotContain("type=\"password\"", html);

        var setup = await Html(client, "/Studio/Setup");
        Assert.Contains("The Studio is off", setup);
        Assert.DoesNotContain("type=\"password\"", setup);

        var studio = await client.GetAsync("/Studio");
        Assert.Equal(HttpStatusCode.Redirect, studio.StatusCode);
        Assert.StartsWith("/Studio/Login", Local(studio));
    }

    [Fact]
    public async Task A_data_directory_that_cannot_be_written_switches_the_studio_off()
    {
        var file = Path.Combine(Path.GetTempPath(), "matdesign-notadir-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(file, "a file where a directory should be");
        try
        {
            var factory = _plain.WithWebHostBuilder(b => { b.UseSetting("MATDESIGN_DATA", file); b.UseSetting("MATDESIGN_ADMIN_PASSWORD", Password); });
            var html = await Html(Client(factory), "/Studio/Login");
            Assert.Contains("is not writable", html);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task The_site_shows_a_visitor_a_way_in_but_not_the_studio_menu()
    {
        var client = Client(StudioOn());

        var html = await Html(client, "/");
        Assert.Contains("href=\"/Studio/Login\"", html);
        Assert.DoesNotContain("data-nav-panel=\"studio\"", html);
        Assert.DoesNotContain("/Studio/Logout", html);
    }

    [Fact]
    public async Task A_page_of_the_studio_leads_a_visitor_to_the_sign_in_and_back_after_it()
    {
        var client = Client(StudioOn());

        var denied = await client.GetAsync("/Studio/Activity");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        var location = Local(denied);
        Assert.StartsWith("/Studio/Login?returnUrl=", location);

        var page = await Html(client, location);
        var token = Token().Match(page).Groups[1].Value;
        var signedIn = await client.PostAsync(location, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "admin",
            ["Password"] = Password,
            ["ReturnUrl"] = "/Studio/Activity",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/Studio/Activity", Local(signedIn));
    }

    [Fact]
    public async Task A_wrong_password_does_not_sign_in()
    {
        var client = Client(StudioOn());

        var response = await SignIn(client, "not the password");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The name or the password is not right.", html);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("matdesign.studio=", StringComparison.Ordinal)));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Studio")).StatusCode);
    }

    [Fact]
    public async Task The_right_password_opens_the_studio_with_its_menu_and_its_pages()
    {
        var client = Client(StudioOn());

        var response = await SignIn(client);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Studio", Local(response));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("matdesign.studio=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        foreach (var url in new[] { "/Studio", "/Studio/Activity" })
        {
            var page = await client.GetAsync(url);
            var html = await page.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Single(Heading().Matches(html));
            Assert.Contains("class=\"mat-nav\"", html);
            Assert.Contains("class=\"mat-crumbs\"", html);
            Assert.Contains("data-nav-panel=\"studio\"", html);
            Assert.Contains("aria-checked=\"true\" data-nav-set=\"studio\"", html);
            Assert.Contains("Signed in to the Studio", html);
            Assert.DoesNotMatch(@"\sstyle\s*=", html);
        }
    }

    [Fact]
    public async Task Only_a_local_address_inside_the_studio_is_followed_after_the_sign_in()
    {
        foreach (var target in new[] { "https://evil.example/", "//evil.example/", "/Rules", "javascript:alert(1)" })
        {
            var client = Client(StudioOn());
            var response = await SignIn(client, returnUrl: target);
            Assert.Equal("/Studio", Local(response));
        }
    }

    [Fact]
    public async Task Signing_out_is_a_post_with_a_token_and_ends_the_session()
    {
        var client = Client(StudioOn());
        await SignIn(client);

        var page = await Html(client, "/Studio");
        Assert.Contains("action=\"/Studio/Logout\"", page);
        var token = Token().Matches(page).Last().Groups[1].Value;

        // without the token nothing happens
        var refused = await client.PostAsync("/Studio/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Studio")).StatusCode);

        var done = await client.PostAsync("/Studio/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Equal("/", Local(done));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Studio")).StatusCode);

        // a link to the page only leads back
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Studio/Logout")).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_client_out_even_for_the_right_one()
    {
        var client = Client(StudioOn());
        for (var i = 0; i < 5; i++)
        {
            await SignIn(client, "wrong " + i);
        }

        var response = await SignIn(client);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Too many attempts", html);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Studio")).StatusCode);
    }

    [Fact]
    public void A_session_of_another_password_is_not_the_same_session()
    {
        StudioService Service(string password)
        {
            var directory = Path.Combine(Path.GetTempPath(), "matdesign-studio-" + Guid.NewGuid().ToString("N"));
            _directories.Add(directory);
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["MATDESIGN_DATA"] = directory, ["MATDESIGN_ADMIN_PASSWORD"] = password })
                .Build();
            return new StudioService(configuration);
        }

        var one = Service("one");
        var two = Service("two");

        Assert.NotEqual(one.Stamp, two.Stamp);
        Assert.True(one.SignsIn("admin", "one"));
        Assert.False(one.SignsIn("admin", "two"));
        Assert.False(one.SignsIn("someone", "one"));
        Assert.False(one.SignsIn("admin", null));
        Assert.False(one.SignsIn("admin", string.Empty));
    }

    [Fact]
    public async Task The_activity_lists_what_was_saved_and_its_tools_narrow_it_down()
    {
        var factory = StudioOn();
        var client = Client(factory);
        await SignIn(client);
        var store = factory.Services.GetRequiredService<StudioService>().Store;
        store.Save("apps", "matx", new { name = "MatX" }, "admin", "first sketch");
        store.Save("rules", "X-01", new { title = "A rule" }, "admin", "for the test");
        store.Delete("apps", "matx", "admin", "not needed");

        var all = await Html(client, "/Studio/Activity");
        Assert.Contains("matx", all);
        Assert.Contains("X-01", all);
        Assert.Contains("not needed", all);
        Assert.Contains("class=\"mat-toolbar\"", all);
        foreach (var dialog in new[] { "activity-search", "activity-filter", "activity-sort" })
        {
            Assert.Contains($"id=\"{dialog}\"", all);
        }

        var onlyRules = await Html(client, "/Studio/Activity?kind=rules");
        Assert.Contains("X-01", onlyRules);
        Assert.DoesNotContain("not needed", onlyRules);

        var deleted = await Html(client, "/Studio/Activity?change=Deleted");
        Assert.Contains("not needed", deleted);
        Assert.DoesNotContain("for the test", deleted);

        var none = await Html(client, "/Studio/Activity?q=zzz");
        Assert.Contains("No change matches", none);

        var overview = await Html(client, "/Studio");
        Assert.Contains("rules: 1 item, 1 revision", overview);
        Assert.Contains("apps: 0 items, 2 revisions", overview);
    }

    [Fact]
    public async Task The_plain_site_is_unchanged_without_the_studio()
    {
        var client = Client(_plain);

        var html = await Html(client, "/");
        Assert.Contains("href=\"/Studio/Login\"", html);
        Assert.Contains("The Studio is off", html);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}
