using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The base of every page of the Studio that needs the administrator. The folder is authorized (Program.cs); this adds the two cases the authorization cannot
/// see: the Studio is off (a session cookie outlived it), which leads to the sign-in page that says so, and the Studio has no administrator yet, which leads to the
/// setup assistant.
/// </summary>
public abstract class StudioPageModel(StudioService studio) : PageModel
{
    protected StudioService Studio { get; } = studio;

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        if (!Studio.Enabled)
        {
            context.Result = RedirectToPage("/Studio/Login");
            return;
        }

        if (Studio.NeedsSetup)
        {
            context.Result = RedirectToPage("/Studio/Setup");
            return;
        }

        base.OnPageHandlerExecuting(context);
    }
}
