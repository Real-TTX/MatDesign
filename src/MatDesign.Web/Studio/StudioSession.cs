using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MatDesign.Web.Studio;

/// <summary>Starting the session of the administrator: the one place that knows what the cookie carries (used by the sign-in and by the setup assistant, which signs the new administrator in).</summary>
public static class StudioSession
{
    public static Task SignInAsync(HttpContext http, StudioService studio)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, studio.Administrator), new Claim(ClaimTypes.Role, "studio"), new Claim(StudioService.StampClaim, studio.Stamp)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(new ClaimsPrincipal(identity));
    }
}
