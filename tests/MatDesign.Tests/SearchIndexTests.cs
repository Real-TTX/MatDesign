using System.Diagnostics;
using System.Net;
using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MatDesign.Tests;

/// <summary>The search index (docs/ARCHITECTURE.md, "Search"): what is in it, how a query is matched and ranked, the synonyms, the typos, the rebuild, the speed.</summary>
public sealed class SearchIndexTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private SearchIndex Index => factory.Services.GetRequiredService<SearchIndex>();

    private DesignData Design => factory.Services.GetRequiredService<DesignData>();

    private (SearchKind Kind, string Id) First(string query)
    {
        var hit = Index.Query(query).Hits.FirstOrDefault();
        Assert.True(hit is not null, $"'{query}' finds nothing");
        return (hit.Doc.Kind, hit.Doc.Id);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------ what is in the index

    [Fact]
    public void Every_rule_component_pattern_app_page_token_and_icon_is_a_document()
    {
        var counts = Index.Counts;
        var navigation = Navigation.For(Design.NavFacts, studio: false).SelectMany(s => s.Pages).Select(p => p.Href).Distinct().Count();
        var toolkit = factory.Services.GetRequiredService<ToolkitFiles>();

        Assert.Equal(Design.Rules.Count, counts[SearchKind.Rule]);
        Assert.Equal(Design.Components.Count, counts[SearchKind.Component]);
        Assert.Equal(PatternCatalog.All.Count, counts[SearchKind.Pattern]);
        Assert.Equal(Design.Apps.Count, counts[SearchKind.App]);
        Assert.Equal(navigation, counts[SearchKind.Page]);
        Assert.Equal(toolkit.Tokens.Count + toolkit.Properties.Count, counts[SearchKind.Token]);
        Assert.True(counts[SearchKind.Icon] >= toolkit.IconNames.Count + 1000, $"{counts[SearchKind.Icon]} icons: the sprite and the library");
        Assert.True(Index.VocabularySize > 3000);
    }

    [Fact]
    public void The_pages_of_the_studio_and_its_content_are_not_searched()
    {
        Assert.DoesNotContain(Index.Documents, d => d.Href.StartsWith("/Studio", StringComparison.Ordinal));
        Assert.DoesNotContain(Index.Documents, d => d.Kind == SearchKind.Page && d.Title is "Checks" or "Releases" or "People" or "Activity");
        Assert.DoesNotContain(Index.Query("Releases").Hits, h => h.Doc.Href.StartsWith("/Studio", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_document_has_a_title_a_line_an_address_in_the_site_an_icon_of_the_sprite_and_a_group()
    {
        var sprite = factory.Services.GetRequiredService<ToolkitFiles>().IconNames.ToHashSet();
        foreach (var doc in Index.Documents)
        {
            Assert.False(string.IsNullOrWhiteSpace(doc.Title), doc.Id);
            Assert.False(string.IsNullOrWhiteSpace(doc.Text), doc.Id + " has no line");
            Assert.False(string.IsNullOrWhiteSpace(doc.Group), doc.Id);
            Assert.StartsWith("/", doc.Href);
            Assert.DoesNotContain("//", doc.Href);
            Assert.Contains(doc.Icon, sprite);
            Assert.True(doc.Text.Length <= 220, doc.Id + ": the line is too long for a suggestion");
        }

        Assert.Equal(Index.Documents.Count, Index.Documents.Select(d => (d.Kind, d.Id)).Distinct().Count());
    }

    [Fact]
    public void A_component_leads_to_its_own_page_and_the_other_kinds_to_pages_that_exist()
    {
        // the page of a component (/Components/{id}) is built by another change: the address is held by SearchLinks, the rest is looked up
        Assert.Equal("/Components/file-upload", SearchLinks.Component("file-upload"));
        Assert.Equal("/Components/a%20b", SearchLinks.Component("a b"));
        Assert.All(Index.Documents.Where(d => d.Kind == SearchKind.Component), d => Assert.Equal(SearchLinks.Component(d.Id), d.Href));
        Assert.All(Index.Documents.Where(d => d.Kind == SearchKind.Rule), d => Assert.Equal(SearchLinks.Rule(d.Id), d.Href));
    }

    [Fact]
    public async Task The_addresses_of_rules_pages_apps_patterns_tokens_and_icons_answer()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var sample = Index.Documents.Where(d => d.Kind != SearchKind.Component)
            .GroupBy(d => d.Kind)
            .SelectMany(g => g.Key is SearchKind.Page or SearchKind.App or SearchKind.Pattern ? g : g.Where((_, i) => i % 23 == 0))
            .ToList();
        Assert.True(sample.Count > 60);
        foreach (var doc in sample)
        {
            var response = await client.GetAsync(doc.Href);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{doc.Kind} {doc.Id}: {doc.Href} answers {(int)response.StatusCode}");
        }
    }

    [Fact]
    public void A_rule_is_shown_with_its_id_and_searched_by_its_title_and_its_id_separately()
    {
        var rule = Index.Documents.Single(d => d is { Kind: SearchKind.Rule, Id: "FORM-21" });
        Assert.Equal("FORM-21 File upload", rule.Title);
        Assert.StartsWith("Forms · Should · ", rule.Text);
        // the component of that name comes before the rule of that name, both are found
        var hits = Index.Query("file upload").Hits.Select(h => (h.Doc.Kind, h.Doc.Id)).ToList();
        Assert.True(hits.IndexOf((SearchKind.Component, "file-upload")) < hits.IndexOf((SearchKind.Rule, "FORM-21")));
        Assert.Contains((SearchKind.Rule, "FORM-21"), hits.Take(3));
    }

    [Fact]
    public void A_token_is_looked_for_without_its_prefix_and_its_whole_name_is_its_id()
    {
        var accent = Index.Documents.Single(d => d is { Kind: SearchKind.Token, Id: "--mat-accent" });
        Assert.Equal("--mat-accent", accent.Title);
        Assert.Equal("/Foundations/Tokens?q=--mat-accent", accent.Href);
        Assert.Equal((SearchKind.Token, "--mat-accent"), First("accent"));
        Assert.Equal((SearchKind.Token, "--mat-accent"), First("--mat-accent"));
        Assert.Equal((SearchKind.Token, "--mat-accent"), First("mat accent"));
        Assert.Contains(Index.Documents, d => d is { Kind: SearchKind.Token, Id: "--mat-btn-radius" });
    }

    [Fact]
    public void The_icons_of_the_library_that_the_sprite_has_too_are_one_document_with_the_tags_of_the_library()
    {
        var home = Assert.Single(Index.Documents, d => d is { Kind: SearchKind.Icon, Id: "home" });
        Assert.StartsWith("/Foundations/Icons?q=", home.Href);
        var library = Index.Documents.First(d => d is { Kind: SearchKind.Icon } && d.Href.StartsWith("/Foundations/IconLibrary", StringComparison.Ordinal));
        Assert.Equal("image", library.Icon);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------ how a query is matched

    [Fact]
    public void A_query_is_trimmed_collapsed_cut_and_a_text_without_a_word_finds_nothing()
    {
        Assert.Equal("a b", SearchIndex.Normalise("  a \t\n  b  "));
        Assert.Equal(SearchIndex.MaxQueryLength, SearchIndex.Normalise(new string('x', 500)).Length);
        Assert.Equal(string.Empty, SearchIndex.Normalise(null));
        Assert.Empty(Index.Query("   ").Hits);
        Assert.Empty(Index.Query("--- !!!").Hits);
        Assert.Empty(Index.Query(null).Hits);
        Assert.Same(SearchResults.Empty, Index.Query(string.Empty));
    }

    [Fact]
    public void Every_word_of_the_query_has_to_be_found_and_a_word_that_is_nowhere_finds_nothing()
    {
        var upload = Index.Query("upload").Total;
        var fileUpload = Index.Query("file upload").Total;
        Assert.True(fileUpload > 0 && fileUpload <= upload);
        Assert.Empty(Index.Query("file upload zzzzzzzz").Hits);
        Assert.Empty(Index.Query("zzzzzzzz").Hits);
        // two words that no document has together find nothing, although each is found alone
        Assert.NotEmpty(Index.Query("tooltip").Hits);
        Assert.NotEmpty(Index.Query("meter").Hits);
        Assert.Empty(Index.Query("tooltip meter pager carousel").Hits);
    }

    [Fact]
    public void Little_words_need_not_be_found_but_count_when_they_are()
    {
        // "dark mode" finds what is dark whether or not it says mode; a query of nothing else has to find its words
        var darkMode = Index.Query("dark mode").Hits.Select(h => h.Doc.Id).ToList();
        Assert.Contains("COLOR-02", darkMode);
        Assert.True(darkMode.Count >= Index.Query("dark").Total / 2);
        Assert.Equal("file-upload", Index.Query("how do i upload a file").Hits.First(h => h.Doc.Kind == SearchKind.Component).Doc.Id);
        // a query of nothing but little words has to find them
        Assert.NotEmpty(Index.Query("the and of").Hits);
    }

    [Theory]
    [InlineData("FORM-15", "FORM-15")]
    [InlineData("form-15", "FORM-15")]
    [InlineData("form 15", "FORM-15")]
    [InlineData("FORM 15", "FORM-15")]
    [InlineData("form15", "FORM-15")]
    [InlineData("  Form_15 ", "FORM-15")]
    [InlineData("FORM-5", "FORM-05")]
    [InlineData("shell-1", "SHELL-01")]
    [InlineData("a11y-3", "A11Y-03")]
    [InlineData("a11y 03", "A11Y-03")]
    public void The_exact_id_of_a_rule_is_the_first_hit_however_it_is_written(string query, string expected)
        => Assert.Equal((SearchKind.Rule, expected), First(query));

    [Fact]
    public void An_id_that_names_no_rule_is_an_ordinary_query_and_a_rule_is_found_when_another_word_is_not()
    {
        Assert.NotEqual((SearchKind.Rule, "FORM-99"), Index.Query("form-99").Hits.Select(h => (h.Doc.Kind, h.Doc.Id)).FirstOrDefault());
        // the id of the rule is the answer even when the second word is not in it
        Assert.Equal((SearchKind.Rule, "FORM-15"), First("form-15"));
        Assert.Contains(Index.Query("form 15 pickers").Hits, h => h.Doc.Id == "FORM-15");
    }

    [Fact]
    public void A_word_in_the_plural_and_in_the_singular_find_the_same_thing()
    {
        Assert.Equal(Index.Query("button").Hits.Take(5).Select(h => h.Doc.Id), Index.Query("buttons").Hits.Take(5).Select(h => h.Doc.Id));
        Assert.Equal((SearchKind.Component, "tooltip"), First("tooltips"));
        Assert.Equal((SearchKind.Component, "toasts"), First("toast"));
        Assert.Equal((SearchKind.Component, "toasts"), First("toasts"));
        Assert.Equal((SearchKind.Component, "cards"), First("card"));
    }

    [Fact]
    public void A_word_that_is_being_typed_finds_what_starts_with_it()
    {
        Assert.Equal((SearchKind.Component, "dropdown"), First("dropd"));
        Assert.Equal((SearchKind.Component, "file-upload"), First("file up"));
        Assert.Equal((SearchKind.Component, "tooltip"), First("toolt"));
        Assert.Equal((SearchKind.Component, "calendar"), First("calen"));
        Assert.NotEmpty(Index.Query("a").Hits);
    }

    [Theory]
    [InlineData("buton", SearchKind.Component, "buttons")]
    [InlineData("buttn", SearchKind.Component, "buttons")]
    [InlineData("dropdwon", SearchKind.Component, "dropdown")]
    [InlineData("tooltp", SearchKind.Component, "tooltip")]
    [InlineData("calender", SearchKind.Component, "calendar")]
    [InlineData("typografy", SearchKind.Component, "typography")]
    [InlineData("toogle", SearchKind.Component, "choices")]
    [InlineData("breadcrum", SearchKind.Component, "breadcrumb")]
    public void A_typo_of_a_long_enough_word_finds_the_thing_one_letter_off_two_for_nine_letters_and_more(string query, SearchKind kind, string id)
    {
        var hits = Index.Query(query).Hits.Take(5).Select(h => (h.Doc.Kind, h.Doc.Id)).ToList();
        Assert.Contains((kind, id), hits);
    }

    [Fact]
    public void A_short_word_is_not_guessed_at_and_a_word_that_something_starts_needs_no_guess()
    {
        // four letters: tabz is not tabs
        Assert.DoesNotContain(Index.Query("tabz").Hits.Take(5), h => h.Doc.Id == "tabs");
        // buttonz is five letters over: one off. butz is not
        Assert.DoesNotContain(Index.Query("butz").Hits.Take(5), h => h.Doc.Id == "buttons");
        // a typo is looked for only where nothing is the word or starts with it
        Assert.DoesNotContain(Index.Query("tab").Hits.Take(3), h => h.Doc.Id == "tabs" && h.Score <= 0);
    }

    [Theory]
    [InlineData("häkchen")]
    [InlineData("HÄKCHEN")]
    [InlineData("hakchen")]
    [InlineData("Hakchen")]
    [InlineData("haekchen")]
    [InlineData("kontrollkästchen")]
    [InlineData("kontrollkaestchen")]
    [InlineData("kontrollkastchen")]
    public void Umlauts_accents_and_their_spellings_with_two_letters_find_the_same_checks(string query)
    {
        var ids = Index.Query(query).Hits.Take(8).Select(h => h.Doc.Id).ToList();
        Assert.Contains("checks", ids);
        Assert.Contains("choices", ids);
    }

    [Fact]
    public void The_same_text_with_different_case_and_accents_gives_the_same_hits()
    {
        var a = Index.Query("Schaltfläche").Hits.Select(h => h.Doc.Id);
        var b = Index.Query("schaltflache").Hits.Select(h => h.Doc.Id);
        var c = Index.Query("SCHALTFLÄCHE").Hits.Select(h => h.Doc.Id);
        Assert.Equal(a, b);
        Assert.Equal(a, c);
        Assert.Equal("buttons", Index.Query("Schaltfläche").Hits.First(h => h.Doc.Kind == SearchKind.Component).Doc.Id);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------ the synonyms, in English and in German

    [Theory]
    [InlineData("schalter", "choices")]
    [InlineData("toggle", "choices")]
    [InlineData("reiter", "tabs")]
    [InlineData("auswahl", "selects")]
    [InlineData("combobox", "selects")]
    [InlineData("modal", "dialogs")]
    [InlineData("overlay", "dialogs")]
    [InlineData("hinweis", "tooltip")]
    [InlineData("datum", "date-picker")]
    [InlineData("kalender", "calendar")]
    [InlineData("hochladen", "file-upload")]
    [InlineData("datei", "file-upload")]
    [InlineData("bewertung", "rating")]
    [InlineData("sterne", "rating")]
    [InlineData("baum", "tree")]
    [InlineData("verzeichnis", "tree")]
    [InlineData("snackbar", "toasts")]
    [InlineData("meldung", "toasts")]
    [InlineData("knopf", "buttons")]
    [InlineData("tabelle", "table")]
    [InlineData("karte", "cards")]
    [InlineData("farbe", "colour-picker")]
    [InlineData("brotkrumen", "breadcrumb")]
    [InlineData("fußzeile", "footer")]
    [InlineData("kopfzeile", "topbar")]
    [InlineData("schieberegler", "slider")]
    [InlineData("fortschritt", "meter")]
    [InlineData("karussell", "carousel")]
    [InlineData("akkordeon", "accordion")]
    public void A_german_or_other_word_finds_the_component_that_is_named_in_english_among_the_first_few(string query, string id)
    {
        var ids = Index.Query(query).Hits.Take(5).Where(h => h.Doc.Kind == SearchKind.Component).Select(h => h.Doc.Id).ToList();
        Assert.Contains(id, ids);
    }

    [Theory]
    [InlineData("dunkel", "COLOR-02")]
    [InlineData("barrierefrei", "A11Y-03")]
    [InlineData("handy", "MOBILE-01")]
    [InlineData("touch", "MOBILE-04")]
    [InlineData("kontrast", "COLOR-05")]
    public void A_german_word_finds_the_rule_among_the_first_few(string query, string id)
    {
        var ids = Index.Query(query).Hits.Take(8).Select(h => h.Doc.Id).ToList();
        Assert.Contains(id, ids);
    }

    [Fact]
    public void A_word_that_the_thing_is_named_by_beats_a_word_that_only_appears_in_its_text()
    {
        // the page Switches is the page for "toggle", not the icon that is called toggle, and not the page that says toggle somewhere in a sentence
        Assert.Equal((SearchKind.Component, "choices"), First("toggle"));
        Assert.Contains((SearchKind.Icon, "toggle"), Index.Query("toggle").Hits.Take(6).Select(h => (h.Doc.Kind, h.Doc.Id)));
        // and the title that has the word itself beats the title that has a synonym of it
        Assert.Equal((SearchKind.Component, "number-stepper"), First("stepper"));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------ ranking: the first result of a query, by hand

    [Theory]
    [InlineData("button", SearchKind.Component, "buttons")]
    [InlineData("buttons", SearchKind.Component, "buttons")]
    [InlineData("tabs", SearchKind.Component, "tabs")]
    [InlineData("reiter", SearchKind.Component, "tabs")]
    [InlineData("dropdown", SearchKind.Component, "dropdown")]
    [InlineData("auswahl", SearchKind.Component, "selects")]
    [InlineData("switch", SearchKind.Component, "choices")]
    [InlineData("schalter", SearchKind.Component, "choices")]
    [InlineData("modal", SearchKind.Component, "dialogs")]
    [InlineData("dialog", SearchKind.Component, "dialogs")]
    [InlineData("popup", SearchKind.Component, "dialogs")]
    [InlineData("upload", SearchKind.Component, "file-upload")]
    [InlineData("datei hochladen", SearchKind.Component, "file-upload")]
    [InlineData("dateiupload", SearchKind.Component, "file-upload")]
    [InlineData("file upload", SearchKind.Component, "file-upload")]
    [InlineData("kalender", SearchKind.Component, "calendar")]
    [InlineData("rating", SearchKind.Component, "rating")]
    [InlineData("sterne", SearchKind.Component, "rating")]
    [InlineData("tree", SearchKind.Component, "tree")]
    [InlineData("baum", SearchKind.Component, "tree")]
    [InlineData("toast", SearchKind.Component, "toasts")]
    [InlineData("snackbar", SearchKind.Component, "toasts")]
    [InlineData("tooltip", SearchKind.Component, "tooltip")]
    [InlineData("hinweis", SearchKind.Component, "tooltip")]
    [InlineData("table", SearchKind.Component, "table")]
    [InlineData("tabelle", SearchKind.Component, "table")]
    [InlineData("card", SearchKind.Component, "cards")]
    [InlineData("karte", SearchKind.Component, "cards")]
    [InlineData("mat-upload", SearchKind.Component, "file-upload")]
    [InlineData("data-upload", SearchKind.Component, "file-upload")]
    [InlineData("search", SearchKind.Component, "search")]
    [InlineData("suche", SearchKind.Component, "search")]
    [InlineData("topbar", SearchKind.Component, "topbar")]
    [InlineData("header bar", SearchKind.Component, "topbar")]
    [InlineData("breadcrumb", SearchKind.Component, "breadcrumb")]
    [InlineData("pagination", SearchKind.Component, "pager")]
    [InlineData("slider", SearchKind.Component, "slider")]
    [InlineData("stepper", SearchKind.Component, "number-stepper")]
    [InlineData("otp", SearchKind.Component, "code-input")]
    [InlineData("accent", SearchKind.Token, "--mat-accent")]
    [InlineData("akzent", SearchKind.Token, "--mat-accent")]
    [InlineData("--mat-accent", SearchKind.Token, "--mat-accent")]
    [InlineData("font", SearchKind.Token, "--mat-font")]
    [InlineData("schrift", SearchKind.Page, "/Foundations/Type")]
    [InlineData("colour", SearchKind.Component, "colour-picker")]
    [InlineData("colours", SearchKind.Page, "/Foundations")]
    [InlineData("dunkel", SearchKind.Page, "/Foundations")]
    [InlineData("white label", SearchKind.Rule, "CODE-09")]
    [InlineData("form-21", SearchKind.Rule, "FORM-21")]
    [InlineData("download", SearchKind.Page, "/Download")]
    [InlineData("changelog", SearchKind.Page, "/Changelog")]
    [InlineData("builder", SearchKind.Page, "/Builder")]
    [InlineData("palette", SearchKind.Page, "/Identity/Palette")]
    [InlineData("rulebook", SearchKind.Page, "/Rules")]
    [InlineData("shell", SearchKind.Page, "/Shell")]
    [InlineData("icon library", SearchKind.Page, "/Foundations/IconLibrary")]
    [InlineData("matpaper", SearchKind.App, "matpaper")]
    [InlineData("list page", SearchKind.Pattern, "list")]
    [InlineData("edit page", SearchKind.Pattern, "edit")]
    public void The_first_hit_of_a_query_is_the_one_a_person_means(string query, SearchKind kind, string id)
        => Assert.Equal((kind, id), First(query));

    [Fact]
    public void An_icon_is_found_by_its_name_and_by_the_tags_of_the_library()
    {
        Assert.Equal((SearchKind.Icon, "arrow-up"), First("arrow-up"));
        Assert.Contains(Index.Query("arrow").Hits.Take(10), h => h.Doc.Kind == SearchKind.Icon);
        // the tags of the library are what an icon is found by: it need not have the word in its name
        var byTag = Index.Query("lamp").Hits.Where(h => h.Doc.Kind == SearchKind.Icon).Take(5).Select(h => h.Doc.Id).ToList();
        Assert.NotEmpty(byTag);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------ the answer for a popup

    [Fact]
    public void The_popup_gets_the_best_hits_in_groups_the_group_of_the_best_hit_first_and_the_counts_of_all()
    {
        var results = Index.Query("button");
        var groups = results.Top(8);

        Assert.Equal(8, groups.Sum(g => g.Hits.Count));
        Assert.Equal(results.Hits[0].Doc.Kind, groups[0].Kind);
        Assert.All(groups, g => Assert.Equal(results.CountOf(g.Kind), g.Count));
        Assert.All(groups, g => Assert.All(g.Hits, h => Assert.Equal(g.Kind, h.Doc.Kind)));
        Assert.Equal(groups.SelectMany(g => g.Hits.Select(h => h.Doc.Id)).Order(), results.Hits.Take(8).Select(h => h.Doc.Id).Order());
        Assert.Equal("Components", groups[0].Name);
        // the hits of one kind only
        var rules = Index.Suggest("button", 5, SearchKind.Rule);
        Assert.Single(rules);
        Assert.Equal(5, rules[0].Hits.Count);
        Assert.Equal(SearchKind.Rule, rules[0].Kind);
        // the limit holds
        Assert.Equal(1, Index.Suggest("button", 0).Sum(g => g.Hits.Count));
        Assert.Equal(50, Index.Suggest("a", 500).Sum(g => g.Hits.Count));
    }

    [Fact]
    public void The_hits_are_sorted_best_first_and_a_tie_goes_to_the_shorter_title_and_then_to_the_id()
    {
        var hits = Index.Query("icon").Hits;
        for (var i = 1; i < hits.Count; i++)
        {
            Assert.True(hits[i - 1].Score >= hits[i].Score, $"{hits[i - 1].Doc.Id} before {hits[i].Doc.Id}");
            if (hits[i - 1].Score == hits[i].Score)
            {
                Assert.True(hits[i - 1].Doc.Title.Length <= hits[i].Doc.Title.Length);
            }
        }
    }

    [Fact]
    public void A_kind_is_read_from_its_name_or_its_heading_in_any_case()
    {
        Assert.True(SearchKinds.TryParse("rule", out var rule) && rule == SearchKind.Rule);
        Assert.True(SearchKinds.TryParse("Rules", out var rules) && rules == SearchKind.Rule);
        Assert.True(SearchKinds.TryParse(" COMPONENT ", out var component) && component == SearchKind.Component);
        Assert.False(SearchKinds.TryParse("nothing", out _));
        Assert.False(SearchKinds.TryParse(null, out _));
        Assert.All(SearchKinds.All, k => Assert.True(SearchKinds.TryParse(SearchKinds.Name(k), out var back) && back == k));
        Assert.All(SearchKinds.All, k => Assert.Contains(SearchKinds.Icon(k), factory.Services.GetRequiredService<ToolkitFiles>().IconNames));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------ a cache, a rebuild, threads, speed

    [Fact]
    public void The_answer_for_a_text_is_kept_until_the_data_changes_and_the_case_makes_no_difference()
    {
        var a = Index.Query("tooltip");
        Assert.Same(a, Index.Query("tooltip"));
        Assert.Same(a, Index.Query("TOOLTIP"));
        Assert.Same(a, Index.Query("  tooltip  "));
        Assert.NotSame(a, Index.Query("tooltip", useCache: false));
        Assert.Equal(a.Hits.Select(h => h.Doc.Id), Index.Query("tooltip", useCache: false).Hits.Select(h => h.Doc.Id));
    }

    [Fact]
    public void A_change_of_the_data_builds_a_new_snapshot_and_the_old_answers_are_gone()
    {
        var before = Index.Query("tooltip");
        var version = Index.Version;

        Design.Reload();

        Assert.NotEqual(version, Index.Version);
        var after = Index.Query("tooltip");
        Assert.NotSame(before, after);
        Assert.Equal(before.Hits.Select(h => h.Doc.Id), after.Hits.Select(h => h.Doc.Id));
    }

    [Fact]
    public async Task What_the_studio_saves_is_found_and_what_it_deletes_is_gone()
    {
        using var studio = new StudioHarness(factory);
        await studio.SignIn();
        var index = studio.Factory.Services.GetRequiredService<SearchIndex>();
        Assert.Empty(index.Query("zyxwvu").Hits);

        var form = new Dictionary<string, string>
        {
            ["Input.Original"] = string.Empty,
            ["Input.Id"] = "FORM-90",
            ["Input.Area"] = "FORM",
            ["Input.Title"] = "Zyxwvu fields",
            ["Input.Statement"] = "A rule for the search test: the zyxwvu control is written with `mat-zyxwvu`.",
            ["Input.Level"] = "Should",
            ["Input.Source"] = "Studio",
            ["Input.Why"] = string.Empty,
            ["Input.Do"] = string.Empty,
            ["Input.Dont"] = string.Empty,
        };
        Assert.Equal(HttpStatusCode.Redirect, (await studio.Post("/Studio/Rules/Edit", "/Studio/Rules/Edit", form)).StatusCode);

        var found = index.Query("zyxwvu").Hits.Select(h => (h.Doc.Kind, h.Doc.Id)).ToList();
        Assert.Contains((SearchKind.Rule, "FORM-90"), found);
        Assert.Equal((SearchKind.Rule, "FORM-90"), found[0]);
        Assert.Equal("FORM-90", index.Query("form 90").Hits[0].Doc.Id);
        // a page of the studio does not appear in what is found
        Assert.DoesNotContain(index.Documents, d => d.Href.StartsWith("/Studio", StringComparison.Ordinal));

        form["Input.Original"] = "FORM-90";
        var deleted = await studio.Post("/Studio/Rules/Edit?id=FORM-90", "/Studio/Rules/Edit?handler=Delete", form);
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Empty(index.Query("zyxwvu").Hits);
    }

    [Fact]
    public async Task Many_threads_may_ask_while_the_data_changes_and_every_answer_is_a_whole_one()
    {
        var index = Index;
        var design = Design;
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var stop = new CancellationTokenSource();
        var writer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                design.Reload();
                Thread.Sleep(5);
            }
        });
        var readers = Enumerable.Range(0, 8).Select(n => Task.Run(() =>
        {
            for (var i = 0; i < 150; i++)
            {
                try
                {
                    var query = new[] { "button", "schalter", "FORM-15", "dropdwon", "datei hochladen", "accent" }[(i + n) % 6];
                    var hits = index.Query(query).Hits;
                    if (hits.Count == 0 || hits.Select(h => (h.Doc.Kind, h.Doc.Id)).Distinct().Count() != hits.Count)
                    {
                        failures.Add($"'{query}': {hits.Count} hits");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(ex.GetType().Name + ": " + ex.Message);
                }
            }
        })).ToArray();

        await Task.WhenAll(readers);
        await stop.CancelAsync();
        await writer;

        Assert.True(failures.IsEmpty, string.Join("\n", failures.Distinct().Take(5)));
    }

    [Fact]
    public void A_query_takes_under_five_milliseconds_for_the_whole_index()
    {
        var queries = new[]
        {
            "button", "dropdown", "reiter", "FORM-15", "dark mode", "datei hochladen", "schalter", "icon", "x", "accent", "datepicr", "white label",
            "tooltip", "häkchen", "buton", "typografy", "a", "mat", "--mat-btn-radius", "wie ändere ich die farbe der schaltflächen",
        };
        _ = Index.Query("warm up");
        foreach (var q in queries)
        {
            _ = Index.Query(q, useCache: false);
        }

        var times = new List<double>();
        foreach (var q in queries)
        {
            for (var run = 0; run < 7; run++)
            {
                var watch = Stopwatch.StartNew();
                _ = Index.Query(q, useCache: false);
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        times.Sort();
        var median = times[times.Count / 2];
        var p90 = times[(int)(times.Count * 0.9)];
        Assert.True(median < 5, $"median {median:0.00} ms");
        Assert.True(p90 < 25, $"90th percentile {p90:0.00} ms (the slowest is {times[^1]:0.00} ms)");
        // a cached answer is a dictionary lookup
        _ = Index.Query("button");
        var cached = Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
        {
            _ = Index.Query("button");
        }

        Assert.True(cached.Elapsed.TotalMilliseconds / 1000 < 1, $"cached: {cached.Elapsed.TotalMilliseconds / 1000:0.000} ms");
    }

    [Fact]
    public void The_index_is_built_once_quickly_and_a_query_builds_nothing_more()
    {
        var watch = Stopwatch.StartNew();
        Design.Reload();
        _ = Index.Query("button");
        var built = watch.Elapsed.TotalMilliseconds;
        Assert.True(built < 2000, $"building took {built:0} ms");
        var version = Index.Version;
        _ = Index.Query("dropdown");
        Assert.Equal(version, Index.Version);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------ a company that starts blank

    [Fact]
    public void A_blank_site_has_none_of_the_apps_of_the_family_and_its_pages_name_its_own_apps()
    {
        using var blank = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("MATDESIGN_TEMPLATE", "blank");
            builder.UseSetting("MATDESIGN_COMPANY", "Acme Industries");
        });
        var index = blank.Services.GetRequiredService<SearchIndex>();

        Assert.DoesNotContain(index.Documents, d => d.Kind == SearchKind.App);
        Assert.DoesNotContain(index.Query("matpaper").Hits, h => h.Doc.Kind == SearchKind.App);
        var identity = index.Documents.Single(d => d is { Kind: SearchKind.Page, Id: "/Identity" });
        Assert.Contains("Acme apps", identity.Text);
        Assert.DoesNotContain(index.Documents.Where(d => d.Kind == SearchKind.Page), d => d.Text.Contains("Mat apps", StringComparison.Ordinal) || d.Title.Contains("Mat", StringComparison.Ordinal) && d.Title != "Matrix");
        // the rules, components, tokens and icons are the toolkit's, in both templates
        Assert.Contains(index.Query("button").Hits, h => h.Doc.Id == "buttons");
        Assert.DoesNotContain(index.Documents, d => d is { Kind: SearchKind.Page, Id: "/Identity/Audit" });
    }
}
