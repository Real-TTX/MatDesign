using System.Net;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MatDesign.Tests;

/// <summary>
/// The pages of the components (LIST-18): the overview of tiles at /Components, a page for each component at /Components/{id} with the buttons and the keys that walk through them,
/// the page of everything at /Components/All, the old addresses with a hash, and the budgets that keep the pages light (the page was 3 MB with 66,000 elements before).
/// </summary>
public sealed partial class ComponentsPagesTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static readonly CatalogSection Section = ComponentCatalog.Sections.Single(s => s.Id == "components");

    private static IReadOnlyList<ComponentDoc> Components => [.. ComponentCatalog.In(Section)];

    [GeneratedRegex(@"<li class=""doc-tile""[^>]*?data-component=""([^""]+)""([^>]*)>(.*?)</li>", RegexOptions.Singleline)]
    private static partial Regex Tile();

    [GeneratedRegex(@"<h1\b")]
    private static partial Regex Heading();

    [GeneratedRegex(@"\sid=""([^""]+)""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex(@"\shref=""#([^""]+)""")]
    private static partial Regex FragmentLink();

    [GeneratedRegex(@"\sstyle\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"<[a-zA-Z][a-zA-Z0-9-]*[\s>/]")]
    private static partial Regex StartTag();

    [GeneratedRegex(@"<title>([^<]*)</title>")]
    private static partial Regex Title();

    private async Task<string> Page(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode}");
        return await response.Content.ReadAsStringAsync();
    }

    private static int Elements(string html) => StartTag().Matches(html).Count;

    /// <summary>The ids of the tiles that the page shows (not hidden), in the order of the page.</summary>
    private static List<string> ShownTiles(string html)
        => [.. Tile().Matches(html).Where(m => !Regex.IsMatch(m.Value[..m.Value.IndexOf('>', StringComparison.Ordinal)], @"\shidden(\s|=|$)")).Select(m => m.Groups[1].Value)];

    // ---------------------------------------------------------------------------------------------------- the overview

    [Fact]
    public async Task The_overview_has_a_tile_for_every_component_under_the_heading_of_its_group_in_the_order_of_the_catalogue()
    {
        var html = await Page("/Components");

        Assert.Single(Heading().Matches(html));
        var tiles = Tile().Matches(html).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(Components.Select(c => c.Id), tiles);
        Assert.Equal(68, tiles.Count);

        // a heading per group, in the order of the catalogue, and the tiles of a group stand under their own heading
        var position = -1;
        foreach (var group in Section.Groups)
        {
            var anchor = ComponentCatalog.GroupAnchor(group);
            var at = html.IndexOf($"<section class=\"doc-group doc-group--tiles mat-anchor\" id=\"{anchor}\"", StringComparison.Ordinal);
            Assert.True(at > position, $"{group}: the heading is missing or out of order");
            position = at;
            Assert.Contains($"<h2 id=\"h-{anchor}\">{group}</h2>", html);
            var block = html[at..html.IndexOf("</section>", at, StringComparison.Ordinal)];
            Assert.Equal(Components.Where(c => c.Group == group).Select(c => c.Id), Tile().Matches(block).Select(m => m.Groups[1].Value));
        }
    }

    [Fact]
    public async Task A_tile_is_a_link_to_the_page_of_its_component_with_an_icon_the_name_the_summary_and_the_facts()
    {
        var html = await Page("/Components");
        var toolkit = (MatDesign.Web.Services.ToolkitFiles)factory.Services.GetService(typeof(MatDesign.Web.Services.ToolkitFiles))!;

        foreach (Match tile in Tile().Matches(html))
        {
            var doc = ComponentCatalog.Get(tile.Groups[1].Value)!;
            var body = tile.Groups[3].Value;
            // the whole tile is the link: one a in the item, and it has everything in it
            Assert.Single(Regex.Matches(body, "<a "));
            Assert.Contains($"<a class=\"doc-tile__link\" href=\"/Components/{doc.Id}\">", body);
            Assert.Contains($"href=\"#mat-i-{doc.Icon}\"", body);
            Assert.Contains($"<span class=\"doc-tile__name\">{System.Net.WebUtility.HtmlEncode(doc.Name)}</span>", body);
            Assert.Contains("<span class=\"doc-tile__summary\">", body);
            Assert.Contains($"<span>{(doc.Examples.Count == 1 ? "1 example" : doc.Examples.Count + " examples")}</span>", body);
            Assert.Equal(doc.Rules.Count > 0, body.Contains(doc.Rules.Count == 1 ? "1 rule</span>" : doc.Rules.Count + " rules</span>", StringComparison.Ordinal));
            var properties = MatDesign.Core.ComponentProperties.ForDoc(toolkit.Properties, doc.Id).Count;
            Assert.Equal(properties > 0, Regex.IsMatch(body, @"<span>\d+ propert(y|ies)</span>"));
        }
    }

    [Fact]
    public async Task The_overview_draws_no_example_and_starts_no_control()
    {
        var html = await Page("/Components");

        // what made the page slow: every example of every component, and the pickers, the calendars, the trees and the schedules that the script starts in them
        foreach (var heavy in new[] { "doc-example", "data-picker", "data-calendar", "data-schedule", "data-tree", "data-carousel", "data-stepper", "data-otp", "data-itemlist", "data-slider", "data-upload", "mat-code" })
        {
            Assert.DoesNotContain(heavy, html);
        }

        Assert.Contains("<script src=\"/js/components.js\"></script>", html);
        Assert.DoesNotContain("/js/doc.js", html);
    }

    [Fact]
    public async Task The_toolbar_of_the_overview_has_a_search_field_a_count_in_a_live_region_and_a_filter_by_group()
    {
        var html = await Page("/Components");

        Assert.True(html.IndexOf("class=\"mat-toolbar\"", StringComparison.Ordinal) < html.IndexOf("class=\"doc-tilearea\"", StringComparison.Ordinal), "the toolbar is above the tiles (LIST-02)");
        Assert.Contains("class=\"mat-input-icon mat-toolbar__search\"", html);
        Assert.Contains("<input class=\"mat-input\" type=\"search\" id=\"cmp-q\" name=\"q\" form=\"cmp-form\"", html);
        Assert.Contains("<span class=\"mat-toolbar__info\" aria-live=\"polite\">68 components</span>", html);
        Assert.Contains("id=\"cmp-filter\"", html);
        Assert.Contains("<select class=\"mat-select\" id=\"cmp-f-group\" name=\"group\" form=\"cmp-form\">", html);
        foreach (var group in Section.Groups)
        {
            Assert.Contains($"<option value=\"{group}\"", html);
        }

        Assert.DoesNotContain("id=\"cmp-sort\"", html);
        Assert.Contains("<form id=\"cmp-form\" method=\"get\" action=\"/Components\" hidden>", html);
        // the index beside the content and in the dialog, with the groups
        Assert.Equal(2, Regex.Matches(html, "<nav class=\"mat-toc\" aria-label=\"Groups of components\" data-scrollspy>").Count);
        Assert.Contains("data-dialog-open=\"toc-dialog\"", html);
        foreach (var group in Section.Groups)
        {
            Assert.Equal(2, Regex.Matches(html, $"href=\"#{ComponentCatalog.GroupAnchor(group)}\"").Count);
        }
    }

    [Theory]
    [InlineData("slider", new[] { "slider" }, new[] { "buttons" })]
    [InlineData("SLIDER", new[] { "slider" }, new[] { "buttons" })]
    [InlineData("form-15", new[] { "date-picker" }, new[] { "buttons" })]
    [InlineData("button group", new[] { "button-group" }, new[] { "slider" })]
    [InlineData("  picker   forms ", new[] { "date-picker", "slot-picker" }, new[] { "buttons" })]
    [InlineData("DÉPÔT", new string[0], new[] { "buttons", "slider" })]
    public async Task The_server_shows_the_tiles_whose_text_has_every_word_of_the_query_ignoring_case_and_accents(string query, string[] expected, string[] absent)
    {
        var html = await Page("/Components?q=" + Uri.EscapeDataString(query));
        var shown = ShownTiles(html);

        Assert.All(expected, e => Assert.Contains(e, shown));
        Assert.All(absent, a => Assert.DoesNotContain(a, shown));
        // and nothing else: exactly the components whose text has every word
        Assert.Equal(Components.Where(c => ComponentOverview.Matches(ComponentOverview.SearchText(c), query)).Select(c => c.Id), shown);
        // the count says how many are left, and the tiles that are not are still in the page (hidden), so that the script can show them again
        Assert.Equal(68, Tile().Matches(html).Count);
        if (shown.Count == 0)
        {
            Assert.Contains("<span class=\"mat-toolbar__info\" aria-live=\"polite\">0 of 68 components</span>", html);
            Assert.DoesNotContain("data-components-empty hidden", html);
        }
        else
        {
            Assert.Contains($"<span class=\"mat-toolbar__info\" aria-live=\"polite\">{shown.Count} of 68 components</span>", html);
            Assert.Contains("data-components-empty hidden", html);
        }
    }

    [Fact]
    public async Task A_group_filter_shows_one_group_and_hides_the_others_with_their_headings_and_an_unknown_group_is_no_filter()
    {
        var html = await Page("/Components?group=Forms");
        var forms = Components.Where(c => c.Group == "Forms").Select(c => c.Id).ToList();

        Assert.Equal(forms, ShownTiles(html));
        Assert.Matches(@"<section class=""doc-group doc-group--tiles mat-anchor"" id=""group-actions""[^>]*\shidden", html);
        Assert.DoesNotMatch(@"<section class=""doc-group doc-group--tiles mat-anchor"" id=""group-forms""[^>]*\shidden", html);
        Assert.Contains($"{forms.Count} of 68 components", html);
        // the filter is on its button and as a chip that removes it
        Assert.Contains("mat-toolbar__btn is-active", html);
        Assert.Contains("Group: Forms", html);
        Assert.Contains("<option value=\"Forms\" selected", html);

        Assert.Equal(68, ShownTiles(await Page("/Components?group=Toys")).Count);
        Assert.Equal(68, ShownTiles(await Page("/Components?group=Layout")).Count);
    }

    [Fact]
    public async Task A_search_that_finds_nothing_shows_the_empty_state_with_the_way_out_and_the_text_stays_in_the_field()
    {
        var html = await Page("/Components?q=zzzzzz");

        Assert.Empty(ShownTiles(html));
        Assert.Contains("<div class=\"mat-empty\" data-components-empty>", html);
        Assert.Contains("No component matches", html);
        Assert.Contains("<a class=\"mat-btn mat-btn--primary\" href=\"/Components\">", html);
        Assert.Contains("value=\"zzzzzz\"", html);
        Assert.Contains("<div class=\"mat-list\" data-components data-group=\"\">", html);
    }

    [Fact]
    public void The_folding_of_the_server_and_the_text_it_looks_at_are_what_the_script_does_with_the_tile()
    {
        Assert.Equal("apfel uber cafe", ComponentOverview.Fold("Äpfel Über Café"));
        Assert.Equal("naive", ComponentOverview.Fold("Naïve"));
        Assert.True(ComponentOverview.Matches(ComponentOverview.Fold("Date and period forms"), "PERIOD date"));
        Assert.False(ComponentOverview.Matches(ComponentOverview.Fold("Date and period forms"), "period tree"));
        Assert.True(ComponentOverview.Matches("anything", null));
        Assert.True(ComponentOverview.Matches("anything", "   "));

        var doc = ComponentCatalog.Get("date-picker")!;
        Assert.Equal($"Forms date-picker {string.Join(' ', doc.Rules)}", ComponentOverview.KeysOf(doc));
        Assert.Equal(ComponentOverview.Fold($"{doc.Name} {doc.Summary} {ComponentOverview.KeysOf(doc)}"), ComponentOverview.SearchText(doc));

        // the script folds the name, the summary and the keys of a tile, in this order, with the same steps
        var script = File.ReadAllText(TestPaths.Web("wwwroot/js/components.js"));
        Assert.Contains("normalize('NFD').replace(/[\\u0300-\\u036f]/g, '').toLowerCase()", script);
        Assert.Contains("name ? name.textContent : '', summary ? summary.textContent : '', tile.getAttribute('data-keys') || ''", script);
        Assert.Contains("fold(input ? input.value : '').split(/\\s+/)", script);
    }

    // ---------------------------------------------------------------------------------------------------- the order of the pages

    [Fact]
    public void The_pages_are_walked_in_the_order_of_the_catalogue_every_component_once_and_a_group_leads_into_the_next()
    {
        var sequence = ComponentPaging.Sequence(Components, Section);

        Assert.Equal(Components.Count, sequence.Count);
        Assert.Equal(sequence.Count, sequence.Select(c => c.Id).Distinct().Count());
        Assert.Equal(Components.Select(c => c.Id).Order(), sequence.Select(c => c.Id).Order());
        // groups in the order of the page, none twice
        var groups = sequence.Select(c => c.Group).Distinct().ToList();
        Assert.Equal(Section.Groups, groups);
        Assert.Equal(sequence.Select(c => Array.IndexOf([.. Section.Groups], c.Group)), sequence.Select(c => Array.IndexOf([.. Section.Groups], c.Group)).Order());

        for (var i = 0; i < sequence.Count; i++)
        {
            var around = ComponentPaging.Around(sequence, sequence[i].Id)!;
            Assert.Equal(i + 1, around.Position);
            Assert.Equal(sequence.Count, around.Count);
            Assert.Equal(i > 0 ? sequence[i - 1].Id : null, around.Previous?.Id);
            Assert.Equal(i < sequence.Count - 1 ? sequence[i + 1].Id : null, around.Next?.Id);
            Assert.Equal(sequence.Where(c => c.Group == sequence[i].Group).Select(c => c.Id), around.InGroup.Select(c => c.Id));
            Assert.Equal(around.Previous is not null && around.Previous.Group != sequence[i].Group, around.PreviousIsOtherGroup);
            Assert.Equal(around.Next is not null && around.Next.Group != sequence[i].Group, around.NextIsOtherGroup);
        }

        // no wrap-around
        Assert.Null(ComponentPaging.Around(sequence, sequence[0].Id)!.Previous);
        Assert.Null(ComponentPaging.Around(sequence, sequence[^1].Id)!.Next);
        Assert.Null(ComponentPaging.Around(sequence, "nothing"));
        Assert.Equal("buttons", ComponentPaging.Around(sequence, "BUTTONS")!.Current.Id);
    }

    // ---------------------------------------------------------------------------------------------------- the page of a component

    [Fact]
    public async Task Every_component_has_a_page_with_one_h1_a_title_of_its_own_its_examples_and_ids_that_are_unique()
    {
        var problems = new List<string>();
        foreach (var doc in Components)
        {
            var url = $"/Components/{doc.Id}";
            var response = await _client.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                problems.Add($"{url}: {(int)response.StatusCode}");
                continue;
            }

            var html = await response.Content.ReadAsStringAsync();
            var name = WebUtility.HtmlEncode(doc.Name);
            if (Heading().Matches(html).Count != 1) { problems.Add($"{url}: {Heading().Matches(html).Count} h1"); }
            if (!html.Contains($"<h1>{name}</h1>", StringComparison.Ordinal)) { problems.Add($"{url}: the h1 is not the name"); }
            if (WebUtility.HtmlDecode(Title().Match(html).Groups[1].Value).Trim() != $"{doc.Name} · Components · MatDesign") { problems.Add($"{url}: title '{Title().Match(html).Groups[1].Value}'"); }
            if (!html.Contains("class=\"mat-crumbs\"") || !html.Contains($"<li aria-current=\"page\">{name}</li>")) { problems.Add($"{url}: the breadcrumb does not end with the name"); }
            if (!html.Contains($"<li><a href=\"/Components\">Components</a></li>") || !html.Contains($"<li><a href=\"/Components#{ComponentCatalog.GroupAnchor(doc.Group)}\">{doc.Group}</a></li>")) { problems.Add($"{url}: the breadcrumb lacks Components or the group"); }
            if (!html.Contains($"<div class=\"doc-examples\" id=\"{doc.Id}\">")) { problems.Add($"{url}: no element with the id of the component around the examples"); }
            var examples = Regex.Matches(html, "<div class=\"doc-example\" role=\"group\"").Count;
            if (examples != doc.Examples.Count) { problems.Add($"{url}: {examples} examples, the component has {doc.Examples.Count}"); }
            for (var i = 1; i <= doc.Examples.Count; i++)
            {
                if (!html.Contains($"<h3 id=\"ex-{doc.Id}-{i}\">") || !html.Contains($"id=\"src-{doc.Id}-{i}\"")) { problems.Add($"{url}: example {i} is not drawn with its code"); }
            }

            if (html.Contains("<section class=\"doc-component ", StringComparison.Ordinal)) { problems.Add($"{url}: another component's section is on the page"); }
            if (InlineStyle().IsMatch(html)) { problems.Add($"{url}: an inline style (CODE-02)"); }
            var ids = IdAttribute().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
            var twice = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (twice.Count > 0) { problems.Add($"{url}: ids twice: {string.Join(", ", twice)}"); }
            var known = ids.ToHashSet();
            var lost = FragmentLink().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Where(f => !known.Contains(f)).Distinct().ToList();
            if (lost.Count > 0) { problems.Add($"{url}: links to sections that are not there: {string.Join(", ", lost)}"); }
            if (!html.Contains("<script src=\"/js/doc.js\"></script>") || !html.Contains("<script src=\"/js/components.js\"></script>")) { problems.Add($"{url}: the scripts of the page are missing"); }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task The_page_of_a_component_has_Previous_and_Next_that_name_its_neighbours_and_the_group_where_it_is_another_one()
    {
        var sequence = ComponentPaging.Sequence(Components, Section);
        var problems = new List<string>();
        foreach (var doc in sequence)
        {
            var html = await Page($"/Components/{doc.Id}");
            var around = ComponentPaging.Around(sequence, doc.Id)!;
            // the examples of the pager have their own Previous and Next: only the two navs of the page count
            var navs = string.Concat(Regex.Matches(html, "<nav class=\"doc-pager[^>]*>.*?</nav>", RegexOptions.Singleline).Select(m => m.Value));
            var prev = Regex.Matches(navs, "<a [^>]*rel=\"prev\"[^>]*>").ToList();
            var next = Regex.Matches(navs, "<a [^>]*rel=\"next\"[^>]*>").ToList();
            // the pager is drawn twice: under the title and under the content
            if (prev.Count != (around.Previous is null ? 0 : 2)) { problems.Add($"{doc.Id}: {prev.Count} Previous"); }
            if (next.Count != (around.Next is null ? 0 : 2)) { problems.Add($"{doc.Id}: {next.Count} Next"); }
            if (around.Previous is not null && !prev.All(p => p.Value.Contains($"href=\"/Components/{around.Previous.Id}\"") && p.Value.Contains("aria-keyshortcuts=\"ArrowLeft\"") && p.Value.Contains($"aria-label=\"Previous component: {WebUtility.HtmlEncode(around.Previous.Name)}"))) { problems.Add($"{doc.Id}: Previous does not lead to {around.Previous.Id}"); }
            if (around.Next is not null && !next.All(p => p.Value.Contains($"href=\"/Components/{around.Next.Id}\"") && p.Value.Contains("aria-keyshortcuts=\"ArrowRight\"") && p.Value.Contains($"aria-label=\"Next component: {WebUtility.HtmlEncode(around.Next.Name)}"))) { problems.Add($"{doc.Id}: Next does not lead to {around.Next.Id}"); }
            if (around.NextIsOtherGroup && !html.Contains($", in {around.Next!.Group}\"")) { problems.Add($"{doc.Id}: Next does not say that it is in {around.Next.Group}"); }
            if (!html.Contains($">{around.Position} of {around.Count}<")) { problems.Add($"{doc.Id}: no position"); }
            if (Regex.Matches(html, "data-components-back").Count < 3) { problems.Add($"{doc.Id}: no way back to the overview"); }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task The_page_of_a_component_lists_its_group_to_jump_and_marks_the_current_one()
    {
        var html = await Page("/Components/date-picker");
        var group = Components.Where(c => c.Group == "Forms").ToList();

        Assert.Equal(2, Regex.Matches(html, "<nav class=\"mat-toc doc-toc-group\" aria-label=\"In Forms\">").Count);
        foreach (var doc in group)
        {
            Assert.Equal(2, Regex.Matches(html, $"mat-toc__link( is-active)?\" href=\"/Components/{doc.Id}\"").Count);
        }

        Assert.Equal(2, Regex.Matches(html, "mat-toc__link is-active\" href=\"/Components/date-picker\" aria-current=\"page\"").Count);
        Assert.Equal(2, Regex.Matches(html, "<nav class=\"mat-toc\" aria-label=\"On this page\" data-scrollspy>").Count);
        foreach (var section in new[] { "toc-examples", "toc-how", "toc-rules" })
        {
            Assert.Equal(2, Regex.Matches(html, $"href=\"#{section}\"").Count);
            Assert.Contains($"id=\"{section}\"", html);
        }

        Assert.Contains("data-dialog-open=\"toc-dialog\"", html);
    }

    [Fact]
    public async Task The_rules_a_component_follows_are_links_with_their_title_level_and_do_and_do_not()
    {
        var html = await Page("/Components/tree");
        var rule = RuleBook.Get("LIST-17")!;

        Assert.Contains("id=\"toc-rules-title\">Rules it follows</h2>", html);
        Assert.Contains($"<a class=\"doc-rule-id\" href=\"/Rules/Detail?id=LIST-17\">LIST-17</a>", html);
        Assert.Contains($"<a class=\"doc-rulecard__title\" href=\"/Rules/Detail?id=LIST-17\">{rule.Title}</a>", html);
        Assert.Contains("<span class=\"mat-pill ", html);
        Assert.Contains("<div class=\"doc-do\"><strong>Do</strong>", html);
        Assert.Contains("<div class=\"doc-dont\"><strong>Do not</strong>", html);
        Assert.Contains(WebUtility.HtmlEncode(rule.Dont!), html);
        foreach (var id in ComponentCatalog.Get("tree")!.Rules)
        {
            Assert.Contains($"<a class=\"doc-rule-id\" href=\"/Rules/Detail?id={id}\">{id}</a>", html);
        }
    }

    [Fact]
    public async Task How_to_use_lists_the_classes_the_attributes_the_behaviour_and_the_properties_and_leaves_out_what_a_component_has_not()
    {
        var buttons = await Page("/Components/buttons");
        var doc = ComponentCatalog.Get("buttons")!;

        Assert.Contains("id=\"toc-how-title\">How to use</h2>", buttons);
        Assert.Contains("<dl class=\"doc-api doc-api--full\">", buttons);
        Assert.All(ComponentApi.Classes(doc), c => Assert.Contains($"<code>{c}</code>", buttons));
        Assert.All(ComponentApi.Attributes(doc), a => Assert.Contains($"<code>{a}</code>", buttons));
        Assert.DoesNotContain("doc-api__extra", buttons);
        Assert.DoesNotContain("data-doc-api-more", buttons);
        Assert.All(doc.Behaviour, line => Assert.Contains("<li>", buttons));
        Assert.Contains("<h3>How it behaves</h3>", buttons);
        Assert.Contains("id=\"toc-properties\"", buttons);
        Assert.Contains("--mat-btn-radius", buttons);
        Assert.Contains("href=\"/Rules/Detail?id=CODE-03\"", buttons);
        Assert.Contains("docs/ADOPTING.md", buttons);

        var meter = await Page("/Components/meter");
        Assert.DoesNotContain("toc-properties", meter);
        Assert.DoesNotContain("--mat-meter", meter);
    }

    [Fact]
    public async Task A_component_that_a_page_uses_has_a_button_and_a_panel_that_show_its_group_in_a_real_page()
    {
        var html = await Page("/Components/table");

        Assert.Contains("data-context-show=\"group-lists-and-data\" data-context-component=\"table\"", html);
        Assert.Contains("<details class=\"doc-context\" id=\"context-group-lists-and-data\" data-context data-context-view=\"list\">", html);
        Assert.Contains("data-src=\"/Preview?app=matdesign&amp;view=list\"", html);
        Assert.DoesNotContain("<iframe class=\"doc-frame\" src=", html);
        Assert.Contains("href=\"#toc-page\"", html);

        // a component that no sample page uses has the panel but no button
        var range = await Page("/Components/range");
        Assert.DoesNotContain("data-context-show", range);
        Assert.Contains("id=\"context-group-forms\"", range);
    }

    [Fact]
    public async Task An_id_that_is_no_component_is_the_404_of_the_site_with_a_way_back_and_other_cases_and_pages_are_sent_on()
    {
        var missing = await _client.GetAsync("/Components/nothing-like-it");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var html = await missing.Content.ReadAsStringAsync();
        Assert.Contains("This component does not exist", html);
        Assert.Contains("<a class=\"mat-btn mat-btn--primary\" href=\"/Components\">", html);
        Assert.Contains("class=\"mat-nav\"", html);
        Assert.Single(Heading().Matches(html));
        Assert.Contains("<li aria-current=\"page\">Not found</li>", html);

        // one address for a component
        var upper = await _client.GetAsync("/Components/Buttons");
        Assert.Equal(HttpStatusCode.MovedPermanently, upper.StatusCode);
        Assert.Equal("/Components/buttons", upper.Headers.Location!.OriginalString);

        // the layout and the utilities stay one page each: their components have no page of their own
        var layout = await _client.GetAsync("/Components/breakpoints");
        Assert.Equal(HttpStatusCode.Redirect, layout.StatusCode);
        Assert.Equal("/Layout#breakpoints", layout.Headers.Location!.OriginalString);
        Assert.Equal("/Utilities#u-spacing", (await _client.GetAsync("/Components/u-spacing")).Headers.Location!.OriginalString);

        // "all" is the page of everything, not a component
        Assert.Contains("id=\"group-actions\"", await Page("/Components/All"));
        Assert.Contains("id=\"group-actions\"", await Page("/Components/all"));
    }

    // ---------------------------------------------------------------------------------------------------- the page of everything

    [Fact]
    public async Task The_page_of_everything_has_every_component_on_one_page_and_no_content_visibility_that_was_tried_and_left_out()
    {
        var html = await Page("/Components/All");

        Assert.Single(Heading().Matches(html));
        foreach (var doc in Components)
        {
            Assert.Contains($"<section class=\"doc-component mat-anchor\" id=\"{doc.Id}\"", html);
        }

        Assert.Equal(Components.Sum(c => c.Examples.Count), Regex.Matches(html, "<div class=\"doc-example__body[ \"]").Count);
        Assert.Contains("<div class=\"doc-components\">", html);
        Assert.Contains("href=\"/Components\"", html);

        // content-visibility was tried on the sections of the components (layout containment makes a section the frame of the fixed panel of a picker, and the :focus-within that undoes it
        // crashes the renderer) and on the code blocks (the page jumps after a scroll, so that a tap lands beside its target): docs/ARCHITECTURE.md, "Components pages", says what was measured
        var css = File.ReadAllText(TestPaths.Web("wwwroot/css/site.css"));
        Assert.DoesNotContain("content-visibility", css);
        Assert.DoesNotContain("doc-components--lazy", html);
        Assert.DoesNotContain("!important", css[css.IndexOf("The components pages (LIST-18", StringComparison.Ordinal)..]);
    }

    // ---------------------------------------------------------------------------------------------------- the menu and the old addresses

    [Theory]
    [InlineData("/Components")]
    [InlineData("/Components/All")]
    [InlineData("/Components/buttons")]
    [InlineData("/Components/slot-picker")]
    public async Task The_menu_item_Components_is_the_current_page_on_the_overview_and_on_every_page_of_the_components(string url)
    {
        var html = await Page(url);

        Assert.Matches(@"<a class=""mat-nav__item is-active"" href=""/Components"" aria-current=""page"">", html);
        Assert.DoesNotMatch(@"<a class=""mat-nav__item is-active"" href=""/(Layout|Utilities|Shell)""", html);
    }

    [Fact]
    public async Task No_page_links_to_the_old_address_with_a_hash_and_the_forms_of_the_examples_go_to_the_page_of_their_component()
    {
        var urls = new List<string> { "/", "/Rules", "/Shell", "/Patterns", "/Download", "/Layout", "/Utilities", "/Components", "/Components/All", "/Components?q=picker" };
        urls.AddRange(Components.Select(c => $"/Components/{c.Id}"));
        urls.AddRange(RuleBook.All.Take(40).Select(r => $"/Rules/Detail?id={r.Id}"));
        var old = new Regex(@"(?:href|action)=""/Components#(?!group-)[^""]*""");
        var problems = new List<string>();
        foreach (var url in urls)
        {
            var html = await Page(url);
            problems.AddRange(old.Matches(html).Select(m => $"{url}: {m.Value}"));
        }

        // the markup of the examples is written in the catalogue: a form that was sent to /Components#id now goes to /Components/id
        var catalogue = ComponentCatalog.All.SelectMany(c => c.Examples.Select(e => e.Html));
        problems.AddRange(catalogue.SelectMany(h => old.Matches(h)).Select(m => "catalogue: " + m.Value));
        foreach (var doc in Components)
        {
            foreach (Match action in Regex.Matches(string.Concat(doc.Examples.Select(e => e.Html)), @"action=""/Components/([^""?#]*)"""))
            {
                if (ComponentCatalog.Get(action.Groups[1].Value) is null) { problems.Add($"{doc.Id}: a form goes to /Components/{action.Groups[1].Value}, which is no component"); }
            }
        }

        // and the sources, the scripts and the documentation say nothing of it either (a hash that is a group of the overview is fine)
        var sources = Directory.EnumerateFiles(TestPaths.Web(string.Empty), "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && (f.EndsWith(".cs") || f.EndsWith(".cshtml") || f.EndsWith(".js")))
            .Concat(Directory.EnumerateFiles(Path.Combine(TestPaths.RepoRoot, "docs"), "*.md"))
            .Append(Path.Combine(TestPaths.RepoRoot, "README.md"))
            .Append(Path.Combine(TestPaths.RepoRoot, "CLAUDE.md"));
        foreach (var file in sources)
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"(?<![A-Za-z])/Components#(?!group-)[A-Za-z0-9_@{-]*"))
            {
                problems.Add($"{Path.GetRelativePath(TestPaths.RepoRoot, file)}: {m.Value}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct()));
    }

    [Fact]
    public void The_script_sends_the_old_hash_addresses_on_walks_with_the_arrow_keys_and_goes_back_through_the_history()
    {
        var script = File.ReadAllText(TestPaths.Web("wwwroot/js/components.js"));

        // /Components#buttons, and the id of a heading, an example or a source of a component, is its page; #group-... is left to the browser
        Assert.Contains("window.location.replace('/Components/' + encodeURIComponent(id))", script);
        Assert.Contains("/^(?:h|props)-(.+)$/", script);
        Assert.Contains("/^(?:ex|src)-(.+)-\\d+$/", script);
        Assert.Contains("window.addEventListener('hashchange', forward)", script);
        // the keys: the arrows only, no modifier, not when the toolkit has taken the key, not in a dialog, a field or a control of an example
        Assert.Contains("e.key !== 'ArrowLeft' && e.key !== 'ArrowRight'", script);
        Assert.Contains("e.defaultPrevented || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey", script);
        Assert.Contains("document.querySelector('dialog[open]')", script);
        foreach (var guard in new[] { "input, textarea, select, [contenteditable]", ".doc-example__live", ".mat-picker", "[role=\"tree\"]", ".mat-schedule", ".mat-menu", "pre" })
        {
            Assert.Contains(guard, script);
        }

        // walking replaces the entry of the history, so that "All components" is one step back; the entry before is read from the history, not kept anywhere
        Assert.Contains("window.location.replace(href)", script);
        Assert.Contains("window.history.back()", script);
        Assert.Contains("nav.entries()", script);
        Assert.DoesNotContain("localStorage", script);
        Assert.DoesNotContain("sessionStorage", script);
        Assert.DoesNotContain("innerHTML", script);
        Assert.DoesNotContain("eval(", script);
        // the search stays in the address with replaceState, so that the browser shows the overview as it was left
        Assert.Contains("window.history.replaceState(", script);
    }

    [Fact]
    public void The_id_all_is_not_a_component_because_it_is_the_page_of_everything_and_the_ids_of_the_page_stay_reserved()
    {
        var toolkit = (MatDesign.Web.Services.ToolkitFiles)factory.Services.GetService(typeof(MatDesign.Web.Services.ToolkitFiles))!;
        ComponentDoc Doc(string id) => MatDesign.Web.Studio.ComponentValidator.Normalize(new ComponentDoc(id, "Hand " + id, "Content", "toggle", "A summary.", ["BUTTON-01"], [new ComponentExample("Example", "<span class=\"mat-pill\">x</span>")], []));
        IEnumerable<string> Problems(string id) => MatDesign.Web.Studio.ComponentValidator.Validate(Doc(id), ComponentCatalog.All, rule => RuleBook.Get(rule) is not null, toolkit.Facts).Where(p => p.Field == "Id").Select(p => p.Message);

        Assert.NotEmpty(Problems("all"));
        Assert.NotEmpty(Problems("toc-examples"));
        Assert.NotEmpty(Problems("group-forms"));
        Assert.Empty(Problems("a-new-one"));
    }

    // ---------------------------------------------------------------------------------------------------- the rule

    [Fact]
    public void The_rule_LIST_18_says_that_a_catalogue_is_an_overview_and_a_page_per_item()
    {
        var rule = RuleBook.Get("LIST-18");

        Assert.NotNull(rule);
        Assert.Equal("LIST", rule.Area);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.InRange(rule.Statement.Length, 10, 1200);
        foreach (var word in new[] { "overview", "one page per item", "tile", "arrow keys", "never every item on one page" })
        {
            Assert.Contains(word, rule.Statement);
        }

        Assert.StartsWith("Test", rule.Check);
        Assert.Contains("doc-tile", rule.Do);
        Assert.NotNull(rule.Dont);
    }

    // ---------------------------------------------------------------------------------------------------- the budgets

    // What the pages weighed when the budgets were set (docs/ARCHITECTURE.md, "Components pages"): the overview 93,134 bytes with 1,463 elements, the largest page of a component (itemlist-contents)
    // 277,938 bytes with 5,727 elements; the components page before was 3,183,876 bytes with 68,881 elements. A budget is the measurement and a fifth more, room for a few components and a few
    // words: when one is reached, look at the page before raising it.
    private const int OverviewBytes = 110_000;
    private const int OverviewElements = 1_800;
    private const int DetailBytes = 340_000;
    private const int DetailElements = 7_000;

    [Fact]
    public async Task The_overview_stays_within_its_budget_of_bytes_and_elements()
    {
        var html = await Page("/Components");
        var bytes = System.Text.Encoding.UTF8.GetByteCount(html);

        Assert.True(bytes <= OverviewBytes, $"the overview is {bytes} bytes (budget {OverviewBytes})");
        Assert.True(Elements(html) <= OverviewElements, $"the overview has {Elements(html)} elements (budget {OverviewElements})");
    }

    [Fact]
    public async Task Every_page_of_a_component_stays_within_its_budget_and_the_largest_is_far_from_the_page_of_everything()
    {
        var sizes = new List<(string Id, int Bytes, int Elements)>();
        foreach (var doc in Components)
        {
            var html = await Page($"/Components/{doc.Id}");
            sizes.Add((doc.Id, System.Text.Encoding.UTF8.GetByteCount(html), Elements(html)));
        }

        var over = sizes.Where(s => s.Bytes > DetailBytes || s.Elements > DetailElements).Select(s => $"{s.Id}: {s.Bytes} bytes, {s.Elements} elements").ToList();
        Assert.True(over.Count == 0, $"over the budget of {DetailBytes} bytes and {DetailElements} elements:\n" + string.Join("\n", over));

        var all = await Page("/Components/All");
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(all) > 5 * sizes.Max(s => s.Bytes), "the page of everything is the whole catalogue: a page of one component must stay a fraction of it");
    }
}
