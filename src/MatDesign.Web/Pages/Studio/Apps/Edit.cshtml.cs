using System.Globalization;
using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Apps;

/// <summary>What the form of an app sends. The port is text so that "abc" becomes a message at the field instead of a binding error.</summary>
public sealed class AppInput
{
    public string? Original { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Tagline { get; set; }

    public string? Category { get; set; }

    public string? Accent { get; set; }

    public string? Glyph { get; set; }

    public string? Repo { get; set; }

    public string? Port { get; set; }

    public string? OnAccent { get; set; }

    public string? AccentText { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Creates, changes, resets and deletes an app. Saving checks everything the registry holds for the built-in apps (AppValidator), contrast included; what is
/// wrong stays on the page with the form as it was typed. A built-in app that is saved becomes a changed one (the item replaces it); deleting that item brings
/// the built-in app back.
/// </summary>
public class EditModel(StudioService studio, DesignData design, ToolkitFiles toolkit) : StudioPageModel(studio)
{
    [BindProperty]
    public AppInput Input { get; set; } = new();

    public bool IsNew => string.IsNullOrEmpty(Input.Original);

    public AppSource Source { get; private set; }

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    public bool Checked { get; private set; }

    /// <summary>The app as the form describes it now, for the preview and the contrast report; null while a colour is not a colour.</summary>
    public MatApp? Preview { get; private set; }

    public AccentReport? Report => Preview is { } app && AppValidator.IsSafe(app) ? AccentReport.For(app) : null;

    public IReadOnlyList<string> Glyphs => toolkit.IconNames;

    public string? ProblemFor(string field) => Problems.FirstOrDefault(p => p.Field == field)?.Message;

    public IActionResult OnGet(string? id)
    {
        if (id is null)
        {
            Input = new AppInput { Category = AppRegistry.Productivity, Accent = "#2f6feb", Glyph = "apps" };
            Preview = Describe(Input, null);
            return Page();
        }

        var app = design.App(id);
        if (app is null)
        {
            return NotFound();
        }

        Input = new AppInput
        {
            Original = app.Id, Id = app.Id, Name = app.Name, Tagline = app.Tagline, Category = app.Category, Accent = app.Accent, Glyph = app.Glyph,
            Repo = app.Repo, Port = app.Port?.ToString(CultureInfo.InvariantCulture), OnAccent = app.OnAccent, AccentText = app.AccentText, Notes = app.Notes,
        };
        Source = design.SourceOf(app.Id);
        Preview = app;
        return Page();
    }

    /// <summary>Saves, or (with the Check button, <c>check</c>) only shows what the form would give: the problems and the contrast report.</summary>
    public IActionResult OnPost(bool check = false)
    {
        var original = design.App(Input.Original);
        if (!string.IsNullOrEmpty(Input.Original) && original is null)
        {
            return NotFound();
        }

        Source = original is null ? AppSource.Added : design.SourceOf(original.Id);
        var app = Describe(Input, original);
        Preview = app;
        var problems = Validate(app, original);
        Checked = check;
        if (problems.Count > 0 || check)
        {
            Problems = problems;
            return Page();
        }

        var note = original is null ? "Created" : "Changed " + string.Join(", ", What(original, app));
        Studio.Store.Save("apps", app.Id, app, Studio.Administrator, note);
        design.Reload();
        TempData["Flash"] = original is null ? $"{app.Name} was added." : $"{app.Name} was saved.";
        return RedirectToPage("/Studio/Apps/Index");
    }

    /// <summary>Deletes an app that was added, or resets a built-in app that was changed (the built-in app is back).</summary>
    public IActionResult OnPostDelete()
    {
        var app = design.App(Input.Original);
        if (app is null)
        {
            return NotFound();
        }

        var source = design.SourceOf(app.Id);
        if (source == AppSource.BuiltIn)
        {
            return BadRequest("A built-in app cannot be deleted.");
        }

        Studio.Store.Delete("apps", app.Id, Studio.Administrator, source == AppSource.Added ? "Deleted" : "Reset to the built-in app");
        design.Reload();
        TempData["Flash"] = source == AppSource.Added ? $"{app.Name} was deleted." : $"{app.Name} is the built-in app again.";
        return RedirectToPage("/Studio/Apps/Index");
    }

    /// <summary>The app the form describes: the app that is edited with the typed values on it (what only the audit knows stays), or a new one.</summary>
    private static MatApp Describe(AppInput input, MatApp? original)
    {
        var port = int.TryParse(input.Port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : (int?)null;
        var app = (original ?? new MatApp(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)) with
        {
            Id = original?.Id ?? input.Id ?? string.Empty,
            Name = input.Name ?? string.Empty,
            Tagline = input.Tagline ?? string.Empty,
            Category = input.Category ?? string.Empty,
            Accent = input.Accent ?? string.Empty,
            Glyph = input.Glyph ?? string.Empty,
            Repo = input.Repo ?? string.Empty,
            Port = port,
            OnAccent = input.OnAccent,
            AccentText = input.AccentText,
            Notes = input.Notes ?? string.Empty,
        };
        return AppValidator.Normalize(app);
    }

    private IReadOnlyList<Problem> Validate(MatApp app, MatApp? original)
    {
        var others = design.Apps.Where(a => original is null || !a.Id.Equals(original.Id, StringComparison.OrdinalIgnoreCase));
        var problems = AppValidator.Validate(app, others, toolkit.IconNames).ToList();
        if (!string.IsNullOrWhiteSpace(Input.Port) && !int.TryParse(Input.Port, NumberStyles.None, CultureInfo.InvariantCulture, out _) && problems.All(p => p.Field != "Port"))
        {
            problems.Add(new Core.Studio.Problem("Port", "A port is a number from 1 to 65535."));
        }

        return problems;
    }

    /// <summary>The names of the fields that differ, for the note of the revision.</summary>
    private static IEnumerable<string> What(MatApp before, MatApp after)
    {
        var names = new List<string>();
        void Compare<T>(string name, T a, T b)
        {
            if (!EqualityComparer<T>.Default.Equals(a, b))
            {
                names.Add(name);
            }
        }

        Compare("name", before.Name, after.Name);
        Compare("tagline", before.Tagline, after.Tagline);
        Compare("category", before.Category, after.Category);
        Compare("accent", before.Accent, after.Accent);
        Compare("icon", before.Glyph, after.Glyph);
        Compare("repository", before.Repo, after.Repo);
        Compare("port", before.Port, after.Port);
        Compare("text colour", before.OnAccent, after.OnAccent);
        Compare("link colour", before.AccentText, after.AccentText);
        Compare("note", before.Notes, after.Notes);
        return names.Count == 0 ? ["nothing"] : names;
    }
}
