using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The setup assistant: the first visitor of a Studio that has no administrator yet names the company, chooses the template the site starts from, and creates the account (a name and a
/// password, twice), is signed in at once and lands in the Studio. It exists only until the account exists; then the address leads to the sign-in. Whoever opens it first becomes the
/// administrator, which is why the page says to do it right after the start. The three steps are one form: a post without the company and the template (a script, an old client) creates the
/// administrator only, and the site stays what <c>MATDESIGN_TEMPLATE</c> and <c>MATDESIGN_COMPANY</c> say, the demo by default.
/// </summary>
public class SetupModel(StudioService studio, DesignData design) : PageModel
{
    public bool Enabled => studio.Enabled;

    public string? Reason => studio.DisabledReason;

    public int MinLength => AdminAccount.MinPasswordLength;

    public IReadOnlyList<ITemplate> Templates => DesignTemplates.All;

    [BindProperty]
    public string? Company { get; set; }

    [BindProperty]
    public string? ShortName { get; set; }

    [BindProperty]
    public string? Template { get; set; }

    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? Repeat { get; set; }

    public IReadOnlyDictionary<string, string> Errors { get; private set; } = new Dictionary<string, string>();

    public string? ErrorFor(string field) => Errors.GetValueOrDefault(field);

    /// <summary>The step to show first: the earliest one that has a problem, else the first.</summary>
    public int FirstStep => ErrorFor("Company") is not null || ErrorFor("ShortName") is not null ? 1 : ErrorFor("Template") is not null ? 2 : Errors.Count > 0 ? 3 : 1;

    public IActionResult OnGet()
    {
        if (studio.Enabled && !studio.NeedsSetup)
        {
            return RedirectToPage("/Studio/Login");
        }

        Name ??= "admin";
        // what the environment says (MATDESIGN_COMPANY, MATDESIGN_TEMPLATE) is where the assistant starts; without it, the demo and an empty name
        var start = studio.TenantDefault;
        Company ??= start?.Company;
        ShortName ??= start is null ? null : start.ShortName;
        Template ??= start?.Template ?? DesignTemplates.DemoId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!studio.Enabled)
        {
            return Page();
        }

        if (!studio.NeedsSetup)
        {
            return RedirectToPage("/Studio/Login");
        }

        // the company and the template come with the assistant's form; a post without them is the administrator alone
        Tenant? tenant = Request.Form.ContainsKey(nameof(Company))
            ? Tenant.For(Company ?? string.Empty, (Template ?? string.Empty).Trim().ToLowerInvariant(), ShortName)
            : null;
        var result = studio.Setup(Name, Password, Repeat, tenant);
        if (!result.Done)
        {
            Errors = result.Errors;
            Password = null;
            Repeat = null;
            return Page();
        }

        design.Reload();
        await StudioSession.SignInAsync(HttpContext, studio);
        TempData["Flash"] = $"The Studio is set up. You are signed in as {studio.Administrator}.";
        return LocalRedirect("/Studio");
    }
}
