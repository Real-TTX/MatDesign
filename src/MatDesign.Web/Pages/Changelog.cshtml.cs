using MatDesign.Core;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MatDesign.Web.Pages;

/// <summary>
/// The changelog of the toolkit as a page: what changed in each version, and, for the version an app carries, what an update takes with it and whether it has to
/// change something of its own. The data is <see cref="Changelog"/> (CHANGELOG.md, embedded in the build), in the words of the company (<see cref="DesignData.Changelog"/>).
/// </summary>
public class ChangelogModel(DesignData design) : PageModel
{
    /// <summary>The version of an app's copy of the toolkit (the form asks for it); empty shows every version.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Since { get; set; }

    public string Version => AppVersion.MatUi;

    /// <summary>The changes that are not released yet, or null.</summary>
    public ChangelogEntry? Unreleased => design.Changelog.Unreleased;

    /// <summary>The released versions that are shown: all of them, or those after <see cref="Since"/>.</summary>
    public IReadOnlyList<ChangelogEntry> Shown { get; private set; } = [];

    /// <summary>Why the version that was asked for cannot be used; null when it can, or nothing was asked.</summary>
    public string? Problem { get; private set; }

    public bool Asked => Since is not null && Problem is null;

    /// <summary>True when one of the versions that an update takes breaks something.</summary>
    public bool Breaking => Shown.Any(e => e.Breaking);

    public void OnGet()
    {
        Since = string.IsNullOrWhiteSpace(Since) ? null : Since.Trim();
        if (Since is not null && !Changelog.IsVersion(Since))
        {
            Problem = "A version is three numbers with dots, like 0.1.0. You find it in the first line of matui.css.";
            Shown = design.Changelog.Releases;
            return;
        }

        Shown = Since is null ? design.Changelog.Releases : design.Changelog.Since(Since);
    }

    /// <summary>The id of the heading of a version, for a link to it.</summary>
    public static string IdOf(ChangelogEntry entry) => "v-" + entry.Version.Replace('.', '-').ToLowerInvariant();
}
