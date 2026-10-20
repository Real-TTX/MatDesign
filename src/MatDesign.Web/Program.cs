using System.Globalization;
using MatDesign.Core;
using MatDesign.Core.Studio;
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

// The Studio (docs/STUDIO.md) is on when it has a writable data directory (the image sets /data); the first visitor creates the administrator in the setup assistant.
// Without a data directory the site is what it always was. The service is created when first needed, so it sees the final configuration (the tests set it after this code has run).
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
        // A session ends when the Studio is switched off, when the person is disabled or removed, or when their password changes (another stamp). The cookie carries the id of the
        // person and no role: the role is read from the people on every request, so a change of it takes effect at once.
        var service = context.HttpContext.RequestServices.GetRequiredService<StudioService>();
        if (!service.Enabled || service.CurrentUser(context.Principal) is null)
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
    options.Conventions.AllowAnonymousToPage("/Studio/Setup");
    options.Conventions.AllowAnonymousToPage("/Studio/Logout");
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DesignData>();
builder.Services.AddSingleton<StudioHistory>();
builder.Services.AddSingleton<DesignChecks>();
builder.Services.AddSingleton<ReleaseService>();
builder.Services.AddSingleton<VersionTracker>();
builder.Services.AddSingleton<AssetFiles>();
builder.Services.AddSingleton<ToolkitFiles>();
builder.Services.AddSingleton<IconLibrary>();
builder.Services.AddSingleton<AuditData>();
builder.Services.AddSingleton<SearchIndex>();

// Behind a TLS-terminating reverse proxy (Matcad) the scheme, the host and the address of the client come from the X-Forwarded-* headers: from a proxy that is known, never from anybody (TrustedProxies).
var trustedProxies = TrustedProxies.Parse(Environment.GetEnvironmentVariable(TrustedProxies.Variable));
builder.Services.AddSingleton(trustedProxies);
builder.Services.Configure<ForwardedHeadersOptions>(trustedProxies.Apply);

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
}

// The icon of the site: the file for the demo, the same glyph in the accent of the site for a company that is not the demo (IDENT-09).
app.UseSiteFavicon();
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
// What version this is, and what changed in each: the manifest is what a program compares (version, hash and SHA-256 of every file), the changelog is what a person reads.
ui.MapGet("/manifest.json", (ToolkitFiles files, HttpContext http) => ToolkitFiles.Result(http, files.ManifestText(), "application/json; charset=utf-8", files.Hash));
ui.MapGet("/CHANGELOG.md", (DesignData design, HttpContext http) => { var text = design.ChangelogText; return ToolkitFiles.Result(http, text, "text/markdown; charset=utf-8", ToolkitFiles.ShortHash(text)); });
ui.MapGet("/css/{name}", (string name, ToolkitFiles files, HttpContext http) =>
    files.Css(name) is { } css ? ToolkitFiles.Result(http, css, "text/css; charset=utf-8", files.Hash) : Results.NotFound());

// The optional icon library: its own files, nothing in matui.css or matui.js depends on them.
ui.MapGet("/iconlib/matui-iconlib.svg", (IconLibrary library, HttpContext http) => ToolkitFiles.Result(http, library.Sprite, "image/svg+xml; charset=utf-8", library.Hash));
ui.MapGet("/iconlib/iconlib.json", (IconLibrary library, HttpContext http) => ToolkitFiles.Result(http, library.Catalogue, "application/json; charset=utf-8", library.Hash));
ui.MapGet("/iconlib/LICENSE.txt", (IconLibrary library, HttpContext http) => ToolkitFiles.Result(http, library.License, "text/plain; charset=utf-8", library.Hash));
ui.MapGet("/iconlib/README.txt", (IconLibrary library, HttpContext http) => ToolkitFiles.Result(http, library.Readme, "text/plain; charset=utf-8", library.Hash));
ui.MapGet("/iconlib/matui-iconlib.zip", (IconLibrary library) => Results.File(library.Zip, "application/zip", "matui-iconlib.zip"));
ui.MapGet("/iconlib/svg/{name}.svg", (string name, IconLibrary library, HttpContext http) =>
    library.Find(name) is null ? Results.NotFound() : ToolkitFiles.Result(http, library.Svg(name), "image/svg+xml; charset=utf-8", library.Hash));

// The files of the brand (stage 13, IDENT-08): logo, word mark, favicon, app icon and fonts, served from the volume. The name is the slot and the first ten digits of the hash of the content,
// so a name always means the same bytes and may be cached for good; only what the current assets name, and only while the file still has its hash, is served. An SVG gets a policy that
// forbids everything (a brand's picture is a picture, also when somebody opens its address directly), and no type is ever sniffed.
ui.MapGet("/brand/{**name}", (string name, AssetFiles assets, HttpContext http) =>
{
    if (assets.Find(name) is not { } file)
    {
        return Results.NotFound();
    }

    var etag = $"\"{file.Sha256}\"";
    var headers = http.Response.Headers;
    headers.CacheControl = "public, max-age=31536000, immutable";
    headers.ETag = etag;
    headers["X-Content-Type-Options"] = "nosniff";
    if (file.ContentType == "image/svg+xml")
    {
        headers.ContentSecurityPolicy = "default-src 'none'; style-src 'none'";
    }

    return http.Request.Headers.IfNoneMatch == etag ? Results.StatusCode(StatusCodes.Status304NotModified) : Results.File(file.Bytes, file.ContentType);
});

