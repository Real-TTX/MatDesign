using System.Text.RegularExpressions;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>The synonyms of the search: the table is data in code, written by hand, in English and in German; this lists it and holds it to its shape.</summary>
public partial class SearchSynonymsTests
{
    [GeneratedRegex(@"^[a-z0-9]+$")]
    private static partial Regex Word();

    private static SearchSynonyms.Group Group(string id) => SearchSynonyms.Groups.Single(g => g.Id == id);

    [Fact]
    public void There_are_at_least_eighty_groups_with_at_least_three_hundred_words_and_every_group_has_its_own_id()
    {
        Assert.True(SearchSynonyms.Groups.Count >= 80, $"only {SearchSynonyms.Groups.Count} groups");
        Assert.True(SearchSynonyms.Groups.Sum(g => g.Folded.Count) >= 300, $"only {SearchSynonyms.Groups.Sum(g => g.Folded.Count)} words");
        Assert.Equal(SearchSynonyms.Groups.Count, SearchSynonyms.Groups.Select(g => g.Id).Distinct().Count());
        Assert.All(SearchSynonyms.Groups, g => Assert.True(g.Folded.Count >= 2, g.Id + " needs two words to be a group"));
    }

    [Fact]
    public void A_word_is_one_word_of_letters_and_digits_and_is_stored_folded()
    {
        foreach (var group in SearchSynonyms.Groups)
        {
            foreach (var term in group.Terms)
            {
                Assert.DoesNotContain(' ', term);
                Assert.Equal(SearchText.Fold(term), SearchText.Fold(SearchText.Fold(term)));
            }

            Assert.All(group.Folded, w => Assert.Matches(Word(), w));
        }
    }

    [Theory]
    [InlineData("select", "dropdown", "auswahl", "combobox")]
    [InlineData("dialog", "modal", "popup", "overlay")]
    [InlineData("switch", "toggle", "schalter")]
    [InlineData("checkbox", "check", "häkchen")]
    [InlineData("tooltip", "hinweis")]
    [InlineData("tabs", "tab", "reiter")]
    [InlineData("datepicker", "datum", "kalender")]
    [InlineData("upload", "datei", "hochladen")]
    [InlineData("rating", "bewertung", "sterne")]
    [InlineData("tree", "baum", "verzeichnis")]
    [InlineData("toast", "meldung", "snackbar")]
    [InlineData("button", "knopf", "schaltfläche")]
    [InlineData("input", "textfeld", "eingabe")]
    [InlineData("table", "tabelle")]
    [InlineData("card", "karte")]
    [InlineData("colour", "color", "farbe")]
    [InlineData("font", "schrift")]
    [InlineData("dark", "dunkel")]
    [InlineData("mobile", "handy", "touch")]
    [InlineData("a11y", "barrierefrei", "accessibility")]
    public void The_groups_the_owner_asked_for_are_there_in_both_languages(string id, params string[] words)
    {
        var group = Group(id);
        foreach (var word in words)
        {
            Assert.Contains(SearchText.Fold(word), group.Folded);
        }
    }

    [Fact]
    public void A_typed_word_agrees_exactly_as_a_start_as_a_compound_or_as_a_typo()
    {
        Assert.Equal(SearchSynonyms.Agreement.Exact, SearchSynonyms.Match("schalter").Single(m => m.Group.Id == "switch").Agreement);
        Assert.Equal(SearchSynonyms.Agreement.Start, SearchSynonyms.Match("ausw").Single(m => m.Group.Id == "select").Agreement);
        Assert.Equal(SearchSynonyms.Agreement.Compound, SearchSynonyms.Match("auswahlmenu").Single(m => m.Group.Id == "select").Agreement);
        Assert.Equal(SearchSynonyms.Agreement.Typo, SearchSynonyms.Match("haekchen").Single(m => m.Group.Id == "checkbox").Agreement);
        Assert.Equal(SearchSynonyms.Agreement.Typo, SearchSynonyms.Match("datepicr").Single(m => m.Group.Id == "datepicker").Agreement);
        Assert.Empty(SearchSynonyms.Match("zzzzzz"));
    }

    [Fact]
    public void A_word_in_two_groups_is_in_both_and_the_strongest_agreement_comes_first()
    {
        var hinweis = SearchSynonyms.Match("hinweis").Select(m => m.Group.Id).ToList();
        Assert.Contains("tooltip", hinweis);
        Assert.Contains("info", hinweis);
        Assert.Equal(SearchSynonyms.Agreement.Exact, SearchSynonyms.Match("hinweis").Single(m => m.Group.Id == "tooltip").Agreement);
        Assert.Equal(SearchSynonyms.Agreement.Exact, SearchSynonyms.Match("hinweis").Single(m => m.Group.Id == "info").Agreement);

        var spinner = SearchSynonyms.Match("spinner");
        Assert.Contains(spinner, m => m.Group.Id == "stepper");
        Assert.Contains(spinner, m => m.Group.Id == "loading");
        Assert.Equal(spinner.OrderBy(m => m.Agreement).Select(m => m.Agreement), spinner.Select(m => m.Agreement));
    }

    [Fact]
    public void The_listing_of_the_table_is_what_the_owner_reads_when_a_word_is_missing()
    {
        // one line per group: this is the test that lists it (it fails when a group is dropped, and its message is the whole table)
        var listing = string.Join("\n", SearchSynonyms.Groups.Select(g => g.Id.PadRight(14) + string.Join(' ', g.Terms)));

        Assert.Contains("switch        switch toggle schalter", listing);
        Assert.Contains("dark          dark dunkel", listing);
        Assert.True(listing.Split('\n').Length >= 80);
    }
}
