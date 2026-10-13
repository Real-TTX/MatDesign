using MatDesign.Core;

namespace MatDesign.Tests;

/// <summary>COLOR-09: the colour schemes. What the CSS says, that the dark values are written twice, that a scheme changes surfaces only, and that every combination reaches AA.</summary>
public class SchemeTests
{
    private static readonly string Tokens = TestPaths.ReadToolkit("css/matui.tokens.css");

    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var scheme in ColorSchemes.All)
        {
            data.Add(scheme.Id);
        }

        return data;
    }

    private static SchemeBlocks Block(string id) => CssTokens.Schemes(Tokens).Single(s => s.Id == id);

    [Fact]
    public void The_css_has_exactly_the_schemes_of_the_registry_and_the_dark_only_one_has_no_light_values()
    {
        var blocks = CssTokens.Schemes(Tokens);
        Assert.Equal(ColorSchemes.All.Select(s => s.Id), blocks.Select(b => b.Id));
        foreach (var scheme in ColorSchemes.All)
        {
            var block = blocks.Single(b => b.Id == scheme.Id);
            Assert.Equal(scheme.DarkOnly, block.Light is null);
            Assert.NotNull(block.Dark);
            Assert.NotNull(block.SystemDark);
        }

        Assert.Equal(["oled"], ColorSchemes.All.Where(s => s.DarkOnly).Select(s => s.Id));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void The_dark_values_of_a_scheme_are_written_twice_and_are_identical(string id)
    {
        // COLOR-02 for the schemes: data-theme="dark" and "the system is dark" must give the same colours.
        var block = Block(id);
        Assert.NotEmpty(block.Dark!);
        Assert.Equal(block.Dark!.OrderBy(d => d.Key), block.SystemDark!.OrderBy(d => d.Key));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void A_scheme_sets_in_dark_every_token_that_it_sets_in_light_so_that_its_light_values_never_show_through(string id)
    {
        // The light block of a scheme is as strong as the dark set of the base tokens and comes later, so a token that it sets and its dark block does not would stay light in dark.
        var block = Block(id);
        if (block.Light is null)
        {
            return;
        }

        Assert.Empty(block.Light.Keys.Except(block.Dark!.Keys));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void A_scheme_changes_surfaces_only_and_never_an_accent_token(string id)
    {
        // COLOR-09: backgrounds, lines, text, the status texts, shadows, the dim layer and the scrollbar. Not the accent, so that every combination of scheme, mode, accent and app works.
        var tokens = CssTokens.Tokens(Tokens).ToDictionary(t => t.Name);
        var block = Block(id);
        foreach (var name in (block.Light?.Keys ?? []).Concat(block.Dark!.Keys).Distinct())
        {
            Assert.True(tokens.ContainsKey(name), $"{id} sets {name}, which is not a token");
            var token = tokens[name];
            var allowed = token.Group is "Surfaces" or "Lines" or "Text" or "Elevation" || (token.Group == "Status" && name.EndsWith("-text", StringComparison.Ordinal));
            Assert.True(allowed, $"{id} sets {name} ({token.Group}): a scheme changes surfaces, lines, text and shadows only");
            Assert.NotEqual("Accent", token.Group);
        }
    }

    [Fact]
    public void A_scheme_is_not_mistaken_for_a_base_token_or_for_the_dark_set()
    {
        var tokens = CssTokens.Tokens(Tokens);
        Assert.Equal(tokens.Count, tokens.Select(t => t.Name).Distinct().Count());
        Assert.DoesNotContain(tokens, t => t.Group.Contains("cheme", StringComparison.Ordinal));
        // the dark value of a base token is the one of data-theme="dark", whatever a scheme says
        Assert.Equal("#151514", tokens.Single(t => t.Name == "--mat-bg").Dark);
        Assert.Equal("#f5f4f1", tokens.Single(t => t.Name == "--mat-bg").Light);
        Assert.Equal("100%", tokens.Single(t => t.Name == "--mat-accent-text-keep").Light);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_scheme_reaches_AA_for_every_app_and_every_preset_in_every_mode_it_has(string id)
    {
        // COLOR-05 for the schemes: text, muted text, links, the text on the accent, the status texts on a surface and on their tint, the current menu item, the border of a control
        // (3 to 1 in the contrast scheme). The accents are the apps of the registry and the presets of a user; the standard look is not required to reach what it never had to.
        var problems = new List<string>();
        foreach (var surfaces in SchemeSurfaces.Modes(Tokens, id))
        {
            problems.AddRange(surfaces.Checks().Where(c => !c.Advisory && !c.Passes).Select(c => $"{id}: {c.Name} {c.Ratio:0.00} < {c.Required}"));
            foreach (var (accent, name, report) in SchemeContrast.Accents(AppRegistry.All))
            {
                problems.AddRange(report.Checks(surfaces).Where(c => !c.Advisory && !c.Passes).Select(c => $"{id} {name} ({accent}): {c.Name} {c.Ratio:0.00} < {c.Required}"));
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void The_standard_look_reaches_what_it_has_always_had_to_and_the_pairings_that_passed_stay_required()
    {
        // Nothing about the standard look changes: the surfaces text sits on pass, and so do the pairings that were checked before the schemes existed.
        foreach (var dark in new[] { false, true })
        {
            var surfaces = SchemeSurfaces.Read(Tokens, null, dark);
            Assert.True(surfaces.IsStandardLook);
            Assert.DoesNotContain(surfaces.Checks(), c => !c.Advisory && !c.Passes);
            foreach (var (_, name, report) in SchemeContrast.Accents(AppRegistry.All))
            {
                Assert.True(report.Checks(surfaces).Where(c => !c.Advisory).All(c => c.Passes), $"{name} in the standard look ({surfaces.Mode})");
            }
        }

        Assert.Equal(AccentReport.LightSurface.ToHex(), SchemeSurfaces.Read(Tokens, null, false).Surface.ToHex());
        Assert.Equal(AccentReport.DarkSurface.ToHex(), SchemeSurfaces.Read(Tokens, null, true).Surface.ToHex());
    }

    [Fact]
    public void The_border_of_a_control_reaches_3_to_1_in_the_contrast_scheme_in_both_modes()
    {
        foreach (var surfaces in SchemeSurfaces.Modes(Tokens, "contrast"))
        {
            var border = Assert.Single(surfaces.Checks(), c => c.Name.StartsWith("Border of a control", StringComparison.Ordinal));
            Assert.False(border.Advisory, "in the contrast scheme the border of a control is required, not advice");
            Assert.True(border.Passes, $"{border.Name}: {border.Ratio:0.00}");
        }
    }

    [Fact]
    public void A_scheme_only_darkens_the_links_by_pulling_them_toward_its_text_and_the_standard_look_keeps_its_own()
    {
        var app = AppRegistry.Get("matmail")!;
        var report = AccentReport.For(app);
        var standard = SchemeSurfaces.Read(Tokens, null, false);
        Assert.Equal(1.0, standard.AccentKeep);
        Assert.Equal(report.LinkLight.ToHex(), report.LinkOn(standard).ToHex());

        var paper = SchemeSurfaces.Read(Tokens, "paper", false);
        Assert.True(paper.AccentKeep < 1);
        Assert.True(Colors.Luminance(report.LinkOn(paper)) < Colors.Luminance(report.LinkLight), "on paper the link is darker than in the standard look");
        var dark = SchemeSurfaces.Read(Tokens, "paper", true);
        Assert.True(Colors.Luminance(report.LinkOn(dark)) > Colors.Luminance(report.LinkDark), "in dark the pull is toward the light text, so this link gets lighter");
    }

    [Fact]
    public void A_dark_only_scheme_has_one_mode_and_is_the_standard_look_in_the_other()
    {
        var oled = SchemeSurfaces.Modes(Tokens, "oled");
        Assert.True(oled.Single().Dark);
        Assert.Equal(2, SchemeSurfaces.Modes(Tokens, "paper").Count);

        var light = SchemeSurfaces.Read(Tokens, "oled", false);
        Assert.True(light.IsStandardLook);
        Assert.Empty(light.Set);
        Assert.Equal(SchemeSurfaces.Read(Tokens, null, false).Bg.ToHex(), light.Bg.ToHex());
        Assert.Equal("#000000", SchemeSurfaces.Read(Tokens, "oled", true).Bg.ToHex());
        Assert.Equal("#000000", SchemeSurfaces.Read(Tokens, "oled", true).Nav.ToHex());
    }

    [Fact]
    public void The_contrast_scheme_has_white_and_black_surfaces_and_lines_instead_of_shadows()
    {
        var light = SchemeSurfaces.Read(Tokens, "contrast", false);
        var dark = SchemeSurfaces.Read(Tokens, "contrast", true);
        Assert.Equal("#ffffff", light.Surface.ToHex());
        Assert.Equal("#ffffff", light.Bg.ToHex());
        Assert.Equal("#000000", dark.Surface.ToHex());
        Assert.Equal("#000000", dark.Bg.ToHex());
        foreach (var surfaces in new[] { light, dark })
        {
            Assert.Equal("none", surfaces.Value("--mat-shadow-1"));
            Assert.StartsWith("0 0 0 1px ", surfaces.Value("--mat-shadow-2"), StringComparison.Ordinal);
            Assert.StartsWith("0 0 0 1px ", surfaces.Value("--mat-shadow-3"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Only_known_ids_are_schemes_and_anything_else_is_the_standard_look()
    {
        Assert.Equal("paper", ColorSchemes.Normalize("paper"));
        Assert.Null(ColorSchemes.Normalize("standard"));
        Assert.Null(ColorSchemes.Normalize("PAPER"));
        Assert.Null(ColorSchemes.Normalize("<script>"));
        Assert.Null(ColorSchemes.Normalize(null));
        Assert.True(ColorSchemes.IsKnown("oled"));
        Assert.False(ColorSchemes.IsKnown("dark"));
    }

    [Fact]
    public void The_weakest_accent_of_every_pairing_is_named_and_none_is_below_what_a_scheme_needs()
    {
        foreach (var id in new[] { "paper", "contrast", "oled" })
        {
            foreach (var surfaces in SchemeSurfaces.Modes(Tokens, id))
            {
                var weakest = SchemeContrast.Weakest(surfaces, AppRegistry.All);
                Assert.True(weakest.Count >= 8);
                Assert.All(weakest, w => Assert.False(string.IsNullOrEmpty(w.Accent)));
                Assert.All(weakest.Where(w => !w.Advisory), w => Assert.True(w.Ratio >= w.Required, $"{id} {surfaces.Mode}: {w.Check} {w.Ratio:0.00} ({w.Accent})"));
            }
        }
    }
}
