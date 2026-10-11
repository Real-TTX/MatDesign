using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Releases;

/// <summary>One run of the pipeline: what every stage did, line by line.</summary>
public class RunModel(StudioService studio, ReleaseService releases) : StudioPageModel(studio)
{
    public PipelineRun Run { get; private set; } = null!;

    public static string Pill(StageStatus status) => status switch { StageStatus.Succeeded => "mat-pill--ok", StageStatus.Failed => "mat-pill--error", _ => "mat-pill--plain" };

    public IActionResult OnGet(string? id)
    {
        var run = releases.Run(id);
        if (run is null)
        {
            return NotFound();
        }

        Run = run;
        return Page();
    }
}
