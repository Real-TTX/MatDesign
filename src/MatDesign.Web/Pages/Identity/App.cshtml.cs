using MatDesign.Core;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Identity;

public class AppModel(DesignData design) : PageModel
{
    public MatApp App { get; private set; } = null!;

    public AccentReport Report { get; private set; } = null!;

    public IActionResult OnGet(string? id)
    {
        var app = design.App(id);
        if (app is null)
        {
            return NotFound();
        }

        App = app;
        Report = AccentReport.For(app);
        return Page();
    }
}
