#!/usr/bin/env node
// Static scan of the Mat apps' repositories against the rules of the rulebook.
//   node tools/audit.js [path-to-the-folder-with-the-repositories]
// It reads layouts, pages, stylesheets and scripts and records what is WRITTEN there (a sidebar, a breadcrumb, a theme switch, browser dialogs,
// literal colours ...). It cannot see how a page looks: a "yes" means the markup or code is there, "unknown" means it could not be told.
// The result is a snapshot: src/MatDesign.Web/Data/audit.json (committed; run the script again to refresh it).
const fs = require('fs');
const path = require('path');

const root = path.resolve(process.argv[2] || path.join(__dirname, '..', '..'));

// id -> { dir (relative to root), extra css/js dirs }
const apps = {
  mattiverse: { dir: 'Mattiverse/src/Mattiverse.Web' },
  matos: { dir: 'MatOS/src/MatOS.Web' },
  matdesign: { dir: 'MatDesign/src/MatDesign.Web', extra: ['MatDesign/src/MatUi'] },
  matdock: { dir: 'MatDock/src/MatDock.Web' },
  matcad: { dir: 'Matcad/src/Matcad' },
  matddns: { dir: 'Matddns/src' },
  matgate: { dir: null, note: 'The repository has no UI of its own (Apache Guacamole and a browser farm).' },
  matbu: { dir: 'MatBu', layoutFiles: ['wwwroot/index.html'] },
  matarchive: { dir: 'Matarchive/src/Matarchive.Web' },
  matmail: { dir: 'MatMail/src/MatMail' },
  matpaper: { dir: 'MatPaper/src/MatPaper' },
  matdo: { dir: 'Matdo/src/Matdo.Web' },
  matfile: { dir: 'MatFile/src/MatFile.Web' },
  matcms: { dir: 'MatCMS/src/MatCMS', extra: ['MatCMS/src/MatCMS.Shared.Web'] },
  matmon: { dir: 'Matmon/src/Matmon.Host' },
  matstat: { dir: 'Matstat/src/Matstat.Web' },
  msbi: { dir: 'msBi/src/msBi.Web' },
  matbiz: { dir: 'Matbiz/src/Matbiz.Web' },
  matsplit: { dir: 'MatSplit/src/MatSplit.Web' },
  mattrakr: { dir: 'MatTrakr/src/MatTrakr' },
  matplay: { dir: 'MatPlay/src/MatPlay' },
  matlan: { dir: 'Matlan/Matlan' },
};

const checks = [
  { id: 'shell.menu-left', title: 'Menu with the logo', rule: 'SHELL-01', how: 'The layout has a sidebar (<aside> or a sidebar class) with a brand in it.' },
  { id: 'shell.breadcrumb', title: 'Breadcrumb', rule: 'SHELL-08', how: 'A breadcrumb is in the layout (yes) or only on some pages (partial).' },
  { id: 'shell.theme', title: 'Theme: system, light, dark', rule: 'SHELL-04', how: 'Dark mode exists (partial) and the choice includes "system" (yes).' },
  { id: 'shell.user', title: 'User menu', rule: 'SHELL-05', how: 'The layout has sign-out or account links and a dropdown or menu around them.' },
  { id: 'shell.version', title: 'Version in the menu', rule: 'SHELL-06', how: 'The layout shows a version.' },
  { id: 'dialog.native', title: 'No browser dialogs', rule: 'DIALOG-01', how: 'No confirm(), alert() or prompt() call in the app\'s scripts and pages.' },
  { id: 'dialog.used', title: 'Dialogs of its own', rule: 'DIALOG-04', how: 'The app uses <dialog> or a dialog/modal class.' },
  { id: 'color.tokens', title: 'Colours from tokens', rule: 'COLOR-01', how: 'Literal colours outside the variable definitions of the CSS: few (yes), some (partial), many (no).' },
  { id: 'code.inline', title: 'No inline styles', rule: 'CODE-02', how: 'style="..." attributes in the pages: few (yes), some (partial), many (no).' },
  { id: 'code.external', title: 'No outside resources', rule: 'CODE-03', how: 'No link or script that loads from another site in the layout.' },
  { id: 'type.system', title: 'System fonts', rule: 'TYPE-01', how: 'No @font-face and no web font link.' },
  { id: 'icon.inline', title: 'Inline SVG icons', rule: 'ICON-01', how: 'An inline SVG sprite or inline icons (yes); an icon font (partial).' },
  { id: 'mobile.query', title: 'Phone layout', rule: 'MOBILE-02', how: 'The CSS has max-width media queries.' },
  { id: 'a11y.motion', title: 'Reduced motion', rule: 'A11Y-05', how: 'The CSS honours prefers-reduced-motion.' },
];

