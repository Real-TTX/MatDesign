namespace MatDesign.Web.Services;

/// <summary>
/// The tree view (LIST-17): folders, categories and areas, a hierarchy of any depth, with one tab stop and the keys of the ARIA tree pattern, one or several chosen,
/// a filter that keeps the parents of a match, and children that are loaded when an item opens.
/// </summary>
internal static class TreeComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    /// <summary>The icon of a folder: the closed one and the open one, of which the state of the item shows one.</summary>
    private static string FolderIcon => $"""<span class="mat-tree__icon mat-tree__icon--folder">{I("folder")}{I("folder-open")}</span>""";

    private static string Count(int n) => $"""<span class="mat-badge mat-badge--soft mat-tree__count">{n}</span>""";

    /// <summary>An item without children: an icon, a label and what <paramref name="more"/> adds to the row.</summary>
    private static string Leaf(string name, string icon = "file", string more = "", string attributes = "")
        => $"""<li class="mat-tree__item"{attributes}><div class="mat-tree__row"><span class="mat-tree__icon">{I(icon)}</span><span class="mat-tree__label">{name}</span>{more}</div></li>""";

    /// <summary>The row of a folder.</summary>
    private static string FolderRow(string name, string more = "")
        => $"""<div class="mat-tree__row">{FolderIcon}<span class="mat-tree__label">{name}</span>{more}</div>""";

    private static string Action(string icon, string label)
        => $"""<button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" aria-label="{label}" data-tip="{label}">{I(icon)}</button>""";

    /// <summary>The actions at the end of a row: add to it and rename it.</summary>
    private static string Actions(string name)
        => $"""<span class="mat-tree__actions">{Action("plus", "Add to " + name)}{Action("edit", "Rename " + name)}</span>""";

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("tree", "Tree view", "Lists and data", "folder",
            "A hierarchy of any depth: folders, categories, the areas of a house. One tab stop and the keys that people know from a file manager; what is open is remembered.",
            ["LIST-17", "LIST-16", "A11Y-01", "A11Y-02", "COLOR-04", "COLOR-08", "MOBILE-04"],
            [
                new("Folders, with a filter", $"""
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-input-icon">
                        {I("search")}
                        <input class="mat-input" type="search" id="tr-files-q" placeholder="Search files" aria-label="Search files" autocomplete="off" data-tree-filter="tr-files" />
                      </div>
                      <ul class="mat-tree" id="tr-files" data-tree data-tree-key="demo-files" aria-label="Files">
                        <li class="mat-tree__item" data-expanded>
                          {FolderRow("Documents", Count(6))}
                          <ul class="mat-tree__group">
                            <li class="mat-tree__item">
                              {FolderRow("Contracts", Count(2))}
                              <ul class="mat-tree__group">
                                {Leaf("Lease.pdf")}
                                {Leaf("Insurance policy.pdf")}
                              </ul>
                            </li>
                            <li class="mat-tree__item" data-expanded>
                              {FolderRow("Reports", Count(3))}
                              <ul class="mat-tree__group">
                                <li class="mat-tree__item" data-expanded>
                                  {FolderRow("2026", Count(2))}
                                  <ul class="mat-tree__group">
                                    {Leaf("Q1 report.pdf")}
                                    {Leaf("Q2 report.pdf", attributes: " aria-selected=\"true\"")}
                                  </ul>
                                </li>
                                <li class="mat-tree__item">
                                  {FolderRow("2025", Count(1))}
                                  <ul class="mat-tree__group">
                                    {Leaf("Annual report.pdf")}
                                  </ul>
                                </li>
                              </ul>
                            </li>
                            {Leaf("Notes.txt", "file-text")}
                          </ul>
                        </li>
                        <li class="mat-tree__item">
                          {FolderRow("Photos", Count(3))}
                          <ul class="mat-tree__group">
                            <li class="mat-tree__item">
                              {FolderRow("Holiday", Count(2))}
                              <ul class="mat-tree__group">
                                {Leaf("Beach.jpg", "image")}
                                {Leaf("Mountain.jpg", "image")}
                              </ul>
                            </li>
                            {Leaf("Portrait.jpg", "image")}
                          </ul>
                        </li>
                        <li class="mat-tree__item">
                          {FolderRow("Archive")}
                        </li>
                      </ul>
                      <p class="mat-tree__empty" hidden></p>
                      <div class="mat-row">
                        <button type="button" class="mat-btn mat-btn--secondary" data-tree-set="open" data-tree-target="#tr-files">Open all</button>
                        <button type="button" class="mat-btn mat-btn--secondary" data-tree-set="closed" data-tree-target="#tr-files">Close all</button>
                      </div>
                    </div>
                    """, "Up and Down walk the items that are shown, Right opens a folder or goes into it, Left closes it or goes to its parent, and a letter goes to the next name that begins with it. The filter hides what does not match, keeps the folders above a match and opens them; a screen reader is told how many matches there are. What you open or close is there on the next visit."),
                new("Several chosen, with boxes", $"""
                    <ul class="mat-tree" id="tr-areas" data-tree data-multiple data-expand-all data-tree-key="demo-areas" aria-label="Areas to include">
                      <li class="mat-tree__item">
                        {FolderRow("Ground floor")}
                        <ul class="mat-tree__group">
                          {Leaf("Kitchen", "home", "", " aria-checked=\"true\"")}
                          {Leaf("Living room", "home", "", " aria-checked=\"true\"")}
                          {Leaf("Hallway", "home")}
                        </ul>
                      </li>
                      <li class="mat-tree__item">
                        {FolderRow("First floor")}
                        <ul class="mat-tree__group">
                          {Leaf("Bedroom", "home")}
                          {Leaf("Bathroom", "home")}
                          {Leaf("Study", "home")}
                        </ul>
                      </li>
                      <li class="mat-tree__item">
                        {FolderRow("Outside")}
                        <ul class="mat-tree__group">
                          {Leaf("Garden", "home")}
                          {Leaf("Garage", "home")}
                        </ul>
                      </li>
                    </ul>
                    """, "The box of a folder is checked when everything in it is, a dash when only some of it is, and checking it checks all of it. Space ticks the item that has the focus. A page that wants what was chosen listens to mat:tree-select, or asks MatUi.tree.get(tree)."),
                new("Children loaded when it opens, with actions", $"""
                    <ul class="mat-tree" id="tr-lazy" data-tree data-tree-key="demo-lazy" aria-label="Categories">
                      <li class="mat-tree__item" data-lazy data-value="tools">
                        {FolderRow("Tools", Actions("Tools"))}
                      </li>
                      <li class="mat-tree__item" data-lazy data-value="garden">
                        {FolderRow("Garden", Actions("Garden"))}
                      </li>
                      <li class="mat-tree__item" data-value="misc">
                        {FolderRow("Miscellaneous", Actions("Miscellaneous"))}
                      </li>
                    </ul>
                    """, "An item with data-lazy has no children yet. When it opens, a small spinner takes the place of the chevron and the tree sends mat:tree-load; the page answers with MatUi.tree.setChildren(item, html). The buttons of a row are reached with the Tab key only from the row that has the tab stop."),
                new("A tree of links, compact, without lines", $"""
                    <ul class="mat-tree mat-tree--compact mat-tree--no-lines" id="tr-nav" data-tree data-tree-toggle="row" aria-label="Help">
                      <li class="mat-tree__item" data-expanded>
                        {FolderRow("Getting started")}
                        <ul class="mat-tree__group">
                          <li class="mat-tree__item" aria-selected="true"><div class="mat-tree__row"><a class="mat-tree__label" href="#" aria-current="page">Installation</a></div></li>
                          <li class="mat-tree__item"><div class="mat-tree__row"><a class="mat-tree__label" href="#">First steps</a></div></li>
                        </ul>
                      </li>
                      <li class="mat-tree__item">
                        {FolderRow("Settings")}
                        <ul class="mat-tree__group">
                          <li class="mat-tree__item"><div class="mat-tree__row"><a class="mat-tree__label" href="#">Accounts</a></div></li>
                          <li class="mat-tree__item"><div class="mat-tree__row"><a class="mat-tree__label" href="#">Notifications</a></div></li>
                        </ul>
                      </li>
                    </ul>
                    """, "The label can be a link: Enter follows it, a click on the row opens a folder (data-tree-toggle=\"row\"), and the item of the current page is the chosen one."),
            ],
            [
                "The tree is a ul (mat-tree, data-tree, an aria-label); an item is a li (mat-tree__item) with a row (mat-tree__row: an icon, mat-tree__label, maybe mat-tree__count and mat-tree__actions) and, for a parent, a ul (mat-tree__group) of more items.",
                "The script writes the roles (tree, treeitem, group), aria-level, aria-setsize, aria-posinset and aria-expanded, and draws the chevron and, with data-multiple, the box. Without it the tree is a nested list that is all open.",
                "The whole tree is one Tab stop, on the item used last. Arrow keys, Home, End, * (open every folder of the level), Enter, Space and the first letters of a name work as in a file manager. The focus is on the item, not on a control in it.",
                "What is open is aria-expanded; what is chosen is aria-selected (one) or, with data-multiple, aria-checked (true, false, mixed). A page drawn by the server can say it: aria-selected=\"true\" on the current item, data-expanded on an open folder, data-expand-all on the tree.",
                "A chosen item is a tint behind the whole row with a stronger label (COLOR-08), never a bar at one side; a folder with only some of its items checked shows a dash, not only a colour (COLOR-04).",
                "data-tree-key remembers what the user opened, per app and item (data-tree-id names an item whose label can change, data-tree-fixed keeps one as drawn). data-tree-toggle=\"row\" lets a click on a folder row open it too; else the chevron, a double click and the keys do.",
                "data-tree-filter=\"id of the tree\" on a search field hides what does not match, keeps and opens the folders above a match and writes the number of matches into a live region; a p.mat-tree__empty after the tree shows when nothing matches (LIST-11).",
                "data-lazy on an item: opening it fires mat:tree-load (detail.item, detail.value) and shows a spinner; the page answers with MatUi.tree.setChildren(item, html), with null on failure (it closes, and can be tried again) or with an empty text (it becomes a leaf).",
                "Events: mat:tree-select (detail.item, detail.selected, detail.values), mat:tree-toggle, mat:tree-load, mat:tree-filter. Script: MatUi.tree.init(scope), expand(item), collapse(item), select(item, on), setChildren(item, html), setAll(tree, open), get(tree).",
                "On a phone and a touch screen a row is 44 px high and a level wider, so that the chevron can be hit; a long label wraps and the page never scrolls sideways; the actions of a row show on hover, on the focus and on the chosen row, and always on a touch screen.",
                "The texts the script writes (matches, loading) are English and German; data-tr-<name> on <html> changes one.",
            ]),
    ];
}
