using System.Text.Json;
using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>Where an app or a rule comes from.</summary>
public enum ItemSource
{
    /// <summary>Written in the code.</summary>
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

    private sealed record Snapshot(
        IReadOnlyList<MatApp> Apps,
        IReadOnlyList<MatApp> Items,
        IReadOnlyDictionary<string, ItemSource> Sources,
        IReadOnlyList<Rule> Rules,
        IReadOnlyList<Rule> RuleItems,
        IReadOnlyDictionary<string, ItemSource> RuleSources);

    /// <summary>A number that grows with every change made in the Studio.</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>Every app of the family: the built-in ones (changed ones replaced) and the added ones.</summary>
    public IReadOnlyList<MatApp> Apps => _snapshot.Apps;

    /// <summary>The apps that were added or changed in the Studio.</summary>
    public IReadOnlyList<MatApp> StudioApps => _snapshot.Items;

    public MatApp? App(string? id) => id is null ? null : Apps.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public ItemSource SourceOf(string id) => _snapshot.Sources.GetValueOrDefault(id.ToLowerInvariant(), ItemSource.BuiltIn);

    /// <summary>The rulebook: the built-in rules (changed ones replaced), and the rules added in the Studio after the last rule of their area.</summary>
    public IReadOnlyList<Rule> Rules => _snapshot.Rules;

    /// <summary>The rules that were added or changed in the Studio.</summary>
    public IReadOnlyList<Rule> StudioRules => _snapshot.RuleItems;

    public Rule? Rule(string? id) => id is null ? null : Rules.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Rule> RulesIn(string areaId) => Rules.Where(r => r.Area == areaId);

    public ItemSource RuleSourceOf(string id) => _snapshot.RuleSources.GetValueOrDefault(id.ToUpperInvariant(), ItemSource.BuiltIn);

    /// <summary>Reads the Studio's items again. Called after every save and delete.</summary>
    public void Reload()
    {
        lock (_gate)
        {
            _snapshot = Load();
            Interlocked.Increment(ref _version);
        }
    }

    /// <summary>The items of a kind that can be read as <typeparamref name="T"/> and are safe; the others (a file edited by hand into nonsense) are left out, the site goes on.</summary>
    private List<T> Items<T>(string kind, Func<T, bool> safe, Func<T, string> idOf) where T : class
    {
        var items = new List<T>();
        if (!_studio.Enabled)
        {
            return items;
        }

        foreach (var stored in _studio.Store.List(kind))
        {
            try
            {
                if (stored.Revision.Data.Deserialize<T>(Options) is { } item && safe(item) && idOf(item).Equals(stored.Id, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(item);
                }
            }
            catch (JsonException)
            {
                // not an item any more
            }
        }

        return items;
    }

    private Snapshot Load()
    {
        var items = Items<MatApp>("apps", AppValidator.IsSafe, a => a.Id);
        var sources = new Dictionary<string, ItemSource>();
        var apps = AppRegistry.All.Select(builtIn =>
        {
            var changed = items.FirstOrDefault(i => i.Id == builtIn.Id);
            if (changed is not null)
            {
                sources[builtIn.Id] = ItemSource.Changed;
            }

            return changed ?? builtIn;
        }).ToList();
        foreach (var added in items.Where(i => AppRegistry.Get(i.Id) is null))
        {
            sources[added.Id] = ItemSource.Added;
            apps.Add(added);
        }

        var ruleItems = Items<Rule>("rules", RuleValidator.IsSafe, r => r.Id);
        var ruleSources = new Dictionary<string, ItemSource>();
        var rules = RuleBook.All.Select(builtIn =>
        {
            var changed = ruleItems.FirstOrDefault(i => i.Id == builtIn.Id);
            if (changed is not null)
            {
                ruleSources[builtIn.Id] = ItemSource.Changed;
            }

            return changed ?? builtIn;
        }).ToList();
        foreach (var added in ruleItems.Where(i => RuleBook.Get(i.Id) is null).OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            ruleSources[added.Id] = ItemSource.Added;
            var last = rules.FindLastIndex(r => r.Area == added.Area);
            rules.Insert(last < 0 ? rules.Count : last + 1, added);
        }

        return new Snapshot(apps, items, sources, rules, ruleItems, ruleSources);
    }
}
