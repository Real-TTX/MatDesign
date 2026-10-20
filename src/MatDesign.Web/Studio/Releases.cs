using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;

namespace MatDesign.Web.Studio;

/// <summary>How far a stage of the pipeline got.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StageStatus
{
    Succeeded,
    Failed,

    /// <summary>Not run, because an earlier stage failed.</summary>
    Skipped,
}

/// <summary>One stage of a run with what it did, line by line.</summary>
public sealed record PipelineStage(string Name, StageStatus Status, IReadOnlyList<string> Log, int Milliseconds);

/// <summary>One run of the pipeline: who started it for which version, and what every stage did. A run is kept whether it worked or not.</summary>
public sealed record PipelineRun(string Id, string Version, string Note, string By, DateTimeOffset At, bool Succeeded, IReadOnlyList<PipelineStage> Stages);

/// <summary>A file of a release with its size and its SHA-256, so an app can check what it copied.</summary>
public sealed record ReleaseFile(string Path, long Bytes, string Sha256);

/// <summary>A release of the design system: the toolkit and the design data of one moment, under a version, immutable.</summary>
public sealed record ReleaseManifest(
    string Version,
    DateTimeOffset At,
    string By,
    string Note,
    string Hash,
    string Run,
    int Errors,
    int Advice,
    int Apps,
    int Rules,
    int Components,
    IReadOnlyList<string> Changelog,
    IReadOnlyList<ReleaseFile> Files,
    string? Toolkit = null,
    string? Company = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WhiteLabelSetting? WhiteLabel = null);

/// <summary>
/// The pipeline that turns the data of the Studio into a release of the design system: <c>Check</c> (every check of <see cref="DesignChecks"/> must pass) → <c>Version</c> (the number must be
/// new and higher; the changelog is what the Studio changed since the last release) → <c>Build</c> (the toolkit files, the design data and the archive, written with their hashes) → <c>Publish</c>
/// (the release is stored under its version and served) → <c>Notify</c> (the published files are read back and checked, and the addresses an app takes the release from are named). A stage that
/// fails stops the run and the later stages are skipped; nothing is published unless Publish ran. A release is never changed or deleted by the site: apps may depend on it.
/// </summary>
public sealed partial class ReleaseService(StudioService studio, DesignData design, DesignChecks checks, ToolkitFiles toolkit, IconLibrary iconLibrary, AssetFiles assetFiles)
{
    public static readonly IReadOnlyList<string> StageNames = ["Check", "Version", "Build", "Publish", "Notify"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] ToolkitKinds = ["apps", "rules", "components", Brand.Kind, Tenant.Kind, BrandAssets.Kind];

    private readonly object _running = new();

    [GeneratedRegex(@"^(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,3})$")]
    private static partial Regex VersionPattern();

    public static bool IsVersion(string? version) => version is not null && VersionPattern().IsMatch(version);

    private static Version Parse(string version) => System.Version.Parse(version);

    /// <summary>The directory a release is kept in.</summary>
    public string DirectoryOf(string version) => Path.Combine(studio.DataDirectory, "releases", version);

    /// <summary>Every release, the highest version first.</summary>
    public IReadOnlyList<ReleaseManifest> Releases()
    {
        if (!studio.Enabled)
        {
            return [];
        }

        var list = new List<ReleaseManifest>();
        foreach (var stored in studio.Store.List("releases"))
        {
            try
            {
                if (stored.Revision.Data.Deserialize<ReleaseManifest>(Json) is { } manifest && IsVersion(manifest.Version) && manifest.Version == stored.Id && Directory.Exists(DirectoryOf(manifest.Version)))
                {
                    list.Add(manifest);
                }
            }
            catch (JsonException)
            {
                // not a release any more: it is not offered
            }
        }

        return list.OrderByDescending(m => Parse(m.Version)).ToList();
    }

    public ReleaseManifest? Latest => Releases().FirstOrDefault();

    public ReleaseManifest? Get(string? version) => IsVersion(version) ? Releases().FirstOrDefault(m => m.Version == version) : null;

