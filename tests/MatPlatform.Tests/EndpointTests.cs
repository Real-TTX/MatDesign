using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace MatPlatform.Tests;

/// <summary>The endpoints of the contract (docs/PLATFORM.md) as <c>MapMatPlatform</c> serves them for the demo app: shapes, status codes, access.</summary>
public sealed class EndpointTests : IAsyncLifetime
{
    private static readonly string[] Protected = ["/api/state", "/api/state/groups", "/api/configure"];

    private readonly DemoStore _store = new();
    private WebApplication _app = null!;

    public async Task InitializeAsync() => _app = await DemoHost.StartAsync(store: _store);

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task The_definition_is_anonymous_with_an_etag_and_the_access_filled_in()
    {
        var client = _app.GetTestClient();
        var response = await client.GetAsync("/api");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api");
        request.Headers.TryAddWithoutValidation("If-None-Match", response.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(request)).StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(
            ["contract", "app", "auth", "pages", "state", "configure", "actions", "provides", "consumes", "roles", "endpoints"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, root.GetProperty("contract").GetInt32());
        Assert.Equal("demoapp", root.GetProperty("app").GetProperty("id").GetString());
        Assert.True(root.GetProperty("auth").GetProperty("apiKey").GetBoolean());
        Assert.False(root.GetProperty("auth").TryGetProperty("identity", out _));
        Assert.Equal(["/api/health", "/api/state", "/api/configure", "/api/actions"], root.GetProperty("endpoints").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(JsonValueKind.Array, root.GetProperty("consumes").ValueKind);
        Assert.Equal("diskMax", root.GetProperty("state")[2].GetProperty("max").GetString());
        Assert.True(root.GetProperty("configure")[4].GetProperty("showIf").GetProperty("smtpEnabled").GetBoolean());
    }

    [Fact]
    public async Task The_definition_follows_accept_language_and_keeps_umlauts()
    {
        var client = _app.GetTestClient();
        async Task<string> Get(string language)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api");
            request.Headers.TryAddWithoutValidation("Accept-Language", language);
            return await (await client.SendAsync(request)).Content.ReadAsStringAsync();
        }

        var german = await Get("de-DE,de;q=0.9");
        Assert.Contains("\"label\":\"Übersicht\"", german);
        Assert.Contains("\"label\":\"Overview\"", await Get("en-US,en;q=0.9"));
        // Another language: the app's own.
        Assert.Contains("\"label\":\"Übersicht\"", await Get("fr-FR,en;q=0.5"));
        Assert.Contains("\"label\":\"Übersicht\"", await _app.GetTestClient().GetStringAsync("/api"));
    }

    [Fact]
    public async Task Health_is_anonymous_and_the_worst_check()
    {
        var response = await _app.GetTestClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var root = doc.RootElement;
            Assert.Equal(["status", "app", "version", "time", "checks"], root.EnumerateObject().Select(p => p.Name));
            Assert.Equal("degraded", root.GetProperty("status").GetString());
            Assert.Equal("demoapp", root.GetProperty("app").GetString());
            Assert.Equal("1.2.3-20261016", root.GetProperty("version").GetString());
            Assert.False(root.GetProperty("checks")[0].TryGetProperty("message", out _));
        }

        _store.Down = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _app.GetTestClient().GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task Everything_else_needs_the_key()
    {
        var anonymous = _app.GetTestClient();
        foreach (var path in Protected)
        {
            var response = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/configure", new { values = new { currency = "CHF" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/actions/refresh", null)).StatusCode);
        Assert.Empty(_store.Ran);
        Assert.Equal("EUR", _store.Values["currency"]);

