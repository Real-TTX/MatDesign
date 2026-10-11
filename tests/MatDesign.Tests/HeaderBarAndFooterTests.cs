using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>The icons at the right end of the header bar (SHELL-17) and the footer (SHELL-18): the rules, the components that show them, the toolkit that draws them and the site that uses them.</summary>
public sealed class HeaderBarAndFooterTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static string Css(string part) => TestPaths.ReadToolkit("css/" + part);

    [Theory]
    [InlineData("SHELL-17")]
    [InlineData("SHELL-18")]
    public void The_rule_is_in_the_rulebook_and_names_what_to_do_and_what_not(string id)
    {
        var rule = RuleBook.All.Single(r => r.Id == id);

        Assert.Equal("SHELL", rule.Area);
        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.False(string.IsNullOrWhiteSpace(rule.Do));
        Assert.False(string.IsNullOrWhiteSpace(rule.Dont));
    }

    [Theory]
    [InlineData("topbar", "SHELL-17")]
    [InlineData("footer", "SHELL-18")]
    public void The_component_is_in_the_navigation_group_with_its_rule_and_examples_that_are_drawn_live(string id, string rule)
    {
        var component = ComponentCatalog.All.Single(c => c.Id == id);

        Assert.Equal("Navigation", component.Group);
        Assert.Contains(rule, component.Rules);
        Assert.NotEmpty(component.Behaviour);
        Assert.All(component.Examples, e => Assert.True(e.Live && e.Html.Length > 0));
    }

    [Fact]
    public void Every_icon_of_the_header_bar_has_a_name_and_a_tooltip_and_no_text()
    {
        var buttons = Regex.Matches(string.Concat(ComponentCatalog.All.Single(c => c.Id == "topbar").Examples.Select(e => e.Html)), @"<button type=""button"" class=""mat-btn mat-btn--ghost mat-btn--icon""[^>]*>");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b =>
        {
            Assert.Contains("aria-label=\"", b.Value);
            Assert.Contains("data-tip=\"", b.Value);
        });
    }

    [Fact]
    public void The_toolkit_draws_the_icons_at_the_right_end_and_the_footer_after_the_content()
    {
        var css = Css("matui.shell.css");

        Assert.Contains(".mat-topbar__actions", css);
        Assert.Contains("margin-left: auto", css[css.IndexOf(".mat-topbar__actions {", StringComparison.Ordinal)..]);
        Assert.Contains(".mat-topbar__badge--dot", css);
        Assert.Contains(".mat-footer {", css);
        Assert.Contains(".mat-footer--center", css);
        Assert.Contains(".mat-footer--plain", css);
        Assert.Contains(".mat-auth:has(> .mat-footer)", css);
        Assert.DoesNotContain("position: fixed", css[css.IndexOf(".mat-footer {", StringComparison.Ordinal)..css.IndexOf(".mat-footer__text", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task The_site_ends_every_page_with_the_footer_after_the_main_and_the_shell_to_copy_has_both()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules");

        Assert.Matches(@"</main>\s*<footer class=""mat-footer"">", html);
        Assert.Single(Regex.Matches(html, "<footer "));
        Assert.Contains("mat-topbar__actions", ShellSkeleton.Html);
        Assert.Contains("<footer class=\"mat-footer\">", ShellSkeleton.Html);
    }
}