    /// <summary>Why a version cannot be released; null when it can: a version is three numbers, higher than the last release.</summary>
    public string? CheckVersion(string? version)
    {
        if (!IsVersion(version))
        {
            return "A version is three numbers with dots, like 1.4.0.";
        }

        var releases = Releases();
        if (releases.Any(m => m.Version == version))
        {
            return $"Version {version} is released already. A release never changes.";
        }

        if (Parse(version!) < Parse(AppVersion.MatUi))
        {
            return $"The toolkit is at {AppVersion.MatUi}: a release carries its files and is not lower than that.";
        }

        if (releases.Count > 0 && Parse(version!) <= Parse(releases[0].Version))
        {
            return $"The last release is {releases[0].Version}: the next version is higher.";
        }

        return null;
    }

    /// <summary>The version the next release would most likely get: the last one with the patch number raised (or the version of the toolkit, when that is higher), or the version of the toolkit for the first.</summary>
    public string SuggestNext()
    {
        if (Latest is not { } latest)
        {
            return AppVersion.MatUi;
        }

        var v = Parse(latest.Version);
        var next = $"{v.Major}.{v.Minor}.{v.Build + 1}";
        return Parse(next) < Parse(AppVersion.MatUi) ? AppVersion.MatUi : next;
    }

    /// <summary>The runs, the newest first.</summary>
    public IReadOnlyList<PipelineRun> Runs(int take = 20)
    {
        if (!studio.Enabled)
        {
            return [];
        }

        var runs = new List<PipelineRun>();
        foreach (var stored in studio.Store.List("runs").OrderByDescending(i => i.Id, StringComparer.Ordinal).Take(take))
        {
            try
            {
                if (stored.Revision.Data.Deserialize<PipelineRun>(Json) is { } run)
                {
                    runs.Add(run);
                }
            }
            catch (JsonException)
            {
                // not a run any more
            }
        }

        return runs;
    }

    public PipelineRun? Run(string? id) => studio.Enabled && StudioStore.IsId(id) ? studio.Store.Get<PipelineRun>("runs", id!) : null;

    private string NextRunId()
    {
        var highest = studio.Store.List("runs", includeDeleted: true)
            .Select(i => i.Id.Length > 1 && int.TryParse(i.Id[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"r{highest + 1:000000}";
    }

    /// <summary>A file of a release by its path, for the address that serves it; null for anything that is not a file inside the release.</summary>
    public (string Path, string ContentType)? FileOf(string? version, string? file)
    {
        if (!studio.Enabled || !IsVersion(version) || string.IsNullOrEmpty(file) || file.Contains("..", StringComparison.Ordinal) || file.Contains('\\') || file.StartsWith('/'))
        {
            return null;
        }

        var root = Path.GetFullPath(DirectoryOf(version!)) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, file));
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
        {
            return null;
        }

        var type = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".css" => "text/css; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".svg" => "image/svg+xml; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".html" => "text/html; charset=utf-8",
            ".zip" => "application/zip",
            ".txt" => "text/plain; charset=utf-8",
            ".md" => "text/markdown; charset=utf-8",
            ".png" => "image/png",
            ".ico" => "image/x-icon",
            ".woff2" => "font/woff2",
            ".woff" => "font/woff",
            _ => null,
        };
        return type is null ? null : (path, type);
    }

