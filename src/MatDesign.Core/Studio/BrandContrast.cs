using System.Globalization;

namespace MatDesign.Core.Studio;

/// <summary>
/// One contrast pairing of the toolkit on the surfaces of a brand: in which colour scheme and mode, what it is, the ratio it reaches and needs, and, for a pairing that depends on the accent, the
/// app that reaches it least well. <see cref="Tokens"/> names the colours of a pairing that does not depend on the accent (the foreground, the background and what a tint is painted over).
/// </summary>
public sealed record BrandContrastResult(string Scheme, string Mode, string Name, double Ratio, double Required, bool Advisory, string? Accent, string? Tokens)
{
    public bool Passes => Ratio >= Required;

    /// <summary>A required pairing that is not reached.</summary>
    public bool Fails => !Advisory && !Passes;

    /// <summary>What a pairing is called wherever it appears: the scheme and its name (which has the mode in it).</summary>
    public string Key => Scheme + "|" + Name;
}

/// <summary>
/// The contrast checks of the toolkit (<see cref="SchemeSurfaces.Checks()"/>, <see cref="AccentReport.Checks(SchemeSurfaces)"/>) run on the surfaces of a brand: the toolkit's own tokens with the brand's
/// values over them, in light and in dark, in the standard look and in every colour scheme, with the accent of every app. This is what <c>COLOR-11</c> asks for before a brand is saved or released.
/// </summary>
public static class BrandContrast
{
    private static IEnumerable<string?> Schemes() => new string?[] { null }.Concat(ColorSchemes.All.Select(s => s.Id));

    /// <summary>Every pairing, for every scheme and mode, on the surfaces of <paramref name="brand"/> (null: the toolkit's own).</summary>
    public static IReadOnlyList<BrandContrastResult> Run(IReadOnlyList<DesignToken> tokens, IReadOnlyList<SchemeBlocks> schemes, Brand? brand, IEnumerable<MatApp> apps)
    {
        var appList = apps.ToList();
        var results = new List<BrandContrastResult>();
        foreach (var scheme in Schemes())
        {
            foreach (var surfaces in SchemeSurfaces.Modes(tokens, schemes, scheme, brand?.Values(false), brand?.Values(true)))
            {
                foreach (var pairing in SchemeSurfaces.Pairings)
                {
                    var check = surfaces.Check(pairing);
                    var colours = pairing.Over is null ? $"{pairing.Foreground} on {pairing.Background}" : $"{pairing.Foreground} on {pairing.Background} over {pairing.Over}";
                    results.Add(new BrandContrastResult(surfaces.Scheme, surfaces.Mode, check.Name, check.Ratio, check.Required, check.Advisory, null, colours));
                }

                foreach (var weakest in SchemeContrast.Weakest(surfaces, appList))
                {
                    results.Add(new BrandContrastResult(surfaces.Scheme, surfaces.Mode, weakest.Check, weakest.Ratio, weakest.Required, weakest.Advisory, weakest.Accent, null));
                }
            }
        }

        return results;
    }

    /// <summary>The pairings that do not depend on the accent in the standard look, for the light and the dark mode of the brand: what the editor shows next to the token table.</summary>
    public static IReadOnlyList<BrandContrastResult> Standard(IReadOnlyList<DesignToken> tokens, IReadOnlyList<SchemeBlocks> schemes, Brand? brand)
    {
        var results = new List<BrandContrastResult>();
        foreach (var surfaces in SchemeSurfaces.Modes(tokens, schemes, null, brand?.Values(false), brand?.Values(true)))
        {
            foreach (var pairing in SchemeSurfaces.Pairings)
            {
                var check = surfaces.Check(pairing);
                results.Add(new BrandContrastResult(surfaces.Scheme, surfaces.Mode, check.Name, check.Ratio, check.Required, check.Advisory, null, pairing.Foreground + " on " + pairing.Background));
            }
        }

        return results;
    }

    /// <summary>
    /// The required pairings that the brand does not reach, as problems: a pairing that fails in the toolkit's own look already (the accent of an app added in the Studio that a scheme does not suit) is
    /// not the brand's fault and is only reported when the brand makes it worse. A brand that sets nothing has nothing to check.
    /// </summary>
    public static IReadOnlyList<Problem> Problems(IReadOnlyList<DesignToken> tokens, IReadOnlyList<SchemeBlocks> schemes, Brand brand, IEnumerable<MatApp> apps)
    {
        if (!brand.HasOverrides)
        {
            return [];
        }

        var appList = apps.ToList();
        var baseline = Run(tokens, schemes, null, appList).ToDictionary(r => r.Key);
        var problems = new List<Problem>();
        foreach (var result in Run(tokens, schemes, brand, appList).Where(r => r.Fails))
        {
            if (baseline.TryGetValue(result.Key, out var own) && own.Fails && own.Ratio <= result.Ratio + 0.005)
            {
                continue;
            }

            var where = result.Scheme == ColorSchemes.Standard ? string.Empty : $" In the {result.Scheme} scheme.";
            var who = result.Accent is null ? string.Empty : $" Weakest with {result.Accent}.";
            var colours = result.Tokens is null ? string.Empty : $" The colours: {result.Tokens}.";
            problems.Add(new("Contrast", $"{result.Name}: {Ratio(result.Ratio)}:1, needs {Ratio(result.Required)}:1.{where}{who}{colours}"));
        }

        return problems;
    }

    private static string Ratio(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
