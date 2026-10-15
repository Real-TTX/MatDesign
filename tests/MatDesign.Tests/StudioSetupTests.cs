using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MatDesign.Tests;

/// <summary>The administrator account itself: a hash, never the password; what the setup assistant accepts; a file that is created once and never overwritten.</summary>
public sealed class AdminAccountTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "matdesign-admin-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void The_password_is_checked_against_a_salted_hash_and_the_name_ignores_case()
    {
        var account = AdminAccount.Create("Matthias", "a long sentence");

        Assert.True(account.Verify("a long sentence"));
        Assert.False(account.Verify("a long sentence "));
        Assert.False(account.Verify("A long sentence"));
        Assert.False(account.Verify(null));
        Assert.False(account.Verify(string.Empty));
        Assert.True(account.SignsInWith("matthias", "a long sentence"));
        Assert.True(account.SignsInWith(" MATTHIAS ", "a long sentence"));
        Assert.False(account.SignsInWith("someone", "a long sentence"));
        Assert.False(account.SignsInWith("matthias", "wrong"));

        var other = AdminAccount.Create("Matthias", "a long sentence");
        Assert.NotEqual(account.Salt, other.Salt);
        Assert.NotEqual(account.Hash, other.Hash);
        Assert.DoesNotContain("long sentence", account.Hash + account.Salt);
        Assert.Equal(AdminAccount.DefaultIterations, account.Iterations);
    }

    [Fact]
    public void A_session_follows_the_password()
    {
        var one = AdminAccount.Create("admin", "first password");
        var two = AdminAccount.Create("admin", "second password");

        Assert.Equal(16, one.Stamp.Length);
        Assert.Equal(one.Stamp, one.Stamp);
        Assert.NotEqual(one.Stamp, two.Stamp);
    }

    [Theory]
    [InlineData("admin", "long enough password", "long enough password", "")]
    [InlineData("Matthias.S-1", "long enough password", "long enough password", "")]
    [InlineData("", "long enough password", "long enough password", "Name")]
    [InlineData("a", "long enough password", "long enough password", "Name")]
    [InlineData("two words", "long enough password", "long enough password", "Name")]
    [InlineData("-admin", "long enough password", "long enough password", "Name")]
    [InlineData("admin", "short", "short", "Password")]
    [InlineData("admin", "", "", "Password")]
    [InlineData("administrator", "Administrator", "Administrator", "Password")]
    [InlineData("admin", "long enough password", "another long password", "Repeat")]
    public void The_assistant_accepts_a_name_and_a_password_that_is_long_and_typed_twice(string name, string password, string repeat, string field)
    {
        var problems = AdminAccount.Validate(name, password, repeat);

        if (field.Length == 0)
        {
            Assert.Empty(problems);
        }
        else
        {
            Assert.True(problems.ContainsKey(field), $"expected a problem with {field}, got: {string.Join(", ", problems.Keys)}");
        }
    }

    [Fact]
    public void The_file_is_written_once_and_read_back_and_a_second_write_does_not_replace_it()
    {
        Assert.Null(AdminAccount.Read(_directory));

        var first = AdminAccount.Create("admin", "the first password");
        Assert.True(first.TryWrite(_directory));
        var again = AdminAccount.Create("intruder", "the second password");
        Assert.False(again.TryWrite(_directory));

        var read = AdminAccount.Read(_directory);
        Assert.NotNull(read);
        Assert.Equal("admin", read.Name);
        Assert.True(read.Verify("the first password"));
        Assert.False(read.Verify("the second password"));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.DoesNotContain("the first password", File.ReadAllText(AdminAccount.PathIn(_directory)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"Name\":\"admin\",\"Salt\":\"!!\",\"Hash\":\"!!\",\"Iterations\":1,\"Created\":\"2026-01-01T00:00:00Z\"}")]
    public void A_file_that_cannot_be_read_is_an_error_and_never_no_administrator(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(AdminAccount.PathIn(_directory), content);

        Assert.Throws<InvalidDataException>(() => AdminAccount.Read(_directory));
    }
}

