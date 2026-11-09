using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MatPlatform.Tests;

/// <summary>
/// MAT_FRAME_ANCESTORS (docs/PLATFORM.md, "Shown inside another app"): only real origins open the app to a frame, and they replace X-Frame-Options with
/// frame-ancestors on every answer; unset, the headers of the app stay as they are.
/// </summary>
public sealed class FrameAncestorsTests
{
    private const string Mattiverse = "http://localhost:8030";

    [Fact]
    public void Origins_are_kept_as_scheme_host_and_port()
    {
        var origins = MatFrameAncestors.Parse("https://Mattiverse.Example.com/ http://192.168.1.5:8030, http://localhost:8030;https://mattiverse.example.com:443");
        Assert.Equal(["https://mattiverse.example.com", "http://192.168.1.5:8030", "http://localhost:8030"], origins);
    }

    [Fact]
    public void An_IPv6_address_keeps_its_brackets()
        => Assert.Equal(["http://[fd00::5]:8030"], MatFrameAncestors.Parse("http://[FD00::5]:8030"));

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("'none'")]
    [InlineData("'self'")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/?a=1")]
    [InlineData("https://example.com/#top")]
    [InlineData("https://user:pw@example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    [InlineData("javascript:alert(1)")]
    public void Anything_that_is_not_an_origin_is_left_out(string value)
        => Assert.Empty(MatFrameAncestors.Parse(value));

    [Fact]
    public void Nothing_set_is_no_origin()
    {
        Assert.Empty(MatFrameAncestors.Parse(null));
        Assert.Empty(MatFrameAncestors.Parse("  "));
    }

    [Fact]
    public void The_directive_replaces_the_one_of_the_policy()
    {
        var policy = MatFrameAncestors.Apply("default-src 'self'; frame-ancestors 'none'; object-src 'none'", [Mattiverse]);
        Assert.Equal("default-src 'self'; object-src 'none'; frame-ancestors 'self' http://localhost:8030", policy);
    }

    [Fact]
    public void Without_a_policy_the_directive_is_the_policy()
        => Assert.Equal("frame-ancestors 'self' https://a.example.com https://b.example.com", MatFrameAncestors.Apply(null, ["https://a.example.com", "https://b.example.com"]));

    [Fact]
    public void The_headers_lose_x_frame_options_and_get_frame_ancestors()
    {
        IHeaderDictionary headers = new HeaderDictionary { ["X-Frame-Options"] = "SAMEORIGIN", ["Content-Security-Policy"] = "default-src 'self'" };
        MatFrameAncestors.ApplyTo(headers, [Mattiverse]);
        Assert.False(headers.ContainsKey("X-Frame-Options"));
        Assert.Equal("default-src 'self'; frame-ancestors 'self' http://localhost:8030", headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public void Without_origins_the_headers_stay_as_they_are()
    {
        IHeaderDictionary headers = new HeaderDictionary { ["X-Frame-Options"] = "DENY" };
        MatFrameAncestors.ApplyTo(headers, []);
        Assert.Equal("DENY", headers["X-Frame-Options"].ToString());
        Assert.False(headers.ContainsKey("Content-Security-Policy"));
    }

    // ---- In an app: AddMatPlatform registers it, the hook has the last word on every answer.

    [Fact]
    public async Task Set_every_answer_names_the_origins_and_has_no_x_frame_options()
    {
        await using var app = await StartAsync($"{Mattiverse} https://mattiverse.example.com");

        var page = await GetAsync(app, "/guarded");
        Assert.False(page.Headers.ContainsKey("X-Frame-Options"));
        Assert.Equal("default-src 'self'; object-src 'none'; frame-ancestors 'self' http://localhost:8030 https://mattiverse.example.com", page.Headers.ContentSecurityPolicy.ToString());

        // An answer of the platform and one that no endpoint gives.
        Assert.Equal("frame-ancestors 'self' http://localhost:8030 https://mattiverse.example.com", (await GetAsync(app, "/api")).Headers.ContentSecurityPolicy.ToString());
        var missing = await GetAsync(app, "/nothing-here");
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);
        Assert.False(missing.Headers.ContainsKey("X-Frame-Options"));
        Assert.Contains("frame-ancestors 'self' http://localhost:8030", missing.Headers.ContentSecurityPolicy.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unset_the_headers_of_the_app_stay_as_they_are()
    {
        await using var app = await StartAsync(null);

        var page = await GetAsync(app, "/guarded");
        Assert.Equal("DENY", page.Headers["X-Frame-Options"].ToString());
        Assert.Equal("default-src 'self'; frame-ancestors 'none'; object-src 'none'", page.Headers.ContentSecurityPolicy.ToString());
        Assert.False((await GetAsync(app, "/api")).Headers.ContainsKey("Content-Security-Policy"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://example.com/path 'none'")]
    [InlineData("   ")]
    public async Task A_value_without_an_origin_changes_nothing(string value)
    {
        await using var app = await StartAsync(value);

        var page = await GetAsync(app, "/guarded");
        Assert.Equal("DENY", page.Headers["X-Frame-Options"].ToString());
        Assert.Equal("default-src 'self'; frame-ancestors 'none'; object-src 'none'", page.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task The_sameorigin_of_the_antiforgery_tokens_goes_too()
    {
        await using (var unset = await StartAsync(null))
        {
            Assert.Equal("SAMEORIGIN", (await GetAsync(unset, "/form")).Headers["X-Frame-Options"].ToString());
        }

        await using var app = await StartAsync(Mattiverse);
        var form = await GetAsync(app, "/form");
        Assert.False(form.Headers.ContainsKey("X-Frame-Options"));
        Assert.Equal("frame-ancestors 'self' http://localhost:8030", form.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task It_is_registered_once_even_when_the_app_asks_for_it_again()
    {
        await using var app = await StartAsync(Mattiverse, services: s => s.AddMatFrameAncestors());

        Assert.Single(app.Services.GetServices<IStartupFilter>(), f => f.GetType().Name == "MatFrameAncestorsStartupFilter");
        Assert.Equal("default-src 'self'; object-src 'none'; frame-ancestors 'self' http://localhost:8030", (await GetAsync(app, "/guarded")).Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task An_app_without_AddMatPlatform_calls_it_itself()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration[MatFrameAncestors.Variable] = Mattiverse;
        builder.Services.AddMatFrameAncestors();
        await using var app = builder.Build();
        app.MapGet("/", (HttpContext http) =>
        {
            http.Response.Headers["X-Frame-Options"] = "DENY";
            return "page";
        });
        await app.StartAsync();

        var context = await app.GetTestServer().SendAsync(c => c.Request.Path = "/");
        Assert.False(context.Response.Headers.ContainsKey("X-Frame-Options"));
        Assert.Equal("frame-ancestors 'self' http://localhost:8030", context.Response.Headers.ContentSecurityPolicy.ToString());
    }

    /// <summary>
    /// The demo app with what a real app does: a middleware of its own that sets its security headers in <c>OnStarting</c> (like MatDock's), a page with them,
    /// and a page with an anti-forgery token (which adds <c>X-Frame-Options: SAMEORIGIN</c>).
    /// </summary>
    private static Task<WebApplication> StartAsync(string? frameAncestors, Action<IServiceCollection>? services = null)
        => DemoHost.StartAsync(
            frameAncestors: frameAncestors,
            services: s =>
            {
                s.AddAntiforgery();
                services?.Invoke(s);
            },
            pipeline: app =>
            {
                app.Use((context, next) =>
                {
                    if (context.Request.Path == "/guarded")
                    {
                        var response = context.Response;
                        response.OnStarting(() =>
                        {
                            response.Headers["X-Frame-Options"] = "DENY";
                            response.Headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; object-src 'none'";
                            return Task.CompletedTask;
                        });
                    }

                    return next(context);
                });
                app.MapGet("/guarded", () => "guarded");
                app.MapGet("/form", (HttpContext http, IAntiforgery antiforgery) => antiforgery.GetAndStoreTokens(http).RequestToken ?? string.Empty);
            });

    private static async Task<HttpResponse> GetAsync(WebApplication app, string path)
        => (await app.GetTestServer().SendAsync(c => c.Request.Path = path)).Response;
}
