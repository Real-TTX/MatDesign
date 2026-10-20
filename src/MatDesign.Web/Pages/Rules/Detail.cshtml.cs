using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Rules;

public class DetailModel(DesignData design, StudioService studio, StudioHistory history) : PageModel
{
    public Rule Rule { get; private set; } = null!;

    public RuleArea Area { get; private set; } = null!;

    public IReadOnlyList<Rule> InArea { get; private set; } = [];

    public Rule? Previous { get; private set; }

    public Rule? Next { get; private set; }

    /// <summary>The lines of the changelog of the rulebook about this rule, the newest first (empty for a rule that was written in the Studio).</summary>
    public IReadOnlyList<RuleChange> Changes { get; private set; } = [];

    /// <summary>Where the rule comes from: the program (built in), the program with a text from the Studio on top (changed), or the Studio alone (added).</summary>
    public ItemSource Origin { get; private set; }

    /// <summary>The history of the revisions in the Studio, for a person who may open the Studio and a rule that the Studio has saved; null for everybody else.</summary>
    public string? StudioHistoryUrl { get; private set; }

    public IActionResult OnGet(string? id)
    {
        var rule = design.Rule(id);
        if (rule is null)
        {
            return NotFound();
        }

        Rule = rule;
        Area = RuleBook.Areas.First(a => a.Id == rule.Area);
        InArea = design.RulesIn(rule.Area).ToList();
        var index = design.Rules.ToList().IndexOf(rule);
        Previous = index > 0 ? design.Rules[index - 1] : null;
        Next = index < design.Rules.Count - 1 ? design.Rules[index + 1] : null;

        Origin = design.RuleSourceOf(rule.Id);
        Changes = RuleChanges.For(rule.Id).Reverse().ToList();
        if (Origin != ItemSource.BuiltIn && history.HasHistory("rules", rule.Id) && studio.CurrentUser(User) is { } person && person.Can(Capability.ViewStudio))
        {
            StudioHistoryUrl = StudioHistory.HistoryUrl("rules", rule.Id);
        }

        return Page();
    }
}
