using System.Globalization;
using System.Text.Json;

namespace MatDesign.Core.Studio;

/// <summary>A field that differs between two revisions of an item. A missing side is null.</summary>
public sealed record FieldChange(string Path, string? Before, string? After)
{
    public bool Added => Before is null;

    public bool Removed => After is null;
}

/// <summary>A line of a text that differs: <see cref="Op"/> is a space (kept), a plus (new) or a minus (gone).</summary>
public sealed record DiffLine(char Op, string Text);

/// <summary>
/// What changed between two revisions, for the person who has to decide whether to restore one: the fields that differ (an object is read field by field, a list item by item, like
/// <c>examples[0].html</c>) and, for a text of several lines, the lines that differ.
/// </summary>
public static class Diff
{
    /// <summary>Every scalar of the item by its path. An object adds its field to the path, a list its index; null reads as an empty string.</summary>
    public static IReadOnlyDictionary<string, string> Flatten(JsonElement element)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(element, string.Empty, fields);
        return fields;
    }

    private static void Walk(JsonElement element, string path, Dictionary<string, string> fields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", fields);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, $"{path}[{index++.ToString(CultureInfo.InvariantCulture)}]", fields);
                }

                break;
            case JsonValueKind.Undefined:
                break;
            default:
                fields[path.Length == 0 ? "value" : path] = element.ValueKind switch
                {
                    JsonValueKind.Null => string.Empty,
                    JsonValueKind.String => element.GetString() ?? string.Empty,
                    _ => element.GetRawText(),
                };
                break;
        }
    }

    /// <summary>The fields that differ from <paramref name="before"/> to <paramref name="after"/>, in the order of the item (the fields that are gone last).</summary>
    public static IReadOnlyList<FieldChange> Changes(JsonElement before, JsonElement after)
    {
        var a = Flatten(before);
        var b = Flatten(after);
        var changes = new List<FieldChange>();
        foreach (var (path, value) in b)
        {
            if (!a.TryGetValue(path, out var old))
            {
                changes.Add(new FieldChange(path, null, value));
            }
            else if (old != value)
            {
                changes.Add(new FieldChange(path, old, value));
            }
        }

        changes.AddRange(a.Where(x => !b.ContainsKey(x.Key)).Select(x => new FieldChange(x.Key, x.Value, null)));
        return changes;
    }

    /// <summary>The lines of two texts with what was kept, what is new and what is gone (the longest common run of lines is kept).</summary>
    public static IReadOnlyList<DiffLine> Lines(string? before, string? after)
    {
        var a = Split(before);
        var b = Split(after);

        // longest common subsequence of the lines
        var length = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                length[i, j] = a[i] == b[j] ? length[i + 1, j + 1] + 1 : Math.Max(length[i + 1, j], length[i, j + 1]);
            }
        }

        var lines = new List<DiffLine>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                lines.Add(new DiffLine(' ', a[x]));
                x++;
                y++;
            }
            else if (length[x + 1, y] >= length[x, y + 1])
            {
                lines.Add(new DiffLine('-', a[x++]));
            }
            else
            {
                lines.Add(new DiffLine('+', b[y++]));
            }
        }

        while (x < a.Length)
        {
            lines.Add(new DiffLine('-', a[x++]));
        }

        while (y < b.Length)
        {
            lines.Add(new DiffLine('+', b[y++]));
        }

        return lines;
    }

    private static string[] Split(string? text) => string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
