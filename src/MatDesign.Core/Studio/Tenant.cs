using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatDesign.Core.Studio;

/// <summary>
/// The company that runs this instance (docs/PRODUCT.md, stage 14): how it names itself and which template its design data start from. There is one tenant per instance. It is
/// written once by the setup assistant to <c>tenant.json</c> and then changed in the Studio like the other items (the item <see cref="ItemId"/> of the kind <see cref="Kind"/>, with a
/// history and a restore). An instance that has none behaves as the <c>demo</c> template, so a volume from before the tenants and every test that never set one see what they always saw.
/// </summary>
/// <param name="Company">The full name of the company.</param>
/// <param name="ShortName">For places where the full name does not fit; also the word in "the Mat apps".</param>
/// <param name="Template"><c>demo</c> (the Mat apps and everything of the family as sample data) or <c>blank</c> (the toolkit's own content only).</param>
/// <param name="SiteName">What the site calls itself: the name under the logo, the title of the pages, the footer, the version line.</param>
/// <param name="Tagline">The line under the site name in the menu.</param>
/// <param name="Accent">The accent of the site itself (#rrggbb) for the <c>blank</c> template, which has no app of its own to take it from; empty means the template's own.</param>
public sealed record Tenant(string Company, string ShortName, string Template, string SiteName, string Tagline, string Accent = "")
{
    /// <summary>The kind of the Studio item that holds the tenant.</summary>
    public const string Kind = "tenant";

    /// <summary>The id of the one item of that kind.</summary>
    public const string ItemId = "tenant";

    /// <summary>The name of the file the setup assistant writes in the data directory.</summary>
    public const string FileName = "tenant.json";

    private static readonly JsonSerializerOptions FileJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The tenant of an instance that has none: the demo, exactly as the site always was.</summary>
    public static Tenant Demo { get; } = new("Mat", "Mat", DesignTemplates.DemoId, "MatDesign", "Identity of the Mat apps");

    /// <summary>The name the site gives itself when nobody chose one: "Acme Design"; the demo template is always "MatDesign".</summary>
    public static string DefaultSiteName(string shortName, string template)
        => template == DesignTemplates.DemoId ? Demo.SiteName : $"{shortName} Design";

    /// <summary>The short name made from the company: its first word, shortened to what fits.</summary>
    public static string ShortNameOf(string company)
    {
        var first = (company ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return first.Length <= 16 ? first : first[..16];
    }

    /// <summary>A tenant for a company and a template, with the names the site would give it itself.</summary>
    public static Tenant For(string company, string template, string? shortName = null)
    {
        var name = (company ?? string.Empty).Trim();
        var shortened = string.IsNullOrWhiteSpace(shortName) ? ShortNameOf(name) : shortName.Trim();
        var tagline = template == DesignTemplates.DemoId ? Demo.Tagline : $"Identity of the {shortened} apps";
        return new Tenant(name, shortened, template, DefaultSiteName(shortened, template), tagline, DesignTemplates.Get(template).DefaultAccent);
    }

    [JsonIgnore]
    public bool IsDemo => Template == DesignTemplates.DemoId;

    /// <summary>"Mat apps": the apps of the company, as the texts of the site say it.</summary>
    [JsonIgnore]
    public string Apps => $"{ShortName} apps";

    /// <summary>"Mat app".</summary>
    [JsonIgnore]
    public string App => $"{ShortName} app";

    /// <summary>"Mat family".</summary>
    [JsonIgnore]
    public string Family => $"{ShortName} family";

    /// <summary>The file in <paramref name="directory"/>; null when there is none. A file that cannot be read is an error and never "no tenant" (the site would turn into another one).</summary>
    public static Tenant? ReadFile(string directory)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var tenant = JsonSerializer.Deserialize<Tenant>(File.ReadAllText(path), FileJson);
            if (tenant is null)
            {
                throw new InvalidDataException("The file is empty.");
            }

            tenant = TenantValidator.Normalize(tenant);
            return TenantValidator.IsSafe(tenant) ? tenant : throw new InvalidDataException("It is not a valid tenant.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new InvalidDataException($"{path} is not a valid tenant file ({ex.Message}). Fix or delete it.", ex);
        }
    }

    /// <summary>Writes the file (the setup assistant does this once). It is finished before it appears under its name.</summary>
    public void WriteFile(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, FileJson));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
