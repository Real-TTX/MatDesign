using System.Text.Json;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Web.Studio;

/// <summary>Where an app, a rule or a component comes from.</summary>
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
    private BrandAssets? _assets;

    public DesignData(StudioService studio)
    {
        _studio = studio;
        _snapshot = Load();
        _assets = LoadAssets();
    }

    private sealed record Kind<T>(IReadOnlyList<T> All, IReadOnlyList<T> Items, IReadOnlyDictionary<string, ItemSource> Sources);

    private sealed record Snapshot(Kind<MatApp> Apps, Kind<Rule> Rules, Kind<ComponentDoc> Components, Brand? Brand);

    /// <summary>A number that grows with every change made in the Studio.</summary>
    public long Version => Interlocked.Read(ref _version);

    // ---- apps

    /// <summary>Every app of the family: the built-in ones (changed ones replaced) and the added ones.</summary>
    public IReadOnlyList<MatApp> Apps => _snapshot.Apps.All;

    /// <summary>The apps that were added or changed in the Studio.</summary>
    public IReadOnlyList<MatApp> StudioApps => _snapshot.Apps.Items;

    public MatApp? App(string? id) => id is null ? null : Apps.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public ItemSource SourceOf(string id) => _snapshot.Apps.Sources.GetValueOrDefault(id.ToLowerInvariant(), ItemSource.BuiltIn);

    // ---- rules

    /// <summary>The rulebook: the built-in rules (changed ones replaced), and the rules added in the Studio after the last rule of their area.</summary>
    public IReadOnlyList<Rule> Rules => _snapshot.Rules.All;

    /// <summary>The rules that were added or changed in the Studio.</summary>
    public IReadOnlyList<Rule> StudioRules => _snapshot.Rules.Items;

    public Rule? Rule(string? id) => id is null ? null : Rules.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Rule> RulesIn(string areaId) => Rules.Where(r => r.Area == areaId);

    public ItemSource RuleSourceOf(string id) => _snapshot.Rules.Sources.GetValueOrDefault(id.ToUpperInvariant(), ItemSource.BuiltIn);

    // ---- components

    /// <summary>The documentation of the controls: the built-in components (changed ones replaced), and the ones added in the Studio after the last component of their group.</summary>
    public IReadOnlyList<ComponentDoc> Components => _snapshot.Components.All;

    /// <summary>The components that were added or changed in the Studio.</summary>
    public IReadOnlyList<ComponentDoc> StudioComponents => _snapshot.Components.Items;

    public ComponentDoc? Component(string? id) => id is null ? null : Components.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ComponentDoc> ComponentsOf(string group) => Components.Where(c => c.Group == group);

    /// <summary>The components a page of the documentation shows: those of its groups.</summary>
    public IEnumerable<ComponentDoc> ComponentsIn(CatalogSection section) => Components.Where(c => section.Groups.Contains(c.Group));

    /// <summary>The names of the components of the Studio (added or changed) that name a rule: while there are any, an added rule cannot be deleted.</summary>
    public IReadOnlyList<string> ComponentsNaming(string ruleId)
        => Components.Where(c => ComponentSourceOf(c.Id) != ItemSource.BuiltIn && c.Rules.Contains(ruleId, StringComparer.OrdinalIgnoreCase)).Select(c => c.Name).ToList();

    public ItemSource ComponentSourceOf(string id) => _snapshot.Components.Sources.GetValueOrDefault(id.ToLowerInvariant(), ItemSource.BuiltIn);

    // ---- brand

    /// <summary>The brand of the company: what the Studio saved, or the built-in brand (the demo data of the Mat family, no token changed).</summary>
    public Brand Brand => _snapshot.Brand ?? Brand.BuiltIn;

    /// <summary>The brand as the Studio saved it; null while it has never been saved (or was reset).</summary>
    public Brand? StudioBrand => _snapshot.Brand;

    /// <summary>Built in until the Studio saves a brand, changed after that (resetting brings the built-in brand back).</summary>
    public ItemSource BrandSource => _snapshot.Brand is null ? ItemSource.BuiltIn : ItemSource.Changed;

    // ---- brand assets (stage 13): logo, word mark, favicon, app icon and fonts, as files on the volume

    /// <summary>The assets of the brand as the Studio saved them (the files they refer to are on the volume); null while nothing was saved, or after a reset. The site, the bundle and the releases read this.</summary>
    public BrandAssets? Assets => _assets;

    /// <summary>The assets, never null: a brand without any is empty (nothing is written, nothing on the site changes).</summary>
    public BrandAssets AssetsOrEmpty => _assets ?? new BrandAssets();

    /// <summary>What the menu counts: the apps (for the palette), the rules and the components of the components page.</summary>
    public NavFacts NavFacts => new(Apps, Rules.Count, ComponentsIn(ComponentCatalog.Sections[0]).Count());

    /// <summary>Reads the Studio's items again. Called after every save and delete.</summary>
    public void Reload()
    {
        lock (_gate)
        {
            _snapshot = Load();
            _assets = LoadAssets();
            Interlocked.Increment(ref _version);
        }
    }

    /// <summary>The items of a kind that can be read as <typeparamref name="T"/> and are safe; the others (a file edited by hand into nonsense) are left out, the site goes on.</summary>
    private List<T> Items<T>(string kind, Func<T, T> clean, Func<T, bool> safe, Func<T, string> idOf) where T : class
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
                if (stored.Revision.Data.Deserialize<T>(Options) is { } read && clean(read) is var item && safe(item) && idOf(item).Equals(stored.Id, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(item);
                }
            }
            catch (Exception ex) when (ex is JsonException or NullReferenceException or ArgumentException)
            {
                // not an item any more
            }
        }

        return items;
    }

    /// <summary>
    /// The built-in entries with the items on top: an item with the id of a built-in entry replaces it where it stands, the others are placed by <paramref name="place"/>
    /// (after the last entry of their area or group).
    /// </summary>
    private static Kind<T> Merge<T>(IReadOnlyList<T> builtIn, List<T> items, Func<T, string> idOf, StringComparison comparison, Func<List<T>, T, int> place)
    {
        var sources = new Dictionary<string, ItemSource>();
        var all = builtIn.Select(b =>
        {
            var changed = items.FirstOrDefault(i => idOf(i).Equals(idOf(b), comparison));
            if (changed is not null)
            {
                sources[idOf(b)] = ItemSource.Changed;
            }

            return changed ?? b;
        }).ToList();
        foreach (var added in items.Where(i => !builtIn.Any(b => idOf(b).Equals(idOf(i), comparison))).OrderBy(idOf, StringComparer.Ordinal))
        {
            sources[idOf(added)] = ItemSource.Added;
            all.Insert(place(all, added), added);
        }

        return new Kind<T>(all, items, sources);
    }

    /// <summary>The one item of the assets, when it can be read and is safe (a file edited by hand into something else is left out, and the volume check says so).</summary>
    private BrandAssets? LoadAssets()
        => Items<BrandAssets>(BrandAssets.Kind, BrandAssetValidator.Normalize, BrandAssetValidator.IsSafe, _ => BrandAssets.ItemId).FirstOrDefault();

    private Snapshot Load()
    {
        var apps = Merge(AppRegistry.All, Items<MatApp>("apps", a => a, AppValidator.IsSafe, a => a.Id), a => a.Id, StringComparison.Ordinal, (all, _) => all.Count);
        var rules = Merge(RuleBook.All, Items<Rule>("rules", r => r, RuleValidator.IsSafe, r => r.Id), r => r.Id, StringComparison.Ordinal,
            (all, added) => all.FindLastIndex(r => r.Area == added.Area) is var last and >= 0 ? last + 1 : all.Count);
        var components = Merge(ComponentCatalog.All, Items<ComponentDoc>("components", ComponentValidator.Normalize, ComponentValidator.IsSafe, c => c.Id), c => c.Id, StringComparison.Ordinal,
            (all, added) => all.FindLastIndex(c => c.Group == added.Group) is var last and >= 0 ? last + 1 : all.Count);
        var brand = Items<Brand>(Brand.Kind, b => b with { Light = b.Light ?? new Dictionary<string, string>(), Dark = b.Dark ?? new Dictionary<string, string>() }, BrandValidator.IsSafe, _ => Brand.ItemId).FirstOrDefault();
        return new Snapshot(apps, rules, components, brand);
    }
}
