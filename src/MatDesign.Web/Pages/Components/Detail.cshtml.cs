using MatDesign.Core;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages.ComponentPages;

/// <summary>
/// The page of one component (<c>/Components/{id}</c>, LIST-18): its live examples with the code beside them, how to use it (the markup contract read from the examples, the behaviour,
/// the documented properties), the rules it follows with their Do and Do not, and the buttons and keys that walk to the previous and the next component. Only the controls of this
/// component are on the page, so only they are started. An id that is not a component is a 404 with a way back to the overview, drawn by the page itself; a component of the layout or the utilities has no page of its own and
/// is sent to its place on the page of its section.
/// </summary>
public class DetailModel(DesignData design, ToolkitFiles toolkit) : PageModel
{
    /// <summary>The component, or null for an id that is none (the page then draws the 404 with the way back).</summary>
    public ComponentDoc? Doc { get; private set; }

    public ComponentNeighbours Around { get; private set; } = null!;

    public IReadOnlyList<ComponentProperty> Properties { get; private set; } = [];

    public IReadOnlyList<string> Classes { get; private set; } = [];

    public IReadOnlyList<string> Attributes { get; private set; } = [];

    /// <summary>The rules the component names, as the rulebook has them now (an id that is no rule any more is left out).</summary>
    public IReadOnlyList<Rule> Rules { get; private set; } = [];

    public ContextPage? Context { get; private set; }

    public IActionResult OnGet(string id)
    {
        var doc = design.Component(id);
        if (doc is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Page();
        }

        var section = ComponentCatalog.SectionOf(doc);
        if (section.Id != "components")
        {
            return Redirect($"{section.Path}#{doc.Id}");
        }

        // one address for a component: /Components/Buttons is /Components/buttons
        if (!string.Equals(id, doc.Id, StringComparison.Ordinal))
        {
            return RedirectPermanent(ComponentCatalog.PageOf(doc));
        }

        Doc = doc;
        Around = ComponentPaging.Around(ComponentPaging.Sequence(design.ComponentsIn(section), section), doc.Id)!;
        Properties = ComponentProperties.ForDoc(toolkit.Properties, doc.Id);
        Classes = ComponentApi.Classes(doc);
        Attributes = ComponentApi.Attributes(doc);
        Rules = [.. doc.Rules.Select(design.Rule).OfType<Rule>()];
        Context = ContextPages.For(doc.Group);
        return Page();
    }
}
