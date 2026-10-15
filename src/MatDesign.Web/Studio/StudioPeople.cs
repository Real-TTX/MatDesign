using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>
/// The changes to the people of the Studio, each one written to the activity list (stage 16): the person who did it is <c>by</c>, what happened and to whom is the note and the item, and nothing
/// secret ever goes in (the log holds the id, the name, the role and whether the account is disabled; a password and its hash stay in <c>users.json</c>). The rules are <see cref="UserStore"/>'s;
/// this is the pairing of a change with its entry, so that no page can change a person without leaving one.
/// </summary>
public sealed class StudioPeople(UserStore users, StudioStore log)
{
    public UserStore Users { get; } = users;

    /// <summary>Writes one entry of the activity list about a person.</summary>
    public void Record(StudioUser user, string by, string note) => log.Save(UserEvent.Kind, user.Id, UserEvent.Of(user), by, note);

    private static string RoleName(string? role) => StudioRole.Find(role)?.Name.ToLowerInvariant() ?? role ?? "none";

    /// <summary>An administrator creates a person; the password is theirs to hand over, and has to be changed at the first sign-in.</summary>
    public UserResult Create(string? name, string? role, string? password, StudioUser actor)
    {
        var result = Users.Create(name, role, password, actor.Name, mustChange: true);
        if (result is { Ok: true, User: { } created })
        {
            Record(created, actor.Name, $"Created as {RoleName(created.Role)}; the password has to be changed at the first sign-in");
        }

        return result;
    }

    public UserResult SetRole(string id, string? role, StudioUser actor)
    {
        var before = Users.ById(id);
        var result = Users.SetRole(id, role, actor.Id);
        if (result is { Ok: true, User: { } changed } && before is not null && before.Role != changed.Role)
        {
            Record(changed, actor.Name, $"Role changed from {RoleName(before.Role)} to {RoleName(changed.Role)}");
        }

        return result;
    }

    public UserResult SetDisabled(string id, bool disabled, StudioUser actor)
    {
        var result = Users.SetDisabled(id, disabled, actor.Id);
        if (result is { Ok: true, User: { } changed })
        {
            Record(changed, actor.Name, disabled ? "Disabled: the person cannot sign in and any session ends" : "Enabled");
        }

        return result;
    }

    public UserResult ResetPassword(string id, string? password, StudioUser actor)
    {
        var result = Users.ResetPassword(id, password, actor.Id);
        if (result is { Ok: true, User: { } changed })
        {
            Record(changed, actor.Name, "Password reset by an administrator; it has to be changed at the next sign-in and every session of the person ended");
        }

        return result;
    }

    /// <summary>A person changes their own password; every other session of the person ends.</summary>
    public UserResult ChangePassword(StudioUser me, string? current, string? password, string? repeat)
    {
        var result = Users.ChangePassword(me.Id, current, password, repeat);
        if (result is { Ok: true, User: { } changed })
        {
            Record(changed, me.Name, "Changed their own password; every other session ended");
        }

        return result;
    }

    public UserResult Delete(string id, StudioUser actor)
    {
        var result = Users.Delete(id, actor.Id);
        if (result is { Ok: true, User: { } removed })
        {
            log.Delete(UserEvent.Kind, removed.Id, actor.Name, $"Deleted the user {removed.Name} ({RoleName(removed.Role)}); what they did stays in the history under their name");
        }

        return result;
    }
}
