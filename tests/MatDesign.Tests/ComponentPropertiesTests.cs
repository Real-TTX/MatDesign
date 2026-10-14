using System.Text.Json;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The documented component properties (SPACE-06, stage 11 of docs/PRODUCT.md): <c>--mat-btn-radius</c> and the like. The CSS declares each one with an <c>@prop</c> line above the
/// rule that uses it; these tests keep the list from going stale (a property that is used and not declared, declared and not used, or with a fallback that is no token) and keep the
/// toolkit's promise: an unset property changes nothing.
/// </summary>
public sealed partial class ComponentPropertiesTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly string[] Parts = ToolkitFiles.CssParts.ToArray();

    private static IReadOnlyList<(string Name, string Css)> Css() => Parts.Select(p => (p, TestPaths.ReadToolkit("css/" + p))).ToList();

    private static HashSet<string> TokenNames() => CssTokens.Tokens(TestPaths.ReadToolkit("css/matui.tokens.css")).Select(t => t.Name).ToHashSet();

    private static IReadOnlyList<ComponentProperty> All() => ComponentProperties.Parse(Css().Select(p => p.Css));

    private static IReadOnlyList<string> Problems(string css, params string[] tokens)
        => ComponentProperties.Problems([("test.css", css)], tokens.ToHashSet());

    // ------------------------------------------------------------------------------------------------ the parser

    [Fact]
    public void A_prop_line_is_read_with_its_five_fields()
    {
        var props = ComponentProperties.Parse("/* @prop btn | --mat-btn-radius | length | var(--mat-radius) | Corner radius of every button. */\n.mat-btn { border-radius: var(--mat-btn-radius, var(--mat-radius)); }");

        var p = Assert.Single(props);
        Assert.Equal("btn", p.Component);
        Assert.Equal("--mat-btn-radius", p.Name);
        Assert.Equal("length", p.Type);
        Assert.Equal("var(--mat-radius)", p.Fallback);
        Assert.Equal("Corner radius of every button.", p.Description);
    }

    [Fact]
    public void Uses_keep_a_fallback_that_has_parentheses_and_leave_comments_out()
    {
        var uses = ComponentProperties.Uses("a.css", "/* var(--mat-btn-x, 1px) is a comment */ .a { width: calc(var(--mat-btn-w, var(--mat-space-2)) + 2px); height: var(--mat-btn-h, 1px); color: var(--mat-text); }");

        Assert.Equal(2, uses.Count);
        Assert.Equal(("--mat-btn-w", "var(--mat-space-2)"), (uses[0].Name, uses[0].Fallback));
        Assert.Equal(("--mat-btn-h", "1px"), (uses[1].Name, uses[1].Fallback));
    }

    [Fact]
    public void A_malformed_prop_line_is_a_problem()
    {
        var problems = Problems("/* @prop btn --mat-btn-radius length var(--mat-radius) Corner. */\n.a { border-radius: var(--mat-btn-radius, var(--mat-radius)); }", "--mat-radius");

        Assert.Contains(problems, p => p.Contains("not in the format"));
    }

    [Fact]
    public void A_property_that_is_used_without_a_prop_line_is_a_problem()
    {
        var problems = Problems(".a { border-radius: var(--mat-btn-radius, var(--mat-radius)); }", "--mat-radius");

        Assert.Contains(problems, p => p.Contains("--mat-btn-radius") && p.Contains("no @prop line"));
    }

    [Fact]
    public void A_runtime_property_of_another_kind_is_not_taken_for_a_missing_declaration()
    {
        // --mat-slider-at, --mat-swatch ... are set by the script or by an element and are no handle of a component group
        Assert.Empty(Problems(".a { left: var(--mat-slider-at, 0%); background: var(--mat-swatch, red); }"));
    }

    [Fact]
    public void A_prop_line_without_a_use_is_a_problem()
    {
        var problems = Problems("/* @prop btn | --mat-btn-radius | length | var(--mat-radius) | Corner radius of every button. */\n.a { color: red; }", "--mat-radius");

        Assert.Contains(problems, p => p.Contains("--mat-btn-radius") && p.Contains("no rule uses it"));
    }

    [Theory]
    [InlineData("length", "var(--mat-nope)", false)]
    [InlineData("length", "var(--mat-radius)", true)]
    [InlineData("length", "1px", true)]
    [InlineData("length", "0", true)]
    [InlineData("length", "12 px", false)]
    [InlineData("length", "red", false)]
    [InlineData("number", "600", true)]
    [InlineData("number", "1.5", true)]
    [InlineData("number", "1px", false)]
    [InlineData("duration", "120ms", true)]
    [InlineData("duration", "1px", false)]
    [InlineData("colour", "#ff0000", false)]
    [InlineData("colour", "var(--mat-border)", true)]
    [InlineData("font", "Arial", false)]
    [InlineData("font", "var(--mat-font)", true)]
    public void A_fallback_is_a_token_that_exists_or_a_literal_of_the_type(string type, string fallback, bool valid)
    {
        var tokens = new HashSet<string> { "--mat-radius", "--mat-border", "--mat-font" };

        Assert.Equal(valid, ComponentProperties.ValidFallback(type, fallback, tokens, out var reason));
        Assert.Equal(valid, reason.Length == 0);
    }

    [Fact]
    public void A_fallback_that_is_no_token_is_a_problem_in_the_declaration_and_in_a_use()
    {
        const string css = "/* @prop btn | --mat-btn-radius | length | var(--mat-nope) | Corner radius of every button. */\n.a { border-radius: var(--mat-btn-radius, var(--mat-nope)); }\n.b { border-radius: var(--mat-btn-radius, var(--mat-other)); }";

        var problems = Problems(css, "--mat-radius");

        Assert.Contains(problems, p => p.Contains("the fallback is wrong") && p.Contains("--mat-nope"));
        Assert.Contains(problems, p => p.Contains("used with the fallback") && p.Contains("--mat-other"));
    }

    [Fact]
    public void A_use_that_differs_from_the_declared_fallback_is_allowed_if_one_use_has_it()
    {
        // the small button falls back to a smaller radius: same property, another default for that size
        const string css = "/* @prop btn | --mat-btn-radius | length | var(--mat-radius) | Corner radius of every button. */\n.a { border-radius: var(--mat-btn-radius, var(--mat-radius)); }\n.b { border-radius: var(--mat-btn-radius, var(--mat-radius-sm)); }";

        Assert.Empty(Problems(css, "--mat-radius", "--mat-radius-sm"));
    }

    [Fact]
    public void A_property_that_is_given_a_value_is_a_problem()
    {
        const string css = "/* @prop btn | --mat-btn-radius | length | var(--mat-radius) | Corner radius of every button. */\n:root { --mat-btn-radius: 4px; }\n.a { border-radius: var(--mat-btn-radius, var(--mat-radius)); }";

        Assert.Contains(Problems(css, "--mat-radius"), p => p.Contains("is given a value"));
    }

    [Fact]
    public void A_component_that_is_unknown_or_does_not_start_the_name_is_a_problem()
    {
        var unknown = Problems("/* @prop zzz | --mat-zzz-radius | length | 1px | Corner radius of a zzz. */\n.a { border-radius: var(--mat-zzz-radius, 1px); }");
        var mismatch = Problems("/* @prop btn | --mat-card-radius | length | 1px | Corner radius of a card. */\n.a { border-radius: var(--mat-card-radius, 1px); }");

        Assert.Contains(unknown, p => p.Contains("not in ComponentProperties.Components"));
        Assert.Contains(mismatch, p => p.Contains("starts with --mat-btn-"));
    }

    // ------------------------------------------------------------------------------------------------ the toolkit

    [Fact]
    public void The_list_of_the_toolkit_is_complete_and_right()
    {
        // every use with a fallback is declared, every declaration is used, every fallback is a token that exists (or a literal of the type), none has a value of its own
        Assert.Empty(ComponentProperties.Problems(Css(), TokenNames()));
    }

    [Fact]
    public void The_set_is_small_and_every_component_group_has_a_few()
    {
        var all = All();

        Assert.InRange(all.Count, 40, 75);
        foreach (var component in ComponentProperties.Components)
        {
            Assert.InRange(ComponentProperties.Of(all, component.Id).Count(), 2, 7);
        }

        Assert.All(all, p => Assert.Contains(p.Type, ComponentProperties.Types));
        Assert.Equal(all.Count, all.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void The_declarations_are_comments_so_the_css_parses_as_before_and_no_part_gives_a_property_a_value()
    {
        // the @prop lines are comments: the bundle stays valid CSS and the parser of the tokens still finds its blocks
        foreach (var (name, css) in Css())
        {
            var blocks = CssTokens.ParseBlocks(css);
            Assert.NotEmpty(blocks);
            Assert.DoesNotContain(blocks.SelectMany(b => b.Declarations), d => d.Name.StartsWith("--mat-btn-", StringComparison.Ordinal) && name != "matui.apps.css");
        }
    }

    [Fact]
    public void A_height_falls_back_to_the_control_height_so_that_a_touch_screen_keeps_its_44_px()
    {
        // the tokens give 44 px under (pointer: coarse); a property that falls back to them keeps that until somebody sets it (and the documentation says a height is a floor)
        var heights = All().Where(p => p.Name.EndsWith("-height", StringComparison.Ordinal) && p.Component is "btn" or "input" or "menu" or "tabs" or "nav").ToList();

        Assert.True(heights.Count >= 5);
        Assert.All(heights, p => Assert.Equal("var(--mat-control-h)", p.Fallback));
        var tokens = TestPaths.ReadToolkit("css/matui.tokens.css");
        Assert.Contains("@media (pointer: coarse)", tokens);
        Assert.Contains("--mat-control-h: 44px;", tokens);
    }

    [Fact]
    public void No_property_is_a_colour_or_a_shadow_and_the_look_stays_flat()
    {
        // COLOR-01: a control has no colour of its own; SPACE-03: no property for a shadow. (The type colour exists for a later property; none is used yet.)
        var all = All();

        Assert.DoesNotContain(all, p => p.Type == "colour");
        Assert.DoesNotContain(all, p => p.Name.Contains("shadow") || p.Name.Contains("colour") || p.Name.Contains("color") || p.Name.Contains("bg"));

        // no use of a property carries !important (a property is a default that a more specific rule or an element can still change)
        var names = all.Select(p => p.Name).ToList();
        var lines = Css().SelectMany(p => p.Css.Split('\n')).Where(l => !l.TrimStart().StartsWith("/*", StringComparison.Ordinal) && names.Any(n => l.Contains($"var({n},", StringComparison.Ordinal)));
        Assert.DoesNotContain(lines, l => l.Contains("!important", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_component_of_the_documentation_that_a_group_names_exists()
    {
        var ids = ComponentCatalog.All.Select(c => c.Id).ToHashSet();

        foreach (var component in ComponentProperties.Components)
        {
            Assert.NotEmpty(component.Docs);
            Assert.All(component.Docs, d => Assert.Contains(d, ids));
        }
    }

    [Fact]
    public void The_rule_is_there_and_short_enough()
    {
        var rule = RuleBook.Get("SPACE-06");

        Assert.NotNull(rule);
        Assert.Equal("SPACE", rule.Area);
        Assert.InRange(rule.Statement.Length, 10, 1200);
        Assert.Contains("--mat-<component>-*", rule.Statement);
    }

    // ------------------------------------------------------------------------------------------------ the site

    [Fact]
    public async Task The_component_page_lists_the_properties_of_a_component_that_has_some_and_only_then()
    {
        var page = await _client.GetStringAsync("/Components");

        // the table is inside the section of the buttons and names the properties
        var buttons = Regex.Match(page, "<section class=\"doc-component mat-anchor\" id=\"buttons\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains("id=\"props-buttons\"", buttons);
        Assert.Contains("--mat-btn-radius", buttons);
        Assert.Contains("var(--mat-radius)", buttons);

        // a component without properties has no table
        var meter = Regex.Match(page, "<section class=\"doc-component mat-anchor\" id=\"meter\".*?</section>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(meter);
        Assert.DoesNotContain("id=\"props-meter\"", meter);
    }

    [Fact]
    public async Task The_tokens_page_has_a_section_with_every_property_grouped_by_component()
    {
        var page = await _client.GetStringAsync("/Foundations/Tokens");

        Assert.Contains("id=\"component-properties\"", page);
        foreach (var p in All())
        {
            Assert.Contains($"<code>{p.Name}</code>", page);
        }

        foreach (var component in ComponentProperties.Components)
        {
            Assert.Contains($"id=\"props-{component.Id}\"", page);
        }
    }

    [Fact]
    public async Task The_properties_are_served_as_json_and_the_download_page_names_them()
    {
        using var json = JsonDocument.Parse(await _client.GetStringAsync("/api/properties"));

        var rows = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(All().Count, rows.Count);
        var radius = rows.Single(r => r.GetProperty("name").GetString() == "--mat-btn-radius");
        Assert.Equal("btn", radius.GetProperty("component").GetString());
        Assert.Equal("length", radius.GetProperty("type").GetString());
        Assert.Equal("var(--mat-radius)", radius.GetProperty("fallback").GetString());
        Assert.Contains("/api/properties", await _client.GetStringAsync("/Download"));
    }

    [Fact]
    public async Task The_bundle_that_apps_copy_carries_the_declarations_and_they_parse_from_it()
    {
        var bundle = await _client.GetStringAsync("/ui/matui.css");

        Assert.Equal(All().Select(p => p.Name), ComponentProperties.Parse(bundle).Select(p => p.Name));
    }
}
