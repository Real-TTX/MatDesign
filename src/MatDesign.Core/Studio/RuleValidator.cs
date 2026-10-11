using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// What a rule of the rulebook has to be, checked before the Studio saves it: an id of its area that nobody else has, a title and a statement that say something,
/// a level, a source. The check of a rule (a test, a report, a reviewer) is not typed: only code can enforce a rule, so a rule from the Studio is checked by a reviewer
/// and a built-in rule keeps what the code says.
/// </summary>
public static partial class RuleValidator
{
    [GeneratedRegex(@"^[A-Z][A-Z0-9]{1,7}-\d{2,3}$")]
    private static partial Regex IdPattern();

    /// <summary>The rule as it is stored: trimmed, the id in capitals, an empty optional text as null, a rule from the Studio checked by review.</summary>
    public static Rule Normalize(Rule rule)
    {
        static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        return rule with
        {
            Id = (rule.Id ?? string.Empty).Trim().ToUpperInvariant(),
            Area = (rule.Area ?? string.Empty).Trim().ToUpperInvariant(),
            Title = (rule.Title ?? string.Empty).Trim(),
            Statement = (rule.Statement ?? string.Empty).Trim(),
            Source = (rule.Source ?? string.Empty).Trim(),
            Why = Optional(rule.Why),
            Do = Optional(rule.Do),
            Dont = Optional(rule.Dont),
            Check = string.IsNullOrWhiteSpace(rule.Check) ? "Review" : rule.Check.Trim(),
        };
    }

    /// <summary>
    /// Everything that is wrong with <paramref name="rule"/>, which must be normalised. <paramref name="others"/> are the other rules: all of them for a new rule,
    /// all but the one being edited otherwise.
    /// </summary>
    public static IReadOnlyList<Problem> Validate(Rule rule, IEnumerable<Rule> others)
    {
        var rest = others.ToList();
        var problems = new List<Problem>();

        if (RuleBook.Areas.All(a => a.Id != rule.Area))
        {
            problems.Add(new("Area", "Choose one of the areas."));
        }

        if (!IdPattern().IsMatch(rule.Id))
        {
            problems.Add(new("Id", "An id is the area, a dash and two or three digits, like FORM-17."));
        }
        else if (RuleBook.Areas.Any(a => a.Id == rule.Area) && !rule.Id.StartsWith(rule.Area + "-", StringComparison.Ordinal))
        {
            problems.Add(new("Id", $"The id of a rule of this area starts with {rule.Area}-, like {rule.Area}-17."));
        }
        else if (rest.Any(r => r.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(new("Id", "A rule with this id exists."));
        }

        if (rule.Title.Length is < 3 or > 80)
        {
            problems.Add(new("Title", "Say in 3 to 80 characters what the rule is about."));
        }

        if (rule.Statement.Length is < 10 or > 1200)
        {
            problems.Add(new("Statement", "State the rule in 10 to 1200 characters."));
        }

        if (!Enum.IsDefined(rule.Level))
        {
            problems.Add(new("Level", "Choose Must, Should or May."));
        }

        if (rule.Source.Length is < 2 or > 60)
        {
            problems.Add(new("Source", "Say where the rule comes from."));
        }

        foreach (var (field, text) in new[] { ("Why", rule.Why), ("Do", rule.Do), ("Dont", rule.Dont) })
        {
            if (text is { Length: > 400 })
            {
                problems.Add(new(field, "Keep it to 400 characters."));
            }
        }

        return problems;
    }

    /// <summary>True when the rule can be shown and cited without harm: a known area, an id of that area, a level, a title and a statement. Checked again for what is read from the disk.</summary>
    public static bool IsSafe(Rule rule)
        => IdPattern().IsMatch(rule.Id) && rule.Id.StartsWith(rule.Area + "-", StringComparison.Ordinal) && RuleBook.Areas.Any(a => a.Id == rule.Area)
            && Enum.IsDefined(rule.Level) && !string.IsNullOrWhiteSpace(rule.Title) && !string.IsNullOrWhiteSpace(rule.Statement) && !string.IsNullOrWhiteSpace(rule.Source);

    /// <summary>The id a new rule of an area would get: the next number after the highest one of the area.</summary>
    public static string NextId(string area, IEnumerable<Rule> all)
    {
        var highest = all
            .Where(r => r.Area == area && r.Id.StartsWith(area + "-", StringComparison.Ordinal) && int.TryParse(r.Id[(area.Length + 1)..], out _))
            .Select(r => int.Parse(r.Id[(area.Length + 1)..], System.Globalization.CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();
        return $"{area}-{highest + 1:00}";
    }
}
