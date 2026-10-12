using System.Text.RegularExpressions;

namespace MatDesign.Core;

/// <summary>One kind of change under a version: <c>Added</c>, <c>Changed</c>, <c>Fixed</c>, <c>Breaking</c> ... and its bullet points.</summary>
public sealed record ChangelogSection(string Name, IReadOnlyList<string> Items);

/// <summary>
/// One version in the changelog (<c>## [0.2.0] - 2026-10-12</c>) or the changes that are not released yet (<c>## [Unreleased]</c>). The sections follow Keep a Changelog
/// (Added, Changed, Deprecated, Removed, Fixed, Security) and add two of the design system: <c>Breaking</c> (what an app has to change in its own code when it takes this version)
/// and <c>Migration</c> (the steps for it).
/// </summary>
public sealed record ChangelogEntry(string Version, string? Date, string Note, IReadOnlyList<ChangelogSection> Sections)
{
    public const string UnreleasedName = "Unreleased";

    public bool IsUnreleased => Version.Equals(UnreleasedName, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> Section(string name)
        => Sections.Where(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).SelectMany(s => s.Items).ToList();

    /// <summary>True when an app has to change something of its own for this version.</summary>
    public bool Breaking => Section("Breaking").Count > 0;

    /// <summary>The steps an app takes to update to this version (empty when there is nothing to do but copy the files).</summary>
    public IReadOnlyList<string> Migration => Section("Migration");

    public int ItemCount => Sections.Sum(s => s.Items.Count);
}

/// <summary>
/// The changelog of the toolkit MatUi (<c>CHANGELOG.md</c>, embedded in the build): what changed in which version, so that an app that copies the toolkit knows what it
/// takes with a new version and what it has to do. The unit tests hold the file to its format and its newest released version to <see cref="AppVersion.MatUi"/>.
/// </summary>
public sealed partial class Changelog
{
    /// <summary>The names a section may have.</summary>
    public static readonly IReadOnlyList<string> SectionNames = ["Added", "Changed", "Deprecated", "Removed", "Fixed", "Security", "Breaking", "Migration"];

    [GeneratedRegex(@"^##\s+\[(?<v>[^\]]+)\](?:\s+-\s+(?<d>\d{4}-\d{2}-\d{2}))?\s*$")]
    private static partial Regex EntryHeading();

    [GeneratedRegex(@"^###\s+(?<n>.+?)\s*$")]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionPattern();

    public Changelog(IReadOnlyList<ChangelogEntry> entries) => Entries = entries;

    /// <summary>Every entry, in the order of the file (the newest first).</summary>
    public IReadOnlyList<ChangelogEntry> Entries { get; }

    /// <summary>What is not released yet, or null.</summary>
    public ChangelogEntry? Unreleased => Entries.FirstOrDefault(e => e.IsUnreleased);

    /// <summary>The released versions, the highest first.</summary>
    public IReadOnlyList<ChangelogEntry> Releases
        => Entries.Where(e => !e.IsUnreleased && IsVersion(e.Version)).OrderByDescending(e => Parse(e.Version)).ToList();

    /// <summary>The newest released version, or null when there is none.</summary>
    public ChangelogEntry? Latest => Releases.FirstOrDefault();

    public static bool IsVersion(string? version) => version is not null && VersionPattern().IsMatch(version);

    public static Version Parse(string version) => System.Version.Parse(version);

    /// <summary>-1, 0 or 1 like <see cref="IComparable"/>: compares two versions of three numbers.</summary>
    public static int Compare(string a, string b) => Parse(a).CompareTo(Parse(b));

    /// <summary>The released versions that are newer than <paramref name="version"/>, the highest first: what an app that carries <paramref name="version"/> takes with an update.</summary>
    public IReadOnlyList<ChangelogEntry> Since(string version)
        => IsVersion(version) ? Releases.Where(e => Compare(e.Version, version) > 0).ToList() : Releases;

