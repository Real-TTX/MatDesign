namespace MatDesign.Core.Studio;

/// <summary>
/// Where the files of a brand live: <c>&lt;data&gt;/assets/&lt;sha256&gt;.&lt;ext&gt;</c>. A file is named by the hash of its content, so a name always means the same bytes: a file is written once
/// and never overwritten, and <b>this class has no way to delete or replace one</b> (a test holds that), so that every revision of the <c>assets</c> item, which only refers to these names, can be
/// restored. A name is never a path: only a 64-digit hash and one of five extensions is accepted. Without a data directory the store is off and holds nothing.
/// </summary>
public sealed class AssetStore
{
    private readonly string? _root;
    private readonly object _gate = new();

    public AssetStore(string? dataDirectory)
    {
        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            _root = Path.Combine(dataDirectory, "assets");
            Directory.CreateDirectory(_root);
        }
    }

    /// <summary>True when there is a volume to keep files on.</summary>
    public bool Enabled => _root is not null;

    /// <summary>The folder of the files; null when the store is off.</summary>
    public string? Root => _root;

    private string? PathOf(string? name) => _root is null || !BrandAssetValidator.IsStoredName(name) ? null : Path.Combine(_root, name!);

    /// <summary>True when the file is on the volume.</summary>
    public bool Exists(string? name) => PathOf(name) is { } path && File.Exists(path);

    /// <summary>The content of a stored file; null when the name is not one or the file is not there.</summary>
    public byte[]? Read(string? name) => PathOf(name) is { } path && File.Exists(path) ? File.ReadAllBytes(path) : null;

    /// <summary>
    /// Keeps a content under its hash and returns the name. A content that is stored already is not written again; a file that has the name but other bytes (it was changed by hand) is
    /// refused rather than overwritten.
    /// </summary>
    public string Put(byte[] content, string ext)
    {
        if (_root is null)
        {
            throw new InvalidOperationException("The Studio has no volume to keep files on.");
        }

        var name = BrandAssetValidator.StoredFileName(content, ext);
        if (!BrandAssetValidator.IsStoredName(name))
        {
            throw new ArgumentException($"'{ext}' is not the extension of a file of a brand.", nameof(ext));
        }

        lock (_gate)
        {
            var target = Path.Combine(_root, name);
            if (File.Exists(target))
            {
                if (new FileInfo(target).Length != content.Length || !File.ReadAllBytes(target).AsSpan().SequenceEqual(content))
                {
                    throw new IOException($"The file {name} is on the volume with other content. It is not overwritten.");
                }

                return name;
            }

            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, content);
            try
            {
                File.Move(temp, target, overwrite: false);
            }
            catch (IOException) when (File.Exists(target))
            {
                File.Delete(temp);
            }

            return name;
        }
    }
}
