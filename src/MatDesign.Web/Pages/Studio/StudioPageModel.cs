using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The base of every page of the Studio that needs a signed-in person. The folder is authorized (Program.cs); this adds what the authorization cannot see: the Studio is off (a session cookie
/// outlived it), which leads to the sign-in page that says so; the Studio has nobody yet, which leads to the setup assistant; the person has to change a password an administrator chose (stage 16),
/// which leads to the account page before anything else; and the <b>capability</b> check: the page and every handler declare what they need with <see cref="RequireCapabilityAttribute"/>, the
/// check is made here on the server from the role the person has now, and a person who lacks it gets a page that says which role would do (status 403), never a bare error and never the handler.
/// </summary>
public abstract class StudioPageModel(StudioService studio) : PageModel
{
    protected StudioService Studio { get; } = studio;

    /// <summary>The person who is signed in, as the people have them now. Set before any handler runs.</summary>
    public StudioUser Me { get; private set; } = null!;

    /// <summary>The name that goes into a revision, a release and the activity list.</summary>
    public string Who => Me.Name;

    /// <summary>Whether the person may do this; a page draws a control only for what the person may use.</summary>
    public bool Can(Capability capability) => Me.Can(capability);

    /// <summary>Whether the person may create, change and delete the design (apps, rules, components, brand, files): the pages that edit them draw their controls only for this.</summary>
    public bool CanEdit => Can(Capability.EditDesign);

    /// <summary>Whether the person may make releases (administrators): the pages that lead to a release draw their buttons only for this.</summary>
    public bool CanRelease => Can(Capability.ManageReleases);

    /// <summary>The reason a page of the design is read only for this person.</summary>
    public string WhyNotEdit => WhyNot(Capability.EditDesign);

    /// <summary>The role of the person as a word.</summary>
    public string RoleName => Me.RoleInfo?.Name ?? Me.Role;

    /// <summary>
    /// Why a person cannot do what a capability allows, as the reason a page gives where it leaves a control out or turns it off: who they are and which role would do. A control that is not there
    /// says nothing, so the page says it once, at the top.
    /// </summary>
    public string WhyNot(Capability capability)
        => $"You are signed in as {Me.Name}, {RoleName.ToLowerInvariant()}. This needs the role {StudioRole.Needed(capability).Name.ToLowerInvariant()}.";

    /// <summary>True for the page where a person changes the password they must change: the one page that is open to them.</summary>
    protected virtual bool OpenWhilePasswordMustChange => false;

    /// <summary>The page that says that the role is not enough.</summary>
    protected IActionResult Forbidden(Capability capability) => StudioAccess.Forbidden(HttpContext, Me, capability);

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

        if (Studio.CurrentUser(HttpContext) is not { } user)
        {
            context.Result = RedirectToPage("/Studio/Login");
            return;
        }

        Me = user;
        if (user.MustChange && !OpenWhilePasswordMustChange)
        {
            context.Result = RedirectToPage("/Studio/Account");
            return;
        }

        // reading the Studio is what every page needs; the page and the handler say what more
        foreach (var capability in StudioAccess.Required(GetType(), context.HandlerMethod?.MethodInfo).Prepend(Capability.ViewStudio))
        {
            if (!user.Can(capability))
            {
                context.Result = Forbidden(capability);
                return;
            }
        }

        base.OnPageHandlerExecuting(context);
    }
}
