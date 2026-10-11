using MatDesign.Core;
using MatDesign.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Patterns;

public class DetailModel(MatDesign.Web.Studio.DesignData design) : PageModel
{
    public PatternDoc Pattern { get; private set; } = null!;

    public PatternDoc? Previous { get; private set; }

    public PatternDoc? Next { get; private set; }

    public IReadOnlyList<Rule> Rules { get; private set; } = [];

    /// <summary>The app whose accent the frames show.</summary>
    public MatApp App { get; private set; } = null!;

    public IReadOnlyList<MatApp> Apps { get; private set; } = [];

    public string FrameUrl(string theme) => $"/Preview?app={App.Id}&view={Pattern.View}&theme={theme}";

    public IActionResult OnGet(string? id, string? app)
    {
        var pattern = id is null ? null : PatternCatalog.Get(id);
        if (pattern is null)
        {
            return NotFound();
        }

        Pattern = pattern;
        Apps = PatternCatalog.Apps.Select(design.App).OfType<MatApp>().ToList();
        App = Apps.FirstOrDefault(a => a.Id.Equals(app, StringComparison.OrdinalIgnoreCase)) ?? Apps[0];
        Rules = pattern.Rules.Select(RuleBook.Get).OfType<Rule>().ToList();
        var all = PatternCatalog.All.ToList();
        var index = all.IndexOf(pattern);
        Previous = index > 0 ? all[index - 1] : null;
        Next = index < all.Count - 1 ? all[index + 1] : null;
        return Page();
    }
}
