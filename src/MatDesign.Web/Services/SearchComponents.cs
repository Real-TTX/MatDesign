namespace MatDesign.Web.Services;

/// <summary>
/// The search with suggestions (FORM-23): a search field that finds as you type and shows what it found in a popup, grouped, with the part of a title that matches marked. It is a wrapper around a real
/// search input, so it works without the script as a plain search in a form; the script adds the popup of a WAI-ARIA combobox, the keys, the recent choices, and on a phone a full-screen panel. The same
/// control sits in the header bar of every page of this site (SHELL-20): the example of the header bar is an extension of that component.
/// </summary>
internal static class SearchComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("search", "Search with suggestions", "Forms", "search",
            "A search field that finds while you type: suggestions in groups with an icon, a title with the matching part marked and one muted line, from the page or from a server. The field is a real search input, so a form sends it with Enter and it works without the script.",
            ["FORM-23", "SHELL-20", "FORM-12", "FORM-15", "A11Y-02", "A11Y-03", "COLOR-04", "COLOR-08", "MOBILE-04"],
            [
                new("Suggestions from the page, in groups", """
                    <div class="mat-field">
                      <label class="mat-label" for="sr-local">Go to a page or a setting</label>
                      <div class="mat-search" data-autocomplete data-items='[{"name":"Pages","items":[{"title":"Overview","text":"The start page with the latest activity","href":"#sr-local","icon":"home","kind":"page","keywords":"start dashboard"},{"title":"Documents","text":"All documents, searched and sorted","href":"#sr-local","icon":"file-text","kind":"page"},{"title":"Calendar","text":"Appointments and deadlines","href":"#sr-local","icon":"calendar","kind":"page"}]},{"name":"Settings","items":[{"title":"Account","text":"Name, password and sign-in","href":"#sr-local","icon":"user","kind":"setting","keywords":"profile login"},{"title":"Notifications","text":"What is sent to you, and when","href":"#sr-local","icon":"bell","kind":"setting"},{"title":"Appearance","text":"Theme and accent colour","href":"#sr-local","icon":"palette","kind":"setting","keywords":"dark light theme"}]}]'>
                        <input class="mat-search__input" type="search" id="sr-local" name="q" placeholder="Try “acc”, “dark” or “cal”" />
                      </div>
                      <span class="mat-hint">The items are the attribute data-items (or MatUi.search.setItems). Every word typed has to be found in a title, a line or the keywords, whatever the case or the accents.</span>
                    </div>
                    """, "Type, then use the arrow keys (they wrap), Home and End, Enter for the option that is active and Escape to close; a second Escape clears the text. A suggestion is a link: a click, Ctrl and click or a touch follows it."),
                new("Suggestions from a server", """
                    <div class="mat-field">
                      <label class="mat-label" for="sr-remote">Find a document or a person</label>
                      <div class="mat-search" data-autocomplete data-search-url="/api/demo/search" data-min="2" data-limit="6">
                        <input class="mat-search__input" type="search" id="sr-remote" name="q" placeholder="Try “report”, “slow report” or “error”" />
                      </div>
                      <span class="mat-hint">A request goes out after 120 ms of quiet. “slow” makes the server wait for a moment (a loading row after 200 ms); “error” makes it fail (a row says so and typing goes on).</span>
                    </div>
                    """, "The script asks `GET /api/demo/search?q=report&limit=6` and gets `{ groups: [ { name, items: [ { title, text, href, icon, kind, id } ] } ] }`. An answer that arrives after a newer question was sent is dropped, and a failed request keeps the field usable."),
                new("Recent choices", """
                    <div class="mat-field">
                      <label class="mat-label" for="sr-recent">Open a page</label>
                      <div class="mat-search" data-autocomplete data-recent="catalogue" data-items='[{"name":"Pages","items":[{"title":"Overview","text":"The start page","href":"#sr-recent","icon":"home","kind":"page"},{"title":"Documents","text":"All documents","href":"#sr-recent","icon":"file-text","kind":"page"},{"title":"Calendar","text":"Appointments and deadlines","href":"#sr-recent","icon":"calendar","kind":"page"},{"title":"Contacts","text":"People and companies","href":"#sr-recent","icon":"users","kind":"page"},{"title":"Reports","text":"Numbers by month","href":"#sr-recent","icon":"bar-chart","kind":"page"},{"title":"Settings","text":"Your account and the app","href":"#sr-recent","icon":"settings","kind":"page"}]}]'>
                        <input class="mat-search__input" type="search" id="sr-recent" name="q" placeholder="Choose one, then click the field again" />
                      </div>
                      <span class="mat-hint">The last five choices are kept in this browser (per app and key) and shown while the field is empty and focused, with a row that forgets them.</span>
                    </div>
                    """, "`data-recent=\"key\"` keeps the last five items that were chosen and the last five queries that were sent, in `localStorage`, per app and key."),
                new("The search of a whole site, in a form", """
                    <form role="search" action="/Search" method="get">
                      <div class="mat-field">
                        <label class="mat-label" for="sr-site">Search the site</label>
                        <div class="mat-search" data-autocomplete data-search-url="/api/search" data-results-url="/Search" data-recent="site-demo">
                          <input class="mat-search__input" type="search" id="sr-site" name="q" placeholder="A rule id, a component, a token ..." />
                        </div>
                        <span class="mat-hint">Enter without a chosen suggestion sends the form to the page of the results; the last row of the popup goes there as well.</span>
                      </div>
                    </form>
                    """, "This is the search that the header bar of every page of this site uses: `data-search-url` for the suggestions, `data-results-url` for the last row, the form for Enter and for a browser without the script."),
                new("Off", """
                    <div class="mat-field">
                      <label class="mat-label" for="sr-off">Search (not available)</label>
                      <div class="mat-search" data-autocomplete data-items='[]'>
                        <input class="mat-search__input" type="search" id="sr-off" name="q" placeholder="Search" disabled />
                      </div>
                    </div>
                    """, "A disabled input disables the clear button and the popup."),
            ],
            [
                "The markup is a wrapper `mat-search[data-autocomplete]` around a real `input type=\"search\"` (`mat-search__input`) with its label (visible, or `mat-sr-only`). In a `<form role=\"search\" action=\"/Search\">` Enter sends it, and without the script it is a plain search field.",
                "The script adds the magnifier, a clear button and the popup, and makes the input a WAI-ARIA 1.2 combobox: `role=\"combobox\"`, `aria-expanded`, `aria-controls`, `aria-autocomplete=\"list\"`, `aria-activedescendant`. Options are `role=\"option\"` links, groups `role=\"group\"` with a heading.",
                "A polite live region says how many suggestions there are, that there are none, or that it is loading.",
                "Down and Up move the active option and wrap, Home and End jump while one is active, Enter follows the active option and otherwise sends the form, Escape closes the popup and a second Escape clears the text, Tab closes it. The typed text is never replaced while the arrow keys walk.",
                "Suggestions come from `data-search-url` (`GET ?q=text&limit=8`, the answer `{ groups: [ { name, items: [ { title, text, href, icon, kind, id } ] } ] }`) or from local items (`data-items`, `MatUi.search.setItems`).",
                "Options: `data-min` (1), `data-delay` (120 ms), `data-limit` (8), `data-results-url` (the row All results goes to the address with `?q=text`) and `data-recent=\"key\"` (the last five choices, per app and key).",
                "A late answer is dropped, a failed request shows a row (\"Search is not available\") and keeps typing possible, a slow one shows a loading row after 200 ms.",
                "The matching part of a title is a `mark.mat-search__hit` made of DOM text nodes: a result called `<img onerror=...>` is shown as text. A suggestion is only followed when its address is http or https.",
                "`data-key=\"/,mod+k\"` focuses the field with the `/` key and with Ctrl+K or Cmd+K, not while another field has the focus; a key that another element has as `data-shortcut` stays that element's, and the hint names the key that works here.",
                "On a phone the focused field is a full-screen panel with an X at the top right; in the header bar (`mat-topbar__search`) it is an icon button there. Targets are 44 px on a touch screen (MOBILE-04).",
                "Events on the wrapper: `mat:search-select` (cancelable) and `mat:search-results`. API: `MatUi.search.init / setItems / open / close / clear / focus / value`; `MatUi.refresh(scope)` finds fields that were added later.",
                "The texts are English and German (`SRCH_TEXT`); change one with `data-sr-<name>` on `<html>`. The shape follows `--mat-search-radius` and `--mat-search-height` (a floor: 44 px on a touch screen).",
            ]),
    ];

    /// <summary>More examples for the header bar: the search between the breadcrumb and the icons.</summary>
    public static IReadOnlyList<ComponentExtension> Extensions { get; } =
    [
        new("topbar", [
                new("With a search field", $"""
                    <header class="mat-topbar">
                      <nav class="mat-crumbs" aria-label="Breadcrumb">
                        <ol><li><a href="#">MatPaper</a></li><li aria-current="page">Documents</li></ol>
                      </nav>
                      <div class="mat-topbar__search">
                        <form role="search" action="/Search" method="get">
                          <label class="mat-sr-only" for="hb-search">Search the site</label>
                          <div class="mat-search" data-autocomplete data-search-url="/api/demo/search" data-min="2">
                            <input class="mat-search__input" type="search" id="hb-search" name="q" placeholder="Search" />
                          </div>
                        </form>
                      </div>
                      <div class="mat-topbar__actions">
                        <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="Help" data-tip="Help">{I("help-circle")}</button>
                      </div>
                    </header>
                    """, "The search is a field of its own width (`--mat-search-width`) between the breadcrumb and the icons, whatever the order of the markup; with `data-key=\"/,mod+k\"` it also shows the key that focuses it. Below 640 px it is one icon button that opens the full-screen panel. It is not one of the icons of SHELL-17.", Frame: "bar"),
            ],
            [
                "A search for the whole app is a field in the bar (`mat-topbar__search` around a `mat-search`, SHELL-20), not an icon that opens a page: it stays in reach on every page, left of the icons. It does not count against the four icons.",
            ],
            Rules: ["SHELL-20", "FORM-23"]),
    ];
}
