using System.Text.Json;
using MatPlatform;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace MatPlatform.Tests;

/// <summary>A tiny app of the platform: its description and its values, kept in a <see cref="DemoStore"/>.</summary>
public sealed class DemoApp(DemoStore store) : IMatPlatformState, IMatPlatformConfiguration, IMatPlatformActions, IMatPlatformHealth
{
    public const string Password = "very-secret-password";

    public static void Describe(MatPlatformBuilder o) => o
        .App("demoapp", "Demo", o.T("Eine Demo für Tests", "A demo for tests"), "1.2.3-20261016", "/icon.svg", "#2f6feb")
        .Page("overview", o.T("Übersicht", "Overview"), "dashboard",
            MatSection.Tiles("groups", "lastMail", "disk"),
            MatSection.Actions("openAdmin", "refresh", "busy"))
        .Page("settings", o.T("Einstellungen", "Settings"), "settings",
            MatSection.Form(o.T("Allgemein", "General"), "publicUrl", "currency", "retries"),
            MatSection.Form("Mail", "smtpEnabled", "smtpHost", "smtpPassword"))
        .Tile("groups", o.T("Gruppen", "Groups"), icon: "users")
        .Tile("lastMail", o.T("Letzte Mail", "Last mail"), "time")
        .Tile(new MatStateItem("disk", o.T("Speicher", "Storage"), "meter") { Unit = "bytes", Max = JsonSerializer.SerializeToElement("diskMax") })
        .Field(new MatField("publicUrl", o.T("Öffentliche Adresse", "Public address"), "url") { Required = true })
        .Field(new MatField("currency", o.T("Währung", "Currency"), "select") { Options = [new("EUR", "Euro"), new("CHF", "Franken")] })
        .Field(new MatField("retries", o.T("Versuche", "Retries"), "number") { Min = 1, Max = 5 })
        .Field("smtpEnabled", o.T("Mails senden", "Send mails"), "bool")
        .Field(new MatField("smtpHost", o.T("Mailserver", "Mail server"), "text") { Required = true, Pattern = "[a-z0-9.-]{1,253}", ShowIf = MatField.When(("smtpEnabled", true)) })
        .Field(new MatField("smtpPassword", o.T("Passwort", "Password"), "secret") { ShowIf = MatField.When(("smtpEnabled", true)) })
        .LinkAction("openAdmin", o.T("Verwaltung", "Admin"), "settings", "/admin")
        .PostAction("refresh", o.T("Aktualisieren", "Refresh"), "refresh")
        .PostAction("busy", o.T("Besetzt", "Busy"), "trash", confirm: o.T("Wirklich?", "Really?"), danger: true)
        .Role("admin", "Administrator")
        .Role("member", o.T("Mitglied", "Member"))
        .Provides("mail.send");

    public Task<IReadOnlyDictionary<string, MatStateValue>> GetStateAsync(MatPlatformCall call, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, MatStateValue>>(new Dictionary<string, MatStateValue>
        {
            ["groups"] = new(12),
            ["lastMail"] = new(null),
            ["disk"] = new(1024, "warn", call.T("Fast voll", "Almost full")),
            ["diskMax"] = new(2048),
        });

    // The secret comes back as it is stored: the platform must turn it into {"isSet": true}.
    public Task<IReadOnlyDictionary<string, object?>> GetConfigurationAsync(MatPlatformCall call, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>(store.Values));

    public Task<IReadOnlyDictionary<string, string>> ConfigureAsync(IReadOnlyDictionary<string, JsonElement> values, MatPlatformCall call, CancellationToken ct)
    {
        if (values.TryGetValue("publicUrl", out var url) && url.GetString()!.Contains("refused", StringComparison.Ordinal))
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { ["publicUrl"] = "The app refuses it." });
        }

