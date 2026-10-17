using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace MatPlatform.Tests;

/// <summary>
/// Behind a TLS-terminating proxy (Matcad) the sign-in through MatIdentity takes the scheme from <c>X-Forwarded-Proto</c>, so that the redirect_uri
/// is https and the correlation cookie Secure; nothing else of the request changes.
/// </summary>
public sealed class ForwardedProtoTests
{
    private static readonly IPAddress Client = IPAddress.Parse("10.1.2.3");

    [Fact]
    public async Task Behind_a_tls_proxy_the_challenge_is_https_and_the_correlation_cookie_secure()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        var response = await SendAsync(app, "/login", "https");

        Assert.Equal(StatusCodes.Status302Found, response.Response.StatusCode);
        Assert.Equal("https://localhost/signin-oidc", RedirectUri(response));
        var correlation = Cookie(response, ".AspNetCore.Correlation.");
        Assert.Contains("secure", correlation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", Cookie(response, ".AspNetCore.OpenIdConnect.Nonce."), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Without_the_header_nothing_changes()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        var response = await SendAsync(app, "/login", null);

        Assert.Equal("http://localhost/signin-oidc", RedirectUri(response));
        Assert.DoesNotContain("secure", Cookie(response, ".AspNetCore.Correlation."), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("garbage")]
    [InlineData("httpsx")]
    [InlineData("")]
    [InlineData("http, https")]
    [InlineData("ws")]
    public async Task Any_other_value_changes_nothing(string value)
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);

        Assert.StartsWith("http|False|", await WhoAmIAsync(app, value));
        Assert.Equal("http://localhost/signin-oidc", RedirectUri(await SendAsync(app, "/login", value)));
    }

    [Theory]
    [InlineData("https")]
    [InlineData("HTTPS")]
    [InlineData(" https ")]
    [InlineData("https, http")]
    [InlineData("https,https")]
    public async Task The_first_value_https_in_any_case_is_https(string value)
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        Assert.StartsWith("https|True|", await WhoAmIAsync(app, value));
    }

    [Fact]
    public async Task The_option_turns_it_off()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority, signIn: o => o.TrustForwardedProto = false);

        Assert.StartsWith("http|False|", await WhoAmIAsync(app, "https"));
        Assert.Equal("http://localhost/signin-oidc", RedirectUri(await SendAsync(app, "/login", "https")));
        Assert.DoesNotContain(app.Services.GetServices<IStartupFilter>(), f => f.GetType().Name == "MatForwardedProtoStartupFilter");
    }

    [Fact]
    public async Task Without_matidentity_it_is_not_registered()
    {
        await using var app = await DemoHost.StartAsync();

        Assert.DoesNotContain(app.Services.GetServices<IStartupFilter>(), f => f.GetType().Name == "MatForwardedProtoStartupFilter");
        Assert.StartsWith("http|False|", await WhoAmIAsync(app, "https"));
    }

    [Fact]
    public async Task With_matidentity_it_is_registered_once()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        Assert.Single(app.Services.GetServices<IStartupFilter>(), f => f.GetType().Name == "MatForwardedProtoStartupFilter");
    }

    [Fact]
    public async Task Only_the_scheme_changes_never_the_client_address_or_the_host()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        var context = await app.GetTestServer().SendAsync(c =>
        {
            Request(c, "/whoami", "https");
            c.Request.Headers["X-Forwarded-For"] = "203.0.113.9";
            c.Request.Headers["X-Forwarded-Host"] = "evil.example";
            c.Request.Headers["X-Forwarded-Prefix"] = "/prefix";
        });

        Assert.Equal("https|True|10.1.2.3|localhost", await Body(context));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    public async Task The_apps_own_forwarded_headers_come_to_the_same_scheme(string sender)
    {
        // The app's ForwardedHeaders runs after it (a trusted sender: loopback is known by default; an untrusted one is ignored by it): the same result.
        await using var app = await DemoHost.StartAsync(
            authority: DemoHost.Authority,
            pipeline: a => a.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto }));
        var context = await app.GetTestServer().SendAsync(c =>
        {
            Request(c, "/whoami", "https");
            c.Connection.RemoteIpAddress = IPAddress.Parse(sender);
        });

        Assert.Equal($"https|True|{sender}|localhost", await Body(context));
    }

    [Fact]
    public void Apply_twice_is_the_same_as_once()
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "http";
        http.Request.Headers[MatForwardedProto.HeaderName] = "https";

        MatForwardedProto.Apply(http);
        MatForwardedProto.Apply(http);

        Assert.Equal("https", http.Request.Scheme);
        Assert.True(http.Request.IsHttps);
    }

    private static Task<HttpContext> SendAsync(WebApplication app, string path, string? proto)
        => app.GetTestServer().SendAsync(c => Request(c, path, proto));

    private static async Task<string> WhoAmIAsync(WebApplication app, string? proto)
        => await Body(await SendAsync(app, "/whoami", proto));

    private static void Request(HttpContext c, string path, string? proto)
    {
        c.Request.Method = HttpMethods.Get;
        c.Request.Path = path;
        c.Connection.RemoteIpAddress = Client;
        if (proto is not null)
        {
            c.Request.Headers[MatForwardedProto.HeaderName] = proto;
        }
    }

    private static async Task<string> Body(HttpContext context)
    {
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static string RedirectUri(HttpContext response)
    {
        var location = new Uri(response.Response.Headers.Location.ToString());
        return QueryHelpers.ParseQuery(location.Query)["redirect_uri"].ToString();
    }

    private static string Cookie(HttpContext response, string prefix)
        => Assert.Single(response.Response.Headers.SetCookie, c => c!.StartsWith(prefix, StringComparison.Ordinal))!;
}
