using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace MatPlatform.Tests;

/// <summary>
/// An account that matches by its address is linked only after its password (docs/PLATFORM.md, "The person"): the package keeps the person as a
/// <see cref="MatIdentityPendingLink"/> and sends the browser to the local sign-in.
/// </summary>
public sealed class PendingLinkTests : IAsyncLifetime
{
    private readonly DemoStore _store = new();
    private WebApplication _app = null!;

    public async Task InitializeAsync() => _app = await DemoHost.StartAsync(store: _store);

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task A_link_required_answer_stores_the_person_and_goes_to_the_local_sign_in()
    {
        _store.SignInAnswer = _ => MatIdentitySignInResult.LinkRequired("42");
        var http = Context();

        var target = await MatIdentitySignIn.CompleteSignInAsync(http, Person(), "/groups");

        Assert.Equal("/login?local=1&link=1", target);
        var cookie = SetCookie(http);
        Assert.Equal(MatIdentityPendingLink.CookieName, cookie.Name.Value);
        Assert.True(cookie.HttpOnly);
        Assert.False(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/", cookie.Path.Value);
        Assert.InRange(cookie.Expires!.Value, DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11));
        // Nothing of the person can be read from the cookie.
        Assert.DoesNotContain("anna", cookie.Value.Value!, StringComparison.OrdinalIgnoreCase);

        var link = MatIdentityPendingLink.Peek(Next(http));
        Assert.NotNull(link);
        Assert.Equal(new MatIdentityPendingLink("sub-anna", "anna@example.com", "Anna", "anna", "admin", "the.id.token", "42"), link);
    }

    [Fact]
    public void The_cookie_is_secure_on_https_and_scoped_to_the_path_of_the_app()
    {
        var http = Context();
        http.Request.Scheme = "https";
        http.Request.PathBase = "/split";

        MatIdentityPendingLink.Store(http, Person(), "42");

        var cookie = SetCookie(http);
        Assert.True(cookie.Secure);
        Assert.Equal("/split", cookie.Path.Value);
    }

