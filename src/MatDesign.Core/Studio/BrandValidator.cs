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
            && SafeMap(brand.Light) && SafeMap(brand.Dark) && SafeMap(brand.Components)
            && (brand.Schemes is null || brand.Schemes.All(p => ColorSchemes.IsKnown(p.Key) && p.Value is not null && SafeMap(p.Value.Light) && SafeMap(p.Value.Dark)));

    /// <summary>True when every name of the map looks like a token and every value is safe to write into a stylesheet (<see cref="IsSafeValue"/>).</summary>
    public static bool SafeMap(IReadOnlyDictionary<string, string>? map)
        => map is null || map.All(p => NamePattern().IsMatch(p.Key) && IsSafeValue(p.Value));

    // ------------------------------------------------------------------------------------------------ normalising

    /// <summary>
    /// The brand as it is stored: texts trimmed, every value in the written form of its kind (<c>#fff</c> is <c>#ffffff</c>, <c>0.50rem</c> is <c>0.5rem</c>), an empty value or a value that is the toolkit's own
    /// left out (a brand that sets nothing writes nothing), the tokens in the order of the CSS. Names that are no token of a brand stay, so that <see cref="Validate"/> can say so. The component properties
    /// (<paramref name="properties"/>) and what is set inside a colour scheme (<paramref name="schemes"/>) are put in order the same way.
    /// </summary>
    public static Brand Normalize(Brand brand, IReadOnlyList<BrandToken> tokens, IReadOnlyList<PropertyToken>? properties = null, IReadOnlyList<SchemeBlocks>? schemes = null)
        => brand with
        {
            Company = (brand.Company ?? string.Empty).Trim(),
            ShortName = (brand.ShortName ?? string.Empty).Trim(),
            Tagline = (brand.Tagline ?? string.Empty).Trim(),
            Light = NormalizeMap(brand.Light, tokens, dark: false),
            Dark = NormalizeMap(brand.Dark, tokens, dark: true),
            Components = NormalizeMap(brand.Components, (properties ?? []).Select(p => p.Token).ToList(), dark: false),
            Schemes = NormalizeSchemes(brand.Schemes, tokens, schemes ?? []),
        };

    /// <summary>A map of values put in the written form of the tokens it is for, without what is empty or the toolkit's own, in the order of <paramref name="tokens"/> (<see cref="Normalize"/> does it for a brand).</summary>
    public static Dictionary<string, string> NormalizeValues(IReadOnlyDictionary<string, string>? map, IReadOnlyList<BrandToken> tokens, bool dark) => NormalizeMap(map, tokens, dark);

    private static Dictionary<string, SchemeValues> NormalizeSchemes(IReadOnlyDictionary<string, SchemeValues>? map, IReadOnlyList<BrandToken> tokens, IReadOnlyList<SchemeBlocks> schemes)
    {
        var cleaned = new Dictionary<string, SchemeValues>();
        var order = ColorSchemes.All.Select(s => s.Id).ToList();
        foreach (var (rawId, values) in (map ?? new Dictionary<string, SchemeValues>()).OrderBy(p => order.IndexOf(p.Key) is var i and >= 0 ? i : int.MaxValue).ThenBy(p => p.Key, StringComparer.Ordinal))
        {
            var id = (rawId ?? string.Empty).Trim().ToLowerInvariant();
            var own = schemes.FirstOrDefault(s => s.Id == id);
            var light = NormalizeMap(values?.Light, tokens, dark: false, own?.Light);
            var dark = NormalizeMap(values?.Dark, tokens, dark: true, own?.Dark);
            if (id.Length > 0 && (light.Count > 0 || dark.Count > 0))
            {
                cleaned[id] = new SchemeValues(light, dark);
            }
        }

        return cleaned;
    }

    private static Dictionary<string, string> NormalizeMap(IReadOnlyDictionary<string, string>? map, IReadOnlyList<BrandToken> tokens, bool dark, IReadOnlyDictionary<string, string>? ownValues = null)
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
                var own = ownValues is not null ? ownValues.GetValueOrDefault(name) : dark ? token.Dark : token.Light;
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
    /// <paramref name="properties"/> are the component properties the brand may set (<see cref="ComponentTokens.Editable"/>): each value must be a value of its type within its limits, and the knob of a
    /// switch has to fit in its track. What the brand sets inside a colour scheme must be a token that the scheme itself sets in that mode.
    /// </summary>
    public static IReadOnlyList<Problem> Validate(Brand brand, IReadOnlyList<DesignToken> designTokens, IReadOnlyList<SchemeBlocks> schemes, IEnumerable<MatApp> apps, IReadOnlyList<PropertyToken>? properties = null)
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
        valuesOk &= CheckComponents(brand.ComponentValues, "Component", properties ?? [], problems);
        valuesOk &= CheckSchemes(brand, tokens, schemes, problems);
        if (!valuesOk)
        {
            return problems;
        }

        problems.AddRange(CheckScale(brand.Light, tokens, "Light"));
        problems.AddRange(BrandContrast.Problems(designTokens, schemes, brand, apps));
        return problems;
    }

    /// <summary>
    /// The values of component properties against their type and limits, and the parts that have to fit each other (a knob inside its track). <paramref name="values"/> is the map to check,
    /// <paramref name="field"/> the start of the field of each problem (<c>Component</c>, <c>Property</c>).
    /// </summary>
    public static bool CheckComponents(IReadOnlyDictionary<string, string> values, string field, IReadOnlyList<PropertyToken> properties, List<Problem> problems, IReadOnlyDictionary<string, string>? under = null)
    {
        var ok = true;
        var byName = properties.ToDictionary(p => p.Name);
        foreach (var (name, value) in values)
        {
            if (!byName.TryGetValue(name, out var property))
            {
                problems.Add(new($"{field}:{name}", $"{name} is not a documented component property."));
                ok = false;
            }
            else if (CheckValue(property.Token, value) is { } message)
            {
                problems.Add(new($"{field}:{name}", message));
                ok = false;
            }
        }

        if (!ok)
        {
            return false;
        }

        // the knob of a switch has to fit in its track: it is smaller than the height, and the track is longer than it is high
        double? Size(string name) => values.TryGetValue(name, out var v) || (under?.TryGetValue(name, out v) ?? false) ? BrandTokens.Pixels(v!) : byName.TryGetValue(name, out var p) ? BrandTokens.Pixels(p.Property.Fallback) : null;
        if (values.ContainsKey("--mat-switch-thumb") || values.ContainsKey("--mat-switch-height"))
        {
            if (Size("--mat-switch-thumb") is { } thumb && Size("--mat-switch-height") is { } height && thumb >= height)
            {
                problems.Add(new($"{field}:{(values.ContainsKey("--mat-switch-thumb") ? "--mat-switch-thumb" : "--mat-switch-height")}", $"The knob of a switch ({Number(thumb)} px) has to be smaller than its track is high ({Number(height)} px)."));
                ok = false;
            }
        }

        if (values.ContainsKey("--mat-switch-width") || values.ContainsKey("--mat-switch-height"))
        {
            if (Size("--mat-switch-width") is { } width && Size("--mat-switch-height") is { } height && width <= height)
            {
                problems.Add(new($"{field}:{(values.ContainsKey("--mat-switch-width") ? "--mat-switch-width" : "--mat-switch-height")}", $"The track of a switch ({Number(width)} px) has to be longer than it is high ({Number(height)} px)."));
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>What a brand sets inside a colour scheme: a known scheme, and only tokens that the scheme sets in that mode, with a value of the kind and within the limits of the token.</summary>
    private static bool CheckSchemes(Brand brand, IReadOnlyList<BrandToken> tokens, IReadOnlyList<SchemeBlocks> schemes, List<Problem> problems)
    {
        var ok = true;
        var byName = tokens.ToDictionary(t => t.Name);
        foreach (var (id, values) in brand.Schemes ?? new Dictionary<string, SchemeValues>())
        {
            if (!ColorSchemes.IsKnown(id))
            {
                problems.Add(new($"Scheme:{id}", $"'{id}' is not a colour scheme of the toolkit ({string.Join(", ", ColorSchemes.All.Select(s => s.Id))})."));
                ok = false;
                continue;
            }

            var blocks = schemes.FirstOrDefault(s => s.Id == id);
            foreach (var (mode, map, own) in new[] { ("Light", values?.Light, blocks?.Light), ("Dark", values?.Dark, blocks?.Dark) })
            {
                foreach (var (name, value) in map ?? new Dictionary<string, string>())
                {
                    var field = $"Scheme:{id}:{mode}:{name}";
                    if (!byName.TryGetValue(name, out var token))
                    {
                        problems.Add(new(field, $"{name} is not a token a brand can set."));
                        ok = false;
                    }
                    else if (own is null || !own.ContainsKey(name))
                    {
                        problems.Add(new(field, own is null
                            ? $"The {id} scheme has no {mode.ToLowerInvariant()} values: {(mode == "Light" ? "in light mode it is the standard look" : "it sets nothing in dark mode")}."
                            : $"{name} is not one of the tokens the {id} scheme sets in {mode.ToLowerInvariant()} mode: change it in the standard look instead."));
                        ok = false;
                    }
                    else if (CheckValue(token, value) is { } message)
                    {
                        problems.Add(new(field, message));
                        ok = false;
                    }
                }
            }
        }

        return ok;
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

    /// <summary>
    /// The type scale stays a scale: each size is at least the one before it. <paramref name="light"/> are the values that are set (the brand's, or the brand's with an app's over them), the
    /// toolkit's own are under them; only a size that is set is blamed. The field of a problem starts with <paramref name="field"/>.
    /// </summary>
    public static IEnumerable<Problem> CheckScale(IReadOnlyDictionary<string, string> light, IReadOnlyList<BrandToken> tokens, string field)
    {
        var byName = tokens.ToDictionary(t => t.Name);
        double? Size(string name) => light.TryGetValue(name, out var v) ? Pixels(v) : byName.TryGetValue(name, out var t) ? Pixels(t.Light) : null;
        for (var i = 1; i < Scale.Length; i++)
        {
            if (Size(Scale[i]) is { } size && Size(Scale[i - 1]) is { } before && size < before && light.ContainsKey(Scale[i]))
            {
                yield return new($"{field}:{Scale[i]}", $"{Scale[i]} ({Number(size)} px) is smaller than {Scale[i - 1]} ({Number(before)} px): the type scale only grows.");
            }
            else if (Size(Scale[i]) is { } later && Size(Scale[i - 1]) is { } earlier && later < earlier && light.ContainsKey(Scale[i - 1]))
            {
                yield return new($"{field}:{Scale[i - 1]}", $"{Scale[i - 1]} ({Number(earlier)} px) is larger than {Scale[i]} ({Number(later)} px): the type scale only grows.");
            }
        }
    }
}
