using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The setup assistant with its two new steps, as a visitor meets it: the company, the template, and the administrator, and what the site is afterwards.</summary>
public sealed partial class TenantSetupTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string Password = "a sentence is a password";

    private readonly WebApplicationFactory<Program> _plain;
    private readonly List<string> _directories = [];

    public TenantSetupTests(WebApplicationFactory<Program> factory) => _plain = factory;

    public void Dispose()
    {
        foreach (var directory in _directories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex Token();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    private string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "matdesign-tenant-setup-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        return directory;
    }

    private WebApplicationFactory<Program> Studio(string? directory = null, string? template = null, string? company = null, string? adminPassword = null)
    {
        directory ??= NewDirectory();
        return _plain.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_DATA", directory);
            if (template is not null) { builder.UseSetting("MATDESIGN_TEMPLATE", template); }
            if (company is not null) { builder.UseSetting("MATDESIGN_COMPANY", company); }
            if (adminPassword is not null) { builder.UseSetting("MATDESIGN_ADMIN_PASSWORD", adminPassword); }
        });
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> Html(HttpClient client, string url) => await (await client.GetAsync(url)).Content.ReadAsStringAsync();

    private static async Task<HttpResponseMessage> Setup(HttpClient client, IDictionary<string, string> fields)
    {
        var page = await Html(client, "/Studio/Setup");
        var all = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = Token().Match(page).Groups[1].Value };
        return await client.PostAsync("/Studio/Setup", new FormUrlEncodedContent(all));
    }

    private static Dictionary<string, string> Form(string company = "Acme Industries", string template = "blank", string shortName = "") => new()
    {
        ["Company"] = company,
        ["ShortName"] = shortName,
        ["Template"] = template,
        ["Name"] = "matthias",
        ["Password"] = Password,
        ["Repeat"] = Password,
    };

    [Fact]
    public async Task The_assistant_has_three_steps_in_one_form_and_follows_the_shell_rules()
    {
        var html = await Html(Client(Studio()), "/Studio/Setup");

        Assert.Single(Heading().Matches(html));
        Assert.Contains("Set up the Studio", html);
        Assert.Equal(3, Regex.Matches(html, "data-setup-step=\"").Count);
        Assert.Contains("Step 1 of 3: your company", html);
        Assert.Contains("Step 2 of 3: where to start", html);
        Assert.Contains("Step 3 of 3: the administrator", html);
        foreach (var field in new[] { "Company", "ShortName", "Name", "Password", "Repeat" })
        {
            Assert.Contains($"id=\"{field}\"", html);
            Assert.Contains($"<label class=\"mat-label\" for=\"{field}\"", html);
        }

        Assert.DoesNotMatch(@"\sstyle\s*=", html);
        Assert.Contains("src=\"/js/setup.js\"", html);
        Assert.Single(Regex.Matches(html, "<form "));
    }

    [Fact]
    public async Task The_second_step_offers_the_two_templates_as_cards_with_their_description()
    {
        var html = await Html(Client(Studio()), "/Studio/Setup");

        Assert.Contains("<strong>Demo</strong><small>The Mat apps as a sample, to look around and change.</small>", html);
        Assert.Contains("<strong>Blank</strong><small>Start with your own brand and no apps.</small>", html);
        Assert.Matches(@"<input type=""radio"" name=""Template"" value=""demo"" checked", html);
        Assert.DoesNotMatch(@"value=""blank"" checked", html);
    }

    [Fact]
    public async Task The_environment_is_where_the_assistant_starts()
    {
        var html = await Html(Client(Studio(template: "blank", company: "Acme Industries")), "/Studio/Setup");

        Assert.Contains("value=\"Acme Industries\"", html);
        Assert.Contains("value=\"Acme\"", html);
        Assert.Matches(@"value=""blank"" checked", html);
        // the page itself already speaks for the company the environment names
        Assert.Contains("data-app=\"this-site\"", html);
    }

    [Fact]
    public async Task The_company_a_blank_template_and_the_administrator_are_made_in_one_post_and_the_site_is_the_companys()
    {
        var directory = NewDirectory();
        var factory = Studio(directory);
        var client = Client(factory);

        var response = await Setup(client, Form());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Studio", response.Headers.Location!.OriginalString);
        var file = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(directory, Tenant.FileName)));
        Assert.Equal("Acme Industries", file.GetProperty("company").GetString());
        Assert.Equal("Acme", file.GetProperty("shortName").GetString());
        Assert.Equal("blank", file.GetProperty("template").GetString());
        Assert.Equal("Acme Design", file.GetProperty("siteName").GetString());

        var design = factory.Services.GetRequiredService<DesignData>();
        Assert.Equal(TenantOrigin.Setup, design.TenantOrigin);
        Assert.Equal("Acme Design", design.SiteApp.Name);
        Assert.Empty(design.Apps);

        var home = await Html(client, "/");
        Assert.Contains("<h1>Acme Design</h1>", home);
        Assert.DoesNotContain("data-app=\"mattiverse\"", home);
        Assert.Contains("Signed in to the Studio", home);
    }

    [Fact]
    public async Task The_demo_template_keeps_the_mat_apps_and_names_the_company()
    {
        var directory = NewDirectory();
        var factory = Studio(directory);

        await Setup(Client(factory), Form("Acme Industries", "demo"));

        var design = factory.Services.GetRequiredService<DesignData>();
        Assert.True(design.IsDemo);
        Assert.Equal("Acme Industries", design.Tenant.Company);
        Assert.Equal("MatDesign", design.Tenant.SiteName);
        Assert.Equal(AppRegistry.All.Count, design.Apps.Count);
    }

    [Fact]
    public async Task What_the_assistant_wrote_is_still_there_after_a_restart()
    {
        var directory = NewDirectory();
        await Setup(Client(Studio(directory)), Form());

        var restarted = Studio(directory);
        var home = await Html(Client(restarted), "/");

        Assert.Contains("<h1>Acme Design</h1>", home);
        Assert.Equal(TenantOrigin.Setup, restarted.Services.GetRequiredService<DesignData>().TenantOrigin);
    }

    [Theory]
    [InlineData("", "blank", "Company", 1)]
    [InlineData("A", "blank", "Company", 1)]
    [InlineData("Acme <b>", "blank", "Company", 1)]
    [InlineData("Acme", "fancy", "Template", 2)]
    [InlineData("Acme", "", "Template", 2)]
    public async Task A_company_or_a_template_that_does_not_fit_is_refused_at_its_step_and_nothing_is_created(string company, string template, string field, int step)
    {
        var directory = NewDirectory();
        var client = Client(Studio(directory));

        var response = await Setup(client, Form(company, template));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("is-invalid", html.Replace("class=\"mat-error\"", string.Empty));
        Assert.Contains($"data-setup-first=\"{step}\"", html);
        Assert.Contains(field == "Company" ? "name=\"Company\"" : "name=\"Template\"", html);
        Assert.False(File.Exists(Path.Combine(directory, Tenant.FileName)));
        Assert.False(File.Exists(UserStore.PathIn(directory)));
        Assert.DoesNotContain($"value=\"{Password}", html);
    }

    [Fact]
    public async Task A_problem_with_the_password_shows_the_last_step_and_keeps_what_was_typed_before()
    {
        var directory = NewDirectory();
        var client = Client(Studio(directory));
        var form = Form();
        form["Repeat"] = "something else entirely";

        var html = await (await Setup(client, form)).Content.ReadAsStringAsync();

        Assert.Contains("data-setup-first=\"3\"", html);
        Assert.Contains("The two passwords are not the same.", html);
        Assert.Contains("value=\"Acme Industries\"", html);
        Assert.Matches(@"value=""blank"" checked", html);
        Assert.False(File.Exists(Path.Combine(directory, Tenant.FileName)));
    }

    [Fact]
    public async Task A_post_with_the_name_and_the_password_only_creates_the_administrator_and_the_site_stays_the_demo()
    {
        // what a script or an old client sends: no company, no template
        var directory = NewDirectory();
        var factory = Studio(directory);
        var response = await Setup(Client(factory), new Dictionary<string, string> { ["Name"] = "matthias", ["Password"] = Password, ["Repeat"] = Password });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(File.Exists(UserStore.PathIn(directory)));
        Assert.False(File.Exists(Path.Combine(directory, Tenant.FileName)));
        var design = factory.Services.GetRequiredService<DesignData>();
        Assert.Equal(TenantOrigin.Default, design.TenantOrigin);
        Assert.Equal(Tenant.Demo, design.Tenant);
        Assert.Equal(AppRegistry.All.Count, design.Apps.Count);
    }

    [Fact]
    public async Task A_post_without_the_company_follows_the_environment_when_it_names_one()
    {
        var factory = Studio(template: "blank", company: "Acme Industries");
        await Setup(Client(factory), new Dictionary<string, string> { ["Name"] = "matthias", ["Password"] = Password, ["Repeat"] = Password });

        var design = factory.Services.GetRequiredService<DesignData>();
        Assert.Equal(TenantOrigin.Environment, design.TenantOrigin);
        Assert.Equal("Acme Design", design.SiteApp.Name);
        Assert.Empty(design.Apps);
    }

    [Fact]
    public async Task The_password_of_the_environment_skips_the_assistant_and_the_site_is_the_demo_unless_the_environment_says_more()
    {
        var plain = Studio(adminPassword: "set from outside");
        Assert.Equal(HttpStatusCode.Redirect, (await Client(plain).GetAsync("/Studio/Setup")).StatusCode);
        Assert.Equal(Tenant.Demo, plain.Services.GetRequiredService<DesignData>().Tenant);

        var company = Studio(template: "blank", company: "Acme", adminPassword: "set from outside");
        Assert.Equal("Acme Design", company.Services.GetRequiredService<DesignData>().SiteApp.Name);
    }

    [Fact]
    public async Task The_release_compose_file_still_has_no_variables_and_no_env_file()
    {
        var compose = await File.ReadAllTextAsync(Path.Combine(TestPaths.RepoRoot, "docker-compose.yml"));

        Assert.DoesNotContain("MATDESIGN_TEMPLATE", compose);
        Assert.DoesNotContain("MATDESIGN_COMPANY", compose);
        Assert.DoesNotContain("env_file", compose);
        Assert.DoesNotMatch(@"\$\{", compose);
    }
}

