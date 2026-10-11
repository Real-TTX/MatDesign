#!/usr/bin/env node
// Builds the icon library into src/MatUi/wwwroot/iconlib/: the sprite, the catalogue, the licence and a short README.
//   node tools/iconlib.js
// The library is optional and separate from the toolkit: MatUi (matui.css, matui.js, matui-icons.svg) works without it, and an app takes the library files only
// when it needs more icons than the small sprite has. It is not part of the CSS bundle.
//
// Sources, all in tools/iconlib/ (committed, so the build needs no network):
//   lucide.json       the Lucide icons (import-lucide.js writes it: shapes, tags, Lucide's own categories, old names as search terms)
//   custom.json       icons drawn for MatDesign in the same style where Lucide has none (radiator, curtains, motion sensor ...); each names its categories
//   categories.json   the categories MatDesign adds (General, Home automation and its areas) with their curated lists of icons; an icon can be in any number of categories
// An icon is in the categories Lucide gave it, plus those of categories.json that list it. `includes` makes a category the union of others (Home automation).
// Output ids are `mat-il-<name>` (the sprite of the toolkit uses `mat-i-<name>`), so that both can be on one page.
const fs = require('fs');
const path = require('path');

const here = path.join(__dirname, 'iconlib');
const target = path.join(__dirname, '..', 'src', 'MatUi', 'wwwroot', 'iconlib');
const read = (file) => fs.readFileSync(path.join(here, file), 'utf8');
const lucide = JSON.parse(read('lucide.json'));
const custom = JSON.parse(read('custom.json'));
const defs = JSON.parse(read('categories.json'));

const NAME = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const ELEMENTS = new Set(['path', 'circle', 'rect', 'line', 'ellipse', 'polyline', 'polygon']);
const ATTRIBUTES = new Set(['d', 'cx', 'cy', 'r', 'x', 'y', 'width', 'height', 'rx', 'ry', 'x1', 'y1', 'x2', 'y2', 'points', 'fill']);
const problems = [];
const fail = (message) => problems.push(message);

/** Only shapes, only geometry (and `fill='currentColor'` for dots): an icon takes its colour from the text around it. */
function checkBody(name, body) {
  const rest = body.replace(/<(\w+)((?:\s+[\w-]+='[^']*')*)\s*\/>/g, (all, element, attributes) => {
    if (!ELEMENTS.has(element)) fail(`${name}: <${element}> is not a shape`);
    for (const a of attributes.matchAll(/([\w-]+)='([^']*)'/g)) {
      if (!ATTRIBUTES.has(a[1])) fail(`${name}: attribute ${a[1]}`);
      if (a[1] === 'fill' && a[2] !== 'currentColor') fail(`${name}: fill ${a[2]} (only currentColor)`);
    }
    return '';
  });
  if (rest.length > 0) fail(`${name}: markup that is not a self-closing shape: ${rest.slice(0, 40)}`);
}

// ---- icons
const icons = new Map(); // name -> { body, tags: Set, categories: Set, origin }
const oldNames = new Map(); // an old name of a Lucide icon -> its name now
for (const [name, i] of Object.entries(lucide.icons)) {
  icons.set(name, { body: i.body, tags: new Set([...i.tags, ...i.aliases]), categories: new Set(i.categories), origin: 'lucide' });
  for (const alias of i.aliases) oldNames.set(alias, name);
}
const lucideCount = icons.size;
for (const [name, i] of Object.entries(custom)) {
  if (icons.has(name)) fail(`${name}: drawn for MatDesign, but Lucide has an icon of that name`);
  if (!Array.isArray(i.categories) || i.categories.length === 0) fail(`${name}: no category`);
  icons.set(name, { body: i.body, tags: new Set(i.tags || []), categories: new Set(i.categories || []), origin: 'matdesign' });
}
for (const [name, i] of icons) {
  if (!NAME.test(name)) fail(`${name}: not a valid name (lower case, digits and dashes)`);
  checkBody(name, i.body);
}

// ---- categories
const categories = new Map(); // id -> { title, description, icon, group, includes }
for (const [id, c] of Object.entries(defs.categories)) {
  categories.set(id, { title: c.title, description: c.description, icon: c.icon, group: c.group, includes: c.includes || [] });
}
for (const [id, c] of Object.entries(lucide.categories)) {
  if (categories.has(id)) fail(`category ${id}: also defined by Lucide`);
  const own = defs.lucide?.[id] || {};
  categories.set(id, { title: own.title || c.title, description: own.description || c.description, icon: c.icon, group: 'topics', includes: [] });
}
const groupIds = new Set(defs.groups.map(g => g.id));
for (const [id, c] of categories) {
  if (!groupIds.has(c.group)) fail(`category ${id}: unknown group ${c.group}`);
  if (!icons.has(c.icon)) fail(`category ${id}: its icon ${c.icon} is not in the library`);
}

/** An icon of a curated list, by its name now: a clear message for a name that Lucide has renamed. */
function resolve(category, name) {
  if (icons.has(name)) return name;
  fail(oldNames.has(name) ? `category ${category}: ${name} is called ${oldNames.get(name)} now` : `category ${category}: no icon named ${name}`);
  return null;
}

