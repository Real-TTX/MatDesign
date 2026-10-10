using MatDesign.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Identity;

public class AppModel : PageModel
{
    public MatApp App { get; private set; } = null!;

    public AccentReport Report { get; private set; } = null!;

    public IActionResult OnGet(string? id)
    {
        var app = id is null ? null : AppRegistry.Get(id);
        if (app is null)
        {
            return NotFound();
        }

        App = app;
        Report = AccentReport.For(app);
        return Page();
    }
}
