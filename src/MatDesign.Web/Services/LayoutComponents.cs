namespace MatDesign.Web.Services;

/// <summary>
/// The layout system and the utility classes (matui.layout.css): breakpoints and containers, the grid of twelve columns, stack, row and auto grid;
/// spacing, display, flex, text, colour, borders and sizing. They are shown on their own pages (Layout, Utilities) like the components.
/// </summary>
internal static class LayoutComponents
{
    private static readonly int[] Scale = [0, 1, 2, 3, 4, 5, 6, 8, 10, 12];

    private static string SpaceRows(string prefix) => string.Concat(Scale.Select(n =>
        $"""
                            <tr><td data-label="Class"><code>mat-{prefix}-{n}</code></td><td data-label="Value" class="mat-num mat-mono">{n * 4} px</td><td data-label="Token" class="mat-hide-sm"><code>{(n == 0 ? "0" : "--mat-space-" + n)}</code></td></tr>
        """));

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("breakpoints", "Breakpoints and containers", "Layout", "monitor",
            "Three breakpoints, mobile first: a class without a breakpoint applies everywhere, a class with one from that width up. A container keeps a page that has no shell to a readable width.",
            ["MOBILE-01", "MOBILE-02", "SHELL-10"],
            [
                new("The breakpoints", $"""
                    <div class="mat-table-wrap">
                      <table class="mat-table">
                        <caption class="mat-sr-only">The breakpoints of the layout classes</caption>
                        <thead><tr><th scope="col">Name</th><th scope="col" class="mat-num">From</th><th scope="col">What it is for</th></tr></thead>
                        <tbody>
                          <tr><td data-label="Name"><code>(none)</code></td><td data-label="From" class="mat-num mat-mono">0</td><td data-label="What it is for">The phone: one column of everything. A class without a breakpoint applies here and up.</td></tr>
                          <tr><td data-label="Name"><code>sm</code></td><td data-label="From" class="mat-num mat-mono">600 px</td><td data-label="What it is for">A large phone or a small tablet: two columns (<code>mat-col-sm-6</code>).</td></tr>
                          <tr><td data-label="Name"><code>md</code></td><td data-label="From" class="mat-num mat-mono">900 px</td><td data-label="What it is for">The menu is a column beside the page, not a drawer (SHELL-01): three columns, things beside each other.</td></tr>
                          <tr><td data-label="Name"><code>lg</code></td><td data-label="From" class="mat-num mat-mono">1200 px</td><td data-label="What it is for">A wide screen: four columns. The content of a page is at most 1240 px wide.</td></tr>
                        </tbody>
                      </table>
                    </div>
                    """, "Resize the window while you look at the grid examples below. A few components have a threshold of their own: a dialog and a picker fill the screen below 640 px, a table becomes cards below 720 px, the labels of a wide form go above the fields below 700 px. The component sets them, not a class."),
                new("Containers", """
                    <div class="mat-stack">
                      <div class="mat-container mat-border mat-rounded mat-p-3">mat-container: the width of the content of a page (at most 1240 px) with the side padding of the page</div>
                      <div class="mat-container mat-container--narrow mat-border mat-rounded mat-p-3">mat-container--narrow: 780 px, for a form or an article</div>
                      <div class="mat-container mat-container--wide mat-border mat-rounded mat-p-3">mat-container--wide: the whole width</div>
                    </div>
                    """, "A page inside the shell is already a container: it is `mat-page` (with `--narrow` or `--wide`). `mat-container` is for a page without the shell, such as the sign-in page or a shared link (SHELL-11)."),
            ],
            [
                "Mobile first: write the phone's layout without a breakpoint and add what changes on a wider screen (`mat-col-12 mat-col-md-6`), not the other way round.",
                "A media query cannot read a custom property, so the breakpoints are numbers in the stylesheet (600, 900 and 1200 px) and in the documentation. They are the same in every app.",
            ]),

