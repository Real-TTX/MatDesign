using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>A file of a release as the Build stage assembles it: its path inside the release and its bytes.</summary>
public sealed record BuildFile(string Path, byte[] Bytes);

/// <summary>What the white label build made of the files of a release: the renamed files, what the renamer did to each text, and what the checks of the result found.</summary>
public sealed record WhiteLabelBuildResult(
    WhiteLabelSetting Setting,
    IReadOnlyList<BuildFile> Files,
    IReadOnlyList<WhiteLabelFile> Report,
    IReadOnlyList<WhiteLabelFinding> Findings,
    string Hash,
    string SourceHash)
{
    public bool Passed => Findings.Count == 0;

    /// <summary>The replacements per rule over every file.</summary>
    public IReadOnlyDictionary<string, int> Totals
        => Report.SelectMany(f => f.Counts).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
}

/// <summary>Where the one white label setting is kept: the item <c>whitelabel</c> of the Studio.</summary>
public static class WhiteLabelStore
{
    /// <summary>The setting the Studio saved, normalised; null when none is saved, or when what is on the volume is not a valid setting.</summary>
    public static WhiteLabelSetting? Saved(StudioService studio)
    {
        if (!studio.Enabled)
        {
            return null;
        }

        try
        {
            return studio.Store.Get<WhiteLabelSetting>(WhiteLabelSetting.Kind, WhiteLabelSetting.ItemId) is { } read && WhiteLabel.Normalize(read) is var setting && WhiteLabel.IsSafe(setting) ? setting : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The setting a release applies: the saved one, unless it renames nothing.</summary>
    public static WhiteLabelSetting? Active(StudioService studio) => Saved(studio) is { IsNoOp: false } setting ? setting : null;
}

/// <summary>
/// The white label build (docs/PRODUCT.md, stage 15): takes the files the Build stage assembled and makes the copy for a company with its own prefix and name. Nothing of the repository changes; the
/// rename is applied to the files of the release, text by text (<see cref="WhiteLabel"/>), the JSON of the design data string by string, and the archive is opened, renamed and written again with a
/// manifest that lists the files as they are now. The result is checked by <see cref="WhiteLabelChecks"/> and the archive is held to the loose files; the build fails on any finding.
/// </summary>
public static class WhiteLabelBuild
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] TextExtensions = [".css", ".js", ".svg", ".json", ".html", ".md", ".txt"];

