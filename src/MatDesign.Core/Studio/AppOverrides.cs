namespace MatDesign.Core.Studio;

/// <summary>
/// What an app may set for itself on top of the brand (docs/PRODUCT.md, stage 12). The order in which values win is the toolkit's defaults, the brand, the app (<c>[data-app]</c>) and, last, a
/// colour scheme the user chose. An app owns its accent (the accent family is derived from it) and its shape; the **surfaces, lines and text are the brand's**, so that the apps of a company look like
/// one family. So the tokens open to an app are the ones that are shape, spacing, type and motion (<see cref="Allows"/>), with the limits and the checks of the brand's editor, plus every
/// documented component property. The dark values are for a token that has a dark value (none of the subset has one today; the writer and the validator handle them for the day one does).
/// </summary>
public static class AppOverrides
{
    private static readonly string[] Groups = ["Shape", "Spacing", "Type", "Motion"];

    /// <summary>True when an app may set the token: a token that is not a colour, in the groups shape, spacing, type and motion of <c>matui.tokens.css</c>.</summary>
    public static bool Allows(BrandToken token)
        => token.Kind != BrandTokenKind.Colour && Groups.Any(g => token.Group.StartsWith(g, StringComparison.Ordinal));

    /// <summary>The tokens an app may set, from the tokens a brand may set, in the order of the CSS.</summary>
    public static IReadOnlyList<BrandToken> Tokens(IReadOnlyList<BrandToken> editable) => editable.Where(Allows).ToList();

    /// <summary>The app with its overrides in the written form of their kinds, without what is empty or the toolkit's own, in the order of the CSS; an app that sets nothing has no maps at all.</summary>
    public static MatApp Normalize(MatApp app, IReadOnlyList<BrandToken> editable, IReadOnlyList<PropertyToken> properties)
    {
        var allowed = Tokens(editable);
        var light = BrandValidator.NormalizeValues(app.Tokens?.Light, allowed, dark: false);
        var dark = BrandValidator.NormalizeValues(app.Tokens?.Dark, allowed, dark: true);
        var props = BrandValidator.NormalizeValues(app.Properties, properties.Select(p => p.Token).ToList(), dark: false);
        return app with
        {
            Tokens = light.Count > 0 || dark.Count > 0 ? new AppTokens(light, dark) : null,
            Properties = props.Count > 0 ? props : null,
        };
    }

    /// <summary>
    /// True when the overrides can be written into a stylesheet without harm: names that look like tokens and values that are safe. The Studio checks this again for an app read from the disk, because
    /// the volume is a place a person can edit by hand; the kind and the limits of each value are <see cref="Validate"/>.
    /// </summary>
    public static bool IsSafe(MatApp app)
        => BrandValidator.SafeMap(app.Tokens?.Light) && BrandValidator.SafeMap(app.Tokens?.Dark) && BrandValidator.SafeMap(app.Properties);

    /// <summary>
    /// What is wrong with the overrides of <paramref name="app"/>, which must be <see cref="Normalize"/>d: a token the brand owns, a value that is not of its kind or outside its limits, a dark value
    /// for a token with none, a property that is not documented, the type scale (with the brand's sizes under the app's) and the knob of a switch. The field of a problem is
    /// <c>Light:--mat-radius</c>, <c>Dark:--mat-...</c> or <c>Property:--mat-btn-radius</c>.
    /// </summary>
    public static IReadOnlyList<Problem> Validate(MatApp app, IReadOnlyList<BrandToken> editable, IReadOnlyList<PropertyToken> properties, Brand? brand = null)
    {
        var problems = new List<Problem>();
        if (!app.HasOverrides)
        {
            return problems;
        }

        var all = editable.ToDictionary(t => t.Name);
        var ok = true;
        foreach (var (mode, dark) in new[] { ("Light", false), ("Dark", true) })
        {
            foreach (var (name, value) in app.Tokens?.Values(dark) ?? new Dictionary<string, string>())
            {
                var field = $"{mode}:{name}";
                if (!all.TryGetValue(name, out var token))
                {
                    problems.Add(new(field, $"{name} is not a token that can be set."));
                    ok = false;
                }
                else if (!Allows(token))
                {
                    problems.Add(new(field, $"{name} belongs to the brand: an app sets its shape, spacing, type and motion, and its accent, but not the colours of the surfaces."));
                    ok = false;
                }
                else if (dark && !token.HasDark)
                {
                    problems.Add(new(field, $"{name} has no dark value: one value, in the Light column, serves both modes."));
                    ok = false;
                }
                else if (BrandValidator.CheckValue(token, value) is { } message)
                {
                    problems.Add(new(field, message));
                    ok = false;
                }
            }
        }

        ok &= BrandValidator.CheckComponents(app.Properties ?? new Dictionary<string, string>(), "Property", properties, problems, brand?.ComponentValues);
        if (ok)
        {
            // the scale is the brand's with the app's over it, and only the app's sizes are blamed: the brand's own values stand under them
            var baseTokens = editable.Select(t => brand is not null && brand.Light.TryGetValue(t.Name, out var own) ? t with { Light = own } : t).ToList();
            problems.AddRange(BrandValidator.CheckScale(app.Tokens?.Light ?? new Dictionary<string, string>(), baseTokens, "Light"));
        }

        return problems;
    }
}
