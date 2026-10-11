using System.Text.Json;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>What changed between two revisions: the fields that differ and the lines that differ.</summary>
public class DiffTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void An_item_is_read_field_by_field_with_lists_by_index_and_null_as_empty()
    {
        var fields = Diff.Flatten(Json("""{"name":"MatX","port":8080,"on":true,"note":null,"tags":["a","b"],"nested":{"deep":{"x":1}},"examples":[{"title":"T","html":"<b>"}]}"""));

        Assert.Equal("MatX", fields["name"]);
        Assert.Equal("8080", fields["port"]);
        Assert.Equal("true", fields["on"]);
        Assert.Equal(string.Empty, fields["note"]);
        Assert.Equal("a", fields["tags[0]"]);
        Assert.Equal("b", fields["tags[1]"]);
        Assert.Equal("1", fields["nested.deep.x"]);
        Assert.Equal("<b>", fields["examples[0].html"]);
        Assert.Empty(Diff.Flatten(Json("{}")));
    }

    [Fact]
    public void The_changes_are_what_differs_what_is_new_and_what_is_gone()
    {
        var changes = Diff.Changes(
            Json("""{"name":"MatX","tagline":"old","port":1,"gone":"x","tags":["a","b"]}"""),
            Json("""{"name":"MatX","tagline":"new","port":1,"fresh":"y","tags":["a","c","d"]}"""));

        Assert.Equal(["tagline", "fresh", "tags[1]", "tags[2]", "gone"], changes.Select(c => c.Path));
        Assert.Equal(new FieldChange("tagline", "old", "new"), changes[0]);
        Assert.True(changes[1].Added);
        Assert.Equal(new FieldChange("tags[1]", "b", "c"), changes[2]);
        Assert.True(changes[3].Added);
        Assert.True(changes[4].Removed);
    }

    [Fact]
    public void Nothing_differs_when_the_revisions_say_the_same()
        => Assert.Empty(Diff.Changes(Json("""{"a":1,"b":["x"]}"""), Json("""{"b":["x"],"a":1}""")));

    [Fact]
    public void Everything_is_new_against_nothing()
    {
        var changes = Diff.Changes(Json("{}"), Json("""{"a":"1","b":"2"}"""));

        Assert.All(changes, c => Assert.True(c.Added));
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void Lines_keep_what_is_common_and_mark_what_is_new_and_what_is_gone()
    {
        var lines = Diff.Lines("one\ntwo\nthree\nfour", "one\n2\nthree\nfour\nfive");

        Assert.Equal(
            [new DiffLine(' ', "one"), new DiffLine('-', "two"), new DiffLine('+', "2"), new DiffLine(' ', "three"), new DiffLine(' ', "four"), new DiffLine('+', "five")],
            lines);
    }

    [Fact]
    public void A_single_line_that_changed_is_one_gone_and_one_new()
        => Assert.Equal([new DiffLine('-', "old"), new DiffLine('+', "new")], Diff.Lines("old", "new"));

    [Fact]
    public void A_value_that_is_new_or_gone_is_only_new_lines_or_only_gone_lines()
    {
        Assert.Equal([new DiffLine('+', "a"), new DiffLine('+', "b")], Diff.Lines(null, "a\nb"));
        Assert.Equal([new DiffLine('-', "a")], Diff.Lines("a", null));
        Assert.Empty(Diff.Lines(null, string.Empty));
        Assert.Empty(Diff.Lines(null, null));
    }

    [Fact]
    public void Lines_ignore_the_kind_of_line_break()
        => Assert.Equal([new DiffLine(' ', "a"), new DiffLine(' ', "b")], Diff.Lines("a\r\nb", "a\nb"));

    [Fact]
    public void A_long_text_is_compared_without_trouble()
    {
        var before = string.Join('\n', Enumerable.Range(0, 400).Select(i => "line " + i));
        var after = string.Join('\n', Enumerable.Range(0, 400).Select(i => i % 50 == 0 ? "changed " + i : "line " + i));

        var lines = Diff.Lines(before, after);

        Assert.Equal(8, lines.Count(l => l.Op == '+'));
        Assert.Equal(8, lines.Count(l => l.Op == '-'));
        Assert.Equal(392, lines.Count(l => l.Op == ' '));
    }
}
