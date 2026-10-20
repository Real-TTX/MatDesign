using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio;

/// <summary>What the form of the company sends: how the site names itself.</summary>
public sealed class CompanyInput
{
    public string? Company { get; set; }

    public string? ShortName { get; set; }

    public string? SiteName { get; set; }

    public string? Tagline { get; set; }

    public string? Accent { get; set; }
}

/// <summary>
/// The company that runs this instance (docs/PRODUCT.md, stage 14): its names, what the site calls itself, the accent of the site for the blank template, and the template the built-in layer comes from.
/// Saving the names checks them like the setup assistant did (<see cref="TenantValidator"/>) and writes a revision of the item <c>tenant</c> with a history of its own. Switching the template is a
/// separate form with a question first: nothing is deleted, the items the Studio saved stay, and switching back brings the other layer back.
/// </summary>
[RequireCapability(Capability.ViewStudio)]
public class CompanyModel(StudioService studio, DesignData design) : StudioPageModel(studio)
{
    [BindProperty]
    public CompanyInput Input { get; set; } = new();

    /// <summary>The tenant as the form describes it (what was typed, normalised), or the one in force on a GET.</summary>
    public Tenant Current { get; private set; } = Tenant.Demo;

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    public bool Checked { get; private set; }

    public TenantOrigin Origin => design.TenantOrigin;

    public ITemplate Template => design.Template;

    public ITemplate Other => DesignTemplates.All.First(t => t.Id != design.Template.Id);

    /// <summary>What a template is, in the words of this company: the demo is "the Mat apps as a sample" only for the demo; a company reads it as a set of sample apps.</summary>
    public string DescriptionOf(ITemplate template) => design.Words.Text(template.Description);

    /// <summary>The sentence of the question when the template is switched: what is hidden, and what is not lost.</summary>
    public string SwitchQuestion
    {
        get
        {
            var leaving = Template;
            var hidden = leaving.Offers.Count == 0
                ? "Nothing of the site is hidden."
                : $"The site stops offering {Join(leaving.Offers)}.";
            var kept = Other.Brings;
            return design.Words.Text($"Switch to the {Other.Name.ToLowerInvariant()} template? {hidden} Nothing is deleted: what you saved in the Studio stays, and switching back undoes this. {kept}");
        }
    }

    public string? ProblemFor(string field) => Problems.FirstOrDefault(p => p.Field == field)?.Message;

    private static string Join(IReadOnlyList<string> parts)
        => parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];

    public IActionResult OnGet()
    {
        Current = design.Tenant;
        Input = InputOf(Current);
        return Page();
    }

    /// <summary>Saves, or (with the Check button, <c>check</c>) only shows what the form would give.</summary>
    [RequireCapability(Capability.ManageCompany)]
    public IActionResult OnPost(bool check = false)
    {
        var current = design.Tenant;
        var typed = TenantValidator.Normalize(new Tenant(Input.Company ?? string.Empty, Input.ShortName ?? string.Empty, current.Template, Input.SiteName ?? string.Empty, Input.Tagline ?? string.Empty, Input.Accent ?? string.Empty));
        var problems = TenantValidator.Validate(typed);
        Current = typed;
        Checked = check;
        Input = InputOf(typed);
        if (problems.Count > 0 || check)
        {
            Problems = problems;
            return Page();
        }

        Save(typed, current, design.StudioTenant is null ? "Created: " : "Changed ");
        TempData["Flash"] = "The company was saved.";
        return RedirectToPage("/Studio/Company");
    }

    /// <summary>Switches to the other template: the names stay, the accent of the site is the template's own when there is none, and the built-in layer changes.</summary>
    [RequireCapability(Capability.ManageCompany)]
    public IActionResult OnPostSwitch()
    {
        var current = design.Tenant;
        var target = Other;
        var switched = TenantValidator.Normalize(current with { Template = target.Id, Accent = current.Accent.Length > 0 ? current.Accent : target.DefaultAccent });
        var problems = TenantValidator.Validate(switched);
        if (problems.Count > 0)
        {
            Current = switched;
            Input = InputOf(switched);
            Problems = problems;
            return Page();
        }

        Save(switched, current, $"Switched to the {target.Name.ToLowerInvariant()} template: ");
        TempData["Flash"] = $"The site now starts from the {target.Name.ToLowerInvariant()} template.";
        return RedirectToPage("/Studio/Company");
    }

    private void Save(Tenant tenant, Tenant before, string prefix)
    {
        var what = What(before, tenant);
        // the first save of a company that was named in the setup (or by the environment) changes nothing yet: say what it is
        var note = what == "nothing" && prefix.StartsWith("Created", StringComparison.Ordinal) ? "Created from " + (design.TenantOrigin == TenantOrigin.Setup ? "the setup" : "the defaults") : prefix + what;
        Studio.Store.Save(Tenant.Kind, Tenant.ItemId, tenant, Who, note);
        design.Reload();
    }

    private static CompanyInput InputOf(Tenant tenant)
        => new() { Company = tenant.Company, ShortName = tenant.ShortName, SiteName = tenant.SiteName, Tagline = tenant.Tagline, Accent = tenant.Accent };

    /// <summary>For the note of the revision: what differs.</summary>
    private static string What(Tenant before, Tenant after)
    {
        var names = new List<string>();
        if (before.Company != after.Company) { names.Add("company"); }
        if (before.ShortName != after.ShortName) { names.Add("short name"); }
        if (before.SiteName != after.SiteName) { names.Add("site name"); }
        if (before.Tagline != after.Tagline) { names.Add("tagline"); }
        if (before.Template != after.Template) { names.Add("template"); }
        if (before.Accent != after.Accent) { names.Add("accent"); }
        return names.Count == 0 ? "nothing" : string.Join(", ", names);
    }
}