        foreach (var (key, value) in values)
        {
            store.Values[key] = value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.Null => null,
                _ => value.GetString(),
            };
        }

        store.LastCaller = call.Caller;
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    public Task<MatActionResult?> RunActionAsync(string id, MatPlatformCall call, CancellationToken ct)
    {
        store.Ran.Add(id);
        return Task.FromResult<MatActionResult?>(id switch
        {
            "refresh" => new MatActionResult(true, call.T("3 Feeds aktualisiert", "3 feeds refreshed")),
            "busy" => new MatActionResult(false, "Not now"),
            _ => null,
        });
    }

    public Task<IReadOnlyList<MatHealthCheck>> CheckHealthAsync(MatPlatformCall call, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MatHealthCheck>>(store.Down
            ? [new MatHealthCheck("database", "down", call.T("Weg", "Gone"))]
            : [new MatHealthCheck("database", "ok"), new MatHealthCheck("mail", "degraded", "SMTP not reachable")]);
}

/// <summary>What the demo app keeps: its settings and what happened.</summary>
public sealed class DemoStore
{
    public Dictionary<string, object?> Values { get; } = new()
    {
        ["publicUrl"] = "https://demo.example.com",
        ["currency"] = "EUR",
        ["retries"] = 3,
        ["smtpEnabled"] = false,
        ["smtpHost"] = "",
        ["smtpPassword"] = DemoApp.Password,
    };

    public List<string> Ran { get; } = [];

    public string? LastCaller { get; set; }

    public bool Down { get; set; }

    /// <summary>What <see cref="DemoUsers"/> answers to a person, null: success.</summary>
    public Func<MatIdentityPerson, MatIdentitySignInResult>? SignInAnswer { get; set; }
}

/// <summary>The demo app's side of the sign-in through MatIdentity: answers what <see cref="DemoStore.SignInAnswer"/> says (default: success).</summary>
public sealed class DemoUsers(DemoStore store) : IMatIdentityUsers
{
    public Task<MatIdentitySignInResult> SignInAsync(MatIdentityPerson person, HttpContext http, CancellationToken ct)
        => Task.FromResult(store.SignInAnswer?.Invoke(person) ?? MatIdentitySignInResult.Success());
}

/// <summary>Starts the demo app in memory (TestServer).</summary>
public static class DemoHost
{
    public const string ApiKey = "test-key-0123456789";

    public const string Authority = "http://identity.test";

    public static async Task<WebApplication> StartAsync(bool withKey = true, string? authority = null, Action<MatPlatformOptions>? configure = null, DemoStore? store = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [MatPlatformSettings.ApiKeyVariable] = withKey ? ApiKey : null,
            [MatPlatformSettings.AuthorityVariable] = authority,
            [MatPlatformSettings.ClientSecretVariable] = authority is null ? null : "test-secret",
        });

        var settings = builder.AddMatPlatform<DemoApp>(DemoApp.Describe, configure);
        builder.Services.AddSingleton(store ?? new DemoStore());
        builder.Services.AddAuthentication("cookie")
            .AddCookie("cookie")
            .AddMatIdentitySignIn<DemoUsers>(settings, o =>
            {
                o.CookieScheme = "cookie";
                o.LoginPath = "/login";
            });
        builder.Services.AddAuthorization();

        if (authority is not null)
        {
            // A fixed OpenID configuration, so no MatIdentity has to run (the tests look at the redirects only).
            var configuration = new OpenIdConnectConfiguration
            {
                Issuer = authority,
                AuthorizationEndpoint = authority + "/connect/authorize",
                TokenEndpoint = authority + "/connect/token",
                EndSessionEndpoint = authority + "/connect/logout",
            };
            builder.Services.PostConfigure<OpenIdConnectOptions>(MatIdentitySignIn.Scheme, options =>
            {
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
            builder.Services.PostConfigure<JwtBearerOptions>(MatApiAccess.BearerScheme, options =>
            {
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        }

        var app = builder.Build();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMatPlatform();
        app.MapGet("/login", (HttpContext http) => Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [MatIdentitySignIn.Scheme]));
        app.MapPost("/logout", (HttpContext http) => MatIdentitySignIn.SignOutAtIdentity(http, "the.id.token"));
        await app.StartAsync();
        return app;
    }

    /// <summary>A client with the key.</summary>
    public static HttpClient Api(this WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(MatApiAccess.HeaderName, ApiKey);
        return client;
    }
}
