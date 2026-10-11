MatUi icon library (optional)

Icons for all kinds of things, sorted into categories (an icon can be in several), for apps that need more than the icons of matui-icons.svg.
The toolkit works without this folder; nothing in matui.css or matui.js depends on it.

  matui-iconlib.svg   one <symbol id="mat-il-NAME"> per icon (1925 icons), 24 px grid, 2 px stroke, round caps, currentColor
  iconlib.json        the catalogue: categories (id, title, description, count) and every icon with its categories, tags and origin
  LICENSE.txt         where the icons come from and under which licence (keep it with the icons)

Use one icon: put the sprite inline once per page and refer to the icon, exactly like the toolkit sprite:

  <svg class="mat-icon" aria-hidden="true" focusable="false"><use href="#mat-il-lamp"></use></svg>

or refer to the file (same origin only):

  <svg class="mat-icon" aria-hidden="true" focusable="false"><use href="/iconlib/matui-iconlib.svg#mat-il-lamp"></use></svg>

or copy only the icons you need into your own sprite. The site's icon library page (Foundations > Icon library) shows every icon, copies the markup of one and
downloads each as a single .svg file. Rules ICON-01 to ICON-03 apply to these icons as well: an icon without a visible label needs an aria-label.
Rebuild: node tools/iconlib.js (needs no network).