/// <summary>The setup assistant as a visitor meets it: the first visitor of a Studio without an administrator creates the account and is in; afterwards the page is gone.</summary>
public sealed partial class StudioSetupTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string Name = "matthias";
    private const string Password = "a sentence is a password";

    private readonly WebApplicationFactory<Program> _plain;
    private readonly List<string> _directories = [];

    public StudioSetupTests(WebApplicationFactory<Program> factory) => _plain = factory;

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

    private string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "matdesign-setup-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        return directory;
    }

    /// <summary>A Studio on a data directory, without any administrator unless the test asks for the password of the environment.</summary>
    private WebApplicationFactory<Program> Studio(string? directory = null, string? environmentPassword = null)
    {
        directory ??= NewDirectory();
        return _plain.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_DATA", directory);
            if (environmentPassword is not null)
            {
                builder.UseSetting("MATDESIGN_ADMIN_PASSWORD", environmentPassword);
            }
        });
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string Local(HttpResponseMessage response)
        => response.Headers.Location!.IsAbsoluteUri ? response.Headers.Location.PathAndQuery : response.Headers.Location.OriginalString;

    private static async Task<string> Html(HttpClient client, string url) => await (await client.GetAsync(url)).Content.ReadAsStringAsync();

    private static async Task<HttpResponseMessage> Setup(HttpClient client, string name = Name, string password = Password, string? repeat = null)
    {
        var page = await Html(client, "/Studio/Setup");
        return await client.PostAsync("/Studio/Setup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Password"] = password,
            ["Repeat"] = repeat ?? password,
            ["__RequestVerificationToken"] = Token().Match(page).Groups[1].Value,
        }));
    }

    private static async Task<HttpResponseMessage> SignIn(HttpClient client, string name, string password)
    {
        var page = await Html(client, "/Studio/Login");
        return await client.PostAsync("/Studio/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Password"] = password,
            ["__RequestVerificationToken"] = Token().Match(page).Groups[1].Value,
        }));
    }

    [Fact]
    public async Task Every_way_into_a_studio_without_an_administrator_ends_at_the_setup()
    {
        var client = Client(Studio());

        var studio = await client.GetAsync("/Studio/Activity");
        Assert.Equal(HttpStatusCode.Redirect, studio.StatusCode);
        Assert.StartsWith("/Studio/Login", Local(studio));

        var login = await client.GetAsync("/Studio/Login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Studio/Setup", Local(login));
    }

    [Fact]
    public async Task The_setup_page_asks_for_a_name_and_a_password_twice_and_follows_the_shell_rules()
    {
        var client = Client(Studio());

        var response = await client.GetAsync("/Studio/Setup");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(Heading().Matches(html));
        Assert.Contains("Set up the Studio", html);
        foreach (var field in new[] { "Name", "Password", "Repeat" })
        {
            Assert.Contains($"id=\"{field}\"", html);
            Assert.Contains($"<label class=\"mat-label\" for=\"{field}\"", html);
        }

        Assert.Contains("value=\"admin\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("autocomplete=\"new-password\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.DoesNotMatch(@"\sstyle\s*=", html);
    }

    [Fact]
    public async Task The_site_offers_the_setup_to_a_visitor()
    {
        var html = await Html(Client(Studio()), "/");

        Assert.Contains("href=\"/Studio/Setup\"", html);
        Assert.Contains("Set up the Studio", html);
        Assert.DoesNotContain("href=\"/Studio/Login\"", html);
    }

    [Theory]
    [InlineData("matthias", "short", "short", "At least 10 characters")]
    [InlineData("matthias", "a sentence is a password", "another sentence entirely", "The two passwords are not the same.")]
    [InlineData("two words", "a sentence is a password", "a sentence is a password", "Use 2 to 32 letters")]
    public async Task Input_that_does_not_fit_is_refused_at_the_field_and_nothing_is_created(string name, string password, string repeat, string message)
    {
        var directory = NewDirectory();
        var client = Client(Studio(directory));

        var response = await Setup(client, name, password, repeat);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, html);
        Assert.Contains("is-invalid", html);
        Assert.DoesNotContain($"value=\"{password}", html);
        Assert.False(File.Exists(UserStore.PathIn(directory)));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("matdesign.studio=", StringComparison.Ordinal)));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Studio/Setup")).StatusCode);
    }

    [Fact]
    public async Task The_assistant_creates_the_administrator_signs_them_in_and_leads_into_the_studio()
    {
        var directory = NewDirectory();
        var client = Client(Studio(directory));

        var response = await Setup(client);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Studio", Local(response));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("matdesign.studio=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var studio = await client.GetAsync("/Studio");
        var html = await studio.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, studio.StatusCode);
        Assert.Contains("Signed in to the Studio", html);
        Assert.Contains("The Studio is set up. You are signed in as matthias, the administrator.", html);

        var file = await File.ReadAllTextAsync(UserStore.PathIn(directory));
        Assert.Contains("matthias", file);
        Assert.DoesNotContain(Password, file);
    }

    [Fact]
    public async Task What_the_administrator_saves_carries_their_name()
    {
        var factory = Studio();
        var client = Client(factory);
        await Setup(client, "Matthias.S");

        var studio = (StudioService)factory.Services.GetService(typeof(StudioService))!;
        Assert.Equal("Matthias.S", studio.Users.ByName("matthias.s")!.Name);
    }

    [Fact]
    public async Task The_assistant_is_gone_once_there_is_an_administrator_and_nobody_can_take_over()
    {
        var factory = Studio();
        var owner = Client(factory);
        await Setup(owner);

        var stranger = Client(factory);
        var page = await stranger.GetAsync("/Studio/Setup");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/Studio/Login", Local(page));

        // a post without the page, as a script would send it, changes nothing either
        var post = await stranger.PostAsync("/Studio/Setup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "intruder",
            ["Password"] = "an intruder password",
            ["Repeat"] = "an intruder password",
        }));
        Assert.NotEqual(HttpStatusCode.OK, post.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await stranger.GetAsync("/Studio")).StatusCode);

        var intruder = await SignIn(stranger, "intruder", "an intruder password");
        Assert.Equal(HttpStatusCode.OK, intruder.StatusCode);
        Assert.Contains("The name or the password is not right.", await intruder.Content.ReadAsStringAsync());

        var rightful = await SignIn(Client(factory), Name, Password);
        Assert.Equal(HttpStatusCode.Redirect, rightful.StatusCode);
        Assert.Equal("/Studio", Local(rightful));
    }

    [Fact]
    public async Task The_administrator_survives_a_restart_and_signs_in_by_name_and_password()
    {
        var directory = NewDirectory();
        await Setup(Client(Studio(directory)));

        var restarted = Client(Studio(directory));
        Assert.Equal(HttpStatusCode.OK, (await restarted.GetAsync("/Studio/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await restarted.GetAsync("/Studio/Setup")).StatusCode);

        var wrongName = await SignIn(Client(Studio(directory)), "someone", Password);
        Assert.Equal(HttpStatusCode.OK, wrongName.StatusCode);
        Assert.Contains("The name or the password is not right.", await wrongName.Content.ReadAsStringAsync());

        var wrongPassword = await SignIn(Client(Studio(directory)), Name, "a sentence is not this password");
        Assert.Equal(HttpStatusCode.OK, wrongPassword.StatusCode);

        var right = await SignIn(Client(Studio(directory)), "MATTHIAS", Password);
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
        Assert.Equal("/Studio", Local(right));
    }

    [Fact]
    public async Task The_password_of_the_environment_skips_the_assistant_and_writes_nothing()
    {
        var directory = NewDirectory();
        var client = Client(Studio(directory, environmentPassword: "set from outside"));

        var login = await client.GetAsync("/Studio/Login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("type=\"password\"", await login.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Studio/Setup")).StatusCode);
        Assert.False(File.Exists(UserStore.PathIn(directory)));
        Assert.Equal(HttpStatusCode.Redirect, (await SignIn(client, "admin", "set from outside")).StatusCode);
    }

    [Fact]
    public void Two_visitors_at_the_same_moment_make_one_administrator()
    {
        var directory = NewDirectory();
        var studio = new StudioService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MATDESIGN_DATA"] = directory }).Build());
        Assert.True(studio.NeedsSetup);

        var results = new SetupResult[8];
        Parallel.For(0, results.Length, i => results[i] = studio.Setup($"visitor{i}", $"password number {i}", $"password number {i}"));

        Assert.Single(results, r => r.Done);
        Assert.False(studio.NeedsSetup);
        var winner = studio.Users.All.Single();
        Assert.StartsWith("visitor", winner.Name);

        Assert.Single(Directory.GetFiles(directory, "users.json*"));
    }

    [Fact]
    public async Task A_broken_administrator_file_switches_the_studio_off_instead_of_offering_the_assistant_to_a_stranger()
    {
        var directory = NewDirectory();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(AdminAccount.PathIn(directory), "garbage");
        var client = Client(Studio(directory));

        var html = await Html(client, "/Studio/Login");
        Assert.Contains("The Studio is off", html);
        Assert.Contains("is not a valid administrator file", html);
        var setup = await Html(client, "/Studio/Setup");
        Assert.Contains("The Studio is off", setup);
        Assert.DoesNotContain("type=\"password\"", setup);
    }
}
