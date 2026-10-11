using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The base of every page of the Studio that needs the administrator. The folder is authorized (Program.cs); this adds the case that a session cookie outlives
/// the Studio itself (the password was taken away): the page then leads to the sign-in page, which says that the Studio is off.
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

        base.OnPageHandlerExecuting(context);
    }
}