        new("grid-cols", "Grid of twelve columns", "Layout", "grid",
            "A grid of twelve columns for a page that needs more than a row of cards: columns that span, change with the screen, are moved and nested.",
            ["MOBILE-01", "SPACE-01"],
            [
                new("Columns that span", """
                    <div class="mat-stack">
                      <div class="mat-cols"><div class="mat-col-6 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">6</div><div class="mat-col-6 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">6</div></div>
                      <div class="mat-cols"><div class="mat-col-4 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">4</div><div class="mat-col-4 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">4</div><div class="mat-col-4 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">4</div></div>
                      <div class="mat-cols"><div class="mat-col-8 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">8</div><div class="mat-col-4 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">4</div></div>
                      <div class="mat-cols"><div class="mat-col-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">3</div><div class="mat-col-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">3</div><div class="mat-col-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">3</div><div class="mat-col-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">3</div></div>
                    </div>
                    """, "A child of `mat-cols` spans all twelve columns until a class says otherwise. A class without a breakpoint (`mat-col-6`) applies at every width, a phone included; for one column on a phone and two beside each other on a wide screen write `mat-col-12 mat-col-md-6` (next example)."),
                new("Changing with the screen", """
                    <div class="mat-cols">
                      <div class="mat-col-12 mat-col-sm-6 mat-col-lg-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">1</div>
                      <div class="mat-col-12 mat-col-sm-6 mat-col-lg-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">2</div>
                      <div class="mat-col-12 mat-col-sm-6 mat-col-lg-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">3</div>
                      <div class="mat-col-12 mat-col-sm-6 mat-col-lg-3 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">4</div>
                    </div>
                    """, "One column on a phone, two from 600 px, four from 1200 px: `mat-col-12 mat-col-sm-6 mat-col-lg-3`. Resize the window to see it."),
                new("Offsets and order", """
                    <div class="mat-stack">
                      <div class="mat-cols"><div class="mat-col-6 mat-col-start-4 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">6 columns, starting at the 4th</div></div>
                      <div class="mat-cols"><div class="mat-col-4 mat-order-last mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">first in the markup, last on the screen</div><div class="mat-col-8 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">second in the markup</div></div>
                    </div>
                    """, "Offsets use `mat-col-start-N`; `mat-order-first` and `mat-order-last` change the order on the screen, not for a screen reader or the Tab key: keep the markup in reading order."),
                new("Gaps, alignment and nesting", """
                    <div class="mat-cols mat-gap-2 mat-items-center">
                      <div class="mat-col-4 mat-tint-accent mat-rounded-sm mat-p-8 mat-text-center">taller</div>
                      <div class="mat-col-4 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">centred</div>
                      <div class="mat-col-4">
                        <div class="mat-cols mat-gap-1">
                          <div class="mat-col-6 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">nested</div><div class="mat-col-6 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">nested</div>
                        </div>
                      </div>
                    </div>
                    """, "`mat-gap-N` sets the gap on the 4 px scale (the default is 16 px); `mat-items-*` aligns the cells of a row; a column can hold its own grid."),
            ],
            [
                "It is CSS grid: a row stays aligned, cells that add up to more than twelve go to the next row, and `min-width: 0` on a child keeps a long word or a wide table from pushing the grid wider than the screen.",
                "For cards of an unknown number that fill the row, use the auto grid (`mat-grid`) in the next component; the twelve columns are for a layout you have decided.",
            ]),

        new("stack-row-grid", "Stack, row and auto grid", "Layout", "layers",
            "The three small layouts that most pages are built from: things under each other, things beside each other, cards that fill the row.",
            ["SPACE-01", "MOBILE-01"],
            [
                new("Stack", """
                    <div class="mat-cols">
                      <div class="mat-col-12 mat-col-md-4"><div class="mat-stack mat-stack--sm"><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-stack--sm</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">8 px</div></div></div>
                      <div class="mat-col-12 mat-col-md-4"><div class="mat-stack"><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-stack</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">16 px</div></div></div>
                      <div class="mat-col-12 mat-col-md-4"><div class="mat-stack mat-stack--lg"><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-stack--lg</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">24 px</div></div></div>
                    </div>
                    """, "A stack puts its children under each other with one gap: for the parts of a card, the fields of a form, the sections of a page."),
                new("Row", """
                    <div class="mat-stack">
                      <div class="mat-row"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-row</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">wraps</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">when</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">the</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">line</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">is</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">full</span></div>
                      <div class="mat-row mat-row--between"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-row--between</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">spread to the edges</span></div>
                      <div class="mat-row mat-row--nowrap"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-text-truncate">mat-row--nowrap never wraps: what does not fit is cut off with an ellipsis</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">fixed</span></div>
                    </div>
                    """, "A row puts its children beside each other, centred on the middle line, and wraps them to the next line when there is no room."),
                new("Auto grid", """
                    <div class="mat-stack">
                      <div class="mat-grid"><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-grid</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">as many</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">columns of at least 260 px</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">as fit</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">the row</div></div>
                      <div class="mat-grid mat-grid--3"><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-grid--3</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">three columns</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">two below 900 px</div><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">one below 600 px</div></div>
                    </div>
                    """, "`mat-grid` fills the row with as many columns as fit; `--2`, `--3` and `--4` fix the number on a wide screen and reduce it on a narrower one."),
            ],
            [
                "These three need no breakpoint: they adapt by themselves, so prefer them to the twelve columns when the layout is just a list of equal things.",
                "`mat-row` and `mat-stack` take the gap of the page; a different gap is a utility (`mat-gap-2`).",
            ]),

