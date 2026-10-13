namespace MatDesign.Web.Services;

/// <summary>
/// The list of rows (LIST-15), like the lists of a phone app: the list and its rows, the kinds of content a row holds (contacts, an inbox, settings, files, tasks,
/// notifications, products, a skeleton, an empty list), choosing and typing in rows, and the two things that need the script: sliding actions and reordering.
/// </summary>
internal static class ListComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    /// <summary>The arrow of a row that opens something.</summary>
    private static string Chev => I("chevron-right", "mat-item__chevron");

    /// <summary>A row of the contacts with an avatar, a name, a line and the arrow; <paramref name="indent"/> is the column the row starts at in the example, so that the code is shown straight.</summary>
    private static string Contact(string initials, string name, string text, int indent)
        => string.Join("\n" + new string(' ', indent),
            new[]
            {
                "<li class=\"mat-item\">",
                "  <a class=\"mat-item__main\" href=\"#\">",
                $"    <span class=\"mat-item__lead\"><span class=\"mat-avatar\" aria-hidden=\"true\">{initials}</span></span>",
                $"    <span class=\"mat-item__body\"><span class=\"mat-item__title\">{name}</span><span class=\"mat-item__text\">{text}</span></span>",
                $"    <span class=\"mat-item__end\">{Chev}</span>",
                "  </a>",
                "</li>",
            });

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("itemlist", "Item list", "Lists and data", "list",
            "Rows like the lists of a phone app: a picture or an icon in front, a title with lines under it, something at the end and the arrow of a row that opens. The whole row is the target.",
            ["LIST-15", "LIST-09", "MOBILE-04", "COLOR-04", "COLOR-08", "A11Y-01", "A11Y-02"],
            [
                new("A list of rows", $"""
                    <ul class="mat-itemlist" aria-label="People">
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">AM</span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Alex Morgan</span><span class="mat-item__text">Administrator</span></span>
                          <span class="mat-item__end">{Chev}</span>
                        </a>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">JD</span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Jamie Doe</span><span class="mat-item__text">User, invited on 2026-10-02</span></span>
                          <span class="mat-item__end">{Chev}</span>
                        </a>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">SL</span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Sam Lee</span><span class="mat-item__text">Guest</span></span>
                          <span class="mat-item__end">{Chev}</span>
                        </a>
                      </li>
                    </ul>
                    """, "A row has three slots, all of them optional: the lead (an avatar, an icon, a thumbnail or a check), the body (a title and the lines under it) and the end (a note, a count, a value, the arrow). The whole row is one target, at least 48 px high."),
                new("Lines: full, from the text, none", $"""
                    <div class="mat-stack">
                      <div class="mat-stack mat-stack--sm">
                        <h4 class="mat-itemlist__header" id="il-lines-full">Full lines, the default</h4>
                        <ul class="mat-itemlist" aria-labelledby="il-lines-full">
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">AM</span></span><span class="mat-item__body"><span class="mat-item__title">Alex Morgan</span></span></div></li>
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">JD</span></span><span class="mat-item__body"><span class="mat-item__title">Jamie Doe</span></span></div></li>
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">SL</span></span><span class="mat-item__body"><span class="mat-item__title">Sam Lee</span></span></div></li>
                        </ul>
                      </div>
                      <div class="mat-stack mat-stack--sm">
                        <h4 class="mat-itemlist__header" id="il-lines-inset">Lines from the text</h4>
                        <ul class="mat-itemlist mat-itemlist--lines-inset" aria-labelledby="il-lines-inset">
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">AM</span></span><span class="mat-item__body"><span class="mat-item__title">Alex Morgan</span></span></div></li>
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">JD</span></span><span class="mat-item__body"><span class="mat-item__title">Jamie Doe</span></span></div></li>
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">SL</span></span><span class="mat-item__body"><span class="mat-item__title">Sam Lee</span></span></div></li>
                        </ul>
                      </div>
                      <div class="mat-stack mat-stack--sm">
                        <h4 class="mat-itemlist__header" id="il-lines-none">No lines</h4>
                        <ul class="mat-itemlist mat-itemlist--lines-none" aria-labelledby="il-lines-none">
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">AM</span></span><span class="mat-item__body"><span class="mat-item__title">Alex Morgan</span></span></div></li>
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">JD</span></span><span class="mat-item__body"><span class="mat-item__title">Jamie Doe</span></span></div></li>
                          <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">SL</span></span><span class="mat-item__body"><span class="mat-item__title">Sam Lee</span></span></div></li>
                        </ul>
                      </div>
                    </div>
                    """, "The lines belong to the list: mat-itemlist--lines-inset starts them under the text (the width of the lead is --mat-item-lead), mat-itemlist--lines-none leaves them out. A plain row is a div instead of a link."),
                new("A grouped card: inset", $"""
                    <div class="mat-stack">
                      <section class="mat-stack mat-stack--sm" aria-labelledby="il-card-account">
                        <h3 class="mat-itemlist__header" id="il-card-account">Account</h3>
                        <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--lines-inset" aria-labelledby="il-card-account">
                          <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__lead"><span class="mat-item__tile">{I("user")}</span></span><span class="mat-item__body"><span class="mat-item__title">Profile</span></span><span class="mat-item__end">{Chev}</span></a></li>
                          <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__lead"><span class="mat-item__tile">{I("lock")}</span></span><span class="mat-item__body"><span class="mat-item__title">Password</span></span><span class="mat-item__end">{Chev}</span></a></li>
                        </ul>
                      </section>
                      <section class="mat-stack mat-stack--sm" aria-labelledby="il-card-app">
                        <h3 class="mat-itemlist__header" id="il-card-app">App</h3>
                        <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--lines-inset" aria-labelledby="il-card-app">
                          <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__lead"><span class="mat-item__tile">{I("bell")}</span></span><span class="mat-item__body"><span class="mat-item__title">Notifications</span></span><span class="mat-item__end">{Chev}</span></a></li>
                          <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__lead"><span class="mat-item__tile">{I("palette")}</span></span><span class="mat-item__body"><span class="mat-item__title">Appearance</span></span><span class="mat-item__end">{Chev}</span></a></li>
                        </ul>
                      </section>
                    </div>
                    """, "mat-itemlist--inset makes the list a card: a border, the large radius and nothing that sticks out. A heading above it (mat-itemlist__header) names the group, and each list is labelled by it."),
                new("What a row can hold", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Kinds of rows">
                      <li class="mat-item">
                        <div class="mat-item__main">
                          <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">AM</span></span>
                          <span class="mat-item__body"><span class="mat-item__title">An avatar, a title and a note</span><span class="mat-item__text">The note is a time, a date or a size</span></span>
                          <span class="mat-item__end"><span class="mat-item__note">09:41</span></span>
                        </div>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead">{I("mail")}</span>
                          <span class="mat-item__body"><span class="mat-item__title">An icon and a count</span></span>
                          <span class="mat-item__end"><span class="mat-badge">12<span class="mat-sr-only"> unread</span></span>{Chev}</span>
                        </a>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__tile mat-item__tile--accent">{I("globe")}</span></span>
                          <span class="mat-item__body"><span class="mat-item__title">An icon tile, a value and the arrow</span></span>
                          <span class="mat-item__end"><span class="mat-item__note">English</span>{Chev}</span>
                        </a>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__thumb"><img src="/favicon.svg" alt="" width="40" height="40" /></span></span>
                          <span class="mat-item__body"><span class="mat-item__title">A thumbnail and two lines</span><span class="mat-item__text">The picture is decoration here</span><span class="mat-item__text">A third line is for what comes last</span></span>
                        </a>
                      </li>
                      <li class="mat-item">
                        <div class="mat-item__main">
                          <span class="mat-item__body"><span class="mat-item__title">A title and a status</span></span>
                          <span class="mat-item__end"><span class="mat-pill mat-pill--ok">Online</span></span>
                        </div>
                      </li>
                      <li class="mat-item">
                        <div class="mat-item__main">
                          <span class="mat-item__body"><span class="mat-item__title">A button beside the text</span><span class="mat-item__text">It is not inside a link</span></span>
                        </div>
                        <div class="mat-item__aside"><button type="button" class="mat-btn mat-btn--secondary mat-btn--sm">Invite</button></div>
                      </li>
                    </ul>
                    """, "What has to be pressed as well (a button, a switch) is mat-item__aside beside mat-item__main, never inside a link: one target never holds another. A pill or a note carries a state in words."),
                new("A link, a button, plain text and the states", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Rows and their states">
                      <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__body"><span class="mat-item__title">A link</span><span class="mat-item__text">It goes to a page</span></span><span class="mat-item__end">{Chev}</span></a></li>
                      <li class="mat-item"><button type="button" class="mat-item__main"><span class="mat-item__body"><span class="mat-item__title">A button</span><span class="mat-item__text">It does something here</span></span></button></li>
                      <li class="mat-item"><div class="mat-item__main"><span class="mat-item__body"><span class="mat-item__title">Plain text</span><span class="mat-item__text">Nothing to press</span></span></div></li>
                      <li class="mat-item is-selected"><button type="button" class="mat-item__main" aria-pressed="true"><span class="mat-item__body"><span class="mat-item__title">Selected</span><span class="mat-item__text">A tint behind the whole row, a stronger title and a check</span></span><span class="mat-item__end">{I("check")}</span></button></li>
                      <li class="mat-item"><a class="mat-item__main" href="#" aria-current="page"><span class="mat-item__body"><span class="mat-item__title">The current page</span><span class="mat-item__text">aria-current marks it</span></span></a></li>
                      <li class="mat-item is-disabled"><a class="mat-item__main" href="#" aria-disabled="true" tabindex="-1"><span class="mat-item__body"><span class="mat-item__title">Disabled</span><span class="mat-item__text">Not available for you</span></span></a></li>
                    </ul>
                    """, "Hover and press add a little of the text colour, the keyboard focus is a ring inside the row, and a selected row is a tint of the accent behind the whole row, never a bar at one side (COLOR-08). A check tells the selection in a shape as well as a colour (COLOR-04)."),
                new("A header and dividers", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Messages by day">
                      <li class="mat-itemlist__divider">Today</li>
                      <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__body"><span class="mat-item__title">Invoice for September</span><span class="mat-item__text">Jamie Doe</span></span><span class="mat-item__end"><span class="mat-item__note">09:41</span></span></a></li>
                      <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__body"><span class="mat-item__title">Lunch on Friday?</span><span class="mat-item__text">Sam Lee</span></span><span class="mat-item__end"><span class="mat-item__note">08:15</span></span></a></li>
                      <li class="mat-itemlist__divider">Earlier</li>
                      <li class="mat-item"><a class="mat-item__main" href="#"><span class="mat-item__body"><span class="mat-item__title">Welcome to the team</span><span class="mat-item__text">Alex Morgan</span></span><span class="mat-item__end"><span class="mat-item__note">Mon</span></span></a></li>
                    </ul>
                    """, "A divider is a row of its own in a flat list; for sections that stay at the top while their rows pass use groups (see the contacts) with mat-itemlist--sticky."),
            ],
            [
                "A row is `mat-item`: `mat-item__main` (a link, a button, a label or a div) holds `mat-item__lead`, `mat-item__body` (one `mat-item__title`, any number of `mat-item__text` lines) and `mat-item__end` (a `mat-item__note`, a badge, a pill, the arrow `mat-item__chevron`).",
                "What else has to be pressed goes into `mat-item__aside` beside `mat-item__main`: a button, a switch, the handle of reordering. An interactive element is never inside a link.",
                "A title and its lines are cut with an ellipsis. `mat-item--wrap` lets a row wrap, `mat-item__text--clamp` shows two lines at most and `mat-item--top` aligns a row that has several lines to its top.",
                "`mat-itemlist--compact` makes the rows 40 px high, `mat-itemlist--lead-lg` widens the lead to 56 px for thumbnails, and `mat-itemlist--scroll` gives the list a height of its own and scrolls inside it.",
                "A row is at least 48 px high, 52 px on a touch screen (MOBILE-04). The list is a ul with an aria-label or a heading that names it, so that a screen reader says how many rows there are.",
                "The focus ring is drawn inside the row, so that no neighbour can clip it (A11Y-01). Without the script a list is a list: lines, links and states work, which is all most lists need.",
            ]),

        new("itemlist-contents", "Item list: kinds of content", "Lists and data", "users",
            "What a row holds depends on the thing it lists: contacts under their letters, an inbox, settings, files, tasks, notifications, products, a list that loads and a list that is empty.",
            ["LIST-15", "LIST-11", "COLOR-04", "COLOR-03", "A11Y-05", "MOBILE-04"],
            [
                new("Contacts under sticky letters, with a filter", $"""
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-input-icon">
                        {I("search")}
                        <input class="mat-input" type="search" id="il-contacts-q" placeholder="Search contacts" aria-label="Search contacts" autocomplete="off" data-item-filter="il-contacts" />
                      </div>
                      <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--sticky mat-itemlist--scroll mat-itemlist--lines-inset" id="il-contacts" aria-label="Contacts" data-itemlist>
                        <li class="mat-itemlist__group">
                          <h3 class="mat-itemlist__divider">A</h3>
                          <ul class="mat-itemlist__items">
                            {Contact("AM", "Alex Morgan", "Acme GmbH", 8)}
                            {Contact("AH", "Amira Hassan", "Nile Trading", 8)}
                          </ul>
                        </li>
                        <li class="mat-itemlist__group">
                          <h3 class="mat-itemlist__divider">B</h3>
                          <ul class="mat-itemlist__items">
                            {Contact("BC", "Ben Carter", "Carter and Sons", 8)}
                            {Contact("BR", "Bianca Rossi", "Studio Rossi", 8)}
                          </ul>
                        </li>
                        <li class="mat-itemlist__group">
                          <h3 class="mat-itemlist__divider">C</h3>
                          <ul class="mat-itemlist__items">
                            {Contact("CM", "Carla Mendes", "Mendes Logistics", 8)}
                            {Contact("CW", "Chen Wei", "Wei Software", 8)}
                          </ul>
                        </li>
                        <li class="mat-itemlist__group">
                          <h3 class="mat-itemlist__divider">D</h3>
                          <ul class="mat-itemlist__items">
                            {Contact("DK", "David Kim", "Kim Architects", 8)}
                            {Contact("DP", "Dina Petrova", "Petrova Design", 8)}
                          </ul>
                        </li>
                        <li class="mat-itemlist__group">
                          <h3 class="mat-itemlist__divider">E</h3>
                          <ul class="mat-itemlist__items">
                            {Contact("EG", "Elena Garcia", "Garcia Foods", 8)}
                            {Contact("EN", "Emil Novak", "Novak Electric", 8)}
                          </ul>
                        </li>
                        <li class="mat-itemlist__empty" hidden></li>
                      </ul>
                    </div>
                    """, "Scroll the list: the letter of the group stays at the top until the next one pushes it away. Type in the field: the rows that do not contain the text are hidden, a letter with no row left goes with them, and a screen reader hears how many are shown. Try ben or muller."),
                new("An inbox: unread rows, the time, a preview", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Inbox">
                      <li class="mat-item mat-item--top is-unread">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__dot" aria-hidden="true"></span>
                          <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">JD</span></span>
                          <span class="mat-item__body">
                            <span class="mat-item__title"><span class="mat-sr-only">Unread: </span>Jamie Doe</span>
                            <span class="mat-item__text">Invoice for September</span>
                            <span class="mat-item__text mat-item__text--clamp">Hi Alex, the invoice is attached. Please let me know if the numbers look right, so that I can send the final one before the end of the month.</span>
                          </span>
                          <span class="mat-item__end"><time class="mat-item__note" datetime="2026-10-12T09:41">09:41</time></span>
                        </a>
                      </li>
                      <li class="mat-item mat-item--top is-unread">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__dot" aria-hidden="true"></span>
                          <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">SL</span></span>
                          <span class="mat-item__body">
                            <span class="mat-item__title"><span class="mat-sr-only">Unread: </span>Sam Lee</span>
                            <span class="mat-item__text">Lunch on Friday?</span>
                            <span class="mat-item__text mat-item__text--clamp">There is a new place around the corner. Are you in?</span>
                          </span>
                          <span class="mat-item__end"><time class="mat-item__note" datetime="2026-10-12T08:15">08:15</time></span>
                        </a>
                      </li>
                      <li class="mat-item mat-item--top">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">KP</span></span>
                          <span class="mat-item__body">
                            <span class="mat-item__title">Kim Park</span>
                            <span class="mat-item__text">Re: the meeting notes</span>
                            <span class="mat-item__text mat-item__text--clamp">Thanks, that is exactly what we agreed on. I will add the dates to the calendar.</span>
                          </span>
                          <span class="mat-item__end"><time class="mat-item__note" datetime="2026-10-09">Fri</time></span>
                        </a>
                      </li>
                    </ul>
                    """, "Unread is told three ways, not by colour alone (COLOR-04): a bold title, a dot, and the words Unread for a screen reader. The preview is clamped to two lines."),
                new("Settings: icon tiles, switches, values", $"""
                    <div class="mat-stack">
                      <section class="mat-stack mat-stack--sm" aria-labelledby="il-set-net">
                        <h3 class="mat-itemlist__header" id="il-set-net">Network</h3>
                        <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--lines-inset" aria-labelledby="il-set-net">
                          <li class="mat-item">
                            <label class="mat-item__main" for="il-set-wifi">
                              <span class="mat-item__lead"><span class="mat-item__tile mat-item__tile--accent">{I("network")}</span></span>
                              <span class="mat-item__body"><span class="mat-item__title" id="il-set-wifi-t">Wi-Fi</span><span class="mat-item__text">Home network</span></span>
                            </label>
                            <div class="mat-item__aside"><label class="mat-switch mat-switch--bare"><input type="checkbox" id="il-set-wifi" aria-labelledby="il-set-wifi-t" checked /><span class="mat-switch__track"></span></label></div>
                          </li>
                          <li class="mat-item">
                            <label class="mat-item__main" for="il-set-data">
                              <span class="mat-item__lead"><span class="mat-item__tile">{I("smartphone")}</span></span>
                              <span class="mat-item__body"><span class="mat-item__title" id="il-set-data-t">Mobile data</span></span>
                            </label>
                            <div class="mat-item__aside"><label class="mat-switch mat-switch--bare"><input type="checkbox" id="il-set-data" aria-labelledby="il-set-data-t" /><span class="mat-switch__track"></span></label></div>
                          </li>
                          <li class="mat-item">
                            <a class="mat-item__main" href="#">
                              <span class="mat-item__lead"><span class="mat-item__tile">{I("shield")}</span></span>
                              <span class="mat-item__body"><span class="mat-item__title">VPN</span></span>
                              <span class="mat-item__end"><span class="mat-item__note">Not connected</span>{Chev}</span>
                            </a>
                          </li>
                        </ul>
                      </section>
                      <section class="mat-stack mat-stack--sm" aria-labelledby="il-set-pref">
                        <h3 class="mat-itemlist__header" id="il-set-pref">Preferences</h3>
                        <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--lines-inset" aria-labelledby="il-set-pref">
                          <li class="mat-item">
                            <a class="mat-item__main" href="#">
                              <span class="mat-item__lead"><span class="mat-item__tile">{I("globe")}</span></span>
                              <span class="mat-item__body"><span class="mat-item__title">Language</span></span>
                              <span class="mat-item__end"><span class="mat-item__note">English</span>{Chev}</span>
                            </a>
                          </li>
                          <li class="mat-item">
                            <a class="mat-item__main" href="#">
                              <span class="mat-item__lead"><span class="mat-item__tile">{I("palette")}</span></span>
                              <span class="mat-item__body"><span class="mat-item__title">Appearance</span></span>
                              <span class="mat-item__end"><span class="mat-item__note">System</span>{Chev}</span>
                            </a>
                          </li>
                          <li class="mat-item">
                            <label class="mat-item__main" for="il-set-quiet">
                              <span class="mat-item__lead"><span class="mat-item__tile">{I("moon")}</span></span>
                              <span class="mat-item__body"><span class="mat-item__title" id="il-set-quiet-t">Do not disturb</span><span class="mat-item__text">From 22:00 to 07:00</span></span>
                            </label>
                            <div class="mat-item__aside"><label class="mat-switch mat-switch--bare"><input type="checkbox" id="il-set-quiet" aria-labelledby="il-set-quiet-t" /><span class="mat-switch__track"></span></label></div>
                          </li>
                        </ul>
                      </section>
                    </div>
                    """, "A row with a switch is a label for it, so a press on the text toggles it, and the switch has the title as its name. A row that opens a page shows its value and the arrow."),
                new("Files: thumbnails, sizes and a menu", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Files">
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__thumb">{I("file-text")}</span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Quarterly report.pdf</span><span class="mat-item__text">Finance, edited today</span></span>
                          <span class="mat-item__end"><span class="mat-item__note">412 KB</span></span>
                        </a>
                        <div class="mat-item__aside"><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="More actions for Quarterly report.pdf" data-tip="More">{I("more-vertical")}</button></div>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__thumb">{I("folder")}</span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Holiday photos</span><span class="mat-item__text">Folder, 128 items</span></span>
                          <span class="mat-item__end"><span class="mat-item__note">5.1 GB</span></span>
                        </a>
                        <div class="mat-item__aside"><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="More actions for Holiday photos" data-tip="More">{I("more-vertical")}</button></div>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__thumb"><img src="/favicon.svg" alt="" width="40" height="40" /></span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Scan 2026-10-02.png</span><span class="mat-item__text">Pictures, yesterday</span></span>
                          <span class="mat-item__end"><span class="mat-item__note">1.8 MB</span></span>
                        </a>
                        <div class="mat-item__aside"><button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="More actions for Scan 2026-10-02.png" data-tip="More">{I("more-vertical")}</button></div>
                      </li>
                    </ul>
                    """, "The thumbnail is a picture or, for a file without one, the icon of its kind. The size is a note at the end. The button beside the row is a menu: it is not inside the link."),
                new("Tasks: a check, done is struck through", $"""
                    <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--tasks" aria-label="Tasks">
                      <li class="mat-item">
                        <label class="mat-item__main" for="il-task-1">
                          <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-task-1" /></span>
                          <span class="mat-item__body"><span class="mat-item__title">Order a new router</span><span class="mat-item__text">Due tomorrow</span></span>
                        </label>
                      </li>
                      <li class="mat-item">
                        <label class="mat-item__main" for="il-task-2">
                          <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-task-2" checked /></span>
                          <span class="mat-item__body"><span class="mat-item__title">Renew the certificate</span><span class="mat-item__text">Done yesterday</span></span>
                        </label>
                      </li>
                      <li class="mat-item">
                        <label class="mat-item__main" for="il-task-3">
                          <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-task-3" /></span>
                          <span class="mat-item__body"><span class="mat-item__title">Check the backups</span><span class="mat-item__text">Every Monday</span></span>
                          <span class="mat-item__end"><span class="mat-pill mat-pill--warn">Late</span></span>
                        </label>
                      </li>
                    </ul>
                    """, "The whole row is the label of the check. A task that is done is muted and struck through as well as checked, so the state does not rest on a colour."),
                new("Notifications: a tone, never only a colour", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Notifications">
                      <li class="mat-item mat-item--top">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__tile mat-item__tile--ok">{I("check-circle")}</span></span>
                          <span class="mat-item__body"><span class="mat-item__title"><span class="mat-sr-only">Success: </span>Backup finished</span><span class="mat-item__text">Documents, 412 MB in 14 seconds</span></span>
                          <span class="mat-item__end"><time class="mat-item__note" datetime="2026-10-12T09:41">09:41</time></span>
                        </a>
                      </li>
                      <li class="mat-item mat-item--top">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__tile mat-item__tile--warn">{I("alert")}</span></span>
                          <span class="mat-item__body"><span class="mat-item__title"><span class="mat-sr-only">Warning: </span>Disk almost full</span><span class="mat-item__text">92 % of the storage is used</span></span>
                          <span class="mat-item__end"><time class="mat-item__note" datetime="2026-10-12T08:02">08:02</time></span>
                        </a>
                      </li>
                      <li class="mat-item mat-item--top">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__tile mat-item__tile--error">{I("x-circle")}</span></span>
                          <span class="mat-item__body"><span class="mat-item__title"><span class="mat-sr-only">Error: </span>Mail server did not answer</span><span class="mat-item__text">Three tries failed</span></span>
                          <span class="mat-item__end"><time class="mat-item__note" datetime="2026-10-11T23:15">Yesterday</time></span>
                        </a>
                      </li>
                      <li class="mat-item mat-item--top">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__tile mat-item__tile--info">{I("info")}</span></span>
                          <span class="mat-item__body"><span class="mat-item__title"><span class="mat-sr-only">Information: </span>An update is available</span><span class="mat-item__text">Version 1.5 brings lists of rows</span></span>
                          <span class="mat-item__end"><time class="mat-item__note" datetime="2026-10-10">Sat</time></span>
                        </a>
                      </li>
                    </ul>
                    """, "The tone is the colour of the tile, the shape of its icon (check, triangle, cross, i) and a hidden word for a screen reader (COLOR-03, COLOR-04)."),
                new("Products: a picture, a title, a price", $"""
                    <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--lead-lg mat-itemlist--lines-inset" aria-label="Products">
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__thumb"><img src="/favicon.svg" alt="" width="56" height="56" /></span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Router with Wi-Fi 7</span><span class="mat-item__text">Dual band, four ports</span><span class="mat-item__text">In stock</span></span>
                          <span class="mat-item__end"><span class="mat-item__note mat-item__note--strong">€ 129.00</span></span>
                        </a>
                      </li>
                      <li class="mat-item">
                        <a class="mat-item__main" href="#">
                          <span class="mat-item__lead"><span class="mat-item__thumb"><img src="/favicon.svg" alt="" width="56" height="56" /></span></span>
                          <span class="mat-item__body"><span class="mat-item__title">Network cable, 5 m</span><span class="mat-item__text">Cat 6, shielded</span><span class="mat-item__text">Ships in 3 days</span></span>
                          <span class="mat-item__end"><span class="mat-item__note mat-item__note--strong">€ 8.90</span></span>
                        </a>
                      </li>
                    </ul>
                    """, "mat-itemlist--lead-lg makes the lead 56 px wide, and the lines from the text follow it. The price is a note in the strong style; a third line says what the others do not."),
                new("Loading: a skeleton", """
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Loading the messages" aria-busy="true">
                      <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-skeleton mat-skeleton--circle"></span></span><span class="mat-item__body"><span class="mat-skeleton mat-skeleton--w-50"></span><span class="mat-skeleton mat-skeleton--w-75"></span></span></div></li>
                      <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-skeleton mat-skeleton--circle"></span></span><span class="mat-item__body"><span class="mat-skeleton mat-skeleton--w-50"></span><span class="mat-skeleton mat-skeleton--w-75"></span></span></div></li>
                      <li class="mat-item"><div class="mat-item__main"><span class="mat-item__lead"><span class="mat-skeleton mat-skeleton--circle"></span></span><span class="mat-item__body"><span class="mat-skeleton mat-skeleton--w-50"></span><span class="mat-skeleton mat-skeleton--w-75"></span></span></div></li>
                    </ul>
                    """, "The rows have the shape of the rows that will come, so the page does not jump when they arrive. The list is aria-busy while it loads; with reduced motion the shimmer stops."),
                new("Empty", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Messages">
                      <li class="mat-itemlist__empty">
                        <div class="mat-empty">
                          <span class="mat-empty__icon">{I("mail")}</span>
                          <div class="mat-empty__title">No messages yet</div>
                          <p>Messages that you receive appear here.</p>
                          <a class="mat-btn mat-btn--primary" href="#">{I("plus")}<span>Write a message</span></a>
                        </div>
                      </li>
                    </ul>
                    """, "An empty list shows an icon, a title, a sentence and the action that fills it (LIST-11). The same row, hidden, is what a filter shows when no row matches."),
            ],
            [
                "A row is built the same way whatever it lists; only the slots change. Put the most important thing in the title and what helps to tell two rows apart in the first line under it.",
                "Dividers in a flat list are rows of their own. For sections with a sticky letter use groups: a `li.mat-itemlist__group` with an `h3.mat-itemlist__divider` and a `ul.mat-itemlist__items`, and `mat-itemlist--sticky` on the list.",
                "A sticky divider stays at the top of the list that scrolls (`mat-itemlist--scroll`) or of the page; `--mat-itemlist-top` sets how far from the top, for a list in a page under a bar that stays.",
                "`data-item-filter=\"id of the list\"` on a search field hides the rows that do not contain its text, ignoring case and accents. A group with no row left is hidden with them, and `li.mat-itemlist__empty` is shown when no row matches.",
                "The filter sends `mat:item-filter` with the list, the text, the number shown and the total. A screen reader is told how many rows are shown. `data-item-search` on a row replaces the text that is searched.",
                "A state is told in more than a colour (COLOR-04): unread is bold with a dot, done is struck through, a tone has its own icon, selected has a check. The hidden words are in `mat-sr-only`.",
                "The texts the script adds come in English and German; `data-il-<name>` on `<html>` changes one (for example `data-il-empty` for the text of an empty filter).",
            ]),

        new("itemlist-choices", "Item list: choosing and typing", "Lists and data", "check-square",
            "Rows that are chosen or filled in: one of several, some of several, rows that are selected with a select-all and a bar of actions, and rows that hold a field.",
            ["LIST-15", "FORM-08", "COLOR-04", "COLOR-08", "A11Y-02", "MOBILE-04"],
            [
                new("Choose one", $"""
                    <fieldset class="mat-fieldset">
                      <legend class="mat-itemlist__header">Delivery</legend>
                      <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--choices mat-itemlist--lines-inset">
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-del-std">
                            <span class="mat-item__lead"><input class="mat-item__check" type="radio" id="il-del-std" name="il-delivery" value="standard" checked /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Standard</span><span class="mat-item__text">3 to 5 days</span></span>
                            <span class="mat-item__end"><span class="mat-item__note">Free</span></span>
                          </label>
                        </li>
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-del-exp">
                            <span class="mat-item__lead"><input class="mat-item__check" type="radio" id="il-del-exp" name="il-delivery" value="express" /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Express</span><span class="mat-item__text">Tomorrow before 12:00</span></span>
                            <span class="mat-item__end"><span class="mat-item__note">€ 9.90</span></span>
                          </label>
                        </li>
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-del-pick">
                            <span class="mat-item__lead"><input class="mat-item__check" type="radio" id="il-del-pick" name="il-delivery" value="pickup" /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Pick up in the shop</span><span class="mat-item__text">Ready in two hours</span></span>
                            <span class="mat-item__end"><span class="mat-item__note">Free</span></span>
                          </label>
                        </li>
                      </ul>
                    </fieldset>
                    """, "The group is a fieldset whose legend is the header, so a screen reader says what is chosen from. The row that is chosen is a tint behind the whole row and a stronger title as well as the filled radio. The arrow keys move between the rows, as they do between radios."),
                new("Choose several", $"""
                    <fieldset class="mat-fieldset">
                      <legend class="mat-itemlist__header">Tell me about</legend>
                      <ul class="mat-itemlist mat-itemlist--inset mat-itemlist--choices mat-itemlist--lines-inset">
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-ntf-1">
                            <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-ntf-1" name="il-notify" value="messages" checked /></span>
                            <span class="mat-item__body"><span class="mat-item__title">New messages</span></span>
                          </label>
                        </li>
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-ntf-2">
                            <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-ntf-2" name="il-notify" value="mentions" checked /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Mentions</span><span class="mat-item__text">When someone writes your name</span></span>
                          </label>
                        </li>
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-ntf-3">
                            <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-ntf-3" name="il-notify" value="summary" /></span>
                            <span class="mat-item__body"><span class="mat-item__title">A summary every week</span></span>
                          </label>
                        </li>
                      </ul>
                    </fieldset>
                    """, "The same rows with checks. They are part of a form that is saved with a button (FORM-04 says a switch is for what takes effect at once)."),
                new("Select rows: a select-all and a bar of actions", $"""
                    <div class="mat-stack mat-stack--sm" data-selectable>
                      <div class="mat-bulk" data-bulk-bar hidden role="status">
                        <span class="mat-bulk__count"><strong data-bulk-count>0</strong> selected</span>
                        <button type="button" class="mat-btn mat-btn--secondary mat-btn--sm">Download</button>
                        <button type="button" class="mat-btn mat-btn--danger mat-btn--sm" data-confirm="Delete the selected files? This cannot be undone." data-confirm-ok="Delete">Delete</button>
                      </div>
                      <ul class="mat-itemlist mat-itemlist--inset" id="il-select" aria-label="Files to select">
                        <li class="mat-itemlist__divider">
                          <label class="mat-item__main" for="il-select-all">
                            <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-select-all" data-select-all /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Select all</span></span>
                          </label>
                        </li>
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-sel-1">
                            <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-sel-1" data-select-row /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Quarterly report.pdf</span><span class="mat-item__text">412 KB</span></span>
                          </label>
                        </li>
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-sel-2">
                            <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-sel-2" data-select-row /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Insurance policy.pdf</span><span class="mat-item__text">188 KB</span></span>
                          </label>
                        </li>
                        <li class="mat-item">
                          <label class="mat-item__main" for="il-sel-3">
                            <span class="mat-item__lead"><input class="mat-item__check" type="checkbox" id="il-sel-3" data-select-row /></span>
                            <span class="mat-item__body"><span class="mat-item__title">Tax return 2025.pdf</span><span class="mat-item__text">96 KB</span></span>
                          </label>
                        </li>
                      </ul>
                    </div>
                    """, "The same attributes as a table: data-selectable around the list and the bar, data-select-row on each check, data-select-all in the header. A selected row is marked is-selected; select-all shows the state in between, and with a filter it takes the rows that are shown."),
                new("Fields in rows", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" aria-label="Contact details">
                      <li class="mat-item mat-item--field mat-item--inline">
                        <div class="mat-item__main"><span class="mat-item__body"><label class="mat-item__label" for="il-f-name">Name</label><input class="mat-item__input" id="il-f-name" name="name" value="Alex Morgan" autocomplete="name" /></span></div>
                      </li>
                      <li class="mat-item mat-item--field mat-item--inline">
                        <div class="mat-item__main"><span class="mat-item__body"><label class="mat-item__label" for="il-f-mail">E-mail</label><input class="mat-item__input" id="il-f-mail" name="mail" type="email" value="alex@example.com" autocomplete="email" /></span></div>
                      </li>
                      <li class="mat-item mat-item--field mat-item--inline">
                        <div class="mat-item__main"><span class="mat-item__body"><label class="mat-item__label" for="il-f-phone">Phone</label><input class="mat-item__input" id="il-f-phone" name="phone" type="tel" placeholder="Not set" autocomplete="tel" /></span></div>
                      </li>
                      <li class="mat-item mat-item--field mat-item--inline">
                        <div class="mat-item__main"><span class="mat-item__body"><label class="mat-item__label" for="il-f-role">Role</label><select class="mat-item__input" id="il-f-role" name="role"><option>Administrator</option><option>User</option><option>Guest</option></select></span></div>
                      </li>
                    </ul>
                    """, "The label is above the value, and beside it from 480 px with mat-item--inline. The row shows the focus as a ring around the whole row. For a form that is validated use the fields of the forms (FORM-03); this is for short lists of settings."),
            ],
            [
                "A row that is chosen has a native input: a radio or a check with the class `mat-item__check` inside a `label` that is `mat-item__main`, so the whole row is the target and the browser does the arrow keys and the grouping.",
                "`mat-itemlist--choices` tints the row of a checked input (`:has(:checked)`). Without it a checked row is only checked: use it where the checks are the choice, not where they are a to-do list.",
                "`mat-itemlist--tasks` strikes through the title of a checked row and mutes it; `is-done` does the same for a row that the server draws as done.",
                "Rows that are selected: `data-selectable` around the list, `data-select-row` on each check and `data-select-all` in the divider, as for a table. The script marks the rows `is-selected` and fills the bar `data-bulk-bar` with the count.",
                "Every input has an id and a label that points to it (FORM-08); a switch beside a row is named by the title through `aria-labelledby`.",
                "A field in a row is `mat-item__input` under a `mat-item__label`: it has no border of its own, the focus is the ring of the row.",
            ]),

        new("itemlist-actions", "Item list: sliding actions and reordering", "Lists and data", "sort",
            "What the script adds: a row that slides sideways to show its actions, by a swipe or without one, and a handle that reorders the rows with the pointer or the keyboard.",
            ["LIST-15", "LIST-08", "A11Y-02", "A11Y-05", "ICON-03", "MOBILE-04", "COLOR-04"],
            [
                new("Swipe a row to show its actions", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" id="il-slide" aria-label="Inbox with actions" data-itemlist>
                      <li class="mat-item mat-item--sliding mat-item--top is-unread" data-item-key="invoice">
                        <div class="mat-item__row">
                          <a class="mat-item__main" href="#">
                            <span class="mat-item__dot" aria-hidden="true"></span>
                            <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">JD</span></span>
                            <span class="mat-item__body"><span class="mat-item__title"><span class="mat-sr-only">Unread: </span>Jamie Doe</span><span class="mat-item__text">Invoice for September</span></span>
                            <span class="mat-item__end"><span class="mat-item__note">09:41</span></span>
                          </a>
                          <div class="mat-item__options mat-item__options--end">
                            <button type="button" class="mat-item__option" data-item-action="archive">{I("archive")}<span>Archive</span></button>
                            <button type="button" class="mat-item__option mat-item__option--accent" data-item-action="star">{I("star")}<span>Star</span></button>
                            <button type="button" class="mat-item__option mat-item__option--danger" data-item-action="delete" data-confirm="Delete this message? This cannot be undone." data-confirm-ok="Delete">{I("trash")}<span>Delete</span></button>
                          </div>
                        </div>
                      </li>
                      <li class="mat-item mat-item--sliding mat-item--top" data-item-key="lunch">
                        <div class="mat-item__row">
                          <a class="mat-item__main" href="#">
                            <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">SL</span></span>
                            <span class="mat-item__body"><span class="mat-item__title">Sam Lee</span><span class="mat-item__text">Lunch on Friday?</span></span>
                            <span class="mat-item__end"><span class="mat-item__note">08:15</span></span>
                          </a>
                          <div class="mat-item__options mat-item__options--end">
                            <button type="button" class="mat-item__option" data-item-action="archive">{I("archive")}<span>Archive</span></button>
                            <button type="button" class="mat-item__option mat-item__option--accent" data-item-action="star">{I("star")}<span>Star</span></button>
                            <button type="button" class="mat-item__option mat-item__option--danger" data-item-action="delete" data-confirm="Delete this message? This cannot be undone." data-confirm-ok="Delete">{I("trash")}<span>Delete</span></button>
                          </div>
                        </div>
                      </li>
                      <li class="mat-item mat-item--sliding mat-item--top" data-item-key="notes">
                        <div class="mat-item__row">
                          <a class="mat-item__main" href="#">
                            <span class="mat-item__lead"><span class="mat-avatar" aria-hidden="true">KP</span></span>
                            <span class="mat-item__body"><span class="mat-item__title">Kim Park</span><span class="mat-item__text">Re: the meeting notes</span></span>
                            <span class="mat-item__end"><span class="mat-item__note">Fri</span></span>
                          </a>
                          <div class="mat-item__options mat-item__options--end">
                            <button type="button" class="mat-item__option" data-item-action="archive">{I("archive")}<span>Archive</span></button>
                            <button type="button" class="mat-item__option mat-item__option--accent" data-item-action="star">{I("star")}<span>Star</span></button>
                            <button type="button" class="mat-item__option mat-item__option--danger" data-item-action="delete" data-confirm="Delete this message? This cannot be undone." data-confirm-ok="Delete">{I("trash")}<span>Delete</span></button>
                          </div>
                        </div>
                      </li>
                    </ul>
                    """, "Drag a row to the left with the mouse or swipe it on a touch screen: far enough (or fast enough) and it stays open. Without a swipe use the More button at the end of the row, or the Left arrow on the row. An action sends mat:item-action; Delete asks first (LIST-08)."),
                new("Actions on both sides", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" id="il-both" aria-label="Tasks with actions" data-itemlist>
                      <li class="mat-item mat-item--sliding" data-item-key="router">
                        <div class="mat-item__row">
                          <div class="mat-item__options mat-item__options--start">
                            <button type="button" class="mat-item__option mat-item__option--accent" data-item-action="done">{I("check")}<span>Done</span></button>
                          </div>
                          <a class="mat-item__main" href="#"><span class="mat-item__body"><span class="mat-item__title">Order a new router</span><span class="mat-item__text">Due tomorrow</span></span></a>
                          <div class="mat-item__options mat-item__options--end">
                            <button type="button" class="mat-item__option" data-item-action="later">{I("clock")}<span>Later</span></button>
                            <button type="button" class="mat-item__option mat-item__option--danger" data-item-action="remove">{I("trash")}<span>Remove</span></button>
                          </div>
                        </div>
                      </li>
                      <li class="mat-item mat-item--sliding" data-item-key="certificate">
                        <div class="mat-item__row">
                          <div class="mat-item__options mat-item__options--start">
                            <button type="button" class="mat-item__option mat-item__option--accent" data-item-action="done">{I("check")}<span>Done</span></button>
                          </div>
                          <a class="mat-item__main" href="#"><span class="mat-item__body"><span class="mat-item__title">Renew the certificate</span><span class="mat-item__text">Due on Friday</span></span></a>
                          <div class="mat-item__options mat-item__options--end">
                            <button type="button" class="mat-item__option" data-item-action="later">{I("clock")}<span>Later</span></button>
                            <button type="button" class="mat-item__option mat-item__option--danger" data-item-action="remove">{I("trash")}<span>Remove</span></button>
                          </div>
                        </div>
                      </li>
                    </ul>
                    """, "Options at the start (mat-item__options--start) are shown by dragging to the right, the ones at the end by dragging to the left. The More button opens the end side and closes it again; the Right arrow on the row opens the start side and the Left arrow the end side."),
                new("Reorder with a handle", $"""
                    <ul class="mat-itemlist mat-itemlist--inset" id="il-reorder" aria-label="Playlist" data-itemlist data-reorder>
                      <li class="mat-item" data-item-key="morning">
                        <div class="mat-item__main"><span class="mat-item__lead"><span class="mat-item__tile">{I("play")}</span></span><span class="mat-item__body"><span class="mat-item__title">Morning</span><span class="mat-item__text">3:12</span></span></div>
                        <div class="mat-item__aside"><button type="button" class="mat-item__handle" aria-label="Reorder Morning" data-tip="Reorder">{I("menu")}</button></div>
                      </li>
                      <li class="mat-item" data-item-key="commute">
                        <div class="mat-item__main"><span class="mat-item__lead"><span class="mat-item__tile">{I("play")}</span></span><span class="mat-item__body"><span class="mat-item__title">Commute</span><span class="mat-item__text">4:05</span></span></div>
                        <div class="mat-item__aside"><button type="button" class="mat-item__handle" aria-label="Reorder Commute" data-tip="Reorder">{I("menu")}</button></div>
                      </li>
                      <li class="mat-item" data-item-key="focus">
                        <div class="mat-item__main"><span class="mat-item__lead"><span class="mat-item__tile">{I("play")}</span></span><span class="mat-item__body"><span class="mat-item__title">Focus</span><span class="mat-item__text">2:48</span></span></div>
                        <div class="mat-item__aside"><button type="button" class="mat-item__handle" aria-label="Reorder Focus" data-tip="Reorder">{I("menu")}</button></div>
                      </li>
                      <li class="mat-item" data-item-key="evening">
                        <div class="mat-item__main"><span class="mat-item__lead"><span class="mat-item__tile">{I("play")}</span></span><span class="mat-item__body"><span class="mat-item__title">Evening</span><span class="mat-item__text">5:30</span></span></div>
                        <div class="mat-item__aside"><button type="button" class="mat-item__handle" aria-label="Reorder Evening" data-tip="Reorder">{I("menu")}</button></div>
                      </li>
                      <li class="mat-item" data-item-key="night">
                        <div class="mat-item__main"><span class="mat-item__lead"><span class="mat-item__tile">{I("play")}</span></span><span class="mat-item__body"><span class="mat-item__title">Night</span><span class="mat-item__text">6:01</span></span></div>
                        <div class="mat-item__aside"><button type="button" class="mat-item__handle" aria-label="Reorder Night" data-tip="Reorder">{I("menu")}</button></div>
                      </li>
                    </ul>
                    """, "Drag a handle with the mouse or a finger: the others make room and the list scrolls near its edge. With the keyboard focus a handle and press Space to pick the row up, move it with the arrow keys (Home and End go to the ends), drop it with Space or Enter or put it back with Escape. mat:reorder tells the new order."),
            ],
            [
                "A row that slides is `mat-item--sliding` with a `mat-item__row` that holds `mat-item__main` and one or two `mat-item__options` (`--start`, `--end`), each a few `mat-item__option` buttons with an icon above a word. The list carries `data-itemlist`.",
                "The row slides by a pointer drag or a swipe: past 40 % of the options, or fast, it stays open; another press, Escape, a press elsewhere or an action closes it. A vertical move is the page scrolling, and a link is not followed by the click that ends a swipe.",
                "Without a swipe: the script adds a More button to the row (named \"More actions for\" and the title, with `aria-expanded`) that opens the end side, and the Left and Right arrows on the row open the end or the start side. The first option takes the focus; Escape closes the row and returns the focus.",
                "The options are `inert` and hidden while the row is closed, so the Tab key never enters them; they are only in the tab order while the row is open. When the focus leaves an open row it closes. Without the script the options stand at the end of the row as buttons.",
                "An option sends `mat:item-action` with the row, the list and the action (`data-item-action`, or the lower-case text). It does not remove anything: the app does that. An option that asks first (`data-confirm`) sends the event when the question is answered.",
                "Reordering: `data-reorder` on the list and a `mat-item__handle` in each row (an icon button with an `aria-label`; the script adds one that is missing). Rows move within their list or group; the rows that are hidden by a filter stay where they are.",
                "A handle works with the pointer (the row follows it and the others make room), and with the keyboard: Space or Enter picks up and drops, the arrow keys, Home and End move, Escape cancels, Tab drops. A live region says what happens: \"Morning picked up, position 1 of 5\".",
                "`mat:reorder` is sent on the row when it is dropped in another place: `detail.order` is the keys of all rows in their new order (`data-item-key`, else the id, else the title), `detail.from` and `detail.to` the positions. Nothing is sent when the order has not changed.",
                "With reduced motion the rows do not slide into place and the transitions of the sliding row stop (A11Y-05). Touch: the handle and the options are at least as high as the row, which is 48 px or more (MOBILE-04); the handle does not scroll the page, the rest of the row does.",
            ]),
    ];
}
