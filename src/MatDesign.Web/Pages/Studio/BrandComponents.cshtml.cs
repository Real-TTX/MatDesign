using MatDesign.Core;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.AspNetCore.Mvc;

namespace MatDesign.Web.Pages.Studio;

/// <summary>
/// The component properties of the brand (docs/PRODUCT.md, stage 12): the 68 documented <c>--mat-&lt;component&gt;-*</c> properties grouped by component, each with its description, type, limits, what the control uses
/// today and an input of its type, and the real control of the documentation beside them with a live preview. They belong to the one brand item: the same save, the same history and the same restore as
/// <see cref="BrandModel"/>; this page keeps the names, the tokens and the colour schemes as they are saved and changes the properties only. What is wrong stays on the page with the form as it was typed.
/// </summary>
[RequireCapability(Capability.ViewStudio)]
public class BrandComponentsModel(StudioService studio, DesignData design, ToolkitFiles toolkit) : StudioPageModel(studio)
{
    /// <summary>The brand as the form describes it (what was typed, normalised), or the saved one on a GET.</summary>
    public Brand Current { get; private set; } = Brand.BuiltIn;

    public IReadOnlyList<Problem> Problems { get; private set; } = [];

    public bool Checked { get; private set; }

    public ItemSource Source => design.BrandSource;

    /// <summary>The groups with properties, in the order of the toolkit, each with the control drawn beside it.</summary>
    public IReadOnlyList<PropertyGroupView> Groups => PropertyPreviews.Groups(toolkit, design);

    public int Count => toolkit.EditableProperties.Count;

    public string? ProblemFor(string field) => Problems.FirstOrDefault(p => p.Field == field)?.Message;

    /// <summary>What the brand sets for a property; empty when it leaves the toolkit's default.</summary>
    public string ValueOf(PropertyToken property) => Current.ComponentValues.GetValueOrDefault(property.Name, string.Empty);

    public static string IdOf(PropertyToken property) => "component-" + property.Key;

    /// <summary>The id of the input a problem is about, for the link in the summary; a problem of another part of the brand leads to its page.</summary>
    public static string TargetOf(string field)
        => field.Split(':') is ["Component", var name] ? "#component-" + name.TrimStart('-') : "/Studio/Brand";

    public IActionResult OnGet()
    {
        Current = design.Brand;
        return Page();
    }

    /// <summary>Saves, or (with the Check button, <c>check</c>) only shows what the form would give: the problems.</summary>
    [RequireCapability(Capability.EditDesign)]
    public IActionResult OnPost(bool check = false)
    {
        var typed = BrandValidator.Normalize(design.Brand with { Components = BrandForm.Values(Request.Form, "component:") }, toolkit.EditableTokens, toolkit.EditableProperties, toolkit.Schemes);
        var problems = BrandValidator.Validate(typed, toolkit.Tokens, toolkit.Schemes, design.Apps, toolkit.EditableProperties);
        Current = typed;
        Checked = check;
        if (problems.Count > 0 || check)
        {
            Problems = problems;
            return Page();
        }

        var before = design.Brand;
        Studio.Store.Save(Brand.Kind, Brand.ItemId, typed, Who, design.StudioBrand is null ? "Created: " + BrandForm.What(before, typed) : "Changed " + BrandForm.What(before, typed));
        design.Reload();
        TempData["Flash"] = "The component properties were saved.";
        return RedirectToPage("/Studio/BrandComponents");
    }
}
