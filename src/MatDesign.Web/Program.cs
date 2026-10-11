using System.Globalization;
using MatDesign.Core;
using MatDesign.Web;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.HttpOverrides;

// Docker HEALTHCHECK: the image has no curl, so the app asks itself and exits with 0 (healthy) or 1.
if (args.Contains("--healthcheck"))
{
    return await HealthCheck.RunAsync();
}

// The site is in English: numbers and dates read the same on every machine (a container has no culture, a developer PC has one).
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// The Studio (docs/STUDIO.md) is on only with a writable data directory and the administrator's password; otherwise the site is what it always was.
// It is created when first needed, so it sees the final configuration (the tests set it after this code has run).
builder.Services.AddSingleton(provider => new StudioService(provider.GetRequiredService<IConfiguration>()));
builder.Services.AddDataProtection().SetApplicationName("MatDesign");
builder.Services.AddOptions<KeyManagementOptions>().Configure<StudioService, ILoggerFactory>((options, studio, logs) =>
{
    // With the Studio on, the keys of the session cookie and of the anti-forgery tokens survive a restart of the container.
    if (studio.Enabled)
    {
        options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(Path.Combine(studio.DataDirectory, "keys")), logs);
    }
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "matdesign.studio";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Studio/Login";
    options.AccessDeniedPath = "/Studio/Login";
    options.ReturnUrlParameter = "returnUrl";
    options.Events.OnValidatePrincipal = async context =>
    {
        // A session ends when the Studio is switched off or its password changes.
        var service = context.HttpContext.RequestServices.GetRequiredService<StudioService>();
        if (!service.Enabled || context.Principal?.FindFirst(StudioService.StampClaim)?.Value != service.Stamp)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
});
builder.Services.AddAuthorization();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Studio");
    options.Conventions.AllowAnonymousToPage("/Studio/Login");
    options.Conventions.AllowAnonymousToPage("/Studio/Logout");
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DesignData>();
builder.Services.AddSingleton<StudioHistory>();
builder.Services.AddSingleton<DesignChecks>();
builder.Services.AddSingleton<ReleaseService>();
builder.Services.AddSingleton<VersionTracker>();
builder.Services.AddSingleton<ToolkitFiles>();
builder.Services.AddSingleton<AuditData>();

// Behind a TLS-terminating reverse proxy (Matcad) the scheme and host come from the X-Forwarded-* headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// ---- The toolkit as a download: one URL per file, so any app (or a plain HTML page) can use it. ----
var ui = app.MapGroup("/ui");
ui.AddEndpointFilter(async (context, next) =>
{
    context.HttpContext.Response.Headers.AccessControlAllowOrigin = "*";
    return await next(context);
});

ui.MapGet("/matui.css", (ToolkitFiles files, HttpContext http) => ToolkitFiles.Result(http, files.CssBundle, "text/css; charset=utf-8", files.Hash));
ui.MapGet("/matui.js", (ToolkitFiles files, HttpContext http) => ToolkitFiles.Result(http, files.Script, "text/javascript; charset=utf-8", files.Hash));
ui.MapGet("/matui.boot.js", (ToolkitFiles files, HttpContext http) => ToolkitFiles.Result(http, files.BootScript, "text/javascript; charset=utf-8", files.Hash));
ui.MapGet("/matui-icons.svg", (ToolkitFiles files, HttpContext http) => ToolkitFiles.Result(http, files.Sprite, "image/svg+xml; charset=utf-8", files.Hash));
ui.MapGet("/matui.zip", (ToolkitFiles files) => Results.File(files.Zip, "application/zip", files.ZipName));
ui.MapGet("/css/{name}", (string name, ToolkitFiles files, HttpContext http) =>
    files.Css(name) is { } css ? ToolkitFiles.Result(http, css, "text/css; charset=utf-8", files.Hash) : Results.NotFound());

// ---- The design system as data: what a tool, a test or another app can read. ----
var api = app.MapGroup("/api");
api.MapGet("/apps", (DesignData design) => design.Apps.Select(a => new { a.Id, a.Name, a.Tagline, a.Category, a.Accent, onAccent = a.OnAccent ?? "#ffffff", a.AccentText, a.Glyph, a.Port }));
api.MapGet("/accents", () => AccentPresets.All);
api.MapGet("/rules", (DesignData design) => design.Rules.Select(r => new { r.Id, r.Area, r.Title, r.Statement, level = r.Level.ToString(), r.Source, r.Check }));
api.MapGet("/tokens", (ToolkitFiles files) => files.Tokens.Select(t => new { t.Name, t.Light, t.Dark, t.Description, t.Group }));
api.MapGet("/icons", (ToolkitFiles files) => files.IconNames);
api.MapGet("/version", (ToolkitFiles files) => new { toolkit = AppVersion.Current, hash = files.Hash });

// ---- The releases of the design system (the Studio makes them): what an app copies from, and how it learns that there is a newer one. Public and read-only, like /ui. ----
api.MapGet("/releases", (ReleaseService service) => service.Releases().Select(m => new { m.Version, m.At, m.Hash, m.Note, files = m.Files.Count, url = $"/releases/{m.Version}/" }));
api.MapGet("/releases/latest", (ReleaseService service) => service.Latest is { } latest ? Results.Json(latest) : Results.NotFound());

var releases = app.MapGroup("/releases");
releases.AddEndpointFilter(async (context, next) =>
{
    context.HttpContext.Response.Headers.AccessControlAllowOrigin = "*";
    return await next(context);
});
releases.MapGet("/latest.json", (ReleaseService service, HttpContext http) =>
{
    http.Response.Headers.CacheControl = "no-cache";
    return service.Latest is { } latest ? Results.Json(latest) : Results.NotFound();
});
releases.MapGet("/{version}/{**file}", (string version, string file, ReleaseService service, HttpContext http) =>
{
    if (service.FileOf(version, file) is not { } found)
    {
        return Results.NotFound();
    }

    // a release never changes, so a copy of it never goes out of date
    http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.File(found.Path, found.ContentType);
});

app.MapGet("/health", () => Results.Text("ok"));
app.MapRazorPages();

app.Run();
return 0;

/// <summary>Makes the entry point visible to the tests that start the whole site in memory.</summary>
public partial class Program;
