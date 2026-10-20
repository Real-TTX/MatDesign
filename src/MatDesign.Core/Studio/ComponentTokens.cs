using System.Globalization;

namespace MatDesign.Core.Studio;

/// <summary>
/// A documented component property (<c>--mat-btn-radius</c>, read from the <c>@prop</c> lines of the CSS) as the editor of the Studio sees it: the property, the <see cref="BrandToken"/> that holds
/// its kind and limits (so the validator, the normaliser and the editor treat it like a token), whether it is a touch target and what the control uses today when it is not set.
/// </summary>
/// <param name="Property">The property as the CSS declares it.</param>
/// <param name="Token">Its kind (colour, length, number, duration, font stack) and the limits that protect usability; <c>Token.Light</c> is the fallback as the CSS writes it.</param>
/// <param name="Touch">True for the heights of a control that is pressed (<see cref="ComponentTokens.TouchProperties"/>): a value under 44 px applies with a fine pointer only (<c>MOBILE-04</c>).</param>
/// <param name="Resolved">What the fallback is today: the value of the token it names (<c>var(--mat-radius)</c> is <c>6px</c>) or the literal.</param>
public sealed record PropertyToken(ComponentProperty Property, BrandToken Token, bool Touch, string Resolved)
{
    public string Name => Property.Name;

    /// <summary>A name that is safe in an id attribute: <c>--mat-btn-radius</c> becomes <c>mat-btn-radius</c>.</summary>
    public string Key => Token.Key;

    /// <summary>True for a property whose fallback is a token (<c>var(--mat-radius)</c>) and not a literal.</summary>
    public bool FallsBackToToken => Property.Fallback.StartsWith("var(", StringComparison.Ordinal);
}

/// <summary>
/// Which component properties the Studio may set and within which limits (docs/PRODUCT.md, stage 12). The properties come from the CSS (<see cref="ComponentProperties"/>); this decides from the
/// name and the type what the limits are: a radius of 0 to 24 px, padding and gaps of 0 to 48 px, a border of 0 to 4 px, the width of a dialog or a toast of 200 to 960 px, the weight of letters of
/// 100 to 900, the sizes of a small label, and, for the heights of the controls that are pressed, a floor. <c>MOBILE-04</c> asks for 44 px on a touch screen: a height under 44 px is valid
/// and is written for <c>(pointer: fine)</c> only, so it can never apply to a touch screen, and a height under <see cref="FloorPx"/> is refused.
/// </summary>
public static class ComponentTokens
{
    /// <summary>The least height of a control that is pressed, with a fine pointer. Below this the editor refuses a value.</summary>
    public const double FloorPx = 32;

    /// <summary>The height of a touch target on a touch screen (<c>MOBILE-04</c>): a height under this is written for a fine pointer only.</summary>
    public const double TouchPx = 44;

    /// <summary>The properties that are the height of a control that is pressed: they fall back to <c>--mat-control-h</c>, which is 44 px on a touch screen.</summary>
    public static readonly IReadOnlyList<string> TouchProperties =
        ["--mat-btn-height", "--mat-input-height", "--mat-tabs-height", "--mat-menu-item-height", "--mat-nav-item-height", "--mat-search-height"];

    /// <summary>The properties that the Studio may set, in the order of the CSS, each with its kind and limits.</summary>
    public static IReadOnlyList<PropertyToken> Editable(IReadOnlyList<ComponentProperty> properties, IReadOnlyList<DesignToken> tokens)
    {
        var byName = tokens.ToDictionary(t => t.Name);
        return properties.Select(p => Describe(p, byName)).ToList();
    }

    private static PropertyToken Describe(ComponentProperty property, IReadOnlyDictionary<string, DesignToken> tokens)
    {
        var kind = property.Type switch
        {
            "length" => BrandTokenKind.Length,
            "number" => BrandTokenKind.Number,
            "duration" => BrandTokenKind.Duration,
            "colour" => BrandTokenKind.Colour,
            _ => BrandTokenKind.FontStack,
        };

        var touch = TouchProperties.Contains(property.Name);
        var (min, max, why) = Limits(property.Name, kind, touch);
        var group = ComponentProperties.Components.FirstOrDefault(c => c.Id == property.Component)?.Title ?? property.Component;
        var token = new BrandToken(property.Name, group, property.Description, kind, property.Fallback, null, min, max, Hint(kind, min, max, touch), why);

        var resolved = property.Fallback;
        if (property.Fallback.StartsWith("var(--", StringComparison.Ordinal) && property.Fallback.EndsWith(')') && tokens.TryGetValue(property.Fallback[4..^1], out var named))
        {
            resolved = named.Light;
        }

        return new PropertyToken(property, token, touch, resolved);
    }

