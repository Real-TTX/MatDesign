using System.Globalization;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>What kind of value a token holds, which decides how a brand may set it and how the editor draws its input.</summary>
public enum BrandTokenKind
{
    /// <summary>A colour: <c>#rgb</c>, <c>#rrggbb</c>, <c>rgb(r, g, b)</c> or <c>rgba(r, g, b, a)</c>.</summary>
    Colour,

    /// <summary>A length in px, rem or em, within a range.</summary>
    Length,

    /// <summary>A number without a unit (a line height), within a range.</summary>
    Number,

    /// <summary>A time in ms or s, within a range.</summary>
    Duration,

    /// <summary>A font stack: names and a generic family at the end, from a safe set of characters.</summary>
    FontStack,

    /// <summary>An easing: a keyword or <c>cubic-bezier(x1, y1, x2, y2)</c>.</summary>
    Easing,
}

/// <summary>
/// A token that a brand may set: its name, group and description as <c>matui.tokens.css</c> gives them, the kind of its value, the limits that protect usability (<see cref="Min"/> and
/// <see cref="Max"/> in px for a length, in ms for a duration, as they are for a number) and the toolkit's own values (<see cref="Light"/>, and <see cref="Dark"/> only for the tokens the
/// dark set of the CSS sets: a brand can give those a dark value of their own).
/// </summary>
public sealed record BrandToken(string Name, string Group, string Description, BrandTokenKind Kind, string Light, string? Dark, double? Min, double? Max, string Hint, string? Why)
{
    public bool HasDark => Dark is not null;

    /// <summary>A name that is safe in an id attribute: <c>--mat-bg</c> becomes <c>mat-bg</c>.</summary>
    public string Key => Name.TrimStart('-');
}

/// <summary>A token that a brand cannot set here, and why.</summary>
public sealed record FixedToken(string Name, string Group, string Reason);

/// <summary>
/// Which tokens of <c>matui.tokens.css</c> a brand may set, and within which limits. The CSS is the source of truth (the tokens are read by <see cref="CssTokens"/>); this decides, from a token's
/// group, name and value, whether it is a colour, a length, a number, a duration, a font stack or an easing, and which tokens are structure instead of look. The limits are the ones the rulebook
/// states: touch targets of 44 px (36 px for compact controls, <c>MOBILE-04</c>), a body text of 14 px or more, a line height of 1.2 to 2, radii of 0 to 24 px.
/// </summary>
public static partial class BrandTokens
{
    private static readonly string[] FixedGroups = ["Accent", "Layers", "Colour picker", "Device"];

    [GeneratedRegex(@"^-?\d*\.?\d+(px|rem|em)$")]
    private static partial Regex LengthPattern();

    [GeneratedRegex(@"^\d*\.?\d+$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^\d*\.?\d+(ms|s)$")]
    private static partial Regex DurationPattern();

    /// <summary>The tokens a brand may set, in the order of the CSS.</summary>
    public static IReadOnlyList<BrandToken> Editable(IReadOnlyList<DesignToken> tokens)
        => tokens.Select(Describe).Where(t => t is not null).Select(t => t!).ToList();

    /// <summary>The tokens a brand may not set, each with the reason: the accent family is derived from the accent of an app, the layers and the safe areas are structure, a shadow is not one value.</summary>
    public static IReadOnlyList<FixedToken> Fixed(IReadOnlyList<DesignToken> tokens)
        => tokens.Where(t => Describe(t) is null).Select(t => new FixedToken(t.Name, t.Group, ReasonFixed(t))).ToList();

    private static string ReasonFixed(DesignToken token)
    {
        if (token.Group == "Accent")
        {
            return "Derived from the accent of each app: an app sets one colour, everything else follows.";
        }

        if (token.Group is "Layers" or "Device")
        {
            return "Structure, not look: the order of the layers and the safe areas of a device.";
        }

        if (token.Group == "Colour picker")
        {
            return "A colour model that is the same in light and dark.";
        }

        if (token.Name == "--mat-radius-pill")
        {
            return "Always a full pill.";
        }

        return token.Name switch
        {
            "--mat-accent-text-keep" => "Set by a colour scheme where its surfaces need darker links.",
            "--mat-gap" or "--mat-content-pad" => "Computed from other tokens.",
            _ when token.Name.StartsWith("--mat-shadow", StringComparison.Ordinal) => "A shadow is several values, not one.",
            _ => "Not one value of a kind a brand may set.",
        };
    }

