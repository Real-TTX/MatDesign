namespace MatDesign.Core.Studio;

/// <summary>
/// What a person may do in the Studio, one entry per thing that needs its own permission (stage 16, <c>CODE-10</c>). A handler that changes something declares the capability it needs; a role
/// is a fixed list of them (<see cref="StudioRole"/>). The set is data in code, so that who may do what is read in one place and is the same on every instance.
/// </summary>
public enum Capability
{
    /// <summary>Open every page of the Studio (except the people) and read it.</summary>
    ViewStudio,

    /// <summary>Create, change and delete apps, rules, components, the brand and its files.</summary>
    EditDesign,

    /// <summary>Restore an old revision of an item the person may edit.</summary>
    RestoreHistory,

    /// <summary>Run the checks of the design system again.</summary>
    RunChecks,

    /// <summary>Make a release (it publishes files that apps copy).</summary>
    ManageReleases,

    /// <summary>Write down which release of the toolkit an app uses.</summary>
    ManageVersions,

    /// <summary>Change the company, its names and its template.</summary>
    ManageCompany,

    /// <summary>Change the class prefix and the product name that releases carry.</summary>
    ManageWhiteLabel,

    /// <summary>Create, change and remove the people who use the Studio, and their passwords.</summary>
    ManageUsers,
}

/// <summary>
/// A role of the Studio: a name and the capabilities it has. The three roles are fixed (<see cref="All"/>); a person has exactly one, and it is read again on every request, so a change takes effect
/// at once. <see cref="Can(string?, Capability)"/> is the one function that decides.
/// </summary>
public sealed record StudioRole(string Id, string Name, string Summary, IReadOnlyList<Capability> Capabilities)
{
    public const string AdministratorId = "administrator";
    public const string EditorId = "editor";
    public const string ViewerId = "viewer";

    /// <summary>Everything, including the people, the company, the white label setting and the releases.</summary>
    public static StudioRole Administrator { get; } = new(
        AdministratorId,
        "Administrator",
        "Everything: the design, the company, the white label setting, the releases, the versions and the people.",
        [.. Enum.GetValues<Capability>()]);

    /// <summary>Maintains the design: apps, rules, components, brand and files, with the history and the checks. Releases, versions, company, white label and people are not theirs.</summary>
    public static StudioRole Editor { get; } = new(
        EditorId,
        "Editor",
        "Creates, changes and deletes apps, rules, components, the brand and its files, restores old revisions and runs the checks. Cannot make releases, change the company or the white label setting, write down versions, or manage people.",
        [Capability.ViewStudio, Capability.EditDesign, Capability.RestoreHistory, Capability.RunChecks]);

    /// <summary>Reads everything but the people and changes nothing.</summary>
    public static StudioRole Viewer { get; } = new(
        ViewerId,
        "Viewer",
        "Opens and reads the pages of the Studio. Nothing can be saved.",
        [Capability.ViewStudio]);

    /// <summary>The three roles, the one with the most permission first.</summary>
    public static IReadOnlyList<StudioRole> All { get; } = [Administrator, Editor, Viewer];

    /// <summary>The role with this id (without regard to case), or null.</summary>
    public static StudioRole? Find(string? id) => All.FirstOrDefault(r => string.Equals(r.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsRole(string? id) => Find(id) is not null;

    /// <summary>Whether this role has the capability.</summary>
    public bool Allows(Capability capability) => Capabilities.Contains(capability);

    /// <summary>
    /// The decision: whether a person with the role <paramref name="roleId"/> may do what <paramref name="capability"/> says. A role that does not exist, or no role, may do nothing: a
    /// file that was edited by hand into something that is not a role locks nobody in.
    /// </summary>
    public static bool Can(string? roleId, Capability capability) => Find(roleId)?.Allows(capability) ?? false;

    /// <summary>The role with the least permission that has the capability: what a page that refuses somebody names.</summary>
    public static StudioRole Needed(Capability capability) => All.Reverse().First(r => r.Allows(capability));

    /// <summary>What the capability is, as a sentence of the list on the account page.</summary>
    public static string Describe(Capability capability) => capability switch
    {
        Capability.ViewStudio => "Open and read every page of the Studio (the people are for administrators)",
        Capability.EditDesign => "Create, change and delete apps, rules, components, the brand and its files",
        Capability.RestoreHistory => "Restore an old revision of an item",
        Capability.RunChecks => "Run the checks again",
        Capability.ManageReleases => "Make releases",
        Capability.ManageVersions => "Write down which release each app uses",
        Capability.ManageCompany => "Change the company, its names and its template",
        Capability.ManageWhiteLabel => "Change the class prefix and the product name of the releases",
        Capability.ManageUsers => "Create, change and remove people, and set their passwords",
        _ => capability.ToString(),
    };
}
