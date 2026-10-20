// Reads the rules out of src/MatDesign.Core/RuleBook.cs as text (no .NET needed) and hashes them the way RuleChanges.HashOf does.
// Shared by rule-hash.js (the hash and the line for the changelog of the rulebook) and rule-history.js (the first date of a rule, from git).
// It understands what RuleBook.cs is written in: `new("ID-NN", "AREA", "Title", "Statement", RuleLevel.Must, Owner, Why: "...", Do: "...", Dont: "...", Check: "...")`, regular
// string literals with the escapes \" \\ \n and `"a" + "b"` joined over lines. A test (RuleChangeToolTests) holds its hashes to the C# ones, so a form it cannot read fails the build.
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const ruleBookFile = path.join(__dirname, '..', 'src', 'MatDesign.Core', 'RuleBook.cs');

/** The text of the file that holds the rules, or the text that is given. */
function readRuleBook(file = ruleBookFile) { return fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n'); }

function unescape(body) {
  return body.replace(/\\(["\\n])/g, (_, c) => (c === 'n' ? '\n' : c));
}

/** The constants of RuleBook (`public const string Owner = "Owner";`): the names that stand for a source. */
function constants(text) {
  const result = {};
  for (const m of text.matchAll(/public const string (\w+) = "((?:[^"\\]|\\.)*)";/g)) { result[m[1]] = unescape(m[2]); }
  return result;
}

/** Parses every `new("AREA-NN", ...)` of the file. */
function parseRules(text) {
  const names = constants(text);
  const rules = [];
  const start = /^\s*new\("([A-Z][A-Z0-9]{1,7}-\d{2,3})",/gm;
  let found;
  while ((found = start.exec(text)) !== null) {
    let i = found.index + found[0].indexOf('new(') + 4;
    const args = [];          // positional values
    const named = {};         // Why, Do, Dont, Check
    let label = null;
    let value = null;         // the string or name being read; null when nothing yet
    const push = () => {
      if (value === null) { return; }
      if (label) { named[label] = value; } else { args.push(value); }
      label = null;
      value = null;
    };
    for (; i < text.length; i++) {
      const c = text[i];
      if (c === '"') {
        let j = i + 1;
        while (text[j] !== '"') { j += text[j] === '\\' ? 2 : 1; }
        value = (value ?? '') + unescape(text.slice(i + 1, j));
        i = j;
      } else if (c === ')') {
        push();
        break;
      } else if (c === ',') {
        push();
      } else if (c === '/' && text[i + 1] === '/') {
        while (i < text.length && text[i] !== '\n') { i++; }
      } else if (/[A-Za-z_]/.test(c)) {
        let j = i;
        while (/[\w.]/.test(text[j])) { j++; }
        const word = text.slice(i, j);
        i = j - 1;
        if (text[j] === ':' && text[j + 1] !== ':') { label = word; i = j; } else { value = word; }
      } else if (c === '+' || /\s/.test(c)) {
        // joins two string literals, or white space
      } else {
        throw new Error(`RuleBook.cs: cannot read '${c}' in ${found[1]}`);
      }
    }
    const [id, area, title, statement, level, source] = args;
    rules.push({
      id, area, title, statement,
      level: level.replace(/^RuleLevel\./, ''),
      source: names[source] ?? source,
      why: named.Why ?? null, do: named.Do ?? null, dont: named.Dont ?? null, check: named.Check ?? 'Review',
    });
  }
  return rules;
}

/** The short hash of what makes a rule a different rule: its statement, its level and its source (the same as RuleChanges.HashOf in C#). */
function hashOf(rule) {
  return crypto.createHash('sha256').update(`${rule.statement}\n${rule.level}\n${rule.source}`, 'utf8').digest('hex').slice(0, 8);
}

module.exports = { readRuleBook, parseRules, hashOf, ruleBookFile };
