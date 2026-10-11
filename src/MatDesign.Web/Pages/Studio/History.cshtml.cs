using System.Text.Json;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The history of one item (an app, a rule or a component): every revision with who, when and why, what changed between any two, and a button that restores an old one. A restore
/// is a new revision that copies the old one, and only goes through when the old revision would be accepted as it is today (the same checks as for a save).
/// </summary>
public class HistoryModel(StudioService studio, StudioHistory history) : StudioPageModel(studio)
{
    private static readonly JsonElement NoData = JsonDocument.Parse("{}").RootElement.Clone();

    [BindProperty(SupportsGet = true)]
    public string? Kind { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>The older revision of the comparison.</summary>
    [BindProperty(SupportsGet = true)]
    public int? A { get; set; }

    /// <summary>The newer revision of the comparison.</summary>
    [BindProperty(SupportsGet = true)]
    public int? B { get; set; }

    /// <summary>Every revision of the item, the newest first.</summary>
    public IReadOnlyList<Revision> Revisions { get; private set; } = [];

    public Revision? Left { get; private set; }

    public Revision? Right { get; private set; }

    public IReadOnlyList<FieldChange> Changes { get; private set; } = [];

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    public string Title { get; private set; } = string.Empty;

    public bool ItemExists { get; private set; }

    public string BackUrl => ItemExists ? StudioHistory.EditUrl(Kind!, Id!) : StudioHistory.ListUrl(Kind!);

    public static string Describe(Revision revision) => StudioFormat.What(revision);

    /// <summary>The lines of a change, with what was kept around it, what is new and what is gone.</summary>
    public static IReadOnlyList<DiffLine> Lines(FieldChange change) => Diff.Lines(change.Before, change.After);

    /// <summary>Reads the revisions of the item and what is compared. False when there is no such item in the history.</summary>
    private bool Load()
    {
        if (!StudioHistory.IsKind(Kind) || !StudioStore.IsId(Id))
        {
            return false;
        }

        Revisions = Studio.Store.History(Kind!, Id!);
        if (Revisions.Count == 0)
        {
            return false;
        }

        Title = history.TitleOf(Kind!, Id!);
        ItemExists = history.Exists(Kind!, Id!);

        Right = Revisions.FirstOrDefault(r => r.Number == B) ?? Revisions[0];
        Left = Revisions.FirstOrDefault(r => r.Number == A && r.Number < Right.Number) ?? Revisions.FirstOrDefault(r => r.Number < Right.Number);
        Changes = Diff.Changes(Left is null || Left.Deleted ? NoData : Left.Data, Right.Deleted ? NoData : Right.Data);
        return true;
    }

    public IActionResult OnGet() => Load() ? Page() : NotFound();

    public IActionResult OnPostRestore(string kind, string id, int revision)
    {
        Kind = kind;
        Id = id;
        if (!Load())
        {
            return NotFound();
        }

        var target = Revisions.FirstOrDefault(r => r.Number == revision);
        if (target is null)
        {
            return NotFound();
        }

        var problems = history.CanRestore(kind, id, target);
        if (problems.Count > 0)
        {
            Problems = problems;
            RestoreFailed = revision;
            return Page();
        }

        var restored = history.Restore(kind, id, revision);
        TempData["Flash"] = $"Revision {revision} of {Title} was restored as revision {restored.Number}.";
        return RedirectToPage("/Studio/History", new { kind, id });
    }

    /// <summary>The revision that was asked to be restored and could not be.</summary>
    public int? RestoreFailed { get; private set; }
}
