using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Components;

public class DetailModel : PageModel
{
    public ComponentDoc Component { get; private set; } = null!;

    public ComponentDoc? Previous { get; private set; }

    public ComponentDoc? Next { get; private set; }

    public IReadOnlyList<Rule> Rules { get; private set; } = [];

    public IActionResult OnGet(string? id)
    {
        var component = id is null ? null : ComponentCatalog.Get(id);
        if (component is null)
        {
            return NotFound();
        }

        Component = component;
        Rules = component.Rules.Select(RuleBook.Get).OfType<Rule>().ToList();
        var all = ComponentCatalog.All.ToList();
        var index = all.IndexOf(component);
        Previous = index > 0 ? all[index - 1] : null;
        Next = index < all.Count - 1 ? all[index + 1] : null;
        return Page();
    }
}
