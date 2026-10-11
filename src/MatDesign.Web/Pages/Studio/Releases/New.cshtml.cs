using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Releases;

/// <summary>
/// The form that starts the pipeline: a version (the next one is suggested) and a note. The version is checked before anything runs, so a typo does not leave a run behind; the checks of the data
/// are shown, and a run with errors is allowed (it stops at Check, and the run is kept). The pipeline is fast, so it runs in the request and the page leads to the run.
/// </summary>
public class NewModel(StudioService studio, ReleaseService releases, DesignChecks checks) : StudioPageModel(studio)
{
    [BindProperty]
    public string? Version { get; set; }

    [BindProperty]
    public string? Note { get; set; }

    public string? VersionProblem { get; private set; }

    public string? NoteProblem { get; private set; }

    public string? Busy { get; private set; }

    public CheckReport Report => checks.Report;

    public ReleaseManifest? Latest => releases.Latest;

    public IReadOnlyList<string> Pending => releases.PendingChanges();

    public IActionResult OnGet()
    {
        Version = releases.SuggestNext();
        return Page();
    }

    public IActionResult OnPost()
    {
        var version = (Version ?? string.Empty).Trim();
        var note = (Note ?? string.Empty).Trim();
        Version = version;
        Note = note;
        VersionProblem = releases.CheckVersion(version);
        if (note.Length > 500)
        {
            NoteProblem = "Keep the note to 500 characters.";
        }

        if (VersionProblem is not null || NoteProblem is not null)
        {
            return Page();
        }

        PipelineRun run;
        try
        {
            run = releases.Execute(version, note, Studio.Administrator);
        }
        catch (InvalidOperationException ex)
        {
            Busy = ex.Message;
            return Page();
        }

        TempData["Flash"] = run.Succeeded ? $"Release {version} was published." : $"The run stopped at {run.Stages.First(s => s.Status == StageStatus.Failed).Name}: nothing was published.";
        return RedirectToPage("/Studio/Releases/Run", new { id = run.Id });
    }
}
