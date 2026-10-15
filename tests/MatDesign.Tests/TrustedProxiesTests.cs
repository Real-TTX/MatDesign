using System.Net;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MatDesign.Tests;

/// <summary>
/// Which sender may say who the client is (docs/STUDIO.md, "Sign-in and the lock-out"): X-Forwarded-For, -Proto and -Host are believed from a proxy that is known and from nobody else, and only
/// the last entry that the proxy added is taken. The lock-out of the sign-in counts by that address, so a header that anybody can write would let a guesser start again at will.
/// </summary>
public sealed class TrustedProxiesTests(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory) : IClassFixture<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>>
{
    private static IPAddress Ip(string text) => IPAddress.Parse(text);

    // ---- what is read from the variable

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_set_means_loopback_and_the_private_networks(string? value)
    {
        var proxies = TrustedProxies.Parse(value);
        Assert.True(proxies.IsDefault);
        Assert.False(proxies.TrustsNone);
        foreach (var inside in new[] { "127.0.0.1", "::1", "10.1.2.3", "172.17.0.1", "172.31.255.254", "192.168.1.20", "169.254.3.4", "fd00::5", "fe80::1", "::ffff:10.0.0.7" })
        {
            Assert.True(proxies.Trusts(Ip(inside)), inside);
        }

        // the internet, the ranges next to the private ones, and a sender that is not known at all
        foreach (var outside in new[] { "8.8.8.8", "172.32.0.1", "172.15.0.1", "192.169.0.1", "11.0.0.1", "2001:db8::1", "203.0.113.9" })
        {
            Assert.False(proxies.Trusts(Ip(outside)), outside);
        }

        Assert.False(proxies.Trusts(null));
    }

    [Fact]
    public void A_list_of_addresses_and_networks_is_exactly_what_is_believed()
    {
        var proxies = TrustedProxies.Parse("172.18.0.2, 198.51.100.0/24;2001:db8::/32");
        Assert.False(proxies.IsDefault);
        Assert.True(proxies.Trusts(Ip("172.18.0.2")));
        Assert.True(proxies.Trusts(Ip("198.51.100.77")));
        Assert.True(proxies.Trusts(Ip("2001:db8:1::9")));
        // a list narrows the default: a private address that is not listed is not believed
        Assert.False(proxies.Trusts(Ip("172.18.0.3")));
        Assert.False(proxies.Trusts(Ip("10.0.0.1")));
        Assert.False(proxies.Trusts(Ip("127.0.0.1")));
    }

    [Fact]
    public void None_believes_nobody_and_the_headers_are_not_read_at_all()
    {
        var proxies = TrustedProxies.Parse(" None ");
        Assert.True(proxies.TrustsNone);
        Assert.False(proxies.Trusts(Ip("127.0.0.1")));
        var options = new ForwardedHeadersOptions();
        proxies.Apply(options);
        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("10.0.0.0/99")]
    [InlineData("300.1.1.1")]
    [InlineData("10.0.0.1, banana")]
    [InlineData(",;")]
    public void A_value_that_cannot_be_read_stops_the_start_and_never_means_everybody(string value)
        => Assert.Throws<FormatException>(() => TrustedProxies.Parse(value));

