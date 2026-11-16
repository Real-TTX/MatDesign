#!/usr/bin/env node
// Writes the generated files of MatDesktop, the toolkit for desktop programs (src/MatDesktop, docs/DESKTOP.md), from the files of MatUi:
//   node tools/desktop.js
//
//   src/MatDesktop/Tokens/MatTokens.g.cs   the base tokens of src/MatUi/wwwroot/css/matui.tokens.css as C# values: the colours in light and dark, the sizes in px, the type, the
//                                         motion, and the rules by which the accent tokens are derived from the app's accent (MatPalette applies them). Plain C#, only System:
//                                         a WinForms tray copies it with MatColor.cs and MatPalette.cs (AGENT-05).
//   src/MatDesktop/Themes/MatIcons.axaml   every symbol of src/MatUi/wwwroot/img/matui-icons.svg as an Avalonia geometry (MatIcon.<name>, and MatIcon.<name>.Fill for the parts the
//                                         sprite fills), drawn by the control MatIcon with the 2 px line of the web (DESK-09).
//
// A desktop window is used with a mouse, so the sizes are those of MatUi's compact density with a fine pointer (`:root[data-density="compact"]` and its `(pointer: fine)` block:
// controls 34 px, body type 14 px). Every token of the base block is either written or skipped here with its reason: a new token stops this script until it is decided.
// CI runs the script and fails when the files in the repository differ from what it writes (they are never edited by hand). LF line endings.
'use strict';

const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const tokensPath = 'src/MatUi/wwwroot/css/matui.tokens.css';
const spritePath = 'src/MatUi/wwwroot/img/matui-icons.svg';
const css = fs.readFileSync(path.join(root, tokensPath), 'utf8').replace(/\r\n/g, '\n');
const sprite = fs.readFileSync(path.join(root, spritePath), 'utf8').replace(/\r\n/g, '\n');

function fail(message) {
  console.error(`desktop.js: ${message}`);
  process.exit(1);
}

// ---------------------------------------------------------------------------------------------------------------- CSS

// The rules of a stylesheet: selector, the media query around it (or null) and the declarations with the comment behind each.
function blocks(text) {
  const result = [];
  scan(text, null);
  return result;

  function scan(body, media) {
    let i = 0;
    while (i < body.length) {
      if (/\s/.test(body[i])) { i++; continue; }
      if (body.startsWith('/*', i)) { const end = body.indexOf('*/', i); i = end < 0 ? body.length : end + 2; continue; }
      const open = body.indexOf('{', i);
      if (open < 0) break;
      const selector = body.slice(i, open).replace(/\s+/g, ' ').trim();
      let depth = 1;
      let j = open + 1;
      while (j < body.length && depth > 0) {
        if (body.startsWith('/*', j)) { const end = body.indexOf('*/', j); j = end < 0 ? body.length : end + 2; continue; }
        if (body[j] === '{') depth++;
        if (body[j] === '}') depth--;
        j++;
      }
      const inner = body.slice(open + 1, j - 1);
      if (selector.startsWith('@media')) scan(inner, selector.slice('@media'.length).trim());
      else if (!selector.startsWith('@')) result.push({ selector, media, body: inner, declarations: declarations(inner) });
      i = j;
    }
  }
}

function declarations(body) {
  const list = [];
  const re = /(--[\w-]+)\s*:\s*((?:[^;/]|\/(?!\*))+?)\s*;[ \t]*(?:\/\*\s*(.*?)\s*\*\/)?/g;
  let m;
  while ((m = re.exec(body)) !== null) list.push({ name: m[1], value: m[2].replace(/\s+/g, ' ').trim(), comment: m[3] || '' });
  return list;
}

const all = blocks(css);
const only = (selector, media = null) => {
  const found = all.filter(b => b.selector === selector && b.media === media);
  if (found.length === 0) fail(`no rule "${selector}"${media ? ` inside @media ${media}` : ''} in ${tokensPath}`);
  return found;
};