        var wrong = _app.GetTestClient();
        wrong.DefaultRequestHeaders.Add("X-Api-Key", DemoHost.ApiKey + "x");
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync("/api/state")).StatusCode);

        var twice = _app.GetTestClient();
        twice.DefaultRequestHeaders.Add("X-Api-Key", [DemoHost.ApiKey, DemoHost.ApiKey]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await twice.GetAsync("/api/state")).StatusCode);

        // Without MatIdentity a bearer token is no way in.
        var bearer = _app.GetTestClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc.def.ghi");
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/state")).StatusCode);
    }

    [Fact]
    public async Task State_answers_every_value_and_writes_null()
    {
        var api = _app.Api();
        var text = await api.GetStringAsync("/api/state");
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(["time", "values"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        var values = doc.RootElement.GetProperty("values");
        Assert.Equal(12, values.GetProperty("groups").GetProperty("value").GetInt32());
        Assert.Equal(["value"], values.GetProperty("groups").EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Null, values.GetProperty("lastMail").GetProperty("value").ValueKind);
        Assert.Equal("warn", values.GetProperty("disk").GetProperty("level").GetString());
        Assert.Equal("Fast voll", values.GetProperty("disk").GetProperty("text").GetString());
        Assert.True(DateTimeOffset.TryParse(doc.RootElement.GetProperty("time").GetString(), out _));
    }

    [Fact]
    public async Task One_value_by_key_and_404_for_an_unknown_key()
    {
        var api = _app.Api();
        Assert.Equal("{\"key\":\"groups\",\"value\":12}", await api.GetStringAsync("/api/state/groups"));
        Assert.Equal("{\"key\":\"lastMail\",\"value\":null}", await api.GetStringAsync("/api/state/lastMail"));

        var unknown = await api.GetAsync("/api/state/nothing");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var doc = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("nothing", out _));
    }

    [Fact]
    public async Task A_secret_is_never_sent_back()
    {
        var api = _app.Api();
        var text = await api.GetStringAsync("/api/configure");
        Assert.DoesNotContain(DemoApp.Password, text);
        using var doc = JsonDocument.Parse(text);
        var values = doc.RootElement.GetProperty("values");
        Assert.Equal("{\"isSet\":true}", values.GetProperty("smtpPassword").GetRawText());
        Assert.Equal("https://demo.example.com", values.GetProperty("publicUrl").GetString());

        // Saving answers the values too, again without the secret; "" clears it.
        var saved = await api.PutAsJsonAsync("/api/configure", new { values = new { smtpPassword = "" } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("\"smtpPassword\":{\"isSet\":false}", await saved.Content.ReadAsStringAsync());

        await api.PutAsJsonAsync("/api/configure", new { values = new { smtpPassword = "another-secret" } });
        Assert.DoesNotContain("another-secret", await api.GetStringAsync("/api/configure"));
        Assert.DoesNotContain("another-secret", await api.GetStringAsync("/api"));
    }

    [Fact]
    public async Task Configure_saves_the_changed_keys_with_the_caller()
    {
        var response = await _app.Api().PutAsJsonAsync("/api/configure", new { values = new { currency = "CHF", retries = 5 } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CHF", doc.RootElement.GetProperty("values").GetProperty("currency").GetString());
        Assert.Equal("CHF", _store.Values["currency"]);
        Assert.Equal("https://demo.example.com", _store.Values["publicUrl"]);
        Assert.Equal("api-key", _store.LastCaller);
    }

    [Theory]
    [InlineData("{\"values\":{\"nothing\":1}}", "nothing")]
    [InlineData("{\"values\":{\"currency\":\"USD\"}}", "currency")]
    [InlineData("{\"values\":{\"currency\":1}}", "currency")]
    [InlineData("{\"values\":{\"retries\":9}}", "retries")]
    [InlineData("{\"values\":{\"retries\":\"3\"}}", "retries")]
    [InlineData("{\"values\":{\"smtpEnabled\":\"yes\"}}", "smtpEnabled")]
    [InlineData("{\"values\":{\"publicUrl\":\"\"}}", "publicUrl")]
    [InlineData("{\"values\":{\"publicUrl\":\"ftp://x\"}}", "publicUrl")]
    [InlineData("{\"values\":{\"publicUrl\":\"https://refused.example.com\"}}", "publicUrl")]
    [InlineData("{\"values\":{\"smtpEnabled\":true}}", "smtpHost")]
    [InlineData("{\"values\":{\"smtpEnabled\":true,\"smtpHost\":\"Mail Server\"}}", "smtpHost")]
    public async Task A_wrong_value_is_400_with_its_key_and_nothing_is_saved(string body, string key)
    {
        var before = new Dictionary<string, object?>(_store.Values);
        var response = await _app.Api().PutAsync("/api/configure", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty(key, out _), await response.Content.ReadAsStringAsync());
        Assert.Equal(before, _store.Values);
    }

    [Fact]
    public async Task A_field_hidden_by_show_if_is_not_required_and_a_shown_one_is()
    {
        var api = _app.Api();
        Assert.Equal(HttpStatusCode.OK, (await api.PutAsJsonAsync("/api/configure", new { values = new { smtpHost = "" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PutAsJsonAsync("/api/configure", new { values = new { smtpEnabled = true, smtpHost = "mail.example.com" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsJsonAsync("/api/configure", new { values = new { smtpHost = "" } })).StatusCode);
    }

    [Fact]
    public async Task A_body_without_values_is_400()
    {
        var api = _app.Api();
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsJsonAsync("/api/configure", new { currency = "CHF" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsync("/api/configure", new StringContent("not json", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task Messages_follow_accept_language()
    {
        var api = _app.Api();
        api.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        var response = await api.PutAsJsonAsync("/api/configure", new { values = new { retries = 9 } });
        Assert.Contains("between 1 and 5", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Actions_run_409_when_they_cannot_and_404_for_unknown_and_link_actions()
    {
        var api = _app.Api();
        var refresh = await api.PostAsync("/api/actions/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal("{\"ok\":true,\"message\":\"3 Feeds aktualisiert\"}", await refresh.Content.ReadAsStringAsync());

        var busy = await api.PostAsync("/api/actions/busy", null);
        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode);
        Assert.Equal("{\"ok\":false,\"message\":\"Not now\"}", await busy.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/actions/nothing", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/actions/openAdmin", null)).StatusCode);
        Assert.Equal(["refresh", "busy"], _store.Ran);
    }
}

/// <summary>Without <c>MAT_API_KEY</c> the key is off: every key is refused, and the definition says so.</summary>
public sealed class WithoutKeyTests
{
    [Fact]
    public async Task Every_protected_endpoint_is_401()
    {
        await using var app = await DemoHost.StartAsync(withKey: false);
        var client = app.Api();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/configure")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/actions/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api"));
        Assert.False(doc.RootElement.GetProperty("auth").GetProperty("apiKey").GetBoolean());
    }
}

/// <summary>With <c>CheckValues = false</c> the app checks every value itself; the platform passes them on.</summary>
public sealed class AppChecksValuesTests
{
    [Fact]
    public async Task Values_reach_the_app_unchecked()
    {
        var store = new DemoStore();
        await using var app = await DemoHost.StartAsync(configure: o => o.CheckValues = false, store: store);

        var response = await app.Api().PutAsJsonAsync("/api/configure", new { values = new { retries = 9 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(9d, store.Values["retries"]);
    }
}
