#!/usr/bin/env node
// When did each rule first appear in src/MatDesign.Core/RuleBook.cs? Read from the history of git, one version of the file at a time (needs a clone with its history).
//   node tools/rule-history.js            one line per rule of the rulebook: the line of the changelog of the rulebook (RuleChangeEntries.cs) that records when it was added
//   node tools/rule-history.js --first    id, date and commit of the first version that has the rule (a table, to look at)
//   node tools/rule-history.js --changes  also the dates on which the statement, the level or the source of a rule changed since then (a table, to look at: it is how the baseline was checked)
// The baseline of the changelog of the rulebook (a line `Add(date, id, hash)` for every rule that existed when the changelog was started) was made with this script;
// from then on a rule that changes gets its line from `node tools/rule-hash.js <id>`, and a unit test (RuleChangesTests) fails until it has one.
const { execFileSync } = require('child_process');
const path = require('path');
const { parseRules, hashOf, readRuleBook, ruleBookFile } = require('./rulebook-source');

const root = path.join(__dirname, '..');
const relative = path.relative(root, ruleBookFile).replace(/\\/g, '/');
const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });

// the commits that touched the file, oldest first, with the date of the author (YYYY-MM-DD)
const commits = git('log', '--reverse', '--format=%H %as', '--', relative)
  .split('\n').filter(Boolean).map(line => { const [hash, date] = line.split(' '); return { hash, date }; });
if (commits.length === 0) { console.error('rule-history: no history for ' + relative + ' (a shallow clone has none: git fetch --unshallow).'); process.exit(1); }

const first = {};     // id -> { date, commit }
const changes = {};   // id -> [ { date, commit } ]
const last = {};      // id -> hash at the previous version
for (const commit of commits) {
  let rules;
  try { rules = parseRules(git('show', `${commit.hash}:${relative}`).replace(/\r\n/g, '\n')); } catch { continue; }
  for (const rule of rules) {
    const hash = hashOf(rule);
    if (!first[rule.id]) { first[rule.id] = { date: commit.date, commit: commit.hash.slice(0, 7) }; changes[rule.id] = []; }
    else if (commit.date < first[rule.id].date) { first[rule.id] = { date: commit.date, commit: commit.hash.slice(0, 7) }; }
    else if (last[rule.id] !== hash) { changes[rule.id].push({ date: commit.date, commit: commit.hash.slice(0, 7) }); }
    last[rule.id] = hash;
  }
}

const now = parseRules(readRuleBook());
const mode = process.argv[2];
for (const rule of now) {
  const when = first[rule.id];
  if (!when) { console.error(`rule-history: ${rule.id} was not found in the history`); process.exit(1); }
  if (mode === '--first') { console.log(`${rule.id.padEnd(10)} ${when.date}  ${when.commit}`); }
  else if (mode === '--changes') { console.log(`${rule.id.padEnd(10)} ${when.date}  changed on: ${changes[rule.id].map(c => c.date).join(', ') || '-'}`); }
  else { console.log(`        Add("${when.date}", "${rule.id}", "${hashOf(rule)}"),`); }
}
