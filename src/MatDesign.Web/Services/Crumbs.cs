namespace MatDesign.Web.Services;

/// <summary>Builds the breadcrumb a page hands to the layout (below "MatDesign"). The last item is the current page.</summary>
public static class Crumbs
{
    public static IReadOnlyList<(string Text, string? Url)> Of(params (string Text, string? Url)[] items) => items;
}