    [Fact]
    public async Task A_success_or_a_refusal_clears_a_pending_link()
    {
        var first = Context();
        MatIdentityPendingLink.Store(first, Person(), "42");

        var http = Next(first);
        Assert.Equal("/groups", await MatIdentitySignIn.CompleteSignInAsync(http, Person(), "/groups"));
        Assert.Contains(http.Response.Headers.SetCookie, c => c!.StartsWith(MatIdentityPendingLink.CookieName + "=;", StringComparison.Ordinal));

        _store.SignInAnswer = _ => MatIdentitySignInResult.Refused("disabled");
        http = Next(first);
        Assert.Equal("/AccessDenied", await MatIdentitySignIn.CompleteSignInAsync(http, Person(), "/groups"));
        Assert.Contains(http.Response.Headers.SetCookie, c => c!.StartsWith(MatIdentityPendingLink.CookieName + "=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_return_address_of_another_site_is_not_followed()
        => Assert.Equal("/", await MatIdentitySignIn.CompleteSignInAsync(Context(), Person(), "//evil.example.com/"));

    [Fact]
    public void Take_for_the_matched_account_gives_the_link_once()
    {
        var first = Context();
        MatIdentityPendingLink.Store(first, Person(), "42");

        var http = Next(first);
        var link = MatIdentityPendingLink.TakeFor(http, "42");

        Assert.Equal("sub-anna", link?.Subject);
        Assert.Contains(http.Response.Headers.SetCookie, c => c!.StartsWith(MatIdentityPendingLink.CookieName + "=;", StringComparison.Ordinal));
    }

    [Fact]
    public void Another_account_never_gets_the_link_and_it_is_deleted()
    {
        var first = Context();
        MatIdentityPendingLink.Store(first, Person(), "42");

        var http = Next(first);
        Assert.Null(MatIdentityPendingLink.TakeFor(http, "7"));
        Assert.Contains(http.Response.Headers.SetCookie, c => c!.StartsWith(MatIdentityPendingLink.CookieName + "=;", StringComparison.Ordinal));
    }

    [Fact]
    public void Clear_deletes_the_link()
    {
        var first = Context();
        MatIdentityPendingLink.Store(first, Person(), "42");

        var http = Next(first);
        MatIdentityPendingLink.Clear(http);

        var cookie = SetCookie(http);
        Assert.Equal(string.Empty, cookie.Value.Value);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
    }

    [Fact]
    public void A_forged_cookie_is_ignored()
    {
        var http = Context();
        http.Request.Headers.Cookie = MatIdentityPendingLink.CookieName + "=" + Uri.EscapeDataString("{\"subject\":\"sub-x\",\"account\":\"1\"}");
        Assert.Null(MatIdentityPendingLink.Peek(http));

        http.Request.Headers.Cookie = MatIdentityPendingLink.CookieName + "=CfDJ8not-a-protected-value";
        Assert.Null(MatIdentityPendingLink.Peek(http));
    }

    [Fact]
    public void A_cookie_of_another_purpose_is_ignored()
    {
        var http = Context();
        var json = JsonSerializer.Serialize(new MatIdentityPendingLink("sub-x", null, null, null, null, null, "1"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var value = http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("Another.Purpose").ToTimeLimitedDataProtector()
            .Protect(json, TimeSpan.FromMinutes(5));
        http.Request.Headers.Cookie = MatIdentityPendingLink.CookieName + "=" + value;

        Assert.Null(MatIdentityPendingLink.Peek(http));
    }

    [Fact]
    public void An_expired_cookie_is_ignored()
    {
        var http = Context();
        var json = JsonSerializer.Serialize(new MatIdentityPendingLink("sub-x", null, null, null, null, null, "1"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var protector = http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(MatIdentityPendingLink.Purpose).ToTimeLimitedDataProtector();

        http.Request.Headers.Cookie = MatIdentityPendingLink.CookieName + "=" + protector.Protect(json, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Null(MatIdentityPendingLink.Peek(http));

        // The same value with a time left is read: the test above failed for the time only.
        http.Request.Headers.Cookie = MatIdentityPendingLink.CookieName + "=" + protector.Protect(json, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal("sub-x", MatIdentityPendingLink.Peek(http)?.Subject);
    }

    [Fact]
    public void Without_a_cookie_there_is_nothing_and_clear_writes_nothing()
    {
        var http = Context();
        Assert.Null(MatIdentityPendingLink.Peek(http));
        Assert.Null(MatIdentityPendingLink.Take(http));
        MatIdentityPendingLink.Clear(http);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public void Link_required_needs_an_account_and_is_not_a_success()
    {
        var result = MatIdentitySignInResult.LinkRequired("42");
        Assert.False(result.Succeeded);
        Assert.Equal("42", result.LinkAccount);
        Assert.Null(MatIdentitySignInResult.Success().LinkAccount);
        Assert.Throws<ArgumentException>(() => MatIdentitySignInResult.LinkRequired(""));
    }

    private DefaultHttpContext Context()
    {
        var http = new DefaultHttpContext { RequestServices = _app.Services.CreateScope().ServiceProvider };
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("demo.test");
        return http;
    }

    /// <summary>The next request of the same browser: the cookie the response of <paramref name="previous"/> set.</summary>
    private DefaultHttpContext Next(DefaultHttpContext previous)
    {
        var cookie = SetCookie(previous);
        var http = Context();
        http.Request.Headers.Cookie = cookie.Name.Value + "=" + cookie.Value.Value;
        return http;
    }

    private static SetCookieHeaderValue SetCookie(HttpContext http)
        => SetCookieHeaderValue.ParseList(http.Response.Headers.SetCookie.Select(c => c ?? string.Empty).ToList()).Single(c => c.Name.Value == MatIdentityPendingLink.CookieName);

    private static MatIdentityPerson Person()
        => new("sub-anna", "anna@example.com", "Anna", "anna", "admin", "the.id.token", new ClaimsPrincipal());
}
