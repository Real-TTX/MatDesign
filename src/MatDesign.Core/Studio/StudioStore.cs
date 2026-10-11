using System.Text.Json;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>One saved state of an item: who changed it, when, why, and the whole item (or the mark that it was deleted).</summary>
public sealed record Revision(int Number, DateTimeOffset At, string By, string Note, bool Deleted, JsonElement Data);

/// <summary>An item of a kind with its newest revision.</summary>
public sealed record StoredItem(string Kind, string Id, Revision Revision);

/// <summary>
/// What the Studio saves. Every item is an append-only log: <c>studio/&lt;kind&gt;/&lt;id&gt;/&lt;revision&gt;.json</c>, one file per save, holding who, when, why and
/// the whole item. The newest file is the current state, so history, comparison and restore need nothing else. A write goes to a temporary file that is
/// renamed, so a crash leaves either the old state or the new one. The store is for one process and one administrator; a lock keeps its writes in order.
/// </summary>
public sealed partial class StudioStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonElement NullData = JsonSerializer.SerializeToElement<object?>(null);

    private readonly string _root;
    private readonly object _gate = new();

    public StudioStore(string dataDirectory)
    {
        _root = Path.Combine(dataDirectory, "studio");
        Directory.CreateDirectory(_root);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]{0,31}$")]
    private static partial Regex KindPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex IdPattern();

    /// <summary>A kind is a lower-case word ("apps", "rules"); it is a folder name, so nothing else is accepted.</summary>
    public static bool IsKind(string? kind) => kind is not null && KindPattern().IsMatch(kind);

    /// <summary>An id is a file name: letters, digits, dot, dash and underscore, never a path.</summary>
    public static bool IsId(string? id) => id is not null && IdPattern().IsMatch(id) && !id.Contains("..", StringComparison.Ordinal);

    private string Folder(string kind, string id)
    {
        if (!IsKind(kind))
        {
            throw new ArgumentException($"'{kind}' is not a kind of item.", nameof(kind));
        }

        if (!IsId(id))
        {
            throw new ArgumentException($"'{id}' is not an id.", nameof(id));
        }

        return Path.Combine(_root, kind, id);
    }

    private static string FileOf(string folder, int number) => Path.Combine(folder, number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".json");

    private static IEnumerable<(int Number, string Path)> Files(string folder)
    {
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "??????.json"))
        {
            if (int.TryParse(Path.GetFileNameWithoutExtension(path), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n))
            {
                yield return (n, path);
            }
        }
    }

    private static Revision Read(string path) => JsonSerializer.Deserialize<Revision>(File.ReadAllText(path), Options)!;

    /// <summary>Saves a new state of the item and returns its revision.</summary>
    public Revision Save(string kind, string id, JsonElement data, string by, string note = "") => Append(kind, id, data, deleted: false, by, note);

    /// <summary>Saves an object as the new state of the item.</summary>
    public Revision Save<T>(string kind, string id, T item, string by, string note = "") => Save(kind, id, JsonSerializer.SerializeToElement(item, Options), by, note);

    /// <summary>Marks the item as deleted. Its history stays, and it can be restored.</summary>
    public Revision Delete(string kind, string id, string by, string note = "") => Append(kind, id, NullData, deleted: true, by, note);

    /// <summary>Saves the content of an earlier revision as the newest one.</summary>
    public Revision Restore(string kind, string id, int number, string by)
    {
        var old = History(kind, id).FirstOrDefault(r => r.Number == number)
            ?? throw new KeyNotFoundException($"{kind}/{id} has no revision {number}.");
        return Append(kind, id, old.Data, old.Deleted, by, $"Restored revision {number}");
    }

    private Revision Append(string kind, string id, JsonElement data, bool deleted, string by, string note)
    {
        var folder = Folder(kind, id);
        lock (_gate)
        {
            Directory.CreateDirectory(folder);
            var number = Files(folder).Select(f => f.Number).DefaultIfEmpty(0).Max() + 1;
            var revision = new Revision(number, DateTimeOffset.UtcNow, Clean(by, 64), Clean(note, 500), deleted, data);
            var target = FileOf(folder, number);
            var temp = target + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(revision, Options));
            File.Move(temp, target, overwrite: false);
            return revision;
        }
    }

    private static string Clean(string text, int max)
    {
        var line = string.Join(' ', (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.Length <= max ? line : line[..max];
    }

    /// <summary>The newest revision of an item, or null when it was never saved.</summary>
    public Revision? Current(string kind, string id)
    {
        lock (_gate)
        {
            var newest = Files(Folder(kind, id)).OrderByDescending(f => f.Number).FirstOrDefault();
            return newest.Path is null ? null : Read(newest.Path);
        }
    }

    /// <summary>The item as an object, or null when it was never saved or is deleted.</summary>
    public T? Get<T>(string kind, string id) where T : class
        => Current(kind, id) is { Deleted: false } revision ? revision.Data.Deserialize<T>(Options) : null;

    /// <summary>Every revision of an item, the newest first.</summary>
    public IReadOnlyList<Revision> History(string kind, string id)
    {
        lock (_gate)
        {
            return Files(Folder(kind, id)).OrderByDescending(f => f.Number).Select(f => Read(f.Path)).ToList();
        }
    }

    /// <summary>The items of a kind with their newest revision, by id. Deleted items are left out unless asked for.</summary>
    public IReadOnlyList<StoredItem> List(string kind, bool includeDeleted = false)
    {
        if (!IsKind(kind))
        {
            throw new ArgumentException($"'{kind}' is not a kind of item.", nameof(kind));
        }

        lock (_gate)
        {
            var folder = Path.Combine(_root, kind);
            if (!Directory.Exists(folder))
            {
                return [];
            }

            var items = new List<StoredItem>();
            foreach (var directory in Directory.EnumerateDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var newest = Files(directory).OrderByDescending(f => f.Number).FirstOrDefault();
                if (newest.Path is null)
                {
                    continue;
                }

                var revision = Read(newest.Path);
                if (includeDeleted || !revision.Deleted)
                {
                    items.Add(new StoredItem(kind, Path.GetFileName(directory), revision));
                }
            }

            return items;
        }
    }

    /// <summary>The kinds that have at least one item.</summary>
    public IReadOnlyList<string> Kinds()
    {
        lock (_gate)
        {
            return Directory.EnumerateDirectories(_root).Select(d => Path.GetFileName(d)).Where(IsKind).Order(StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>The newest revisions of everything, the newest first: what the activity list shows.</summary>
    public IReadOnlyList<StoredItem> Activity(int take = 200, string? kind = null)
    {
        lock (_gate)
        {
            var all = new List<StoredItem>();
            foreach (var k in kind is null ? Kinds() : [kind])
            {
                if (!IsKind(k) || !Directory.Exists(Path.Combine(_root, k)))
                {
                    continue;
                }

                foreach (var directory in Directory.EnumerateDirectories(Path.Combine(_root, k)))
                {
                    foreach (var (_, path) in Files(directory))
                    {
                        all.Add(new StoredItem(k, Path.GetFileName(directory), Read(path)));
                    }
                }
            }

            return all.OrderByDescending(i => i.Revision.At).ThenByDescending(i => i.Revision.Number).Take(take).ToList();
        }
    }
}
