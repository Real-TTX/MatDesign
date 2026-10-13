namespace MatDesign.Web.Services;

/// <summary>
/// Groups in a table (LIST-16): the rows of a table that belong to something (the hosts of a domain, the files of a folder) under a heading that opens and closes,
/// with the items on a line below the heading and the action that adds one as the last row of the group. The script remembers what the user opened or closed.
/// </summary>
internal static class GroupComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("table-groups", "Groups in a table", "Lists and data", "layers",
            "Rows under a heading that opens and closes: the hosts of a domain, the files of a folder. The rows hang on a line below the heading, and the action that adds one is the last row of the group.",
            ["LIST-16", "LIST-07", "LIST-09", "TABLE-02", "A11Y-02", "MOBILE-02"],
            [
                new("Groups that open and close, with the add row last", $"""
                    <div class="mat-table-wrap">
                      <table class="mat-table" data-groups="demo-hosts" id="grp-hosts">
                        <caption class="mat-sr-only">Hosts by domain</caption>
                        <thead>
                          <tr>
                            <th scope="col"><span class="mat-row mat-row--nowrap">Host <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" data-groups-set="toggle" data-groups-target="#grp-hosts" aria-label="Open or close all groups" data-tip="Open or close all">{I("chevron-down")}</button></span></th>
                            <th scope="col" class="mat-hide-sm">Target</th>
                            <th scope="col">Status</th>
                          </tr>
                        </thead>
                        <tbody class="mat-group" data-group="example.org">
                          <tr class="mat-group__head"><td class="mat-group__cell" colspan="3"><div class="mat-group__bar"><button type="button" class="mat-group__toggle" data-group-toggle aria-expanded="true">{I("chevron-down", "mat-group__chev")}<span class="mat-group__title">example.org</span><span class="mat-badge mat-badge--soft">3</span></button></div></td></tr>
                          <tr class="mat-group__item"><td data-label="Host"><div class="mat-table__name"><span><a href="#">app</a><span class="mat-muted">.example.org</span></span></div></td><td data-label="Target" class="mat-hide-sm mat-mono">http://app:8080</td><td data-label="Status"><span class="mat-pill mat-pill--ok">Active</span></td></tr>
                          <tr class="mat-group__item"><td data-label="Host"><div class="mat-table__name"><span><a href="#">api</a><span class="mat-muted">.example.org</span></span></div></td><td data-label="Target" class="mat-hide-sm mat-mono">http://api:8080</td><td data-label="Status"><span class="mat-pill mat-pill--ok">Active</span></td></tr>
                          <tr class="mat-group__item"><td data-label="Host"><div class="mat-table__name"><span><a href="#">files</a><span class="mat-muted">.example.org</span></span></div></td><td data-label="Target" class="mat-hide-sm mat-mono">https://nas.lan:5001</td><td data-label="Status"><span class="mat-pill">Inactive</span></td></tr>
                          <tr class="mat-group__add"><td class="mat-group__cell" colspan="3"><a class="mat-btn mat-btn--ghost mat-btn--sm" href="#">{I("plus")}<span>Add a subdomain</span></a></td></tr>
                        </tbody>
                        <tbody class="mat-group is-collapsed" data-group="example.net">
                          <tr class="mat-group__head"><td class="mat-group__cell" colspan="3"><div class="mat-group__bar"><button type="button" class="mat-group__toggle" data-group-toggle aria-expanded="false">{I("chevron-down", "mat-group__chev")}<span class="mat-group__title">example.net</span><span class="mat-badge mat-badge--soft">2</span></button></div></td></tr>
                          <tr class="mat-group__item"><td data-label="Host"><div class="mat-table__name"><span><a href="#">status</a><span class="mat-muted">.example.net</span></span></div></td><td data-label="Target" class="mat-hide-sm mat-mono">http://status:3001</td><td data-label="Status"><span class="mat-pill mat-pill--ok">Active</span></td></tr>
                          <tr class="mat-group__item"><td data-label="Host"><div class="mat-table__name"><span><a href="#">blog</a><span class="mat-muted">.example.net</span></span></div></td><td data-label="Target" class="mat-hide-sm mat-mono">http://blog:2368</td><td data-label="Status"><span class="mat-pill">Inactive</span></td></tr>
                          <tr class="mat-group__add"><td class="mat-group__cell" colspan="3"><a class="mat-btn mat-btn--ghost mat-btn--sm" href="#">{I("plus")}<span>Add a subdomain</span></a></td></tr>
                        </tbody>
                      </table>
                    </div>
                    """, "Press a heading: its rows fold away, and the next visit finds the group as you left it. The button in the first column header opens or closes every group. The add row is the last row of its group, so the eye ends on it."),
                new("An action in the heading, and a group that stays open", $"""
                    <div class="mat-table-wrap">
                      <table class="mat-table mat-table--compact" data-groups="demo-files" id="grp-files">
                        <caption class="mat-sr-only">Files by folder</caption>
                        <thead><tr><th scope="col">Name</th><th scope="col" class="mat-num">Size</th></tr></thead>
                        <tbody class="mat-group" data-group="documents" data-group-fixed>
                          <tr class="mat-group__head"><td class="mat-group__cell" colspan="2"><div class="mat-group__bar"><button type="button" class="mat-group__toggle" data-group-toggle aria-expanded="true">{I("chevron-down", "mat-group__chev")}<span class="mat-group__title">Documents</span><span class="mat-badge mat-badge--soft">2</span></button><span class="mat-group__actions"><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" aria-label="Upload to Documents" data-tip="Upload">{I("upload")}</button></span></div></td></tr>
                          <tr class="mat-group__item"><td data-label="Name">Quarterly report</td><td data-label="Size" class="mat-num mat-mono">412 KB</td></tr>
                          <tr class="mat-group__item"><td data-label="Name">Insurance policy</td><td data-label="Size" class="mat-num mat-mono">188 KB</td></tr>
                        </tbody>
                        <tbody class="mat-group" data-group="photos">
                          <tr class="mat-group__head"><td class="mat-group__cell" colspan="2"><div class="mat-group__bar"><button type="button" class="mat-group__toggle" data-group-toggle aria-expanded="true">{I("chevron-down", "mat-group__chev")}<span class="mat-group__title">Photos</span><span class="mat-badge mat-badge--soft">1</span></button><span class="mat-group__actions"><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-btn--sm" aria-label="Upload to Photos" data-tip="Upload">{I("upload")}</button></span></div></td></tr>
                          <tr class="mat-group__item"><td data-label="Name">Holiday</td><td data-label="Size" class="mat-num mat-mono">5.1 MB</td></tr>
                        </tbody>
                      </table>
                    </div>
                    """, "An action for the whole group stands at the end of the heading, as an icon with a name. A group with data-group-fixed is not changed by what was remembered: the server uses it for the group that holds a search hit or the current item."),
            ],
            [
                "A group is a tbody with the class mat-group and a data-group (its name for the remembered state); the table has data-groups (its key). Its first row is the heading, the toggle inside it is a button with aria-expanded and the name of the group as its text.",
                "Without the script every group stays open and the table reads like any other. With it a closed group has is-collapsed and its rows are not in the tab order (they are not displayed).",
                "The rows after the heading are items (mat-group__item): the first cell takes the line. The last row may be the add row (mat-group__add) with one button that adds to this group; the action that adds a group stays under the table (LIST-07).",
                "The cells of the heading and of the add row span the table (colspan) and carry mat-group__cell instead of data-label.",
                "On a phone a heading is a bar over the cards of its rows, the line is gone and the add row is a card with its button (TABLE-02).",
                "Events: mat:group-toggle (detail.group, detail.open). Script: MatUi.groups.set(group, open) and MatUi.groups.setAll(scope, open).",
            ]),
    ];
}
