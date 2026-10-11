using System.Text.RegularExpressions;

namespace MatDesign.Web.Services;

/// <summary>
/// What a developer looks for before copying an example: the classes of the toolkit and the data attributes that the markup of a component uses, in the
/// order they appear. They are read from the examples themselves, so the list can never differ from what the examples show.
/// </summary>
public static partial class ComponentApi
{
    [GeneratedRegex(@"\sclass=""([^""]*)""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex(@"\s(data-[a-z][a-z0-9-]*)")]
    private static partial Regex DataAttribute();

    /// <summary>The <c>mat-</c> classes of the examples (not the icon class, which every icon has).</summary>
    public static IReadOnlyList<string> Classes(ComponentDoc doc)
        => doc.Examples
            .SelectMany(e => ClassAttribute().Matches(e.Html))
            .SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(c => c.StartsWith("mat-", StringComparison.Ordinal) && c != "mat-icon")
            .Distinct()
            .ToList();

    /// <summary>The <c>data-</c> attributes of the examples that the script of the toolkit reads (not <c>data-label</c>, which the tables repeat in every cell).</summary>
    public static IReadOnlyList<string> Attributes(ComponentDoc doc)
        => doc.Examples
            .SelectMany(e => DataAttribute().Matches(e.Html))
            .Select(m => m.Groups[1].Value)
            .Where(a => a != "data-label")
            .Distinct()
            .ToList();
}
