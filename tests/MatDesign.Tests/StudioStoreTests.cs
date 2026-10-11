using System.Text.Json;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>The store of the Studio: an append-only log per item, so that history and restore need nothing else.</summary>
public sealed class StudioStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "matdesign-store-" + Guid.NewGuid().ToString("N"));
    private readonly StudioStore _store;

    public StudioStoreTests() => _store = new StudioStore(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed record Thing(string Name, int Size);

    [Fact]
    public void An_item_that_was_never_saved_is_not_there()
    {
        Assert.Null(_store.Current("apps", "none"));
        Assert.Null(_store.Get<Thing>("apps", "none"));
        Assert.Empty(_store.History("apps", "none"));
        Assert.Empty(_store.List("apps"));
        Assert.Empty(_store.Kinds());
    }

    [Fact]
    public void A_saved_item_comes_back_as_it_was_saved_and_the_revisions_count_up()
    {
        var first = _store.Save("apps", "matx", new Thing("MatX", 1), "admin", "first");
        var second = _store.Save("apps", "matx", new Thing("MatX", 2), "admin", "bigger");

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
        Assert.Equal(new Thing("MatX", 2), _store.Get<Thing>("apps", "matx"));
        Assert.Equal(["bigger", "first"], _store.History("apps", "matx").Select(r => r.Note));
    }

    [Fact]
    public void Deleting_keeps_the_history_and_the_item_can_be_restored()
    {
        _store.Save("rules", "X-01", new Thing("a", 1), "admin");
        _store.Save("rules", "X-01", new Thing("b", 2), "admin");
        var deleted = _store.Delete("rules", "X-01", "admin", "not needed");

        Assert.True(deleted.Deleted);
        Assert.Null(_store.Get<Thing>("rules", "X-01"));
        Assert.Empty(_store.List("rules"));
        Assert.Single(_store.List("rules", includeDeleted: true));
        Assert.Equal(3, _store.History("rules", "X-01").Count);

        var restored = _store.Restore("rules", "X-01", 1, "admin");
        Assert.Equal(4, restored.Number);
        Assert.Equal("Restored revision 1", restored.Note);
        Assert.Equal(new Thing("a", 1), _store.Get<Thing>("rules", "X-01"));
        Assert.Single(_store.List("rules"));
    }

    [Fact]
    public void Restoring_a_revision_that_does_not_exist_says_so()
        => Assert.Throws<KeyNotFoundException>(() => _store.Restore("apps", "matx", 7, "admin"));

    [Fact]
    public void Items_are_listed_by_id_and_each_kind_has_its_own_list()
    {
        _store.Save("apps", "b", new Thing("b", 1), "admin");
        _store.Save("apps", "a", new Thing("a", 1), "admin");
        _store.Save("rules", "R-1", new Thing("r", 1), "admin");

        Assert.Equal(["a", "b"], _store.List("apps").Select(i => i.Id));
        Assert.Equal(["R-1"], _store.List("rules").Select(i => i.Id));
        Assert.Equal(["apps", "rules"], _store.Kinds());
    }

    [Fact]
    public async Task The_activity_lists_the_newest_changes_first_across_all_kinds()
    {
        _store.Save("apps", "a", new Thing("a", 1), "admin", "one");
        await Task.Delay(15);
        _store.Save("rules", "R-1", new Thing("r", 1), "admin", "two");
        await Task.Delay(15);
        _store.Save("apps", "a", new Thing("a", 2), "admin", "three");

        Assert.Equal(["three", "two", "one"], _store.Activity().Select(i => i.Revision.Note));
        Assert.Equal(["three", "one"], _store.Activity(kind: "apps").Select(i => i.Revision.Note));
        Assert.Equal(["three", "two"], _store.Activity(2).Select(i => i.Revision.Note));
    }

    [Theory]
    [InlineData("../x", "id")]
    [InlineData("apps", "../escape")]
    [InlineData("apps", "a/b")]
    [InlineData("apps", "a\\b")]
    [InlineData("apps", "")]
    [InlineData("Apps", "id")]
    [InlineData("apps/x", "id")]
    [InlineData("apps", "..")]
    [InlineData("apps", ".hidden")]
    public void A_kind_or_an_id_that_is_a_path_is_refused(string kind, string id)
        => Assert.Throws<ArgumentException>(() => _store.Save(kind, id, new Thing("x", 1), "admin"));

    [Fact]
    public void A_note_and_a_name_are_one_short_line()
    {
        var revision = _store.Save("apps", "a", new Thing("a", 1), "ad\nmin", "line one\r\nline two " + new string('x', 600));

        Assert.DoesNotContain('\n', revision.By);
        Assert.DoesNotContain('\n', revision.Note);
        Assert.Equal(500, revision.Note.Length);
    }

    [Fact]
    public void A_revision_is_a_file_that_a_person_can_read_and_nothing_is_left_half_written()
    {
        _store.Save("apps", "matx", new Thing("MatX", 1), "admin", "why");
        var folder = Path.Combine(_directory, "studio", "apps", "matx");

        var files = Directory.GetFiles(folder);
        Assert.Equal(["000001.json"], files.Select(Path.GetFileName));
        using var json = JsonDocument.Parse(File.ReadAllText(files[0]));
        Assert.Equal("admin", json.RootElement.GetProperty("by").GetString());
        Assert.Equal("why", json.RootElement.GetProperty("note").GetString());
        Assert.Equal("MatX", json.RootElement.GetProperty("data").GetProperty("name").GetString());
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    [Fact]
    public async Task Saves_from_several_threads_get_distinct_revision_numbers()
    {
        var numbers = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => _store.Save("apps", "busy", new Thing("t", i), "admin").Number)));

        Assert.Equal(Enumerable.Range(1, 24), numbers.Order());
    }
}
