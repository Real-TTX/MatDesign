using MatDesign.Core.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace MatDesign.Web.Studio;

/// <summary>
/// Declares what a page of the Studio, or one handler of it, needs (stage 16, <c>CODE-10</c>): the capability a person's role must have. On a page model it is what opening the page needs; on an
/// <c>OnPost*</c> handler it is what the change needs, and every handler that changes something must have one (a test reads the whole assembly and fails for one that has none). The check is made on
/// the server by <c>StudioPageModel</c> before the handler runs, from the role the person has now; several attributes must all hold. On a minimal endpoint, see
/// <see cref="StudioAccess.RequireCapability"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireCapabilityAttribute(Capability capability) : Attribute
{
    public Capability Capability { get; } = capability;
}

/// <summary>What the page that refuses somebody says: who they are, what the page needed, and which role has it.</summary>
public sealed record ForbiddenInfo(StudioUser User, Capability Capability, StudioRole Needed);

/// <summary>The refusals of the Studio: a page of its own (with the shell, one title and the way out) at status 403, and the check for an endpoint that is not a page.</summary>
public static class StudioAccess
{
    public const string ForbiddenView = "/Pages/Studio/_Forbidden.cshtml";

    /// <summary>The capabilities a handler, with its page, declares: the attributes of the page model and those of the handler.</summary>
    public static IReadOnlyList<Capability> Required(Type pageModel, System.Reflection.MethodInfo? handler)
        => [.. pageModel.GetCustomAttributes(typeof(RequireCapabilityAttribute), inherit: true).Cast<RequireCapabilityAttribute>().Select(a => a.Capability),
            .. handler?.GetCustomAttributes(typeof(RequireCapabilityAttribute), inherit: true).Cast<RequireCapabilityAttribute>().Select(a => a.Capability) ?? []];

    /// <summary>The page that says "not for your role": status 403, the shell around it, and what role would do.</summary>
    public static IActionResult Forbidden(HttpContext http, StudioUser user, Capability capability)
    {
        var data = new ViewDataDictionary<ForbiddenInfo>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = new ForbiddenInfo(user, capability, StudioRole.Needed(capability)),
        };
        return new ViewResult { ViewName = ForbiddenView, ViewData = data, StatusCode = StatusCodes.Status403Forbidden };
    }

    /// <summary>
    /// An endpoint that is not a page and changes something (the Studio has one, the renamer of the white label build): signed in, a password that is not waiting to be changed, and the
    /// capability, else 403. The attribute is also metadata of the endpoint, so a test can list the endpoints and find one that declares none.
    /// </summary>
    public static RouteHandlerBuilder RequireCapability(this RouteHandlerBuilder builder, Capability capability)
        => builder.RequireAuthorization()
            .WithMetadata(new RequireCapabilityAttribute(capability))
            .AddEndpointFilter(async (context, next) =>
            {
                var studio = context.HttpContext.RequestServices.GetRequiredService<StudioService>();
                return studio.CurrentUser(context.HttpContext) is { MustChange: false } user && user.Can(capability)
                    ? await next(context)
                    : Results.StatusCode(StatusCodes.Status403Forbidden);
            });

    /// <summary>The page of the menu that a role may open, by its address: the people are for administrators, everything else is for every role that can read the Studio.</summary>
    public static Capability ViewCapabilityOf(string? href) => href is not null && href.StartsWith("/Studio/Users", StringComparison.OrdinalIgnoreCase) ? Capability.ManageUsers : Capability.ViewStudio;
}
