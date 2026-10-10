namespace MatDesign.Web.Services;

/// <summary>A numbered part of a page pattern: what it is and where the rule for it is written.</summary>
public sealed record PatternPart(string Title, string Text, string? Rule = null);

/// <summary>
/// A page type of the family: what the page consists of, shown in a real frame of the sample app (<see cref="View"/> is the view of
/// <c>/Preview</c>), with the rules that make it that way.
/// </summary>
public sealed record PatternDoc(
    string Id,
    string Name,
    string Kind,
    string Icon,
    string Summary,
    string View,
    IReadOnlyList<PatternPart> Parts,
    IReadOnlyList<string> Rules);

public static class PatternCatalog
{
    public static readonly IReadOnlyList<string> Kinds = ["Lists", "Forms", "Overview", "Account"];

    /// <summary>The apps the pattern frames can be shown in (their accents differ, nothing else does).</summary>
    public static readonly IReadOnlyList<string> Apps = ["matmail", "matpaper", "matsplit", "matdock", "mattiverse"];

    public static PatternDoc? Get(string id) => All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<PatternDoc> All { get; } =
    [
        new("list", "List page", "Lists", "list",
            "Items as a table: the toolbar above, the pagination directly under the table, the actions below it.",
            "list",
            [
                new("Breadcrumb and page head", "Where the page is, then an icon, the title and one line.", "SHELL-08"),
                new("Toolbar", "Search, Filter and Sort are three buttons; each opens a dialog. The count is on the right.", "LIST-03"),
                new("Tools in effect", "A highlighted button and a chip with an X for every search and filter that is on.", "LIST-04"),
                new("Table", "A name column, pills for states, numbers right-aligned in mono; one icon button per row to edit.", "TABLE-03"),
                new("Pagination", "Directly under the table, nothing in between.", "LIST-06"),
                new("Actions", "Below the pagination, left-aligned: the primary action first (New item).", "LIST-07"),
            ],
            ["LIST-01", "LIST-02", "LIST-03", "LIST-04", "LIST-05", "LIST-06", "LIST-07", "LIST-09", "LIST-11", "LIST-12", "TABLE-02", "MOBILE-03"]),

        new("gallery", "Gallery", "Lists", "grid",
            "The same items as tiles, with the same toolbar above.",
            "gallery",
            [
                new("Toolbar", "Exactly the one of the list page; in a list that offers both views a switch sits at its right.", "LIST-10"),
                new("Tiles", "A picture or an icon, the title and one line of meta data. The whole tile is the link.", "LIST-10"),
                new("Pagination and actions", "As on the list page: pagination first, the actions below.", "LIST-06"),
            ],
            ["LIST-02", "LIST-06", "LIST-07", "LIST-10", "MOBILE-02"]),

        new("edit", "Edit page", "Forms", "edit",
            "Create and edit: a card with the fields, the buttons under it from positive to negative.",
            "form",
            [
                new("Breadcrumb", "The list the item belongs to, then the item. There is no back button in the head.", "SHELL-08"),
                new("Fields", "Label above, hint below, error at the field. Required fields have a star.", "FORM-03"),
                new("Grid", "Two columns on a desktop, one on a phone; long fields span both.", "FORM-07"),
                new("Dependent field", "Appears only when it is needed, here while the switch is on.", "FORM-04"),
                new("Buttons", "Under the content: Save, Back, a space, then Delete.", "FORM-01"),
                new("Confirmation", "Delete asks in a dialog; on a phone it is full screen.", "DIALOG-06"),
            ],
            ["LIST-01", "LIST-08", "FORM-01", "FORM-02", "FORM-03", "FORM-04", "FORM-05", "FORM-06", "FORM-07", "FORM-11", "DIALOG-03"]),

        new("settings", "Settings", "Forms", "settings",
            "Several groups of settings on one page: tabs, one card per group, one Save under all of them.",
            "settings",
            [
                new("Tabs", "A tab per group. The panels belong to one form.", "FORM-10"),
                new("Cards", "A group is a card with its fields, switches or choices.", "FORM-10"),
                new("Theme", "The same choice as the button in the menu, as a segmented control.", "SHELL-04"),
                new("Buttons", "Under the content, as on every form.", "FORM-01"),
            ],
            ["FORM-01", "FORM-10", "SHELL-04", "IDENT-05", "SHELL-12"]),

        new("dashboard", "Dashboard", "Overview", "dashboard",
            "What is going on, at a glance: numbers, states, one hint.",
            "dashboard",
            [
                new("Numbers", "A grid of cards with a label, a number and one line of context. Four per row on a desktop.", "SPACE-01"),
                new("Meter", "A bar for a share, with a name and a value for screen readers.", "A11Y-03"),
                new("States", "A pill with text per service: green, amber, red, grey. Never colour alone.", "COLOR-04"),
                new("Hint", "An alert with a lead word.", "COLOR-03"),
            ],
            ["COLOR-03", "COLOR-04", "TABLE-03", "A11Y-03", "SPACE-01"]),

        new("sign-in", "Sign-in page", "Account", "login",
            "No menu: a centred card on the accent wallpaper, the logo above the title.",
            "auth",
            [
                new("Wallpaper", "The accent of the app, softly, behind a centred card.", "SHELL-11"),
                new("Logo and name", "The logo tile of the app above the title.", "IDENT-03"),
                new("Fields", "E-mail and password with the right autocomplete values; errors at the field.", "FORM-05"),
                new("Button", "One full-width primary button. Nothing else competes with it.", "BUTTON-02"),
            ],
            ["SHELL-11", "IDENT-03", "FORM-05", "A11Y-04", "BUTTON-02"]),
    ];
}
