using System.Globalization;
using System.Text;
using MatDesign.Core;

namespace MatDesign.Web.Services;

/// <summary>
/// A tile of the overview at <c>/Components</c>: a component with what the tile says about it. <see cref="Search"/> is what the filter looks at (name, summary,
/// group, id and the ids of its rules, folded: lower case, no accents); the page carries only <see cref="ComponentOverview.KeysOf"/> (the name and the summary are in the tile),
/// and the script folds the same three parts, so that the script and the server filter by the same text.
/// <see cref="Visible"/> is false for a tile the query or the group filter hides; it is still in the page (hidden), so that the script can show it again.
/// </summary>
public sealed record ComponentTile(ComponentDoc Doc, int Examples, int Rules, int Properties, string Search, bool Visible)
{
    /// <summary>Where the tile leads: the page of the component.</summary>
    public string Url => ComponentCatalog.PageOf(Doc);
}

/// <summary>A group of the overview with its tiles, in the order of the catalogue.</summary>
public sealed record TileGroup(string Group, string Anchor, string Summary, IReadOnlyList<ComponentTile> Tiles)
{
    public int VisibleCount => Tiles.Count(t => t.Visible);
}

/// <summary>
/// The overview of the components (LIST-18): a tile per component under the heading of its group, no example drawn. Pure: the page model and the unit tests build it from
/// the same data, and the filter is the same as the script's (<c>wwwroot/js/components.js</c>): every word of the query must be in the folded text of the tile.
/// </summary>
public static class ComponentOverview
{
    /// <summary>Lower case without accents, so that "Ä" is found by "a" and "date" by "Date". Letters only change; nothing is added.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(char.ToLowerInvariant(c));
            }
        }

        return folded.ToString();
    }

    /// <summary>True when every word of <paramref name="query"/> is in <paramref name="folded"/> (the folded text of a tile); an empty query matches everything.</summary>
    public static bool Matches(string folded, string? query)
        => string.IsNullOrWhiteSpace(query) || Fold(query).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).All(word => folded.Contains(word, StringComparison.Ordinal));

    /// <summary>What a tile does not show in words: the group, the id and the ids of the rules (written into the tile as <c>data-keys</c>).</summary>
    public static string KeysOf(ComponentDoc doc) => $"{doc.Group} {doc.Id} {string.Join(' ', doc.Rules)}";

    /// <summary>The text of a tile the filter looks at: the name, the summary and the keys.</summary>
    public static string SearchText(ComponentDoc doc) => Fold($"{doc.Name} {doc.Summary} {KeysOf(doc)}");

    /// <summary>
    /// The tiles of the groups of <paramref name="section"/>, a group per heading, in the order of the catalogue (a group without any component is left out).
    /// <paramref name="query"/> and <paramref name="group"/> only decide which tile is <see cref="ComponentTile.Visible"/>.
    /// </summary>
    public static IReadOnlyList<TileGroup> Build(IEnumerable<ComponentDoc> docs, CatalogSection section, IReadOnlyList<ComponentProperty> properties, string? query = null, string? group = null)
    {
        var list = docs.ToList();
        var groups = new List<TileGroup>();
        foreach (var name in section.Groups)
        {
            var tiles = list.Where(d => d.Group == name).Select(d =>
            {
                var search = SearchText(d);
                var visible = (string.IsNullOrEmpty(group) || group == name) && Matches(search, query);
                return new ComponentTile(d, d.Examples.Count, d.Rules.Count, ComponentProperties.ForDoc(properties, d.Id).Count, search, visible);
            }).ToList();
            if (tiles.Count > 0)
            {
                groups.Add(new TileGroup(name, ComponentCatalog.GroupAnchor(name), ComponentCatalog.GroupSummaries.GetValueOrDefault(name, string.Empty), tiles));
            }
        }

        return groups;
    }
}

/// <summary>Where a component stands in the sequence of the components and its neighbours (the buttons and keys of the page of a component).</summary>
public sealed record ComponentNeighbours(ComponentDoc Current, int Position, int Count, ComponentDoc? Previous, ComponentDoc? Next, IReadOnlyList<ComponentDoc> InGroup)
{
    /// <summary>The previous component starts another group (or there is none): the button says where it leads.</summary>
    public bool PreviousIsOtherGroup => Previous is not null && Previous.Group != Current.Group;

    public bool NextIsOtherGroup => Next is not null && Next.Group != Current.Group;
}

/// <summary>
/// The order in which the pages of the components are walked with the Previous and Next buttons and the arrow keys: the groups in the order of the catalogue and, inside a group, the
/// order of the catalogue; the last component of a group is followed by the first of the next one. There is no wrap-around: the first has no previous and the last no next.
/// </summary>
public static class ComponentPaging
{
    public static IReadOnlyList<ComponentDoc> Sequence(IEnumerable<ComponentDoc> docs, CatalogSection section)
    {
        var list = docs.ToList();
        return [.. section.Groups.SelectMany(g => list.Where(d => d.Group == g))];
    }

    /// <summary>The neighbours of the component <paramref name="id"/> in <paramref name="sequence"/>, or null when it is not in it.</summary>
    public static ComponentNeighbours? Around(IReadOnlyList<ComponentDoc> sequence, string id)
    {
        var index = -1;
        for (var i = 0; i < sequence.Count; i++)
        {
            if (sequence[i].Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return null;
        }

        var current = sequence[index];
        return new ComponentNeighbours(
            current,
            index + 1,
            sequence.Count,
            index > 0 ? sequence[index - 1] : null,
            index < sequence.Count - 1 ? sequence[index + 1] : null,
            [.. sequence.Where(d => d.Group == current.Group)]);
    }
}