        new("u-spacing", "Spacing", "Utilities", "sliders",
            "Margin, padding and gap on the 4 px scale: ten steps from 0 to 48 px. A class is a property, a side and a step.",
            ["SPACE-01", "MOBILE-04"],
            [
                new("The scale", $"""
                    <div class="mat-table-wrap">
                      <table class="mat-table mat-table--compact">
                        <caption class="mat-sr-only">The steps of the spacing scale</caption>
                        <thead><tr><th scope="col">Step</th><th scope="col" class="mat-num">Size</th><th scope="col" class="mat-hide-sm">Token</th></tr></thead>
                        <tbody>
                    {SpaceRows("p")}
                        </tbody>
                      </table>
                    </div>
                    """, "`mat-m-4` is a margin on all sides, `mat-mt-4` on top, `mat-mb-` below, `mat-ms-` at the start and `mat-me-` at the end (so it works from right to left too), `mat-mx-` left and right, `mat-my-` top and bottom. The same with `mat-p-` for padding, and `mat-gap-`, `mat-gap-x-` and `mat-gap-y-`. Margins also take `auto`."),
                new("Padding and margin on a box", """
                    <div class="mat-row mat-items-start">
                      <div class="mat-tint-info mat-rounded"><div class="mat-p-1 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">p-1</div></div>
                      <div class="mat-tint-info mat-rounded mat-p-4"><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">p-4</div></div>
                      <div class="mat-tint-info mat-rounded mat-px-8 mat-py-2"><div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">px-8 py-2</div></div>
                      <div class="mat-tint-warning mat-rounded"><div class="mat-m-5 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">m-5</div></div>
                    </div>
                    """, "The tinted area is the box; the accent cell sits inside it, so the padding is the space between them."),
                new("Gap and auto margins", """
                    <div class="mat-stack">
                      <div class="mat-row mat-gap-1"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">gap-1</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">gap-1</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">gap-1</span></div>
                      <div class="mat-row mat-gap-6"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">gap-6</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">gap-6</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">gap-6</span></div>
                      <div class="mat-row mat-row--nowrap"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">Left</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-ms-auto">mat-ms-auto: pushed to the end</span></div>
                      <div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-w-50 mat-mx-auto">mat-mx-auto mat-w-50: centred</div>
                    </div>
                    """),
            ],
            [
                "Everything that is not on the scale is a reason to ask why: a 13 px gap makes a page look as if it had been put together by two people (SPACE-01).",
                "A utility sets one property on one element. For the space between the parts of a page prefer `mat-stack` and its `--sm` and `--lg`: one rule for the whole list, no class on every child.",
            ]),

        new("u-display", "Display and visibility", "Utilities", "eye",
            "Show or hide something by the width of the screen, change how an element lays out its children, or hide it from the eye but not from a screen reader.",
            ["MOBILE-01", "A11Y-03"],
            [
                new("By width", """
                    <div class="mat-stack">
                      <div class="mat-tint-info mat-rounded mat-p-3 mat-d-md-none">Shown below 900 px only: <code>mat-d-md-none</code></div>
                      <div class="mat-tint-success mat-rounded mat-p-3 mat-d-none mat-d-md-block">Shown from 900 px: <code>mat-d-none mat-d-md-block</code></div>
                      <div class="mat-tint-accent mat-rounded mat-p-3">Always shown</div>
                    </div>
                    """, "Resize the window: the first box is only there on a phone, the second only on a wide screen. Write the narrow case first and the wide case with a breakpoint."),
                new("Display values", """
                    <div class="mat-stack">
                      <div><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-d-inline">mat-d-inline</span> <span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-d-inline">next to the text</span></div>
                      <div><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-d-block">mat-d-block: its own line</span></div>
                      <div class="mat-d-flex mat-gap-2"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-d-flex</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">children in a row</span></div>
                      <div class="mat-d-grid mat-gap-2"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-d-grid</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">children in a column</span></div>
                    </div>
                    """, "The values are `none`, `block`, `inline`, `inline-block`, `flex`, `inline-flex` and `grid`, each also with `sm`, `md` and `lg`."),
                new("Invisible, and only for a screen reader", """
                    <div class="mat-stack">
                      <div class="mat-row mat-row--nowrap"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">before</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-invisible">mat-invisible keeps its room</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">after</span></div>
                      <div class="mat-row"><button type="button" class="mat-btn mat-btn--secondary">Delete<span class="mat-sr-only"> the invoice of March</span></button><span class="mat-muted">The button says more to a screen reader.</span></div>
                    </div>
                    """, "`mat-invisible` hides without giving up the space (and is also gone for a screen reader). `mat-sr-only` is the other way round: it can be heard but not seen. The `hidden` attribute removes the element for everyone."),
            ],
            [
                "A control that is only hidden by width is still on the page: it must not be the only way to do something on the other width (MOBILE-01).",
                "Do not hide an error, a required field or a way to get out of a dialog with a width class.",
            ]),

