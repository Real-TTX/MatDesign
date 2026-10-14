using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// What a tenant has to be, checked before the Studio saves it and again when it is read from the disk: names of a sensible length without control characters (they are written into pages,
/// a README and a changelog), a template that exists, and for the blank template an accent that reaches AA in light and dark, like the accent of any app (<c>IDENT-02</c>).
/// </summary>
public static partial class TenantValidator
{
    public const int CompanyMax = 60;
    public const int ShortNameMax = 16;
    public const int SiteNameMax = 40;
    public const int TaglineMax = 90;

    [GeneratedRegex(@"^#[0-9a-f]{6}$")]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"[\p{Cc}\p{Cf}\p{Zl}\p{Zp}<>]")]
    private static partial Regex Unsafe();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private static string Clean(string? text) => Spaces().Replace((text ?? string.Empty).Trim(), " ");

    /// <summary>
    /// The tenant as it is stored: names trimmed with their spaces collapsed, the template and the accent in lower case, a short name made from the company and a site name made from the short
    /// name when they were left empty, the accent of the template when there is none.
    /// </summary>
    public static Tenant Normalize(Tenant tenant)
    {
        var company = Clean(tenant.Company);
        var template = (tenant.Template ?? string.Empty).Trim().ToLowerInvariant();
        var shortName = Clean(tenant.ShortName);
        if (shortName.Length == 0)
        {
            shortName = Tenant.ShortNameOf(company);
        }

        var siteName = Clean(tenant.SiteName);
        if (siteName.Length == 0)
        {
            siteName = Tenant.DefaultSiteName(shortName, template);
        }

        var accent = Clean(tenant.Accent).ToLowerInvariant();
        if (accent.Length == 0 && DesignTemplates.Find(template) is { } known)
        {
            accent = known.DefaultAccent;
        }

        return new Tenant(company, shortName, template, siteName, Clean(tenant.Tagline), accent);
    }

    /// <summary>True when the tenant can be written into a page and a file without harm (names that are text, a template that exists, an accent that is a colour). The Studio checks this when it reads an item.</summary>
    public static bool IsSafe(Tenant tenant)
        => tenant.Company is { Length: >= 1 and <= CompanyMax } && !Unsafe().IsMatch(tenant.Company)
            && tenant.ShortName is { Length: >= 1 and <= ShortNameMax } && !Unsafe().IsMatch(tenant.ShortName)
            && tenant.SiteName is { Length: >= 1 and <= SiteNameMax } && !Unsafe().IsMatch(tenant.SiteName)
            && (tenant.Tagline ?? string.Empty).Length <= TaglineMax && !Unsafe().IsMatch(tenant.Tagline ?? string.Empty)
            && DesignTemplates.Find(tenant.Template) is not null
            && (tenant.Accent.Length == 0 || HexPattern().IsMatch(tenant.Accent));

    /// <summary>Everything that is wrong with <paramref name="tenant"/>, which must be normalised, in words that say how to fix it.</summary>
    public static IReadOnlyList<Problem> Validate(Tenant tenant)
    {
        var problems = new List<Problem>();

        void Name(string field, string value, int min, int max, string what)
        {
            if (value.Length < min || value.Length > max)
            {
                problems.Add(new(field, $"{what} in {min} to {max} characters."));
            }
            else if (Unsafe().IsMatch(value))
            {
                problems.Add(new(field, "Use letters, digits and the usual punctuation; no angle brackets or control characters."));
            }
        }

        Name("Company", tenant.Company, 2, CompanyMax, "Give the full name of the company");
        Name("ShortName", tenant.ShortName, 1, ShortNameMax, "Give a short name, for places where the full name does not fit,");
        Name("SiteName", tenant.SiteName, 2, SiteNameMax, "Name the site as it should appear under the logo");
        if (tenant.Tagline.Length > TaglineMax)
        {
            problems.Add(new("Tagline", $"Keep the tagline to {TaglineMax} characters."));
        }
        else if (Unsafe().IsMatch(tenant.Tagline))
        {
            problems.Add(new("Tagline", "Use letters, digits and the usual punctuation; no angle brackets or control characters."));
        }

        if (DesignTemplates.Find(tenant.Template) is null)
        {
            problems.Add(new("Template", "Choose a template: demo or blank."));
        }

        if (tenant.Accent.Length > 0 && !HexPattern().IsMatch(tenant.Accent))
        {
            problems.Add(new("Accent", "A colour like #2563eb."));
        }
        else if (tenant.Accent.Length > 0 && DesignTemplates.Find(tenant.Template) is { } template && !tenant.IsDemo)
        {
            problems.AddRange(AppValidator.Contrast(template.SiteApp(tenant, [])).Select(p => new Problem("Accent", p.Message)));
        }

        return problems;
    }
}
