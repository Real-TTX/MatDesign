using System.Diagnostics;
using MatDesign.Core.Studio;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio.Releases;

/// <summary>What the form of the white label setting sends.</summary>
public sealed class WhiteLabelInput
{
    public string? Prefix { get; set; }

    public string? Name { get; set; }
}

/// <summary>
/// The white label build (docs/PRODUCT.md, stage 15): the class prefix and the product name a company's releases carry. The page validates what is typed, shows what it turns the names of the toolkit into,
/// and runs a trial build (the renamer and the checks on the files of the next release, nothing published) before the setting is saved: a setting whose trial fails is not saved, and the same trial is the
/// <c>whitelabel</c> check of the Studio. Resetting deletes the item, and releases carry the toolkit's own names again.
/// </summary>
public class WhiteLabelModel(StudioService studio, DesignData design, ReleaseService releases) : StudioPageModel(studio)
{
    [BindProperty]
    public WhiteLabelInput Input { get; set; } = new();

    /// <summary>The setting the form describes (what was typed, normalised), or the saved one on a GET.</summary>
    public WhiteLabelSetting Current { get; private set; } = WhiteLabelSetting.None;

    /// <summary>The setting the Studio saved; null while none is.</summary>
    public WhiteLabelSetting? Saved => WhiteLabelStore.Saved(Studio);

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    /// <summary>The trial build, when this request ran one.</summary>
    public WhiteLabelBuildResult? Trial { get; private set; }

    public long TrialMilliseconds { get; private set; }

    /// <summary>True when the request was the Check button: nothing was saved.</summary>
    public bool Checked { get; private set; }

    public string? ProblemFor(string field) => Problems.FirstOrDefault(p => p.Field == field)?.Message;

    public IActionResult OnGet()
    {
        Current = Saved ?? WhiteLabelSetting.None;
        Input = new WhiteLabelInput { Prefix = Current.Prefix, Name = Current.Name };
        return Page();
    }

    /// <summary>Saves the setting, unless it is not valid or its trial build fails.</summary>
    public IActionResult OnPost()
    {
        var typed = Describe();
        Problems = WhiteLabel.Validate(typed);
        if (Problems.Count > 0)
        {
            return Page();
        }

        if (!typed.IsNoOp)
        {
            RunTrial(typed);
            if (Trial is { Passed: false })
            {
                return Page();
            }
        }

        var before = Saved;
        Studio.Store.Save(WhiteLabelSetting.Kind, WhiteLabelSetting.ItemId, typed, Studio.Administrator, before is null ? $"Created: {Summary(typed)}" : $"Changed to {Summary(typed)}");
        design.Reload();
        TempData["Flash"] = typed.IsNoOp ? "The setting was saved: it renames nothing, releases carry the toolkit's own names." : "The white label setting was saved. The next release carries it.";
        return RedirectToPage("/Studio/Releases/WhiteLabel");
    }

    /// <summary>Runs the trial build on what is typed and shows the result; nothing is saved.</summary>
    public IActionResult OnPostCheck()
    {
        Checked = true;
        var typed = Describe();
        Problems = WhiteLabel.Validate(typed);
        if (Problems.Count == 0 && !typed.IsNoOp)
        {
            RunTrial(typed);
        }

        return Page();
    }

    /// <summary>Deletes the setting: releases carry the names of the toolkit again.</summary>
    public IActionResult OnPostReset()
    {
        if (Saved is not null)
        {
            Studio.Store.Delete(WhiteLabelSetting.Kind, WhiteLabelSetting.ItemId, Studio.Administrator, "Reset: releases carry the toolkit's own names");
            design.Reload();
            TempData["Flash"] = "The setting is gone: releases carry the toolkit's own names.";
        }

        return RedirectToPage("/Studio/Releases/WhiteLabel");
    }

    private WhiteLabelSetting Describe()
    {
        // an empty field is a mistake to point out, not "no rename": the toolkit's own prefix and name are typed
        var typed = new WhiteLabelSetting((Input.Prefix ?? string.Empty).Trim().ToLowerInvariant(), (Input.Name ?? string.Empty).Trim());
        Current = typed;
        Input = new WhiteLabelInput { Prefix = Input.Prefix?.Trim(), Name = Input.Name?.Trim() };
        return typed;
    }

    private void RunTrial(WhiteLabelSetting setting)
    {
        var clock = Stopwatch.StartNew();
        Trial = releases.TrialBuild(setting);
        TrialMilliseconds = clock.ElapsedMilliseconds;
    }

    private static string Summary(WhiteLabelSetting setting) => setting.IsNoOp ? "no rename" : $"{setting.Prefix}- / {setting.Name}";
}
