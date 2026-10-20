using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>What the search does to text: folding, words, plurals, the distance of two words (docs/ARCHITECTURE.md, "Search").</summary>
public class SearchTextTests
{
    [Theory]
    [InlineData("Häkchen", "hakchen")]
    [InlineData("HÄKCHEN", "hakchen")]
    [InlineData("Schaltfläche", "schaltflache")]
    [InlineData("Größe", "grosse")]
    [InlineData("Straße", "strasse")]
    [InlineData("Übersicht", "ubersicht")]
    [InlineData("Öffnen", "offnen")]
    [InlineData("café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("Łódź", "lodz")]
    [InlineData("œuvre", "oeuvre")]
    [InlineData("Ærø", "aero")]
    [InlineData("--mat-accent", "--mat-accent")]
    [InlineData("FORM-15", "form-15")]
    public void Case_accents_umlauts_and_the_sharp_s_are_folded_away(string text, string expected)
        => Assert.Equal(expected, SearchText.Fold(text));

    [Fact]
    public void A_letter_that_came_decomposed_folds_like_the_one_that_came_whole()
    {
        // a and a combining diaeresis (what a keyboard or a copy from a Mac can give)
        Assert.Equal("hakchen", SearchText.Fold("Häkchen"));
        Assert.Equal(SearchText.Fold("Häkchen"), SearchText.Fold("Häkchen"));
    }

    [Fact]
    public void The_folding_does_not_ask_the_runtime_to_normalise_because_the_site_runs_with_the_invariant_globalization()
    {
        // The image has no ICU: with InvariantGlobalization a normalisation form does nothing for an umlaut, so a search that used it would find "häkchen" on a developer PC and not in the
        // container (found by running the site: the unit tests run with ICU and passed). The folding is a table.
        var source = File.ReadAllText(TestPaths.Web("Services/SearchText.cs"));
        Assert.DoesNotContain(".Normalize(", source);
        Assert.DoesNotContain("NormalizationForm", source);
        Assert.Contains("<InvariantGlobalization>true</InvariantGlobalization>", File.ReadAllText(TestPaths.Web("MatDesign.Web.csproj")));
    }

    [Fact]
    public void The_map_says_where_each_folded_character_came_from()
    {
        var map = new List<int>();
        var folded = SearchText.Fold("Straße Häkchen", map);

        Assert.Equal("strasse hakchen", folded);
        Assert.Equal(folded.Length, map.Count);
        // the two s of "ss" both come from the ß at index 4, and the h of "hakchen" from the H at index 7
        Assert.Equal([0, 1, 2, 3, 4, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13], map);
    }

    [Fact]
    public void Words_are_the_letters_and_digits_between_everything_else()
    {
        Assert.Equal(["mat", "accent", "soft"], SearchText.Terms("--mat-accent-soft"));
        Assert.Equal(["form", "15"], SearchText.Terms("FORM-15"));
        Assert.Equal(["a11y", "01"], SearchText.Terms("A11Y 01"));
        Assert.Equal(["schaltflache", "primary"], SearchText.Terms("  Schaltfläche,   primary! "));
        Assert.Empty(SearchText.Terms("--- ... !!!"));
        Assert.Empty(SearchText.Terms(null));
        Assert.Empty(SearchText.Terms(string.Empty));
    }

    [Theory]
    [InlineData("buttons", "button")]
    [InlineData("button", "button")]
    [InlineData("boxes", "box")]
    [InlineData("checkboxes", "checkbox")]
    [InlineData("switches", "switch")]
    [InlineData("parties", "party")]
    [InlineData("lines", "line")]
    [InlineData("class", "class")]
    [InlineData("status", "status")]
    [InlineData("axis", "axis")]
    [InlineData("tabs", "tab")]
    [InlineData("is", "is")]
    public void A_plural_is_taken_off_for_the_comparison_of_a_title_with_a_query(string word, string stem)
        => Assert.Equal(stem, SearchText.Stem(word));

    [Theory]
    [InlineData("button", "button", 2, 0)]
    [InlineData("buton", "button", 2, 1)]
    [InlineData("buttton", "button", 2, 1)]
    [InlineData("dropdwon", "dropdown", 2, 1)]
    [InlineData("dropdown", "dropdwon", 2, 1)]
    [InlineData("hakchen", "haekchen", 2, 1)]
    [InlineData("kitten", "sitting", 3, 3)]
    [InlineData("abcdef", "uvwxyz", 2, 3)]
    [InlineData("a", "abcdef", 2, 3)]
    [InlineData("", "ab", 2, 2)]
    public void The_distance_counts_an_insertion_a_deletion_a_replaced_letter_and_two_swapped_letters_as_one_and_stops_above_the_limit(string a, string b, int max, int expected)
        => Assert.Equal(expected, SearchText.Distance(a, b, max));

    [Fact]
    public void A_word_longer_than_the_limit_is_only_the_same_or_far_away()
    {
        var long1 = new string('a', SearchText.FuzzyLimit + 5);
        var long2 = new string('a', SearchText.FuzzyLimit + 4) + "b";
        Assert.Equal(0, SearchText.Distance(long1, long1, 2));
        Assert.Equal(3, SearchText.Distance(long1, long2, 2));
    }

    [Fact]
    public void The_distance_is_the_same_from_every_thread_and_after_any_earlier_call()
    {
        // the rows of the calculation are a buffer of each thread: a call must not see what an earlier one left in it
        var words = new[] { "button", "buton", "dropdown", "dropdwon", "a", "tabs", "reiter", "datepicker", "datepicr", "kontrollkastchen", "kontrollkaestchen" };
        var expected = words.SelectMany(a => words.Select(b => (a, b, SearchText.Distance(a, b, 2)))).ToList();
        var results = new System.Collections.Concurrent.ConcurrentBag<bool>();
        Parallel.For(0, 200, _ =>
        {
            foreach (var (a, b, d) in expected)
            {
                results.Add(SearchText.Distance(a, b, 2) == d && SearchText.Distance(b, a, 2) == d);
            }
        });

        Assert.All(results, ok => Assert.True(ok));
    }

    [Fact]
    public void The_marks_of_the_page_are_where_the_words_are_in_the_title_whatever_the_case_and_the_accents()
    {
        var html = SearchHighlight.Mark("Häkchen und Schaltflächen", ["hakchen", "schaltfl"]);

        Assert.Equal("<mark class=\"mat-search__hit\">Häkchen</mark> und <mark class=\"mat-search__hit\">Schaltfl</mark>ächen".Replace("ä", "&#xE4;"), Html(html));
    }

    [Fact]
    public void A_title_is_never_markup_and_a_mark_never_cuts_an_entity()
    {
        var html = Html(SearchHighlight.Mark("<img src=x onerror=alert(1)> & \"quoted\"", ["img", "quoted"]));

        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;<mark class=\"mat-search__hit\">img</mark> src=x onerror=alert(1)&gt;", html);
        Assert.Contains("&amp;", html);
        Assert.Contains("&quot;<mark class=\"mat-search__hit\">quoted</mark>&quot;", html);
    }

    [Fact]
    public void Touching_and_overlapping_words_are_one_mark_and_a_word_that_is_not_there_marks_nothing()
    {
        Assert.Single(SearchHighlight.Ranges("buttons", ["butt", "utton", "ons"]));
        Assert.Equal((0, 7), SearchHighlight.Ranges("buttons", ["butt", "utton", "ons"])[0]);
        Assert.Empty(SearchHighlight.Ranges("buttons", ["xyz"]));
        Assert.Empty(SearchHighlight.Ranges("buttons", [""]));
        Assert.Empty(SearchHighlight.Ranges(string.Empty, ["a"]));
    }

    private static string Html(Microsoft.AspNetCore.Html.IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }
}
