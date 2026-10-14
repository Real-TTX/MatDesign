using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.Extensions.Configuration;

namespace MatDesign.Tests;

/// <summary>The tenant itself (docs/PRODUCT.md, stage 14): what a company has to give, what is made of what it leaves empty, the file the setup assistant writes, and the templates.</summary>
public sealed class TenantTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "matdesign-tenant-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static Tenant Acme(string template = DesignTemplates.BlankId)
        => TenantValidator.Normalize(Tenant.For("Acme Industries", template));

    // ---- what a tenant is made of

    [Fact]
    public void A_company_gets_the_names_the_site_would_give_it_itself()
    {
        var blank = Acme();

        Assert.Equal("Acme Industries", blank.Company);
        Assert.Equal("Acme", blank.ShortName);
        Assert.Equal("Acme Design", blank.SiteName);
        Assert.Equal("Identity of the Acme apps", blank.Tagline);
        Assert.Equal("#2563eb", blank.Accent);
        Assert.False(blank.IsDemo);
        Assert.Equal("Acme apps", blank.Apps);
        Assert.Equal("Acme app", blank.App);
        Assert.Equal("Acme family", blank.Family);
    }

    [Fact]
    public void The_demo_tenant_is_what_the_site_always_called_itself()
    {
        var demo = Tenant.Demo;

        Assert.True(demo.IsDemo);
        Assert.Equal("MatDesign", demo.SiteName);
        Assert.Equal("Identity of the Mat apps", demo.Tagline);
        Assert.Equal("Mat apps", demo.Apps);
        Assert.Equal("Mat app", demo.App);
        Assert.Equal("Mat family", demo.Family);
        Assert.Empty(TenantValidator.Validate(TenantValidator.Normalize(demo)));
        Assert.Equal(demo, TenantValidator.Normalize(demo));
    }

    [Fact]
    public void The_site_name_of_the_demo_template_is_MatDesign_whatever_the_company()
    {
        var demo = Acme(DesignTemplates.DemoId);

        Assert.Equal("MatDesign", demo.SiteName);
        Assert.Equal("Acme", demo.ShortName);
        Assert.Equal(string.Empty, demo.Accent);
    }

    [Theory]
    [InlineData("Acme", "Acme")]
    [InlineData("  Acme   Industries  GmbH ", "Acme")]
    [InlineData("Donaudampfschifffahrtsgesellschaft", "Donaudampfschiff")]
    [InlineData("", "")]
    public void The_short_name_is_the_first_word_shortened_to_what_fits(string company, string expected)
        => Assert.Equal(expected, Tenant.ShortNameOf(company));

    [Fact]
    public void Normalizing_trims_collapses_spaces_and_fills_what_was_left_empty()
    {
        var tenant = TenantValidator.Normalize(new Tenant("  Acme   Industries ", " ", " BLANK ", "", "  A  tagline  ", " #2563EB "));

        Assert.Equal("Acme Industries", tenant.Company);
        Assert.Equal("Acme", tenant.ShortName);
        Assert.Equal("blank", tenant.Template);
        Assert.Equal("Acme Design", tenant.SiteName);
        Assert.Equal("A tagline", tenant.Tagline);
        Assert.Equal("#2563eb", tenant.Accent);
    }

    [Fact]
    public void A_blank_tenant_without_an_accent_gets_the_neutral_one_and_a_demo_tenant_none()
    {
        Assert.Equal("#2563eb", TenantValidator.Normalize(new Tenant("Acme", "Acme", "blank", "Acme Design", string.Empty)).Accent);
        Assert.Equal(string.Empty, TenantValidator.Normalize(new Tenant("Acme", "Acme", "demo", "MatDesign", string.Empty)).Accent);
    }

    // ---- what a tenant has to be

    [Fact]
    public void A_good_tenant_has_no_problem_in_either_template()
    {
        Assert.Empty(TenantValidator.Validate(Acme()));
        Assert.Empty(TenantValidator.Validate(Acme(DesignTemplates.DemoId)));
        Assert.True(TenantValidator.IsSafe(Acme()));
    }

    [Theory]
    [InlineData("Company", "A", "2 to 60")]
    [InlineData("Company", "", "2 to 60")]
    [InlineData("ShortName", "A very long short name indeed", "1 to 16")]
    [InlineData("SiteName", "x", "2 to 40")]
    [InlineData("Template", "fancy", "demo or blank")]
    [InlineData("Accent", "blue", "#2563eb")]
    [InlineData("Tagline", "a tagline that is far too long to fit in the one line under the logo of the menu, so it is refused with a note", "90 characters")]
    public void What_does_not_fit_is_refused_with_a_message_that_says_how_to_fix_it(string field, string value, string message)
    {
        var tenant = Acme();
        tenant = field switch
        {
            "Company" => tenant with { Company = value },
            "ShortName" => tenant with { ShortName = value },
            "SiteName" => tenant with { SiteName = value },
            "Template" => tenant with { Template = value },
            "Accent" => tenant with { Accent = value },
            _ => tenant with { Tagline = value },
        };

        var problems = TenantValidator.Validate(tenant);

        Assert.Contains(problems, p => p.Field == field && p.Message.Contains(message));
    }

    [Theory]
    [InlineData("Acme <b>Corp</b>")]
    [InlineData("Acme\nCorp")]
    [InlineData("Acme\u0000Corp")]
    public void Names_are_text_and_never_markup_or_control_characters(string company)
    {
        var tenant = Acme() with { Company = company };

        Assert.Contains(TenantValidator.Validate(tenant), p => p.Field == "Company");
        Assert.False(TenantValidator.IsSafe(tenant));
    }

    [Fact]
    public void The_accent_of_a_blank_site_has_to_reach_AA_like_the_accent_of_any_app()
    {
        // yellow with white text is 1.07:1
        var problems = TenantValidator.Validate(Acme() with { Accent = "#ffff00" });

        Assert.NotEmpty(problems);
        Assert.All(problems, p => Assert.Equal("Accent", p.Field));
    }

    [Fact]
    public void The_demo_does_not_use_the_accent_so_it_is_not_held_to_AA()
        => Assert.DoesNotContain(TenantValidator.Validate(Acme(DesignTemplates.DemoId) with { Accent = "#ffff00" }), p => p.Field == "Accent");

    [Fact]
    public void An_unsafe_tenant_is_not_safe_and_an_unknown_template_is_not_one()
    {
        Assert.False(TenantValidator.IsSafe(Acme() with { Template = "x" }));
        Assert.False(TenantValidator.IsSafe(Acme() with { Accent = "red" }));
        Assert.False(TenantValidator.IsSafe(Acme() with { SiteName = string.Empty }));
        Assert.False(TenantValidator.IsSafe(Acme() with { Company = new string('x', 61) }));
    }

    // ---- the file the setup assistant writes

    [Fact]
    public void The_file_is_written_and_read_back_and_nothing_is_left_beside_it()
    {
        Assert.Null(Tenant.ReadFile(_directory));

        var tenant = Acme();
        tenant.WriteFile(_directory);

        Assert.Equal(tenant, Tenant.ReadFile(_directory));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.Contains("Acme Industries", File.ReadAllText(Path.Combine(_directory, Tenant.FileName)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"company\":\"\",\"shortName\":\"\",\"template\":\"blank\",\"siteName\":\"\",\"tagline\":\"\"}")]
    [InlineData("{\"company\":\"Acme\",\"shortName\":\"Acme\",\"template\":\"fancy\",\"siteName\":\"Acme Design\",\"tagline\":\"\"}")]
    public void A_file_that_cannot_be_read_is_an_error_and_never_no_tenant(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, Tenant.FileName), content);

        Assert.Throws<InvalidDataException>(() => Tenant.ReadFile(_directory));
    }

    [Fact]
    public void A_file_with_only_the_names_is_completed_like_an_item_is()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, Tenant.FileName), "{\"company\":\"Acme Industries\",\"template\":\"blank\"}");

        var read = Tenant.ReadFile(_directory)!;

        Assert.Equal("Acme", read.ShortName);
        Assert.Equal("Acme Design", read.SiteName);
        Assert.Equal("#2563eb", read.Accent);
    }

    // ---- the templates

    [Fact]
    public void There_are_two_templates_and_an_unknown_id_is_not_one()
    {
        Assert.Equal(["demo", "blank"], DesignTemplates.All.Select(t => t.Id));
        Assert.Null(DesignTemplates.Find("fancy"));
        Assert.Null(DesignTemplates.Find(null));
        Assert.Same(DesignTemplates.Demo, DesignTemplates.Get("fancy"));
        Assert.Same(DesignTemplates.Blank, DesignTemplates.Get("blank"));
    }

    [Fact]
    public void The_cards_of_the_assistant_say_what_each_template_is()
    {
        Assert.Equal("The Mat apps as a sample, to look around and change.", DesignTemplates.Demo.Description);
        Assert.Equal("Start with your own brand and no apps.", DesignTemplates.Blank.Description);
        Assert.All(DesignTemplates.All, t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
    }

    [Fact]
    public void The_demo_template_is_the_registry_as_it_is_and_the_blank_one_has_no_apps()
    {
        Assert.Same(AppRegistry.All, DesignTemplates.Demo.Apps);
        Assert.Empty(DesignTemplates.Blank.Apps);
        Assert.True(DesignTemplates.Demo.OffersAudit);
        Assert.True(DesignTemplates.Demo.OffersSampleApps);
        Assert.False(DesignTemplates.Blank.OffersAudit);
        Assert.False(DesignTemplates.Blank.OffersSampleApps);
        Assert.NotEmpty(DesignTemplates.Demo.Offers);
        Assert.Empty(DesignTemplates.Blank.Offers);
    }

    [Fact]
    public void The_site_of_the_demo_is_the_matdesign_app_and_the_site_of_a_blank_start_is_made_from_the_tenant()
    {
        var demo = DesignTemplates.Demo.SiteApp(Tenant.Demo, AppRegistry.All);
        Assert.Equal("matdesign", demo.Id);
        Assert.Equal("#d6336c", demo.Accent);

        var blank = DesignTemplates.Blank.SiteApp(Acme() with { SiteName = "Acme Design", Tagline = "Our look" }, []);
        Assert.Equal(DesignTemplates.SiteAppId, blank.Id);
        Assert.Equal("Acme Design", blank.Name);
        Assert.Equal("Our look", blank.Tagline);
        Assert.Equal("#2563eb", blank.Accent);
        Assert.Equal("palette", blank.Glyph);
    }

    [Fact]
    public void The_site_of_the_demo_takes_the_name_the_company_gave_it_and_the_name_of_the_registry_otherwise()
    {
        var renamed = DesignTemplates.Demo.SiteApp(Tenant.Demo with { SiteName = "Acme Design" }, AppRegistry.All);
        Assert.Equal("Acme Design", renamed.Name);
        Assert.Equal("matdesign", renamed.Id);
        Assert.Equal("#d6336c", renamed.Accent);

        // the Studio changed the matdesign app: the shell follows it as long as the company did not name the site itself
        var changed = AppRegistry.All.Select(a => a.Id == "matdesign" ? a with { Name = "MatDesign 2" } : a).ToList();
        Assert.Equal("MatDesign 2", DesignTemplates.Demo.SiteApp(Tenant.Demo, changed).Name);
    }

    [Fact]
    public void The_id_of_the_site_of_a_blank_start_can_never_be_the_id_of_an_app_a_company_adds()
    {
        var site = DesignTemplates.Blank.SiteApp(Acme(), []);

        // an app of the Studio has an id of two to 24 lower-case letters: a dash is not one
        Assert.False(AppValidator.IsSafe(site));
        Assert.Contains(AppValidator.Validate(AppValidator.Normalize(site), [], ["palette"]), p => p.Field == "Id");
    }

    [Fact]
    public void The_neutral_accent_of_a_blank_start_reaches_AA_in_light_and_dark()
    {
        var site = DesignTemplates.Blank.SiteApp(Acme(), []);

        Assert.Empty(AppValidator.Contrast(site));
        Assert.All(AccentReport.For(site).Checks().Where(c => !c.Advisory), c => Assert.True(c.Passes, c.Name));
    }

    [Fact]
    public void The_brand_of_a_blank_start_is_neutral_and_carries_the_names_of_the_company()
    {
        var brand = DesignTemplates.Blank.BrandFor(Acme());

        Assert.Equal("Acme Industries", brand.Company);
        Assert.Equal("Acme", brand.ShortName);
        Assert.False(brand.HasOverrides);
        Assert.Same(Brand.BuiltIn, DesignTemplates.Demo.BrandFor(Tenant.Demo));
    }

    // ---- the rule

    [Fact]
    public void IDENT_09_is_the_owners_rule_that_a_program_names_its_surfaces_after_its_company()
    {
        var rule = RuleBook.Get("IDENT-09")!;

        Assert.Equal("IDENT", rule.Area);
        Assert.Equal(RuleLevel.Must, rule.Level);
        Assert.Equal(RuleBook.Owner, rule.Source);
        Assert.True(rule.Statement.Length <= 1200, $"the statement has {rule.Statement.Length} characters");
        Assert.Contains("never after another company", rule.Statement);
        Assert.Contains("docs/PRODUCT.md", rule.Why);
        Assert.Empty(RuleValidator.Validate(rule, RuleBook.All.Where(r => r.Id != rule.Id)));
    }

    // ---- the menu follows the template

    [Fact]
    public void The_menu_offers_the_audit_only_when_the_template_has_one()
    {
        var withAudit = Navigation.Create(NavFacts.BuiltIn).SelectMany(s => s.Pages).Select(p => p.Href);
        var without = Navigation.Create(NavFacts.BuiltIn with { Audit = false }).SelectMany(s => s.Pages).Select(p => p.Href);

        Assert.Contains("/Identity/Audit", withAudit);
        Assert.DoesNotContain("/Identity/Audit", without);
        Assert.Equal(withAudit.Count() - 1, without.Count());
    }

    [Fact]
    public void The_studio_menu_has_the_company_next_to_the_brand()
    {
        var items = Navigation.StudioSet.Pages.Select(p => p.Href).ToList();

        Assert.Contains("/Studio/Company", items);
        Assert.Equal(items.IndexOf("/Studio/Company") + 1, items.IndexOf("/Studio/Brand"));
    }

    // ---- what the environment says

    private static StudioService WithEnvironment(string? template, string? company)
    {
        var settings = new Dictionary<string, string?>();
        if (template is not null) { settings["MATDESIGN_TEMPLATE"] = template; }
        if (company is not null) { settings["MATDESIGN_COMPANY"] = company; }
        return new StudioService(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public void Without_a_word_from_the_environment_there_is_no_default_tenant_and_the_site_is_the_demo()
        => Assert.Null(WithEnvironment(null, null).TenantDefault);

    [Fact]
    public void The_environment_can_name_the_template_and_the_company()
    {
        var both = WithEnvironment("blank", "Acme Industries").TenantDefault!;
        Assert.Equal("Acme Industries", both.Company);
        Assert.Equal("blank", both.Template);
        Assert.Equal("Acme Design", both.SiteName);

        var onlyTemplate = WithEnvironment("BLANK", null).TenantDefault!;
        Assert.Equal("Your company", onlyTemplate.Company);
        Assert.Equal("blank", onlyTemplate.Template);

        var onlyCompany = WithEnvironment(null, "Acme").TenantDefault!;
        Assert.Equal("demo", onlyCompany.Template);
        Assert.Equal("MatDesign", onlyCompany.SiteName);

        Assert.Equal(Tenant.Demo, WithEnvironment("demo", null).TenantDefault);
    }

    [Theory]
    [InlineData("fancy", "Acme", "demo")]
    [InlineData("blank", "Acme <script>", null)]
    public void A_template_that_does_not_exist_is_the_demo_and_a_company_that_is_not_text_is_ignored(string template, string company, string? expected)
    {
        var tenant = WithEnvironment(template, company).TenantDefault;

        Assert.Equal(expected, tenant?.Template);
    }
}
