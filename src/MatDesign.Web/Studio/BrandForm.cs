using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>
/// What the forms of the brand, of a colour scheme and of the overrides of an app have in common (docs/STUDIO.md, "Brand"): the names of the fields (<c>light:--mat-bg</c>, <c>dark:--mat-bg</c>,
/// <c>component:--mat-btn-radius</c>, <c>property:--mat-btn-radius</c>, <c>scheme:paper:light:--mat-bg</c>), reading them from a posted form, and the words for the note of a revision.
/// </summary>
public static class BrandForm
{
    /// <summary>The fields of a form that start with <paramref name="prefix"/>: the name after the prefix and what was typed (an empty field is there too: it is how a value is cleared).</summary>
    public static Dictionary<string, string> Values(IFormCollection form, string prefix)
        => form.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToDictionary(k => k[prefix.Length..], k => form[k].ToString(), StringComparer.Ordinal);

    /// <summary>What the form sets inside each colour scheme: the fields <c>scheme:paper:light:--mat-bg</c> and <c>scheme:paper:dark:--mat-bg</c>.</summary>
    public static Dictionary<string, SchemeValues> Schemes(IFormCollection form)
    {
        var schemes = new Dictionary<string, SchemeValues>();
        foreach (var scheme in ColorSchemes.All)
        {
            var light = Values(form, $"scheme:{scheme.Id}:light:");
            var dark = Values(form, $"scheme:{scheme.Id}:dark:");
            if (light.Count > 0 || dark.Count > 0)
            {
                schemes[scheme.Id] = new SchemeValues(light, dark);
            }
        }

        return schemes;
    }

    /// <summary>The id of the input of a token in a colour scheme: <c>scheme-paper-light-mat-bg</c>.</summary>
    public static string SchemeId(string scheme, bool dark, BrandToken token) => $"scheme-{scheme}-{(dark ? "dark" : "light")}-{token.Key}";

    /// <summary>The names whose value differs between two maps, each with the label in front (<c>light --mat-radius</c>), at most six, in a fixed order.</summary>
    public static IEnumerable<string> Changed(string label, IReadOnlyDictionary<string, string>? a, IReadOnlyDictionary<string, string>? b)
    {
        a ??= new Dictionary<string, string>();
        b ??= new Dictionary<string, string>();
        return b.Keys.Union(a.Keys).Where(k => a.GetValueOrDefault(k) != b.GetValueOrDefault(k)).Order(StringComparer.Ordinal).Take(6).Select(name => $"{label} {name}");
    }

    /// <summary>The note of a revision of the brand: the names that differ (at most six per kind), so that the history and the changelog say what was touched.</summary>
    public static string What(Brand before, Brand after)
    {
        var names = new List<string>();
        if (before.Company != after.Company) { names.Add("company"); }
        if (before.ShortName != after.ShortName) { names.Add("short name"); }
        if (before.Tagline != after.Tagline) { names.Add("tagline"); }

        void Differ(string label, IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) => names.AddRange(Changed(label, a, b));

        Differ("light", before.Values(false), after.Values(false));
        Differ("dark", before.Values(true), after.Values(true));
        Differ("component", before.ComponentValues, after.ComponentValues);
        foreach (var scheme in ColorSchemes.All)
        {
            Differ($"{scheme.Id} light", before.SchemeFor(scheme.Id)?.Values(false) ?? new Dictionary<string, string>(), after.SchemeFor(scheme.Id)?.Values(false) ?? new Dictionary<string, string>());
            Differ($"{scheme.Id} dark", before.SchemeFor(scheme.Id)?.Values(true) ?? new Dictionary<string, string>(), after.SchemeFor(scheme.Id)?.Values(true) ?? new Dictionary<string, string>());
        }

        return names.Count == 0 ? "nothing" : string.Join(", ", names);
    }
}
