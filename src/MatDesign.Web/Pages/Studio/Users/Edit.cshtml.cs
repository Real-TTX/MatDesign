using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Users;

/// <summary>
/// One person of the Studio (administrators only): a new person (a name, a role and a first password the administrator hands over, which has to be changed at the first sign-in), or an existing one
/// (change the role, disable or enable the account, set a new password, remove the person). Each change is its own form and its own handler, so that none is a side effect of another. The guards
/// are the store's (<see cref="UserStore"/>: the last active administrator, oneself); the page leaves out what would be refused and says why, and a refusal that arrives anyway is shown at the top.
/// The history of what the person did stays: their name stays in the revisions and the activity list.
/// </summary>
[RequireCapability(Capability.ManageUsers)]
public class EditModel(StudioService studio) : StudioPageModel(studio)
{
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Role { get; set; }

    /// <summary>The first password of a new person, or the new one in a reset. It is never written back into a page.</summary>
    [BindProperty]
    public string? Password { get; set; }

    /// <summary>The person who is edited; null for a new one.</summary>
    public StudioUser? Person { get; private set; }

    public string? Error { get; private set; }

    public string? ErrorField { get; private set; }

    public bool IsNew => string.IsNullOrEmpty(Id);

    public bool IsSelf => Person is not null && Person.Id == Me.Id;

    /// <summary>True when the person is the only active administrator: the store refuses to demote, disable or delete them.</summary>
    public bool IsLastAdministrator => Person is { IsActiveAdministrator: true } && Studio.Users.ActiveAdministrators <= 1;

    public bool CanChangeRole => Person is not null && !IsSelf && !IsLastAdministrator;

    public bool CanDisable => Person is not null && !IsSelf && !IsLastAdministrator;

    public bool CanDelete => Person is not null && !IsSelf && !IsLastAdministrator;

    public int MinLength => AdminAccount.MinPasswordLength;

    public string? ProblemFor(string field) => ErrorField == field ? Error : null;

    private bool Load()
    {
        if (IsNew)
        {
            Role ??= StudioRole.ViewerId;
            return true;
        }

        Person = Studio.Users.ById(Id);
        if (Person is null || Person.Builtin)
        {
            return false;
        }

        Role = Person.Role;
        return true;
    }

    public IActionResult OnGet() => Load() ? Page() : NotFound();

    private IActionResult Failed(UserResult result)
    {
        Error = result.Error;
        ErrorField = result.Field;
        Password = null;
        Load();
        return Page();
    }

    private IActionResult Done(string flash, string? backTo = null)
    {
        TempData["Flash"] = flash;
        return backTo is null ? RedirectToPage("/Studio/Users/Index") : LocalRedirect(backTo);
    }

    [RequireCapability(Capability.ManageUsers)]
    public IActionResult OnPostCreate()
    {
        var result = Studio.People.Create(Name, Role, Password, Me);
        if (!result.Ok)
        {
            Id = null;
            return Failed(result);
        }

        return Done($"{result.User!.Name} was added as {RoleOf(result.User)}. Hand over the password: it has to be changed at the first sign-in.");
    }

    [RequireCapability(Capability.ManageUsers)]
    public IActionResult OnPostRole()
    {
        var chosen = Role;
        if (!Load())
        {
            return NotFound();
        }

        var result = Studio.People.SetRole(Person!.Id, chosen, Me);
        if (!result.Ok)
        {
            return Failed(result);
        }

        return Done($"{Person.Name} is {RoleOf(result.User!)} now. It takes effect at their next click.", $"/Studio/Users/Edit?id={Person.Id}");
    }

    [RequireCapability(Capability.ManageUsers)]
    public IActionResult OnPostDisable(bool disable)
    {
        if (!Load())
        {
            return NotFound();
        }

        var result = Studio.People.SetDisabled(Person!.Id, disable, Me);
        if (!result.Ok)
        {
            return Failed(result);
        }

        return Done(disable ? $"{Person.Name} is disabled and is signed out." : $"{Person.Name} is enabled again.", $"/Studio/Users/Edit?id={Person.Id}");
    }

    [RequireCapability(Capability.ManageUsers)]
    public IActionResult OnPostReset()
    {
        if (!Load())
        {
            return NotFound();
        }

        var result = Studio.People.ResetPassword(Person!.Id, Password, Me);
        if (!result.Ok)
        {
            return Failed(result);
        }

        return Done($"The password of {Person.Name} was set. Hand it over: it has to be changed at the next sign-in, and every session of {Person.Name} ended.", $"/Studio/Users/Edit?id={Person.Id}");
    }

    [RequireCapability(Capability.ManageUsers)]
    public IActionResult OnPostDelete()
    {
        if (!Load())
        {
            return NotFound();
        }

        var result = Studio.People.Delete(Person!.Id, Me);
        if (!result.Ok)
        {
            return Failed(result);
        }

        return Done($"{Person.Name} was removed. What they did stays in the history under their name.");
    }

    private static string RoleOf(StudioUser user) => (user.RoleInfo?.Name ?? user.Role).ToLowerInvariant();
}
