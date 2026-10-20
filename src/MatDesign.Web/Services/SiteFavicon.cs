using System.Net;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Services;

/// <summary>
/// The icon of the site (<c>/favicon.svg</c>, and the picture the examples of the documentation use). The file of the demo is the glyph of the design app in its own colour, and it is served exactly as it is. A company that is not the
/// demo (<c>IDENT-09</c>) must not carry another company's colour in its browser tab: for it the same glyph is drawn in the accent of the site and named after the site. A brand with a favicon of its own replaces it (<c>IDENT-08</c>),
/// as before; this is only what a site without one shows.
/// </summary>
public static class SiteFavicon
{
    public const string Path = "/favicon.svg";

    /// <summary>The colour the glyph has in the file the demo serves.</summary>
    public const string DemoColour = "#d6336c";

    /// <summary>The name the file gives the picture.</summary>
    public const string DemoName = "MatDesign";

    /// <summary>The file with the colour and the name of the site: the same shapes, nothing else changes.</summary>
    public static string Of(string demoFile, string accent, string siteName)
        => demoFile.Replace($"fill=\"{DemoColour}\"", $"fill=\"{accent}\"", StringComparison.Ordinal)
            .Replace($"aria-label=\"{DemoName}\"", $"aria-label=\"{WebUtility.HtmlEncode(siteName)}\"", StringComparison.Ordinal);

    /// <summary>Answers <c>/favicon.svg</c> for a site that is not the demo, before the static file does; every other request, and every request of the demo, goes on to the file.</summary>
    public static IApplicationBuilder UseSiteFavicon(this IApplicationBuilder app)
    {
        string? demoFile = null;
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path == Path && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && context.RequestServices.GetRequiredService<DesignData>() is { IsDemo: false } design)
            {
                demoFile ??= ReadDemoFile(context.RequestServices.GetRequiredService<IWebHostEnvironment>());
                if (demoFile is not null)
                {
                    var site = design.SiteApp;
                    var svg = Of(demoFile, site.Accent, site.Name);
                    var result = ToolkitFiles.Result(context, svg, "image/svg+xml; charset=utf-8", ToolkitFiles.ShortHash(site.Accent + site.Name));
                    context.Response.Headers.CacheControl = "no-cache";
                    await result.ExecuteAsync(context);
                    return;
                }
            }

            await next();
        });
    }

    private static string? ReadDemoFile(IWebHostEnvironment env)
    {
        var file = env.WebRootFileProvider.GetFileInfo(Path.TrimStart('/'));
        if (!file.Exists)
        {
            return null;
        }

        using var reader = new StreamReader(file.CreateReadStream());
        return reader.ReadToEnd();
    }
}
