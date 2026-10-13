using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// What the component pages show with each example (its code, beside it or below it, coloured and to copy), what they say about a component (the classes and
/// attributes it uses) and the real page of the sample app that each group can be seen in (with the marks that outline the components in it).
/// </summary>
public sealed partial class ComponentPageTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [GeneratedRegex(@"<pre class=""mat-code"" id=""(src-[^""]+)"" tabindex=""0""><code>(.*?)</code></pre>", RegexOptions.Singleline)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"<([a-z][a-z0-9]*)([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex StartTag();

    private Task<string> Page(string url) => _client.GetStringAsync(url);

    private static string Text(string highlighted) => WebUtility.HtmlDecode(Regex.Replace(highlighted, @"</?span[^>]*>", string.Empty));

    /// <summary>Looks for what a simple selector names (<c>.class</c>, <c>tag.class[attribute]</c>, a comma list, a descendant: the last part counts) among the tags of a page.</summary>
    internal static bool Matches(string html, string selector)
        => selector.Split(',').Select(part => part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1]).Any(compound =>
        {
            var tag = Regex.Match(compound, @"^[a-z][a-z0-9]*").Value;
            var classes = Regex.Matches(compound, @"\.([\w-]+)").Select(m => m.Groups[1].Value).ToList();
            var attributes = Regex.Matches(compound, @"\[([\w-]+)(?:=""([^""]*)"")?\]").Select(m => (m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null)).ToList();
            return StartTag().Matches(html).Any(t =>
            {
                var attrs = t.Groups[2].Value;
                var classAttribute = Regex.Match(attrs, @"\sclass=""([^""]*)""").Groups[1].Value.Split(' ');
                return (tag.Length == 0 || t.Groups[1].Value.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    && classes.All(classAttribute.Contains)
                    && attributes.All(a => a.Item2 is null ? Regex.IsMatch(attrs, @"(^|\s)" + Regex.Escape(a.Item1) + @"(\s|=|$)") : attrs.Contains($"{a.Item1}=\"{a.Item2}\"", StringComparison.Ordinal));
            });
        });

    [Fact]
    public void The_selector_matcher_finds_classes_tags_attributes_lists_and_descendants()
    {
        const string html = "<main class=\"mat-page\"><button class=\"mat-btn mat-btn--primary\" data-tip=\"x\">a</button><dialog class=\"mat-dialog mat-dialog--sm\" id=\"d\"></dialog><input class=\"mat-input\" data-msg-required=\"m\"></main>";

        Assert.True(Matches(html, ".mat-btn"));
        Assert.True(Matches(html, ".mat-page .mat-btn"));
        Assert.True(Matches(html, "[data-tip]"));
        Assert.True(Matches(html, "[data-msg-required]"));
        Assert.True(Matches(html, "dialog.mat-dialog"));
        Assert.True(Matches(html, ".mat-stack, .mat-input"));
        Assert.True(Matches(html, "input[data-msg-required=\"m\"]"));
        Assert.False(Matches(html, ".mat-toolbar"));
        Assert.False(Matches(html, "dialog.mat-btn"));
        Assert.False(Matches(html, "[data-nothing]"));
        Assert.False(Matches(html, ".mat-btn--sm"));
    }

    // ---------------------------------------------------------------------------------------------------- the code of an example

    [Theory]
    [InlineData("/Components")]
    [InlineData("/Layout")]
    [InlineData("/Utilities")]
    public async Task Every_example_shows_its_code_with_it_coloured_with_a_copy_button_and_nothing_behind_a_click(string url)
    {
        var section = ComponentCatalog.Sections.Single(s => s.Path == url);
        var html = await Page(url);
        var examples = ComponentCatalog.In(section).SelectMany(c => c.Examples.Select((e, i) => (Id: $"src-{c.Id}-{i + 1}", Example: e))).ToList();

        Assert.Equal(examples.Count, Regex.Matches(html, "<div class=\"doc-example__body[ \"]").Count);
        Assert.Equal(examples.Count, Regex.Matches(html, "<div class=\"doc-code\">").Count);
        Assert.DoesNotContain("doc-example__code", html);
        var blocks = CodeBlock().Matches(html).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        foreach (var (id, example) in examples)
        {
            Assert.True(blocks.ContainsKey(id), $"{id}: no code block");
            // the markup of an example is shown formatted (indented, wrapped); a script as it is written
            Assert.Equal(example.Js ?? MatDesign.Core.Studio.HtmlFormatter.Pretty(example.Html), Text(blocks[id]));
            Assert.Contains($"data-copy=\"#{id}\"", html);
        }

        Assert.Contains("mat-code__tag", html);
    }

    [Fact]
    public async Task A_script_example_is_labelled_script_and_an_example_with_a_demo_can_be_tried_beside_its_code()
    {
        var html = await Page("/Components");

        Assert.Contains("<span class=\"doc-code__lang\">Script</span>", html);
        Assert.Contains("<span class=\"doc-code__lang\">HTML</span>", html);
        Assert.Contains("mat-code__kw", html);
    }

    [Fact]
    public async Task An_example_that_needs_the_whole_width_has_its_code_below_and_the_others_have_it_beside()
    {
        var html = await Page("/Components");

        Assert.True(ComponentCatalog.Get("toolbar")!.Examples[0].Wide);
        Assert.True(ComponentCatalog.Get("table")!.Examples[0].Wide);
        Assert.False(ComponentCatalog.Get("buttons")!.Examples[0].Wide);
        var toolbar = html[html.IndexOf("id=\"ex-toolbar-1\"", StringComparison.Ordinal)..];
        Assert.StartsWith("id=\"ex-toolbar-1\"", toolbar);
        Assert.Contains("<div class=\"doc-example__body doc-example__body--stack\">", toolbar[..toolbar.IndexOf("id=\"ex-toolbar-2\"", StringComparison.Ordinal)]);
        var buttons = html[html.IndexOf("id=\"ex-buttons-1\"", StringComparison.Ordinal)..];
        Assert.StartsWith("id=\"ex-buttons-1\"", buttons);
        Assert.Contains("<div class=\"doc-example__body\">", buttons[..buttons.IndexOf("id=\"ex-buttons-2\"", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task The_page_offers_to_put_the_code_below_the_examples_and_the_css_puts_it_beside_them_on_a_wide_window()
    {
        var html = await Page("/Components");
        var css = File.ReadAllText(TestPaths.Web("wwwroot/css/site.css"));

        Assert.Contains("data-doc-code=\"beside\" aria-pressed=\"true\"", html);
        Assert.Contains("data-doc-code=\"below\" aria-pressed=\"false\"", html);
        Assert.Matches(@"@media \(min-width: 1600px\) \{\s*:root:not\(\[data-doc-code=""below""\]\) \.doc-example__body:not\(\.doc-example__body--stack\):not\(\.doc-example__body--alone\) \{ grid-template-columns: minmax\(0, 1fr\) minmax\(0, 1fr\)", css);
        Assert.Contains("data-doc-code", File.ReadAllText(TestPaths.Web("wwwroot/js/doc.js")));
    }

    [Fact]
    public void The_classes_and_the_attributes_of_a_component_are_read_from_its_examples_in_the_order_they_appear()
    {
        var buttons = ComponentCatalog.Get("buttons")!;

        var classes = ComponentApi.Classes(buttons);
        Assert.Equal("mat-row", classes[0]);
        Assert.Equal("mat-btn", classes[1]);
        Assert.Contains("mat-btn--primary", classes);
        Assert.DoesNotContain("mat-icon", classes);
        Assert.Equal(classes.Count, classes.Distinct().Count());
        Assert.Contains("data-tip", ComponentApi.Attributes(buttons));
        Assert.DoesNotContain("data-label", ComponentApi.Attributes(ComponentCatalog.Get("table")!));
        Assert.Contains("data-tabs", ComponentApi.Attributes(ComponentCatalog.Get("tabs")!));
    }

    [Fact]
    public async Task A_component_with_many_classes_lists_the_first_ones_and_opens_the_rest_with_a_button()
    {
        var html = await Page("/Components");
        string Section(string id) => html[html.IndexOf($"<section class=\"doc-component mat-anchor\" id=\"{id}\"", StringComparison.Ordinal)..][..html[html.IndexOf($"<section class=\"doc-component mat-anchor\" id=\"{id}\"", StringComparison.Ordinal)..].IndexOf("</header>", StringComparison.Ordinal)];

        var toolbar = Section("toolbar");
        var count = ComponentApi.Classes(ComponentCatalog.Get("toolbar")!).Count;
        Assert.True(count > 14);
        Assert.Equal(count - 14, Regex.Matches(toolbar, "<code class=\"doc-api__extra\">").Count);
        Assert.Contains($"data-more=\"+{count - 14} more\">+{count - 14} more</button>", toolbar);
        Assert.DoesNotContain("data-doc-api-more", Section("buttons"));
        Assert.DoesNotContain("doc-api__extra", Section("buttons"));
    }

    [Fact]
    public async Task A_component_names_its_classes_and_attributes_under_its_rules()
    {
        var html = await Page("/Components");

        Assert.Contains("<dt>Classes</dt>", html);
        Assert.Contains("<code>mat-btn--primary</code>", html);
        Assert.Contains("<dt>Attributes</dt>", html);
        Assert.Contains("<code>data-tip</code>", html);
    }

    // ---------------------------------------------------------------------------------------------------- the group in a real page

    [Fact]
    public void Every_group_of_the_components_and_the_layout_can_be_seen_in_a_page_and_each_mark_names_a_component_of_its_group()
    {
        foreach (var group in ComponentCatalog.Groups.Where(g => g != "Utilities"))
        {
            var page = ContextPages.For(group);
            Assert.NotNull(page);
            Assert.NotEmpty(page.Marks);
            Assert.All(page.Marks, mark =>
            {
                var component = ComponentCatalog.Get(mark.Component);
                Assert.True(component is not null, $"{group}: no component {mark.Component}");
                Assert.Equal(group, component.Group);
            });
            Assert.Equal(page.Marks.Count, page.Marks.Select(m => m.Component).Distinct().Count());
        }

        Assert.Null(ContextPages.For("Utilities"));
    }

    [Fact]
    public async Task Every_mark_finds_what_it_names_in_the_page_of_the_sample_app_that_it_points_to()
    {
        foreach (var page in ContextPages.All)
        {
            foreach (var mark in page.Marks)
            {
                var view = mark.View ?? page.View;
                var html = await Page(ContextPages.Url(view));
                // an open dialog is not in the page until the button has been pressed: its markup is
                var selector = mark.Selector.Replace("[open]", string.Empty, StringComparison.Ordinal);
                Assert.True(Matches(html, selector), $"{page.Group}/{mark.Component}: {mark.Selector} is not in {ContextPages.Url(view)}");
                if (mark.Open is not null)
                {
                    Assert.True(Matches(html, mark.Open), $"{page.Group}/{mark.Component}: nothing to press at {mark.Open}");
                }

                Assert.Matches(@"^(list|gallery|form|settings|dashboard)(&q=[\w-]+)?$", view);
            }
        }
    }

    [Fact]
    public async Task The_components_page_has_a_panel_for_each_group_that_loads_nothing_until_it_is_opened()
    {
        var html = await Page("/Components");

        foreach (var group in ComponentCatalog.Sections.Single(s => s.Id == "components").Groups)
        {
            var page = ContextPages.For(group)!;
            var anchor = ComponentCatalog.GroupAnchor(group);
            Assert.Contains($"<details class=\"doc-context\" id=\"context-{anchor}\" data-context data-context-view=\"{page.View}\">", html);
            Assert.Contains($"<span>See the {group.ToLowerInvariant()} in a page</span>", html);
            foreach (var mark in page.Marks)
            {
                Assert.Contains($"data-context-component=\"{mark.Component}\" data-context-view=\"{WebUtility.HtmlEncode(mark.View ?? page.View)}\"", html);
            }
        }

        Assert.Matches(@"<iframe class=""doc-frame""[^>]*data-src=""/Preview\?app=matdesign&amp;view=form""></iframe>", html);
        Assert.DoesNotContain("<iframe class=\"doc-frame\" src=", html);
    }

    [Fact]
    public async Task A_component_that_a_page_uses_has_a_button_that_shows_it_there_and_one_that_it_does_not_use_has_none()
    {
        var html = await Page("/Components");

        foreach (var page in ContextPages.All.Where(p => ComponentCatalog.Sections.Single(s => s.Id == "components").Groups.Contains(p.Group)))
        {
            foreach (var mark in page.Marks)
            {
                Assert.Contains($"data-context-show=\"{ComponentCatalog.GroupAnchor(page.Group)}\" data-context-component=\"{mark.Component}\"", html);
            }
        }

        Assert.Equal(
            ContextPages.All.Where(p => p.Group != "Layout").SelectMany(p => p.Marks).Count(),
            Regex.Matches(html, "data-context-show=").Count);
        Assert.Null(ContextPages.Of("range"));
        Assert.DoesNotContain("data-context-component=\"range\"", html);
    }

    [Fact]
    public async Task The_layout_page_has_its_panel_and_the_utilities_page_has_none()
    {
        Assert.Contains("id=\"context-group-layout\"", await Page("/Layout"));
        Assert.DoesNotContain("doc-context", await Page("/Utilities"));
    }

    [Fact]
    public void The_script_loads_a_frame_when_its_panel_is_opened_outlines_a_component_without_scrolling_this_page_and_never_loads_what_is_shown()
    {
        var script = File.ReadAllText(TestPaths.Web("wwwroot/js/doc.js"));

        Assert.Contains("details[data-context]", script);
        Assert.Contains("frame.getAttribute('data-view') === view && frame.getAttribute('data-loaded') === 'true'", script);
        Assert.Contains("outline: 3px solid var(--mat-accent)", script);
        Assert.DoesNotContain("first.scrollIntoView", script);
        Assert.Contains("data-context-show", script);
    }

    [Fact]
    public async Task The_sample_pages_that_the_panels_show_answer()
    {
        foreach (var view in ContextPages.All.SelectMany(p => p.Marks.Select(m => m.View ?? p.View).Append(p.View)).Distinct())
        {
            var response = await _client.GetAsync(ContextPages.Url(view));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
