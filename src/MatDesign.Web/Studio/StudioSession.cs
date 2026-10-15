using System.Security.Claims;
using MatDesign.Core.Studio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MatDesign.Web.Studio;

/// <summary>
/// Starting the session of a person: the one place that knows what the cookie carries. It is the <b>id</b> of the person and the stamp of the account, nothing about what the person may do: the
/// role is read from the people on every request (<see cref="StudioService.CurrentUser(HttpContext)"/>), so a role that changes, a person who is disabled or removed, or a password that
/// changes takes effect at once and no cookie can carry a role that is out of date. Used by the sign-in, by the setup assistant (which signs the first user in) and by a password change
/// (which signs the person in again with the new stamp, while every other session of the person ends).
/// </summary>
public static class StudioSession
{
    /// <summary>What the cookie carries: the name (for the shell and the anti-forgery token), the id of the person and the stamp of the account. No role, no capability: those are read from the people.</summary>
    public static ClaimsPrincipal PrincipalOf(StudioUser user)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(StudioService.StampClaim, user.SessionStamp)],
            CookieAuthenticationDefaults.AuthenticationScheme));

    public static Task SignInAsync(HttpContext http, StudioUser user) => http.SignInAsync(PrincipalOf(user));
}
