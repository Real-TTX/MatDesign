using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>
/// SHELL-19 (a main action in the menu), the switcher at the end of the menu (SHELL-15) and highlighted actions in the header bar (SHELL-17): the CSS that draws them,
/// the rules that name them and the places where they are documented. The browser checks look at the result in the shell.
/// </summary>
public class MenuActionTests
{
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.shell.css");

    private static string Block(string selector)
    {
        var start = Css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{selector} is not in matui.shell.css");
        return Css[start..Css.IndexOf('}', start)];
    }

    private static string Adopting => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "docs", "ADOPTING.md"));

    [Fact]
    public void The_main_action_is_filled_with_the_accent_and_stays_filled_a_step_darker_on_its_own_page()
    {
        var item = Block(".mat-nav__item--primary");
        Assert.Contains("background: var(--mat-accent);", item);
        Assert.Contains("color: var(--mat-on-accent);", item);
        Assert.Contains("background: var(--mat-accent-hover)", Block(".mat-nav__item--primary:hover"));
        Assert.Contains("background: var(--mat-accent-active)", Block(".mat-nav__item--primary:active"));
        Assert.Contains("background: var(--mat-accent-active)", Block(".mat-nav__item--primary[aria-current=\"page\"]"));
        Assert.Contains("box-shadow: inset 0 0 0 2px var(--mat-on-accent)", Block(".mat-nav__item--primary:focus-visible"));

        // the states of the plain item (the current page is a tint, the icon is muted) have the same weight: the main action has to come after them to win
        var plain = Css.IndexOf(".mat-nav__item[aria-current=\"page\"] .mat-icon", StringComparison.Ordinal);
        Assert.True(plain >= 0 && plain < Css.IndexOf(".mat-nav__item--primary {", StringComparison.Ordinal), "the main action is written before the states of the plain item and would lose to them");
        Assert.Contains("color: inherit", Block(".mat-nav__item--primary .mat-icon"));
    }

    [Fact]
    public void The_switcher_can_sit_at_the_end_of_the_menu_and_is_written_after_its_base_class()
    {
        Assert.Contains("padding: var(--mat-space-2) var(--mat-space-3) var(--mat-space-3)", Block(".mat-nav__switch--end"));
        Assert.True(Css.IndexOf(".mat-nav__switch--end {", StringComparison.Ordinal) > Css.IndexOf(".mat-nav__switch {", StringComparison.Ordinal));
    }

    [Fact]
    public void A_main_action_of_the_header_bar_is_filled_and_a_second_one_is_secondary_both_with_room_and_in_line_with_the_content()
    {
        var actions = Block(".mat-topbar__actions:has(> .mat-btn--primary, > .mat-btn--secondary)");
        Assert.Contains("gap: var(--mat-space-2)", actions);
        Assert.Contains("margin-right: 0", actions);
    }

    [Fact]
    public void The_rules_name_the_main_action_the_switcher_at_the_end_and_the_highlighted_icons()
    {
        var main = RuleBook.All.Single(r => r.Id == "SHELL-19");
        Assert.Equal("SHELL", main.Area);
        Assert.Equal(RuleLevel.Should, main.Level);
        Assert.Equal(RuleBook.Owner, main.Source);
        Assert.Contains("mat-nav__item--primary", main.Statement);
        Assert.False(string.IsNullOrWhiteSpace(main.Why));
        Assert.False(string.IsNullOrWhiteSpace(main.Do));
        Assert.False(string.IsNullOrWhiteSpace(main.Dont));

        Assert.Contains("mat-nav__switch--end", RuleBook.All.Single(r => r.Id == "SHELL-15").Statement);
        var bar = RuleBook.All.Single(r => r.Id == "SHELL-17").Statement;
        Assert.Contains("mat-btn--primary", bar);
        Assert.Contains("mat-btn--secondary", bar);
    }

    [Fact]
    public void They_are_documented_in_the_adoption_guide_and_in_examples_of_the_components()
    {
        foreach (var documented in new[] { "mat-nav__item--primary", "mat-nav__switch--end", "mat-btn--primary mat-btn--icon", "mat-btn--secondary mat-btn--icon", "SHELL-19" })
        {
            Assert.Contains(documented, Adopting);
        }

        var navmenu = ComponentCatalog.All.Single(c => c.Id == "navmenu");
        Assert.Contains("SHELL-19", navmenu.Rules);
        var action = navmenu.Examples.Single(e => e.Title == "A main action");
        Assert.Equal(1, Regex.Count(action.Html, "mat-nav__item--primary"));
        Assert.Matches(@"<ul class=""mat-nav__list"">\s*<li><a class=""mat-nav__item mat-nav__item--primary""", action.Html);

        var switcher = ComponentCatalog.All.Single(c => c.Id == "switcher");
        var end = switcher.Examples.Single(e => e.Title == "At the end of the menu");
        Assert.Contains("data-menu-place=\"top\"", end.Html);
        Assert.True(end.Html.IndexOf("mat-nav__switch--end", StringComparison.Ordinal) > end.Html.LastIndexOf("data-nav-panel=", StringComparison.Ordinal), "the switcher has to come after the panels");

        var topbar = ComponentCatalog.All.Single(c => c.Id == "topbar");
        var highlighted = topbar.Examples.Single(e => e.Title == "A main action and a secondary one");
        Assert.Equal(1, Regex.Count(highlighted.Html, "mat-btn--primary mat-btn--icon"));
        Assert.Equal(1, Regex.Count(highlighted.Html, "mat-btn--secondary mat-btn--icon"));
        Assert.DoesNotContain("mat-btn--ghost", highlighted.Html);
        Assert.True(highlighted.Html.IndexOf("mat-btn--secondary", StringComparison.Ordinal) < highlighted.Html.IndexOf("mat-btn--primary", StringComparison.Ordinal), "the main action is the last one, at the right end");
    }
}