    private static (double? Min, double? Max, string? Why) Limits(string name, BrandTokenKind kind, bool touch)
    {
        if (kind != BrandTokenKind.Length && kind != BrandTokenKind.Number)
        {
            return kind == BrandTokenKind.Duration ? (0, 1000, "Motion stays under a second, or it is in the way.") : (null, null, null);
        }

        if (kind == BrandTokenKind.Number)
        {
            return name.EndsWith("-font-weight", StringComparison.Ordinal) ? (100, 900, "A weight of letters is a number from 100 to 900.") : (null, null, null);
        }

        if (touch)
        {
            return (FloorPx, 72, $"A control that is pressed is at least 44 px high on a touch screen (MOBILE-04): a value under 44 px is written for a fine pointer (a mouse) only, and none under {FloorPx.ToString("0", CultureInfo.InvariantCulture)} px is taken.");
        }

        return name switch
        {
            _ when name.EndsWith("-radius", StringComparison.Ordinal) => (0, 24, "A radius of 0 to 24 px: larger turns a control into something else."),
            _ when name.EndsWith("-border-width", StringComparison.Ordinal) || name.EndsWith("-indicator-width", StringComparison.Ordinal) => (0, 4, "A line of 0 to 4 px."),
            "--mat-dialog-width" or "--mat-toast-width" => (200, 960, "A dialog or a toast of 200 to 960 px: narrower cuts its content, wider is more than a screen."),
            "--mat-search-width" => (160, 560, "A search field in the header bar of 160 to 560 px: narrower shows too little of what is typed, wider pushes the breadcrumb away."),
            "--mat-table-font-size" or "--mat-topbar-crumb-font-size" => (12, 22, "Text in a table and a breadcrumb stays readable: 12 px or more."),
            _ when name.EndsWith("-font-size", StringComparison.Ordinal) => (11, 18, "The text of a small label stays readable: 11 px or more."),
            "--mat-badge-size" => (14, 32, "The size of a count badge."),
            "--mat-pill-height" => (18, 40, "The height of a status pill."),
            "--mat-check-size" => (14, 28, "The size of a checkbox or radio button."),
            "--mat-switch-width" => (28, 72, "The length of the track of a switch."),
            "--mat-switch-height" => (16, 40, "The height of the track of a switch."),
            "--mat-switch-thumb" => (10, 36, "The knob of a switch."),
            _ when name.EndsWith("-pad", StringComparison.Ordinal) || name.EndsWith("-pad-x", StringComparison.Ordinal) || name.EndsWith("-pad-y", StringComparison.Ordinal) || name.EndsWith("-gap", StringComparison.Ordinal)
                => (0, 48, "Space of 0 to 48 px."),
            _ => (null, null, null),
        };
    }

    private static string Hint(BrandTokenKind kind, double? min, double? max, bool touch)
    {
        static string N(double? v) => v!.Value.ToString("0.##", CultureInfo.InvariantCulture);
        return kind switch
        {
            BrandTokenKind.Length when touch => $"{N(min)} to {N(max)} px; under 44 px only with a mouse.",
            BrandTokenKind.Length when min is not null => $"{N(min)} to {N(max)} px.",
            BrandTokenKind.Length => "A length in px, rem or em.",
            BrandTokenKind.Number when min is not null => $"A number from {N(min)} to {N(max)}.",
            BrandTokenKind.Number => "A number without a unit.",
            BrandTokenKind.Duration => $"{N(min)} to {N(max)} ms.",
            BrandTokenKind.Colour => "A colour: #rrggbb.",
            _ => "Font names ending in a generic family.",
        };
    }

    /// <summary>True when the value of a touch property is under the height of a touch target: it is written for a fine pointer only.</summary>
    public static bool AppliesToFinePointerOnly(PropertyToken property, string value)
        => property.Touch && BrandTokens.Pixels(value) is { } px && px < TouchPx;
}