    /// <summary>The token as a brand sees it, or null when a brand cannot set it.</summary>
    public static BrandToken? Describe(DesignToken token)
    {
        if (FixedGroups.Contains(token.Group) || token.Name is "--mat-accent-text-keep" or "--mat-gap" or "--mat-content-pad" or "--mat-radius-pill")
        {
            return null;
        }

        var value = token.Light.Trim();
        BrandTokenKind kind;
        if (value.StartsWith('#') || value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            kind = BrandTokenKind.Colour;
        }
        else if (LengthPattern().IsMatch(value))
        {
            kind = BrandTokenKind.Length;
        }
        else if (DurationPattern().IsMatch(value))
        {
            kind = BrandTokenKind.Duration;
        }
        else if (NumberPattern().IsMatch(value))
        {
            kind = BrandTokenKind.Number;
        }
        else if (token.Name is "--mat-font" or "--mat-mono")
        {
            kind = BrandTokenKind.FontStack;
        }
        else if (value.StartsWith("cubic-bezier(", StringComparison.Ordinal))
        {
            kind = BrandTokenKind.Easing;
        }
        else
        {
            return null;
        }

        var (min, max, why) = Limits(token.Name, kind);
        return new BrandToken(token.Name, token.Group, token.Description, kind, token.Light, token.Dark, min, max, HintOf(kind, min, max), why);
    }

    private static (double? Min, double? Max, string? Why) Limits(string name, BrandTokenKind kind)
    {
        const string touch = "Touch targets are at least 44 px, 36 px for compact controls (MOBILE-04).";
        switch (kind)
        {
            case BrandTokenKind.Duration:
                return (0, 1000, "Motion stays under a second, or it is in the way.");
            case BrandTokenKind.Number:
                return name is "--mat-lh" or "--mat-lh-tight" ? (1.2, 2, "A line height of 1.2 to 2 keeps text readable.") : (null, null, null);
            case BrandTokenKind.Length:
                break;
            default:
                return (null, null, null);
        }

        return name switch
        {
            "--mat-radius-xs" or "--mat-radius-sm" or "--mat-radius" or "--mat-radius-lg" or "--mat-radius-xl" => (0, 24, "A radius of 0 to 24 px: larger turns a control into something else (the pill is its own token)."),
            "--mat-control-h" or "--mat-control-h-lg" => (44, 72, touch),
            "--mat-control-h-sm" => (36, 56, touch),
            "--mat-topbar-h" => (48, 96, "A bar that holds controls of 44 px."),
            "--mat-nav-w" => (200, 400, "A menu narrower than this cuts its labels, a wider one eats the page."),
            "--mat-content-max" => (640, 2400, "The width of the page content."),
            "--mat-fs-xs" => (11, 16, "Captions stay readable: 11 px or more."),
            "--mat-fs-sm" => (12, 18, "Table text stays readable: 12 px or more."),
            "--mat-fs-md" => (14, 22, "Body text is at least 14 px."),
            "--mat-fs-lg" => (16, 28, null),
            "--mat-fs-xl" => (18, 36, null),
            "--mat-fs-2xl" => (22, 48, null),
            "--mat-fs-3xl" => (26, 72, null),
            _ when name.StartsWith("--mat-space-", StringComparison.Ordinal) => (2, 96, "The spacing scale stays between 2 and 96 px."),
            _ => (null, null, null),
        };
    }

    private static string HintOf(BrandTokenKind kind, double? min, double? max)
    {
        static string N(double? v) => v!.Value.ToString("0.##", CultureInfo.InvariantCulture);
        return kind switch
        {
            BrandTokenKind.Colour => "A colour: #rrggbb, or rgba(r, g, b, a) for a tint.",
            BrandTokenKind.Length when min is not null => $"{N(min)} to {N(max)} px (a length in px, rem or em).",
            BrandTokenKind.Length => "A length in px, rem or em.",
            BrandTokenKind.Number when min is not null => $"A number from {N(min)} to {N(max)}.",
            BrandTokenKind.Number => "A number without a unit.",
            BrandTokenKind.Duration => $"{N(min)} to {N(max)} ms (a time in ms or s).",
            BrandTokenKind.FontStack => "Font names separated by commas, ending in a generic family such as sans-serif.",
            _ => "ease, ease-in, ease-out, ease-in-out, linear or cubic-bezier(x1, y1, x2, y2).",
        };
    }

    /// <summary>A length in px (rem and em count 16 px), or null when the text is not a length.</summary>
    public static double? Pixels(string value)
    {
        var text = value.Trim();
        if (text == "0")
        {
            return 0;
        }

        if (!LengthPattern().IsMatch(text))
        {
            return null;
        }

        var unit = text.EndsWith("rem", StringComparison.Ordinal) ? 3 : 2;
        var number = double.Parse(text[..^unit], CultureInfo.InvariantCulture);
        return text.EndsWith("px", StringComparison.Ordinal) ? number : number * 16;
    }
}