        new("u-flex", "Flex and alignment", "Utilities", "template",
            "Direction, distribution and alignment of the children of a flexible box, and how they grow.",
            ["MOBILE-01", "SPACE-01"],
            [
                new("Distribution", """
                    <div class="mat-stack">
                      <div class="mat-d-flex mat-justify-start mat-gap-2"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">start</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-justify-start</span></div>
                      <div class="mat-d-flex mat-justify-center mat-gap-2"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">center</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-justify-center</span></div>
                      <div class="mat-d-flex mat-justify-end mat-gap-2"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">end</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-justify-end</span></div>
                      <div class="mat-d-flex mat-justify-between"><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">between</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-justify-between</span><span class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">edges</span></div>
                    </div>
                    """, "`mat-justify-*` distributes the children along the main axis: `start`, `center`, `end`, `between`, `around` and `evenly`."),
                new("Alignment across", """
                    <div class="mat-d-flex mat-gap-3 mat-items-center">
                      <div class="mat-tint-accent mat-rounded-sm mat-p-8 mat-text-center">tall</div>
                      <div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-items-center</div>
                      <div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-self-end">mat-self-end</div>
                    </div>
                    """, "`mat-items-*` (`start`, `center`, `end`, `baseline`, `stretch`) aligns all children, `mat-self-*` one of them."),
                new("Growing, and changing direction", """
                    <div class="mat-d-flex mat-flex-column mat-flex-md-row mat-gap-3">
                      <div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-flex-grow">mat-flex-grow takes the room that is left</div>
                      <div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-flex-shrink-0">mat-flex-shrink-0</div>
                      <div class="mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">fixed</div>
                    </div>
                    """, "A column on a phone and a row from 900 px: `mat-flex-column mat-flex-md-row`. `mat-flex-fill` is `flex: 1 1 auto`; `mat-flex-wrap` lets the children go to the next line."),
            ],
            [
                "A flex utility needs the box to be flex: add `mat-d-flex` (or use `mat-row` or `mat-stack`, which are already).",
                "`mat-order-*` changes only the picture: the Tab key and a screen reader keep the order of the markup.",
            ]),

