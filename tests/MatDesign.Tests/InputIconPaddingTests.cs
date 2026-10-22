using System.Text.RegularExpressions;

namespace MatDesign.Tests;

/// <summary>
/// The field inside <c>mat-input-icon</c>: the room for an icon is at the side where the icon stands. The eye of a password field is at the END; a rule that gave every field of the wrapper the room of a start icon
/// left the text of a password far from its edge (the left padding was 37.6 px instead of the 12 px of a plain field).
/// </summary>
public sealed partial class InputIconPaddingTests
{
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.forms.css");

    [GeneratedRegex(@"(?m)^\s*(\.mat-input-icon > \.mat-input[^{]*)\{([^}]*)\}")]
    private static partial Regex Rule();

    [Fact]
    public void Only_a_field_that_follows_an_icon_gets_the_room_of_a_start_icon()
    {
        var rules = Rule().Matches(Css).Select(m => (Selector: m.Groups[1].Value.Trim(), Body: m.Groups[2].Value)).ToList();
        var withLeft = rules.Where(r => r.Body.Contains("padding-left", StringComparison.Ordinal)).ToList();
        Assert.Single(withLeft);
        // a start icon comes first: the field is a sibling after it (the equal specificity keeps the rules of the toolbar as they were)
        Assert.Equal(".mat-input-icon > .mat-input:where(.mat-icon ~ .mat-input)", withLeft[0].Selector);
        Assert.Contains("calc(var(--mat-space-3) + 1.6rem)", withLeft[0].Body);
    }

    [Fact]
    public void The_end_variant_makes_room_for_the_button_and_not_at_the_start()
    {
        var end = Regex.Match(Css, @"\.mat-input-icon--end > \.mat-input \{([^}]*)\}");
        Assert.True(end.Success, "the rule of the end variant was not found");
        Assert.Contains("padding-right", end.Groups[1].Value);
        Assert.DoesNotContain("padding-left", end.Groups[1].Value);
    }

    [Fact]
    public void The_catalogue_puts_the_icon_before_the_field_where_it_is_at_the_start()
    {
        // every example with a start icon has the icon as the first child of the wrapper, so that the sibling selector finds it
        foreach (var component in MatDesign.Web.Services.ComponentCatalog.All)
        {
            foreach (var example in component.Examples)
            {
                foreach (Match wrapper in Regex.Matches(example.Html, @"<div class=""mat-input-icon( [^""]*)?"">\s*(?<first><[^>]+>)"))
                {
                    var first = wrapper.Groups["first"].Value;
                    var end = wrapper.Value.Contains("mat-input-icon--end", StringComparison.Ordinal);
                    if (!end)
                    {
                        Assert.True(first.Contains("mat-icon", StringComparison.Ordinal) || first.Contains("<svg", StringComparison.Ordinal),
                            $"{component.Id}: the wrapper of a start icon has to begin with the icon, it begins with {first}");
                    }
                }
            }
        }
    }
}
