using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// Your own account, for every role: who you are, your role and what it allows (every capability in words, allowed or not), your password and the way out. Changing the password needs the current one,
/// is held to the same rules as every password, and ends every other session of the person (a new stamp; this session is signed in again with it). It is also the one page that is open to a person
/// whose password an administrator chose: they land here at the sign-in and nothing else opens until they have chosen their own.
/// </summary>
[RequireCapability(Capability.ViewStudio)]
public class AccountModel(StudioService studio) : StudioPageModel(studio)
{
    [BindProperty]
    public string? Current { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? Repeat { get; set; }

    public string? Error { get; private set; }

    public string? ErrorField { get; private set; }

    protected override bool OpenWhilePasswordMustChange => true;

    public int MinLength => AdminAccount.MinPasswordLength;

    public string? ProblemFor(string field) => ErrorField == field ? Error : null;

    /// <summary>Every capability with whether this role has it.</summary>
    public IReadOnlyList<(Capability Capability, string Text, bool Allowed)> Capabilities
        => [.. Enum.GetValues<Capability>().Select(c => (c, StudioRole.Describe(c), Me.Can(c)))];

    public void OnGet()
    {
    }

    [RequireCapability(Capability.ViewStudio)]
    public async Task<IActionResult> OnPostPasswordAsync()
    {
        if (Me.Builtin)
        {
            Error = "The password of the built-in administrator is the value of MATDESIGN_ADMIN_PASSWORD. Change it there.";
            ErrorField = "Form";
            return Page();
        }

        var wasForced = Me.MustChange;
        var result = Studio.People.ChangePassword(Me, Current, Password, Repeat);
        if (!result.Ok)
        {
            Error = result.Error;
            ErrorField = result.Field;
            Current = Password = Repeat = null;
            return Page();
        }

        // this session continues with the new stamp; every other session of the person has ended with the old one
        await StudioSession.SignInAsync(HttpContext, result.User!);
        TempData["Flash"] = "Your password was changed. Every other session of yours has ended.";
        return wasForced ? RedirectToPage("/Studio/Index") : RedirectToPage("/Studio/Account");
    }
}
