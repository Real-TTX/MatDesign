using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MatPlatform.Tests;

/// <summary>
/// The back-channel logout (OpenID Connect Back-Channel Logout 1.0, 1.4.0): MatIdentity posts a signed logout token, the package checks it and hands
/// <c>sub</c> / <c>sid</c> to the app's <see cref="IMatIdentitySessions"/>.
/// </summary>
public sealed class BackchannelLogoutTests
{
    private const string Path = "/signout-backchannel";

    [Fact]
    public async Task A_valid_token_ends_the_session_it_names()
    {
        var store = new DemoStore();
        await using var app = await Start(store);

        var response = await Post(app, Token());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal([("person-1", "session-1")], store.Ended);
    }

    [Fact]
    public async Task A_token_with_the_subject_alone_ends_every_session_of_the_person()
    {
        var store = new DemoStore();
        await using var app = await Start(store);

        var response = await Post(app, Token(c => c.Remove("sid")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([("person-1", (string?)null)], store.Ended);
    }

    [Fact]
    public async Task A_token_with_the_session_alone_is_valid_too()
    {
        var store = new DemoStore();
        await using var app = await Start(store);

        Assert.Equal(HttpStatusCode.OK, (await Post(app, Token(c => c.Remove("sub")))).StatusCode);
        Assert.Equal([((string?)null, "session-1")], store.Ended);
    }

    [Theory]
    [InlineData("another key", "not valid")]
    [InlineData("another issuer", "issuer")]
    [InlineData("another audience", "udience")]
    [InlineData("issued long ago", "too old")]
    [InlineData("issued in the future", "future")]
    [InlineData("no iat", "no iat")]
    [InlineData("expired", "IDX10223")]
    [InlineData("no events", "events")]
    [InlineData("another event", "events")]
    [InlineData("an event that is not an object", "events")]
    [InlineData("neither sub nor sid", "neither sub nor sid")]
    [InlineData("a nonce", "nonce")]
    [InlineData("no jti", "no jti")]
    [InlineData("an ID token type", "type JWT")]
    [InlineData("not signed", "not valid")]
    [InlineData("not a token", "not valid")]
    public async Task An_invalid_token_is_400_and_ends_nothing(string kind, string reason)
    {
        var store = new DemoStore();
        await using var app = await Start(store);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var token = kind switch
        {
            "another key" => Token(credentials: DemoHost.NewCredentials("demo-key")),
            "another issuer" => Token(c => c["iss"] = "http://other.test"),
            "another audience" => Token(c => c["aud"] = "otherapp"),
            "issued long ago" => Token(c => c["iat"] = now - 3600),
            "issued in the future" => Token(c => c["iat"] = now + 3600),
            "no iat" => Token(c => c.Remove("iat")),
            "expired" => Token(c => { c["nbf"] = now - 900; c["exp"] = now - 600; }),
            "no events" => Token(c => c.Remove("events")),
            "another event" => Token(c => c["events"] = new Dictionary<string, object> { ["http://schemas.openid.net/event/other"] = new Dictionary<string, object>() }),
            "an event that is not an object" => Token(c => c["events"] = new Dictionary<string, object> { [MatIdentityLogoutToken.EventType] = "yes" }),
            "neither sub nor sid" => Token(c => { c.Remove("sub"); c.Remove("sid"); }),
            "a nonce" => Token(c => c["nonce"] = "n-1"),
            "no jti" => Token(c => c.Remove("jti")),
            "an ID token type" => Token(type: "JWT"),
            "not signed" => Unsigned(),
            _ => "not-a-token",
        };

        var response = await Post(app, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", body.RootElement.GetProperty("error").GetString());
        Assert.Contains(reason, body.RootElement.GetProperty("error_description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Ended);
    }

    [Fact]
    public async Task Without_the_form_field_it_is_400()
    {
        await using var app = await Start(new DemoStore());

        Assert.Equal(HttpStatusCode.BadRequest, (await app.GetTestClient().PostAsync(Path, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.GetTestClient().PostAsync(Path, new StringContent("logout_token=x"))).StatusCode);
    }

    [Fact]
    public async Task A_token_sent_again_is_400()
    {
        var store = new DemoStore();
        await using var app = await Start(store);
        var token = Token();

        Assert.Equal(HttpStatusCode.OK, (await Post(app, token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(app, token)).StatusCode);
        Assert.Single(store.Ended);
    }

    [Fact]
    public async Task An_app_that_fails_to_end_the_sessions_answers_400()
    {
        var store = new DemoStore { FailEnding = true };
        await using var app = await Start(store);

        Assert.Equal(HttpStatusCode.BadRequest, (await Post(app, Token())).StatusCode);
    }

    [Fact]
    public async Task Without_matidentity_there_is_no_endpoint()
    {
        await using var app = await DemoHost.StartAsync(withSessions: true);

        Assert.Equal(HttpStatusCode.NotFound, (await Post(app, Token())).StatusCode);
    }

    [Fact]
    public async Task An_app_without_the_hook_answers_501()
    {
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority);

        var response = await Post(app, Token());

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task The_path_can_be_changed_and_get_is_not_answered()
    {
        var store = new DemoStore();
        await using var app = await DemoHost.StartAsync(authority: DemoHost.Authority, store: store, withSessions: true, signIn: o => o.BackchannelLogoutPath = "/auth/backchannel");

        Assert.Equal(HttpStatusCode.NotFound, (await Post(app, Token())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(app, Token(), "/auth/backchannel")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await app.GetTestClient().GetAsync("/auth/backchannel")).StatusCode);
    }

    [Fact]
    public async Task A_second_map_maps_nothing()
    {
        await using var app = await Start(new DemoStore());

        Assert.Null(app.MapMatIdentityBackchannelLogout());
    }

    [Fact]
    public async Task The_sign_in_hands_the_sid_of_the_id_token_to_the_app()
    {
        var store = new DemoStore();
        MatIdentityPerson? seen = null;
        store.SignInAnswer = person =>
        {
            seen = person;
            return MatIdentitySignInResult.Success();
        };
        await using var app = await Start(store);

        var http = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        var options = app.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(MatIdentitySignIn.Scheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "person-1"), new Claim("sid", "session-1")], "test"));
        var context = new TicketReceivedContext(
            http,
            new AuthenticationScheme(MatIdentitySignIn.Scheme, null, typeof(OpenIdConnectHandler)),
            options,
            new AuthenticationTicket(principal, new AuthenticationProperties(), MatIdentitySignIn.Scheme));

        await options.Events.TicketReceived(context);

        Assert.Equal("person-1", seen!.Subject);
        Assert.Equal("session-1", seen.SessionId);
    }

    [Fact]
    public void The_replay_cache_forgets_an_id_once_it_expired()
    {
        var cache = new MatIdentityLogoutReplayCache();
        var now = DateTimeOffset.UtcNow;

        Assert.True(cache.TryRemember("a", now.AddMinutes(5), now));
        Assert.False(cache.TryRemember("a", now.AddMinutes(5), now.AddMinutes(1)));
        Assert.True(cache.TryRemember("b", now.AddMinutes(20), now.AddMinutes(6)));
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryRemember("a", now.AddMinutes(11), now.AddMinutes(6)));
    }

    private static Task<WebApplication> Start(DemoStore store)
        => DemoHost.StartAsync(authority: DemoHost.Authority, store: store, withSessions: true);

    private static Task<HttpResponseMessage> Post(WebApplication app, string token, string path = Path)
        => app.GetTestClient().PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["logout_token"] = token }));

    /// <summary>A logout token as MatIdentity writes it, changed by <paramref name="change"/>.</summary>
    private static string Token(Action<Dictionary<string, object>>? change = null, SigningCredentials? credentials = null, string type = MatIdentityLogoutToken.TokenType)
    {
        var claims = Claims();
        change?.Invoke(claims);
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            TokenType = type,
            SigningCredentials = credentials ?? DemoHost.Credentials,
        });
    }

    /// <summary>The same claims with <c>alg: none</c>.</summary>
    private static string Unsigned()
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"logout+jwt"}""");
        var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(Claims()));
        return header + "." + payload + ".";
    }

    private static Dictionary<string, object> Claims()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new Dictionary<string, object>
        {
            ["iss"] = DemoHost.Authority,
            ["aud"] = "demoapp",
            ["iat"] = now,
            ["nbf"] = now,
            ["exp"] = now + 120,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["sub"] = "person-1",
            ["sid"] = "session-1",
            ["events"] = new Dictionary<string, object> { [MatIdentityLogoutToken.EventType] = new Dictionary<string, object>() },
        };
    }
}
