using System.Globalization;

namespace MatDesign.Core;

/// <summary>An sRGB colour (0..255 per channel). Parsed from <c>#rrggbb</c>; the maths below mirrors what the browser does.</summary>
public readonly record struct Rgb(double R, double G, double B)
{
    public static Rgb White { get; } = new(255, 255, 255);

    public static Rgb Black { get; } = new(0, 0, 0);

    public static Rgb Parse(string hex)
    {
        var h = hex.Trim().TrimStart('#');
        if (h.Length == 3)
        {
            h = string.Concat(h.Select(c => $"{c}{c}"));
        }

        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            throw new FormatException($"'{hex}' is not a colour like #aabbcc.");
        }

        return new Rgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
    }

    public string ToHex() => $"#{Clamp(R):x2}{Clamp(G):x2}{Clamp(B):x2}";

    private static int Clamp(double v) => (int)Math.Round(Math.Min(255, Math.Max(0, v)));
}

/// <summary>A colour in OKLab, the space <c>color-mix(in oklab, ...)</c> mixes in.</summary>
public readonly record struct Oklab(double L, double A, double B);

/// <summary>Colour maths: OKLab mixing like CSS <c>color-mix</c>, WCAG contrast and a perceptual distance.</summary>
public static class Colors
{
    private static double ToLinear(double c)
    {
        c /= 255;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double FromLinear(double c)
    {
        var v = c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return Math.Clamp(v, 0, 1) * 255;
    }

    public static Oklab ToOklab(Rgb c)
    {
        var r = ToLinear(c.R);
        var g = ToLinear(c.G);
        var b = ToLinear(c.B);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return new Oklab(
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public static Rgb FromOklab(Oklab c)
    {
        var l = Math.Pow(c.L + 0.3963377774 * c.A + 0.2158037573 * c.B, 3);
        var m = Math.Pow(c.L - 0.1055613458 * c.A - 0.0638541728 * c.B, 3);
        var s = Math.Pow(c.L - 0.0894841775 * c.A - 1.2914855480 * c.B, 3);
        return new Rgb(
            FromLinear(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
            FromLinear(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
            FromLinear(-0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
    }

    /// <summary><c>color-mix(in oklab, a share%, b)</c>: <paramref name="share"/> (0..1) is the part of <paramref name="a"/>.</summary>
    public static Rgb Mix(Rgb a, Rgb b, double share)
    {
        var x = ToOklab(a);
        var y = ToOklab(b);
        return FromOklab(new Oklab(
            x.L * share + y.L * (1 - share),
            x.A * share + y.A * (1 - share),
            x.B * share + y.B * (1 - share)));
    }

    /// <summary>Relative luminance (WCAG 2.x).</summary>
    public static double Luminance(Rgb c) => 0.2126 * ToLinear(c.R) + 0.7152 * ToLinear(c.G) + 0.0722 * ToLinear(c.B);

    /// <summary>WCAG contrast ratio, 1..21.</summary>
    public static double Contrast(Rgb a, Rgb b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>Distance of two colours in OKLab, scaled so that about 1.0 is "clearly different at a glance" and below 0.1 "easily confused".</summary>
    public static double Distance(Rgb a, Rgb b)
    {
        var x = ToOklab(a);
        var y = ToOklab(b);
        return Math.Sqrt(Math.Pow(x.L - y.L, 2) + Math.Pow(x.A - y.A, 2) + Math.Pow(x.B - y.B, 2)) * 10;
    }

    /// <summary>A colour from OKLCH (lightness 0..1, chroma, hue in degrees), clipped to sRGB.</summary>
    public static Rgb FromOklch(double l, double c, double hueDegrees)
    {
        var h = hueDegrees * Math.PI / 180;
        return FromOklab(new Oklab(l, c * Math.Cos(h), c * Math.Sin(h)));
    }
}
