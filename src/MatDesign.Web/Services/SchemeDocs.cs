using MatDesign.Core;

namespace MatDesign.Web.Services;

/// <summary>
/// A row of the contrast table of a colour scheme: one pairing with its ratio in light and in dark. A pairing that depends on the accent shows the weakest accent
/// (the ratio is the lowest of all the apps and presets); a mode that the scheme does not have (a dark-only scheme in light mode, which is the standard look) is null.
/// </summary>
public sealed record SchemeCheckRow(string Pairing, double? Light, string? LightAccent, double? Dark, string? DarkAccent, double Required, bool Advisory)
{
    public bool Passes => (Light is null || Light >= Required) && (Dark is null || Dark >= Required);
}

/// <summary>What the pages show about the colour schemes (<c>COLOR-09</c>): the contrast numbers and the tokens, read from the CSS through <see cref="ToolkitFiles"/>.</summary>
public static class SchemeDocs
{
    private static string Stem(string name)
        => name.Replace(" (light)", string.Empty, StringComparison.Ordinal).Replace(" (dark)", string.Empty, StringComparison.Ordinal);

    private static List<(string Stem, double Ratio, double Required, bool Advisory, string? Accent)> Entries(SchemeSurfaces surfaces, IReadOnlyList<MatApp> apps)
        =>
        [
            .. surfaces.Checks().Select(c => (Stem(c.Name), c.Ratio, c.Required, c.Advisory, (string?)null)),
            .. SchemeContrast.Weakest(surfaces, apps).Select(w => (Stem(w.Check), w.Ratio, w.Required, w.Advisory, (string?)w.Accent)),
        ];

    /// <summary>
    /// The pairings of a scheme (null or "standard" for the standard look) in light and in dark: first those that do not depend on the accent, then those that do, each with the
    /// weakest of the <paramref name="apps"/> and the presets. Required ones have to reach AA; advice is shown as such.
    /// </summary>
    public static IReadOnlyList<SchemeCheckRow> Rows(ToolkitFiles toolkit, string? scheme, IEnumerable<MatApp> apps)
    {
        var list = apps.ToList();
        var darkOnly = ColorSchemes.Get(ColorSchemes.Normalize(scheme))?.DarkOnly == true;
        var light = darkOnly ? [] : Entries(toolkit.Surfaces(scheme, dark: false), list);
        var dark = Entries(toolkit.Surfaces(scheme, dark: true), list);
        return dark
            .Select(d =>
            {
                var l = light.FirstOrDefault(x => x.Stem == d.Stem);
                return new SchemeCheckRow(d.Stem, l.Stem is null ? null : l.Ratio, l.Accent, d.Ratio, d.Accent, d.Required, d.Advisory);
            })
            .ToList();
    }

    /// <summary>
    /// What one scheme sets, token by token and in the order of the base tokens: the value in light and in dark in that scheme and what the token is for. Where a scheme has no
    /// light value (a dark-only scheme) the light value is the standard one: in light mode that is what is shown.
    /// </summary>
    public static IReadOnlyList<DesignToken> TokensOf(ToolkitFiles toolkit, string schemeId)
    {
        var block = toolkit.Schemes.FirstOrDefault(b => b.Id == schemeId);
        if (block is null)
        {
            return [];
        }

        var order = toolkit.Tokens.Select((t, i) => (t.Name, i)).ToDictionary(x => x.Name, x => x.i);
        var byName = toolkit.Tokens.ToDictionary(t => t.Name);
        return (block.Light?.Keys ?? []).Concat(block.Dark!.Keys).Distinct()
            .OrderBy(n => order.GetValueOrDefault(n, int.MaxValue))
            .Select(name => new DesignToken(name, block.Light?.GetValueOrDefault(name) ?? byName[name].Light, block.Dark!.GetValueOrDefault(name) ?? byName[name].Dark, byName[name].Description, byName[name].Group))
            .ToList();
    }

    /// <summary>Every scheme's tokens as rows for the page of the tokens (the group "Schemes"): the description says which scheme the value belongs to.</summary>
    public static IReadOnlyList<DesignToken> TokenRows(ToolkitFiles toolkit)
        => [.. ColorSchemes.All.SelectMany(scheme => TokensOf(toolkit, scheme.Id).Select(t => t with { Description = $"{scheme.Name}: {t.Description}", Group = "Schemes" }))];
}
