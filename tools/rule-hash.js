#!/usr/bin/env node
// The hash of a rule for the changelog of the rulebook (docs/VERSIONING.md, "The rulebook has a changelog too"):
//   node tools/rule-hash.js FORM-15                       the hash of the rule as it is now, and the line to add to src/MatDesign.Core/RuleChangeEntries.cs
//   node tools/rule-hash.js FORM-15 "The statement names the tags."    the same with the note filled in
//   node tools/rule-hash.js --all                         every rule and its hash (what RuleChangesTests compares with the C# hash)
//   node tools/rule-hash.js FORM-15 --kind Added          Added | Changed (the default) | Removed | Moved
// The hash is the first eight digits of the SHA-256 of `statement \n level \n source` (RuleChanges.HashOf in C# does the same: a test holds the two together). It is read from
// RuleBook.cs as text, so it needs no build. A rule changes the hash when its statement, its level or its source change; a new title, reason or example does not.
const { parseRules, hashOf, readRuleBook } = require('./rulebook-source');

const args = process.argv.slice(2);
const flag = name => { const i = args.indexOf(name); return i < 0 ? null : args[i + 1]; };
const kind = flag('--kind') || 'Changed';
const positional = args.filter((a, i) => !a.startsWith('--') && !(args[i - 1] || '').startsWith('--'));
const rules = parseRules(readRuleBook());

if (args.includes('--all')) {
  for (const rule of rules) { console.log(`${rule.id} ${hashOf(rule)}`); }
  process.exit(0);
}

const [id, note] = positional;
if (!id) { console.error('rule-hash: say which rule (node tools/rule-hash.js FORM-15) or use --all.'); process.exit(1); }
const rule = rules.find(r => r.id.toLowerCase() === id.toLowerCase());
if (!rule) { console.error(`rule-hash: there is no rule ${id} in RuleBook.cs.`); process.exit(1); }
if (!['Added', 'Changed', 'Removed', 'Moved'].includes(kind)) { console.error('rule-hash: --kind is Added, Changed, Removed or Moved.'); process.exit(1); }

const today = new Date().toISOString().slice(0, 10);
const helper = { Added: 'Add', Changed: 'Chg', Removed: 'Rem', Moved: 'Mov' }[kind];
console.log(`${rule.id}  ${rule.level}  ${rule.source}  hash ${hashOf(rule)}`);
console.log('Add this line at the end of RuleChangeEntries.cs (the list is oldest first):');
console.log(`        ${helper}("${today}", "${rule.id}", "${hashOf(rule)}", "${(note || 'Say in one short sentence what changed.').replace(/"/g, '\\"')}"),`);
