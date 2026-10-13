#!/usr/bin/env node
// Checks the copy of the toolkit MatUi that an app carries (docs/VERSIONING.md):
//   node tools/matui-check.js <folder of the app> [--changelog <CHANGELOG.md or its address>] [--json]
// It finds the files of MatUi in the folder (matui.css, matui.js, matui.boot.js, matui-icons.svg, matui.manifest.json) by what is written in them, and says
//   - which version each file is, and whether they are all the same version (the files of one version belong together),
//   - whether a file was changed by hand: the archive that an app copies holds matui.manifest.json with the SHA-256 of every file,
//   - what an update takes with it: the versions after this one in the changelog, whether one of them breaks something and the migration steps.
// The changelog is the one of this repository (CHANGELOG.md next to tools/), or the one at an address (--changelog http://localhost:8060/ui/CHANGELOG.md).
// Exit code: 0 the copy is the newest version and unchanged, 1 an update is available, 2 the update needs changes in the app, or the copy is mixed or changed by hand.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const args = process.argv.slice(2);
const flag = name => { const i = args.indexOf(name); return i < 0 ? null : (args[i + 1] ?? true); };
const json = args.includes('--json');
const folder = args.find((a, i) => !a.startsWith('--') && !(args[i - 1] || '').startsWith('--'));
if (!folder) { console.error('usage: node tools/matui-check.js <folder of the app> [--changelog <file or address>] [--json]'); process.exit(64); }

const SKIP = new Set(['node_modules', 'bin', 'obj', '.git', '.vs', '.idea', 'packages', 'dist-old']);
const NAMES = new Set(['matui.css', 'matui.js', 'matui.boot.js', 'matui-icons.svg', 'matui.manifest.json']);

// ---- the changelog (the same format as MatDesign.Core.Changelog reads)
function parseChangelog(markdown) {
  const entries = [];
  let entry = null, section = null;
  for (const raw of markdown.replace(/\r\n/g, '\n').split('\n')) {
    const line = raw.replace(/\s+$/, '');
    const heading = /^##\s+\[([^\]]+)\](?:\s+-\s+(\d{4}-\d{2}-\d{2}))?\s*$/.exec(line);
    if (heading) { entry = { version: heading[1].trim(), date: heading[2] || null, note: [], sections: [] }; entries.push(entry); section = null; continue; }
    if (!entry || line.length === 0) { continue; }
    const sectionHeading = /^###\s+(.+?)\s*$/.exec(line);
    if (sectionHeading) { section = { name: sectionHeading[1], items: [] }; entry.sections.push(section); continue; }
    if (!section) { entry.note.push(line.trim()); continue; }
    if (/^[-*]\s/.test(line)) { section.items.push(line.slice(2).trim()); } else if (/^\s/.test(line) && section.items.length) { section.items[section.items.length - 1] += ' ' + line.trim(); }
  }
  return entries.map(e => ({ ...e, note: e.note.join(' '), unreleased: /^unreleased$/i.test(e.version) }));
}
const compare = (a, b) => { const x = a.split('.').map(Number), y = b.split('.').map(Number); for (let i = 0; i < 3; i += 1) { if (x[i] !== y[i]) { return x[i] - y[i]; } } return 0; };
const isVersion = v => /^\d+\.\d+\.\d+$/.test(v || '');

async function loadChangelog() {
  const source = flag('--changelog');
  if (source && /^https?:/i.test(source)) {
    const response = await fetch(source);
    if (!response.ok) { throw new Error(`${source}: ${response.status}`); }
    return { text: await response.text(), from: source };
  }
  const file = source ? path.resolve(source) : path.join(__dirname, '..', 'CHANGELOG.md');
  return { text: fs.readFileSync(file, 'utf8'), from: file };
}

// ---- the copy of the app
function walk(dir, found) {
  for (const item of fs.readdirSync(dir, { withFileTypes: true })) {
    if (item.isDirectory()) { if (!SKIP.has(item.name)) { walk(path.join(dir, item.name), found); } continue; }
    if (NAMES.has(item.name)) { found.push(path.join(dir, item.name)); }
  }
}
const sha = text => crypto.createHash('sha256').update(text, 'utf8').digest('hex');

function describe(file) {
  const text = fs.readFileSync(file, 'utf8');
  const name = path.basename(file);
  let version = null, note = null;
  if (name === 'matui.css') {
    const first = text.split('\n')[0];
    const m = /^\/\*\s*MatUi\s+(\d+\.\d+\.\d+)\s+\(([0-9a-f]+)\)/.exec(first);
    if (m) { version = m[1]; } else if (/^\/\*\s*MatUi\s+\S+\s+\([0-9a-f]+\)/.test(first)) { note = 'from before the toolkit had a version: ' + first.slice(0, 80); }
  } else if (name === 'matui.js') {
    const m = /Version (\d+\.\d+\.\d+) of the toolkit MatUi/.exec(text.slice(0, 600)) || /version: '(\d+\.\d+\.\d+)'/.exec(text);
    if (m) { version = m[1]; } else if (/window\.MatUi\s*=/.test(text)) { note = 'from before the toolkit had a version'; }
  } else if (name === 'matui.boot.js') {
    const m = /MatUi boot (\d+\.\d+\.\d+):/.exec(text.slice(0, 200));
    if (m) { version = m[1]; } else { note = 'from before the toolkit had a version'; }
  } else if (name === 'matui.manifest.json') {
    try { version = JSON.parse(text).version; } catch (e) { note = 'not readable'; }
  }
  return { file, name, text, version: isVersion(version) ? version : null, note };
}

