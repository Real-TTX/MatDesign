using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>A file of the brand as the site serves it and a release carries it: the public name below <c>brand/</c>, the bytes, the content type and the SHA-256.</summary>
public sealed record BrandFile(string Name, string Slot, byte[] Bytes, string ContentType, string Sha256);

/// <summary>
/// The files the brand's assets refer to (docs/PRODUCT.md, stage 13): the store on the volume, and the files of the current assets as they are served (<c>/ui/brand/&lt;name&gt;</c>), written into
/// every release under <c>brand/</c>, listed in the manifests and put into the archive. A file is served only when it is on the volume and still has the hash in its name: whatever is on
/// the volume under a name that is not its content is never sent out. The list follows <see cref="DesignData.Version"/>.
/// </summary>
public sealed class AssetFiles(StudioService studio, DesignData design)
{
    private readonly object _gate = new();
    private volatile Cache? _cache;

    private sealed record Cache(long Version, IReadOnlyList<BrandFile> Files);

    /// <summary>The store of the files; off (it holds nothing) while the Studio is off.</summary>
    public AssetStore Store { get; } = new(studio.Enabled ? studio.DataDirectory : null);

    /// <summary>The files of the current assets, in the order of the item. A file that is missing or does not match its hash is left out (the check <c>assets</c> reports it).</summary>
    public IReadOnlyList<BrandFile> Files()
    {
        var version = design.Version;
        if (_cache is { } cache && cache.Version == version)
        {
            return cache.Files;
        }

        lock (_gate)
        {
            if (_cache is { } again && again.Version == version)
            {
                return again.Files;
            }

            var files = new List<BrandFile>();
            if (design.Assets is { } assets)
            {
                foreach (var reference in assets.References())
                {
                    if (files.Any(f => f.Name == reference.Name) || Store.Read(reference.File) is not { } bytes || BrandAssetValidator.Sha256(bytes) != reference.File[..64] || BrandAssets.ContentTypeOf(reference.File) is not { } type)
                    {
                        continue;
                    }

                    files.Add(new BrandFile(reference.Name, reference.Slot, bytes, type, reference.File[..64]));
                }
            }

            _cache = new Cache(version, files);
            return files;
        }
    }

    /// <summary>The file with a public name (<c>logo-1a2b3c4d5e.svg</c>, <c>fonts/acme-400-normal-1a2b3c4d5e.woff2</c>); null when the current assets have none of that name.</summary>
    public BrandFile? Find(string? name) => name is null ? null : Files().FirstOrDefault(f => f.Name == name);
}