// The base tokens with their group (the last "/* ---- Name ---- */" before them), in the order of the file.
const base = [];
{
  let group = '';
  for (const raw of only(':root')[0].body.split('\n')) {
    const line = raw.trim();
    const heading = /^\/\*\s*-+\s*(.*?)\s*-+\s*\*\/$/.exec(line);
    if (heading) { group = heading[1]; continue; }
    for (const d of declarations(line)) base.push({ ...d, group });
  }
}
const darkValues = Object.fromEntries(only(':root[data-theme="dark"]').flatMap(b => b.declarations).map(d => [d.name, d.value]));
const accentLight = only(':root, [data-app]')[0].declarations;
const accentDark = only(':root[data-theme="dark"], :root[data-theme="dark"] [data-app]')[0].declarations;
const compact = Object.fromEntries([
  ...only(':root[data-density="compact"]').flatMap(b => b.declarations),
  ...only(':root[data-density="compact"]', '(pointer: fine)').flatMap(b => b.declarations),
].map(d => [d.name, d.value]));

// ------------------------------------------------------------------------------------------------------------- values

const pascal = name => name.replace(/^--mat-/, '').split('-').map(p => p.charAt(0).toUpperCase() + p.slice(1)).join('');
const number = v => (Math.round(v * 10000) / 10000).toString();
const escapeXml = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const doc = (text, name) => `/// <summary>${escapeXml(text ? `${text} (${name})` : name)}</summary>`;

function colour(value, name) {
  const v = value.trim();
  let m = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(v);
  if (m) {
    const h = m[1].length === 3 ? m[1].split('').map(c => c + c).join('') : m[1];
    return { r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16), a: 1 };
  }
  m = /^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+%?)\s*)?\)$/.exec(v);
  if (m) {
    const alpha = m[4] === undefined ? 1 : m[4].endsWith('%') ? Number(m[4].slice(0, -1)) / 100 : Number(m[4]);
    return { r: Number(m[1]), g: Number(m[2]), b: Number(m[3]), a: alpha };
  }
  fail(`${name}: "${value}" is not a colour this script reads (#rgb, #rrggbb or rgba())`);
}

const rgba = c => `new(${c.r}, ${c.g}, ${c.b}${c.a === 1 ? '' : `, ${number(c.a)}`})`;

// What a value of a size is in px: px as it is, rem times 16, a var() of another size of the list.
function pixels(value, name, known) {
  const v = value.trim();
  let m = /^(-?[\d.]+)px$/.exec(v);
  if (m) return Number(m[1]);
  m = /^(-?[\d.]+)rem$/.exec(v);
  if (m) return Number(m[1]) * 16;
  m = /^var\((--[\w-]+)\)$/.exec(v);
  if (m && known.has(m[1])) return known.get(m[1]);
  fail(`${name}: "${value}" is not a size this script reads (px, rem or var() of a size)`);
}

// The groups of the base block and what becomes of them. A token of a group that is not here stops the script.
const SKIP = {
  'Elevation': 'shadows: the flat look draws lines, not shadows; the scroll thumb is written as a colour',
  'Layers': 'stacking order of a web page; a desktop window has its own',
  'Colour picker': 'the colour model of the web picker, not a colour of a surface',
  'Device': 'safe areas of a phone',
};
const SKIP_TOKENS = {
  '--mat-accent-text-keep': 'the share of the accent that a colour scheme gives up; MatDesktop 0.1 has no colour schemes and keeps all of it',
  '--mat-shadow-1': SKIP.Elevation, '--mat-shadow-2': SKIP.Elevation, '--mat-shadow-3': SKIP.Elevation,
};

