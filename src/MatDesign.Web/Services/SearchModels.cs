namespace MatDesign.Web.Services;

/// <summary>What a document of the search is: the things the site's search finds (docs/ARCHITECTURE.md, "Search"). The Studio's own content, the changelog and the documents are not in it.</summary>
public enum SearchKind
{
    Rule,
    Component,
    Token,
    Icon,
    Page,
    App,
    Pattern,
}

/// <summary>The names of the kinds as the API and the page use them.</summary>
public static class SearchKinds
{
    public static IReadOnlyList<SearchKind> All { get; } = Enum.GetValues<SearchKind>();

    /// <summary>The name in the JSON and in the address: <c>rule</c>, <c>component</c> ...</summary>
    public static string Name(SearchKind kind) => kind switch
    {
        SearchKind.Rule => "rule",
        SearchKind.Component => "component",
        SearchKind.Token => "token",
        SearchKind.Icon => "icon",
        SearchKind.Page => "page",
        SearchKind.App => "app",
        _ => "pattern",
    };

    /// <summary>The heading of the group of a kind: <c>Rules</c>, <c>Components</c> ...</summary>
    public static string Heading(SearchKind kind) => kind switch
    {
        SearchKind.Rule => "Rules",
        SearchKind.Component => "Components",
        SearchKind.Token => "Tokens",
        SearchKind.Icon => "Icons",
        SearchKind.Page => "Pages",
        SearchKind.App => "Apps",
        _ => "Patterns",
    };

    /// <summary>What a kind is called for one result, in the tag at the right of a suggestion: <c>Rule</c>, <c>Component</c> ...</summary>
    public static string Singular(SearchKind kind) => kind switch
    {
        SearchKind.Rule => "Rule",
        SearchKind.Component => "Component",
        SearchKind.Token => "Token",
        SearchKind.Icon => "Icon",
        SearchKind.Page => "Page",
        SearchKind.App => "App",
        _ => "Pattern",
    };

    /// <summary>The icon of the sprite that stands for a kind where a result has none of its own.</summary>
    public static string Icon(SearchKind kind) => kind switch
    {
        SearchKind.Rule => "book",
        SearchKind.Component => "toggle",
        SearchKind.Token => "palette",
        SearchKind.Icon => "star",
        SearchKind.Page => "file-text",
        SearchKind.App => "apps",
        _ => "template",
    };

    /// <summary>Reads <c>rule</c> or <c>rules</c> (any case); false for anything else.</summary>
    public static bool TryParse(string? text, out SearchKind kind)
    {
        var name = (text ?? string.Empty).Trim();
        foreach (var candidate in All)
        {
            if (name.Equals(Name(candidate), StringComparison.OrdinalIgnoreCase) || name.Equals(Heading(candidate), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }
}

/// <summary>
/// One thing the search can find: what it is, its id, the title that is shown (and found), one line of text, where it leads, the icon of the sprite that is drawn before it and the
/// group it belongs to (the area of a rule, the group of a component, a kind of token).
/// </summary>
public sealed record SearchDoc(SearchKind Kind, string Id, string Title, string Text, string Href, string Icon, string Group);

/// <summary>A document with the score that put it where it is.</summary>
public sealed record SearchHit(SearchDoc Doc, double Score);

/// <summary>The hits of one kind, in order, and how many there are of that kind in all.</summary>
public sealed record SearchGroup(SearchKind Kind, string Name, int Count, IReadOnlyList<SearchHit> Hits);

/// <summary>
/// The answer to one query: every document that matches all its words, best first (<see cref="Hits"/>), and how many there are of each kind. <see cref="Top"/> takes the best of them for a
/// popup; the page takes a kind and pages through it.
/// </summary>
public sealed class SearchResults
{
    public static SearchResults Empty { get; } = new(string.Empty, [], []);

    public SearchResults(string query, IReadOnlyList<string> terms, IReadOnlyList<SearchHit> hits)
    {
        Query = query;
        Terms = terms;
        Hits = hits;
        Counts = hits.GroupBy(h => h.Doc.Kind).ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>The query as it was asked (trimmed, one space between words).</summary>
    public string Query { get; }

    /// <summary>The words of the query, folded: what the page highlights.</summary>
    public IReadOnlyList<string> Terms { get; }

    /// <summary>Every hit, best first.</summary>
    public IReadOnlyList<SearchHit> Hits { get; }

    public int Total => Hits.Count;

    public IReadOnlyDictionary<SearchKind, int> Counts { get; }

    public int CountOf(SearchKind kind) => Counts.GetValueOrDefault(kind);

    /// <summary>The hits of one kind, best first.</summary>
    public IEnumerable<SearchHit> Of(SearchKind kind) => Hits.Where(h => h.Doc.Kind == kind);

    /// <summary>
    /// The best <paramref name="limit"/> hits (of one <paramref name="kind"/>, or of all), put into groups by kind: the group of the best hit first, and in each group the hits in the order of their score.
    /// <see cref="SearchGroup.Count"/> is the number of hits of that kind in all, not in the groups.
    /// </summary>
    public IReadOnlyList<SearchGroup> Top(int limit, SearchKind? kind = null)
    {
        var take = (kind is { } k ? Of(k) : Hits).Take(Math.Max(0, limit)).ToList();
        return take
            .GroupBy(h => h.Doc.Kind)
            .Select(g => new SearchGroup(g.Key, SearchKinds.Heading(g.Key), CountOf(g.Key), g.ToList()))
            .ToList();
    }
}
