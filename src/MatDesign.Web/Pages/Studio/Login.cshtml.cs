using System.Security.Claims;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The sign-in of the Studio: one administrator, whose password comes from the environment. A wrong password costs half a second, and five of them in a
/// minute lock the client out for a minute. When the Studio is off the page says what is missing instead of showing a form that cannot work.
/// </summary>
public class LoginModel(StudioService studio) : PageModel
{
    public bool Enabled => studio.Enabled;

    public string? Reason => studio.DisabledReason;

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    /// <summary>Only an address inside the Studio is followed after the sign-in: never an address of another site.</summary>
    private string Target => Url.IsLocalUrl(ReturnUrl) && ReturnUrl!.StartsWith("/Studio", StringComparison.OrdinalIgnoreCase) ? ReturnUrl : "/Studio";

    public IActionResult OnGet()
        => studio.Enabled && User.Identity?.IsAuthenticated == true ? LocalRedirect(Target) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
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

        if (!studio.CheckPassword(Password))
        {
            studio.Failed(client);
            await Task.Delay(500);
            Error = "The password is not right.";
            Password = null;
            return Page();
        }

        studio.Succeeded(client);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, studio.Administrator), new Claim(ClaimTypes.Role, "studio"), new Claim(StudioService.StampClaim, studio.Stamp)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        return LocalRedirect(Target);
    }
}
