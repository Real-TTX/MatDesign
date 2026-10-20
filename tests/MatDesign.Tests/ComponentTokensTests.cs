using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>
/// Stage 12 of docs/PRODUCT.md, the part without a site: the component properties as values the Studio may set (type, limits, the touch-target floor), what a brand may set inside a colour scheme, the
/// overrides of an app, and the overlay that writes them in the order of the cascade (the toolkit, the brand, the app, with the dark set twice and identical and a touch target never below 44 px on a touch screen).
/// </summary>
public class ComponentTokensTests
{
    private static readonly string TokensCss = TestPaths.ReadToolkit("css/matui.tokens.css");
    private static readonly IReadOnlyList<DesignToken> Tokens = CssTokens.Tokens(TokensCss);
    private static readonly IReadOnlyList<SchemeBlocks> Schemes = CssTokens.Schemes(TokensCss);
    private static readonly IReadOnlyList<BrandToken> Editable = BrandTokens.Editable(Tokens);
    private static readonly IReadOnlyList<ComponentProperty> Properties = ComponentProperties.Parse(ToolkitFiles.CssParts.Select(p => TestPaths.ReadToolkit("css/" + p)));
    private static readonly IReadOnlyList<PropertyToken> Props = ComponentTokens.Editable(Properties, Tokens);

    private static PropertyToken Prop(string name) => Props.Single(p => p.Name == name);

    private static string? Check(string name, string value)
        => BrandValidator.CheckValue(Prop(name).Token, BrandValidator.NormalizeValue(Prop(name).Token, value));

    private static Brand Make(Dictionary<string, string>? components = null, Dictionary<string, SchemeValues>? schemes = null, Dictionary<string, string>? light = null, Dictionary<string, string>? dark = null)
        => new("Acme", "Acme", "Anvils", light ?? [], dark ?? [], components ?? [], schemes ?? []);

    private static IReadOnlyList<Problem> Problems(Brand brand)
        => BrandValidator.Validate(BrandValidator.Normalize(brand, Editable, Props, Schemes), Tokens, Schemes, AppRegistry.All, Props);

    private static string Overlay(Brand brand) => ToolkitFiles.BrandOverlay(BrandValidator.Normalize(brand, Editable, Props, Schemes), Editable, Props, Schemes);

    // ---- the properties as values

