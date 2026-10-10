using System.Text.RegularExpressions;
using MatDesign.Core;

namespace MatDesign.Tests;

/// <summary>What the rulebook says about the toolkit's CSS, checked on the CSS itself.</summary>
public partial class ToolkitCssTests
{
    public static TheoryData<string> Parts => new() { "tokens", "base", "shell", "controls", "forms", "lists", "dialogs", "layout", "apps" };

    public static TheoryData<string> PartsWithoutColours => new() { "base", "shell", "controls", "forms", "lists", "dialogs", "layout" };

    private static string Css(string part) => TestPaths.ReadToolkit($"css/matui.{part}.css");

    private static string WithoutComments(string css) => CommentRegex().Replace(css, string.Empty);

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(")]
    private static partial Regex LiteralColourRegex();

    [Theory]
    [MemberData(nameof(Parts))]
    public void A_stylesheet_has_no_stray_comment_terminator(string part)
    {
        // A "*/" inside a comment ends it early and the rest of the file is garbage: the site then looks unstyled.
        var rest = WithoutComments(Css(part));
        Assert.DoesNotContain("*/", rest);
        Assert.DoesNotContain("/*", rest);
    }

    [Theory]
    [MemberData(nameof(PartsWithoutColours))]
    public void Colours_live_in_the_tokens_only(string part)
    {
        // COLOR-01: no literal colour outside matui.tokens.css (and the accents in matui.apps.css).
        var found = LiteralColourRegex().Matches(WithoutComments(Css(part))).Select(m => m.Value).Distinct().ToList();
        Assert.True(found.Count == 0, $"matui.{part}.css has literal colours: {string.Join(", ", found)}. Make them tokens.");
    }

    [Fact]
    public void The_two_places_that_write_the_dark_set_are_identical()
    {
        // COLOR-02: data-theme="dark" and "the system is dark" must give the same colours.
        var blocks = CssTokens.ParseBlocks(Css("tokens"));
        var explicitDark = blocks
            .Where(b => b.Media is null && b.Selector.StartsWith(":root[data-theme=\"dark\"]", StringComparison.Ordinal))
            .SelectMany(b => b.Declarations.Select(d => (Selector: b.Selector.Contains("[data-app]") ? "accent" : "base", d.Name, d.Value)))
            .ToHashSet();
        var systemDark = blocks
            .Where(b => b.Media is not null && b.Media.Contains("prefers-color-scheme: dark", StringComparison.Ordinal) && b.Selector.StartsWith(":root:not(", StringComparison.Ordinal))
            .SelectMany(b => b.Declarations.Select(d => (Selector: b.Selector.Contains("[data-app]") ? "accent" : "base", d.Name, d.Value)))
            .ToHashSet();

        Assert.NotEmpty(explicitDark);
        Assert.Empty(explicitDark.Except(systemDark));
        Assert.Empty(systemDark.Except(explicitDark));
    }

    [Fact]
    public void Every_colour_token_has_a_dark_value_or_is_derived_from_one_that_has()
    {
        var tokens = CssTokens.Tokens(Css("tokens"));
        var missing = tokens
            .Where(t => t.IsColor && t.Dark is null && !t.Light.StartsWith("color-mix", StringComparison.Ordinal) && !t.Light.StartsWith("var(", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToList();
        Assert.True(missing.Count == 0, "Colour tokens without a dark value: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_token_says_what_it_is_for()
    {
        var undocumented = CssTokens.Tokens(Css("tokens")).Where(t => t.Description.Length == 0).Select(t => t.Name).ToList();
        Assert.True(undocumented.Count == 0, "Tokens without a comment: " + string.Join(", ", undocumented));
    }

    [Fact]
    public void The_tokens_are_found_in_groups()
    {
        var tokens = CssTokens.Tokens(Css("tokens"));
        Assert.True(tokens.Count > 80, $"only {tokens.Count} tokens were read");
        Assert.All(tokens, t => Assert.StartsWith("--mat-", t.Name));
        Assert.Equal(tokens.Count, tokens.Select(t => t.Name).Distinct().Count());
        foreach (var group in new[] { "Surfaces", "Text", "Status", "Accent" })
        {
            Assert.Contains(tokens, t => t.Group == group);
        }
    }

    [Fact]
    public void Every_app_of_the_registry_has_its_accent_in_the_stylesheet_and_nothing_else_does()
    {
        var blocks = CssTokens.ParseBlocks(Css("apps"))
            .Where(b => b.Selector.StartsWith("[data-app=\"", StringComparison.Ordinal))
            .ToDictionary(b => b.Selector["[data-app=\"".Length..^2], b => b.Declarations.ToDictionary(d => d.Name, d => d.Value));

        Assert.Equal(AppRegistry.All.Select(a => a.Id).Order(), blocks.Keys.Order());
        foreach (var app in AppRegistry.All)
        {
            var css = blocks[app.Id];
            Assert.Equal(app.Accent, css["--mat-app-accent"]);
            Assert.Equal(app.OnAccent, css.GetValueOrDefault("--mat-app-on-accent"));
            Assert.Equal(app.AccentText, css.GetValueOrDefault("--mat-app-accent-text"));
        }
    }

    [Fact]
    public void The_accent_presets_of_the_registry_are_in_the_stylesheet()
    {
        var blocks = CssTokens.ParseBlocks(Css("apps")).ToDictionary(b => b.Selector, b => b.Declarations.ToDictionary(d => d.Name, d => d.Value));
        foreach (var preset in AccentPresets.All)
        {
            Assert.Equal(preset.Value, blocks[$"[data-accent=\"{preset.Id}\"]"]["--mat-app-accent"]);
        }

        Assert.True(blocks.ContainsKey("[data-accent=\"custom\"]"));
    }

    [Fact]
    public void Every_accent_preset_is_readable_with_white_text()
    {
        foreach (var preset in AccentPresets.All)
        {
            Assert.True(Colors.Contrast(Rgb.White, Rgb.Parse(preset.Value)) >= 4.5, $"{preset.Name} {preset.Value} is too light for white text");
        }
    }

    [Fact]
    public void Buttons_in_a_row_are_ordered_by_the_control_not_by_the_author()
    {
        // FORM-02: primary 0, the others 1, the spacer 2, danger and "end" 3.
        var css = Css("controls");
        Assert.Matches(@"\.mat-btn-row > \.mat-btn--primary[^{]*\{ order: 0; \}", css);
        Assert.Matches(@"\.mat-btn-row > \.mat-btn,[^{]*\{ order: 1; \}", css);
        Assert.Matches(@"\.mat-btn-row__spacer \{ order: 2;", css);
        Assert.Matches(@"\.mat-btn-row > \.mat-btn--danger[^{]*\{ order: 3; \}", css);
    }

    [Fact]
    public void Every_dialog_is_full_screen_on_a_phone()
    {
        // DIALOG-03: below 640 px a dialog fills the screen, with no radius and no margin.
        var css = Css("dialogs");
        var phone = css[css.IndexOf("@media (max-width: 640px)", StringComparison.Ordinal)..];
        Assert.Contains("width: 100%; height: 100%;", phone);
        Assert.Contains("inset: 0;", phone);
        Assert.Contains("border-radius: 0;", phone);
    }

    [Fact]
    public void On_a_phone_the_drawer_covers_the_top_bar_so_the_logo_stays_visible_and_has_an_X()
    {
        // SHELL-01: the logo is above the items also in the drawer. The top bar (z-index 200) must not hide it.
        var css = Css("shell");
        var drawer = css[css.IndexOf("@media (max-width: 900px)", StringComparison.Ordinal)..];
        Assert.Contains("z-index: calc(var(--mat-z-topbar) + 2);", drawer);
        Assert.Contains(".mat-backdrop { z-index: calc(var(--mat-z-topbar) + 1); }", drawer);
        Assert.Contains(".mat-nav__brand > .mat-nav__close { display: inline-flex; }", drawer);
        Assert.Contains(".mat-nav__brand > .mat-nav__close { display: none;", css);
    }

    [Fact]
    public void Form_layouts_exist_and_the_label_beside_the_field_goes_back_above_on_a_phone()
    {
        // FORM-14
        var css = Css("forms");
        foreach (var layout in new[] { ".mat-form__grid--3", ".mat-form__grid--4", ".mat-form--horizontal", ".mat-form--inline", ".mat-form__section" })
        {
            Assert.Contains(layout, css);
        }

        var phone = css[css.IndexOf("@media (max-width: 700px) {\n  .mat-form--horizontal", StringComparison.Ordinal)..];
        Assert.Contains("display: flex", phone[..phone.IndexOf("}\n}", StringComparison.Ordinal)]);
    }

    [Fact]
    public void A_floating_label_needs_a_space_as_placeholder_and_no_visible_placeholder()
    {
        // FORM-13: the label floats when the field is not :placeholder-shown, so the placeholder is a space and is drawn transparent.
        var css = Css("forms");
        Assert.Contains(":not(:placeholder-shown) ~ .mat-label", css);
        Assert.Contains(".mat-field--float > .mat-input::placeholder", css);
    }

    [Fact]
    public void Reduced_motion_is_honoured()
    {
        // A11Y-05
        Assert.Contains("prefers-reduced-motion: reduce", Css("base"));
    }

    [Fact]
    public void Scrollbars_are_hidden_until_something_scrolls()
    {
        // SPACE-05
        var css = Css("base");
        Assert.Contains("scrollbar-color: transparent transparent", css);
        Assert.Contains(".is-scrolling", css);
    }

    [Fact]
    public void The_toolkit_uses_system_fonts_only()
    {
        // TYPE-01: no web font, no @font-face, no external URL.
        foreach (var part in new[] { "tokens", "base", "shell", "controls", "forms", "lists", "dialogs", "layout", "apps" })
        {
            var css = WithoutComments(Css(part));
            Assert.DoesNotContain("@font-face", css);
            Assert.DoesNotContain("@import", css);
            Assert.DoesNotMatch(@"url\(\s*['""]?https?:", css);
        }
    }

    [Fact]
    public void The_bundle_has_all_parts_in_the_order_of_the_cascade()
    {
        Assert.Equal(["matui.tokens.css", "matui.base.css", "matui.shell.css", "matui.controls.css", "matui.forms.css", "matui.lists.css", "matui.dialogs.css", "matui.layout.css", "matui.apps.css"],
            MatDesign.Web.Services.ToolkitFiles.CssParts);
    }
}
