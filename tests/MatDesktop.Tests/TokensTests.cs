using System.Globalization;
using MatDesign.Core;

namespace MatDesktop.Tests;

/// <summary>
/// The generated tokens (tools/desktop.js) against what MatDesign.Core reads from the same CSS, and the palette of MatDesktop against Core's colour maths: two readers and two
/// implementations of color-mix that have to agree.
/// </summary>
public sealed class TokensTests
{
    private static readonly string Css = File.ReadAllText(Paths.Tokens);
    private static readonly IReadOnlyList<DesignToken> Tokens = CssTokens.Tokens(Css);

    /// <summary>What the compact density sets with a mouse: the block without a media query and its <c>(pointer: fine)</c> block.</summary>
    private static readonly Dictionary<string, string> Compact = CssTokens.ParseBlocks(Css)
        .Where(b => b.Selector == ":root[data-density=\"compact\"]" && (b.Media is null || b.Media == "(pointer: fine)"))
        .SelectMany(b => b.Declarations)
        .GroupBy(d => d.Name)
        .ToDictionary(g => g.Key, g => g.Last().Value);

    private static DesignToken Token(string name) => Tokens.Single(t => t.Name == name);

    private static void Same(Rgba expected, MatRgba actual, string what)
    {
        Assert.True(
            Math.Abs(expected.Color.R - actual.R) < 0.01 && Math.Abs(expected.Color.G - actual.G) < 0.01 && Math.Abs(expected.Color.B - actual.B) < 0.01 && Math.Abs(expected.Alpha - actual.A) < 0.0001,
            $"{what}: the CSS says {expected}, MatTokens {actual}");
    }

