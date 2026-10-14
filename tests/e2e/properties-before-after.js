// Proof that the component properties (SPACE-06) change nothing for an app that sets none: the computed style of every element with a toolkit class, on the pages
// of the site, is dumped with the CSS of the toolkit as it was BEFORE the properties (taken from git and served in place of /ui/matui.css) and with the CSS of the
// running site, and the two dumps are compared. Used once when the properties were made, and to be run again when a rule that uses a property is changed.
//   cd tests/e2e && node properties-before-after.js [http://localhost:8060] [git ref of the CSS before, default f2c4841]
// The check in run.js that stays (it needs no git): every property set to its documented default gives the same computed styles as none set.
const { chromium } = require('playwright-core');
const { execFileSync } = require('child_process');

const BASE = (process.argv[2] || process.env.BASE || 'http://localhost:8060').replace(/\/$/, '');
const REF = process.argv[3] || 'f2c4841';
const PARTS = ['tokens', 'base', 'shell', 'controls', 'forms', 'pickers', 'lists', 'dialogs', 'layout', 'apps'].map(p => `matui.${p}.css`);

// The properties that a rule with a component property sets (every corner, side and axis), and the size the element takes up.
const STYLE = ['borderTopLeftRadius', 'borderTopRightRadius', 'borderBottomLeftRadius', 'borderBottomRightRadius', 'minWidth', 'minHeight', 'width', 'height',
  'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft', 'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth', 'borderTopStyle', 'borderBottomStyle',
  'rowGap', 'columnGap', 'fontSize', 'fontWeight', 'top', 'left', 'transform', 'marginBottom', 'marginLeft', 'maxWidth', 'display'];

function oldBundle() {
  return PARTS.map(p => execFileSync('git', ['show', `${REF}:src/MatUi/wwwroot/css/${p}`], { encoding: 'utf8', maxBuffer: 1 << 26 })).join('\n');
}

// One line per element: where it is, its class and the values; the pseudo-element of the switch knob as well.
function dump(page) {
  return page.evaluate((props) => {
    const lines = {};
    const pick = (cs) => props.map(p => cs[p]).join(',');
    let i = 0;
    for (const el of document.querySelectorAll('body *')) {
      const cls = typeof el.className === 'string' ? el.className : '';
      if (!/\bmat-/.test(cls) && !el.closest('.doc-example__live')) { continue; }
      if (el.closest('.mat-spinner, .mat-skeleton, .mat-toast, .mat-meter--indeterminate')) { continue; } // they move
      i += 1;
      const r = el.getBoundingClientRect();
      const key = `${i} ${el.tagName.toLowerCase()}.${cls.replace(/\s+/g, '.')}`;
      lines[key] = pick(getComputedStyle(el)) + ` | ${r.width.toFixed(2)}x${r.height.toFixed(2)}`;
      if (el.classList.contains('mat-switch__track')) { lines[key + '::after'] = pick(getComputedStyle(el, '::after')); }
    }
    return lines;
  }, STYLE);
}

async function load(browser, options, url, scheme, css) {
  const ctx = await browser.newContext({ ...options, colorScheme: scheme });
  const page = await ctx.newPage();
  if (css) { await page.route('**/ui/matui.css*', route => route.fulfill({ status: 200, contentType: 'text/css', body: css })); }
  await page.goto(BASE + url, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => document.readyState === 'complete');
  await page.waitForTimeout(500);
  return { ctx, page };
}

(async () => {
  const before = oldBundle();
  // the two sides must really differ: the old CSS has no property, the running site's has
  const live = await (await fetch(BASE + '/ui/matui.css')).text();
  if (before.includes('@prop') || !live.includes('@prop')) { throw new Error(`the baseline ${REF} must be from before the component properties and the site must have them`); }
  console.log(`baseline ${REF}: ${before.length} characters of CSS without properties; running site: ${live.length} with ${(live.match(/@prop /g) || []).length} declarations`);
  const browser = await chromium.launch({ channel: 'msedge', headless: true }).catch(() => chromium.launch({ channel: 'chrome', headless: true }));
  const desktop = { viewport: { width: 1360, height: 900 } };
  const phone = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true };
  const scenarios = [
    ['/Components', desktop, 'light'], ['/Components', desktop, 'dark'], ['/Components', phone, 'light'],
    ['/Preview', desktop, 'light'], ['/Preview', phone, 'light'], ['/Foundations/Tokens', desktop, 'light'], ['/Identity', phone, 'light'],
  ];
  let bad = 0;
  let total = 0;
  for (const [url, options, scheme] of scenarios) {
    const results = [];
    for (const css of [before, null]) {
      const { ctx, page } = await load(browser, options, url, scheme, css);
      if (url === '/Components') {
        // a dialog and a toast are built by the script: open both, let them settle, and take them too
        await page.evaluate(() => { window.MatUi.alert('A notice'); window.MatUi.toast('Saved', { tone: 'success' }); });
        await page.waitForTimeout(700);
      }
      results.push(await dump(page));
      await ctx.close();
    }
    const [a, b] = results;
    const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
    const diffs = [...keys].filter(k => a[k] !== b[k]);
    total += keys.size;
    bad += diffs.length;
    const label = `${url} ${options.isMobile ? 'phone' : 'desktop'} ${scheme}`;
    console.log(`${diffs.length === 0 ? 'identical' : 'DIFFERENT'}  ${label}: ${keys.size} elements`);
    for (const k of diffs.slice(0, 8)) { console.log(`    ${k}\n      before ${a[k]}\n      after  ${b[k]}`); }
  }
  await browser.close();
  console.log(`\n${total} elements compared, ${bad} differ.`);
  process.exit(bad === 0 ? 0 : 1);
})().catch(e => { console.error('FAILED to run:', e.message); process.exit(2); });
