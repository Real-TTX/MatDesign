using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Rules;

/// <summary>What the form of a rule sends. The level is text so that nonsense becomes a message at the field.</summary>
public sealed class RuleInput
{
    public string? Original { get; set; }

    public string? Id { get; set; }

    public string? Area { get; set; }

    public string? Title { get; set; }

    public string? Statement { get; set; }

    public string? Level { get; set; }

    public string? Source { get; set; }

    public string? Why { get; set; }

    public string? Do { get; set; }

    public string? Dont { get; set; }
}

/// <summary>
/// Creates, changes, resets and deletes a rule. A built-in rule that is saved becomes a changed one (the item replaces it); deleting that item brings the built-in rule
/// back. The id and the area of a rule that exists cannot change, because rules are cited by id in reviews, commits and the catalogue. Who checks a rule is not typed:
/// a rule from the Studio is checked by a reviewer, a built-in rule keeps what the code says.
/// </summary>
public class EditModel(StudioService studio, DesignData design) : StudioPageModel(studio)
{
    [BindProperty]
    public RuleInput Input { get; set; } = new();

    public bool IsNew => string.IsNullOrEmpty(Input.Original);

    public ItemSource Source { get; private set; }

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    public bool Checked { get; private set; }

    /// <summary>The rule as the form describes it now, for the preview.</summary>
    public Rule? Preview { get; private set; }

    /// <summary>Who checks the rule, as the code says for a built-in rule.</summary>
    public string CheckedBy => design.Rule(Input.Original)?.Check ?? "Review";

    public string? ProblemFor(string field) => Problems.FirstOrDefault(p => p.Field == field)?.Message;

    /// <summary>The sources to choose from: those the rulebook has, the Studio, and the one the rule has now.</summary>
    public IReadOnlyList<string> Sources => design.Rules.Select(r => r.Source).Append(RuleBook.Studio).Append(Input.Source ?? RuleBook.Studio).Distinct().Order(StringComparer.Ordinal).ToList();

    public IActionResult OnGet(string? id, string? area)
    {
        if (id is null)
        {
            var chosen = RuleBook.Areas.FirstOrDefault(a => a.Id == area)?.Id ?? RuleBook.Areas[0].Id;
            Input = new RuleInput { Area = chosen, Id = RuleValidator.NextId(chosen, design.Rules), Level = nameof(RuleLevel.Should), Source = RuleBook.Studio };
            Preview = null;
            return Page();
        }

        var rule = design.Rule(id);
        if (rule is null)
        {
            return NotFound();
        }

        Input = new RuleInput
        {
            Original = rule.Id, Id = rule.Id, Area = rule.Area, Title = rule.Title, Statement = rule.Statement, Level = rule.Level.ToString(),
            Source = rule.Source, Why = rule.Why, Do = rule.Do, Dont = rule.Dont,
        };
        Source = design.RuleSourceOf(rule.Id);
        Preview = rule;
        return Page();
    }

    /// <summary>Saves, or (with the Check button, <c>check</c>) only shows what the form would give: the problems and how the rule reads.</summary>
    public IActionResult OnPost(bool check = false)
    {
        var original = design.Rule(Input.Original);
        if (!string.IsNullOrEmpty(Input.Original) && original is null)
        {
            return NotFound();
        }

        Source = original is null ? ItemSource.Added : design.RuleSourceOf(original.Id);
        var rule = Describe(Input, original);
        Preview = rule;
        var others = design.Rules.Where(r => original is null || !r.Id.Equals(original.Id, StringComparison.OrdinalIgnoreCase));
        var problems = RuleValidator.Validate(rule, others).ToList();
        Checked = check;
        if (problems.Count > 0 || check)
        {
            Problems = problems;
            return Page();
        }

        var note = original is null ? "Created" : "Changed " + string.Join(", ", What(original, rule));
        Studio.Store.Save("rules", rule.Id, rule, Studio.Administrator, note);
        design.Reload();
        TempData["Flash"] = original is null ? $"{rule.Id} was added." : $"{rule.Id} was saved.";
        return RedirectToPage("/Studio/Rules/Index");
    }

    /// <summary>Deletes a rule that was added, or resets a built-in rule that was changed (the built-in rule is back).</summary>
    public IActionResult OnPostDelete()
    {
        var rule = design.Rule(Input.Original);
        if (rule is null)
        {
            return NotFound();
        }

        var source = design.RuleSourceOf(rule.Id);
        if (source == ItemSource.BuiltIn)
        {
            return BadRequest("A built-in rule cannot be deleted.");
        }

        // a rule that a component of the Studio names cannot go before the component lets go of it (a changed built-in rule only goes back to the built-in one, which keeps its id)
        if (source == ItemSource.Added)
        {
            var users = design.ComponentsNaming(rule.Id);
            if (users.Count > 0)
            {
                Source = source;
                Preview = rule;
                Problems = [new Problem("Id", $"{rule.Id} is named by {string.Join(", ", users)}. Take it out of the rules of the component first.")];
                return Page();
            }
        }

        Studio.Store.Delete("rules", rule.Id, Studio.Administrator, source == ItemSource.Added ? "Deleted" : "Reset to the built-in rule");
        design.Reload();
        TempData["Flash"] = source == ItemSource.Added ? $"{rule.Id} was deleted." : $"{rule.Id} is the built-in rule again.";
        return RedirectToPage("/Studio/Rules/Index");
    }

    /// <summary>The rule the form describes: the rule that is edited with the typed values on it (id and area stay), or a new one checked by review.</summary>
    private static Rule Describe(RuleInput input, Rule? original)
    {
        var level = Enum.TryParse<RuleLevel>(input.Level, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : (RuleLevel)(-1);
        var rule = (original ?? new Rule(string.Empty, string.Empty, string.Empty, string.Empty, RuleLevel.Should, string.Empty)) with
        {
            Id = original?.Id ?? input.Id ?? string.Empty,
            Area = original?.Area ?? input.Area ?? string.Empty,
            Title = input.Title ?? string.Empty,
            Statement = input.Statement ?? string.Empty,
            Level = level,
            Source = input.Source ?? string.Empty,
            Why = input.Why,
            Do = input.Do,
            Dont = input.Dont,
            Check = original?.Check ?? "Review",
        };
        return RuleValidator.Normalize(rule);
    }

    /// <summary>The names of the fields that differ, for the note of the revision.</summary>
    private static IEnumerable<string> What(Rule before, Rule after)
    {
        var names = new List<string>();
        void Compare<T>(string name, T a, T b)
        {
            if (!EqualityComparer<T>.Default.Equals(a, b))
            {
                names.Add(name);
            }
        }

        Compare("title", before.Title, after.Title);
        Compare("statement", before.Statement, after.Statement);
        Compare("level", before.Level, after.Level);
        Compare("source", before.Source, after.Source);
        Compare("reason", before.Why, after.Why);
        Compare("do", before.Do, after.Do);
        Compare("don't", before.Dont, after.Dont);
        return names.Count == 0 ? ["nothing"] : names;
    }
}