for (const [id, c] of Object.entries(defs.categories)) {
  for (const listed of c.icons || []) {
    const name = resolve(id, listed);
    if (name) icons.get(name).categories.add(id);
  }
}
for (const [name, i] of icons) {
  for (const c of i.categories) if (!categories.has(c)) fail(`${name}: unknown category ${c}`);
}
for (const [id, c] of categories) {
  for (const part of c.includes) {
    if (!categories.has(part)) { fail(`category ${id}: includes unknown ${part}`); continue; }
    for (const i of icons.values()) if (i.categories.has(part)) i.categories.add(id);
  }
}
const counts = new Map([...categories.keys()].map(id => [id, 0]));
for (const [name, i] of icons) {
  if (i.categories.size === 0) fail(`${name}: in no category`);
  for (const c of i.categories) counts.set(c, (counts.get(c) || 0) + 1);
}
for (const [id, n] of counts) if (n === 0) fail(`category ${id}: no icons`);

if (problems.length > 0) {
  console.error(problems.slice(0, 60).join('\n') + (problems.length > 60 ? `\n... and ${problems.length - 60} more` : ''));
  process.exit(1);
}

// ---- files
const names = [...icons.keys()].sort();
const order = (id) => {
  const c = categories.get(id);
  const g = defs.groups.findIndex(x => x.id === c.group);
  const own = Object.keys(defs.categories).indexOf(id);
  return [g, own >= 0 ? own : 1e6, c.title.toLowerCase()];
};
const categoryIds = [...categories.keys()].sort((a, b) => {
  const x = order(a), y = order(b);
  return x[0] - y[0] || x[1] - y[1] || x[2].localeCompare(y[2]);
});
const rank = new Map(categoryIds.map((id, n) => [id, n]));

const sprite = '<svg xmlns="http://www.w3.org/2000/svg" class="mat-sprite" width="0" height="0" aria-hidden="true" focusable="false">\n' +
  names.map(n => `  <symbol id="mat-il-${n}" viewBox="0 0 24 24">${icons.get(n).body}</symbol>`).join('\n') + '\n</svg>\n';

const items = names.map(n => {
  const i = icons.get(n);
  return '  ' + JSON.stringify({ name: n, categories: [...i.categories].sort((a, b) => rank.get(a) - rank.get(b)), tags: [...i.tags].sort(), origin: i.origin });
});
const catalogue = `{\n "name": "MatUi icon library",\n "icons": ${names.length},\n` +
  ` "source": ${JSON.stringify({ lucide: { ...lucide.source, icons: lucideCount }, matdesign: { icons: names.length - lucideCount } })},\n` +
  ` "groups": ${JSON.stringify(defs.groups)},\n` +
  ` "categories": [\n${categoryIds.map(id => '  ' + JSON.stringify({ id, ...pick(categories.get(id)), count: counts.get(id) })).join(',\n')}\n ],\n` +
  ` "items": [\n${items.join(',\n')}\n ]\n}\n`;

function pick(c) { return { title: c.title, description: c.description, group: c.group, icon: c.icon }; }

const licence = `MatUi icon library - where the icons come from

${lucideCount} icons are from Lucide (https://lucide.dev, ${lucide.source.repository}, commit ${lucide.source.commit}). Their licence, and the one of the icons that Lucide
took over from Feather, follow below; the notice has to stay with the icons when they are copied.

${names.length - lucideCount} icons were drawn for MatDesign in the style of Lucide (24 px grid, 2 px stroke, round caps) where Lucide has no icon for a smart home (radiator,
curtains, motion sensor ...). In iconlib.json they have "origin": "matdesign". They belong to MatDesign.

----------------------------------------------------------------------------------------------------------------------------------------------

${fs.readFileSync(path.join(here, 'LICENSE-lucide.txt'), 'utf8').trimEnd()}
`;

const readme = `MatUi icon library (optional)

Icons for all kinds of things, sorted into categories (an icon can be in several), for apps that need more than the icons of matui-icons.svg.
The toolkit works without this folder; nothing in matui.css or matui.js depends on it.

  matui-iconlib.svg   one <symbol id="mat-il-NAME"> per icon (${names.length} icons), 24 px grid, 2 px stroke, round caps, currentColor
  iconlib.json        the catalogue: categories (id, title, description, count) and every icon with its categories, tags and origin
  LICENSE.txt         where the icons come from and under which licence (keep it with the icons)

Use one icon: put the sprite inline once per page and refer to the icon, exactly like the toolkit sprite:

  <svg class="mat-icon" aria-hidden="true" focusable="false"><use href="#mat-il-lamp"></use></svg>

or refer to the file (same origin only):

  <svg class="mat-icon" aria-hidden="true" focusable="false"><use href="/iconlib/matui-iconlib.svg#mat-il-lamp"></use></svg>

or copy only the icons you need into your own sprite. The site's icon library page (Foundations > Icon library) shows every icon, copies the markup of one and
downloads each as a single .svg file. Rules ICON-01 to ICON-03 apply to these icons as well: an icon without a visible label needs an aria-label.
Rebuild: node tools/iconlib.js (needs no network).
`;

fs.mkdirSync(target, { recursive: true });
fs.writeFileSync(path.join(target, 'matui-iconlib.svg'), sprite);
fs.writeFileSync(path.join(target, 'iconlib.json'), catalogue);
fs.writeFileSync(path.join(target, 'LICENSE.txt'), licence);
fs.writeFileSync(path.join(target, 'README.txt'), readme);

const multi = [...icons.values()].filter(i => i.categories.size > 1).length;
console.log(`${names.length} icons (${lucideCount} Lucide, ${names.length - lucideCount} drawn for MatDesign) in ${categories.size} categories; ${multi} are in more than one -> src/MatUi/wwwroot/iconlib/`);
console.log(categoryIds.filter(id => categories.get(id).group !== 'topics').map(id => `  ${id}: ${counts.get(id)}`).join('\n'));
