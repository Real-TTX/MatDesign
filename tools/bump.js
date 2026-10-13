#!/usr/bin/env node
// Makes the next version of the toolkit MatUi (semantic versioning, docs/VERSIONING.md):
//   node tools/bump.js <patch|minor|major|x.y.z> [--date 2026-10-12] [--dry] [--root <repository>]
// It turns "Unreleased" of CHANGELOG.md into the new version (with the date of today) and leaves an empty "Unreleased" above it, and writes the new number where it
// is kept: MatUiVersion in Directory.Build.props, `Version x of the toolkit MatUi` and `version: 'x'` in matui.js, `MatUi boot x:` in matui.boot.js.
// A unit test holds all of them to each other and the newest version of the changelog to MatUiVersion, so a number that is forgotten fails the build.
// It refuses when Unreleased says nothing, when a "Breaking" section has no "Migration", and when the new version is not higher than the current one.
const fs = require('fs');
const path = require('path');

const args = process.argv.slice(2);
const flag = name => { const i = args.indexOf(name); return i < 0 ? null : (args[i + 1] ?? true); };
const dry = args.includes('--dry');
const root = path.resolve(flag('--root') || path.join(__dirname, '..'));
const kind = args.find((a, i) => !a.startsWith('--') && !(args[i - 1] || '').startsWith('--'));
const date = flag('--date') || new Date().toISOString().slice(0, 10);

function fail(message) { console.error('bump: ' + message); process.exit(1); }
if (!kind) { fail('say what to bump: patch, minor, major or a version like 0.2.0.'); }
if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) { fail('the date is YYYY-MM-DD.'); }

const files = {
  props: path.join(root, 'Directory.Build.props'),
  changelog: path.join(root, 'CHANGELOG.md'),
  script: path.join(root, 'src', 'MatUi', 'wwwroot', 'js', 'matui.js'),
  boot: path.join(root, 'src', 'MatUi', 'wwwroot', 'js', 'matui.boot.js'),
};
for (const [name, file] of Object.entries(files)) { if (!fs.existsSync(file)) { fail(`${name} not found: ${file}`); } }

const read = file => fs.readFileSync(file, 'utf8');
const eolOf = text => (text.includes('\r\n') ? '\r\n' : '\n');

// ---- the current version, and the next
const props = read(files.props);
const found = /<MatUiVersion>(\d+\.\d+\.\d+)<\/MatUiVersion>/.exec(props);
if (!found) { fail('MatUiVersion is not in Directory.Build.props.'); }
const current = found[1];
const parts = current.split('.').map(Number);
const next = /^\d+\.\d+\.\d+$/.test(kind) ? kind
  : kind === 'major' ? `${parts[0] + 1}.0.0`
    : kind === 'minor' ? `${parts[0]}.${parts[1] + 1}.0`
      : kind === 'patch' ? `${parts[0]}.${parts[1]}.${parts[2] + 1}` : null;
if (!next) { fail(`'${kind}' is not patch, minor, major or a version of three numbers.`); }
const compare = (a, b) => { const x = a.split('.').map(Number), y = b.split('.').map(Number); for (let i = 0; i < 3; i += 1) { if (x[i] !== y[i]) { return x[i] - y[i]; } } return 0; };
if (compare(next, current) <= 0) { fail(`${next} is not higher than the current version ${current}.`); }

// ---- the changelog: what is under Unreleased becomes the new version
const log = read(files.changelog);
const eol = eolOf(log);
const lines = log.replace(/\r\n/g, '\n').split('\n');
const start = lines.findIndex(l => /^##\s+\[Unreleased\]\s*$/i.test(l));
if (start < 0) { fail('CHANGELOG.md has no "## [Unreleased]" entry.'); }
let end = lines.findIndex((l, i) => i > start && /^##\s+\[/.test(l));
if (end < 0) { end = lines.length; }
const body = lines.slice(start + 1, end);
const bullets = body.filter(l => /^[-*]\s+\S/.test(l)).length;
if (bullets === 0) { fail('Unreleased says nothing: write down what changed first (one bullet per change under Added, Changed, Fixed ...).'); }
const section = name => { const i = body.findIndex(l => new RegExp(`^###\\s+${name}\\s*$`, 'i').test(l)); if (i < 0) { return []; } const out = []; for (let j = i + 1; j < body.length && !/^###\s/.test(body[j]); j += 1) { if (/^[-*]\s+\S/.test(body[j])) { out.push(body[j]); } } return out; };
if (section('Breaking').length > 0 && section('Migration').length === 0) { fail('Unreleased has a Breaking section and no Migration: tell an app what to do.'); }
// what the numbers promise: after 1.0 a break is a new major, before 1.0 a break is at least a new minor; something new is at least a minor
const nextParts = next.split('.').map(Number);
if (section('Breaking').length > 0) {
  if (parts[0] > 0 && nextParts[0] === parts[0]) { fail('Unreleased breaks something: after 1.0 that is a major version (node tools/bump.js major).'); }
  if (parts[0] === 0 && nextParts[0] === 0 && nextParts[1] === parts[1]) { fail('Unreleased breaks something: before 1.0 that is at least a new minor version (node tools/bump.js minor).'); }
}

if (section('Added').length > 0 && nextParts[0] === parts[0] && nextParts[1] === parts[1]) { console.warn('bump: Unreleased adds something and the version is only a patch: a new minor version would say so.'); }

const released = ['## [Unreleased]', '', `## [${next}] - ${date}`, ...body.filter((l, i) => !(i === 0 && l === '')), ''];
// the old heading and its body are replaced by the empty Unreleased and the new version
const rebuilt = [...lines.slice(0, start), ...released, ...lines.slice(end)];
const newLog = rebuilt.join('\n').replace(/\n{3,}/g, '\n\n').replace(/\n*$/, '\n').replace(/\n/g, eol);

// ---- the three places in the sources
function change(file, text, pattern, replacement, what) {
  if (!pattern.test(text)) { fail(`${what} was not found in ${path.relative(root, file)}.`); }
  return text.replace(pattern, replacement);
}
const scriptText = read(files.script);
const bootText = read(files.boot);
const newProps = change(files.props, props, /<MatUiVersion>\d+\.\d+\.\d+<\/MatUiVersion>/, `<MatUiVersion>${next}</MatUiVersion>`, 'MatUiVersion');
let newScript = change(files.script, scriptText, /Version \d+\.\d+\.\d+ of the toolkit MatUi/, `Version ${next} of the toolkit MatUi`, '"Version x of the toolkit MatUi"');
newScript = change(files.script, newScript, /version: '\d+\.\d+\.\d+'/, `version: '${next}'`, "version: 'x'");
const newBoot = change(files.boot, bootText, /MatUi boot \d+\.\d+\.\d+:/, `MatUi boot ${next}:`, '"MatUi boot x:"');

console.log(`MatUi ${current} -> ${next}  (${date}, ${bullets} change${bullets === 1 ? '' : 's'}${section('Breaking').length ? ', BREAKING' : ''})`);
if (dry) { console.log('--dry: nothing was written.'); process.exit(0); }
fs.writeFileSync(files.props, newProps);
fs.writeFileSync(files.changelog, newLog);
fs.writeFileSync(files.script, newScript);
fs.writeFileSync(files.boot, newBoot);
console.log('Written: Directory.Build.props, CHANGELOG.md, matui.js, matui.boot.js.');
console.log('Next:   dotnet test       (holds the numbers to each other)');
console.log(`        git commit -am "MatUi ${next}" && git tag matui-v${next}`);
console.log('        then the release in the Studio (Studio > Releases) takes this version over, and apps update with docs/VERSIONING.md.');
