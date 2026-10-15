using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

namespace MatDesign.Tests;

/// <summary>
/// A site in memory has no socket, so a request has no sender address. This puts one there, first in the pipeline and before the forwarded headers are read, from the header
/// <see cref="Header"/> of the test's request: it is what the connection would say. The header <c>X-Forwarded-For</c> then keeps the meaning it has on a real site (it counts only from a proxy
/// that is known, TrustedProxiesTests), so a test that needs another client address sets this one.
/// </summary>
public sealed class TestSenderStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Sender";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, pipeline) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var value) && IPAddress.TryParse(value.ToString(), out var sender))
            {
                context.Connection.RemoteIpAddress = sender;
            }

            await pipeline();
        });
        next(app);
    };
}
