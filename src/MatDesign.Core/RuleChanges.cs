using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MatDesign.Core;

/// <summary>What happened to a rule. Stored by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleChangeKind
{
    /// <summary>The rule is in the rulebook from this day on.</summary>
    Added,

    /// <summary>The statement, the level or the source of the rule changed (or its wording in a way that a reader of the rulebook should know).</summary>
    Changed,

    /// <summary>The rule is gone. Its id is never used again.</summary>
    Removed,

    /// <summary>The rule lives under another id now (the note names it). The old id is gone, like a removed one.</summary>
    Moved,
}

/// <summary>
/// One line of the changelog of the rulebook: on <see cref="Date"/> the rule <see cref="RuleId"/> was added, changed, removed or moved, with a short <see cref="Note"/>.
/// <see cref="Hash"/> is the <see cref="RuleChanges.HashOf(Rule)"/> of the rule as it was after the change: the newest entry of a rule must carry the hash of its current text,
/// so a rule cannot change without a line here (a test fails).
/// </summary>
public sealed record RuleChange(DateOnly Date, string RuleId, RuleChangeKind Kind, string Note, string Hash)
{
    /// <summary>The date as written in the data and in the JSON: 2026-10-20.</summary>
    public string DateText => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>True for the kinds that end the life of an id: the rule is not under this id any more.</summary>
    public bool EndsTheId => Kind is RuleChangeKind.Removed or RuleChangeKind.Moved;
}

/// <summary>
/// The changelog of the rulebook (docs/VERSIONING.md, "The rulebook has a changelog too"): what was added to the rules, changed in them or taken out, and when. The rules are the product,
/// so a change of a rule is written down like a change of the toolkit is in CHANGELOG.md. The data is code (<c>RuleChangeEntries.cs</c>, oldest first: a new line goes at the end) and
/// is held to the rulebook by a unit test: <see cref="Problems(IReadOnlyList{Rule}, IReadOnlyList{RuleChange}, DateOnly)"/> is everything that is wrong with it.
/// Rules that were written in the Studio keep their own history there; they are not in this list.
/// </summary>
public static partial class RuleChanges
{
    /// <summary>The longest note: a short sentence, not a paragraph.</summary>
    public const int MaxNote = 200;

    [GeneratedRegex(@"^[A-Z][A-Z0-9]{1,7}-\d{2,3}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^[0-9a-f]{8}$")]
    private static partial Regex HashPattern();

    [GeneratedRegex(@"\b([A-Z][A-Z0-9]{1,7}-\d{2,3})\b")]
    private static partial Regex CitedId();

    /// <summary>Every line of the changelog of the rulebook, oldest first, as written in <c>RuleChangeEntries.cs</c>.</summary>
    public static IReadOnlyList<RuleChange> All { get; } = Entries();

    /// <summary>
    /// The short hash of what makes a rule a different rule: the first eight hex digits of the SHA-256 of its statement, its level and its source, joined by line feeds.
    /// <c>node tools/rule-hash.js ID</c> computes the same from the text of RuleBook.cs and prints the line to add; a test holds the two together.
    /// </summary>
    public static string HashOf(Rule rule) => HashOf(rule.Statement, rule.Level, rule.Source);

    /// <summary>The hash of a statement, a level and a source (see <see cref="HashOf(Rule)"/>).</summary>
    public static string HashOf(string statement, RuleLevel level, string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{statement}\n{level}\n{source}")))[..8].ToLowerInvariant();

    /// <summary>The lines about one rule, oldest first.</summary>
    public static IReadOnlyList<RuleChange> For(string ruleId)
        => All.Where(c => c.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Everything that is wrong with the changelog of the rulebook against the rules (empty when it is as it should be). Uses the built-in rules, the built-in lines and today.</summary>
    public static IReadOnlyList<string> Problems() => Problems(RuleBook.All, All, DateOnly.FromDateTime(DateTime.UtcNow));

    /// <summary>
    /// Everything that is wrong with <paramref name="entries"/> against <paramref name="rules"/>: a rule without a line, a line for a rule that does not exist, a rule whose newest line does not carry the
    /// hash of its text (it changed, so a line has to say so: the message gives the line to add), lines out of order, dates that are not days, notes that are not short sentences.
    /// </summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<Rule> rules, IReadOnlyList<RuleChange> entries, DateOnly today)
    {
        var problems = new List<string>();
        var current = rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var areas = RuleBook.Areas.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);

        DateOnly? previous = null;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var where = $"line {i + 1} ({entry.DateText} {entry.RuleId} {entry.Kind})";
            if (!IdPattern().IsMatch(entry.RuleId) || !areas.Contains(entry.RuleId[..entry.RuleId.LastIndexOf('-')]))
            {
                problems.Add($"{where}: '{entry.RuleId}' is not the id of a rule of a known area.");
            }

            if (!Enum.IsDefined(entry.Kind))
            {
                problems.Add($"{where}: the kind is not Added, Changed, Removed or Moved.");
            }

            if (!HashPattern().IsMatch(entry.Hash))
            {
                problems.Add($"{where}: the hash '{entry.Hash}' is not eight lower-case hex digits (node tools/rule-hash.js {entry.RuleId}).");
            }

            if (entry.Date > today.AddDays(1) || entry.Date < new DateOnly(2026, 1, 1))
            {
                problems.Add($"{where}: the date is not a day between 2026-01-01 and today.");
            }

            if (previous is { } before && entry.Date < before)
            {
                problems.Add($"{where}: the lines are oldest first, and this one is older than the line before it ({before:yyyy-MM-dd}). Put a new line at the end.");
            }

            previous = entry.Date;
            problems.AddRange(NoteProblems(entry, where, current));
        }

        foreach (var group in entries.GroupBy(e => e.RuleId, StringComparer.Ordinal))
        {
            var id = group.Key;
            var lines = group.ToList();
            if (lines[0].Kind != RuleChangeKind.Added)
            {
                problems.Add($"{id}: the first line about the rule must be Added, not {lines[0].Kind}.");
            }

            if (lines.Count(l => l.Kind == RuleChangeKind.Added) > 1)
            {
                problems.Add($"{id}: Added twice. An id is used once; a rule that comes back has a new id.");
            }

            var ended = lines.FindIndex(l => l.EndsTheId);
            if (ended >= 0 && ended < lines.Count - 1)
            {
                problems.Add($"{id}: there is a line after the rule was {lines[ended].Kind}. An id that is gone stays gone.");
            }

            var newest = lines[^1];
            if (current.TryGetValue(id, out var rule))
            {
                if (newest.EndsTheId)
                {
                    problems.Add($"{id}: the newest line says {newest.Kind}, but the rule is in the rulebook.");
                }
                else if (newest.Hash != HashOf(rule))
                {
                    problems.Add(Stale(rule, newest, today));
                }
            }
            else if (!newest.EndsTheId)
            {
                problems.Add($"{id}: there is no such rule any more. Add a line that says Removed (or Moved, with the new id in the note): {Line(newest.RuleId, today, RuleChangeKind.Removed, newest.Hash, "Say why it went.")}");
            }
        }

        var known = entries.Select(e => e.RuleId).ToHashSet(StringComparer.Ordinal);
        foreach (var rule in rules.Where(r => !known.Contains(r.Id)))
        {
            problems.Add($"{rule.Id}: the rule has no line in the changelog of the rulebook. Add: {Line(rule.Id, today, RuleChangeKind.Added, HashOf(rule), "Say in one short sentence what the rule is for.")}");
        }

        return problems;
    }

