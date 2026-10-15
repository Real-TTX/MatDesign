using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace MatDesign.Web.Services;

/// <summary>
/// Which senders may say, with <c>X-Forwarded-For</c>, <c>-Proto</c> and <c>-Host</c>, who the client is and how it reached the site (docs/STUDIO.md, "Sign-in and the lock-out"). Behind a reverse
/// proxy (Matcad) the address that the site sees is the proxy's, and the proxy tells the real one; but a header that anybody can write is not to be believed, because the lock-out of the sign-in
/// counts by that address and the cookie and the links follow the scheme and the host. So the headers are taken from a proxy that is known, and only from it, and only the last entry that
/// it added (<c>ForwardLimit</c> 1): what a client wrote itself stands before it and is ignored.
/// <para>
/// By default a proxy is a loopback address or an address of a private network (the proxy of the stack runs next to the site in a Docker network of the host). The variable
/// <see cref="Variable"/> narrows it: a list of addresses and networks (<c>172.18.0.2, 10.0.0.0/8</c>) or <c>none</c> (the headers are not read at all). A value that cannot be read stops the
/// start of the site with a message, instead of trusting everybody.
/// </para>
/// </summary>
public sealed class TrustedProxies
{
    /// <summary>The name of the environment variable that lists the proxies that are believed.</summary>
    public const string Variable = "MATDESIGN_TRUSTED_PROXIES";

    /// <summary>The networks that are believed when nothing is set: loopback, the private ranges of IPv4 (RFC 1918), link-local addresses, unique local and link-local addresses of IPv6.</summary>
    public static IReadOnlyList<IPNetwork> DefaultNetworks { get; } =
    [
        IPNetwork.Parse("127.0.0.0/8"), IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("10.0.0.0/8"), IPNetwork.Parse("172.16.0.0/12"), IPNetwork.Parse("192.168.0.0/16"), IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("fc00::/7"), IPNetwork.Parse("fe80::/10"),
    ];

    private TrustedProxies(IReadOnlyList<IPAddress> addresses, IReadOnlyList<IPNetwork> networks, bool none, bool isDefault)
    {
        Addresses = addresses;
        Networks = networks;
        TrustsNone = none;
        IsDefault = isDefault;
    }

    public IReadOnlyList<IPAddress> Addresses { get; }

    public IReadOnlyList<IPNetwork> Networks { get; }

    /// <summary>True when the variable says <c>none</c>: no header is read, the address is the one of the connection.</summary>
    public bool TrustsNone { get; }

    /// <summary>True when nothing was set and the private networks stand.</summary>
    public bool IsDefault { get; }

    /// <summary>Reads the variable. Null, empty or blank is the default; <c>none</c> trusts nobody; else a list separated by commas or semicolons.</summary>
    /// <exception cref="FormatException">An entry is neither an address nor a network, or the list names nothing.</exception>
    public static TrustedProxies Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new TrustedProxies([], DefaultNetworks, none: false, isDefault: true);
        }

        var parts = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1 && parts[0].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return new TrustedProxies([], [], none: true, isDefault: false);
        }

        if (parts.Length == 0)
        {
            throw new FormatException($"{Variable} names no proxy: leave it out for the private networks, or write none.");
        }

        var addresses = new List<IPAddress>();
        var networks = new List<IPNetwork>();
        foreach (var part in parts)
        {
            if (part.Contains('/'))
            {
                if (!IPNetwork.TryParse(part, out var network))
                {
                    throw new FormatException($"{Variable}: \"{part}\" is not a network such as 10.0.0.0/8.");
                }

                networks.Add(network);
            }
            else if (IPAddress.TryParse(part, out var address))
            {
                addresses.Add(address);
            }
            else
            {
                throw new FormatException($"{Variable}: \"{part}\" is not an address such as 172.18.0.2 or a network such as 10.0.0.0/8.");
            }
        }

        return new TrustedProxies(addresses, networks, none: false, isDefault: false);
    }

    /// <summary>True when the header of this sender is believed.</summary>
    public bool Trusts(IPAddress? sender)
    {
        if (sender is null || TrustsNone)
        {
            return false;
        }

        if (sender.IsIPv4MappedToIPv6)
        {
            sender = sender.MapToIPv4();
        }

        return Addresses.Any(a => (a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a).Equals(sender)) || Networks.Any(n => n.Contains(sender));
    }

    /// <summary>Puts the proxies into the options of the forwarded headers middleware: only these, only the last entry, nothing at all for <c>none</c>.</summary>
    public void Apply(ForwardedHeadersOptions options)
    {
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.ForwardLimit = 1;
        if (TrustsNone)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        foreach (var address in Addresses)
        {
            options.KnownProxies.Add(address);
        }

        foreach (var network in Networks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }
}
