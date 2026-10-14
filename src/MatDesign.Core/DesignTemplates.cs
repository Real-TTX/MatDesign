using MatDesign.Core.Studio;

namespace MatDesign.Core;

/// <summary>
/// A template is the built-in layer an instance starts from (docs/PRODUCT.md, stage 14): which apps, which audit and which sample content the site offers before the Studio has saved anything,
/// and the brand that is in force until the company saves its own. What the Studio saved sits on top of it, for every template. The rules and the documentation of the components are the
/// toolkit's and not a template's: every template has them. Core has no web types, so a template is data and a few rules; the pages ask <c>DesignData</c>, which asks the tenant's template.
/// </summary>
public interface ITemplate
{
    /// <summary>The id the tenant stores: <c>demo</c> or <c>blank</c>.</summary>
    string Id { get; }

    /// <summary>The name on the card of the setup assistant and on the Company page.</summary>
    string Name { get; }

    /// <summary>One sentence for that card.</summary>
    string Description { get; }

    /// <summary>The apps that are built in (the Studio's apps are added to these).</summary>
    IReadOnlyList<MatApp> Apps { get; }

    /// <summary>True when the site shows the audit snapshot of the Mat apps (a scan of other repositories: sample data of the demo).</summary>
    bool OffersAudit { get; }

    /// <summary>True when the pages that show sample apps (the patterns, the colour page) may name the built-in apps of the demo.</summary>
    bool OffersSampleApps { get; }

    /// <summary>The accent of the site itself when the tenant sets none.</summary>
    string DefaultAccent { get; }

    /// <summary>What this template offers that the other does not, in words for a sentence: switching away from it hides these (nothing is deleted, switching back brings them back).</summary>
    IReadOnlyList<string> Offers { get; }

    /// <summary>The app that is the site itself: the logo, the colour and the name of the shell. <paramref name="apps"/> are the apps of the site as <c>DesignData</c> merged them.</summary>
    MatApp SiteApp(Tenant tenant, IReadOnlyList<MatApp> apps);

    /// <summary>The brand that is in force until the Studio saves one: no token changed.</summary>
    Brand BrandFor(Tenant tenant);
}

/// <summary>The templates there are. An unknown id is never a template: <see cref="Find"/> says so, <see cref="Get"/> falls back to the demo.</summary>
public static class DesignTemplates
{
    public const string DemoId = "demo";
    public const string BlankId = "blank";

    /// <summary>The id of the app that stands for a blank site: it can never be the id of an app of the Studio (an id of letters only), so no app takes its place.</summary>
    public const string SiteAppId = "this-site";

    public static ITemplate Demo { get; } = new DemoTemplate();

    public static ITemplate Blank { get; } = new BlankTemplate();

    public static IReadOnlyList<ITemplate> All { get; } = [Demo, Blank];

    public static ITemplate? Find(string? id) => All.FirstOrDefault(t => t.Id == id);

    public static ITemplate Get(string? id) => Find(id) ?? Demo;

    private sealed class DemoTemplate : ITemplate
    {
        public string Id => DemoId;

        public string Name => "Demo";

        public string Description => "The Mat apps as a sample, to look around and change.";

        public IReadOnlyList<MatApp> Apps => AppRegistry.All;

        public bool OffersAudit => true;

        public bool OffersSampleApps => true;

        public string DefaultAccent => string.Empty;

        public IReadOnlyList<string> Offers =>
        [
            "the Mat apps with their accents",
            "the audit of their repositories",
            "the Mat brand",
            "the sample apps of the patterns and of the colour page",
        ];

        public MatApp SiteApp(Tenant tenant, IReadOnlyList<MatApp> apps)
        {
            // the site of the demo is the matdesign app, with the colour and the name it has in the registry (or as the Studio changed it); a company that gave its site another name than the
            // demo's shows that name in the shell, so that the logo, the title and the heading say the same
            var site = apps.FirstOrDefault(a => a.Id == "matdesign") ?? AppRegistry.Get("matdesign")!;
            return tenant.SiteName == Tenant.Demo.SiteName ? site : site with { Name = tenant.SiteName };
        }

        public Brand BrandFor(Tenant tenant) => Studio.Brand.BuiltIn;
    }

    private sealed class BlankTemplate : ITemplate
    {
        private static readonly IReadOnlyDictionary<string, string> Nothing = new Dictionary<string, string>();

        public string Id => BlankId;

        public string Name => "Blank";

        public string Description => "Start with your own brand and no apps.";

        public IReadOnlyList<MatApp> Apps => [];

        public bool OffersAudit => false;

        public bool OffersSampleApps => false;

        public string DefaultAccent => "#2563eb";

        public IReadOnlyList<string> Offers => [];

        public MatApp SiteApp(Tenant tenant, IReadOnlyList<MatApp> apps)
            => new(SiteAppId, tenant.SiteName, tenant.Tagline, AppRegistry.Platform, tenant.Accent.Length > 0 ? tenant.Accent : DefaultAccent, "palette", string.Empty);

        public Brand BrandFor(Tenant tenant) => new(tenant.Company, tenant.ShortName, tenant.Tagline, Nothing, Nothing);
    }
}
