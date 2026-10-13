using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>COLOR-10: a colour chosen by the user is data. How the script reads it, how the CSS draws it and where it is documented.</summary>
public partial class MarksTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");

    private static readonly string Controls = TestPaths.ReadToolkit("css/matui.controls.css");

    [GeneratedRegex(@"var MARK_COLOUR = /(.+)/([a-z]*);")]
    private static partial Regex ScriptPattern();

    /// <summary>The pattern that matui.js reads data-colour with, as a .NET regex (the two dialects agree on this one). The browser check reads the same table.</summary>
    private static Regex ColourPattern()
    {
        var match = ScriptPattern().Match(Js);
        Assert.True(match.Success, "the pattern of the colour of a mark was not found in matui.js");
        return new Regex(match.Groups[1].Value, match.Groups[2].Value.Contains('i', StringComparison.Ordinal) ? RegexOptions.IgnoreCase : RegexOptions.None);
    }

    public static TheoryData<string, bool> Colours => new()
    {
        { "#abc", true }, { "#ABC", true }, { "#aBc", true }, { "#aabbcc", true }, { "#AABBCC", true }, { "#d6336c", true }, { "#000", true }, { "#fff", true },
        { "abc", false }, { "aabbcc", false }, { "#ab", false }, { "#abcd", false }, { "#abcde", false }, { "#aabbccd", false }, { "#aabbccdd", false }, { "#ggg", false }, { "#12345g", false },
        { "red", false }, { "rgb(0, 0, 0)", false }, { "", false }, { " #abc", false }, { "#abc ", false }, { "url(#abc)", false },
    };

    [Theory]
    [MemberData(nameof(Colours))]
    public void The_script_reads_three_or_six_hex_digits_with_a_hash_in_any_case_and_nothing_else(string value, bool valid)
        => Assert.Equal(valid, ColourPattern().IsMatch(value));

    [Theory]
    [MemberData(nameof(Colours))]
    public void The_markup_check_and_the_script_agree_on_what_a_colour_is(string value, bool valid)
    {
        var facts = new MarkupFacts(new HashSet<string> { "mat-tag" }, new HashSet<string>());
        var problems = MarkupChecks.Check([$"""<span class="mat-tag" data-colour="{value}">x</span>"""], facts);
        Assert.Equal(!valid, problems.Any(p => p.Check == MarkupChecks.Colour));
    }

    [Fact]
    public void The_css_draws_a_mark_from_the_swatch_property_and_never_derives_the_colour_of_the_text_from_it()
    {
        Assert.Matches(@"\.mat-dot \{[^}]*width: 10px; height: 10px; border-radius: 50%;[^}]*var\(--mat-swatch", Controls);
        Assert.Contains(".mat-tag[data-colour].is-coloured::before {", Controls);
        Assert.Contains(".mat-icon[data-colour] { color: var(--mat-swatch); }", Controls);
        Assert.Contains(".mat-picker__option[data-colour].is-coloured::before {", TestPaths.ReadToolkit("css/matui.pickers.css"));

        // the text of a tag is the muted colour; the rule of the dot sets a background and a ring, no colour of text
        Assert.Contains("color: var(--mat-text-muted)", Regex.Match(Controls, @"\.mat-tag \{[^}]*\}").Value);
        Assert.DoesNotMatch(@"(?<![-\w])color:", Regex.Match(Controls, @"\.mat-tag\[data-colour\][^{]*\{[^}]*\}").Value);
        // a ring in the colour of the text keeps a white dot visible in light mode and a black one in dark mode
        Assert.Contains("box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--mat-text)", Regex.Match(Controls, @"\.mat-dot \{[^}]*\}").Value);
    }

    [Fact]
    public void The_marks_are_documented_with_their_rule_among_the_pills_and_show_up_in_the_api_of_the_component()
    {
        var pills = ComponentCatalog.All.Single(c => c.Id == "pills");
        Assert.Contains("COLOR-10", pills.Rules);
        Assert.NotNull(RuleBook.Get("COLOR-10"));
        Assert.Contains("data-colour", ComponentApi.Attributes(pills));
        Assert.Contains("mat-dot", ComponentApi.Classes(pills));
        Assert.Contains("mat-tag", ComponentApi.Classes(pills));
        Assert.Contains(pills.Examples, e => e.Html.Contains("<svg class=\"mat-icon\" data-colour=", StringComparison.Ordinal));
        Assert.Contains(pills.Examples, e => e.Html.Contains("class=\"mat-dot\" data-colour=", StringComparison.Ordinal) && e.Html.Contains("aria-hidden=\"true\"", StringComparison.Ordinal));

        var header = Js[..Js.IndexOf("*/", StringComparison.Ordinal)];
        Assert.Contains("data-colour=\"#rrggbb\"", header);
        // the script applies it at the start and for markup that is added later
        Assert.Contains("applyColours();", Js);
        Assert.Contains("applyColours(scope)", Js[Js.IndexOf("refresh: function", StringComparison.Ordinal)..]);
    }
}
