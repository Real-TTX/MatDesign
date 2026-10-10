using System.Text.RegularExpressions;

namespace MatDesign.Tests;

/// <summary>What the rulebook says about the toolkit's script, checked on the script itself.</summary>
public partial class ScriptTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"(?m)^\s*//.*$")]
    private static partial Regex LineComment();

    [GeneratedRegex(@"data-[a-z]+(?:-[a-z]+)*")]
    private static partial Regex DataAttribute();

    private static string Code => LineComment().Replace(BlockComment().Replace(Js, string.Empty), string.Empty);

    [Fact]
    public void No_browser_dialog_is_ever_opened()
    {
        // DIALOG-01: confirmations, alerts and inputs are dialogs of the toolkit, never window.confirm, alert or prompt.
        Assert.DoesNotMatch(@"window\.(confirm|alert|prompt)\s*\(", Code);
        Assert.DoesNotMatch(@"(?<![\w.$])(?:alert|confirm|prompt)\s*\(", Code);
    }

    [Fact]
    public void The_script_is_plain_and_dependency_free()
    {
        Assert.DoesNotMatch(@"(?m)^\s*(import|export)\s", Code);
        Assert.DoesNotContain("require(", Code);
        Assert.DoesNotContain("eval(", Code);
        Assert.DoesNotContain("new Function(", Code);
        Assert.DoesNotMatch(@"https?://", Code);
        Assert.Contains("'use strict'", Code);
    }

    [Fact]
    public void Every_documented_data_attribute_is_handled()
    {
        // The header lists the attributes an app can use. A documented attribute that nothing reads is a lie.
        var header = BlockComment().Match(Js).Value;
        var documented = DataAttribute().Matches(header).Select(m => m.Value).Where(v => v != "data-attributes").Distinct().ToList();
        Assert.True(documented.Count >= 12, $"only {documented.Count} attributes are documented");

        var code = Code;
        foreach (var attribute in documented)
        {
            Assert.True(code.Contains(attribute, StringComparison.Ordinal), $"{attribute} is documented in the header of matui.js but the script never reads it");
        }
    }

    [Theory]
    [InlineData("dialog")]
    [InlineData("confirm")]
    [InlineData("alert")]
    [InlineData("prompt")]
    [InlineData("toast")]
    [InlineData("theme")]
    [InlineData("accent")]
    [InlineData("refresh")]
    public void The_public_api_has_its_documented_members(string member)
    {
        var api = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains($"{member}:", api);
    }

    [Fact]
    public void The_theme_cycles_system_light_dark_and_system_means_no_attribute()
    {
        // SHELL-04
        Assert.Contains("var THEMES = ['system', 'light', 'dark'];", Js);
        Assert.Contains("root.removeAttribute('data-theme')", Js);
    }

    [Fact]
    public void A_preview_frame_does_not_save_its_theme_for_the_whole_site()
        => Assert.Contains("!root.hasAttribute('data-preview')", Js);

    [Fact]
    public void Scrollbars_show_only_while_scrolling()
    {
        // SPACE-05
        Assert.Contains("is-scrolling", Js);
        Assert.Contains("setTimeout", Js);
    }

    [Fact]
    public void The_boot_script_only_applies_what_is_saved_and_is_tiny()
    {
        var boot = TestPaths.ReadToolkit("js/matui.boot.js");
        Assert.True(boot.Length < 1500, $"the boot script is {boot.Length} bytes");
        Assert.Contains("mat-theme", boot);
        Assert.Contains("hasAttribute('data-theme')", boot);
        Assert.DoesNotContain("fetch(", boot);
    }

    [Fact]
    public void Dialogs_are_native_and_return_to_the_button_that_opened_them()
    {
        // DIALOG-05
        Assert.Contains("showModal", Js);
        Assert.Contains("opener", Js);
    }
}
