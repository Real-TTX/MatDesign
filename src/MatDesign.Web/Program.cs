using System.Globalization;
using MatDesign.Core;
using MatDesign.Web;
using MatDesign.Web.Services;
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

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
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
api.MapGet("/apps", () => AppRegistry.All.Select(a => new { a.Id, a.Name, a.Tagline, a.Category, a.Accent, onAccent = a.OnAccent ?? "#ffffff", a.AccentText, a.Glyph, a.Port }));
api.MapGet("/accents", () => AccentPresets.All);
api.MapGet("/rules", () => RuleBook.All.Select(r => new { r.Id, r.Area, r.Title, r.Statement, level = r.Level.ToString(), r.Source, r.Check }));
api.MapGet("/tokens", (ToolkitFiles files) => files.Tokens.Select(t => new { t.Name, t.Light, t.Dark, t.Description, t.Group }));
api.MapGet("/icons", (ToolkitFiles files) => files.IconNames);
api.MapGet("/version", (ToolkitFiles files) => new { toolkit = AppVersion.Current, hash = files.Hash });

app.MapGet("/health", () => Results.Text("ok"));
app.MapRazorPages();

app.Run();
return 0;

/// <summary>Makes the entry point visible to the tests that start the whole site in memory.</summary>
public partial class Program;
