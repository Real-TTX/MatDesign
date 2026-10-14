using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>COLOR-11: what a brand may set (the tokens a brand can set, the type of each value, the limits that protect usability) and how its colours are held to AA.</summary>
public class BrandValidatorTests
{
    private static readonly string TokensCss = TestPaths.ReadToolkit("css/matui.tokens.css");
    private static readonly IReadOnlyList<DesignToken> Tokens = CssTokens.Tokens(TokensCss);
    private static readonly IReadOnlyList<SchemeBlocks> Schemes = CssTokens.Schemes(TokensCss);
    private static readonly IReadOnlyList<BrandToken> Editable = BrandTokens.Editable(Tokens);

    private static BrandToken Token(string name) => Editable.Single(t => t.Name == name);

    private static Brand Make(Dictionary<string, string>? light = null, Dictionary<string, string>? dark = null)
        => new("Acme", "Acme", "Anvils for everyone", light ?? [], dark ?? []);

    private static IReadOnlyList<Problem> Check(Brand brand)
        => BrandValidator.Validate(BrandValidator.Normalize(brand, Editable), Tokens, Schemes, AppRegistry.All);

    private static string? Value(string token, string value) => BrandValidator.CheckValue(Token(token), BrandValidator.NormalizeValue(Token(token), value));

    // ---- which tokens

    [Fact]
    public void The_editable_tokens_are_look_not_structure_and_every_kind_is_there()
    {
        var names = Editable.Select(t => t.Name).ToHashSet();
        foreach (var name in new[] { "--mat-bg", "--mat-text", "--mat-radius", "--mat-space-4", "--mat-control-h", "--mat-fs-md", "--mat-lh", "--mat-font", "--mat-mono", "--mat-dur", "--mat-ease", "--mat-nav-w" })
        {
            Assert.Contains(name, names);
        }

        foreach (var name in new[] { "--mat-accent", "--mat-accent-text", "--mat-z-dialog", "--mat-safe-top", "--mat-picker-hues", "--mat-shadow-2", "--mat-radius-pill", "--mat-gap", "--mat-content-pad", "--mat-accent-text-keep" })
        {
            Assert.DoesNotContain(name, names);
        }

        Assert.Equal(Enum.GetValues<BrandTokenKind>().Order(), Editable.Select(t => t.Kind).Distinct().Order());
        Assert.All(Editable, t => Assert.False(string.IsNullOrWhiteSpace(t.Hint)));
        Assert.Equal(Tokens.Count, Editable.Count + BrandTokens.Fixed(Tokens).Count);
        Assert.All(BrandTokens.Fixed(Tokens), f => Assert.False(string.IsNullOrWhiteSpace(f.Reason)));
    }

    [Fact]
    public void Only_a_token_with_a_dark_value_in_the_css_can_have_one_in_a_brand()
    {
        Assert.True(Token("--mat-bg").HasDark);
        Assert.False(Token("--mat-radius").HasDark);
        var problems = Check(Make(dark: new() { ["--mat-radius"] = "8px" }));
        Assert.Contains(problems, p => p.Field == "Dark:--mat-radius" && p.Message.Contains("no dark value"));
    }

    [Fact]
    public void An_unknown_name_or_a_token_a_brand_cannot_set_is_refused()
    {
        var problems = Check(Make(new() { ["--mat-nope"] = "1px", ["--mat-accent"] = "#ff0000", ["--mat-z-dialog"] = "5", ["color"] = "red" }));
        foreach (var name in new[] { "--mat-nope", "--mat-accent", "--mat-z-dialog", "color" })
        {
            Assert.Contains(problems, p => p.Field == "Light:" + name && p.Message.Contains("not a token a brand can set"));
        }
    }

    // ---- values by their kind