        new("u-text", "Text", "Utilities", "type",
            "Alignment, wrapping and cutting, size, weight and style of a piece of text, and the colours of text that can be read on a surface.",
            ["TYPE-02", "TYPE-04", "COLOR-05"],
            [
                new("Alignment, size and weight", """
                    <div class="mat-stack">
                      <p class="mat-text-start">mat-text-start (follows the direction of the text)</p>
                      <p class="mat-text-center">mat-text-center</p>
                      <p class="mat-text-end">mat-text-end</p>
                      <div class="mat-row mat-items-baseline">
                        <span class="mat-fs-xs">fs-xs</span><span class="mat-fs-sm">fs-sm</span><span class="mat-fs-md">fs-md</span><span class="mat-fs-lg">fs-lg</span><span class="mat-fs-xl">fs-xl</span><span class="mat-fs-2xl">fs-2xl</span>
                      </div>
                      <div class="mat-row">
                        <span class="mat-fw-normal">fw-normal</span><span class="mat-fw-medium">fw-medium</span><span class="mat-fw-strong">fw-strong</span>
                        <span class="mat-italic">italic</span><span class="mat-underline">underline</span><span class="mat-line-through">line-through</span><span class="mat-text-upper">upper</span><span class="mat-text-capitalize">capitalize words</span>
                      </div>
                    </div>
                    """, "The sizes are the steps of the type scale (TYPE-02). Use a heading for a title, not `mat-fs-xl` on a paragraph: a screen reader finds headings."),
                new("Wrapping and cutting", """
                    <div class="mat-stack">
                      <div class="mat-w-50"><p class="mat-text-truncate mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-text-truncate cuts a long single line with an ellipsis instead of breaking it</p></div>
                      <div class="mat-w-50"><p class="mat-text-break mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center mat-mono">mat-text-break: https://example.com/a/very/long/address/without/any/space/that/would/push/the/box/wider</p></div>
                      <div class="mat-w-50"><p class="mat-text-nowrap mat-overflow-x-auto mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-text-nowrap: this line never wraps, so the box scrolls sideways when it must</p></div>
                    </div>
                    """, "A cut text keeps its full text: give the element a `title` or put the whole text in a tooltip, and never cut what the user must read (an error, a price)."),
                new("Colour of text", """
                    <div class="mat-stack mat-stack--sm">
                      <span class="mat-text-default">mat-text-default: the text colour</span>
                      <span class="mat-text-muted">mat-text-muted: a note, a second line</span>
                      <span class="mat-text-accent">mat-text-accent: the accent as text</span>
                      <span class="mat-text-success">mat-text-success: it worked</span>
                      <span class="mat-text-warning">mat-text-warning: have a look</span>
                      <span class="mat-text-danger">mat-text-danger: it failed</span>
                      <span class="mat-text-info">mat-text-info: for your information</span>
                    </div>
                    """, "These are the colours that reach a contrast of 4.5 to 1 on a surface in light and dark (COLOR-05). The colour supports the words; it never replaces them (COLOR-04)."),
            ],
            [
                "There is no class for the faint text colour on purpose: it is for a placeholder or a disabled control, not for text that has to be read.",
                "A line of more than about 70 characters is hard to read; limit a block of text with `mat-prose` or a width, not with a smaller font (TYPE-04).",
            ]),

        new("u-colour", "Backgrounds and tints", "Utilities", "palette",
            "The surfaces of the toolkit as backgrounds, and five tints that bring the text colour that is readable on them.",
            ["COLOR-01", "COLOR-04", "COLOR-05"],
            [
                new("Surfaces", """
                    <div class="mat-grid mat-grid--4">
                      <div class="mat-bg-page mat-border mat-rounded mat-p-4">mat-bg-page</div>
                      <div class="mat-bg-surface mat-border mat-rounded mat-p-4">mat-bg-surface</div>
                      <div class="mat-bg-surface-2 mat-border mat-rounded mat-p-4">mat-bg-surface-2</div>
                      <div class="mat-bg-surface-3 mat-border mat-rounded mat-p-4">mat-bg-surface-3</div>
                    </div>
                    """, "They follow light and dark by themselves. A card is a surface with a border and a radius; use `mat-card` when it is a card."),
                new("Tints", """
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-tint-accent mat-rounded mat-p-3"><strong>Selected.</strong> mat-tint-accent</div>
                      <div class="mat-tint-success mat-rounded mat-p-3"><strong>Saved.</strong> mat-tint-success</div>
                      <div class="mat-tint-warning mat-rounded mat-p-3"><strong>Almost full.</strong> mat-tint-warning</div>
                      <div class="mat-tint-danger mat-rounded mat-p-3"><strong>Failed.</strong> mat-tint-danger</div>
                      <div class="mat-tint-info mat-rounded mat-p-3"><strong>Note.</strong> mat-tint-info</div>
                    </div>
                    """, "A tint sets the background and the text colour together, so the text stays readable. The word says what the colour means (COLOR-04); for a message that stays on the page use an alert."),
            ],
            [
                "The status colours keep one meaning in every app: green is done, amber is have a look, red failed, blue is information (COLOR-03). Do not use a tint for decoration.",
                "A colour outside the tokens is not available as a class on purpose (COLOR-01).",
            ]),

