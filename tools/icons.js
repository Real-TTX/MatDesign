#!/usr/bin/env node
// Builds src/MatUi/wwwroot/img/matui-icons.svg: one <symbol id="mat-i-<name>"> per icon.
//   node tools/icons.js
// Icons are 24x24 stroke icons (Feather / Lucide style): `fill: none; stroke: currentColor` come from the .mat-icon class, so an icon
// follows the text colour and the theme. Add an icon here, run the script, commit both files. No icon font, no CDN, no emoji.
const fs = require('fs');
const path = require('path');

const base = JSON.parse(fs.readFileSync(path.join(__dirname, 'icons.base.json'), 'utf8'));

const extra = {
  // list and toolbar
  'sort': "<path d='M11 5h10'/><path d='M11 9h7'/><path d='M11 13h4'/><path d='m3 17 3 3 3-3'/><path d='M6 18V4'/>",
  'sliders': "<line x1='4' y1='21' x2='4' y2='14'/><line x1='4' y1='10' x2='4' y2='3'/><line x1='12' y1='21' x2='12' y2='12'/><line x1='12' y1='8' x2='12' y2='3'/><line x1='20' y1='21' x2='20' y2='16'/><line x1='20' y1='12' x2='20' y2='3'/><line x1='1' y1='14' x2='7' y2='14'/><line x1='9' y1='8' x2='15' y2='8'/><line x1='17' y1='16' x2='23' y2='16'/>",
  'list': "<line x1='8' y1='6' x2='21' y2='6'/><line x1='8' y1='12' x2='21' y2='12'/><line x1='8' y1='18' x2='21' y2='18'/><line x1='3' y1='6' x2='3.01' y2='6'/><line x1='3' y1='12' x2='3.01' y2='12'/><line x1='3' y1='18' x2='3.01' y2='18'/>",
  'grid': "<rect x='3' y='3' width='7' height='7'/><rect x='14' y='3' width='7' height='7'/><rect x='14' y='14' width='7' height='7'/><rect x='3' y='14' width='7' height='7'/>",
  'more-horizontal': "<circle cx='12' cy='12' r='1'/><circle cx='19' cy='12' r='1'/><circle cx='5' cy='12' r='1'/>",
  'more-vertical': "<circle cx='12' cy='12' r='1'/><circle cx='12' cy='5' r='1'/><circle cx='12' cy='19' r='1'/>",
  'chevron-down': "<polyline points='6 9 12 15 18 9'/>",
  'chevron-up': "<polyline points='18 15 12 9 6 15'/>",
  'arrow-down': "<line x1='12' y1='5' x2='12' y2='19'/><polyline points='19 12 12 19 5 12'/>",
  'arrow-right': "<line x1='5' y1='12' x2='19' y2='12'/><polyline points='12 5 19 12 12 19'/>",
  'minus': "<line x1='5' y1='12' x2='19' y2='12'/>",
  'eye-off': "<path d='M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24'/><line x1='1' y1='1' x2='23' y2='23'/>",
  'help-circle': "<circle cx='12' cy='12' r='10'/><path d='M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3'/><line x1='12' y1='17' x2='12.01' y2='17'/>",
  'bell': "<path d='M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9'/><path d='M13.73 21a2 2 0 0 1-3.46 0'/>",
  'tag': "<path d='M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z'/><line x1='7' y1='7' x2='7.01' y2='7'/>",
  'star': "<polygon points='12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2'/>",
  'user-circle': "<circle cx='12' cy='12' r='10'/><circle cx='12' cy='10' r='3'/><path d='M7 20.66V19a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v1.66'/>",
  'check-square': "<polyline points='9 11 12 14 22 4'/><path d='M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11'/>",
  // docs and areas of the MatDesign site
  'book': "<path d='M4 19.5A2.5 2.5 0 0 1 6.5 17H20'/><path d='M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z'/>",
  'code': "<polyline points='16 18 22 12 16 6'/><polyline points='8 6 2 12 8 18'/>",
  'type': "<polyline points='4 7 4 4 20 4 20 7'/><line x1='9' y1='20' x2='15' y2='20'/><line x1='12' y1='4' x2='12' y2='20'/>",
  'palette': "<circle cx='13.5' cy='6.5' r='.5' fill='currentColor'/><circle cx='17.5' cy='10.5' r='.5' fill='currentColor'/><circle cx='8.5' cy='7.5' r='.5' fill='currentColor'/><circle cx='6.5' cy='12.5' r='.5' fill='currentColor'/><path d='M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z'/>",
  'sidebar': "<rect x='3' y='3' width='18' height='18' rx='2'/><line x1='9' y1='3' x2='9' y2='21'/>",
  'message-square': "<path d='M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z'/>",
  'toggle': "<rect x='1' y='5' width='22' height='14' rx='7'/><circle cx='16' cy='12' r='3'/>",
  'table': "<path d='M9 3H5a2 2 0 0 0-2 2v4m6-6h10a2 2 0 0 1 2 2v4M9 3v18m0 0h10a2 2 0 0 0 2-2V9M9 21H5a2 2 0 0 1-2-2V9m0 0h18'/>",
  'wrench': "<path d='M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z'/>",
  'smartphone': "<rect x='5' y='2' width='14' height='20' rx='2' ry='2'/><line x1='12' y1='18' x2='12.01' y2='18'/>",
  'accessibility': "<circle cx='12' cy='4.5' r='1.5'/><path d='M5 8l7 1.5L19 8'/><path d='M12 9.5V14'/><path d='M9 21l3-7 3 7'/>",
  'mail': "<path d='M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z'/><polyline points='22,6 12,13 2,6'/>",
  'mail-m': "<rect x='2.5' y='4.5' width='19' height='15' rx='3'/><path d='M6.5 16V9.2l5.5 4.6 5.5-4.6V16'/>",
  // glyphs of the app logos (one per app, see the registry)
  'briefcase': "<rect x='2' y='7' width='20' height='14' rx='2' ry='2'/><path d='M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16'/>",
  'bar-chart': "<line x1='12' y1='20' x2='12' y2='10'/><line x1='18' y1='20' x2='18' y2='4'/><line x1='6' y1='20' x2='6' y2='16'/>",
  'archive': "<polyline points='21 8 21 21 3 21 3 8'/><rect x='1' y='3' width='22' height='5'/><line x1='10' y1='12' x2='14' y2='12'/>",
  'hard-drive': "<line x1='22' y1='12' x2='2' y2='12'/><path d='M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z'/><line x1='6' y1='16' x2='6.01' y2='16'/><line x1='10' y1='16' x2='10.01' y2='16'/>",
  'tv': "<rect x='2' y='7' width='20' height='15' rx='2' ry='2'/><polyline points='17 2 12 7 7 2'/>",
  'paperclip': "<path d='m16 6-8.414 8.586a2 2 0 0 0 2.829 2.829l8.414-8.586a4 4 0 1 0-5.657-5.657l-8.379 8.551a6 6 0 1 0 8.485 8.485l8.379-8.551'/>",
  'progress-play': "<circle cx='12' cy='12' r='9.4' stroke-opacity='.3' stroke-width='3.2'/><path d='M12 2.6A9.4 9.4 0 1 1 3.06 14.9' stroke-width='3.2'/><polygon points='9.12 7.29 9.12 16.71 16.98 12' fill='currentColor' stroke-width='1' stroke-linejoin='round'/>",
  'dollar': "<line x1='12' y1='1' x2='12' y2='23'/><path d='M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'/>",
  'gamepad': "<line x1='6' y1='12' x2='10' y2='12'/><line x1='8' y1='10' x2='8' y2='14'/><line x1='15' y1='13' x2='15.01' y2='13'/><line x1='18' y1='11' x2='18.01' y2='11'/><rect x='2' y='6' width='20' height='12' rx='2'/>",
  'file-text': "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><polyline points='14 2 14 8 20 8'/><line x1='16' y1='13' x2='8' y2='13'/><line x1='16' y1='17' x2='8' y2='17'/>",
  'list-checks': "<path d='M3 7.5l3 3 5-5'/><path d='M14 8h7'/><path d='M3 16.5l3 3 5-5'/><path d='M14 17h7'/>",
  // rating (FORM-22): the shapes besides the star; the script fills them with the accent
  'heart': "<path d='M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z'/>",
  'circle': "<circle cx='12' cy='12' r='9'/>",
  // the open folder of the tree view (LIST-17); the closed one is `folder`
  'folder-open': "<path d='m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2'/>",
};

const all = { ...base, ...extra };
const names = Object.keys(all).sort();

for (const [name, inner] of Object.entries(all)) {
  if (!/^[a-z0-9-]+$/.test(name)) throw new Error(`Bad icon name: ${name}`);
  if (/[<>]/.test(inner.replace(/<[^>]+>/g, ''))) throw new Error(`Icon ${name} has stray markup`);
}

const symbols = names.map(n => `  <symbol id="mat-i-${n}" viewBox="0 0 24 24">${all[n]}</symbol>`).join('\n');
const svg = `<svg xmlns="http://www.w3.org/2000/svg" class="mat-sprite" width="0" height="0" aria-hidden="true" focusable="false">\n${symbols}\n</svg>\n`;

const target = path.join(__dirname, '..', 'src', 'MatUi', 'wwwroot', 'img', 'matui-icons.svg');
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, svg);
fs.writeFileSync(path.join(__dirname, '..', 'src', 'MatUi', 'wwwroot', 'img', 'matui-icons.json'), JSON.stringify(names, null, 1) + '\n');
console.log(`${names.length} icons written to ${path.relative(process.cwd(), target)}`);
