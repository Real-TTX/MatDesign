using MatDesign.Core;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Rules;

public class DetailModel(DesignData design) : PageModel
{
    public Rule Rule { get; private set; } = null!;

    public RuleArea Area { get; private set; } = null!;

    public IReadOnlyList<Rule> InArea { get; private set; } = [];

    public Rule? Previous { get; private set; }

    public Rule? Next { get; private set; }

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
        return Page();
    }
}