(async () => {
  const root = path.resolve(folder);
  if (!fs.existsSync(root) || !fs.statSync(root).isDirectory()) { console.error(`${root} is not a folder.`); process.exit(64); }
  const paths = [];
  walk(root, paths);
  const files = paths.map(describe);
  const result = { folder: root, copies: [], problems: [], status: 'none' };

  // group the files by the folder they are in: that is a copy
  const groups = new Map();
  for (const f of files) { const key = path.dirname(f.file); (groups.get(key) || groups.set(key, []).get(key)).push(f); }
  const { text: changelogText, from } = await loadChangelog().catch(e => { console.error('The changelog could not be read: ' + e.message); process.exit(64); });
  const releases = parseChangelog(changelogText).filter(e => !e.unreleased && isVersion(e.version)).sort((a, b) => compare(b.version, a.version));
  const newest = releases.length ? releases[0].version : null;
  let worst = 0;

  for (const [dir, group] of groups) {
    if (!group.some(f => f.name === 'matui.css' || f.name === 'matui.js')) { continue; }
    const copy = { dir: path.relative(root, dir) || '.', files: group.map(f => ({ name: f.name, version: f.version, note: f.note })), version: null, newest, modified: [], newer: [], breaking: false, migration: [], problems: [] };
    const versions = [...new Set(group.filter(f => f.version).map(f => f.version))];
    if (versions.length === 0) {
      copy.problems.push('no version in these files: the copy is from before the toolkit had a version. Update it by copying all the files again.');
      worst = Math.max(worst, newest ? 1 : 0);
    } else if (versions.length > 1) {
      copy.problems.push(`mixed versions (${group.map(f => `${f.name} ${f.version || '?'}`).join(', ')}): the files of one version belong together. Copy all the files of one version again.`);
      worst = 2;
    }
    copy.version = versions.length === 1 ? versions[0] : null;

    // changed by hand: against the manifest that came with the archive
    const manifest = group.find(f => f.name === 'matui.manifest.json');
    if (manifest) {
      try {
        const listed = JSON.parse(manifest.text).files || [];
        for (const entry of listed) {
          const target = path.join(dir, entry.name);
          if (!fs.existsSync(target)) { copy.problems.push(`${entry.name} is in the manifest and not in the folder.`); continue; }
          // the same file with the line ends of the app's repository (git on Windows) or a byte order mark is not a change
          const content = fs.readFileSync(target, 'utf8');
          if (sha(content) !== entry.sha256 && sha(content.replace(/^﻿/, '').replace(/\r\n/g, '\n')) !== entry.sha256) { copy.modified.push(entry.name); }
        }
      } catch (e) { copy.problems.push('matui.manifest.json is not readable.'); }
      if (copy.modified.length) { copy.problems.push(`changed since it was copied: ${copy.modified.join(', ')}. An update overwrites them: move what is needed to the app's own files first.`); worst = 2; }
    } else {
      copy.problems.push('no matui.manifest.json: it cannot be told whether a file was changed by hand (the archive of the toolkit holds one).');
    }

    if (copy.version && newest) {
      copy.newer = releases.filter(r => compare(r.version, copy.version) > 0).sort((a, b) => compare(a.version, b.version)).map(r => ({
        version: r.version, date: r.date,
        breaking: r.sections.some(s => /^breaking$/i.test(s.name) && s.items.length > 0),
        breakingItems: r.sections.filter(s => /^breaking$/i.test(s.name)).flatMap(s => s.items),
        migration: r.sections.filter(s => /^migration$/i.test(s.name)).flatMap(s => s.items),
      }));
      copy.breaking = copy.newer.some(r => r.breaking);
      if (copy.newer.length) { worst = Math.max(worst, copy.breaking ? 2 : 1); }
      if (compare(copy.version, newest) > 0) { copy.problems.push(`this copy (${copy.version}) is newer than the changelog (${newest}): is the changelog the right one?`); }
    }
    result.copies.push(copy);
  }

  if (result.copies.length === 0) { result.problems.push('no copy of MatUi found (matui.css or matui.js).'); worst = 64; }
  result.status = worst === 0 ? 'current' : worst === 1 ? 'update available' : worst === 2 ? 'update needs changes, or the copy is mixed or changed' : 'nothing found';
  result.changelog = from;

  if (json) { console.log(JSON.stringify(result, null, 2)); process.exit(worst === 64 ? 64 : worst); }
  console.log(`MatUi in ${root}`);
  console.log(`changelog: ${from}${newest ? `  (newest version ${newest})` : ''}`);
  for (const copy of result.copies) {
    console.log(`\n${copy.dir}:  ${copy.version ? 'version ' + copy.version : 'no version'}`);
    for (const f of copy.files) { console.log(`  ${f.name.padEnd(22)} ${f.version || f.note || '-'}`); }
    if (copy.version && newest && copy.newer.length === 0 && copy.modified.length === 0 && copy.problems.length === 0) { console.log('  up to date.'); }
    for (const r of copy.newer) {
      console.log(`  ${r.version}${r.date ? ' (' + r.date + ')' : ''}${r.breaking ? '  BREAKING' : ''}`);
      for (const b of r.breakingItems) { console.log(`      breaks: ${b}`); }
      for (const m of r.migration) { console.log(`      do:     ${m}`); }
    }
    if (copy.newer.length) { console.log(`  ${copy.newer.length} newer version${copy.newer.length === 1 ? '' : 's'}: copy ALL the files of ${newest} again${copy.breaking ? ' and do the steps above' : ''}.`); }
    for (const p of copy.problems) { console.log('  ! ' + p); }
  }
  for (const p of result.problems) { console.log('! ' + p); }
  console.log(`\n${result.status}.`);
  process.exit(worst === 64 ? 64 : worst);
})();
