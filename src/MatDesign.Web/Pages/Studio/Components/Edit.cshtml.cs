using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Components;

/// <summary>What the form of one example sends. <see cref="Js"/>, <see cref="Demo"/> and <see cref="Frame"/> belong to the built-in examples with a script or a frame; the form carries them along.</summary>
public sealed class ExampleInput
{
    public string? Title { get; set; }

    public string? Note { get; set; }

    public string? Html { get; set; }

    public bool Live { get; set; } = true;

    public string? Js { get; set; }

    public string? Demo { get; set; }

    public string? Frame { get; set; }
}

/// <summary>What the form of a component sends. The rules are one line of ids, the behaviour one line per sentence.</summary>
public sealed class ComponentInput
{
    public string? Original { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Group { get; set; }

    public string? Icon { get; set; }

    public string? Summary { get; set; }

    public string? Rules { get; set; }

    public string? Behaviour { get; set; }

    public List<ExampleInput> Examples { get; set; } = [];
}

/// <summary>
/// Creates, changes, resets and deletes a component of the documentation. Saving holds the markup of every example to the contract of the toolkit (the same checks as the unit tests);
/// the page shows the examples live next to the form, so a fault is seen before it is saved. A built-in component that is saved becomes a changed one; deleting that item brings the
/// built-in component back. Examples are added and removed by buttons that answer with the form as it is, nothing saved.
/// </summary>
public class EditModel(StudioService studio, DesignData design, ToolkitFiles toolkit) : StudioPageModel(studio)
{
    [BindProperty]
    public ComponentInput Input { get; set; } = new();

    public bool IsNew => string.IsNullOrEmpty(Input.Original);

    public ItemSource Source { get; private set; }

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    public bool Checked { get; private set; }

    /// <summary>The component as the form describes it now, for the preview next to it.</summary>
    public ComponentDoc? Preview { get; private set; }

    public string? ProblemFor(string field) => Problems.FirstOrDefault(p => p.Field == field)?.Message;

    /// <summary>Every problem of a field (the markup of an example can have several), for the lines under it.</summary>
    public IEnumerable<string> ProblemsFor(string field) => Problems.Where(p => p.Field == field).Select(p => p.Message);

    /// <summary>True when the markup may be drawn on the page: nothing in it breaks the checks that keep a page safe.</summary>
    public bool CanShow(string html)
        => !MarkupChecks.Check([html], toolkit.Facts).Any(p => MarkupChecks.Dangerous.Contains(p.Check));

    public IActionResult OnGet(string? id, string? group)
    {
        if (id is null)
        {
            var chosen = ComponentCatalog.Groups.Contains(group ?? string.Empty) ? group! : "Content";
            Input = new ComponentInput { Group = chosen, Icon = "toggle", Examples = [new ExampleInput()] };
            return Page();
        }

        var doc = design.Component(id);
        if (doc is null)
        {
            return NotFound();
        }

        Input = new ComponentInput
        {
            Original = doc.Id, Id = doc.Id, Name = doc.Name, Group = doc.Group, Icon = doc.Icon, Summary = doc.Summary,
            Rules = string.Join(", ", doc.Rules), Behaviour = string.Join('\n', doc.Behaviour),
            Examples = [.. doc.Examples.Select(e => new ExampleInput { Title = e.Title, Note = e.Note, Html = e.Html, Live = e.Live, Js = e.Js, Demo = e.Demo, Frame = e.Frame })],
        };
        Source = design.ComponentSourceOf(doc.Id);
        Preview = doc;
        return Page();
    }