const colours = [];
const sizes = [];
const type = [];
const motion = [];
const known = new Map();
for (const d of base) {
  if (SKIP_TOKENS[d.name] || (SKIP[d.group] && d.name !== '--mat-scroll-thumb')) continue;
  const value = compact[d.name] ?? d.value;
  if (['Surfaces', 'Lines', 'Text', 'Status'].includes(d.group) || d.name === '--mat-scroll-thumb') {
    colours.push({ name: d.name, comment: d.comment, light: colour(d.value, d.name), dark: colour(darkValues[d.name] ?? d.value, d.name) });
  } else if (['Shape', 'Spacing (4 px scale)', 'Layout'].includes(d.group)) {
    const px = pixels(value, d.name, known);
    known.set(d.name, px);
    sizes.push({ name: d.name, comment: d.comment, px, compact: compact[d.name] !== undefined });
  } else if (d.group === 'Type') {
    if (d.name === '--mat-font' || d.name === '--mat-mono') type.push({ name: d.name, comment: d.comment, text: value });
    else if (/^--mat-lh/.test(d.name)) type.push({ name: d.name, comment: d.comment, ratio: Number(value) });
    else type.push({ name: d.name, comment: d.comment, px: pixels(value, d.name, known), compact: compact[d.name] !== undefined });
  } else if (d.group === 'Motion') {
    const ms = /^([\d.]+)ms$/.exec(value);
    const ease = /^cubic-bezier\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*\)$/.exec(value);
    if (ms) motion.push({ name: d.name, comment: d.comment, ms: Number(ms[1]) });
    else if (ease) motion.push({ name: d.name, comment: d.comment, ease: value, points: ease.slice(1, 5).map(Number) });
    else fail(`${d.name}: "${value}" is neither a duration in ms nor a cubic-bezier()`);
  } else {
    fail(`${d.name} (group "${d.group}") is a token this script does not know: write it or skip it with a reason`);
  }
}

for (const name of Object.keys(compact)) {
  if (!sizes.some(s => s.name === name) && !type.some(t => t.name === name)) fail(`the compact density sets ${name}, which is not a size or a type size here`);
}

