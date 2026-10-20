namespace MatDesign.Web.Services;

/// <summary>
/// Where a result of the search leads. One place for every address, so that a page that moves (the components have a page each, the rules may get a shorter address) is one line here and the
/// search follows.
/// </summary>
public static class SearchLinks
{
    public static string Rule(string id) => "/Rules/Detail?id=" + Uri.EscapeDataString(id);

    /// <summary>The page of one component (<c>/Components/{id}</c>); <c>/Components/All</c> shows every one.</summary>
    public static string Component(string id) => "/Components/" + Uri.EscapeDataString(id);

    /// <summary>The list of tokens with this name searched for.</summary>
    public static string Token(string name) => "/Foundations/Tokens?q=" + Uri.EscapeDataString(name);

    /// <summary>The icon of the toolkit's sprite, searched for in the gallery.</summary>
    public static string Icon(string name) => "/Foundations/Icons?q=" + Uri.EscapeDataString(name);

    /// <summary>An icon of the optional library, searched for in its gallery.</summary>
    public static string IconLibrary(string name) => "/Foundations/IconLibrary?q=" + Uri.EscapeDataString(name);

    public static string App(string id) => "/Identity/App?id=" + Uri.EscapeDataString(id);

    public static string Pattern(string id) => "/Patterns/Detail?id=" + Uri.EscapeDataString(id);

    /// <summary>The page of the results for a text, with the kind that is chosen.</summary>
    public static string Results(string query, SearchKind? kind = null)
        => "/Search?q=" + Uri.EscapeDataString(query) + (kind is { } k ? "&kind=" + SearchKinds.Name(k) : string.Empty);
}
