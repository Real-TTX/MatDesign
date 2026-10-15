using MatDesign.Web.Studio;

namespace MatDesign.Web.Services;

/// <summary>
/// What the editor draws next to the properties of one component group (docs/PRODUCT.md, stage 12): the example of the documentation that shows the control, or, for a control that
/// is not on the page until something opens it (a dialog, a toast, a context menu), a still picture of it in the same markup. <see cref="Doc"/> and <see cref="Example"/> name the example of the
/// catalogue (the id of the component and the place of the example in it); <see cref="Html"/> is the still picture when the catalogue has no example that stands on its own; <see cref="Frame"/> is the
/// look of the area the control is drawn in (<c>menu</c>, <c>bar</c>: the same frames the catalogue uses; <c>dialog</c>, <c>toast</c>, <c>popup</c>: a control that is fixed on a page is drawn in the flow).
/// </summary>
public sealed record PropertyPreview(string Component, string Doc, int Example, string? Html = null, string? Frame = null);

/// <summary>
/// The mapping from the component groups of <see cref="MatDesign.Core.ComponentProperties.Components"/> to what is drawn beside their properties. It is data: a test holds every group to a preview, every
/// example to the catalogue, and the markup of a still picture to the contract of the examples.
/// </summary>
public static partial class PropertyPreviews
{
    private static string I(string name) => ComponentCatalog.Icon(name);

    public static IReadOnlyList<PropertyPreview> All { get; } =
    [
        new("btn", "buttons", 0),
        new("input", "fields", 0),
        new("card", "cards", 0),
        new("dialog", "dialogs", 1, Html: $"""
            <dialog class="mat-dialog" aria-labelledby="pv-dialog-title">
              <div class="mat-dialog__form">
                <div class="mat-dialog__head">{I("edit")}<h2 class="mat-dialog__title" id="pv-dialog-title">Rename</h2><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" aria-label="Close">{I("x")}</button></div>
                <div class="mat-dialog__body"><div class="mat-field"><label class="mat-label" for="pv-dialog-name">Name</label><input class="mat-input" id="pv-dialog-name" name="name" value="Quarterly report" /></div></div>
                <div class="mat-dialog__foot"><div class="mat-row"><button type="button" class="mat-btn mat-btn--primary">Save</button><button type="button" class="mat-btn mat-btn--secondary">Cancel</button></div></div>
              </div>
            </dialog>
            """, Frame: "dialog"),
        new("menu", "menu", 0, Html: $"""
            <div class="mat-menu" role="menu" aria-label="Actions">
              <a class="mat-menu__item" role="menuitem" href="#">{I("edit")}<span>Rename</span></a>
              <a class="mat-menu__item" role="menuitem" href="#">{I("copy")}<span>Duplicate</span></a>
              <div class="mat-menu__sep" role="separator"></div>
              <a class="mat-menu__item mat-menu__item--danger" role="menuitem" href="#">{I("trash")}<span>Delete</span></a>
            </div>
            """, Frame: "popup"),
        new("tag", "pills", 1),
        new("pill", "pills", 0),
        new("badge", "pills", 1),
        new("tabs", "tabs", 0),
        new("table", "table", 0),
        new("alert", "alerts", 0),
        new("toast", "toasts", 0, Html: $"""
            <div class="mat-toast mat-toast--success" role="status">{I("check-circle")}<span>Saved. The backup finished in 14 seconds.</span></div>
            """, Frame: "toast"),
        new("switch", "choices", 0),
        new("check", "checks", 0),
        new("nav", "navmenu", 0, Frame: "menu"),
        new("topbar", "topbar", 0, Frame: "bar"),
    ];

    /// <summary>The preview of a component group, or null.</summary>
    public static PropertyPreview? Of(string component) => All.FirstOrDefault(p => p.Component == component);

    /// <summary>
    /// The markup to draw: the still picture, or the example of the component of the documentation as the site has it now (the Studio may have changed it); null when the example is gone.
    /// <paramref name="frame"/> is the frame of the area (the example's own, or the one of the preview).
    /// </summary>
    public static string? Resolve(PropertyPreview preview, DesignData design, out string? frame)
    {
        var example = design.Component(preview.Doc)?.Examples.ElementAtOrDefault(preview.Example);
        frame = preview.Frame ?? example?.Frame;
        var html = preview.Html ?? (example is { Live: true, Html.Length: > 0 } ? example.Html : null);
        return html is null ? null : Detach(html);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<(input|select|textarea|button)\b")]
    private static partial System.Text.RegularExpressions.Regex Control();

    /// <summary>
    /// The markup with every form control tied to a form that does not exist (<c>form="pv-none"</c>), so that a preview that stands inside the form of an editor can never submit it: Enter in a field
    /// of the preview does nothing. The same way the sample of the brand page does it.
    /// </summary>
    public static string Detach(string html) => Control().Replace(html, "<$1 form=\"pv-none\"");

    /// <summary>
    /// The groups of component properties with the control drawn beside each: every group that has properties, in the order of the toolkit. Used by the page of the brand's components and
    /// by the overrides of an app, so that both show the same controls.
    /// </summary>
    public static IReadOnlyList<PropertyGroupView> Groups(ToolkitFiles toolkit, DesignData design)
        => MatDesign.Core.ComponentProperties.Components
            .Select(c =>
            {
                var properties = toolkit.EditableProperties.Where(p => p.Property.Component == c.Id).ToList();
                var preview = Of(c.Id);
                string? frame = null;
                string? html = null;
                var title = string.Empty;
                if (preview is not null)
                {
                    html = Resolve(preview, design, out frame);
                    title = preview.Html is not null ? "A still picture of the control" : design.Component(preview.Doc)?.Examples.ElementAtOrDefault(preview.Example)?.Title ?? string.Empty;
                }

                return new PropertyGroupView(c, properties, html, frame, title);
            })
            .Where(g => g.Properties.Count > 0)
            .ToList();
}

/// <summary>A component group of the editor: its properties and the control that is drawn beside them (null when the example of the documentation is gone).</summary>
public sealed record PropertyGroupView(MatDesign.Core.PropertyComponent Component, IReadOnlyList<MatDesign.Core.Studio.PropertyToken> Properties, string? PreviewHtml, string? Frame, string PreviewTitle);

/// <summary>
/// What the partial of a component group needs to draw its table and its preview: the group, what is set now (<see cref="ValueOf"/>) and what is wrong (<see cref="ProblemFor"/>), the
/// <see cref="Prefix"/> of the fields (<c>component</c> for the brand, <c>property</c> for an app: <c>component:--mat-btn-radius</c>), the form the inputs belong to when they sit outside it,
/// the app whose <c>data-app</c> the preview wears (null for the brand) and whether the preview is stacked above the table (a narrow page) instead of beside it.
/// </summary>
public sealed record PropertyGroupEditor(PropertyGroupView Group, Func<MatDesign.Core.Studio.PropertyToken, string> ValueOf, Func<string, string?> ProblemFor, string Prefix, string? FormId = null, string? AppId = null, bool Stacked = false, bool ReadOnly = false)
{
    /// <summary>The id of the input of a property: <c>component-mat-btn-radius</c>.</summary>
    public string IdOf(MatDesign.Core.Studio.PropertyToken property) => Prefix + "-" + property.Key;

    /// <summary>The field of a problem about a property: <c>Component:--mat-btn-radius</c>.</summary>
    public string FieldOf(MatDesign.Core.Studio.PropertyToken property) => char.ToUpperInvariant(Prefix[0]) + Prefix[1..] + ":" + property.Name;
}