        new("u-borders", "Borders, radius and shadow", "Utilities", "box",
            "A line, rounded corners and a shadow for an element that is not a component of its own.",
            ["SPACE-02", "SPACE-03"],
            [
                new("Borders", """
                    <div class="mat-grid mat-grid--3">
                      <div class="mat-border mat-rounded mat-p-3">mat-border</div>
                      <div class="mat-border-top mat-p-3">mat-border-top</div>
                      <div class="mat-border-bottom mat-p-3">mat-border-bottom</div>
                      <div class="mat-border-start mat-p-3">mat-border-start</div>
                      <div class="mat-border-end mat-p-3">mat-border-end</div>
                      <div class="mat-border mat-border-0 mat-tint-info mat-rounded mat-p-3">mat-border-0 removes it</div>
                    </div>
                    """),
                new("Radius", """
                    <div class="mat-row mat-items-center">
                      <div class="mat-tint-accent mat-rounded-0 mat-p-3">rounded-0</div>
                      <div class="mat-tint-accent mat-rounded-sm mat-p-3">rounded-sm</div>
                      <div class="mat-tint-accent mat-rounded mat-p-3">rounded</div>
                      <div class="mat-tint-accent mat-rounded-lg mat-p-3">rounded-lg</div>
                      <div class="mat-tint-accent mat-rounded-xl mat-p-3">rounded-xl</div>
                      <div class="mat-tint-accent mat-rounded-pill mat-px-5 mat-py-2">rounded-pill</div>
                      <img class="mat-rounded-circle" src="/favicon.svg" alt="" width="64" height="64" />
                    </div>
                    """, "Inputs and small buttons use 8 px, buttons and menu items 12, cards and dialogs 18 (SPACE-02)."),
                new("Shadow", """
                    <div class="mat-grid mat-grid--4">
                      <div class="mat-bg-surface mat-border mat-rounded mat-p-4 mat-shadow-0">shadow-0</div>
                      <div class="mat-bg-surface mat-rounded mat-p-4 mat-shadow-1">shadow-1: at rest</div>
                      <div class="mat-bg-surface mat-rounded mat-p-4 mat-shadow-2">shadow-2: a menu</div>
                      <div class="mat-bg-surface mat-rounded mat-p-4 mat-shadow-3">shadow-3: a dialog</div>
                    </div>
                    """, "The higher the shadow, the further the element is above the page (SPACE-03)."),
            ],
            [
                "A border is one pixel and uses the border colour of the theme, so it is visible in light and dark. A thicker line or another colour is a component.",
                "`mat-border-start` and `mat-border-end` follow the direction of the text.",
            ]),

        new("u-sizing", "Sizing, position and overflow", "Utilities", "sidebar",
            "Widths as a part of the parent, the width of the content, what happens to content that is too big, and an element that stays in view.",
            ["SPACE-01", "MOBILE-01"],
            [
                new("Widths", """
                    <div class="mat-stack mat-stack--sm">
                      <div class="mat-w-25 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-w-25</div>
                      <div class="mat-w-50 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-w-50</div>
                      <div class="mat-w-75 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-w-75</div>
                      <div class="mat-w-100 mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-w-100</div>
                      <div class="mat-w-fit mat-tint-accent mat-rounded-sm mat-p-3 mat-text-center">mat-w-fit: as wide as its content</div>
                    </div>
                    """, "`mat-w-auto`, `mat-h-100`, `mat-h-auto` and `mat-mw-100` (never wider than the parent: for an image or a table) are there too. `mat-min-w-0` lets a child of a flex box or a grid become narrower than its content."),
                new("Overflow and position", """
                    <div class="mat-stack">
                      <div class="mat-overflow-x-auto mat-border mat-rounded mat-p-3"><pre class="mat-code"><code>a line that is longer than the box so that the box scrolls sideways instead of making the page wider than a phone</code></pre></div>
                      <div class="mat-relative mat-border mat-rounded mat-p-5">mat-relative<span class="mat-absolute mat-top-0 mat-end-0 mat-m-2"><span class="mat-badge mat-badge--error">3<span class="mat-sr-only"> new</span></span></span></div>
                    </div>
                    """, "`mat-overflow-hidden`, `-auto`, `-visible` and `-x-auto` decide what happens to content that is too big. `mat-absolute` places an element in the nearest `mat-relative` parent."),
                new("Staying in view", """
                    <aside class="mat-sticky-top mat-bg-surface mat-border mat-rounded mat-p-4">The index, a summary of the order, the filter of a long list</aside>
                    """, "`mat-sticky-top` keeps an element in view below the top bar while the page scrolls past it, as the index of this page does. It needs room in its parent: put it in a column that is as tall as the content beside it.", Live: false),
            ],
            [
                "No page is wider than the phone: a wide table, a code block or a long address scrolls inside its own box (`mat-overflow-x-auto`), never the page (MOBILE-01).",
                "Use `mat-fixed` rarely: a fixed element covers what is behind it on a small screen, and the keyboard of a phone pushes it around.",
            ]),
    ];
}