    /// <summary>Saves, or (with the Check button, <c>check</c>) only shows what the form would give: the problems and the examples live.</summary>
    public IActionResult OnPost(bool check = false)
    {
        var original = design.Component(Input.Original);
        if (!string.IsNullOrEmpty(Input.Original) && original is null)
        {
            return NotFound();
        }

        Source = original is null ? ItemSource.Added : design.ComponentSourceOf(original.Id);
        var doc = Describe(Input, original);
        Preview = doc;
        var others = design.Components.Where(c => original is null || c.Id != original.Id);
        var problems = ComponentValidator.Validate(doc, others, rule => design.Rule(rule) is not null, toolkit.Facts);
        Checked = check;
        if (problems.Count > 0 || check)
        {
            Problems = problems;
            return Page();
        }

        var note = original is null ? "Created" : "Changed " + string.Join(", ", What(original, doc));
        Studio.Store.Save("components", doc.Id, doc, Studio.Administrator, note);
        design.Reload();
        TempData["Flash"] = original is null ? $"{doc.Name} was added." : $"{doc.Name} was saved.";
        return RedirectToPage("/Studio/Components/Index");
    }

    /// <summary>Adds an empty example to the form. Nothing is saved.</summary>
    public IActionResult OnPostAddExample()
    {
        Input.Examples.Add(new ExampleInput());
        Prepare();
        return Page();
    }

    /// <summary>Takes an example out of the form. Nothing is saved; the component keeps it until Save.</summary>
    public IActionResult OnPostRemoveExample(int index)
    {
        if (index >= 0 && index < Input.Examples.Count)
        {
            Input.Examples.RemoveAt(index);
        }

        Prepare();
        return Page();
    }

    /// <summary>Deletes a component that was added, or resets a built-in component that was changed (the built-in component is back).</summary>
    public IActionResult OnPostDelete()
    {
        var doc = design.Component(Input.Original);
        if (doc is null)
        {
            return NotFound();
        }

        var source = design.ComponentSourceOf(doc.Id);
        if (source == ItemSource.BuiltIn)
        {
            return BadRequest("A built-in component cannot be deleted.");
        }

        Studio.Store.Delete("components", doc.Id, Studio.Administrator, source == ItemSource.Added ? "Deleted" : "Reset to the built-in component");
        design.Reload();
        TempData["Flash"] = source == ItemSource.Added ? $"{doc.Name} was deleted." : $"{doc.Name} is the built-in component again.";
        return RedirectToPage("/Studio/Components/Index");
    }

    /// <summary>The state of the page for a form that is answered without being saved: the component that is edited, the preview of what was typed.</summary>
    private void Prepare()
    {
        var original = design.Component(Input.Original);
        Source = original is null ? ItemSource.Added : design.ComponentSourceOf(original.Id);
        Preview = Describe(Input, original);
    }

    /// <summary>The component the form describes: the one that is edited with the typed values on it (its id stays), or a new one.</summary>
    private static ComponentDoc Describe(ComponentInput input, ComponentDoc? original)
    {
        var doc = new ComponentDoc(
            original?.Id ?? input.Id ?? string.Empty,
            input.Name ?? string.Empty,
            input.Group ?? string.Empty,
            input.Icon ?? original?.Icon ?? "toggle",
            input.Summary ?? string.Empty,
            (input.Rules ?? string.Empty).Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries),
            [.. input.Examples.Select(e => new ComponentExample(e.Title ?? string.Empty, e.Html ?? string.Empty, e.Note, e.Js, e.Demo, e.Live, e.Frame))],
            (input.Behaviour ?? string.Empty).Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries));
        return ComponentValidator.Normalize(doc);
    }

    /// <summary>The names of what differs, for the note of the revision.</summary>
    private static IEnumerable<string> What(ComponentDoc before, ComponentDoc after)
    {
        var names = new List<string>();
        if (before.Name != after.Name)
        {
            names.Add("name");
        }

        if (before.Group != after.Group)
        {
            names.Add("group");
        }

        if (before.Summary != after.Summary)
        {
            names.Add("summary");
        }

        if (!before.Rules.SequenceEqual(after.Rules))
        {
            names.Add("rules");
        }

        if (!before.Behaviour.SequenceEqual(after.Behaviour))
        {
            names.Add("good to know");
        }

        if (!before.Examples.SequenceEqual(after.Examples))
        {
            names.Add("examples");
        }

        return names.Count == 0 ? ["nothing"] : names;
    }
}
