using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace MatPlatform.Tests;

/// <summary>The sign-in through MatIdentity: off without <c>MATIDENTITY_AUTHORITY</c>, code with PKCE and the end-session endpoint with it.</summary>
public sealed class SignInTests
{
    [Fact]
    public async Task With_matidentity_the_challenge_is_code_with_pkce_for_the_app()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        var response = await Browser(app).GetAsync("/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(DemoHost.Authority + "/connect/authorize", location.OriginalString);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("demoapp", query["client_id"].ToString());
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.EndsWith("/signin-oidc", query["redirect_uri"].ToString());
        Assert.Equal(["openid", "profile", "email", "roles", "offline_access"], query["scope"].ToString().Split(' '));
    }

    [Fact]
    public async Task The_sign_out_goes_to_the_end_session_endpoint_with_the_id_token()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        var response = await Browser(app).PostAsync("/logout", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(DemoHost.Authority + "/connect/logout", location.OriginalString);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("the.id.token", query["id_token_hint"].ToString());
        Assert.EndsWith("/signout-callback-oidc", query["post_logout_redirect_uri"].ToString());
    }

    [Fact]
    public async Task With_matidentity_the_definition_names_it_and_a_bad_token_is_401()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);
        using var doc = JsonDocument.Parse(await app.GetTestClient().GetStringAsync("/api"));
        Assert.Equal(DemoHost.Authority, doc.RootElement.GetProperty("auth").GetProperty("identity").GetString());

        var bearer = app.GetTestClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await bearer.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Api().GetAsync("/api/state")).StatusCode);
    }

    [Fact]
    public async Task Without_matidentity_there_is_no_callback()
    {
        await using var app = await DemoHost.StartAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await Browser(app).GetAsync("/signin-oidc")).StatusCode);
    }

    [Fact]
    public void The_settings_come_from_the_environment_with_the_app_id_as_the_default_client()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [MatPlatformSettings.ApiKeyVariable] = "  ",
            [MatPlatformSettings.AuthorityVariable] = "https://id.example.com/",
            [MatPlatformSettings.InternalUrlVariable] = "http://matidentity:2300/",
        }).Build();

        var settings = MatPlatformSettings.From(configuration, "demoapp");

        Assert.Null(settings.ApiKey);
        Assert.Equal("https://id.example.com", settings.Authority);
        Assert.Equal("http://matidentity:2300", settings.InternalUrl);
        Assert.Equal("demoapp", settings.ClientId);
        Assert.True(settings.IdentityEnabled);
    }

    private static HttpClient Browser(WebApplication app) => app.GetTestClient();
}

/// <summary>MATIDENTITY_INTERNAL_URL: the server-to-server calls go to the internal address, everything else stays as it is.</summary>
public sealed class BackchannelHandlerTests
{
    [Theory]
    [InlineData("https://id.example.com/.well-known/openid-configuration", "http://matidentity:2300/.well-known/openid-configuration")]
    [InlineData("https://id.example.com/.well-known/jwks", "http://matidentity:2300/.well-known/jwks")]
    [InlineData("https://id.example.com/connect/token", "http://matidentity:2300/connect/token")]
    [InlineData("https://ID.example.com:443/connect/userinfo?x=1", "http://matidentity:2300/connect/userinfo?x=1")]
    public void An_address_of_the_public_authority_is_rewritten(string from, string to)
    {
        using var handler = new MatIdentityBackchannelHandler(new Uri("https://id.example.com"), new Uri("http://matidentity:2300"));
        Assert.Equal(new Uri(to), handler.Rewrite(new Uri(from)));
    }

    [Theory]
    [InlineData("https://other.example.com/connect/token")]
    [InlineData("http://id.example.com/connect/token")]
    [InlineData("https://id.example.com:8443/connect/token")]
    public void Other_addresses_stay(string from)
    {
        using var handler = new MatIdentityBackchannelHandler(new Uri("https://id.example.com"), new Uri("http://matidentity:2300"));
        Assert.Null(handler.Rewrite(new Uri(from)));
    }

    [Fact]
    public void An_authority_with_a_path_keeps_the_rest_of_the_path()
    {
        using var handler = new MatIdentityBackchannelHandler(new Uri("https://example.com/id"), new Uri("http://matidentity:2300"));
        Assert.Equal(new Uri("http://matidentity:2300/connect/token"), handler.Rewrite(new Uri("https://example.com/id/connect/token")));
        Assert.Null(handler.Rewrite(new Uri("https://example.com/identity/connect/token")));
    }

    [Fact]
    public async Task The_request_goes_to_the_internal_address()
    {
        var inner = new RecordingHandler();
        using var handler = new MatIdentityBackchannelHandler(new Uri("https://id.example.com"), new Uri("http://matidentity:2300"), inner);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://id.example.com/.well-known/openid-configuration");

        Assert.Equal(new Uri("http://matidentity:2300/.well-known/openid-configuration"), inner.Seen);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