    [Fact]
    public void The_options_hold_the_proxies_and_only_the_last_entry_of_the_header()
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Parse("172.18.0.2, 10.0.0.0/8").Apply(options);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Single(options.KnownProxies);
        Assert.Single(options.KnownIPNetworks);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost, options.ForwardedHeaders);

        // the default is not an empty list (an empty list believes everybody, which the site used to do)
        var defaults = new ForwardedHeadersOptions();
        TrustedProxies.Parse(null).Apply(defaults);
        Assert.Equal(TrustedProxies.DefaultNetworks.Count, defaults.KnownIPNetworks.Count);
        Assert.NotEmpty(defaults.KnownIPNetworks);
    }

    // ---- what the middleware does with them, in a small site of its own

    private static async Task<(string Address, string Scheme, string Host)> Ask(string? variable, string sender, IDictionary<string, string> headers)
    {
        using var host = await new HostBuilder().ConfigureWebHost(web => web
            .UseTestServer()
            .ConfigureServices(services => services.Configure<ForwardedHeadersOptions>(TrustedProxies.Parse(variable).Apply))
            .Configure(app =>
            {
                // the connection, as a real socket would say it
                app.Use(async (context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
                    await next();
                });
                app.UseForwardedHeaders();
                app.Run(context => context.Response.WriteAsync($"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}|{context.Request.Host}"));
            })).StartAsync();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        var parts = (await (await client.SendAsync(request)).Content.ReadAsStringAsync()).Split('|');
        return (parts[0], parts[1], parts[2]);
    }

    private static readonly Dictionary<string, string> Claim = new()
    {
        ["X-Forwarded-For"] = "198.51.100.7",
        ["X-Forwarded-Proto"] = "https",
        ["X-Forwarded-Host"] = "design.example.org",
    };

    [Fact]
    public async Task A_sender_that_is_not_a_proxy_cannot_say_who_it_is()
    {
        // somebody who reaches the site by itself and writes the headers: nothing is believed (address, scheme and host stay what the connection says)
        var (address, scheme, host) = await Ask(null, "203.0.113.9", Claim);
        Assert.Equal("203.0.113.9", address);
        Assert.Equal("http", scheme);
        Assert.NotEqual("design.example.org", host);
    }

    [Fact]
    public async Task A_proxy_that_is_known_says_the_client_the_scheme_and_the_host()
    {
        var (address, scheme, host) = await Ask(null, "172.18.0.2", Claim);
        Assert.Equal("198.51.100.7", address);
        Assert.Equal("https", scheme);
        Assert.Equal("design.example.org", host);
    }

    [Fact]
    public async Task What_the_client_wrote_before_the_proxy_is_ignored_only_the_last_entry_counts()
    {
        // the proxy appends the address of the one that called it: what stands before was written by that client
        var chain = new Dictionary<string, string> { ["X-Forwarded-For"] = "6.6.6.6, 198.51.100.7" };
        var (address, _, _) = await Ask(null, "172.18.0.2", chain);
        Assert.Equal("198.51.100.7", address);
    }

    [Fact]
    public async Task A_list_in_the_variable_makes_another_private_sender_a_stranger()
    {
        var (listed, _, _) = await Ask("172.18.0.2", "172.18.0.2", Claim);
        Assert.Equal("198.51.100.7", listed);
        var (other, _, _) = await Ask("172.18.0.2", "172.18.0.9", Claim);
        Assert.Equal("172.18.0.9", other);
    }

    [Fact]
    public async Task None_reads_no_header_even_from_loopback()
    {
        var (address, scheme, _) = await Ask("none", "127.0.0.1", Claim);
        Assert.Equal("127.0.0.1", address);
        Assert.Equal("http", scheme);
    }

    // ---- the lock-out, through the whole site

    private static async Task<HttpResponseMessage> Try(HttpClient client, string name, string password, string forwardedFor)
    {
        var page = await (await client.GetAsync("/Studio/Login")).Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Studio/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = name, ["Password"] = password, ["__RequestVerificationToken"] = token }),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task A_guesser_cannot_start_again_by_writing_another_address_in_a_header()
    {
        using var studio = new StudioHarness(factory);
        await studio.SignIn();
        studio.AddUser("Eddie", "editor");

        // the connection is a stranger's (not a private network, so no proxy); every try writes another X-Forwarded-For
        var guesser = studio.NewClient();
        guesser.DefaultRequestHeaders.Add(TestSenderStartupFilter.Header, "203.0.113.50");
        for (var i = 0; i < 5; i++)
        {
            await Try(guesser, "Eddie", "wrong " + i, $"198.51.100.{i + 1}");
        }

        var locked = await Try(guesser, "Eddie", StudioHarness.PersonPassword, "198.51.100.99");
        Assert.Contains("Too many attempts", await locked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Behind_a_proxy_that_is_known_every_client_has_its_own_count()
    {
        using var studio = new StudioHarness(factory);
        await studio.SignIn();
        studio.AddUser("Eddie", "editor");

        // the proxy (a private address) tells two clients apart: one is locked, the other signs in
        var proxy = studio.NewClient();
        proxy.DefaultRequestHeaders.Add(TestSenderStartupFilter.Header, "172.18.0.2");
        for (var i = 0; i < 5; i++)
        {
            await Try(proxy, "Eddie", "wrong " + i, "198.51.100.10");
        }

        Assert.Contains("Too many attempts", await (await Try(proxy, "Eddie", StudioHarness.PersonPassword, "198.51.100.10")).Content.ReadAsStringAsync());
        var other = await Try(proxy, "Eddie", StudioHarness.PersonPassword, "198.51.100.11");
        Assert.Equal(HttpStatusCode.Redirect, other.StatusCode);
    }
}