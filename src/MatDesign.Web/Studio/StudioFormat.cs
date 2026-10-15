using System.Globalization;
using System.Text.Json;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>How the Studio words what it stored: the same on every machine (UTC, invariant culture).</summary>
public static class StudioFormat
{
    /// <summary>What a revision did, in a word.</summary>
    public static string What(Revision revision)
        => revision.Deleted ? "Deleted" : revision.Number == 1 ? "Created" : revision.Note.StartsWith("Restored", StringComparison.Ordinal) ? "Restored" : "Changed";

    /// <summary>What the activity list calls an item: the id, and for a person (kind <c>users</c>) their name, because the id is only a stable handle.</summary>
    public static string ItemTitle(StoredItem item)
        => item.Kind == UserEvent.Kind && item.Revision.Data.ValueKind == JsonValueKind.Object && item.Revision.Data.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString()!
            : item.Id;

    /// <summary>"1 item", "2 items".</summary>
    public static string Count(int number, string noun) => number == 1 ? $"1 {noun}" : $"{number} {noun}s";

    public static string When(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

    /// <summary>The name of a kind as a person says it ("apps" becomes "App").</summary>
    public static string KindName(string kind) => kind switch
    {
        "apps" => "App",
        "rules" => "Rule",
        "components" => "Component",
        "releases" => "Release",
        "runs" => "Run",
        "usage" => "Version",
        "brand" => "Brand",
        "whitelabel" => "White label",
        "assets" => "Brand assets",
        "users" => "Person",
        _ => char.ToUpperInvariant(kind[0]) + kind[1..],
    };
}
