namespace MatDesign.Web.Services;

/// <summary>
/// Where a component is used in a page. <see cref="Selector"/> finds it in the sample page (a class or an attribute, nothing more, so a test can look for it);
/// <see cref="View"/> is the page that has it when that is not the page of the group; <see cref="Open"/> is a button to press in the page first (it opens a dialog).
/// </summary>
public sealed record ContextMark(string Component, string Selector, string? View = null, string? Open = null);

/// <summary>A real page of the sample app that shows the components of one group in use, and the marks that point at the components in it.</summary>
public sealed record ContextPage(string Group, string View, string Title, string Text, IReadOnlyList<ContextMark> Marks);

/// <summary>
/// The pages of the sample app (<c>/Preview</c>) that show the groups of components in use: the components page offers one for each group, next to the
/// components, so that a developer sees a control where it belongs and not only on its own. A component that has a mark can be shown in its page.
/// </summary>
public static class ContextPages
{
    /// <summary>The address of a view of the sample app, as the frames of the site open it.</summary>
    public static string Url(string view) => $"/Preview?app=matdesign&view={view}";

    public static ContextPage? For(string group) => All.FirstOrDefault(p => p.Group == group);

    /// <summary>The page and the mark that show a component in use, or null when the sample pages do not use it.</summary>
    public static (ContextPage Page, ContextMark Mark)? Of(string componentId)
    {
        foreach (var page in All)
        {
            var mark = page.Marks.FirstOrDefault(m => m.Component == componentId);
            if (mark is not null)
            {
                return (page, mark);
            }
        }

        return null;
    }

    public static IReadOnlyList<ContextPage> All { get; } =
    [
        new("Actions", "form", "The edit page",
            "The buttons of a form: Save, Back and Delete in one row under the content, from positive to negative.",
            [
                new("buttons", ".mat-page .mat-btn"),
                new("button-row", ".mat-btn-row"),
            ]),

        new("Forms", "form", "The edit page",
            "Fields with the label above and the hint below, a switch that shows a dependent field, and the grid of two columns.",
            [
                new("fields", ".mat-field"),
                new("input-types", ".mat-input"),
                new("selects", ".mat-select"),
                new("choices", ".mat-switch"),
                new("form-layout", ".mat-form"),
                new("form-grid", ".mat-form__grid"),
                new("validation", "[data-msg-required]"),
            ]),

        new("Lists and data", "list", "The list page",
            "The toolbar above, the table, the pagination directly under it and the actions below. Search, Filter and Sort work in this page.",
            [
                new("toolbar", ".mat-toolbar"),
                new("table", ".mat-table-wrap"),
                new("pager", ".mat-pager"),
                new("gallery", ".mat-gallery", "gallery"),
            ]),

        new("Content", "dashboard", "The overview page",
            "Cards with facts, a meter for the storage and pills for the state of each part.",
            [
                new("cards", ".mat-card"),
                new("pills", ".mat-pill"),
                new("meter", ".mat-meter"),
                new("empty", ".mat-empty", "list&q=nothing-matches-this"),
            ]),

        new("Navigation", "settings", "The settings page",
            "The menu with its submenus and badges, the header bar with the breadcrumb, tabs for the settings and the footer.",
            [
                new("navmenu", ".mat-nav"),
                new("switcher", ".mat-switcher"),
                new("topbar", ".mat-topbar"),
                new("breadcrumb", ".mat-crumbs"),
                new("tabs", ".mat-tabs"),
                new("footer", ".mat-footer"),
                new("menu", ".mat-user"),
            ]),

        new("Feedback", "dashboard", "The overview page",
            "An alert in the page, and, on the list page, the tooltips of the row actions and the filter dialog.",
            [
                new("alerts", ".mat-alert"),
                new("tooltip", "[data-tip]", "list"),
                new("dialogs", "dialog.mat-dialog[open]", "list", "[data-dialog-open=\"sample-filter\"]"),
            ]),

        new("Layout", "dashboard", "The overview page",
            "A grid of four cards, stacked and spread rows: the layouts a page is built from.",
            [
                new("grid-cols", ".mat-grid"),
                new("stack-row-grid", ".mat-stack, .mat-row"),
            ]),
    ];
}