    /// <summary>
    /// Runs the pipeline for <paramref name="version"/> and keeps the run, whatever happened. Only one run at a time: a second call while one is running throws.
    /// </summary>
    public PipelineRun Execute(string version, string note, string by)
    {
        if (!Monitor.TryEnter(_running))
        {
            throw new InvalidOperationException("A release is being made. Wait for it to end.");
        }

        try
        {
            var runId = NextRunId();
            var at = DateTimeOffset.UtcNow;
            var stages = new List<PipelineStage>();
            string? buildDirectory = null;
            string? changelog = null;
            var changes = new List<string>();
            var files = new List<ReleaseFile>();
            CheckReport? report = null;
            ReleaseManifest? manifest = null;
            var releaseHash = toolkit.Hash;
            WhiteLabelSetting? appliedWhiteLabel = null;
            var failed = false;

            void Stage(string name, Func<List<string>, bool> run)
            {
                if (failed)
                {
                    stages.Add(new PipelineStage(name, StageStatus.Skipped, ["Not run: an earlier stage failed."], 0));
                    return;
                }

                var log = new List<string>();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                bool ok;
                try
                {
                    ok = run(log);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    log.Add("Error: " + ex.Message);
                    ok = false;
                }

                stages.Add(new PipelineStage(name, ok ? StageStatus.Succeeded : StageStatus.Failed, log, (int)clock.ElapsedMilliseconds));
                failed = !ok;
            }

            Stage("Check", log =>
            {
                report = checks.Run();
                foreach (var result in report.Results)
                {
                    log.Add($"{(result.Errors > 0 ? "FAIL" : result.Advice > 0 ? "advice" : "pass")}  {result.Check.Name}{(result.Errors > 0 ? $": {result.Errors} error(s)" : result.Advice > 0 ? $": {result.Advice} piece(s) of advice" : string.Empty)}");
                }

                foreach (var finding in report.Findings.Where(f => f.Severity == Severity.Error).Take(20))
                {
                    log.Add("  - " + finding.Message);
                }

                log.Add(report.Passed ? "Every check passes." : $"{report.Errors} error(s): a release needs every check to pass.");
                return report.Passed;
            });

            Stage("Version", log =>
            {
                if (CheckVersion(version) is { } problem)
                {
                    log.Add(problem);
                    return false;
                }

                log.Add($"Version {version} is new" + (Latest is { } last ? $" and higher than {last.Version}." : ": it is the first release."));
                changes = ChangesSince(Latest?.At);
                // the header of the changelog names the company whose design this is (stage 14)
                changelog = changes.Count == 0 ? $"{design.Tenant.SiteName}: no change in the Studio since the last release." : $"{design.Tenant.SiteName}: {changes.Count} change(s) in the Studio since the last release.";
                log.Add(changelog);
                log.AddRange(changes.Take(20).Select(c => "  - " + c));
                return true;
            });

            Stage("Build", log =>
            {
                buildDirectory = Path.Combine(studio.DataDirectory, "releases", $".building-{Guid.NewGuid():N}");
                Directory.CreateDirectory(buildDirectory);
                void Write(string path, byte[] bytes)
                {
                    var target = Path.Combine(buildDirectory, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, bytes);
                    files.Add(new ReleaseFile(path.Replace('\\', '/'), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
                    log.Add($"{path}  {bytes.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes");
                }

                var assembled = AssembleFiles(version);

                // a company's release carries its own prefix and name (stage 15): the copy is renamed here and checked before anything is written
                if (WhiteLabelStore.Active(studio) is { } whiteLabel)
                {
                    var built = WhiteLabelBuild.Apply(assembled, whiteLabel, toolkit.Hash);
                    LogWhiteLabel(log, built);
                    if (!built.Passed)
                    {
                        return false;
                    }

                    assembled = built.Files.ToList();
                    releaseHash = built.Hash;
                    appliedWhiteLabel = whiteLabel;
                }

                foreach (var file in assembled)
                {
                    Write(file.Path, file.Bytes);
                }

                // the files of the brand (IDENT-08), written as they are (bytes, never renamed by a white label build): logo, word mark, favicon, app icon and fonts, under brand/ (the @font-face rules of matui.css are relative to it), with their hashes in the manifest
                var brandFiles = assetFiles.Files();
                foreach (var file in brandFiles)
                {
                    Write($"{BrandAssets.Folder}/{file.Name}", file.Bytes);
                }

                log.Add(brandFiles.Count == 0 ? "No brand assets saved: nothing under brand/." : $"Brand assets: {brandFiles.Count} file(s) under brand/ ({design.AssetsOrEmpty.Faces.Count} font face(s)).");
                return true;
            });

            Stage("Publish", log =>
            {
                var directory = DirectoryOf(version);
                if (Directory.Exists(directory))
                {
                    log.Add($"The directory for {version} exists already.");
                    return false;
                }

                manifest = new ReleaseManifest(version, DateTimeOffset.UtcNow, by, note, releaseHash, runId, report!.Errors, report.Advice, design.Apps.Count, design.Rules.Count, design.Components.Count, changes, files, AppVersion.MatUi, design.Tenant.Company, appliedWhiteLabel);
                File.WriteAllText(Path.Combine(buildDirectory!, "manifest.json"), JsonSerializer.Serialize(manifest, Json), new UTF8Encoding(false));
                Directory.Move(buildDirectory!, directory);
                buildDirectory = null;
                studio.Store.Save("releases", version, manifest, by, note.Length == 0 ? $"Release {version}" : note);
                design.Reload();
                log.Add($"Published as {version}: {files.Count + 1} files in {directory}.");
                return true;
            });

            Stage("Notify", log =>
            {
                var published = Get(version);
                if (published is null)
                {
                    log.Add("The release is not found after it was published.");
                    return false;
                }

                foreach (var file in published.Files)
                {
                    var path = Path.Combine(DirectoryOf(version), file.Path);
                    if (!File.Exists(path) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() != file.Sha256)
                    {
                        log.Add($"{file.Path} does not match its hash.");
                        return false;
                    }
                }

                log.Add($"All {published.Files.Count} files read back with the hash in the manifest.");
                log.Add($"An app takes it from /releases/{version}/ and learns that it is the newest from /releases/latest.json (now {Latest?.Version}).");
                return true;
            });

            if (buildDirectory is not null && Directory.Exists(buildDirectory))
            {
                Directory.Delete(buildDirectory, recursive: true);
            }

            var run = new PipelineRun(runId, version, note, by, at, !failed, stages);
            studio.Store.Save("runs", runId, run, by, $"Release {version}: {(run.Succeeded ? "released" : "failed")}");
            return run;
        }
        finally
        {
            Monitor.Exit(_running);
        }
    }

    /// <summary>What the Studio changed since the last release (everything, when there is none yet): what the changelog of the next release would say.</summary>
    public IReadOnlyList<string> PendingChanges() => ChangesSince(Latest?.At);

    /// <summary>What the Studio changed since <paramref name="since"/> (everything, when there is no release yet): one line per revision of an app, a rule or a component.</summary>
    private List<string> ChangesSince(DateTimeOffset? since)
        => studio.Store.Activity(int.MaxValue)
            .Where(i => ToolkitKinds.Contains(i.Kind) && (since is null || i.Revision.At > since))
            .OrderBy(i => i.Revision.At)
            .Select(i => $"{StudioFormat.KindName(i.Kind)} {i.Id}: {StudioFormat.What(i.Revision).ToLowerInvariant()}{(i.Revision.Note.Length > 0 && i.Revision.Note != StudioFormat.What(i.Revision) ? ", " + i.Revision.Note : string.Empty)}")
            .ToList();

    /// <summary>The files of a release as the toolkit and the design data give them now, in the order the Build stage writes them (the names of the source; a white label build renames them).</summary>
    private List<BuildFile> AssembleFiles(string version)
    {
        var files = new List<BuildFile>();
        static byte[] Text(string text) => new UTF8Encoding(false).GetBytes(text);
        void Add(string path, byte[] bytes) => files.Add(new BuildFile(path, bytes));

        Add("matui.css", Text(toolkit.ReleaseBundle(version)));
        Add("matui.js", Text(toolkit.ReleaseScript(version)));
        Add("matui.boot.js", Text(toolkit.ReleaseBootScript(version)));
        Add("CHANGELOG.md", Text(design.ChangelogText));
        Add("matui-icons.svg", Text(toolkit.Sprite));
        foreach (var part in ToolkitFiles.CssParts)
        {
            Add($"css/{part}", Text(toolkit.Css(part)!));
        }

        Add($"matui-{version}.zip", toolkit.ReleaseZip(version));

        // the optional icon library, in a folder of its own: an app that does not need it never has to look there
        Add("iconlib/matui-iconlib.svg", Text(iconLibrary.Sprite));
        Add("iconlib/iconlib.json", Text(iconLibrary.Catalogue));
        Add("iconlib/LICENSE.txt", Text(iconLibrary.License));
        Add("design.json", Text(DesignJson()));
        Add(Styleguide.CssName, Text(Styleguide.Css()));
        Add(Styleguide.SampleName, Text(Styleguide.SampleImage()));
        Add(Styleguide.HtmlName, Text(Styleguide.Html(design, toolkit, version, toolkit.Hash)));
        return files;
    }

    /// <summary>The log of the white label step of the Build stage: which rename was applied, how often per file, and what the checks found.</summary>
    private static void LogWhiteLabel(List<string> log, WhiteLabelBuildResult built)
    {
        log.Add($"White label: {WhiteLabel.Describe(built.Setting, built.Totals)}.");
        foreach (var file in built.Report.Where(f => f.Replacements > 0))
        {
            log.Add($"  {file.Path}: {StudioFormat.Count(file.Replacements, "replacement")}");
        }

        log.Add($"The hash of the copy is {built.Hash} (the toolkit's is {built.SourceHash}).");
        log.AddRange(WhiteLabelChecks.Lines(built.Findings).Take(40).Select(l => "  ! " + l));
        log.Add(built.Passed
            ? $"The checks of the toolkit run on the copy: {WhiteLabelChecks.Summary(built.Findings)}"
            : "The copy is not released: " + WhiteLabelChecks.Summary(built.Findings));
    }

    /// <summary>
    /// A trial of the white label build: the renamer and every check on the files of the next release as they are now, without making a release. Used by the page of the setting and by the check of the Studio.
    /// </summary>
    public WhiteLabelBuildResult TrialBuild(WhiteLabelSetting setting) => WhiteLabelBuild.Apply(AssembleFiles(SuggestNext()), setting, toolkit.Hash);

    /// <summary>The design data of this moment as JSON: what an app or a tool can read without the site.</summary>
    private string DesignJson()
    {
        var data = new Dictionary<string, object>
        {
            ["tenant"] = new { design.Tenant.Company, design.Tenant.ShortName, design.Tenant.SiteName, design.Tenant.Tagline, template = design.Tenant.Template },
            ["brand"] = new
            {
                design.Brand.Company, design.Brand.ShortName, design.Brand.Tagline,
                light = design.Brand.Values(false), dark = design.Brand.Values(true),
                components = design.Brand.ComponentValues,
                schemes = (design.Brand.Schemes ?? new Dictionary<string, SchemeValues>()).Where(s => s.Value.HasValues).ToDictionary(s => s.Key, s => new { light = s.Value.Values(false), dark = s.Value.Values(true) }),
            },
            ["apps"] = design.Apps.Select(a => new
            {
                a.Id, a.Name, a.Tagline, a.Category, a.Accent, onAccent = a.OnAccent ?? "#ffffff", a.AccentText, a.Glyph, a.Port,
                overrides = a.HasOverrides ? new { light = a.Tokens?.Values(false), dark = a.Tokens?.Values(true), properties = a.Properties } : null,
            }),
            ["rules"] = design.Rules.Select(r => new { r.Id, r.Area, r.Title, r.Statement, level = r.Level.ToString(), r.Source, r.Why, r.Do, r.Dont, r.Check }),
            ["components"] = design.Components.Select(c => new
            {
                c.Id, c.Name, c.Group, c.Summary, c.Rules, c.Behaviour,
                examples = c.Examples.Select(e => new { e.Title, e.Html, e.Note, e.Js, e.Live, e.Frame }),
            }),
        };

        // the brand's assets (stage 13): only when there are some, so a release without them has the design.json it always had. Files are named as they lie in the release, under brand/.
        if (design.Assets is { IsEmpty: false } assets)
        {
            string? Public(AssetFile? file, string slot) => file is null ? null : $"{BrandAssets.Folder}/{BrandAssets.PublicName(slot, file)}";
            data["assets"] = new
            {
                logo = assets.Logo is null ? null : new { file = Public(assets.Logo, "logo"), dark = Public(assets.LogoDark, "logo-dark"), alt = assets.LogoAlt, assets.Logo.Width, assets.Logo.Height },
                wordmark = assets.Wordmark is null ? null : new { file = Public(assets.Wordmark, "wordmark"), dark = Public(assets.WordmarkDark, "wordmark-dark"), alt = assets.WordmarkAlt, assets.Wordmark.Width, assets.Wordmark.Height },
                favicon = Public(assets.Favicon, "favicon"),
                appIcon = Public(assets.AppIcon, "app-icon"),
                fonts = assets.Faces.Select(f => new { f.Family, f.Weight, f.Style, file = $"{BrandAssets.Folder}/{BrandAssets.PublicName(f)}" }),
            };
        }

        return JsonSerializer.Serialize(data, Json);
    }
}
