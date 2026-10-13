using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>LIST-13: the carousel, its documentation, its texts and its CSS contract.</summary>
public partial class CarouselTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.lists.css");

    [GeneratedRegex(@"(?s)var CAR_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [Fact]
    public void The_carousel_is_documented_among_the_content_and_follows_its_rule()
    {
        var component = ComponentCatalog.All.Single(c => c.Id == "carousel");
        Assert.Equal("Content", component.Group);
        Assert.Contains("LIST-13", component.Rules);
        Assert.Contains("A11Y-05", component.Rules);
        Assert.True(component.Examples.Count >= 3);
        Assert.NotEmpty(component.Behaviour);
        var rule = RuleBook.Get("LIST-13");
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.May, rule.Level);
    }

    [Fact]
    public void Every_carousel_of_an_example_is_a_named_region_with_a_track_and_at_least_two_slides()
    {
        var examples = ComponentCatalog.All.Single(c => c.Id == "carousel").Examples;
        Assert.All(examples, e =>
        {
            Assert.Contains("data-carousel", e.Html);
            Assert.Matches(@"<section class=""mat-carousel[^""]*""[^>]*aria-label=""[^""]+""", e.Html);
            Assert.Contains("aria-roledescription=\"carousel\"", e.Html);
            Assert.Contains("class=\"mat-carousel__track\"", e.Html);
            Assert.True(Regex.Count(e.Html, "class=\"mat-carousel__slide\"") >= 2, e.Title);
        });
    }

    [Fact]
    public void A_show_that_moves_by_itself_is_the_exception_and_is_the_only_one_that_does()
    {
        var examples = ComponentCatalog.All.Single(c => c.Id == "carousel").Examples;
        Assert.Equal(1, examples.Count(e => e.Html.Contains("data-autoplay", StringComparison.Ordinal)));
        Assert.Contains("data-loop", examples.Single(e => e.Html.Contains("data-autoplay", StringComparison.Ordinal)).Html);
    }

    [Fact]
    public void The_carousel_has_every_text_in_both_languages()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the carousel were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 10, $"only {en.Count} texts");
        Assert.Equal(en, de);
    }

    [Fact]
    public void The_script_keeps_what_is_out_of_view_unreachable_names_the_slides_and_respects_reduced_motion()
    {
        var section = Js[Js.IndexOf("var CAR_TEXT", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("function initCarousels", StringComparison.Ordinal)];
        Assert.Contains("setAttribute('inert', '')", section);
        Assert.Contains("aria-roledescription", section);
        Assert.Contains("aria-live", section);
        Assert.Contains("prefers-reduced-motion: reduce", section);
        // the buttons at the ends are dimmed, not disabled: a disabled button loses the focus that is on it
        Assert.Contains("aria-disabled", section);
        Assert.DoesNotContain(".disabled =", section);
        // the user who stops the show stops it for good
        Assert.Contains("carStop(c)", section);
    }

    [Fact]
    public void The_strip_scrolls_and_snaps_in_css_so_it_works_without_the_script()
    {
        Assert.Contains("scroll-snap-type: x mandatory;", Css);
        Assert.Contains("scroll-snap-align: start;", Css);
        Assert.Contains("overflow-x: auto;", Css);
        // the scrollbar goes only when the script has added the buttons
        Assert.Contains(".mat-carousel.is-ready .mat-carousel__track { scrollbar-width: none; }", Css);
    }

    [Fact]
    public void What_the_script_hides_stays_hidden_and_the_dots_are_a_touch_target()
    {
        Assert.Contains(".mat-carousel__bar[hidden] { display: none; }", Css);
        // MOBILE-04: 36 px for a compact control
        Assert.Matches(@"\.mat-carousel__dot \{[^}]*width: 36px; height: 36px;", Css);
        // COLOR-04: the current page is longer, not only another colour
        Assert.Matches(@"\.mat-carousel__dot\[aria-current=""true""\]::before \{[^}]*width: 22px;", Css);
        Assert.Contains("prefers-reduced-motion", Css[Css.IndexOf(".mat-carousel", StringComparison.Ordinal)..]);
    }
}
