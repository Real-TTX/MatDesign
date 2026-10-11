using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The sign-in of the Studio: the one administrator, by name and password. A Studio that has no administrator yet leads to the setup assistant instead.
/// A wrong name or password costs half a second, and five of them in a minute lock the client out for a minute (the page does not say which one was wrong).
/// When the Studio is off the page says what is missing instead of showing a form that cannot work.
/// </summary>
public class LoginModel(StudioService studio) : PageModel
{
    public bool Enabled => studio.Enabled;

    public string? Reason => studio.DisabledReason;

    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    /// <summary>Only an address inside the Studio is followed after the sign-in: never an address of another site.</summary>
    private string Target => Url.IsLocalUrl(ReturnUrl) && ReturnUrl!.StartsWith("/Studio", StringComparison.OrdinalIgnoreCase) ? ReturnUrl : "/Studio";

    public IActionResult OnGet()
    {
        if (studio.NeedsSetup)
        {
            return RedirectToPage("/Studio/Setup");
        }

        return studio.Enabled && User.Identity?.IsAuthenticated == true ? LocalRedirect(Target) : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (studio.NeedsSetup)
        {
            return RedirectToPage("/Studio/Setup");
        }

        if (!studio.Enabled)
        {
            return Page();
        }

        var client = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (studio.LockedFor(client) is { } wait)
        {
            Error = $"Too many attempts. Try again in {Math.Ceiling(wait.TotalSeconds)} seconds.";
            return Page();
        }

        if (!studio.SignsIn(Name, Password))
        {
            studio.Failed(client);
            await Task.Delay(500);
            Error = "The name or the password is not right.";
            Password = null;
            return Page();
        }

        studio.Succeeded(client);
        await StudioSession.SignInAsync(HttpContext, studio);
        return LocalRedirect(Target);
    }
}