    [Fact]
    public void Every_documented_property_is_editable_with_a_kind_and_limits()
    {
        Assert.Equal(Properties.Count, Props.Count);
        Assert.Equal(71, Props.Count);
        Assert.All(Props, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Token.Hint));
            if (p.Property.Type is "length" or "number")
            {
                Assert.True(p.Token.Min is not null && p.Token.Max is not null, p.Name + " has no limits");
            }
        });

        Assert.All(Props.Where(p => p.Property.Type == "length"), p => Assert.Equal(BrandTokenKind.Length, p.Token.Kind));
        Assert.All(Props.Where(p => p.Property.Type == "number"), p => Assert.Equal(BrandTokenKind.Number, p.Token.Kind));
    }

    [Fact]
    public void The_resolved_default_is_the_value_of_the_token_the_control_falls_back_to_or_the_literal()
    {
        Assert.Equal("6px", Prop("--mat-btn-radius").Resolved);
        Assert.True(Prop("--mat-btn-radius").FallsBackToToken);
        Assert.Equal("1px", Prop("--mat-btn-border-width").Resolved);
        Assert.False(Prop("--mat-btn-border-width").FallsBackToToken);
        Assert.Equal("40px", Prop("--mat-btn-height").Resolved);
    }

    [Theory]
    [InlineData("--mat-btn-radius", "0", null)]
    [InlineData("--mat-btn-radius", "24px", null)]
    [InlineData("--mat-btn-radius", "25px", "above the most")]
    [InlineData("--mat-card-radius", "1.5rem", null)]
    [InlineData("--mat-card-radius", "2rem", "above the most")]
    [InlineData("--mat-card-pad", "48px", null)]
    [InlineData("--mat-card-pad", "49px", "above the most")]
    [InlineData("--mat-btn-pad-x", "0", null)]
    [InlineData("--mat-btn-pad-x", "-1px", "below the least")]
    [InlineData("--mat-alert-gap", "4", "A length like")]
    [InlineData("--mat-card-border-width", "4px", null)]
    [InlineData("--mat-card-border-width", "5px", "above the most")]
    [InlineData("--mat-tabs-indicator-width", "3px", null)]
    [InlineData("--mat-dialog-width", "200px", null)]
    [InlineData("--mat-dialog-width", "199px", "below the least")]
    [InlineData("--mat-toast-width", "960px", null)]
    [InlineData("--mat-toast-width", "961px", "above the most")]
    [InlineData("--mat-btn-font-weight", "700", null)]
    [InlineData("--mat-btn-font-weight", "950", "above the most")]
    [InlineData("--mat-btn-font-weight", "bold", "A number without a unit")]
    [InlineData("--mat-table-font-size", "12px", null)]
    [InlineData("--mat-table-font-size", "11px", "below the least")]
    [InlineData("--mat-tag-font-size", "11px", null)]
    [InlineData("--mat-tag-font-size", "10px", "below the least")]
    [InlineData("--mat-switch-width", "red", "A length like")]
    [InlineData("--mat-badge-radius", "8px; } body { x: 1", "does not belong")]
    [InlineData("--mat-badge-radius", "url(x)", "does not belong")]
    public void A_value_is_held_to_the_type_and_the_limits_of_its_property(string name, string value, string? message)
    {
        var result = Check(name, value);
        if (message is null)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.NotNull(result);
            Assert.Contains(message, result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_heights_of_the_controls_that_are_pressed_have_a_floor_of_32_px()
    {
        Assert.Equal(["--mat-btn-height", "--mat-input-height", "--mat-menu-item-height", "--mat-nav-item-height", "--mat-search-height", "--mat-tabs-height"], ComponentTokens.TouchProperties.Order());
        foreach (var name in ComponentTokens.TouchProperties)
        {
            Assert.True(Prop(name).Touch);
            Assert.Null(Check(name, "32px"));
            Assert.Null(Check(name, "36px"));
            Assert.Null(Check(name, "44px"));
            Assert.Null(Check(name, "72px"));
            Assert.Contains("below the least", Check(name, "31px"), StringComparison.Ordinal);
            Assert.Contains("MOBILE-04", Check(name, "31px"), StringComparison.Ordinal);
            Assert.Contains("above the most", Check(name, "73px"), StringComparison.Ordinal);
        }

        Assert.Equal(6, Props.Count(p => p.Touch));
        // a height under the touch target is for a fine pointer only
        Assert.True(ComponentTokens.AppliesToFinePointerOnly(Prop("--mat-btn-height"), "36px"));
        Assert.True(ComponentTokens.AppliesToFinePointerOnly(Prop("--mat-btn-height"), "2.25rem"));
        Assert.False(ComponentTokens.AppliesToFinePointerOnly(Prop("--mat-btn-height"), "44px"));
        Assert.False(ComponentTokens.AppliesToFinePointerOnly(Prop("--mat-btn-radius"), "4px"));
    }

    [Fact]
    public void The_knob_of_a_switch_has_to_fit_its_track()
    {
        Assert.Empty(Problems(Make(new() { ["--mat-switch-height"] = "28px", ["--mat-switch-thumb"] = "22px", ["--mat-switch-width"] = "50px" })));
        Assert.Contains(Problems(Make(new() { ["--mat-switch-thumb"] = "24px" })), p => p.Field == "Component:--mat-switch-thumb" && p.Message.Contains("smaller than its track", StringComparison.Ordinal));
        Assert.Contains(Problems(Make(new() { ["--mat-switch-height"] = "16px" })), p => p.Message.Contains("smaller than its track", StringComparison.Ordinal));
        Assert.Contains(Problems(Make(new() { ["--mat-switch-width"] = "28px", ["--mat-switch-height"] = "30px", ["--mat-switch-thumb"] = "20px" })), p => p.Message.Contains("longer than it is high", StringComparison.Ordinal));
    }

    [Fact]
    public void A_name_that_is_no_documented_property_is_refused_and_a_valid_brand_is_clean()
    {
        var problems = Problems(Make(new() { ["--mat-btn-nope"] = "1px", ["--mat-radius"] = "4px" }));
        Assert.Contains(problems, p => p.Field == "Component:--mat-btn-nope" && p.Message.Contains("not a documented component property", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Field == "Component:--mat-radius");
        Assert.Empty(Problems(Make(new() { ["--mat-btn-radius"] = "0", ["--mat-card-pad"] = "1rem", ["--mat-btn-height"] = "36px" })));
    }

    [Fact]
    public void Normalising_puts_the_values_in_the_written_form_in_the_order_of_the_css_and_drops_empty_values_and_the_literal_default()
    {
        var brand = BrandValidator.Normalize(Make(new() { ["--mat-card-pad"] = " 1.50rem ", ["--mat-btn-radius"] = "0", ["--mat-btn-border-width"] = "1px", ["--mat-tabs-gap"] = "" }), Editable, Props, Schemes);
        Assert.Equal(["--mat-btn-radius", "--mat-card-pad"], brand.ComponentValues.Keys);
        Assert.Equal("0px", brand.ComponentValues["--mat-btn-radius"]);
        Assert.Equal("1.5rem", brand.ComponentValues["--mat-card-pad"]);
    }

    // ---- the colour schemes

    [Fact]
    public void A_brand_may_change_inside_a_scheme_only_what_the_scheme_sets_in_that_mode()
    {
        var paper = Schemes.Single(s => s.Id == "paper");
        Assert.True(paper.Light!.ContainsKey("--mat-bg"));
        Assert.Empty(Problems(Make(schemes: new() { ["paper"] = new(new Dictionary<string, string> { ["--mat-bg"] = "#fbf6ea" }, new Dictionary<string, string>()) })));

        var problems = Problems(Make(schemes: new()
        {
            ["paper"] = new(new Dictionary<string, string> { ["--mat-radius"] = "4px", ["--mat-bg"] = "nope" }, new Dictionary<string, string>()),
            ["oled"] = new(new Dictionary<string, string> { ["--mat-bg"] = "#000000" }, new Dictionary<string, string>()),
            ["glass"] = new(new Dictionary<string, string> { ["--mat-bg"] = "#000000" }, new Dictionary<string, string>()),
        }));
        Assert.Contains(problems, p => p.Field == "Scheme:paper:Light:--mat-radius" && p.Message.Contains("not one of the tokens the paper scheme sets", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Field == "Scheme:paper:Light:--mat-bg" && p.Message.Contains("A colour", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Field == "Scheme:oled:Light:--mat-bg" && p.Message.Contains("no light values", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Field == "Scheme:glass" && p.Message.Contains("not a colour scheme", StringComparison.Ordinal));
    }

    [Fact]
    public void A_scheme_value_that_is_the_schemes_own_is_not_stored_and_an_empty_scheme_disappears()
    {
        var own = Schemes.Single(s => s.Id == "paper").Light!["--mat-bg"];
        var brand = BrandValidator.Normalize(Make(schemes: new() { ["paper"] = new(new Dictionary<string, string> { ["--mat-bg"] = own }, new Dictionary<string, string>()) }), Editable, Props, Schemes);
        Assert.Empty(brand.Schemes!);
        Assert.False(brand.HasOverrides);
        Assert.Equal(string.Empty, Overlay(brand));
    }

    [Fact]
    public void The_contrast_checks_run_on_the_surfaces_of_the_scheme_with_the_override_and_a_failing_pairing_is_an_error()
    {
        // the text of the paper scheme turned into a light grey on its paper: it fails there, and nowhere else
        var problems = Problems(Make(schemes: new() { ["paper"] = new(new Dictionary<string, string> { ["--mat-text"] = "#c8c8c0" }, new Dictionary<string, string>()) }));
        Assert.Contains(problems, p => p.Field == "Contrast" && p.Message.Contains("In the paper scheme", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, p => p.Field == "Contrast" && p.Message.Contains("In the contrast scheme", StringComparison.Ordinal));

        // the same colour in the dark of the oled scheme is a light text on black: fine
        Assert.Empty(Problems(Make(schemes: new() { ["oled"] = new(new Dictionary<string, string>(), new Dictionary<string, string> { ["--mat-text"] = "#f4f4f4" }) })));

        // the surfaces the checks read have the override, the scheme's own values are under it
        var surfaces = SchemeSurfaces.Modes(Tokens, Schemes, "paper", null, null, new Dictionary<string, string> { ["--mat-bg"] = "#ffffff" }, null);
        Assert.Equal("#ffffff", surfaces[0].Value("--mat-bg"));
        Assert.NotEqual("#ffffff", surfaces[1].Value("--mat-bg"));
    }

    // ---- the overlay: the order of the cascade, the dark set twice, the touch rule

    [Fact]
    public void A_brand_with_properties_writes_one_block_and_a_height_under_44_px_only_for_a_fine_pointer()
    {
        var css = Overlay(Make(new() { ["--mat-btn-radius"] = "0", ["--mat-card-pad"] = "16px", ["--mat-btn-height"] = "36px", ["--mat-input-height"] = "48px", ["--mat-nav-item-height"] = "32px" }));

        var blocks = CssTokens.ParseBlocks(css);
        var everywhere = blocks.Single(b => b.Selector == ":root" && b.Media is null);
        var fine = blocks.Single(b => b.Selector == ":root" && b.Media == "(pointer: fine)");
        Assert.Equal(["--mat-btn-radius", "--mat-card-pad", "--mat-input-height"], everywhere.Declarations.Select(d => d.Name).Order());
        Assert.Equal(["--mat-btn-height", "--mat-nav-item-height"], fine.Declarations.Select(d => d.Name).Order());
        // the one that is 48 px is everywhere, a touch target under 44 px is nowhere outside the media query
        Assert.DoesNotContain(everywhere.Declarations, d => d.Name is "--mat-btn-height" or "--mat-nav-item-height");
        Assert.DoesNotContain("pointer: coarse", css, StringComparison.Ordinal);
    }

    [Fact]
    public void A_brand_that_sets_no_property_writes_none_and_a_value_outside_its_limits_is_not_written()
    {
        Assert.Equal(string.Empty, Overlay(Make()));
        var css = ToolkitFiles.BrandOverlay(Make(new() { ["--mat-btn-radius"] = "30px", ["--mat-btn-height"] = "20px", ["--mat-nope"] = "1px", ["--mat-card-pad"] = "x; } body {" }), Editable, Props, Schemes);
        Assert.Equal(string.Empty, css);
    }

    [Fact]
    public void A_scheme_override_is_written_after_the_brand_with_the_selectors_of_the_scheme_and_the_dark_set_twice_and_identical()
    {
        var css = Overlay(Make(
            light: new() { ["--mat-radius"] = "10px" },
            schemes: new()
            {
                ["paper"] = new(new Dictionary<string, string> { ["--mat-bg"] = "#fbf6ea" }, new Dictionary<string, string> { ["--mat-bg"] = "#1c1810", ["--mat-text"] = "#f2ead8" }),
                ["oled"] = new(new Dictionary<string, string>(), new Dictionary<string, string> { ["--mat-surface"] = "#0c0c0c" }),
            }));

        var blocks = CssTokens.ParseBlocks(css);
        Assert.Equal(["--mat-bg"], blocks.Single(b => b.Selector == ":root[data-scheme=\"paper\"]").Declarations.Select(d => d.Name));
        var dark = blocks.Single(b => b.Selector == ":root[data-scheme=\"paper\"][data-theme=\"dark\"]");
        var system = blocks.Single(b => b.Selector == ":root[data-scheme=\"paper\"]:not([data-theme=\"light\"]):not([data-theme=\"dark\"])" && b.Media!.Contains("prefers-color-scheme: dark", StringComparison.Ordinal));
        Assert.Equal(dark.Declarations.Select(d => (d.Name, d.Value)), system.Declarations.Select(d => (d.Name, d.Value)));
        Assert.Equal(2, dark.Declarations.Count);

        // oled has dark values only, and no light block is written for it
        Assert.DoesNotContain(blocks, b => b.Selector == ":root[data-scheme=\"oled\"]");
        Assert.Contains(blocks, b => b.Selector == ":root[data-scheme=\"oled\"][data-theme=\"dark\"]");

        // after the brand's own blocks
        Assert.True(css.IndexOf(":root {\n  --mat-radius", StringComparison.Ordinal) < css.IndexOf("[data-scheme=\"paper\"]", StringComparison.Ordinal));
    }

    [Fact]
    public void A_scheme_override_of_a_token_the_scheme_does_not_set_is_not_written()
    {
        var css = ToolkitFiles.BrandOverlay(Make(schemes: new() { ["paper"] = new(new Dictionary<string, string> { ["--mat-radius"] = "4px" }, new Dictionary<string, string>()) }), Editable, Props, Schemes);
        Assert.Equal(string.Empty, css);
    }

    // ---- the overrides of an app

    private static MatApp App(Dictionary<string, string>? light = null, Dictionary<string, string>? dark = null, Dictionary<string, string>? props = null)
        => AppRegistry.Get("matmail")! with
        {
            Tokens = light is null && dark is null ? null : new AppTokens(light ?? [], dark ?? []),
            Properties = props,
        };

    private static IReadOnlyList<Problem> AppProblems(MatApp app, Brand? brand = null)
        => AppOverrides.Validate(AppOverrides.Normalize(app, Editable, Props), Editable, Props, brand);

    [Fact]
    public void An_app_may_set_shape_spacing_type_and_motion_but_not_colours_or_the_layout()
    {
        var names = AppOverrides.Tokens(Editable).Select(t => t.Name).ToHashSet();
        foreach (var name in new[] { "--mat-radius", "--mat-radius-lg", "--mat-space-4", "--mat-fs-md", "--mat-lh", "--mat-font", "--mat-dur", "--mat-ease" })
        {
            Assert.Contains(name, names);
        }

        foreach (var name in new[] { "--mat-bg", "--mat-text", "--mat-danger", "--mat-nav-w", "--mat-control-h", "--mat-topbar-h", "--mat-accent" })
        {
            Assert.DoesNotContain(name, names);
        }

        Assert.All(AppOverrides.Tokens(Editable), t => Assert.NotEqual(BrandTokenKind.Colour, t.Kind));
        Assert.Empty(AppProblems(App(light: new() { ["--mat-radius"] = "2px", ["--mat-fs-md"] = "1rem" }, props: new() { ["--mat-btn-radius"] = "0" })));
        Assert.Contains(AppProblems(App(light: new() { ["--mat-bg"] = "#ffffff" })), p => p.Field == "Light:--mat-bg" && p.Message.Contains("belongs to the brand", StringComparison.Ordinal));
        Assert.Contains(AppProblems(App(light: new() { ["--mat-control-h"] = "44px" })), p => p.Field == "Light:--mat-control-h");
        Assert.Contains(AppProblems(App(dark: new() { ["--mat-radius"] = "2px" })), p => p.Field == "Dark:--mat-radius" && p.Message.Contains("no dark value", StringComparison.Ordinal));
    }

    [Fact]
    public void An_apps_values_have_the_limits_of_the_brand_editor_and_the_type_scale_includes_the_brands_sizes()
    {
        Assert.Contains(AppProblems(App(light: new() { ["--mat-radius"] = "30px" })), p => p.Message.Contains("above the most", StringComparison.Ordinal));
        Assert.Contains(AppProblems(App(light: new() { ["--mat-fs-md"] = "12px" })), p => p.Field == "Light:--mat-fs-md");
        Assert.Contains(AppProblems(App(props: new() { ["--mat-btn-height"] = "20px" })), p => p.Field == "Property:--mat-btn-height" && p.Message.Contains("MOBILE-04", StringComparison.Ordinal));
        Assert.Contains(AppProblems(App(props: new() { ["--mat-nope"] = "1px" })), p => p.Field == "Property:--mat-nope");
        Assert.Empty(AppProblems(App(props: new() { ["--mat-btn-height"] = "34px" })));

        // the app's body text may not be larger than the large text of the brand: the scale only grows
        var brand = Make(light: new() { ["--mat-fs-lg"] = "1rem" });
        Assert.Contains(AppProblems(App(light: new() { ["--mat-fs-md"] = "1.25rem" }), brand), p => p.Message.Contains("type scale only grows", StringComparison.Ordinal));
        // the knob of a switch is held against the brand's track
        var wide = Make(new() { ["--mat-switch-height"] = "20px", ["--mat-switch-thumb"] = "14px" });
        Assert.Contains(AppProblems(App(props: new() { ["--mat-switch-thumb"] = "24px" }), wide), p => p.Message.Contains("smaller than its track", StringComparison.Ordinal));
    }

    [Fact]
    public void An_app_that_sets_nothing_has_no_maps_and_stays_as_it_was()
    {
        var app = AppRegistry.Get("matmail")!;
        var normalised = AppOverrides.Normalize(app with { Tokens = new AppTokens(new Dictionary<string, string> { ["--mat-radius"] = "" }, new Dictionary<string, string>()), Properties = new Dictionary<string, string>() }, Editable, Props);
        Assert.Null(normalised.Tokens);
        Assert.Null(normalised.Properties);
        Assert.Equal(app, normalised);
        Assert.False(normalised.HasOverrides);
        Assert.DoesNotContain("tokens", System.Text.Json.JsonSerializer.Serialize(normalised, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), StringComparison.Ordinal);
        Assert.DoesNotContain("properties", System.Text.Json.JsonSerializer.Serialize(normalised, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), StringComparison.Ordinal);
    }

    [Fact]
    public void An_app_with_overrides_is_safe_only_when_its_values_are()
    {
        Assert.True(AppValidator.IsSafe(App(light: new() { ["--mat-radius"] = "4px" })));
        Assert.False(AppValidator.IsSafe(App(light: new() { ["--mat-radius"] = "4px; } * { x: 1" })));
        Assert.False(AppValidator.IsSafe(App(props: new() { ["--mat-btn-radius"] = "url(x)" })));
        Assert.False(AppValidator.IsSafe(App(props: new() { ["color"] = "red" })));
    }

    [Fact]
    public void The_overlay_of_an_app_is_on_data_app_after_the_brand_and_writes_nothing_for_an_app_without_overrides()
    {
        var plain = AppRegistry.Get("matmail")!;
        Assert.Equal(string.Empty, ToolkitFiles.AppOverridesOverlay([plain, AppRegistry.Get("matdo")!], Editable, Props));

        var app = AppOverrides.Normalize(App(light: new() { ["--mat-radius"] = "2px" }, props: new() { ["--mat-btn-radius"] = "0", ["--mat-btn-height"] = "34px", ["--mat-card-pad"] = "1rem" }), Editable, Props);
        var css = ToolkitFiles.AppOverridesOverlay([plain, app], Editable, Props);
        var blocks = CssTokens.ParseBlocks(css);
        Assert.Equal(["--mat-radius"], blocks.Single(b => b.Selector == "[data-app=\"matmail\"]" && b.Media is null && b.Declarations.Any(d => d.Name == "--mat-radius")).Declarations.Select(d => d.Name));
        var props = blocks.Where(b => b.Selector == "[data-app=\"matmail\"]" && b.Media is null && b.Declarations.Any(d => d.Name == "--mat-btn-radius")).Single();
        Assert.Equal(["--mat-btn-radius", "--mat-card-pad"], props.Declarations.Select(d => d.Name));
        var fine = blocks.Single(b => b.Media == "(pointer: fine)");
        Assert.Equal("[data-app=\"matmail\"]", fine.Selector);
        Assert.Equal(["--mat-btn-height"], fine.Declarations.Select(d => d.Name));
        Assert.DoesNotContain("matdo", css, StringComparison.Ordinal);
    }

    [Fact]
    public void The_dark_set_of_an_app_is_written_twice_and_identical_for_a_token_that_has_a_dark_value()
    {
        // the subset of the toolkit has no token with a dark value, so the writer is held to it with a token list that has one
        var tokens = Editable.Select(t => t.Name == "--mat-radius" ? t with { Dark = "6px" } : t).ToList();
        var app = AppRegistry.Get("matmail")! with { Tokens = new AppTokens(new Dictionary<string, string> { ["--mat-radius"] = "2px" }, new Dictionary<string, string> { ["--mat-radius"] = "3px" }) };
        var blocks = CssTokens.ParseBlocks(ToolkitFiles.AppOverridesOverlay([app], tokens, Props));

        var dark = blocks.Single(b => b.Selector == ":root[data-theme=\"dark\"][data-app=\"matmail\"]");
        var system = blocks.Single(b => b.Selector == ":root:not([data-theme=\"light\"]):not([data-theme=\"dark\"])[data-app=\"matmail\"]" && b.Media!.Contains("prefers-color-scheme: dark", StringComparison.Ordinal));
        Assert.Equal("3px", dark.Declarations.Single().Value);
        Assert.Equal(dark.Declarations.Select(d => (d.Name, d.Value)), system.Declarations.Select(d => (d.Name, d.Value)));
    }

    [Fact]
    public void The_rules_that_state_the_order_the_limits_and_the_touch_rule_are_short_enough_for_the_studio()
    {
        foreach (var id in new[] { "COLOR-11", "SPACE-06" })
        {
            var rule = RuleBook.All.Single(r => r.Id == id);
            Assert.True(rule.Statement.Length <= 1200, $"{id} is {rule.Statement.Length} characters");
            Assert.Empty(RuleValidator.Validate(rule, RuleBook.All.Where(r => r.Id != id)));
        }

        Assert.Contains("pointer: fine", RuleBook.All.Single(r => r.Id == "SPACE-06").Statement, StringComparison.Ordinal);
        Assert.Contains("never surfaces", RuleBook.All.Single(r => r.Id == "COLOR-11").Statement, StringComparison.Ordinal);
    }

    // ---- the mapping to the documentation

    [Fact]
    public void Every_component_group_has_a_preview_that_names_an_example_of_its_documentation()
    {
        foreach (var group in ComponentProperties.Components)
        {
            var preview = PropertyPreviews.Of(group.Id);
            Assert.NotNull(preview);
            Assert.Contains(preview!.Doc, ComponentProperties.Components.Single(c => c.Id == group.Id).Docs.Append("menu"));
            var doc = ComponentCatalog.Get(preview.Doc);
            Assert.NotNull(doc);
            Assert.True(preview.Example < doc!.Examples.Count, group.Id + ": the example is not there");
            if (preview.Html is null)
            {
                Assert.True(doc.Examples[preview.Example] is { Live: true, Html.Length: > 0 }, group.Id + ": the example cannot be drawn");
            }
        }

        Assert.Equal(ComponentProperties.Components.Count, PropertyPreviews.All.Count);
        Assert.Equal(PropertyPreviews.All.Count, PropertyPreviews.All.Select(p => p.Component).Distinct().Count());
    }

    [Fact]
    public void A_still_picture_follows_the_contract_of_the_examples_and_a_preview_is_cut_off_from_any_form()
    {
        var facts = new MarkupFacts(MarkupChecks.ClassesIn(ToolkitFiles.CssParts.Select(p => TestPaths.ReadToolkit("css/" + p))), IconNamesOfSprite());
        foreach (var preview in PropertyPreviews.All.Where(p => p.Html is not null))
        {
            var problems = MarkupChecks.Check([preview.Html!], facts);
            // a dialog that is drawn still has no way to close itself, and that is the point
            Assert.True(problems.All(p => p.Check == "dialogs"), preview.Component + ": " + string.Join("; ", problems.Select(p => p.Check + ": " + p.Message)));
        }

        var detached = PropertyPreviews.Detach("<input class=\"mat-input\" /><button type=\"button\">x</button><select></select><textarea></textarea><span>a</span>");
        Assert.Equal("<input form=\"pv-none\" class=\"mat-input\" /><button form=\"pv-none\" type=\"button\">x</button><select form=\"pv-none\"></select><textarea form=\"pv-none\"></textarea><span>a</span>", detached);
    }

    private static IReadOnlySet<string> IconNamesOfSprite()
        => System.Text.RegularExpressions.Regex.Matches(TestPaths.ReadToolkit("img/matui-icons.svg"), "<symbol id=\"mat-i-([a-z0-9-]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
}
