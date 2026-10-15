using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The whole site in memory with the Studio on (a data directory of its own and a password), a client that has signed in, and the small helpers to post a form.</summary>
public sealed partial class StudioHarness : IDisposable
{
    public const string Password = "correct horse battery staple";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "matdesign-studio-" + Guid.NewGuid().ToString("N"));
    private readonly bool _owns = true;

    /// <summary>A site that the test configured itself (a Studio that was set up by the assistant, with no password in the environment): the helpers are the same, and its directory is the test's to delete.</summary>
    public StudioHarness(WebApplicationFactory<Program> configured, string directory)
    {
        _directory = directory;
        _owns = false;
        Factory = configured;
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public StudioHarness(WebApplicationFactory<Program> factory, IReadOnlyDictionary<string, string>? settings = null)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_DATA", _directory);
            builder.UseSetting("MATDESIGN_ADMIN_PASSWORD", Password);
            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }

            // the sender of a request, for the tests of the lock-out (X-Test-Sender)
            builder.ConfigureServices(services => services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, TestSenderStartupFilter>());
        });
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient Client { get; }

    public StudioStore Store => Factory.Services.GetRequiredService<StudioService>().Store;

    public DesignData Design => Factory.Services.GetRequiredService<DesignData>();

    public DesignChecks Checks => Factory.Services.GetRequiredService<DesignChecks>();

    public ReleaseService Releases => Factory.Services.GetRequiredService<ReleaseService>();

    public string DataDirectory => Factory.Services.GetRequiredService<StudioService>().DataDirectory;

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex Token();

    public void Dispose()
    {
        if (_owns && Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    public StudioService Service => Factory.Services.GetRequiredService<StudioService>();

    /// <summary>The password of the people the tests create: long enough, not common.</summary>
    public const string PersonPassword = "a fine long password 1";

    /// <summary>Another client with a cookie jar of its own: another browser.</summary>
    public HttpClient NewClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Creates a person who has no password to change (as after their first sign-in), straight in the store.</summary>
    public StudioUser AddUser(string name, string role, string password = PersonPassword, bool mustChange = false)
    {
        var result = Service.Users.Create(name, role, password, "test", mustChange);
        Assert.True(result.Ok, result.Error);
        return result.User!;
    }

    /// <summary>Signs a client in as a person through the real form; returns the response (a redirect when it worked).</summary>
    public async Task<HttpResponseMessage> SignInAs(HttpClient client, string name, string password = PersonPassword)
    {
        var page = await (await client.GetAsync("/Studio/Login")).Content.ReadAsStringAsync();
        return await client.PostAsync("/Studio/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Password"] = password,
            ["__RequestVerificationToken"] = Token().Match(page).Groups[1].Value,
        }));
    }

    /// <summary>A client that is signed in as a person.</summary>
    public async Task<HttpClient> ClientOf(string name, string password = PersonPassword)
    {
        var client = NewClient();
        var response = await SignInAs(client, name, password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>The anti-forgery token of this client: the account page is open to every role (and to a person who must change the password).</summary>
    public async Task<string> TokenOf(HttpClient client)
        => Token().Match(await (await client.GetAsync("/Studio/Account")).Content.ReadAsStringAsync()).Groups[1].Value;

    /// <summary>Posts a form as a client, with its token.</summary>
    public async Task<HttpResponseMessage> PostAs(HttpClient client, string url, IEnumerable<KeyValuePair<string, string>>? fields = null)
        => await client.PostAsync(url, new FormUrlEncodedContent((fields ?? []).Append(new("__RequestVerificationToken", await TokenOf(client)))));

    /// <summary>Every file of the data directory with its content hash (the keys of the cookies aside): what a refused change must leave as it was.</summary>
    public string Snapshot()
    {
        var root = DataDirectory;
        var lines = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "keys" + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !f.EndsWith(".tmp", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f) + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)))[..16])
            .Order(StringComparer.Ordinal);
        return string.Join("\n", lines);
    }

    public async Task<string> Html(string url) => await (await Client.GetAsync(url)).Content.ReadAsStringAsync();

    public async Task<string> Get(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task SignIn()
    {
        var page = await Html("/Studio/Login");
        var response = await Client.PostAsync("/Studio/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "admin",
            ["Password"] = Password,
            ["__RequestVerificationToken"] = Token().Match(page).Groups[1].Value,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>Posts a form like the browser does: the token comes from the page that holds the form.</summary>
    public Task<HttpResponseMessage> Post(string formPage, string postUrl, IDictionary<string, string> fields)
        => Post(formPage, postUrl, fields.AsEnumerable());

    /// <summary>The same for a form in which a name appears more than once (a checkbox and its hidden false).</summary>
    public async Task<HttpResponseMessage> Post(string formPage, string postUrl, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var page = await Html(formPage);
        var all = fields.Append(new("__RequestVerificationToken", Token().Match(page).Groups[1].Value));
        return await Client.PostAsync(postUrl, new FormUrlEncodedContent(all));
    }

    /// <summary>The fields of an app form, valid unless a test changes them.</summary>
    public static Dictionary<string, string> AppForm(string id = "matx", string original = "") => new()
    {
        ["Input.Original"] = original,
        ["Input.Id"] = id,
        ["Input.Name"] = "MatX",
        ["Input.Tagline"] = "An app for the tests",
        ["Input.Category"] = "Productivity",
        ["Input.Accent"] = "#0f766e",
        ["Input.Glyph"] = "apps",
        ["Input.Repo"] = "",
        ["Input.Port"] = "",
        ["Input.OnAccent"] = "",
        ["Input.AccentText"] = "",
        ["Input.Notes"] = "",
    };
}
