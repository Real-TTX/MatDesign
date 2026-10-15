using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Versions;

/// <summary>What the form of the version of an app sends.</summary>
public sealed class VersionInput
{
    public string? AppId { get; set; }

    public string? Version { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Writes down which release of MatUi an app uses, with a note. One button takes the latest release over (the usual case after the app copied it), another takes the record away. An empty
/// version means "not written down".
/// </summary>
[RequireCapability(Capability.ViewStudio)]
public class EditModel(StudioService studio, VersionTracker tracker) : StudioPageModel(studio)
{
    [BindProperty]
    public VersionInput Input { get; set; } = new();

    public UsageRow Row { get; private set; } = null!;

    public string? Latest => tracker.LatestVersion;

    public string? VersionProblem { get; private set; }

    public string? NoteProblem { get; private set; }

    public IActionResult OnGet(string? id)
    {
        var row = tracker.Of(id);
        if (row is null)
        {
            return NotFound();
        }

        Row = row;
        Input = new VersionInput { AppId = row.App.Id, Version = row.Version, Note = row.Note };
        return Page();
    }

    [RequireCapability(Capability.ManageVersions)]
    public IActionResult OnPost() => Save(useLatest: false);

    /// <summary>Takes the latest release as the version, whatever is typed in the field.</summary>
    [RequireCapability(Capability.ManageVersions)]
    public IActionResult OnPostLatest() => Save(useLatest: true);

    private IActionResult Save(bool useLatest)
    {
        var row = tracker.Of(Input.AppId);
        if (row is null)
        {
            return NotFound();
        }

        Row = row;
        var version = useLatest ? tracker.LatestVersion : Input.Version;
        var note = (Input.Note ?? string.Empty).Trim();
        VersionProblem = VersionTracker.Check(version);
        if (note.Length > 200)
        {
            NoteProblem = "Keep the note to 200 characters.";
        }

        if (VersionProblem is not null || NoteProblem is not null)
        {
            return Page();
        }

        tracker.Set(row.App.Id, version, note, Who);
        TempData["Flash"] = string.IsNullOrWhiteSpace(version) ? $"The version of {row.App.Name} is not written down." : $"{row.App.Name} uses {version.Trim()}.";
        return RedirectToPage("/Studio/Versions/Index");
    }

    /// <summary>Takes the record away; the app is "not written down" again.</summary>
    [RequireCapability(Capability.ManageVersions)]
    public IActionResult OnPostForget()
    {
        var row = tracker.Of(Input.AppId);
        if (row is null)
        {
            return NotFound();
        }

        tracker.Forget(row.App.Id, Who);
        TempData["Flash"] = $"The record of {row.App.Name} was taken away.";
        return RedirectToPage("/Studio/Versions/Index");
    }
}