    [Theory]
    [InlineData("--mat-bg", "#fff", null)]
    [InlineData("--mat-bg", "#FAF9F7", null)]
    [InlineData("--mat-border", "rgba(1, 2, 3, .5)", null)]
    [InlineData("--mat-border", "rgb(1,2,3)", null)]
    [InlineData("--mat-bg", "red", "A colour")]
    [InlineData("--mat-bg", "#12", "A colour")]
    [InlineData("--mat-bg", "rgb(300, 2, 3)", "A colour")]
    [InlineData("--mat-border", "rgba(1, 2, 3, 2)", "A colour")]
    [InlineData("--mat-bg", "#ffffff; } body { display: none", "does not belong")]
    [InlineData("--mat-radius", "8px", null)]
    [InlineData("--mat-radius", "0.5rem", null)]
    [InlineData("--mat-radius", "0", null)]
    [InlineData("--mat-radius", "25px", "above the most")]
    [InlineData("--mat-radius", "1.5rem", null)]
    [InlineData("--mat-radius", "1.6rem", "above the most")]
    [InlineData("--mat-radius", "-1px", "below the least")]
    [InlineData("--mat-radius", "50%", "A length")]
    [InlineData("--mat-control-h", "44px", null)]
    [InlineData("--mat-control-h", "43px", "Touch targets")]
    [InlineData("--mat-control-h-sm", "35px", "Touch targets")]
    [InlineData("--mat-control-h-sm", "36px", null)]
    [InlineData("--mat-fs-md", "13.9px", "Body text")]
    [InlineData("--mat-fs-md", "0.875rem", null)]
    [InlineData("--mat-fs-md", "1rem", null)]
    [InlineData("--mat-lh", "1.2", null)]
    [InlineData("--mat-lh", "2", null)]
    [InlineData("--mat-lh", "1.1", "line height")]
    [InlineData("--mat-lh", "2.1", "line height")]
    [InlineData("--mat-lh", "1.5em", "A number")]
    [InlineData("--mat-dur", "200ms", null)]
    [InlineData("--mat-dur", "0.2s", null)]
    [InlineData("--mat-dur", "1001ms", "above the most")]
    [InlineData("--mat-dur", "fast", "A time")]
    [InlineData("--mat-ease", "ease-in-out", null)]
    [InlineData("--mat-ease", "cubic-bezier(.4, 0, .2, 1)", null)]
    [InlineData("--mat-ease", "cubic-bezier(2, 0, .2, 1)", "x values")]
    [InlineData("--mat-ease", "bounce", "cubic-bezier")]
    public void A_value_is_held_to_the_type_and_the_limits_of_its_token(string token, string value, string? message)
    {
        var problem = Value(token, value);
        if (message is null)
        {
            Assert.Null(problem);
        }
        else
        {
            Assert.NotNull(problem);
            Assert.Contains(message, problem, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("system-ui, \"Segoe UI\", sans-serif", true)]
    [InlineData("Inter, 'Helvetica Neue', Arial, sans-serif", true)]
    [InlineData("Georgia, serif", true)]
    [InlineData("Georgia, Times", false)]
    [InlineData("Georgia; color: red, serif", false)]
    [InlineData("Georgia, serif } body { color: red", false)]
    [InlineData("url(https://example.com/f.woff), serif", false)]
    [InlineData("expression(alert(1)), serif", false)]
    [InlineData("@import 'x', serif", false)]
    [InlineData("\"Unclosed, serif", false)]
    [InlineData("a", false)]
    public void A_font_stack_has_safe_characters_and_ends_in_a_generic_family(string stack, bool valid)
    {
        Assert.Equal(valid, Value("--mat-font", stack) is null);
        Assert.Equal(valid, Value("--mat-mono", stack) is null);
    }

    [Fact]
    public void A_value_is_at_most_300_characters()
    {
        Assert.NotNull(Value("--mat-font", string.Concat(Enumerable.Repeat("Georgia, ", 40)) + "serif"));
        Assert.False(BrandValidator.IsSafeValue(new string('a', 301)));
    }

    [Theory]
    [InlineData("#fff")]
    [InlineData("rgba(1, 2, 3, .5)")]
    [InlineData("1.5rem")]
    [InlineData("system-ui, sans-serif")]
    public void A_safe_value_is_safe(string value) => Assert.True(BrandValidator.IsSafeValue(value));

    [Theory]
    [InlineData("")]
    [InlineData("a; b")]
    [InlineData("a } b")]
    [InlineData("a { b")]
    [InlineData("url(x)")]
    [InlineData("URL(x)")]
    [InlineData("@import x")]
    [InlineData("expression(1)")]
    [InlineData("a /* b */")]
    [InlineData("a \\ b")]
    [InlineData("a <b>")]
    [InlineData("var(--x)")]
    [InlineData("rgb(1, 2, 3")]
    [InlineData("\"open")]
    public void An_unsafe_value_is_not_written_into_a_stylesheet(string value) => Assert.False(BrandValidator.IsSafeValue(value));

    [Fact]
    public void The_type_scale_only_grows()
    {
        var problems = Check(Make(new() { ["--mat-fs-lg"] = "15px" }));
        Assert.Contains(problems, p => p.Field == "Light:--mat-fs-lg" || p.Field == "Light:--mat-fs-md");
        Assert.Empty(Check(Make(new() { ["--mat-fs-md"] = "1rem", ["--mat-fs-lg"] = "1.25rem" })));
    }

    // ---- the brand

    [Fact]
    public void Normalize_writes_values_in_the_form_of_their_kind_drops_what_is_the_toolkits_own_and_orders_by_the_css()
    {
        var brand = BrandValidator.Normalize(
            Make(new() { [" --MAT-RADIUS "] = " 8.0px ", ["--mat-bg"] = "#FFF", ["--mat-space-4"] = "16px", ["--mat-text"] = "", ["--mat-dur"] = "0.30s" }, new() { ["--mat-bg"] = "#151514", ["--mat-text"] = "#FFFFFF" }),
            Editable);
        Assert.Equal(["--mat-bg", "--mat-radius", "--mat-dur"], brand.Light.Keys);
        Assert.Equal("#ffffff", brand.Light["--mat-bg"]);
        Assert.Equal("8px", brand.Light["--mat-radius"]);
        Assert.Equal("0.3s", brand.Light["--mat-dur"]);
        Assert.False(brand.Light.ContainsKey("--mat-space-4"));
        Assert.Equal(["--mat-text"], brand.Dark.Keys);
        Assert.Equal("#ffffff", brand.Dark["--mat-text"]);
    }

    [Fact]
    public void A_brand_that_sets_nothing_has_no_problem_and_no_overrides()
    {
        var brand = BrandValidator.Normalize(Make(new() { ["--mat-bg"] = "#f5f4f1" }, new() { ["--mat-bg"] = "#151514" }), Editable);
        Assert.False(brand.HasOverrides);
        Assert.Empty(Check(Brand.BuiltIn));
        Assert.Empty(Check(Make()));
    }

    [Fact]
    public void The_names_are_checked()
    {
        var problems = Check(new Brand(" ", string.Empty, new string('x', 91), new Dictionary<string, string>(), new Dictionary<string, string>()));
        Assert.Equal(["Company", "ShortName", "Tagline"], problems.Select(p => p.Field).Order());
        Assert.Empty(Check(new Brand("Acme", "A", string.Empty, new Dictionary<string, string>(), new Dictionary<string, string>())));
    }

    [Fact]
    public void IsSafe_holds_what_is_read_from_a_disk_that_a_person_can_edit()
    {
        Assert.True(BrandValidator.IsSafe(Make(new() { ["--mat-bg"] = "#ffffff" })));
        Assert.True(BrandValidator.IsSafe(Brand.BuiltIn));
        Assert.False(BrandValidator.IsSafe(Make(new() { ["--mat-bg"] = "red; } * { display: none" })));
        Assert.False(BrandValidator.IsSafe(Make(new() { ["} * {"] = "1px" })));
        Assert.False(BrandValidator.IsSafe(Make(new() { ["--mat-bg"] = "url(x)" })));
        Assert.False(BrandValidator.IsSafe(Make() with { Company = string.Empty }));
        Assert.False(BrandValidator.IsSafe(Make() with { ShortName = new string('x', 17) }));
    }

    // ---- contrast

    [Fact]
    public void Text_that_does_not_reach_AA_on_the_surface_is_an_error_with_the_numbers()
    {
        var problems = Check(Make(new() { ["--mat-text"] = "#bbbbbb" }));
        Assert.Contains(problems, p => p.Field == "Contrast" && p.Message.Contains("Text on the page (light)") && p.Message.Contains(":1, needs 4.50:1"));
        Assert.All(problems.Where(p => p.Field == "Contrast"), p => Assert.Contains(":1", p.Message));
    }

    [Fact]
    public void The_dark_mode_is_checked_on_its_own_values()
    {
        // light text on a dark page is fine; the same page with dark text is not
        Assert.DoesNotContain(Check(Make(dark: new() { ["--mat-bg"] = "#000000", ["--mat-text"] = "#ffffff" })), p => p.Message.Contains("(dark)"));
        var problems = Check(Make(dark: new() { ["--mat-text"] = "#222222" }));
        Assert.Contains(problems, p => p.Message.Contains("Text on the page (dark)"));
        Assert.DoesNotContain(problems, p => p.Message.Contains("(light)"));
    }

    [Fact]
    public void A_surface_that_takes_the_link_of_an_app_below_AA_names_the_app()
    {
        // the accents of the family are links on white: a surface of a middling blue is under every one of them
        var problems = Check(Make(new() { ["--mat-surface"] = "#7a8fc4" }));
        Assert.Contains(problems, p => p.Field == "Contrast" && p.Message.Contains("Weakest with"));
    }

    [Fact]
    public void Advice_never_blocks()
    {
        // faint text and the border of a control are advice in the standard look
        var problems = Check(Make(new() { ["--mat-text-faint"] = "#eeeeee", ["--mat-border-strong"] = "rgba(0, 0, 0, .05)" }));
        Assert.DoesNotContain(problems, p => p.Message.Contains("Faint text"));
        Assert.DoesNotContain(problems, p => p.Message.Contains("Border of a control"));
    }

    [Fact]
    public void A_brand_is_held_to_every_colour_scheme_too()
    {
        // paper sets its own text and surfaces, so a brand's text colour does not reach it; the status text is the brand's and has to hold on paper as well
        var results = BrandContrast.Run(Tokens, Schemes, Make(new() { ["--mat-warning-text"] = "#e0b000" }), AppRegistry.All);
        Assert.Contains(results, r => r.Scheme == "paper" && r.Fails && r.Name.StartsWith("Warning text", StringComparison.Ordinal));
        Assert.Contains(results, r => r.Scheme == "contrast" && r.Mode == "dark");
        Assert.DoesNotContain(results, r => r.Scheme == "oled" && r.Mode == "light");
    }

    [Fact]
    public void A_brand_is_not_blamed_for_what_fails_without_it()
    {
        // the numbers of the toolkit itself: nothing it holds required fails, so a harmless brand adds nothing
        Assert.DoesNotContain(BrandContrast.Run(Tokens, Schemes, null, AppRegistry.All), r => r.Fails);
        Assert.Empty(BrandContrast.Problems(Tokens, Schemes, Make(new() { ["--mat-radius"] = "8px" }), AppRegistry.All));
    }

    [Fact]
    public void The_pairings_are_data_that_the_checks_and_the_editor_share()
    {
        var surfaces = SchemeSurfaces.Read(TokensCss, null, dark: false);
        Assert.Equal(SchemeSurfaces.Pairings.Count, surfaces.Checks().Count);
        Assert.Equal(surfaces.Checks().Select(c => c.Name), SchemeSurfaces.Pairings.Select(p => p.Name.Replace("{mode}", "light", StringComparison.Ordinal)));
        Assert.All(SchemeSurfaces.Pairings, p =>
        {
            Assert.Contains(Editable, t => t.Name == p.Foreground);
            Assert.Contains(Editable, t => t.Name == p.Background);
            Assert.True(p.Over is null || Editable.Any(t => t.Name == p.Over));
        });
    }

    [Fact]
    public void The_toolkits_own_values_for_a_token_are_the_default_of_the_brand_surfaces()
    {
        var plain = SchemeSurfaces.Read(Tokens, Schemes, null, false, null);
        var branded = SchemeSurfaces.Read(Tokens, Schemes, null, false, new Dictionary<string, string> { ["--mat-bg"] = "#102030" });
        Assert.Equal("#f5f4f1", plain.Bg.ToHex());
        Assert.Equal("#102030", branded.Bg.ToHex());
        // a scheme wins over the brand, like its more specific selector does
        Assert.Equal(SchemeSurfaces.Read(Tokens, Schemes, "paper", false, null).Bg.ToHex(), SchemeSurfaces.Read(Tokens, Schemes, "paper", false, new Dictionary<string, string> { ["--mat-bg"] = "#102030" }).Bg.ToHex());
    }
}