const SKIP = /(^|[\\/])(bin|obj|node_modules|\.git|\.claude|data|lib|vendor|wwwroot[\\/]lib|worktrees|publish|App_Data)([\\/]|$)/i;

function walk(dir, filter, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (SKIP.test(path.relative(root, p))) continue;
    if (e.isDirectory()) walk(p, filter, out);
    else if (filter(p)) out.push(p);
  }
  return out;
}

const read = (f) => { try { return fs.readFileSync(f, 'utf8'); } catch { return ''; } };
const rel = (f) => path.relative(root, f).replace(/\\/g, '/');
const count = (text, rx) => (text.match(rx) || []).length;
const result = (state, note) => ({ state, note });

function scan(id, spec) {
  if (!spec.dir) {
    return { id, layout: '', results: Object.fromEntries(checks.map(c => [c.id, result('unknown', spec.note || 'No UI found.')])) };
  }

  const base = path.join(root, spec.dir);
  const dirs = [base, ...(spec.extra || []).map(d => path.join(root, d))];
  const layoutFiles = spec.layoutFiles
    ? spec.layoutFiles.map(f => path.join(base, f)).filter(fs.existsSync)
    : walk(base, p => /(^|[\\/])_Layout[^\\/]*\.(cshtml|razor)$|MainLayout\.razor$/.test(p));
  const pages = dirs.flatMap(d => walk(d, p => /\.(cshtml|razor|html)$/.test(p)));
  const css = dirs.flatMap(d => walk(d, p => /\.css$/.test(p) && !/\.min\.|bootstrap|\.cshtml\.css$/i.test(p)));
  const js = dirs.flatMap(d => walk(d, p => /\.js$/.test(p) && !/\.min\.|bootstrap|jquery|chart|vendor|sw\.js|service-worker/i.test(p)));

  const layout = layoutFiles.map(read).join('\n');
  const pageText = pages.map(read).join('\n');
  const cssText = css.map(read).join('\n').replace(/\/\*[\s\S]*?\*\//g, '');
  const jsText = js.map(read).join('\n');
  const all = layout + pageText;
  const r = {};

  // --- shell
  const sidebar = /<aside|class="[^"]*(sidebar|app-nav|side-nav|mat-nav)\b/i.test(layout);
  const brand = /brand|logo/i.test(layout);
  r['shell.menu-left'] = sidebar ? result(brand ? 'yes' : 'partial', brand ? 'Sidebar with a brand in the layout.' : 'Sidebar, but no brand found in it.')
    : result(/<nav|navbar/i.test(layout) ? 'partial' : 'no', /<nav|navbar/i.test(layout) ? 'Only a top navigation.' : 'No sidebar in the layout.');

  const inLayout = /breadcrumb|crumb/i.test(layout);
  const inPages = count(pageText, /breadcrumb|crumb/gi);
  r['shell.breadcrumb'] = inLayout ? result('yes', 'In the layout.') : inPages > 0 ? result('partial', `Only on some pages (${inPages} places).`) : result('no', 'None found.');

  const dark = /prefers-color-scheme|data-theme=["']?dark|\[data-theme="dark"\]|data-mode="dark"|\.dark\b|data-bs-theme/i.test(cssText + layout);
  const system = /system/i.test(layout + jsText) && /(theme|mode)/i.test(layout + jsText) && /data-theme-mode|data-theme-toggle|theme-toggle|theme-switch|data-ms-theme/i.test(layout + jsText + cssText);
  r['shell.theme'] = dark ? (system ? result('yes', 'Dark mode and a system option.') : result('partial', 'Dark mode, but no system option found.')) : result('no', 'No dark mode found.');

  const signout = /logout|sign.?out|abmelden|logoff/i.test(layout);
  const account = /account|profile|konto/i.test(layout);
  const menu = /dropdown|popover|<details|role="menu"|user-menu|usermenu|account-menu/i.test(layout);
  r['shell.user'] = signout && menu ? result('yes', 'Sign-out and a menu around it.') : (signout || account) ? result('partial', 'Account or sign-out link, but no menu around it.') : result('no', 'No account or sign-out in the layout.');

  const version = /version/i.test(layout);
  r['shell.version'] = version ? result('yes', 'A version is shown in the layout.') : result('no', 'No version in the layout.');

  // --- dialogs
  const nativeDialogs = count(jsText + pageText, /(?<![\w.])(confirm|alert|prompt)\s*\(/g) - count(jsText + pageText, /(?:Mat\w*|dlg|dialog|ui|\w+)\.(confirm|alert|prompt)\s*\(/g);
  const nativeCalls = Math.max(0, count(jsText + pageText, /\bwindow\.(confirm|alert|prompt)\s*\(|(?<![\w.$])(confirm|alert|prompt)\s*\(\s*['"`]/g));
  r['dialog.native'] = nativeCalls === 0 ? result('yes', 'No browser dialogs.') : nativeCalls <= 3 ? result('partial', `${nativeCalls} call(s) of confirm, alert or prompt.`) : result('no', `${nativeCalls} calls of confirm, alert or prompt.`);
  void nativeDialogs;

  const dialogs = count(all + cssText, /<dialog|class="[^"]*(modal|dialog)/gi);
  r['dialog.used'] = dialogs > 0 ? result('yes', `${dialogs} dialog or modal elements.`) : result('no', 'No dialog elements found.');

  // --- code and colour
  const decl = cssText.replace(/--[\w-]+\s*:[^;}]*;?/g, ' ');
  const literal = count(decl, /#[0-9a-fA-F]{3,8}\b|rgba?\(\s*\d/g);
  r['color.tokens'] = literal <= 15 ? result('yes', `${literal} literal colours outside the variables.`) : literal <= 120 ? result('partial', `${literal} literal colours outside the variables.`) : result('no', `${literal} literal colours outside the variables.`);

  const inline = count(pageText, /\sstyle="/g);
  r['code.inline'] = inline <= 4 ? result('yes', `${inline} inline style attributes.`) : inline <= 40 ? result('partial', `${inline} inline style attributes.`) : result('no', `${inline} inline style attributes.`);

  const external = count(layout, /<(link|script)[^>]+(href|src)="https?:\/\/(?!localhost)/gi);
  r['code.external'] = external === 0 ? result('yes', 'The layout loads nothing from another site.') : result('no', `${external} outside resource(s) in the layout.`);

  const webfont = /@font-face|fonts\.googleapis|fonts\.gstatic|use\.typekit|bunny\.net/i.test(cssText + layout);
  r['type.system'] = webfont ? result('no', 'A web font is loaded.') : result('yes', 'System fonts.');

  const sprite = /<symbol|<use\s|<svg/i.test(all);
  const iconFont = /\bti ti-|\bfa-|\bfas fa|bootstrap-icons|\bbi bi-|material-icons|\bmdi-/i.test(all + cssText);
  r['icon.inline'] = sprite && !iconFont ? result('yes', 'Inline SVG.') : sprite && iconFont ? result('partial', 'Inline SVG and an icon font.') : iconFont ? result('partial', 'An icon font.') : result('unknown', 'No icons found.');

  // --- phones and accessibility
  const queries = count(cssText, /@media[^{]*max-width/g);
  r['mobile.query'] = queries > 0 ? result('yes', `${queries} max-width media queries.`) : result('no', 'No max-width media query.');
  r['a11y.motion'] = /prefers-reduced-motion/i.test(cssText) ? result('yes', 'prefers-reduced-motion is honoured.') : result('no', 'prefers-reduced-motion is not mentioned.');

  return { id, layout: layoutFiles.map(rel).join(', ') || '(none found)', results: r };
}

const out = {
  generated: new Date().toISOString().slice(0, 10),
  checks,
  apps: Object.entries(apps).map(([id, spec]) => scan(id, spec)),
};

const target = path.join(__dirname, '..', 'src', 'MatDesign.Web', 'Data', 'audit.json');
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, JSON.stringify(out, null, 1));

const mark = { yes: 'Y', partial: '~', no: '-', unknown: '?' };
console.log('app'.padEnd(11), checks.map((c, i) => String(i + 1).padStart(2)).join(' '));
for (const a of out.apps) console.log(a.id.padEnd(11), checks.map(c => ' ' + mark[a.results[c.id].state]).join(' '));
console.log(checks.map((c, i) => `${i + 1}=${c.title}`).join('  '));
console.log('written to', path.relative(process.cwd(), target));