// The derived accent tokens: each declaration of the two accent blocks is one of these forms, or the script stops.
const DEFAULT_ACCENT = (/var\(--mat-app-accent,\s*(#[0-9a-f]{6})\)/i.exec(accentLight.find(d => d.name === '--mat-accent')?.value || '') || [])[1];
if (!DEFAULT_ACCENT) fail('the default accent (var(--mat-app-accent, #......)) of --mat-accent was not found');
let focusRingWidth = null;

function accentRule(d, mode) {
  const v = d.value;
  const share = s => number(Number(s) / 100);
  let m;
  if ((m = /^var\(--mat-app-accent, (#[0-9a-f]{6})\)$/i.exec(v))) return `new("${d.name}", MatAccentKind.App)`;
  if ((m = /^color-mix\(in oklab, var\(--mat-app-accent, #[0-9a-f]{6}\) ([\d.]+)%, (white|black)\)$/i.exec(v))) return `new("${d.name}", MatAccentKind.AppMix, ${share(m[1])}, MatMixWith.${m[2] === 'white' ? 'White' : 'Black'})`;
  if ((m = /^color-mix\(in oklab, var\(--mat-accent\) ([\d.]+)%, (white|black)\)$/.exec(v))) return `new("${d.name}", MatAccentKind.Mix, ${share(m[1])}, MatMixWith.${m[2] === 'white' ? 'White' : 'Black'})`;
  if ((m = /^color-mix\(in srgb, var\(--mat-accent\) ([\d.]+)%, transparent\)$/.exec(v))) return `new("${d.name}", MatAccentKind.Alpha, ${share(m[1])})`;
  if ((m = /^0 0 0 ([\d.]+)px color-mix\(in srgb, var\(--mat-accent\) ([\d.]+)%, transparent\)$/.exec(v))) {
    if (focusRingWidth !== null && focusRingWidth !== Number(m[1])) fail('the focus ring has another width in dark mode');
    focusRingWidth = Number(m[1]);
    return `new("${d.name}", MatAccentKind.Alpha, ${share(m[2])})`;
  }
  if ((m = /^var\(--mat-app-on-accent, (#[0-9a-f]{6})\)$/i.exec(v))) return `new("${d.name}", MatAccentKind.OnApp, Value: "${m[1].toLowerCase()}")`;
  if ((m = /^(#[0-9a-f]{6})$/i.exec(v))) return `new("${d.name}", MatAccentKind.Fixed, Value: "${m[1].toLowerCase()}")`;
  // the text colour of the accent: the app's own (light only) or the accent mixed toward black or white, then mixed with the text by --mat-accent-text-keep (100% in MatDesktop)
  if ((m = /^color-mix\(in oklab, var\(--mat-app-accent-text, color-mix\(in oklab, var\(--mat-accent\) ([\d.]+)%, (white|black)\)\) var\(--mat-accent-text-keep, 100%\), var\(--mat-text\)\)$/.exec(v))) {
    return `new("${d.name}", MatAccentKind.TextApp, ${share(m[1])}, MatMixWith.${m[2] === 'white' ? 'White' : 'Black'})`;
  }
  if ((m = /^color-mix\(in oklab, color-mix\(in oklab, var\(--mat-accent\) ([\d.]+)%, (white|black)\) var\(--mat-accent-text-keep, 100%\), var\(--mat-text\)\)$/.exec(v))) {
    return `new("${d.name}", MatAccentKind.Mix, ${share(m[1])}, MatMixWith.${m[2] === 'white' ? 'White' : 'Black'})`;
  }
  fail(`${d.name} (${mode}): "${v}" is a derivation of the accent this script does not know`);
}

const rulesLight = accentLight.map(d => accentRule(d, 'light'));
const rulesDark = accentDark.map(d => accentRule(d, 'dark'));
const accentNames = accentLight.map(d => d.name).join(',');
if (accentNames !== accentDark.map(d => d.name).join(',')) fail('the light and the dark accent blocks do not derive the same tokens in the same order');
if (focusRingWidth === null) fail('the focus ring (--mat-focus-ring) was not found');

// ------------------------------------------------------------------------------------------------------- MatTokens.g.cs

const LF = '\n';
const out = [];
out.push('// <auto-generated>');
out.push(`// Generated by tools/desktop.js from ${tokensPath}: do not edit. Change the CSS and run \`node tools/desktop.js\` (CI fails when this file is stale).`);
out.push('// </auto-generated>');
out.push('// Plain C#, only System: a WinForms tray copies it together with MatColor.cs and MatPalette.cs (AGENT-05, tools/matdesktop-copy.js --tokens-only).');
out.push('namespace MatDesktop;');
out.push('');
out.push('/// <summary>');
out.push('/// The tokens of MatUi (matui.tokens.css) for a desktop program: the colours of the surfaces, lines, text and states in light and dark, the sizes, the type and the motion.');
out.push('/// The sizes are those of the compact density with a mouse (<c>data-density="compact"</c> and <c>pointer: fine</c>: controls 34 px, body type 14 px), because a desktop window is used');
out.push('/// with a mouse. The accent tokens depend on the app\'s accent (IDENT-01) and are computed by <see cref="MatPalette"/> with the rules in <see cref="AccentLight"/> and <see cref="AccentDark"/>.');
out.push('/// </summary>');
out.push('public static class MatTokens');
out.push('{');
out.push(`    /// <summary>The file the values come from.</summary>`);
out.push(`    public const string Source = "${tokensPath}";`);
out.push('');
out.push('    /// <summary>The accent of an app that sets none (<c>var(--mat-app-accent, ...)</c>).</summary>');
out.push(`    public const string DefaultAccent = "${DEFAULT_ACCENT.toLowerCase()}";`);
out.push('');
out.push('    /// <summary>The width of the focus ring in px (<c>--mat-focus-ring</c>); its colour is the accent token <c>--mat-focus-ring</c>.</summary>');
out.push(`    public const double FocusRingWidth = ${number(focusRingWidth)};`);
for (const [mode, key] of [['Light', 'light'], ['Dark', 'dark']]) {
  out.push('');
  out.push(`    /// <summary>The colours of ${key} mode${key === 'dark' ? ' (the block <c>:root[data-theme="dark"]</c>; a token without a dark value keeps its light one)' : ''}.</summary>`);
  out.push(`    public static class ${mode}`);
  out.push('    {');
  colours.forEach((c, i) => {
    if (i > 0) out.push('');
    out.push(`        ${doc(c.comment, c.name)}`);
    out.push(`        public static readonly MatRgba ${pascal(c.name)} = ${rgba(c[key])};`);
  });
  out.push('    }');
}
out.push('');
out.push('    /// <summary>Sizes in px (device-independent pixels), with the compact density of a mouse where it sets one.</summary>');
out.push('    public static class Size');
out.push('    {');
sizes.forEach((s, i) => {
  if (i > 0) out.push('');
  out.push(`        ${doc(s.comment + (s.compact ? ' Compact density.' : ''), s.name)}`);
  out.push(`        public const double ${pascal(s.name)} = ${number(s.px)};`);
});
out.push('    }');
out.push('');
out.push('    /// <summary>Type: the font stacks of the web, the sizes in px (rem times 16, the compact density where it sets one) and the line heights as a factor.</summary>');
out.push('    public static class Type');
out.push('    {');
type.forEach((t, i) => {
  if (i > 0) out.push('');
  out.push(`        ${doc(t.comment + (t.compact ? ' Compact density.' : ''), t.name)}`);
  if (t.text !== undefined) out.push(`        public const string ${pascal(t.name)} = "${t.text.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}";`);
  else if (t.ratio !== undefined) out.push(`        public const double ${pascal(t.name)} = ${number(t.ratio)};`);
  else out.push(`        public const double ${pascal(t.name)} = ${number(t.px)};`);
});
out.push('    }');
out.push('');
out.push('    /// <summary>Motion: durations in ms and the one easing curve (its four control points as well).</summary>');
out.push('    public static class Motion');
out.push('    {');
motion.forEach((t, i) => {
  if (i > 0) out.push('');
  out.push(`        ${doc(t.comment, t.name)}`);
  if (t.ms !== undefined) out.push(`        public const double ${pascal(t.name)} = ${number(t.ms)};`);
  else {
    out.push(`        public const string ${pascal(t.name)} = "${t.ease}";`);
    out.push('');
    out.push(`        /// <summary>The control points of ${pascal(t.name)}: x1, y1, x2, y2.</summary>`);
    out.push(`        public static readonly double[] ${pascal(t.name)}Points = [${t.points.map(number).join(', ')}];`);
  }
});
out.push('    }');
out.push('');
out.push('    /// <summary>How the accent tokens of light mode are derived from the app\'s accent (the block <c>:root, [data-app]</c>).</summary>');
out.push('    public static readonly IReadOnlyList<MatAccentRule> AccentLight =');
out.push('    [');
for (const r of rulesLight) out.push(`        ${r},`);
out.push('    ];');
out.push('');
out.push('    /// <summary>How the accent tokens of dark mode are derived (the block <c>:root[data-theme="dark"], ... [data-app]</c>): a lighter accent with dark text on it.</summary>');
out.push('    public static readonly IReadOnlyList<MatAccentRule> AccentDark =');
out.push('    [');
for (const r of rulesDark) out.push(`        ${r},`);
out.push('    ];');
out.push('');
out.push('    /// <summary>Every colour token of this file with its light and dark value, in the order of the CSS.</summary>');
out.push('    public static readonly IReadOnlyList<MatTokenColor> Colors =');
out.push('    [');
for (const c of colours) out.push(`        new("${c.name}", Light.${pascal(c.name)}, Dark.${pascal(c.name)}),`);
out.push('    ];');
out.push('');
out.push('    /// <summary>Every size and type size of this file in px, in the order of the CSS.</summary>');
out.push('    public static readonly IReadOnlyList<MatTokenNumber> Pixels =');
out.push('    [');
for (const s of sizes) out.push(`        new("${s.name}", Size.${pascal(s.name)}),`);
for (const t of type.filter(t => t.px !== undefined)) out.push(`        new("${t.name}", Type.${pascal(t.name)}),`);
out.push('    ];');
out.push('}');
const tokensFile = out.join(LF) + LF;

// ------------------------------------------------------------------------------------------------------- MatIcons.axaml

const fmt = v => (Math.round(v * 1000) / 1000).toString();
const attrs = tag => {
  const result = {};
  for (const m of tag.matchAll(/([a-zA-Z0-9-]+)=['"]([^'"]*)['"]/g)) result[m[1]] = m[2];
  return result;
};

function rectPath(a) {
  const x = Number(a.x || 0), y = Number(a.y || 0), w = Number(a.width), h = Number(a.height);
  const r = Math.min(Number(a.rx || a.ry || 0), w / 2, h / 2);
  if (r <= 0) return `M${fmt(x)},${fmt(y)} H${fmt(x + w)} V${fmt(y + h)} H${fmt(x)} Z`;
  return `M${fmt(x + r)},${fmt(y)} H${fmt(x + w - r)} A${fmt(r)},${fmt(r)} 0 0 1 ${fmt(x + w)},${fmt(y + r)} ` +
    `V${fmt(y + h - r)} A${fmt(r)},${fmt(r)} 0 0 1 ${fmt(x + w - r)},${fmt(y + h)} H${fmt(x + r)} ` +
    `A${fmt(r)},${fmt(r)} 0 0 1 ${fmt(x)},${fmt(y + h - r)} V${fmt(y + r)} A${fmt(r)},${fmt(r)} 0 0 1 ${fmt(x + r)},${fmt(y)} Z`;
}

function ellipsePath(cx, cy, rx, ry) {
  return `M${fmt(cx - rx)},${fmt(cy)} A${fmt(rx)},${fmt(ry)} 0 1 0 ${fmt(cx + rx)},${fmt(cy)} A${fmt(rx)},${fmt(ry)} 0 1 0 ${fmt(cx - rx)},${fmt(cy)} Z`;
}

function pointsPath(points, close) {
  const values = points.trim().split(/[\s,]+/).map(Number);
  let d = '';
  for (let i = 0; i < values.length; i += 2) d += `${i === 0 ? 'M' : ' L'}${fmt(values[i])},${fmt(values[i + 1])}`;
  return close ? d + ' Z' : d;
}

// SVG path data is turned into absolute commands that Avalonia's parser reads the same way as a browser: numbers written out ("l.06.06", "1-2.8"), the
// flags of an arc as single digits ("0 011"), every relative command made absolute (a path element starts at the origin, and the parts of an icon are joined), and the smooth
// curves S and T written as C and Q with the reflected control point of the spec: Avalonia reflects the control point of an earlier curve even after a line, which bent the bell.
function normalizePath(d) {
  let i = 0;
  const isCmd = c => /[MmLlHhVvCcSsQqTtAaZz]/.test(c);
  const skip = () => { while (i < d.length && /[\s,]/.test(d[i])) i++; };
  const num = () => {
    skip();
    const m = /^[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?/.exec(d.slice(i));
    if (!m) fail(`a path has no number at ${i}: ${d}`);
    i += m[0].length;
    return Number(m[0]);
  };
  const flag = () => {
    skip();
    const c = d[i++];
    if (c !== '0' && c !== '1') fail(`a path has a bad arc flag: ${d}`);
    return c;
  };
  const point = (px, py) => `${fmt(px)},${fmt(py)}`;
  const out = [];
  let x = 0, y = 0, sx = 0, sy = 0, cubic = null, quad = null, cmd = null;
  for (;;) {
    skip();
    if (i >= d.length) break;
    if (isCmd(d[i])) cmd = d[i++];
    else if (cmd === null) fail(`a path does not start with a command: ${d}`);
    const rel = cmd === cmd.toLowerCase();
    const C = cmd.toUpperCase();
    const ox = rel ? x : 0;
    const oy = rel ? y : 0;
    let nextCubic = null;
    let nextQuad = null;
    if (C === 'Z') {
      out.push('Z');
      x = sx; y = sy;
      cmd = null;
    } else if (C === 'M') {
      x = ox + num(); y = oy + num();
      sx = x; sy = y;
      out.push(`M${point(x, y)}`);
      cmd = rel ? 'l' : 'L'; // pairs after a moveto are line-tos
    } else if (C === 'L') {
      x = ox + num(); y = oy + num();
      out.push(`L${point(x, y)}`);
    } else if (C === 'H') {
      x = ox + num();
      out.push(`L${point(x, y)}`);
    } else if (C === 'V') {
      y = oy + num();
      out.push(`L${point(x, y)}`);
    } else if (C === 'C' || C === 'S') {
      const c1 = C === 'C' ? [ox + num(), oy + num()] : cubic ? [2 * x - cubic[0], 2 * y - cubic[1]] : [x, y];
      const c2 = [ox + num(), oy + num()];
      x = ox + num(); y = oy + num();
      out.push(`C${point(c1[0], c1[1])} ${point(c2[0], c2[1])} ${point(x, y)}`);
      nextCubic = c2;
    } else if (C === 'Q' || C === 'T') {
      const q = C === 'Q' ? [ox + num(), oy + num()] : quad ? [2 * x - quad[0], 2 * y - quad[1]] : [x, y];
      x = ox + num(); y = oy + num();
      out.push(`Q${point(q[0], q[1])} ${point(x, y)}`);
      nextQuad = q;
    } else if (C === 'A') {
      const rx = num(), ry = num(), rotation = num(), large = flag(), sweep = flag();
      x = ox + num(); y = oy + num();
      out.push(`A${point(rx, ry)} ${fmt(rotation)} ${large} ${sweep} ${point(x, y)}`);
    } else {
      fail(`a path has the command ${cmd}: ${d}`);
    }
    cubic = nextCubic;
    quad = nextQuad;
  }
  return out.join(' ');
}

function elementPath(kind, a) {
  switch (kind) {
    case 'path': return normalizePath(a.d);
    case 'rect': return rectPath(a);
    case 'circle': return ellipsePath(Number(a.cx), Number(a.cy), Number(a.r), Number(a.r));
    case 'ellipse': return ellipsePath(Number(a.cx), Number(a.cy), Number(a.rx), Number(a.ry));
    case 'line': return `M${fmt(Number(a.x1))},${fmt(Number(a.y1))} L${fmt(Number(a.x2))},${fmt(Number(a.y2))}`;
    case 'polyline': return pointsPath(a.points, false);
    case 'polygon': return pointsPath(a.points, true);
    default: fail(`an icon has a <${kind}>, which this script cannot turn into a path`);
  }
}

const icons = [];
for (const symbol of sprite.matchAll(/<symbol id="mat-i-([a-z0-9-]+)" viewBox="0 0 24 24">([\s\S]*?)<\/symbol>/g)) {
  const stroke = [];
  const filled = [];
  for (const el of symbol[2].matchAll(/<([a-z]+)\b([^>]*?)\/?>/g)) {
    const a = attrs(el[2]);
    (a.fill === 'currentColor' ? filled : stroke).push(elementPath(el[1], a));
  }
  icons.push({ name: symbol[1], stroke: stroke.join(' '), fill: filled.join(' ') });
}
const symbolCount = (sprite.match(/<symbol\b/g) || []).length;
if (icons.length !== symbolCount || icons.length === 0) fail(`read ${icons.length} of the ${symbolCount} symbols of the sprite`);

const iconLines = [];
for (const icon of icons) {
  if (icon.stroke) iconLines.push(`  <StreamGeometry x:Key="MatIcon.${icon.name}">${icon.stroke}</StreamGeometry>`);
  else iconLines.push(`  <StreamGeometry x:Key="MatIcon.${icon.name}">M0,0</StreamGeometry>`);
  if (icon.fill) iconLines.push(`  <StreamGeometry x:Key="MatIcon.${icon.name}.Fill">${icon.fill}</StreamGeometry>`);
}
const iconsFile = [
  '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
  '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
  '                    x:Class="MatDesktop.MatIconResources">',
  `  <!-- Generated by tools/desktop.js from ${spritePath}: do not edit. Every symbol of the sprite (${icons.length}) on its 24 x 24 grid: MatIcon.<name> is stroked`,
  '       with the 2 px line of the web and round ends, MatIcon.<name>.Fill (where the sprite fills a part) is filled. Drawn by the control MatIcon (DESK-09). -->',
  ...iconLines,
  '</ResourceDictionary>',
  '',
].join(LF);

// ------------------------------------------------------------------------------------------------------------- write

const targets = [
  [path.join(root, 'src', 'MatDesktop', 'Tokens', 'MatTokens.g.cs'), tokensFile],
  [path.join(root, 'src', 'MatDesktop', 'Themes', 'MatIcons.axaml'), iconsFile],
];
for (const [file, text] of targets) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text);
}
console.log(`${colours.length} colours, ${sizes.length} sizes, ${type.length} type values, ${motion.length} motion values and ${rulesLight.length} accent rules written to src/MatDesktop/Tokens/MatTokens.g.cs`);
console.log(`${icons.length} icons written to src/MatDesktop/Themes/MatIcons.axaml`);
