using System.Reflection;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>The store of the brand's files (IDENT-08): a file is named by its SHA-256, written once and never overwritten or deleted, and a name is never a path.</summary>
public sealed class AssetStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "matdesign-assets-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void A_file_is_kept_under_the_hash_of_its_content()
    {
        var store = new AssetStore(_directory);
        var content = AssetFixtures.Png(32, 32);
        var name = store.Put(content, ".png");
        Assert.Equal(BrandAssetValidator.Sha256(content) + ".png", name);
        Assert.True(File.Exists(Path.Combine(_directory, "assets", name)));
        Assert.Equal(content, store.Read(name));
        Assert.True(store.Exists(name));
    }

    [Fact]
    public void The_same_content_is_kept_once_and_the_file_is_not_written_again()
    {
        var store = new AssetStore(_directory);
        var content = AssetFixtures.Bytes(AssetFixtures.GoodSvg());
        var first = store.Put(content, ".svg");
        var path = Path.Combine(_directory, "assets", first);
        var written = File.GetLastWriteTimeUtc(path);
        Thread.Sleep(30);
        Assert.Equal(first, store.Put(content, ".svg"));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "assets")));
    }

    [Fact]
    public void Other_content_is_another_file_and_the_first_one_stays()
    {
        var store = new AssetStore(_directory);
        var a = store.Put(AssetFixtures.Bytes(AssetFixtures.GoodSvg()), ".svg");
        var b = store.Put(AssetFixtures.Bytes(AssetFixtures.GoodSvg(inner: "<circle r=\"1\"/>")), ".svg");
        Assert.NotEqual(a, b);
        Assert.NotNull(store.Read(a));
        Assert.NotNull(store.Read(b));
    }

    [Fact]
    public void A_file_that_was_changed_on_the_volume_is_never_overwritten_by_a_put()
    {
        var store = new AssetStore(_directory);
        var content = AssetFixtures.Png(16, 16);
        var name = store.Put(content, ".png");
        File.WriteAllText(Path.Combine(_directory, "assets", name), "somebody edited this by hand");
        Assert.Throws<IOException>(() => store.Put(content, ".png"));
        Assert.Equal("somebody edited this by hand", File.ReadAllText(Path.Combine(_directory, "assets", name)));
    }

    [Fact]
    public void A_name_that_is_not_a_hash_and_an_extension_reads_nothing()
    {
        var store = new AssetStore(_directory);
        File.WriteAllText(Path.Combine(_directory, "secret.txt"), "not for the web");
        foreach (var name in new[] { "../secret.txt", "..\\secret.txt", "secret.txt", "", "/etc/passwd", "C:\\Windows\\win.ini", new string('a', 64) + ".exe", new string('a', 64) + ".svg/../../secret.txt" })
        {
            Assert.Null(store.Read(name));
            Assert.False(store.Exists(name));
        }

        Assert.Null(store.Read(null));
    }

    [Fact]
    public void A_put_with_a_strange_extension_is_refused()
    {
        var store = new AssetStore(_directory);
        Assert.Throws<ArgumentException>(() => store.Put([1, 2, 3], ".exe"));
        Assert.Throws<ArgumentException>(() => store.Put([1, 2, 3], "/../x"));
    }

    [Fact]
    public void Without_a_volume_the_store_is_off_and_holds_nothing()
    {
        var store = new AssetStore(null);
        Assert.False(store.Enabled);
        Assert.Null(store.Read(new string('a', 64) + ".png"));
        Assert.Throws<InvalidOperationException>(() => store.Put([1], ".png"));
    }

    [Fact]
    public void The_store_has_no_way_to_delete_or_replace_a_file()
    {
        // a file is immutable: nothing public can remove or rewrite one, so no revision of the brand can lose a file
        var members = typeof(AssetStore).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).ToList();
        Assert.DoesNotContain(members, n => n.Contains("Delete", StringComparison.OrdinalIgnoreCase) || n.Contains("Remove", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Overwrite", StringComparison.OrdinalIgnoreCase) || n.Contains("Replace", StringComparison.OrdinalIgnoreCase) || n.Contains("Clear", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["Exists", "Put", "Read"], members.Where(n => n is "Put" or "Read" or "Exists").Order(StringComparer.Ordinal).ToArray());
    }
}
