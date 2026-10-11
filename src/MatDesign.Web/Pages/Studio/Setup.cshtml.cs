using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The setup assistant: the first visitor of a Studio that has no administrator yet creates the account (a name and a password, twice), is signed in at once and lands
/// in the Studio. It exists only until the account exists; then the address leads to the sign-in. Whoever opens it first becomes the administrator, which is why the
/// page says to do it right after the start.
/// </summary>
public class SetupModel(StudioService studio) : PageModel
{
    public bool Enabled => studio.Enabled;

    public string? Reason => studio.DisabledReason;

    public int MinLength => AdminAccount.MinPasswordLength;

    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? Repeat { get; set; }

    public IReadOnlyDictionary<string, string> Errors { get; private set; } = new Dictionary<string, string>();

    public string? ErrorFor(string field) => Errors.GetValueOrDefault(field);

    public IActionResult OnGet()
    {
        if (studio.Enabled && !studio.NeedsSetup)
        {
            return RedirectToPage("/Studio/Login");
        }

        Name ??= "admin";
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

        var result = studio.Setup(Name, Password, Repeat);
        if (!result.Done)
        {
            Errors = result.Errors;
            Password = null;
            Repeat = null;
            return Page();
        }

        await StudioSession.SignInAsync(HttpContext, studio);
        TempData["Flash"] = $"The Studio is set up. You are signed in as {studio.Administrator}.";
        return LocalRedirect("/Studio");
    }
}
