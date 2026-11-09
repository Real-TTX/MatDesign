using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using MatPlatform;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MatPlatform.Tests;

/// <summary>The bindings of 2.0 (docs/PLATFORM.md "Bindings"): a provider of a key capability, a consumer, the broker endpoints, the Connections area.</summary>
public sealed class BindingTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), "matplatform-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>A provider of <c>files.store</c> (a key capability) and a consumer of <c>office.edit</c> and <c>mail.smtp</c>, with contributions.</summary>
    internal static void Describe(MatPlatformBuilder o) => o
        .App("demoapp", "Demo", "A demo", "2.0.0-20261109", "/icon.svg", "#2f6feb", "/home")
        .Tile("groups", "Groups")
        .Tile(new MatStateItem("disk", "Storage", "meter") { Unit = "bytes", Max = JsonSerializer.SerializeToElement("diskMax") })
        .PostAction("refresh", "Refresh", "refresh")
        .Provides("files.store", label: "Keeps files for")
        .Consumes("office.edit", optional: true, label: "Opens office files with")
        .Consumes("mail.smtp", optional: true, parameters: new Dictionary<string, string> { ["sender"] = "demo" })
        .Launcher("home", "Demo", "/icon.svg", "/home", badge: "groups")
        .Launcher("files", "Files", "/img/files.svg", "/files", audience: MatAudience.Admin)
        .Widget(new MatWidget("groups", "Groups", MatWidgetKinds.Stat) { Items = ["groups"], Url = "/groups", Refresh = 60 })
        .Widget(new MatWidget("disk", "Storage", MatWidgetKinds.Meter, "medium") { Items = ["disk"], Audience = MatAudience.Admin })
        .Widget(new MatWidget("recent", "Recent files", MatWidgetKinds.List, "wide") { Audience = MatAudience.Person })
        .QuickAction("home", "New group", "plus", url: "/groups/new")
        .QuickAction("home", "Refresh", "refresh", action: "refresh");

    public void Dispose()
    {
        if (Directory.Exists(_data))
        {
            Directory.Delete(_data, recursive: true);
        }
    }

    private Task<WebApplication> StartAsync(bool data = true, IDictionary<string, string?>? configuration = null, bool admin = true, Action<IServiceCollection>? services = null,
        Action<MatPlatformBuilder>? describe = null)
        => DemoHost.StartAsync(
            describe: describe ?? Describe,
            configure: o =>
            {
                o.DataDirectory = data ? _data : null;
                o.ConnectionsAccess = _ => Task.FromResult(admin);
            },
            configuration: configuration,
            services: services,
            endpoints: app =>
            {
                // An endpoint of the capability, behind the key of a binding.
                app.MapGroup("/api/provides/files.store").AllowAnonymous().AddEndpointFilter(MatBindingAccess.Require("files.store"))
                    .MapGet("/ping", (HttpContext http) => "pong " + MatBindingAccess.ConsumerOf(http));
                app.MapGet("/token", (HttpContext http, IAntiforgery antiforgery) => antiforgery.GetAndStoreTokens(http).RequestToken);
            });

    private static StringContent Bind(object body) => new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    private static object Body(bool renew = false, string managedBy = "mattiverse") => new
    {
        version = 1,
        managedBy,
        consumer = new { name = "MatDrive", url = "https://drive.example.com", urls = new[] { "https://drive.example.com", "http://nas:4810/" }, internalUrl = "http://mattiverse-matdrive-web:4810" },
        @params = new Dictionary<string, string> { ["sender"] = "drive" },
        provider = new { url = "https://demo.example.com", internalUrl = "http://mattiverse-demo-web:8080" },
        renew,
    };

    [Fact]
    public async Task A_broker_binds_a_consumer_of_a_key_capability_and_the_key_opens_the_capability()
    {
        await using var app = await StartAsync();
        var api = app.Api();

        var made = await api.PutAsync("/api/bindings/files.store/matdrive", Bind(Body()));
        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        var variables = (await made.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("variables");
        Assert.Equal("https://demo.example.com", variables.GetProperty("MAT_FILES_STORE_URL").GetString());
        Assert.Equal("http://mattiverse-demo-web:8080", variables.GetProperty("MAT_FILES_STORE_INTERNAL_URL").GetString());
        var key = variables.GetProperty("MAT_FILES_STORE_KEY").GetString()!;
        Assert.StartsWith(MatBindingStore.KeyStart, key, StringComparison.Ordinal);

        // The key opens the capability and names the consumer; another key does not.
        var client = app.GetTestClient();
        using (var ping = new HttpRequestMessage(HttpMethod.Get, "/api/provides/files.store/ping"))
        {
            ping.Headers.Authorization = new("Bearer", key);
            Assert.Equal("pong matdrive", await (await client.SendAsync(ping)).Content.ReadAsStringAsync());
        }

        using (var wrong = new HttpRequestMessage(HttpMethod.Get, "/api/provides/files.store/ping"))
        {
            wrong.Headers.Authorization = new("Bearer", "mat_not-the-key");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrong)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/provides/files.store/ping")).StatusCode);

        // The same request again is the same binding: the key is not handed out again ...
        var again = (await (await api.PutAsync("/api/bindings/files.store/matdrive", Bind(Body()))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("variables");
        Assert.True(again.GetProperty("MAT_FILES_STORE_KEY").GetProperty("isSet").GetBoolean());

        // ... and is listed once, with who made it.
        var list = await api.GetFromJsonAsync<JsonElement>("/api/bindings");
        var binding = Assert.Single(list.GetProperty("bindings").EnumerateArray());
        Assert.Equal("matdrive", binding.GetProperty("consumer").GetString());
        Assert.Equal("MatDrive", binding.GetProperty("name").GetString());
        Assert.Equal("mattiverse", binding.GetProperty("managedBy").GetString());

        // A new key when asked: the old one stops working.
        var renewed = (await (await api.PutAsync("/api/bindings/files.store/matdrive", Bind(Body(renew: true)))).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("variables").GetProperty("MAT_FILES_STORE_KEY").GetString()!;
        Assert.NotEqual(key, renewed);
        using (var old = new HttpRequestMessage(HttpMethod.Get, "/api/provides/files.store/ping"))
        {
            old.Headers.Authorization = new("Bearer", key);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(old)).StatusCode);
        }

        // Only who made it removes it; then the key is refused.
        Assert.Equal(HttpStatusCode.NotFound, (await api.DeleteAsync("/api/bindings/files.store/matdrive?managedBy=someone")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync("/api/bindings/files.store/matdrive?managedBy=mattiverse")).StatusCode);
        using (var gone = new HttpRequestMessage(HttpMethod.Get, "/api/provides/files.store/ping"))
        {
            gone.Headers.Authorization = new("Bearer", renewed);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(gone)).StatusCode);
        }
    }

    [Fact]
    public async Task The_store_keeps_only_the_hash_of_a_key()
    {
        await using var app = await StartAsync();
        var key = (await (await app.Api().PutAsync("/api/bindings/files.store/matdrive", Bind(Body()))).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("variables").GetProperty("MAT_FILES_STORE_KEY").GetString()!;
        var file = await File.ReadAllTextAsync(Path.Combine(_data, MatBindingStore.Folder, MatBindingStore.FileName));
        Assert.DoesNotContain(key, file, StringComparison.Ordinal);
        Assert.Contains(MatBindingStore.Hash(key), file, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_the_key_changes_bindings_and_wrong_requests_are_refused()
    {
        await using var app = await StartAsync();
        var anonymous = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsync("/api/bindings/files.store/matdrive", Bind(Body()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/bindings")).StatusCode);

        var api = app.Api();
        Assert.Equal(HttpStatusCode.NotFound, (await api.PutAsync("/api/bindings/office.edit/matdrive", Bind(Body()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsync("/api/bindings/files.store/Mat_Drive", Bind(Body()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsync("/api/bindings/files.store/matdrive", Bind(new { version = 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PutAsync("/api/bindings/files.store/matdrive", Bind(new { version = 2, managedBy = "mattiverse" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.DeleteAsync("/api/bindings/files.store/nobody")).StatusCode);
    }

    [Fact]
    public async Task Without_a_data_folder_the_app_keeps_no_bindings()
    {
        await using var app = await StartAsync(data: false);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Api().PutAsync("/api/bindings/files.store/matdrive", Bind(Body()))).StatusCode);
        var view = await app.Api().GetFromJsonAsync<JsonElement>("/api/connections");
        Assert.False(view.GetProperty("offers")[0].GetProperty("byHand").GetBoolean());
        Assert.False(view.GetProperty("uses")[0].GetProperty("byHand").GetBoolean());
        Assert.False(Directory.Exists(_data));
    }

    [Fact]
    public async Task A_handler_of_the_app_wins_over_the_generic_one()
    {
        var handler = new RecordingHandler();
        await using var app = await StartAsync(services: s => s.AddSingleton<IMatBindingHandler>(handler));
        var answer = (await (await app.Api().PutAsync("/api/bindings/files.store/matdrive", Bind(Body()))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("variables");
        Assert.Equal("from-the-app", answer.GetProperty("MAT_FILES_STORE_KEY").GetString());
        var request = Assert.Single(handler.Requests);
        Assert.Equal("matdrive", request.Consumer.Id);
        Assert.Equal("http://mattiverse-matdrive-web:4810", request.Consumer.InternalUrl);
        Assert.Equal("https://demo.example.com", request.Provider.Url);
        Assert.Equal("mattiverse", request.ManagedBy);

        // 2.1.0: every public address of the consumer and its parameters.
        Assert.Equal(["https://drive.example.com", "http://nas:4810"], request.Consumer.Addresses());
        Assert.Equal("drive", request.Param("sender", "matdrive"));
        Assert.Equal("/signin-oidc", request.Param("callback", "/signin-oidc"));
    }

    [Fact]
    public void A_party_without_its_list_of_addresses_has_its_one_address()
    {
        Assert.Equal(["https://drive.example.com"], new MatBindingParty("matdrive", "MatDrive", "https://drive.example.com/").Addresses());
        Assert.Empty(new MatBindingParty("matdrive", "MatDrive").Addresses());
    }

    [Fact]
    public async Task The_variables_bind_a_consumer_and_fix_its_values()
    {
        await using var app = await StartAsync(configuration: new Dictionary<string, string?>
        {
            ["MAT_OFFICE_EDIT_URL"] = "https://office.example.com/",
            ["MAT_OFFICE_EDIT_INTERNAL_URL"] = "http://mattiverse-matoffice-web:4820",
            ["MAT_OFFICE_EDIT_KEY"] = "mo_secret",
        });
        var bindings = app.Services.GetRequiredService<MatBindings>();
        var office = await bindings.GetAsync("office.edit");
        Assert.Equal(MatBindingSource.Variables, office.Source);
        Assert.Equal("https://office.example.com", office.Url);
        Assert.Equal("http://mattiverse-matoffice-web:4820", office.ServerUrl);
        Assert.Equal("mo_secret", office.Key);
        Assert.True(bindings.IsFixed("office.edit"));

        // Fixed: nothing can be entered by hand.
        var problems = await bindings.SaveByHandAsync("office.edit", new Dictionary<string, string?> { ["url"] = "https://other.example.com", ["key"] = "x" }, "en");
        Assert.NotEmpty(problems);

        // The view never carries the secret.
        var json = await app.Api().GetStringAsync("/api/connections");
        Assert.DoesNotContain("mo_secret", json, StringComparison.Ordinal);
        var use = JsonDocument.Parse(json).RootElement.GetProperty("uses")[0];
        Assert.Equal("variables", use.GetProperty("source").GetString());
        Assert.Equal("Opens office files with", use.GetProperty("label").GetString());
        Assert.True(use.GetProperty("values")[2].GetProperty("value").GetProperty("isSet").GetBoolean());
    }

    [Fact]
    public async Task Values_entered_by_hand_bind_a_consumer_without_variables()
    {
        await using var app = await StartAsync();
        var bindings = app.Services.GetRequiredService<MatBindings>();
        Assert.False((await bindings.GetAsync("office.edit")).IsBound);

        var refused = await bindings.SaveByHandAsync("office.edit", new Dictionary<string, string?> { ["url"] = "ftp://office", ["key"] = "" }, "en");
        Assert.Contains("url", refused.Keys);
        Assert.Contains("key", refused.Keys);
        Assert.False((await bindings.GetAsync("office.edit")).IsBound);

        Assert.Empty(await bindings.SaveByHandAsync("office.edit", new Dictionary<string, string?> { ["url"] = "https://office.example.com/", ["key"] = "mo_by_hand" }, "en"));
        var office = await bindings.GetAsync("office.edit");
        Assert.Equal(MatBindingSource.ByHand, office.Source);
        Assert.Equal("https://office.example.com", office.ServerUrl);
        Assert.Equal("mo_by_hand", office.Key);

        // An empty secret keeps the stored one; the file holds it protected.
        Assert.Empty(await bindings.SaveByHandAsync("office.edit", new Dictionary<string, string?> { ["url"] = "https://office2.example.com", ["key"] = "" }, "en"));
        Assert.Equal("mo_by_hand", (await bindings.GetAsync("office.edit")).Key);
        Assert.DoesNotContain("mo_by_hand", await File.ReadAllTextAsync(Path.Combine(_data, MatBindingStore.Folder, MatBindingStore.FileName)), StringComparison.Ordinal);

        // The engines' capabilities are set with their variables only.
        Assert.NotEmpty(await bindings.SaveByHandAsync("mail.smtp", new Dictionary<string, string?> { ["host"] = "smtp" }, "en"));

        Assert.True(await bindings.ClearByHandAsync("office.edit"));
        Assert.False((await bindings.GetAsync("office.edit")).IsBound);
    }

    [Fact]
    public async Task The_connections_list_uses_offers_and_contributions()
    {
        await using var app = await StartAsync();
        await app.Api().PutAsync("/api/bindings/files.store/matdrive", Bind(Body()));
        var view = await app.Api().GetFromJsonAsync<JsonElement>("/api/connections");

        var uses = view.GetProperty("uses").EnumerateArray().ToList();
        Assert.Equal(["office.edit", "mail.smtp"], uses.Select(u => u.GetProperty("capability").GetString()));
        Assert.Equal("none", uses[0].GetProperty("source").GetString());
        Assert.True(uses[0].GetProperty("byHand").GetBoolean());
        Assert.False(uses[1].GetProperty("byHand").GetBoolean());
        Assert.Equal("MAT_OFFICE_EDIT_KEY", uses[0].GetProperty("values")[2].GetProperty("variable").GetString());

        var offer = Assert.Single(view.GetProperty("offers").EnumerateArray());
        Assert.Equal("Keeps files for", offer.GetProperty("label").GetString());
        Assert.Equal("matdrive", Assert.Single(offer.GetProperty("bindings").EnumerateArray()).GetProperty("consumer").GetString());

        var contributes = view.GetProperty("contributes");
        Assert.Equal(2, contributes.GetProperty("launchers").GetArrayLength());
        Assert.Equal(3, contributes.GetProperty("widgets").GetArrayLength());
    }

    [Fact]
    public async Task The_definition_carries_the_contributions_and_they_are_valid()
    {
        await using var app = await StartAsync();
        var json = await app.GetTestClient().GetStringAsync("/api");
        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal(2, root.GetProperty("contract").GetInt32());
        var launcher = root.GetProperty("contributes").GetProperty("launchers")[0];
        Assert.Equal("/home", launcher.GetProperty("url").GetString());
        Assert.Equal("groups", launcher.GetProperty("badge").GetString());
        Assert.Equal("demo", root.GetProperty("consumes")[1].GetProperty("params").GetProperty("sender").GetString());
        Assert.Empty(MatPlatformValidator.Validate(json));
    }

    [Fact]
    public async Task A_list_widget_answers_at_most_eight_rows()
    {
        await using var app = await StartAsync();
        var api = app.Api();
        var items = (await api.GetFromJsonAsync<JsonElement>("/api/widgets/recent")).GetProperty("items");
        Assert.Equal(8, items.GetArrayLength());
        Assert.Equal("/files/1", items[0].GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync("/api/widgets/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync("/api/widgets/nothing")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetTestClient().GetAsync("/api/widgets/recent")).StatusCode);
    }

    [Fact]
    public async Task The_form_connects_an_app_by_hand_and_shows_its_key_once()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var tokenAnswer = await client.GetAsync("/token");
        var cookie = string.Join("; ", tokenAnswer.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        var token = await tokenAnswer.Content.ReadAsStringAsync();

        async Task<HttpResponseMessage> Post(Dictionary<string, string> fields, bool withToken = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, MatConnectionEndpoints.FormPath) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", cookie);
            if (withToken)
            {
                request.Headers.Add("RequestVerificationToken", token);
            }

            return await client.SendAsync(request);
        }

        var connect = new Dictionary<string, string> { ["do"] = "connect", ["capability"] = "files.store", ["name"] = "My Drive", ["returnUrl"] = "//evil.example.com" };
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(connect, withToken: false)).StatusCode);

        var answer = await Post(connect);
        Assert.Equal(HttpStatusCode.Redirect, answer.StatusCode);
        Assert.Equal("/", answer.Headers.Location!.OriginalString);
        Assert.Contains(answer.Headers.GetValues("Set-Cookie"), c => c.StartsWith(MatConnectionEndpoints.FlashCookie + "=", StringComparison.Ordinal));

        var binding = Assert.Single((await app.Api().GetFromJsonAsync<JsonElement>("/api/bindings")).GetProperty("bindings").EnumerateArray());
        Assert.Equal("my-drive", binding.GetProperty("consumer").GetString());
        Assert.Equal(MatConnections.ManagedByHand, binding.GetProperty("managedBy").GetString());

        // The same name twice is refused (renew instead); remove takes any binding.
        await Post(connect);
        Assert.Single((await app.Api().GetFromJsonAsync<JsonElement>("/api/bindings")).GetProperty("bindings").EnumerateArray());
        await Post(new Dictionary<string, string> { ["do"] = "remove", ["capability"] = "files.store", ["consumer"] = "my-drive", ["returnUrl"] = "/admin" });
        Assert.Empty((await app.Api().GetFromJsonAsync<JsonElement>("/api/bindings")).GetProperty("bindings").EnumerateArray());
    }

    [Fact]
    public async Task The_form_is_only_for_who_the_app_lets_in()
    {
        await using var app = await StartAsync(admin: false);
        var answer = await app.GetTestClient().PostAsync(MatConnectionEndpoints.FormPath, new FormUrlEncodedContent(new Dictionary<string, string> { ["do"] = "clear" }));
        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
    }

    [Fact]
    public async Task The_area_is_drawn_with_the_toolkit_and_never_shows_a_secret()
    {
        await using var app = await StartAsync(configuration: new Dictionary<string, string?>
        {
            ["MATSMTP_HOST"] = "mattiverse-matsmtp-matsmtp",
            ["MATSMTP_PASSWORD"] = "smtp-secret",
        });
        await app.Api().PutAsync("/api/bindings/files.store/matdrive", Bind(new
        {
            version = 1,
            managedBy = "mattiverse",
            consumer = new { name = "<script>alert(1)</script>" },
        }));

        var html = await RenderAsync(app, admin: true);
        Assert.Contains("data-connections", html, StringComparison.Ordinal);
        Assert.Contains($"action=\"{MatConnectionEndpoints.FormPath}\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"v.url\"", html, StringComparison.Ordinal);
        Assert.Contains("mat-pill--ok", html, StringComparison.Ordinal);
        Assert.Contains("MATSMTP_HOST", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("smtp-secret", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" style=", html, StringComparison.Ordinal);
        Assert.Contains("Icons", html, StringComparison.Ordinal);

        Assert.Equal(string.Empty, await RenderAsync(app, admin: false));
    }

    [Fact]
    public async Task A_handler_refuses_a_binding_and_the_broker_and_the_area_get_its_message()
    {
        await using var app = await StartAsync(services: s => s.AddSingleton<IMatBindingHandler>(new RefusingHandler()));
        var api = app.Api();

        var taken = await api.PutAsync("/api/bindings/files.store/matdrive", Bind(Body()));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("drive.example.com has a route made by hand.", (await taken.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("host").GetString());

        var missing = await api.PutAsync("/api/bindings/files.store/other", Bind(new { version = 1, managedBy = "mattiverse", consumer = new { name = "Other" } }));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("A host is needed.", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("host").GetString());

        // The form of the Connections area: back to the page with the message, nothing bound.
        var client = app.GetTestClient();
        var tokenAnswer = await client.GetAsync("/token");
        var cookie = string.Join("; ", tokenAnswer.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        using var request = new HttpRequestMessage(HttpMethod.Post, MatConnectionEndpoints.FormPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["do"] = "connect", ["capability"] = "files.store", ["name"] = "Other", ["returnUrl"] = "/admin" }),
        };
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("RequestVerificationToken", await tokenAnswer.Content.ReadAsStringAsync());
        var answer = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, answer.StatusCode);
        Assert.Contains(answer.Headers.GetValues("Set-Cookie"), c => c.StartsWith(MatConnectionEndpoints.FlashCookie + "=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task New_credentials_are_offered_only_for_a_capability_with_a_secret()
    {
        await using var app = await StartAsync(
            describe: o => { Describe(o); o.Provides("web.route"); },
            services: s => s.AddSingleton<IMatBindingHandler>(new RouteHandler()));
        await app.Api().PutAsync("/api/bindings/files.store/matdrive", Bind(Body()));

        var html = await RenderAsync(app, admin: true);
        Assert.Equal(2, CountOf(html, "value=\"remove\""));
        Assert.Equal(1, CountOf(html, "value=\"renew\""));
    }

    [Fact]
    public async Task The_app_names_the_language_of_the_area()
    {
        await using var app = await StartAsync();
        Assert.Contains("<h2>Nutzt</h2>", await RenderAsync(app, admin: true, language: _ => "de"), StringComparison.Ordinal);
        Assert.Contains("<h2>Uses</h2>", await RenderAsync(app, admin: true, language: _ => "en"), StringComparison.Ordinal);

        // A language MatPlatform does not know: the app's default (de in the demo).
        Assert.Contains("<h2>Nutzt</h2>", await RenderAsync(app, admin: true, language: _ => "fr"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_app_that_uses_only_the_engines_is_not_asked_to_enter_values()
    {
        // office.edit can be entered by hand: the area says so and draws its form
        await using (var app = await StartAsync())
        {
            var html = await RenderAsync(app, admin: true, language: _ => "en");
            Assert.Contains("enter the values here", html, StringComparison.Ordinal);
        }

        // identity.oidc and mail.smtp are set by their variables only: no form, and the text does not ask for one
        await using (var app = await StartAsync(describe: o => o
            .App("demoapp", "Demo", "A demo", "2.0.0-20261109", "/icon.svg", "#2f6feb", "/home")
            .Consumes("identity.oidc", optional: true)
            .Consumes("mail.smtp", optional: true)))
        {
            var html = await RenderAsync(app, admin: true, language: _ => "en");
            Assert.Contains("without it, set the variables.", html, StringComparison.Ordinal);
            Assert.DoesNotContain("enter the values here", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
        }
    }

    private static int CountOf(string text, string part) => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;

    /// <summary>Runs the tag helper as a Razor page would, in a request of its own.</summary>
    private static async Task<string> RenderAsync(WebApplication app, bool admin, Func<HttpContext, string>? language = null)
    {
        using var scope = app.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<MatPlatformOptions>();
        var access = options.ConnectionsAccess;
        var uiLanguage = options.UiLanguage;
        options.ConnectionsAccess = _ => Task.FromResult(admin);
        options.UiLanguage = language;
        try
        {
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("demo.example.com");
            http.Request.Path = "/admin/connections";
            http.Request.Headers.AcceptLanguage = "en";
            var helper = ActivatorUtilities.CreateInstance<MatConnectionsTagHelper>(scope.ServiceProvider);
            helper.ViewContext = new ViewContext { HttpContext = http };
            var output = new TagHelperOutput("mat-connections", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
            await helper.ProcessAsync(new TagHelperContext([], new Dictionary<object, object>(), "id"), output);
            if (output.TagName is null)
            {
                return string.Empty;
            }

            using var writer = new StringWriter();
            output.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }
        finally
        {
            options.ConnectionsAccess = access;
            options.UiLanguage = uiLanguage;
        }
    }

    /// <summary>Refuses: a conflict for MatDrive (a route made by hand), a missing host for anyone else.</summary>
    private sealed class RefusingHandler : IMatBindingHandler
    {
        public string Capability => "files.store";

        public Task<IReadOnlyList<MatBindingInfo>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MatBindingInfo>>([]);

        public Task<MatBindResult> BindAsync(MatBindRequest request, CancellationToken ct)
            => throw (request.Consumer.Name == "MatDrive"
                ? new MatBindingRefusedException("host", "drive.example.com has a route made by hand.", conflict: true)
                : new MatBindingRefusedException("host", "A host is needed."));

        public Task<bool> UnbindAsync(string consumer, string? managedBy, CancellationToken ct) => Task.FromResult(false);
    }

    /// <summary>A capability without a secret (web.route): one route bound.</summary>
    private sealed class RouteHandler : IMatBindingHandler
    {
        public string Capability => "web.route";

        public Task<IReadOnlyList<MatBindingInfo>> ListAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MatBindingInfo>>([new MatBindingInfo("web.route", "matdrive", "MatDrive", "mattiverse")]);

        public Task<MatBindResult> BindAsync(MatBindRequest request, CancellationToken ct)
            => Task.FromResult(new MatBindResult(new Dictionary<string, string?> { ["publicUrl"] = "https://drive.example.com" }));

        public Task<bool> UnbindAsync(string consumer, string? managedBy, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class RecordingHandler : IMatBindingHandler
    {
        public List<MatBindRequest> Requests { get; } = [];

        public string Capability => "files.store";

        public Task<IReadOnlyList<MatBindingInfo>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MatBindingInfo>>([]);

        public Task<MatBindResult> BindAsync(MatBindRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new MatBindResult(new Dictionary<string, string?> { [MatCapabilities.Key] = "from-the-app" }));
        }

        public Task<bool> UnbindAsync(string consumer, string? managedBy, CancellationToken ct) => Task.FromResult(false);
    }
}

/// <summary>The checks of the contributions and the names of the capabilities.</summary>
public sealed class ContributionValidatorTests
{
    private static MatApiDefinition Valid => MatPlatformBuilder.Build(BindingTests.Describe, "en");

    [Fact]
    public void A_valid_description_has_no_problems() => Assert.Empty(MatPlatformValidator.Validate(Valid));

    [Fact]
    public void Wrong_contributions_are_named()
    {
        var definition = Valid with
        {
            Contributes = new MatContributions
            {
                Launchers = [new MatLauncher("home", "", "icon.svg", "https://elsewhere.example.com") { Audience = "friends", Badge = "nothing" }, new MatLauncher("home", "Again", "/a.svg", "/a")],
                Widgets =
                [
                    new MatWidget("w1", "Kind", "chart", "huge"),
                    new MatWidget("w2", "Too many", MatWidgetKinds.Tiles) { Items = ["groups", "groups", "groups", "groups", "groups"] },
                    new MatWidget("w3", "Unknown key", MatWidgetKinds.Stat) { Items = ["nothing"], Refresh = 5, Url = "x" },
                    new MatWidget("w4", "Not a meter", MatWidgetKinds.Meter) { Items = ["groups"] },
                ],
                QuickActions = [new MatQuickAction("nobody", "Both", "plus") { Url = "/a", Action = "refresh" }, new MatQuickAction("home", "Not a post", "plus") { Action = "nothing" }],
            },
        };
        var problems = MatPlatformValidator.Validate(definition);
        foreach (var expected in new[]
        {
            "launcher home: the label is empty", "launcher home: the icon \"icon.svg\"", "launcher home: the url \"https://elsewhere.example.com\"",
            "launcher home: the audience \"friends\"", "launcher home: the badge \"nothing\"", "contributes.launchers: \"home\" is there 2 times",
            "widget w1: the kind \"chart\"", "widget w1: the size \"huge\"", "widget w2: a tiles widget shows 1 to 4 keys of state, not 5",
            "widget w3: \"nothing\" is not a key of state", "widget w3: refresh is 5 seconds", "widget w3: the url \"x\"", "widget w4: \"groups\" is not a meter",
            "quick action \"Both\": the launcher \"nobody\"", "quick action \"Both\": needs either a url or a post action", "quick action \"Not a post\": \"nothing\" is not a post action",
        })
        {
            Assert.Contains(problems, p => p.StartsWith(expected, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_capability_is_written_down_or_a_key_capability()
    {
        Assert.Equal("MAT_OFFICE_EDIT", MatCapabilities.PrefixOf("office.edit"));
        Assert.Equal(["MAT_FILES_STORE_URL", "MAT_FILES_STORE_INTERNAL_URL", "MAT_FILES_STORE_KEY"], MatCapabilities.Of("files.store").Values.Select(v => v.Variable));
        Assert.True(MatCapabilities.Of("files.store").Value("key")!.Secret);
        Assert.Equal(MatSmtpSettings.HostVariable, MatCapabilities.Of("mail.smtp").Value("host")!.Variable);
        Assert.Equal(MatPlatformSettings.AuthorityVariable, MatCapabilities.Of("identity.oidc").Value("authority")!.Variable);
        Assert.Equal(MatFrameAncestors.Variable, MatCapabilities.Of("shell.embed").Values.Single().Variable);
        Assert.False(MatCapabilities.Of("mail.smtp").ByHand);
        Assert.True(MatCapabilities.Of("office.edit").ByHand);
        Assert.Equal("my-drive-2", MatConnections.IdOf("  My Drive (2) "));
    }
}
