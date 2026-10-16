// On GitHub a failed check is also an error annotation of the run: anyone can read those without signing in, the log of a job needs a sign-in.
// Elsewhere it does nothing.
const path = require('path');

function annotate(script, text) {
  if (!process.env.GITHUB_ACTIONS) { return; }
  const message = String(text).replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
  console.log(`::error title=${path.basename(script)}::${message}`);
}

module.exports = { annotate };
