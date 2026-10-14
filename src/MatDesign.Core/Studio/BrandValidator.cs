using System.Globalization;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// What a brand has to be, checked before the Studio saves it (<c>COLOR-11</c>): a company name, a short name and a tagline that say something, and token values that are values of the
/// kind of their token (a colour, a length, a number, a duration, a font stack, an easing), within the limits that protect usability (touch targets of 44 px, a body text of 14 px or more, a line
/// height of 1.2 to 2, radii of 0 to 24 px), for tokens the toolkit defines and a brand may set. Colours are held to the contrast the toolkit holds itself to (<see cref="BrandContrast"/>): the
/// surfaces of the brand, in light and dark and in every colour scheme, with the accent of every app. A pairing that fails is an error that blocks saving; advice stays advice.
/// </summary>
public static partial class BrandValidator
{
    /// <summary>The longest value of a token (a font stack is the longest).</summary>
    public const int MaxValueLength = 300;

    private static readonly string[] GenericFamilies =
        ["serif", "sans-serif", "monospace", "cursive", "fantasy", "system-ui", "ui-serif", "ui-sans-serif", "ui-monospace", "ui-rounded"];

    private static readonly string[] EasingKeywords = ["ease", "ease-in", "ease-out", "ease-in-out", "linear"];

    [GeneratedRegex(@"^--mat-[a-z0-9-]{1,40}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^#(?:[0-9a-f]{3}|[0-9a-f]{6})$", RegexOptions.IgnoreCase)]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*(\d*\.?\d+%?)\s*)?\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbPattern();

    [GeneratedRegex(@"^(-?\d*\.?\d+)(px|rem|em)$")]
    private static partial Regex LengthPattern();

    [GeneratedRegex(@"^\d*\.?\d+$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(\d*\.?\d+)(ms|s)$")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"^cubic-bezier\(\s*(-?\d*\.?\d+)\s*,\s*(-?\d*\.?\d+)\s*,\s*(-?\d*\.?\d+)\s*,\s*(-?\d*\.?\d+)\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex BezierPattern();

    // letters, digits, space, comma, point, quotes, percent, hash, parentheses, dash and underscore: enough for every value of every kind, nothing that ends a declaration or a rule
    [GeneratedRegex(@"^[A-Za-z0-9 ,.'""%#()_-]*$")]
    private static partial Regex SafeCharacters();

    [GeneratedRegex(@"^[A-Za-z0-9 ._-]+$")]
    private static partial Regex FontName();

    private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------------------------------------ what is safe to write into a stylesheet

    /// <summary>
    /// True when a value can be written into a custom property without harm: nothing that ends a declaration or a rule (<c>; { } &lt; &gt; \</c>), no comment, no <c>url(</c>, <c>@import</c> or
    /// <c>expression</c>, balanced quotes and brackets. The kind and the limits of a token are <see cref="CheckValue"/>; this is the part that never depends on which token it is.
    /// </summary>
    public static bool IsSafeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxValueLength || !SafeCharacters().IsMatch(value))
        {
            return false;
        }

        var lower = value.ToLowerInvariant();
        if (lower.Contains("url(", StringComparison.Ordinal) || lower.Contains("expression", StringComparison.Ordinal) || lower.Contains("import", StringComparison.Ordinal) || lower.Contains("var(", StringComparison.Ordinal))
        {
            return false;
        }

        var depth = 0;
        foreach (var c in value)
        {
            depth += c == '(' ? 1 : c == ')' ? -1 : 0;
            if (depth < 0)
            {
                return false;
            }
        }

        return depth == 0 && value.Count(c => c == '"') % 2 == 0 && value.Count(c => c == '\'') % 2 == 0;
    }

    /// <summary>
    /// True when the brand can be written into a stylesheet and a page without harm: texts of a sane length, names of tokens that look like tokens, values that are safe. The Studio checks this again
    /// when it reads the item from the disk, because the volume is a place a person can edit by hand; the kind and the limits of each token are <see cref="Validate"/>.
    /// </summary>
    public static bool IsSafe(Brand brand)
        => !string.IsNullOrWhiteSpace(brand.Company) && brand.Company.Length <= 60
            && !string.IsNullOrWhiteSpace(brand.ShortName) && brand.ShortName.Length <= 16
            && (brand.Tagline ?? string.Empty).Length <= 90
            && SafeMap(brand.Light) && SafeMap(brand.Dark);

    private static bool SafeMap(IReadOnlyDictionary<string, string>? map)
        => map is null || map.All(p => NamePattern().IsMatch(p.Key) && IsSafeValue(p.Value));

    // ------------------------------------------------------------------------------------------------ normalising

    /// <summary>
    /// The brand as it is stored: texts trimmed, every value in the written form of its kind (<c>#fff</c> is <c>#ffffff</c>, <c>0.50rem</c> is <c>0.5rem</c>), an empty value or a value that is the toolkit's own
    /// left out (a brand that sets nothing writes nothing), the tokens in the order of the CSS. Names that are no token of a brand stay, so that <see cref="Validate"/> can say so.
    /// </summary>
    public static Brand Normalize(Brand brand, IReadOnlyList<BrandToken> tokens)
        => brand with
        {
            Company = (brand.Company ?? string.Empty).Trim(),
            ShortName = (brand.ShortName ?? string.Empty).Trim(),
            Tagline = (brand.Tagline ?? string.Empty).Trim(),
            Light = NormalizeMap(brand.Light, tokens, dark: false),
            Dark = NormalizeMap(brand.Dark, tokens, dark: true),
        };

    private static Dictionary<string, string> NormalizeMap(IReadOnlyDictionary<string, string>? map, IReadOnlyList<BrandToken> tokens, bool dark)
    {
        var byName = tokens.ToDictionary(t => t.Name);
        var cleaned = new Dictionary<string, string>();
        foreach (var (rawName, rawValue) in map ?? new Dictionary<string, string>())
        {
            var name = (rawName ?? string.Empty).Trim().ToLowerInvariant();
            var value = (rawValue ?? string.Empty).Trim();
            if (name.Length == 0 || value.Length == 0)
            {
                continue;
            }

            if (byName.TryGetValue(name, out var token))
            {
                value = NormalizeValue(token, value);
                var own = dark ? token.Dark : token.Light;
                if (own is not null && NormalizeValue(token, own) == value)
                {
                    continue;
                }
            }

            cleaned[name] = value;
        }

        var order = tokens.Select((t, i) => (t.Name, i)).ToDictionary(x => x.Name, x => x.i);
        return cleaned.OrderBy(p => order.GetValueOrDefault(p.Key, int.MaxValue)).ThenBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
    }

    /// <summary>The written form of a value of this token: what it is compared and stored as. A value that is not a value of the kind is only trimmed and its spaces collapsed.</summary>
    public static string NormalizeValue(BrandToken token, string value)
    {
        var text = Regex.Replace(value.Trim(), @"\s+", " ");
        switch (token.Kind)
        {
            case BrandTokenKind.Colour:
                return ParseColour(text) is { } colour ? colour.Alpha >= 1 ? colour.Color.ToHex() : $"rgba({colour.Color.R:0}, {colour.Color.G:0}, {colour.Color.B:0}, {Number(colour.Alpha)})" : text;
            case BrandTokenKind.Length:
                return text == "0" ? "0px" : LengthPattern().Match(text) is { Success: true } length ? Number(double.Parse(length.Groups[1].Value, CultureInfo.InvariantCulture)) + length.Groups[2].Value.ToLowerInvariant() : text;
            case BrandTokenKind.Number:
                return NumberPattern().IsMatch(text) ? Number(double.Parse(text, CultureInfo.InvariantCulture)) : text;
            case BrandTokenKind.Duration:
                return DurationPattern().Match(text.ToLowerInvariant()) is { Success: true } time ? Number(double.Parse(time.Groups[1].Value, CultureInfo.InvariantCulture)) + time.Groups[2].Value : text;
            case BrandTokenKind.FontStack:
                return string.Join(", ", text.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0));
            default:
                if (BezierPattern().Match(text) is { Success: true } bezier)
                {
                    return "cubic-bezier(" + string.Join(", ", Enumerable.Range(1, 4).Select(i => Number(double.Parse(bezier.Groups[i].Value, CultureInfo.InvariantCulture)))) + ")";
                }

                return text.ToLowerInvariant();
        }
    }

    /// <summary>A colour in the written forms a brand may use, or null.</summary>
    public static Rgba? ParseColour(string text)
    {
        var value = text.Trim();
        if (HexPattern().IsMatch(value))
        {
            return new Rgba(Rgb.Parse(value.ToLowerInvariant()), 1);
        }

        if (RgbPattern().Match(value) is not { Success: true } m)
        {
            return null;
        }

        var channels = Enumerable.Range(1, 3).Select(i => int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture)).ToList();
        if (channels.Any(c => c > 255))
        {
            return null;
        }

        var alpha = 1.0;
        if (m.Groups[4].Success)
        {
            var raw = m.Groups[4].Value;
            alpha = raw.EndsWith('%') ? double.Parse(raw[..^1], CultureInfo.InvariantCulture) / 100 : double.Parse(raw, CultureInfo.InvariantCulture);
            if (alpha is < 0 or > 1)
            {
                return null;
            }
        }

        return new Rgba(new Rgb(channels[0], channels[1], channels[2]), alpha);
    }

    // ------------------------------------------------------------------------------------------------ one value

    /// <summary>
    /// What is wrong with <paramref name="value"/> (already <see cref="NormalizeValue"/>d) as a value of <paramref name="token"/>, in words that say how to fix it; null when it is fine: the kind,
    /// the characters, and the range of a length, a number or a duration.
    /// </summary>
    public static string? CheckValue(BrandToken token, string value)
    {
        if (value.Length > MaxValueLength)
        {
            return $"At most {MaxValueLength} characters.";
        }

        if (!IsSafeValue(value))
        {
            return "This value has a character or a word that does not belong in a value (a semicolon, a brace, a url, an import).";
        }

        switch (token.Kind)
        {
            case BrandTokenKind.Colour:
                return ParseColour(value) is null ? "A colour like #2f6feb or rgba(31, 30, 27, 0.13)." : null;
            case BrandTokenKind.Length:
                if (Pixels(value) is not { } pixels)
                {
                    return "A length like 16px, 1rem or 0.5em.";
                }

                return OutOfRange(token, pixels, "px");
            case BrandTokenKind.Number:
                if (!NumberPattern().IsMatch(value))
                {
                    return "A number without a unit, like 1.5.";
                }

                return OutOfRange(token, double.Parse(value, CultureInfo.InvariantCulture), string.Empty);
            case BrandTokenKind.Duration:
                if (DurationPattern().Match(value) is not { Success: true } time)
                {
                    return "A time like 180ms or 0.2s.";
                }

                var ms = double.Parse(time.Groups[1].Value, CultureInfo.InvariantCulture) * (time.Groups[2].Value == "s" ? 1000 : 1);
                return OutOfRange(token, ms, "ms");
            case BrandTokenKind.FontStack:
                return CheckFontStack(value);
            default:
                if (EasingKeywords.Contains(value))
                {
                    return null;
                }

                if (BezierPattern().Match(value) is not { Success: true } bezier)
                {
                    return "ease, ease-in, ease-out, ease-in-out, linear or cubic-bezier(x1, y1, x2, y2).";
                }

                foreach (var i in new[] { 1, 3 })
                {
                    var x = double.Parse(bezier.Groups[i].Value, CultureInfo.InvariantCulture);
                    if (x is < 0 or > 1)
                    {
                        return "In cubic-bezier(x1, y1, x2, y2) the x values are from 0 to 1.";
                    }
                }

                return null;
        }
    }

    private static double? Pixels(string value) => BrandTokens.Pixels(value);

    private static string? OutOfRange(BrandToken token, double value, string unit)
    {
        var suffix = unit.Length == 0 ? string.Empty : " " + unit;
        var why = token.Why is null ? string.Empty : " " + token.Why;
        if (token.Min is { } min && value < min)
        {
            return $"{Number(value)}{suffix} is below the least this token takes, {Number(min)}{suffix}.{why}";
        }

        if (token.Max is { } max && value > max)
        {
            return $"{Number(value)}{suffix} is above the most this token takes, {Number(max)}{suffix}.{why}";
        }

        return null;
    }

    private static string? CheckFontStack(string value)
    {
        if (value.Length < 3)
        {
            return "Name at least one font and a generic family.";
        }

        var parts = value.Split(',').Select(p => p.Trim()).ToList();
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                return "A comma without a font name.";
            }

            var quoted = part.Length >= 3 && ((part[0] == '"' && part[^1] == '"') || (part[0] == '\'' && part[^1] == '\''));
            if (!FontName().IsMatch(quoted ? part[1..^1] : part))
            {
                return $"'{part}' is not a font name: letters, digits, spaces, dots, dashes and quotes only.";
            }
        }

        return GenericFamilies.Contains(parts[^1].ToLowerInvariant())
            ? null
            : "End the stack with a generic family (" + string.Join(", ", GenericFamilies.Take(3)) + " ...), so that text has a font when the others are missing.";
    }

    // ------------------------------------------------------------------------------------------------ the brand

    /// <summary>
    /// Everything that is wrong with <paramref name="brand"/>, which must be <see cref="Normalize"/>d: the texts, every value (the name must be a token a brand may set, the value of its kind and
    /// within its limits, a dark value only for a token that has one), the type scale, and the contrast of the colours (<see cref="BrandContrast"/>). <paramref name="designTokens"/> and
    /// <paramref name="schemes"/> are what <see cref="CssTokens"/> reads from the CSS; <paramref name="apps"/> are the apps whose accents have to reach AA on the brand's surfaces.
    /// </summary>
    public static IReadOnlyList<Problem> Validate(Brand brand, IReadOnlyList<DesignToken> designTokens, IReadOnlyList<SchemeBlocks> schemes, IEnumerable<MatApp> apps)
    {
        var tokens = BrandTokens.Editable(designTokens);
        var problems = new List<Problem>();

        if (brand.Company.Length is < 2 or > 60)
        {
            problems.Add(new("Company", "Name the company in 2 to 60 characters."));
        }

        if (brand.ShortName.Length is < 1 or > 16)
        {
            problems.Add(new("ShortName", "Give a short name of 1 to 16 characters, for places where the full name does not fit."));
        }

        if (brand.Tagline.Length > 90)
        {
            problems.Add(new("Tagline", "Keep the tagline to 90 characters."));
        }

        var valuesOk = CheckMap(brand.Light, "Light", dark: false, tokens, problems);
        valuesOk &= CheckMap(brand.Dark, "Dark", dark: true, tokens, problems);
        if (!valuesOk)
        {
            return problems;
        }

        problems.AddRange(CheckScale(brand, tokens));
        problems.AddRange(BrandContrast.Problems(designTokens, schemes, brand, apps));
        return problems;
    }

    private static bool CheckMap(IReadOnlyDictionary<string, string>? map, string mode, bool dark, IReadOnlyList<BrandToken> tokens, List<Problem> problems)
    {
        var ok = true;
        var byName = tokens.ToDictionary(t => t.Name);
        foreach (var (name, value) in map ?? new Dictionary<string, string>())
        {
            var field = $"{mode}:{name}";
            if (!byName.TryGetValue(name, out var token))
            {
                problems.Add(new(field, $"{name} is not a token a brand can set."));
                ok = false;
            }
            else if (dark && !token.HasDark)
            {
                problems.Add(new(field, $"{name} has no dark value: one value, in the Light column, serves both modes."));
                ok = false;
            }
            else if (CheckValue(token, value) is { } message)
            {
                problems.Add(new(field, message));
                ok = false;
            }
        }

        return ok;
    }

    private static readonly string[] Scale = ["--mat-fs-xs", "--mat-fs-sm", "--mat-fs-md", "--mat-fs-lg", "--mat-fs-xl", "--mat-fs-2xl", "--mat-fs-3xl"];

    /// <summary>The type scale stays a scale: each size is at least the one before it, with the brand's values over the toolkit's.</summary>
    private static IEnumerable<Problem> CheckScale(Brand brand, IReadOnlyList<BrandToken> tokens)
    {
        var byName = tokens.ToDictionary(t => t.Name);
        double? Size(string name) => brand.Light.TryGetValue(name, out var v) ? Pixels(v) : byName.TryGetValue(name, out var t) ? Pixels(t.Light) : null;
        for (var i = 1; i < Scale.Length; i++)
        {
            if (Size(Scale[i]) is { } size && Size(Scale[i - 1]) is { } before && size < before && brand.Light.ContainsKey(Scale[i]))
            {
                yield return new($"Light:{Scale[i]}", $"{Scale[i]} ({Number(size)} px) is smaller than {Scale[i - 1]} ({Number(before)} px): the type scale only grows.");
            }
            else if (Size(Scale[i]) is { } later && Size(Scale[i - 1]) is { } earlier && later < earlier && brand.Light.ContainsKey(Scale[i - 1]))
            {
                yield return new($"Light:{Scale[i - 1]}", $"{Scale[i - 1]} ({Number(earlier)} px) is larger than {Scale[i]} ({Number(later)} px): the type scale only grows.");
            }
        }
    }
}
