using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The sign-in of the Studio: a person, by name and password. A Studio that has nobody yet leads to the setup assistant instead.
/// A wrong name or password costs half a second, and five of them in a minute for the same name from the same address lock that pair out for a minute (the page does not say which one was wrong,
/// nor whether the name exists, nor whether the account is disabled). A person whose password has to be changed lands on their account page.
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

        return studio.Enabled && studio.CurrentUser(User) is not null ? LocalRedirect(Target) : Page();
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
        if (studio.LockedFor(Name, client) is { } wait)
        {
            Error = $"Too many attempts. Try again in {Math.Ceiling(wait.TotalSeconds)} seconds.";
            return Page();
        }

        if (studio.SignIn(Name, Password) is not { } user)
        {
            studio.Failed(Name, client);
            await Task.Delay(500);
            Error = "The name or the password is not right.";
            Password = null;
            return Page();
        }

        studio.Succeeded(Name, client);
        studio.Users.RecordSignIn(user.Id);
        await StudioSession.SignInAsync(HttpContext, user);
        return LocalRedirect(user.MustChange ? "/Studio/Account" : Target);
    }
}
