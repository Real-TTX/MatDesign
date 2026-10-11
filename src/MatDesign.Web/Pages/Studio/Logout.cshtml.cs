using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>Signing out changes something, so it is a POST (with the anti-forgery token of the form in the user menu); a GET only leads back.</summary>
public class LogoutModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Studio/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync();
        return LocalRedirect("/");
    }
}