    private static string Stale(Rule rule, RuleChange newest, DateOnly today)
        => $"{rule.Id}: the text of the rule is not the one the newest line was written for (hash {newest.Hash}, now {HashOf(rule)}). A rule is changed together with a line of the changelog of the rulebook: add this at the end of RuleChangeEntries.cs "
            + $"(node tools/rule-hash.js {rule.Id} \"note\" prints it): {Line(rule.Id, today, RuleChangeKind.Changed, HashOf(rule), "Say in one short sentence what changed.")}";

    private static string Line(string id, DateOnly date, RuleChangeKind kind, string hash, string note)
    {
        var helper = kind switch { RuleChangeKind.Added => "Add", RuleChangeKind.Changed => "Chg", RuleChangeKind.Removed => "Rem", _ => "Mov" };
        return $"{helper}(\"{date:yyyy-MM-dd}\", \"{id}\", \"{hash}\", \"{note}\"),";
    }

    private static IEnumerable<string> NoteProblems(RuleChange entry, string where, IReadOnlyDictionary<string, Rule> current)
    {
        var note = entry.Note;
        if (note.Length == 0)
        {
            if (entry.Kind != RuleChangeKind.Added)
            {
                yield return $"{where}: a line that says {entry.Kind} needs a note: one short sentence that says what changed.";
            }

            yield break;
        }

        if (note.Length > MaxNote)
        {
            yield return $"{where}: the note has {note.Length} characters; keep it to {MaxNote}, a short sentence.";
        }

        if (note != note.Trim() || note.Contains('\n') || note.Contains('\r'))
        {
            yield return $"{where}: the note is one line without spaces at its ends.";
        }

        if (!char.IsUpper(note[0]) && !char.IsDigit(note[0]) && note[0] != '`')
        {
            yield return $"{where}: the note starts with a capital letter, like a sentence.";
        }

        if (note[^1] is not ('.' or '!' or '?'))
        {
            yield return $"{where}: the note ends with a full stop, like a sentence.";
        }

        if (entry.Kind == RuleChangeKind.Moved && !CitedId().Matches(note).Any(m => m.Value != entry.RuleId && current.ContainsKey(m.Value)))
        {
            yield return $"{where}: a Moved line names the new id of the rule in its note (an id that exists).";
        }
    }

    // ---- helpers for writing the data in RuleChangeEntries.cs: Add, Chg, Rem and Mov take the day, the id, the hash and the note

    private static RuleChange Make(string date, string id, RuleChangeKind kind, string hash, string note)
        => new(DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture), id, kind, note, hash);

    /// <summary>The rule is in the rulebook from this day on. The note is optional (the rule says what it is).</summary>
    private static RuleChange Add(string date, string id, string hash, string note = "") => Make(date, id, RuleChangeKind.Added, hash, note);

    /// <summary>The rule changed: <paramref name="hash"/> is the hash of the rule after the change.</summary>
    private static RuleChange Chg(string date, string id, string hash, string note) => Make(date, id, RuleChangeKind.Changed, hash, note);

    /// <summary>The rule is gone.</summary>
    private static RuleChange Rem(string date, string id, string hash, string note) => Make(date, id, RuleChangeKind.Removed, hash, note);

    /// <summary>The rule is under another id now (named in the note).</summary>
    private static RuleChange Mov(string date, string id, string hash, string note) => Make(date, id, RuleChangeKind.Moved, hash, note);
}
