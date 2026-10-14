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
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
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
