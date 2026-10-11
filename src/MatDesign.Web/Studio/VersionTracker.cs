using System.Text.Json.Serialization;
using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>What the Studio knows about the toolkit an app uses: the version of MatUi it carries, written down by hand, with a note. Kept as an item of the kind <c>usage</c>, one per app.</summary>
public sealed record AppUsage(string AppId, string? Version, string? Note);

/// <summary>How the toolkit of an app stands against the releases.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VersionState
{
    /// <summary>Nobody wrote down which version the app uses.</summary>
    Unknown,

    /// <summary>The app uses the latest release.</summary>
    Current,

    /// <summary>The app uses an older release; <see cref="UsageRow.Behind"/> says how many releases came since.</summary>
    Behind,

    /// <summary>The version is not one of the releases (a copy from before the first release, or a build of the app's own).</summary>
    Foreign,

    /// <summary>A version is written down, but nothing has been released yet to compare it with.</summary>
    NoRelease,
}

/// <summary>An app with the version of MatUi it uses and where that stands.</summary>
public sealed record UsageRow(MatApp App, string? Version, string? Note, VersionState State, int Behind, DateTimeOffset? RecordedAt);

/// <summary>
/// Which MatUi version each app uses and which are behind. The Studio does not look into the apps: a person writes the version down (the page of an app offers the latest with one button), and
/// the page compares it with the releases. Behind is advice (a check says so), never an error: updating an app is the work of its own repository.
/// </summary>
public sealed class VersionTracker(StudioService studio, DesignData design, ReleaseService releases)
{
    private const string Kind = "usage";

    /// <summary>Why a version cannot be written down; null when it can. An empty version means "unknown".</summary>
    public static string? Check(string? version)
        => string.IsNullOrWhiteSpace(version) || ReleaseService.IsVersion(version.Trim()) ? null : "A version is three numbers with dots, like 0.1.0, or empty.";

    private AppUsage? UsageOf(string appId)
        => studio.Enabled && StudioStore.IsId(appId) ? studio.Store.Get<AppUsage>(Kind, appId) : null;

    private DateTimeOffset? RecordedAt(string appId)
        => studio.Enabled && StudioStore.IsId(appId) ? studio.Store.Current(Kind, appId)?.At : null;

    private UsageRow Describe(MatApp app, IReadOnlyList<ReleaseManifest> all)
    {
        var usage = UsageOf(app.Id);
        var version = string.IsNullOrWhiteSpace(usage?.Version) ? null : usage!.Version!.Trim();
        var recorded = usage is null ? (DateTimeOffset?)null : RecordedAt(app.Id);
        if (version is null)
        {
            return new UsageRow(app, null, usage?.Note, VersionState.Unknown, 0, recorded);
        }

        if (all.Count == 0)
        {
            return new UsageRow(app, version, usage!.Note, VersionState.NoRelease, 0, recorded);
        }

        if (all[0].Version == version)
        {
            return new UsageRow(app, version, usage!.Note, VersionState.Current, 0, recorded);
        }

        var index = all.ToList().FindIndex(m => m.Version == version);
        return index < 0
            ? new UsageRow(app, version, usage!.Note, VersionState.Foreign, 0, recorded)
            : new UsageRow(app, version, usage!.Note, VersionState.Behind, index, recorded);
    }

    /// <summary>Every app of the family with its version and where it stands.</summary>
    public IReadOnlyList<UsageRow> All()
    {
        var all = releases.Releases();
        return design.Apps.Select(a => Describe(a, all)).ToList();
    }

    public UsageRow? Of(string? appId) => design.App(appId) is { } app ? Describe(app, releases.Releases()) : null;

    /// <summary>The version of the latest release, or null when there is none.</summary>
    public string? LatestVersion => releases.Latest?.Version;

    /// <summary>The number of apps that use an older release.</summary>
    public int BehindCount => All().Count(v => v.State == VersionState.Behind);

    /// <summary>Writes down the version an app uses and a note. With neither, the record is taken away. The version must have been checked with <see cref="Check"/>.</summary>
    public void Set(string appId, string? version, string? note, string by)
    {
        if (design.App(appId) is null)
        {
            throw new KeyNotFoundException($"There is no app {appId}.");
        }

        var cleanVersion = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (Check(cleanVersion) is { } problem)
        {
            throw new ArgumentException(problem, nameof(version));
        }

        if (cleanVersion is null && cleanNote is null)
        {
            Forget(appId, by);
            return;
        }

        studio.Store.Save(Kind, appId, new AppUsage(appId, cleanVersion, cleanNote), by, cleanVersion is null ? "Version forgotten, note kept" : $"Uses MatUi {cleanVersion}");
        design.Reload();
    }

    /// <summary>Takes the record of an app away (its history stays).</summary>
    public void Forget(string appId, string by)
    {
        if (studio.Enabled && UsageOf(appId) is not null)
        {
            studio.Store.Delete(Kind, appId, by, "Forgotten");
            design.Reload();
        }
    }
}
