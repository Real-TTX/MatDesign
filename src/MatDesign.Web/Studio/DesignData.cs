using System.Text.Json;
using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>Where an app of the family comes from.</summary>
public enum AppSource
{
    /// <summary>Written in the registry of the code.</summary>
    BuiltIn,

    /// <summary>Created in the Studio.</summary>
    Added,

    /// <summary>Built in, and changed in the Studio (the item with the same id replaces it).</summary>
    Changed,
}

/// <summary>
/// The design data as the site shows it: what is built in, with what the Studio saved on top (docs/STUDIO.md). An item with the id of a built-in entry
/// replaces it; deleting the item brings the built-in one back. The pages read from here, not from the registries in the code, so a change made in the
/// Studio is seen everywhere. <see cref="Version"/> changes with every <see cref="Reload"/>, which is how the toolkit files know that they are out of date.
/// </summary>
public sealed class DesignData
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly StudioService _studio;
    private readonly object _gate = new();
    private Snapshot _snapshot;
    private long _version;

    public DesignData(StudioService studio)
    {
        _studio = studio;
        _snapshot = Load();
    }

    private sealed record Snapshot(IReadOnlyList<MatApp> Apps, IReadOnlyList<MatApp> Items, IReadOnlyDictionary<string, AppSource> Sources);

    /// <summary>A number that grows with every change made in the Studio.</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>Every app of the family: the built-in ones (changed ones replaced) and the added ones.</summary>
    public IReadOnlyList<MatApp> Apps => _snapshot.Apps;

    /// <summary>The apps that were added or changed in the Studio.</summary>
    public IReadOnlyList<MatApp> StudioApps => _snapshot.Items;

    public MatApp? App(string? id) => id is null ? null : Apps.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public AppSource SourceOf(string id) => _snapshot.Sources.GetValueOrDefault(id.ToLowerInvariant(), AppSource.BuiltIn);

    /// <summary>Reads the Studio's items again. Called after every save and delete.</summary>
    public void Reload()
    {
        lock (_gate)
        {
            _snapshot = Load();
            Interlocked.Increment(ref _version);
        }
    }

    private Snapshot Load()
    {
        var items = new List<MatApp>();
        if (_studio.Enabled)
        {
            foreach (var stored in _studio.Store.List("apps"))
            {
                try
                {
                    if (stored.Revision.Data.Deserialize<MatApp>(Options) is { } app && AppValidator.IsSafe(app) && app.Id.Equals(stored.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(app);
                    }
                }
                catch (JsonException)
                {
                    // a file that was edited by hand and is not an app any more: it is left out, the site goes on
                }
            }
        }

        var sources = new Dictionary<string, AppSource>();
        var apps = AppRegistry.All.Select(builtIn =>
        {
            var changed = items.FirstOrDefault(i => i.Id == builtIn.Id);
            if (changed is not null)
            {
                sources[builtIn.Id] = AppSource.Changed;
            }

            return changed ?? builtIn;
        }).ToList();
        foreach (var added in items.Where(i => AppRegistry.Get(i.Id) is null))
        {
            sources[added.Id] = AppSource.Added;
            apps.Add(added);
        }

        return new Snapshot(apps, items, sources);
    }
}