// ---- The design system as data: what a tool, a test or another app can read. ----
var api = app.MapGroup("/api");
api.MapGet("/apps", (DesignData design) => design.Apps.Select(a => new { a.Id, a.Name, a.Tagline, a.Category, a.Accent, onAccent = a.OnAccent ?? "#ffffff", a.AccentText, a.Glyph, a.Port }));
api.MapGet("/accents", () => AccentPresets.All);
api.MapGet("/schemes", (ToolkitFiles files) => ColorSchemes.All.Select(s => new
{
    s.Id,
    s.Name,
    s.Description,
    s.DarkOnly,
    light = files.Schemes.FirstOrDefault(b => b.Id == s.Id)?.Light,
    dark = files.Schemes.FirstOrDefault(b => b.Id == s.Id)?.Dark,
}));
api.MapGet("/rules", (DesignData design) => design.Rules.Select(r => new { r.Id, r.Area, r.Title, r.Statement, level = r.Level.ToString(), r.Source, r.Check }));
api.MapGet("/tokens", (ToolkitFiles files) => files.Tokens.Select(t => new { t.Name, t.Light, t.Dark, t.Description, t.Group }));
api.MapGet("/properties", (ToolkitFiles files) => files.Properties.Select(p => new { p.Component, p.Name, p.Type, p.Fallback, p.Description }));
api.MapGet("/icons", (ToolkitFiles files) => files.IconNames);
api.MapGet("/iconlib", (IconLibrary library) => Results.Content(library.Catalogue, "application/json; charset=utf-8"));
// toolkit = the build of the site, matui = the version of the toolkit (semantic versioning: the number an app compares with its copy)
api.MapGet("/version", (ToolkitFiles files) => new { toolkit = AppVersion.Current, matui = AppVersion.MatUi, hash = files.Hash });
// What an app takes with an update: the versions after <since> (all of them without it), each with its sections and, where it breaks something, its migration steps.
api.MapGet("/changelog", (DesignData design, string? since) =>
{
    if (since is not null && !Changelog.IsVersion(since))
    {
        return Results.BadRequest(new { error = "since is a version of three numbers, like 0.1.0." });
    }

    var log = design.Changelog;
    var releases = since is null ? log.Releases : log.Since(since);
    static object Shape(ChangelogEntry e) => new
    {
        version = e.Version,
        date = e.Date,
        note = e.Note,
        breaking = e.Breaking,
        migration = e.Migration,
        sections = e.Sections.GroupBy(s => s.Name).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Items).ToList()),
    };

    return Results.Json(new
    {
        version = AppVersion.MatUi,
        since,
        upToDate = since is not null && Changelog.Compare(since, AppVersion.MatUi) >= 0,
        breaking = releases.Any(r => r.Breaking),
        unreleased = log.Unreleased is { } unreleased ? Shape(unreleased) : null,
        releases = releases.Select(Shape),
    });
});
// What changed in the rulebook (the changelog of the rules, RuleChanges): newest first; ?rule=, ?kind=, ?area= and ?since=2026-10-15 narrow it down.
api.MapGet("/rules/changes", (DesignData design, string? rule, string? kind, string? area, string? since) => RuleChangesApi.Query(design, rule, kind, area, since));

// ---- Demo endpoints of the documentation: what the examples of the select picker call (create what is missing, suggestions, FORM-16). They keep nothing, need no
// Studio and no token (nothing is changed, so there is nothing to forge: the script sends the anti-forgery token when the page has one, an app's own handler checks it). ----
api.MapPost("/demo/create", async (HttpContext http) =>
{
    var form = http.Request.HasFormContentType ? await http.Request.ReadFormAsync() : null;
    var name = (form?["name"].ToString() ?? string.Empty).Trim();
    if (name.Length == 0)
    {
        return Results.Json(new { error = "Type a name first." }, statusCode: StatusCodes.Status400BadRequest);
    }

    if (name.Length > 40)
    {
        return Results.Json(new { error = "A name has 40 characters at most." }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    if (name.Equals("error", StringComparison.OrdinalIgnoreCase))
    {
        // so that the example can show what a refusal looks like
        return Results.Json(new { error = "This demo refuses the name \"error\" on purpose, to show what a failure looks like." }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    var key = "n-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name.ToLowerInvariant())))[..8].ToLowerInvariant();
    return Results.Json(new { value = key, name, description = "Created just now" });
});
api.MapGet("/demo/suggest", (string? set) => Results.Json(new
{
    items = set switch
    {
        // an option of the example (a value), or a name that is not an option yet
        "tags" => new object[] { new { value = "finance" }, new { value = "contracts" }, new { name = "Invoices 2026" } },
        "folders" => new object[] { new { value = "documents" }, new { value = "photos" } },
        _ => [],
    },
}));

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
    if (file.StartsWith(BrandAssets.Folder + "/", StringComparison.Ordinal))
    {
        // the files of the brand: no sniffing, and an SVG that is opened on its own may do nothing
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (found.ContentType.StartsWith("image/svg+xml", StringComparison.Ordinal))
        {
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'none'";
        }
    }

    return Results.File(found.Path, found.ContentType);
});

app.MapGet("/health", () => Results.Text("ok"));
app.MapSearch();
app.MapWhiteLabel();
app.MapRazorPages();

app.Run();
return 0;

/// <summary>Makes the entry point visible to the tests that start the whole site in memory.</summary>
public partial class Program;