    private static void Close(Rgb expected, MatRgba actual, string what)
    {
        Assert.True(actual.A >= 0.9999, $"{what} is not opaque");
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 1 && Math.Abs(expected.G - actual.G) <= 1 && Math.Abs(expected.B - actual.B) <= 1,
            $"{what}: Core computes {expected.ToHex()}, MatPalette {actual.Hex}");
    }

    private static MatRgba Opaque(MatRgba c, bool dark) => c.Over(dark ? MatRgba.Black : MatRgba.White);

    [Fact]
    public void Every_colour_is_the_value_of_the_css_in_light_and_dark()
    {
        Assert.NotEmpty(MatTokens.Colors);
        foreach (var colour in MatTokens.Colors)
        {
            var token = Token(colour.Token);
            Same(Rgba.Parse(token.Light), colour.Light, colour.Token + " (light)");
            Same(Rgba.Parse(token.Dark ?? token.Light), colour.Dark, colour.Token + " (dark)");
        }
    }

    [Fact]
    public void No_colour_of_the_surfaces_lines_text_and_states_is_left_out()
    {
        var written = MatTokens.Colors.Select(c => c.Token).ToHashSet(StringComparer.Ordinal);
        var colours = Tokens.Where(t => t.Group != "Accent" && t.Group != "Colour picker" && (t.Light.StartsWith('#') || t.Light.StartsWith("rgb", StringComparison.Ordinal)));
        Assert.All(colours, t => Assert.True(written.Contains(t.Name), $"{t.Name} is a colour of the CSS that MatTokens.g.cs does not have"));
        Assert.Contains("--mat-surface", written);
        Assert.Contains("--mat-scroll-thumb", written);
    }

    [Fact]
    public void Every_size_is_the_value_of_the_compact_density_with_a_mouse_in_px()
    {
        var known = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var size in MatTokens.Pixels)
        {
            var value = Compact.TryGetValue(size.Token, out var compact) ? compact : Token(size.Token).Light;
            double px;
            if (value.EndsWith("rem", StringComparison.Ordinal))
            {
                px = double.Parse(value[..^3], CultureInfo.InvariantCulture) * 16;
            }
            else if (value.EndsWith("px", StringComparison.Ordinal))
            {
                px = double.Parse(value[..^2], CultureInfo.InvariantCulture);
            }
            else
            {
                px = known[value["var(".Length..^1]];
            }

            known[size.Token] = px;
            Assert.True(Math.Abs(px - size.Pixels) < 0.001, $"{size.Token}: the CSS gives {px} px, MatTokens {size.Pixels}");
        }

        // the compact density with a mouse: controls of 34 px, body type of 14 px (docs/DESKTOP.md)
        Assert.Equal(34, MatTokens.Size.ControlH);
        Assert.Equal(14, MatTokens.Type.FsMd);
        Assert.Equal(32, MatTokens.Size.ControlHSm);
    }

    [Fact]
    public void The_type_and_the_motion_are_the_values_of_the_css()
    {
        Assert.Equal(MatTokens.Type.Font, Token("--mat-font").Light);
        Assert.Equal(MatTokens.Type.Mono, Token("--mat-mono").Light);
        Assert.Equal(MatTokens.Type.Lh, double.Parse(Token("--mat-lh").Light, CultureInfo.InvariantCulture));
        Assert.Equal(Token("--mat-dur").Light, $"{MatTokens.Motion.Dur.ToString(CultureInfo.InvariantCulture)}ms");
        Assert.Equal(MatTokens.Motion.Ease, Token("--mat-ease").Light);
        Assert.Equal(4, MatTokens.Motion.EasePoints.Length);
    }

    [Fact]
    public void There_is_a_rule_for_every_accent_token_of_the_css_in_both_modes()
    {
        var accent = Tokens.Where(t => t.Group == "Accent").Select(t => t.Name).ToList();
        Assert.Equal(accent, MatTokens.AccentLight.Select(r => r.Token));
        Assert.Equal(accent, MatTokens.AccentDark.Select(r => r.Token));
        Assert.Contains("#5b5fe6", Token("--mat-accent").Light, StringComparison.Ordinal);
        Assert.Equal("#5b5fe6", MatTokens.DefaultAccent);
    }

    /// <summary>Every app of the registry with its own colours, and the default accent with none.</summary>
    public static TheoryData<string> Accents()
    {
        var data = new TheoryData<string>();
        foreach (var app in AppRegistry.All)
        {
            data.Add(app.Id);
        }

        data.Add("default");
        return data;
    }

    private static MatApp App(string id) => id == "default"
        ? AppRegistry.Get("matdesign")! with { Accent = MatTokens.DefaultAccent, OnAccent = null, AccentText = null }
        : AppRegistry.Get(id)!;

    [Theory]
    [MemberData(nameof(Accents))]
    public void The_palette_of_an_accent_is_what_core_computes_within_one_per_channel(string id)
    {
        var app = App(id);
        var report = AccentReport.For(app);
        var light = MatPalette.For(app.Accent, dark: false, app.OnAccent, app.AccentText);
        var dark = MatPalette.For(app.Accent, dark: true, app.OnAccent, app.AccentText);

        Close(report.Accent, light.Accent, $"{id} accent (light)");
        Close(report.Hover, light["--mat-accent-hover"], $"{id} hover (light)");
        Close(report.OnAccent, light.OnAccent, $"{id} text on the accent (light)");
        Close(report.LinkLight, light.AccentText, $"{id} accent text (light)");
        Close(report.DarkAccent, dark.Accent, $"{id} accent (dark)");
        Close(report.DarkOnAccent, dark.OnAccent, $"{id} text on the accent (dark)");
        Close(report.LinkDark, dark.AccentText, $"{id} accent text (dark)");

        // the tint of the current menu item, as Core paints it over the menu (AccentReport.Checks)
        foreach (var (palette, isDark, share) in new[] { (light, false, 0.19), (dark, true, 0.26) })
        {
            var surfaces = SchemeSurfaces.Read(Css, null, isDark);
            var core = new Rgba(isDark ? report.DarkAccent : report.Accent, share).Over(surfaces.Nav);
            var mine = palette["--mat-accent-soft-strong"].Over(Opaque(palette["--mat-nav-bg"], isDark));
            Close(core, mine, $"{id} current menu item ({(isDark ? "dark" : "light")})");

            var contrast = MatColor.Contrast(palette.OnAccent, palette.Accent);
            var expected = Colors.Contrast(isDark ? report.DarkOnAccent : report.OnAccent, isDark ? report.DarkAccent : report.Accent);
            Assert.True(Math.Abs(contrast - expected) < 0.05, $"{id}: contrast of the text on the accent {contrast:0.00} against {expected:0.00}");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_surfaces_and_texts_of_a_palette_are_the_surfaces_core_reads(bool dark)
    {
        var surfaces = SchemeSurfaces.Read(Css, null, dark);
        var palette = MatPalette.For(null, dark);
        Close(surfaces.Bg, Opaque(palette.Bg, dark), "bg");
        Close(surfaces.Surface, Opaque(palette.Surface, dark), "surface");
        Close(surfaces.Surface3, Opaque(palette["--mat-surface-3"], dark), "surface 3");
        Close(surfaces.Nav, Opaque(palette["--mat-nav-bg"], dark), "menu");
        Close(surfaces.Text, palette.Text.Over(Opaque(palette.Surface, dark)), "text");
        Close(surfaces.Paint("--mat-text-muted", surfaces.Surface), palette.TextMuted.Over(Opaque(palette.Surface, dark)), "muted text");
        Close(surfaces.Paint("--mat-border", surfaces.Surface), palette.Border.Over(Opaque(palette.Surface, dark)), "border on a surface");
    }

    [Fact]
    public void The_colour_maths_is_cores()
    {
        var a = Rgb.Parse("#295cff");
        foreach (var share in new[] { 0.0, 0.11, 0.5, 0.74, 0.86, 1.0 })
        {
            Close(Colors.Mix(a, Rgb.Black, share), MatColor.Mix(MatRgba.Parse("#295cff"), MatRgba.Black, share), $"mix {share}");
            Close(Colors.Mix(a, Rgb.White, share), MatColor.Mix(MatRgba.Parse("#295cff"), MatRgba.White, share), $"mix {share} white");
        }

        Assert.Equal(Colors.Contrast(Rgb.White, a), MatColor.Contrast(MatRgba.White, MatRgba.Parse("#295cff")), 6);
        Assert.Equal(new MatRgba(31, 30, 27, 0.13), MatRgba.Parse("rgba(31, 30, 27, .13)"));
        Assert.Equal("#5b5fe6", MatRgba.Parse("#5B5FE6").Hex);
        Assert.Equal("#215B5FE6", MatRgba.Parse("#5b5fe6").WithAlpha(0.13).ArgbHex);
        Assert.Throws<FormatException>(() => MatRgba.Parse("blue"));
    }

    [Fact]
    public void An_app_that_sets_its_own_text_colours_keeps_them_in_light_and_the_css_ones_in_dark()
    {
        var palette = MatPalette.For("#e8a200", dark: false, "#0b1020", "#8a6100");
        Assert.Equal("#0b1020", palette.OnAccent.Hex);
        Assert.Equal("#8a6100", palette.AccentText.Hex);
        var dark = MatPalette.For("#e8a200", dark: true, "#ffffff", "#8a6100");
        Assert.Equal("#0b1020", dark.OnAccent.Hex);
        Assert.NotEqual("#8a6100", dark.AccentText.Hex);
        Assert.Equal(0.19, palette["--mat-accent-soft-strong"].A, 6);
    }
}
