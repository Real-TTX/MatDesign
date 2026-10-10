using MatDesign.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Rules;

public class DetailModel : PageModel
{
    public Rule Rule { get; private set; } = null!;

    public RuleArea Area { get; private set; } = null!;

    public IReadOnlyList<Rule> InArea { get; private set; } = [];

    public Rule? Previous { get; private set; }

    public Rule? Next { get; private set; }

    public IActionResult OnGet(string? id)
    {
        var rule = id is null ? null : RuleBook.Get(id);
        if (rule is null)
        {
            return NotFound();
        }

        Rule = rule;
        Area = RuleBook.Areas.First(a => a.Id == rule.Area);
        InArea = RuleBook.In(rule.Area).ToList();
        var index = RuleBook.All.ToList().IndexOf(rule);
        Previous = index > 0 ? RuleBook.All[index - 1] : null;
        Next = index < RuleBook.All.Count - 1 ? RuleBook.All[index + 1] : null;
        return Page();
    }
}
