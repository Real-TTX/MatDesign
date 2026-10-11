#!/usr/bin/env node
// Imports the Lucide icon set (ISC licence) into tools/iconlib/lucide.json, the vendored source of the icon library.
//   git clone --depth 1 --filter=blob:none --sparse https://github.com/lucide-icons/lucide.git /tmp/lucide
//   (cd /tmp/lucide && git sparse-checkout set icons categories)
//   node tools/iconlib/import-lucide.js /tmp/lucide
// It is run when Lucide is updated, not on every build: the result is committed, so `node tools/iconlib.js` needs no network and no checkout.
// What is taken: the shapes, the tags, the categories and the old names (aliases, as search terms). Deprecated icons are left out. The text of the licence is copied
// next to the data, because the notice has to travel with the icons.
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const root = process.argv[2];
if (!root || !fs.existsSync(path.join(root, 'icons'))) {
  console.error('usage: node tools/iconlib/import-lucide.js <a checkout of lucide-icons/lucide with icons/ and categories/>');
  process.exit(1);
}

const ELEMENTS = new Set(['path', 'circle', 'rect', 'line', 'ellipse', 'polyline', 'polygon']);
const ATTRIBUTES = new Set(['d', 'cx', 'cy', 'r', 'x', 'y', 'width', 'height', 'rx', 'ry', 'x1', 'y1', 'x2', 'y2', 'points', 'fill']);

/** The shapes of an icon in the form of the toolkit sprite: single-quoted attributes, no whitespace between the elements. */
function body(name, svg) {
  const inner = svg.replace(/^[\s\S]*?>\s*/, '').replace(/<\/svg>\s*$/, '');
  const parts = [];
  for (const m of inner.matchAll(/<(\w+)([^>]*?)\s*\/?>/g)) {
    if (!ELEMENTS.has(m[1])) throw new Error(`${name}: unexpected element <${m[1]}>`);
    const attrs = [];
    for (const a of m[2].matchAll(/([\w:-]+)="([^"]*)"/g)) {
      if (!ATTRIBUTES.has(a[1])) throw new Error(`${name}: unexpected attribute ${a[1]}`);
      if (a[1] === 'fill' && a[2] !== 'currentColor') throw new Error(`${name}: fill ${a[2]}`);
      if (a[2].includes("'")) throw new Error(`${name}: quote in ${a[1]}`);
      attrs.push(`${a[1]}='${a[2]}'`);
    }
    parts.push(`<${m[1]} ${attrs.join(' ')}/>`);
  }
  if (parts.length === 0) throw new Error(`${name}: no shapes`);
  return parts.join('');
}

const read = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));

const categories = {};
for (const file of fs.readdirSync(path.join(root, 'categories')).filter(f => f.endsWith('.json')).sort()) {
  const c = read(path.join(root, 'categories', file));
  categories[file.replace(/\.json$/, '')] = { title: c.title, description: c.description, icon: c.icon };
}

const icons = {};
let skipped = 0;
for (const file of fs.readdirSync(path.join(root, 'icons')).filter(f => f.endsWith('.json')).sort()) {
  const name = file.replace(/\.json$/, '');
  const meta = read(path.join(root, 'icons', file));
  if (meta.deprecated) { skipped++; continue; }
  for (const c of meta.categories) if (!categories[c]) throw new Error(`${name}: unknown category ${c}`);
  icons[name] = {
    body: body(name, fs.readFileSync(path.join(root, 'icons', name + '.svg'), 'utf8')),
    tags: meta.tags,
    categories: meta.categories,
    aliases: (meta.aliases || []).map(a => (typeof a === 'string' ? a : a.name)),
  };
}

let commit = 'unknown';
try { commit = execFileSync('git', ['-C', root, 'rev-parse', '--short', 'HEAD'], { encoding: 'utf8' }).trim(); } catch { /* not a git checkout */ }

// One icon per line: a new import shows in a diff as the icons that changed.
const lines = Object.entries(icons).map(([name, i]) => `  ${JSON.stringify(name)}: ${JSON.stringify(i)}`);
const catLines = Object.entries(categories).map(([id, c]) => `  ${JSON.stringify(id)}: ${JSON.stringify(c)}`);
const out = `{\n "source": ${JSON.stringify({ name: 'Lucide', url: 'https://lucide.dev', repository: 'https://github.com/lucide-icons/lucide', commit, license: 'ISC (parts MIT, from Feather)' })},\n` +
  ` "categories": {\n${catLines.join(',\n')}\n },\n "icons": {\n${lines.join(',\n')}\n }\n}\n`;
const here = __dirname;
fs.writeFileSync(path.join(here, 'lucide.json'), out);
fs.writeFileSync(path.join(here, 'LICENSE-lucide.txt'), fs.readFileSync(path.join(root, 'LICENSE'), 'utf8').replace(/\r\n/g, '\n'));
console.log(`${Object.keys(icons).length} icons, ${Object.keys(categories).length} categories (${skipped} deprecated left out), Lucide ${commit} -> tools/iconlib/lucide.json`);