/// <summary>
/// A Studio on a blank start and on the demo, signed in: the Company page, its history and restore, the checks, the releases that name the company, and every page of the Studio with the shell rules kept
/// for a company that has no apps and for one that has.
/// </summary>
public sealed partial class TenantStudioTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly StudioHarness _studio;

    public TenantStudioTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _studio = new StudioHarness(factory, new Dictionary<string, string> { ["MATDESIGN_TEMPLATE"] = "blank", ["MATDESIGN_COMPANY"] = "Acme Industries" });
    }

    public void Dispose() => _studio.Dispose();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"<title>([^<]*)</title>")]
    private static partial Regex Title();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"<td\b([^>]*)>")]
    private static partial Regex TableCell();

    private static Dictionary<string, string> Names(string company = "Acme Industries", string shortName = "Acme", string siteName = "Acme Design", string tagline = "Our look", string accent = "#2563eb") => new()
    {
        ["Input.Company"] = company,
        ["Input.ShortName"] = shortName,
        ["Input.SiteName"] = siteName,
        ["Input.Tagline"] = tagline,
        ["Input.Accent"] = accent,
    };

    private DesignData Design => _studio.Design;

    // ---- the page

    [Fact]
    public async Task The_company_page_shows_the_tenant_and_follows_the_shell_rules()
    {
        await _studio.SignIn();
        var html = await _studio.Get("/Studio/Company");

        Assert.Single(Heading().Matches(html));
        Assert.Contains("data-nav-panel=\"studio\"", html);
        Assert.Contains("value=\"Acme Industries\"", html);
        Assert.Contains("value=\"Acme Design\"", html);
        Assert.Contains("name=\"Input.Accent\"", html);
        Assert.DoesNotMatch(@"\sstyle\s*=", html);
        Assert.Contains("href=\"/Studio/Company\"", html);
        Assert.Contains("aria-current=\"page\"", html);
    }

    [Fact]
    public async Task The_accent_is_asked_for_only_while_the_template_is_blank()
    {
        await _studio.SignIn();
        Assert.Contains("name=\"Input.Accent\"", await _studio.Get("/Studio/Company"));

        _studio.Store.Save(Tenant.Kind, Tenant.ItemId, Tenant.Demo, "admin", "Created");
        Design.Reload();

        Assert.DoesNotContain("name=\"Input.Accent\"", await _studio.Get("/Studio/Company"));
    }

    [Fact]
    public async Task Saving_the_names_writes_a_revision_and_the_site_follows()
    {
        await _studio.SignIn();

        var response = await _studio.Post("/Studio/Company", "/Studio/Company", Names(company: "Acme Industries GmbH", siteName: "Acme Brand", tagline: "Our look, our rules", accent: "#0f766e"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var item = _studio.Store.Current(Tenant.Kind, Tenant.ItemId)!;
        Assert.Equal(1, item.Number);
        Assert.StartsWith("Created: ", item.Note);
        Assert.Equal(TenantOrigin.Studio, Design.TenantOrigin);
        Assert.Equal("Acme Industries GmbH", Design.Tenant.Company);

        var home = await _studio.Get("/");
        Assert.Contains("<h1>Acme Brand</h1>", home);
        Assert.Contains("class=\"mat-nav__tagline\">Our look, our rules<", home);
        Assert.Contains($"class=\"mat-nav__version\">Acme Brand {AppVersion.Current}<", home);
        Assert.Contains("<meta name=\"theme-color\" content=\"#0f766e\" />", home);
        Assert.Contains("[data-app=\"this-site\"] { --mat-app-accent: #0f766e; }", await _studio.Get("/ui/matui.css"));
    }

    [Fact]
    public async Task A_second_save_is_a_second_revision_and_says_what_changed()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company", Names());
        await _studio.Post("/Studio/Company", "/Studio/Company", Names(tagline: "Another line", siteName: "Acme House"));

        var item = _studio.Store.Current(Tenant.Kind, Tenant.ItemId)!;
        Assert.Equal(2, item.Number);
        Assert.Equal("Changed site name, tagline", item.Note);
    }

    [Theory]
    [InlineData("Input.Company", "A", "2 to 60")]
    [InlineData("Input.ShortName", "a short name that is much too long", "1 to 16")]
    [InlineData("Input.SiteName", "x", "2 to 40")]
    [InlineData("Input.Accent", "blue", "#2563eb")]
    [InlineData("Input.Accent", "#ffff00", "needs")]
    public async Task A_name_or_an_accent_that_does_not_fit_stays_on_the_page_with_what_was_typed_and_nothing_is_saved(string field, string value, string message)
    {
        await _studio.SignIn();
        var form = Names();
        form[field] = value;

        var response = await _studio.Post("/Studio/Company", "/Studio/Company", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is-invalid", html);
        Assert.Null(_studio.Store.Current(Tenant.Kind, Tenant.ItemId));
        Assert.Equal(TenantOrigin.Environment, Design.TenantOrigin);
    }

    [Fact]
    public async Task Check_shows_that_the_form_would_pass_and_saves_nothing()
    {
        await _studio.SignIn();

        var response = await _studio.Post("/Studio/Company", "/Studio/Company?check=true", Names());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("All checks pass.", html);
        Assert.Null(_studio.Store.Current(Tenant.Kind, Tenant.ItemId));
    }

    // ---- the template

    [Fact]
    public async Task The_page_asks_before_the_template_is_switched_and_says_what_is_hidden_and_that_nothing_is_deleted()
    {
        await _studio.SignIn();
        var blank = await _studio.Get("/Studio/Company");
        Assert.Contains("data-confirm=\"Switch to the demo template?", blank);
        Assert.Contains("Nothing of the site is hidden.", blank);
        Assert.Contains("Nothing is deleted", blank);

        await _studio.Post("/Studio/Company", "/Studio/Company?handler=Switch", new Dictionary<string, string>());
        var demo = await _studio.Get("/Studio/Company");
        Assert.Contains("data-confirm=\"Switch to the blank template?", demo);
        Assert.Contains("The site stops offering the Mat apps with their accents, the audit of their repositories, the Mat brand and the sample apps of the patterns and of the colour page.", demo);
    }

    [Fact]
    public async Task Switching_to_the_demo_brings_the_mat_apps_back_and_switching_again_hides_them_and_keeps_what_the_studio_saved()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", StudioHarness.AppForm());
        Assert.Equal(["matx"], Design.Apps.Select(a => a.Id));

        var toDemo = await _studio.Post("/Studio/Company", "/Studio/Company?handler=Switch", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, toDemo.StatusCode);
        Assert.True(Design.IsDemo);
        Assert.Equal(AppRegistry.All.Count + 1, Design.Apps.Count);
        Assert.Equal("matdesign", Design.SiteApp.Id);
        Assert.Contains("data-app=\"mattiverse\"", await _studio.Get("/"));
        Assert.Contains("href=\"/Identity/Audit\"", await _studio.Get("/"));
        var revision = _studio.Store.Current(Tenant.Kind, Tenant.ItemId)!;
        Assert.StartsWith("Switched to the demo template: template", revision.Note);

        await _studio.Post("/Studio/Company", "/Studio/Company?handler=Switch", new Dictionary<string, string>());

        Assert.False(Design.IsDemo);
        Assert.Equal(["matx"], Design.Apps.Select(a => a.Id));
        Assert.DoesNotContain("data-app=\"mattiverse\"", await _studio.Get("/"));
        Assert.NotNull(_studio.Store.Current("apps", "matx"));
        Assert.Equal(2, _studio.Store.Current(Tenant.Kind, Tenant.ItemId)!.Number);
        Assert.Equal("#2563eb", Design.Tenant.Accent);
    }

    [Fact]
    public async Task The_demo_site_of_a_company_that_named_it_differently_says_that_name_in_the_shell_and_keeps_the_colour_of_the_demo()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company?handler=Switch", new Dictionary<string, string>());

        var home = await _studio.Get("/");

        Assert.Contains("class=\"mat-nav__name\">Acme Design<", home);
        Assert.Contains("<title>Overview · Acme Design</title>", home);
        Assert.Contains("aria-label=\"Acme Design\"", home);
        Assert.Matches(@"<html lang=""en"" data-app=""matdesign""\s*>", home);
        Assert.Contains("<h1>Acme Design</h1>", home);
    }

    [Fact]
    public async Task An_app_added_in_the_studio_is_the_first_one_of_a_blank_site_and_shows_everywhere()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", StudioHarness.AppForm("matx"));

        var home = await _studio.Get("/");
        Assert.Contains("data-app=\"matx\"", home);
        Assert.DoesNotContain("No apps yet", home);
        Assert.Contains("1 apps</a>", home);
        var identity = await _studio.Get("/Identity");
        Assert.Contains("<table", identity);
        Assert.Contains("MatX", identity);
        Assert.Contains("[data-app=\"matx\"] { --mat-app-accent: #0f766e; }", await _studio.Get("/ui/matui.css"));
        var apiApps = await _studio.Get("/api/apps");
        Assert.Contains("\"id\":\"matx\"", apiApps);
        Assert.DoesNotContain("mattiverse", apiApps);
        // the palette of one app has no pair, the frames of the patterns use the app
        Assert.Contains("app=matx&amp;view=list", await _studio.Get("/Patterns/Detail?id=list"));
    }

    // ---- history and restore

    [Fact]
    public async Task The_company_has_a_history_page_a_diff_and_a_restore_like_the_other_items()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company", Names());
        await _studio.Post("/Studio/Company", "/Studio/Company", Names(siteName: "Acme House", tagline: "Other"));

        var history = await _studio.Get("/Studio/History?kind=tenant&id=tenant");
        Assert.Contains("Acme Industries", history);
        Assert.Contains("Changed site name, tagline", history);
        Assert.Contains("href=\"/Studio/Company\"", history);

        var restore = await _studio.Post("/Studio/History?kind=tenant&id=tenant", "/Studio/History?handler=Restore&kind=tenant&id=tenant&revision=1", new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);

        var current = _studio.Store.Current(Tenant.Kind, Tenant.ItemId)!;
        Assert.Equal(3, current.Number);
        Assert.Equal("Acme Design", Design.Tenant.SiteName);
        Assert.Equal("Our look", Design.Tenant.Tagline);
        Assert.Equal("Acme Design", Design.SiteApp.Name);
    }

    [Fact]
    public async Task A_revision_that_would_not_be_accepted_today_is_not_restored()
    {
        // the first revision has an accent that reaches AA; the second one is saved by hand with one that does not (a volume edited by a person)
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company", Names());
        _studio.Store.Save(Tenant.Kind, Tenant.ItemId, Design.Tenant with { Accent = "#ffff00" }, "admin", "By hand");
        await _studio.Post("/Studio/Company", "/Studio/Company", Names(siteName: "Acme House"));
        _studio.Design.Reload();
        var history = _studio.Factory.Services.GetRequiredService<StudioHistory>();

        var two = _studio.Store.History(Tenant.Kind, Tenant.ItemId).First(r => r.Number == 2);

        Assert.NotEmpty(history.CanRestore(Tenant.Kind, Tenant.ItemId, two));
        Assert.Empty(history.CanRestore(Tenant.Kind, Tenant.ItemId, _studio.Store.History(Tenant.Kind, Tenant.ItemId).First(r => r.Number == 1)));
    }

    [Fact]
    public async Task The_activity_list_names_the_company_with_a_link_to_its_page()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company", Names());

        var activity = await _studio.Get("/Studio/Activity");

        Assert.Contains("Tenant", activity);
        Assert.Contains("kind=tenant", activity);
        Assert.Contains("Created: tagline", activity);
    }

    // ---- checks

    [Fact]
    public async Task The_company_check_passes_for_both_templates_and_is_part_of_the_report()
    {
        Assert.Contains(_studio.Checks.Report.Results, r => r.Check.Id == "tenant" && r.Passed);
        Assert.True(_studio.Checks.Report.Passed, string.Join("\n", _studio.Checks.Report.Findings.Where(f => f.Severity == Severity.Error).Select(f => f.Message)));

        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company?handler=Switch", new Dictionary<string, string>());

        Assert.True(_studio.Checks.Report.Passed, string.Join("\n", _studio.Checks.Report.Findings.Where(f => f.Severity == Severity.Error).Select(f => f.Message)));
        Assert.Contains("The company is valid", await _studio.Get("/Studio/Checks"));
    }

    [Fact]
    public void A_site_that_kept_the_name_of_the_demo_gets_advice_when_it_is_not_the_demo()
    {
        _studio.Store.Save(Tenant.Kind, Tenant.ItemId, Design.Tenant with { SiteName = "MatDesign" }, "admin", "Created");
        Design.Reload();

        var advice = _studio.Checks.Report.Results.Single(r => r.Check.Id == "tenant").Findings;

        Assert.Single(advice);
        Assert.Equal(Severity.Advice, advice[0].Severity);
        Assert.Equal("/Studio/Company", _studio.Checks.UrlOf(advice[0]));
    }

    [Fact]
    public void A_company_on_the_volume_that_is_not_valid_is_left_out_and_reported_and_the_site_goes_on()
    {
        _studio.Store.Save(Tenant.Kind, Tenant.ItemId, new Tenant(string.Empty, string.Empty, "fancy", string.Empty, string.Empty), "admin", "Created");
        Design.Reload();

        Assert.Equal(TenantOrigin.Environment, Design.TenantOrigin);
        Assert.Equal("Acme Industries", Design.Tenant.Company);
        Assert.Contains(_studio.Checks.Report.Findings, f => f.Check == "volume" && f.Kind == Tenant.Kind && f.Severity == Severity.Error);
    }

    [Fact]
    public async Task A_tenant_file_that_cannot_be_read_leaves_the_site_as_the_default_and_the_checks_say_so()
    {
        await File.WriteAllTextAsync(Path.Combine(_studio.DataDirectory, Tenant.FileName), "garbage");
        Design.Reload();

        Assert.Contains(_studio.Checks.Report.Findings, f => f.Check == "volume" && f.Message.Contains("is not a valid tenant file"));
    }

    // ---- releases

    [Fact]
    public async Task A_release_names_the_company_in_its_manifest_its_design_data_its_changelog_its_styleguide_and_its_archive()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company", Names());

        var run = _studio.Releases.Execute("0.1.0", "For the company.", "admin");

        Assert.True(run.Succeeded, string.Join("\n", run.Stages.SelectMany(s => s.Log)));
        Assert.Contains(run.Stages.SelectMany(s => s.Log), l => l.StartsWith("Acme Design: ", StringComparison.Ordinal));
        var directory = Path.Combine(_studio.DataDirectory, "releases", "0.1.0");

        var manifest = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(directory, "manifest.json")));
        Assert.Equal("Acme Industries", manifest.GetProperty("company").GetString());

        var design = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(directory, "design.json")));
        var tenant = design.GetProperty("tenant");
        Assert.Equal("Acme Industries", tenant.GetProperty("company").GetString());
        Assert.Equal("Acme Design", tenant.GetProperty("siteName").GetString());
        Assert.Equal("blank", tenant.GetProperty("template").GetString());
        Assert.Equal(0, design.GetProperty("apps").GetArrayLength());

        var styleguide = await File.ReadAllTextAsync(Path.Combine(directory, "styleguide.html"));
        Assert.Contains("<title>Acme Design: MatUi 0.1.0 – styleguide</title>", styleguide);
        Assert.Contains("The design system of Acme Industries.", styleguide);
        Assert.Contains("This release has no apps yet.", styleguide);

        using var zip = ZipFile.OpenRead(Path.Combine(directory, "matui-0.1.0.zip"));
        using var reader = new StreamReader(zip.GetEntry("matui/README.txt")!.Open());
        var readme = await reader.ReadToEndAsync();
        Assert.Contains("Made by Acme Design", readme);
        Assert.Contains("The UI toolkit of the Acme apps.", readme);
        Assert.DoesNotContain("MatDesign", readme);

        var css = await File.ReadAllTextAsync(Path.Combine(directory, "matui.css"));
        Assert.Contains("[data-app=\"this-site\"] { --mat-app-accent: #2563eb; }", css);
    }

    [Fact]
    public async Task A_release_of_the_demo_is_what_it_was_and_still_names_the_company()
    {
        var demo = new StudioHarness(_factory, new Dictionary<string, string> { ["MATDESIGN_TEMPLATE"] = "demo" });
        try
        {
            var run = demo.Releases.Execute("0.1.0", string.Empty, "admin");
            Assert.True(run.Succeeded, string.Join("\n", run.Stages.SelectMany(s => s.Log)));
            var directory = Path.Combine(demo.DataDirectory, "releases", "0.1.0");
            var styleguide = await File.ReadAllTextAsync(Path.Combine(directory, "styleguide.html"));
            Assert.Contains("<title>MatUi 0.1.0 – styleguide</title>", styleguide);
            Assert.DoesNotContain("The design system of", styleguide);
            var design = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(directory, "design.json")));
            Assert.Equal("MatDesign", design.GetProperty("tenant").GetProperty("siteName").GetString());
            Assert.Equal(AppRegistry.All.Count, design.GetProperty("apps").GetArrayLength());
        }
        finally
        {
            demo.Dispose();
        }
    }

    // ---- every page of the Studio, for a company with no app and for one with an app

    private static IEnumerable<string> Pages()
    {
        yield return "/Studio";
        yield return "/Studio/Company";
        yield return "/Studio/Apps";
        yield return "/Studio/Apps?q=nothing-matches-this";
        yield return "/Studio/Apps/Edit";
        yield return "/Studio/Rules";
        yield return "/Studio/Rules/Edit";
        yield return "/Studio/Rules/Edit?id=SHELL-01";
        yield return "/Studio/Components";
        yield return "/Studio/Components/Edit?id=buttons";
        yield return "/Studio/Activity";
        yield return "/Studio/History?kind=tenant&id=tenant";
        yield return "/Studio/Checks";
        yield return "/Studio/Releases";
        yield return "/Studio/Releases/New";
        yield return "/Studio/Versions";
        yield return "/Studio/Brand";
    }

    private async Task<List<string>> Crawl(IEnumerable<string> pages)
    {
        var problems = new List<string>();
        foreach (var url in pages)
        {
            var response = await _studio.Client.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                problems.Add($"{url}: {(int)response.StatusCode}");
                continue;
            }

            if (Title().Match(html) is not { Success: true } title || title.Groups[1].Value.Trim().Length < 3) { problems.Add($"{url}: no title"); }
            if (!title_ends_with_site(html)) { problems.Add($"{url}: the title does not end with the site name"); }
            if (Heading().Matches(html).Count != 1) { problems.Add($"{url}: {Heading().Matches(html).Count} h1"); }
            if (!html.Contains("class=\"mat-nav\"")) { problems.Add($"{url}: no menu"); }
            if (!html.Contains("data-nav-panel=\"studio\"")) { problems.Add($"{url}: no Studio menu"); }
            if (!html.Contains("class=\"mat-nav__brand\"")) { problems.Add($"{url}: no logo above the menu"); }
            if (!html.Contains("class=\"mat-crumbs\"")) { problems.Add($"{url}: no breadcrumb (SHELL-08)"); }
            if (!html.Contains("data-theme-toggle")) { problems.Add($"{url}: no theme button (SHELL-04)"); }
            if (!html.Contains("class=\"mat-user\"")) { problems.Add($"{url}: no user (SHELL-05)"); }
            if (!html.Contains("class=\"mat-nav__version\"")) { problems.Add($"{url}: no version (SHELL-06)"); }
            if (!html.Contains("<main class=\"mat-page")) { problems.Add($"{url}: no main landmark"); }
            if (InlineStyle().IsMatch(html)) { problems.Add($"{url}: an inline style (CODE-02)"); }

            var ids = IdAttribute().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
            foreach (var twice in ids.GroupBy(i => i).Where(g => g.Count() > 1))
            {
                problems.Add($"{url}: the id {twice.Key} is on the page twice");
            }

            foreach (Match cell in TableCell().Matches(html))
            {
                if (!cell.Groups[1].Value.Contains("data-label=") && !cell.Groups[1].Value.Contains("mat-table__cell-actions"))
                {
                    problems.Add($"{url}: a cell without data-label: {cell.Value}");
                }
            }
        }

        return problems;

        static bool title_ends_with_site(string html) => Title().Match(html).Groups[1].Value.EndsWith("Acme Design", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_page_of_the_studio_of_a_company_without_apps_follows_the_shell_rules()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company", Names());
        Assert.Empty(Design.Apps);

        var problems = await Crawl(Pages());

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task Every_page_of_the_studio_of_a_company_with_an_app_and_a_release_follows_the_shell_rules()
    {
        await _studio.SignIn();
        await _studio.Post("/Studio/Company", "/Studio/Company", Names());
        await _studio.Post("/Studio/Apps/Edit", "/Studio/Apps/Edit", StudioHarness.AppForm());
        Assert.True(_studio.Releases.Execute("0.1.0", "For the page tests.", "admin").Succeeded);
        var run = _studio.Releases.Runs(1)[0].Id;

        var problems = await Crawl(Pages().Concat(["/Studio/Apps/Edit?id=matx", "/Studio/History?kind=apps&id=matx", $"/Studio/Releases/Run?id={run}", "/Studio/Versions/Edit?id=matx"]));

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task The_studio_of_a_blank_site_has_an_empty_state_with_the_way_out_where_the_apps_would_be()
    {
        await _studio.SignIn();

        var apps = await _studio.Get("/Studio/Apps");

        Assert.Contains("mat-empty", apps);
        Assert.Contains("No apps yet", apps);
        Assert.Contains("href=\"/Studio/Apps/Edit\"", apps);
        Assert.DoesNotContain("<table", apps);
        var versions = await _studio.Get("/Studio/Versions");
        Assert.Contains("mat-empty", versions);
    }

    [Fact]
    public async Task The_login_and_the_sign_in_say_the_name_of_the_site()
    {
        var login = await _studio.Html("/Studio/Login");

        Assert.Contains("Maintain and create what Acme Design shows.", login);
        Assert.Contains("data-app=\"this-site\"", login);
        Assert.DoesNotContain("MatDesign", login);
    }
}