    private static readonly DateTimeOffset ArchiveTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static bool IsText(string path) => TextExtensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>The hash a copy carries in its first line: the same formula as the toolkit's (the parts in the order of the bundle, the script, the boot script, the sprite), over the renamed files.</summary>
    public static string HashOf(IEnumerable<string> parts, string script, string boot, string sprite)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(parts) + script + boot + sprite)))[..10].ToLowerInvariant();

    /// <summary>
    /// Makes the copy. <paramref name="files"/> are the files of the release as the Build stage assembles them (with the names of the source), <paramref name="sourceHash"/> is the hash the toolkit names in
    /// the first line of its bundle. Returns the renamed files in the same order, the replacements per file and the findings of the checks.
    /// </summary>
    public static WhiteLabelBuildResult Apply(IReadOnlyList<BuildFile> files, WhiteLabelSetting setting, string sourceHash)
    {
        var report = new List<WhiteLabelFile>();
        var renamed = new List<(string Path, string? Text, byte[]? Bytes)>();
        foreach (var file in files)
        {
            var path = WhiteLabel.RenamePath(file.Path, setting);
            if (file.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                renamed.Add((path, null, file.Bytes));
            }
            else if (IsText(file.Path))
            {
                var original = Encoding.UTF8.GetString(file.Bytes);
                var result = file.Path == "design.json" ? WhiteLabel.RenameJson(original, setting, Json) : WhiteLabel.RenameCounted(original, setting);
                renamed.Add((path, result.Text, null));
                report.Add(new WhiteLabelFile(path, original, result.Text, result.Counts));
            }
            else
            {
                renamed.Add((path, null, file.Bytes));
            }
        }

        // the hash of the copy: the parts, the script, the boot script and the sprite, as they are now
        string Text(string name) => report.First(f => f.Path == name).Text;
        var stem = setting.FileStem;
        var parts = report.Where(f => f.Path.StartsWith("css/", StringComparison.Ordinal)).Select(f => f.Text);
        var hash = HashOf(parts, Text($"{stem}.js"), Text($"{stem}.boot.js"), Text($"{stem}-icons.svg"));

        // the old hash stands in the first line of the bundle, in the style guide and in the README of the archive: the copy names its own
        var hashed = report.Select(f => f with { Text = f.Text.Replace(sourceHash, hash, StringComparison.Ordinal) }).ToList();
        var byPath = hashed.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var outFiles = new List<BuildFile>();
        var findings = new List<WhiteLabelFinding>();
        foreach (var (path, text, bytes) in renamed)
        {
            if (text is not null)
            {
                outFiles.Add(new BuildFile(path, new UTF8Encoding(false).GetBytes(byPath[path].Text)));
            }
            else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var zip = RenameArchive(bytes!, setting, sourceHash, hash, hashed, findings);
                outFiles.Add(new BuildFile(path, zip));
            }
            else
            {
                outFiles.Add(new BuildFile(path, bytes!));
            }
        }

        findings.InsertRange(0, WhiteLabelChecks.Run(setting, hashed));
        return new WhiteLabelBuildResult(setting, outFiles, hashed, findings, hash, sourceHash);
    }

    /// <summary>
    /// The archive as the copy carries it: every entry renamed (its name and its text), the manifest inside written again with the names, sizes and hashes of the renamed entries, and the entries
    /// held to the loose files of the release. Entries have a fixed time, so the same input is the same archive.
    /// </summary>
    private static byte[] RenameArchive(byte[] archive, WhiteLabelSetting setting, string sourceHash, string hash, IReadOnlyList<WhiteLabelFile> loose, List<WhiteLabelFinding> findings)
    {
        using var input = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        var entries = new List<(string Name, string Text)>();
        foreach (var entry in input.Entries.Where(e => e.Name.Length > 0))
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var text = reader.ReadToEnd();
            var renamedText = entry.Name.EndsWith(".manifest.json", StringComparison.Ordinal) ? text : WhiteLabel.Rename(text, setting);
            entries.Add((WhiteLabel.RenamePath(entry.FullName, setting), renamedText.Replace(sourceHash, hash, StringComparison.Ordinal)));
        }

        var folder = $"{setting.FileStem}/";
        var manifestIndex = entries.FindIndex(e => e.Name.EndsWith(".manifest.json", StringComparison.Ordinal));
        if (manifestIndex >= 0)
        {
            var manifest = JsonNode.Parse(input.Entries.First(e => e.Name.EndsWith(".manifest.json", StringComparison.Ordinal)).Open())!.AsObject();
            manifest["name"] = setting.Name;
            manifest["hash"] = hash;
            var list = new JsonArray();
            foreach (var entry in entries.Where((e, i) => i != manifestIndex && !e.Name.EndsWith("README.txt", StringComparison.Ordinal)))
            {
                var name = entry.Name.StartsWith(folder, StringComparison.Ordinal) ? entry.Name[folder.Length..] : entry.Name;
                list.Add(new JsonObject
                {
                    ["name"] = name,
                    ["url"] = name,
                    ["bytes"] = Encoding.UTF8.GetByteCount(entry.Text),
                    ["sha256"] = Sha(entry.Text),
                });
            }

            manifest["files"] = list;
            // the changelog address and the other strings of the manifest are renamed like any text
            foreach (var key in manifest.Select(p => p.Key).Where(k => k is "changelog").ToList())
            {
                manifest[key] = WhiteLabel.Rename(manifest[key]!.GetValue<string>(), setting);
            }

            entries[manifestIndex] = (entries[manifestIndex].Name, manifest.ToJsonString(Json));
        }

        CheckArchive(entries, setting, folder, hash, loose, findings);

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = ArchiveTime;
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(text);
            }
        }

        return stream.ToArray();
    }

    private static void CheckArchive(IReadOnlyList<(string Name, string Text)> entries, WhiteLabelSetting setting, string folder, string hash, IReadOnlyList<WhiteLabelFile> loose, List<WhiteLabelFinding> findings)
    {
        const string check = "archive";
        foreach (var (name, text) in entries)
        {
            if (!name.StartsWith(folder, StringComparison.Ordinal))
            {
                findings.Add(new(check, $"The entry {name} is not in the folder {folder}.", name));
                continue;
            }

            var relative = name[folder.Length..];
            if (loose.FirstOrDefault(f => f.Path == relative) is { } file && file.Text != text)
            {
                findings.Add(new(check, "The entry is not the same as the file of the release.", name));
            }

            if (relative.EndsWith(".manifest.json", StringComparison.Ordinal))
            {
                var manifest = JsonNode.Parse(text)!.AsObject();
                if (manifest["name"]?.GetValue<string>() != setting.Name || manifest["hash"]?.GetValue<string>() != hash)
                {
                    findings.Add(new(check, "The manifest does not name the product and the hash of the copy.", name));
                }

                foreach (var item in manifest["files"]!.AsArray())
                {
                    var listed = item!["name"]!.GetValue<string>();
                    var entry = entries.FirstOrDefault(e => e.Name == folder + listed);
                    if (entry.Name is null || Sha(entry.Text) != item["sha256"]!.GetValue<string>())
                    {
                        findings.Add(new(check, $"{listed} is not in the archive, or does not match its hash in the manifest.", name));
                    }
                }
            }
        }

        if (!entries.Any(e => e.Name == folder + "README.txt" && e.Text.StartsWith(setting.Name + " ", StringComparison.Ordinal)))
        {
            findings.Add(new(check, "The README of the archive does not begin with the name of the product.", folder + "README.txt"));
        }
    }
}