    /// <summary>Everything that is wrong with the file: the format, the order, versions twice, a break without steps. Empty when it is as it should be.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previous = null;
        foreach (var entry in Entries)
        {
            if (!entry.IsUnreleased && !IsVersion(entry.Version))
            {
                problems.Add($"'{entry.Version}' is not a version of three numbers.");
                continue;
            }

            if (!names.Add(entry.Version))
            {
                problems.Add($"Version {entry.Version} is in the changelog twice.");
            }

            if (entry.IsUnreleased && !ReferenceEquals(Entries[0], entry))
            {
                problems.Add("Unreleased is the first entry.");
            }

            if (!entry.IsUnreleased)
            {
                if (previous is not null && Compare(entry.Version, previous) >= 0)
                {
                    problems.Add($"Version {entry.Version} is not below {previous}: the newest version comes first.");
                }

                previous = entry.Version;
                if (entry.ItemCount == 0)
                {
                    problems.Add($"Version {entry.Version} says nothing about what changed.");
                }
            }

            foreach (var section in entry.Sections)
            {
                if (!SectionNames.Contains(section.Name, StringComparer.OrdinalIgnoreCase))
                {
                    problems.Add($"{entry.Version}: '{section.Name}' is not a section ({string.Join(", ", SectionNames)}).");
                }
                else if (section.Items.Count == 0)
                {
                    problems.Add($"{entry.Version}: the section {section.Name} is empty.");
                }
            }

            if (entry.Breaking && entry.Migration.Count == 0)
            {
                problems.Add($"{entry.Version} breaks something and has no Migration: tell an app what to do.");
            }
        }

        return problems;
    }

    /// <summary>The changelog that this build carries.</summary>
    public static Changelog Load()
    {
        using var stream = typeof(Changelog).Assembly.GetManifestResourceStream("CHANGELOG.md")
            ?? throw new InvalidOperationException("CHANGELOG.md is not embedded in MatDesign.Core.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd(), out _);
    }

    /// <summary>The text of the changelog as it is in the file, for serving it.</summary>
    public static string LoadText()
    {
        using var stream = typeof(Changelog).Assembly.GetManifestResourceStream("CHANGELOG.md")
            ?? throw new InvalidOperationException("CHANGELOG.md is not embedded in MatDesign.Core.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Reads the markdown of a changelog. <paramref name="lines"/> counts the lines that were not understood (text between entries is ignored).</summary>
    public static Changelog Parse(string markdown, out int lines)
    {
        lines = 0;
        var entries = new List<ChangelogEntry>();
        string? version = null, date = null;
        var note = new List<string>();
        var sections = new List<(string Name, List<string> Items)>();
        (string Name, List<string> Items)? section = null;

        void Close()
        {
            if (version is not null)
            {
                entries.Add(new ChangelogEntry(version, date, string.Join(' ', note), sections.Select(s => new ChangelogSection(s.Name, s.Items)).ToList()));
            }

            version = null;
            date = null;
            note = [];
            sections = [];
            section = null;
        }

        foreach (var raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            var heading = EntryHeading().Match(line);
            if (heading.Success)
            {
                Close();
                version = heading.Groups["v"].Value.Trim();
                date = heading.Groups["d"].Success ? heading.Groups["d"].Value : null;
                continue;
            }

            if (version is null)
            {
                continue;
            }

            var sectionHeading = SectionHeading().Match(line);
            if (sectionHeading.Success)
            {
                section = (sectionHeading.Groups["n"].Value, []);
                sections.Add(section.Value);
                continue;
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (section is null)
            {
                note.Add(line.Trim());
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                section.Value.Items.Add(line[2..].Trim());
            }
            else if (char.IsWhiteSpace(line[0]) && section.Value.Items.Count > 0)
            {
                section.Value.Items[^1] += " " + line.Trim();
            }
            else
            {
                lines++;
            }
        }

        Close();
        return new Changelog(entries);
    }
}
