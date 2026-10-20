// Touch audit: is every interactive element of the site a target of at least 44 px on a touch screen (MOBILE-04)?
//   cd tests/e2e && node touch-audit.js [http://localhost:8060]        TOUCH_PAGES=/Layout,/Utilities node touch-audit.js   (only these pages)
// It opens the pages in a touch context (isMobile, hasTouch: 390 and 768 px wide, so `pointer: coarse` is on), collects every visible interactive element and measures
// the EFFECTIVE hit rectangle, not the drawn mark (touch-hit.js: the element is scrolled into view and the script walks outwards from a point that reaches it, a pixel at a
// time, as long as `document.elementFromPoint` still names the element, its label or something inside them; a padding on a label, a ::before that reaches beyond the mark and
// a stretched link all count). Every element whose rectangle is under 44 px in width or height is a finding, except the exceptions of ALLOW below, each with its reason.
// It also opens what is not on the page at first (the drawer of the menu, dialogs, context menus, the panels of the pickers) and measures that. Exit code 1 on a finding.
const { chromium } = require('playwright-core');
const { installTouchHit } = require('./touch-hit');

const BASE = (process.argv[2] || process.env.BASE || 'http://localhost:8060').replace(/\/$/, '');
const MIN = 44;

// What is not counted, and why. The tests run in the page (`exceptionOf` below); only the reasons are here. Keep this short: a control that is merely small is fixed, not listed.
const ALLOW = {
  'inline-link': 'A link inside a sentence (or the name of a row that goes on after it) is as tall as the line of text around it and as long as its text. WCAG 2.5.8 exempts it and a paragraph cannot have 44 px lines; a link that stands alone in its block is a target and is measured.',
  'compact': 'A control of a compact variant (--sm, --compact) is the 36 px that MOBILE-04 allows it; below 36 px it is a finding like any other.',
  'otp-shrink': 'A code with more boxes than fit in a row at 44 px (eight or more on a phone, FORM-19) shrinks its boxes instead of overflowing; they stay 44 px high and are typed through (one Tab stop, typing and pasting move on by themselves).',
  'preview': 'The sample in the brand editor of the Studio is a picture of the brand being edited: it shows the values as typed (a control height of 40 px, which a touch screen never applies) and nothing in it is a control for its own sake.',
  'grid-fit': 'The day of a calendar or a schedule in a box that is narrower than seven times 44 px (a calendar with week numbers on a phone, two months side by side, a calendar in a framed example): seven columns share the box, the days are 44 px high and never wider than their column. In an app a month fits a phone (47 px).',
};

// The pages of the site without an administrator, and the views of the sample app.
const PAGES = ['/', '/Rules', '/Identity', '/Foundations', '/Shell', '/Components', '/Components/All', '/Components/buttons', '/Components/tree', '/Layout', '/Utilities', '/Patterns', '/Builder', '/Download',
  '/Preview?app=matmail&view=list', '/Preview?app=matmail&view=gallery', '/Preview?app=matmail&view=form', '/Preview?app=matmail&view=dashboard',
  '/Preview?app=matmail&view=settings', '/Preview?app=matmail&view=auth'];

// What is measured. An invisible input (a switch, a radio of a rating) stands for its label.
// A row of a tree is the target (the script takes the tap on the row), not its label link or the list item that holds its children.
const CANDIDATES = 'a[href]:not(.mat-tree__label), button, input:not([type="hidden"]), select, textarea, summary, [role="button"], [role="tab"], [role="switch"], [role="checkbox"], [role="radio"], '
  + '[role="menuitem"], [role="menuitemcheckbox"], [role="menuitemradio"], [role="option"], [role="slider"], [role="link"], [tabindex]:not([tabindex^="-"]):not([role="treeitem"]), '
  + '.mat-tree__row, .mat-tree__toggle, .mat-slider__thumb, .mat-rating.is-ready:not(.mat-rating--readonly) .mat-rating__layer--empty .mat-rating__cell';

// The compact variants that the rule allows 36 px (MOBILE-04): the explicit --sm / --compact classes of the toolkit.
const COMPACT_MIN = 36;
const COMPACT = '.mat-btn--sm, .mat-input-group--sm, .mat-input--sm, .mat-select--sm, .mat-otp--sm, .mat-rating--sm, .mat-slider--sm, .mat-picker--sm, .mat-tree--compact, .mat-itemlist--compact, .mat-upload--compact, .mat-table--compact';

// Runs in the page: the measure of every candidate that is on show now and has not been measured before on this page.
function measure(args) {
  const { candidates, min } = args;
  const results = [];
  const seen = (window.__touchSeen = window.__touchSeen || new WeakSet());
  const path = (el) => {
    const cls = String(el.getAttribute('class') || '').split(/\s+/).filter(Boolean).slice(0, 2).map(c => '.' + c).join('');
    const type = el.tagName === 'INPUT' ? '[' + (el.getAttribute('type') || 'text') + ']' : '';
    const role = el.getAttribute('role') ? '[role=' + el.getAttribute('role') + ']' : '';
    return el.tagName.toLowerCase() + cls + type + role;
  };
  const exceptionOf = (el, hit) => {
    // a link that has other text beside it in the same block: in a sentence, or the name of a row that goes on (app + .example.org)
    if (el.tagName === 'A' && getComputedStyle(el).display === 'inline' && !el.classList.contains('mat-btn')) {
      const rest = (el.parentElement.textContent || '').replace(el.textContent, '').trim();
      if (rest) { return 'inline-link'; }
    }
    // the days of a month grid that has to fit the box it is in (a calendar or a schedule in a narrow column): 44 px high, as wide as seven columns of the box allow
    if (el.matches('.mat-cal__day, .mat-schedule__daybtn') && hit.h + .5 >= 44) { return 'grid-fit'; }
    if (el.classList.contains('mat-otp__box') && hit.h + .5 >= 44) { return 'otp-shrink'; }
    if (el.closest('[data-brand-preview]')) { return 'preview'; }
    return '';
  };
  // while a drawer or a dialog is open only what is in it is measured (the page behind it is covered, and was measured before)
  let scopes = args.scope ? [...document.querySelectorAll(args.scope)].filter(e => e.checkVisibility()) : [];
  if (!scopes.length) { scopes = [document]; }
  for (const el of scopes.flatMap(s => [...s.querySelectorAll(candidates)])) {
    if (seen.has(el)) { continue; }
    const hit = window.__touchHit(el, min);
    if (!hit) { continue; }
    seen.add(el);
    seen.add(hit.el);
    if (hit.w + .5 >= min && hit.h + .5 >= min && !hit.covered) { continue; }
    // a compact variant is measured against the compact floor of the rule; one that has it is listed as an exception of its own kind
    const compactEl = hit.el.closest(args.compact) || (hit.el.closest('.mat-stepper') && hit.el.closest('.mat-stepper').querySelector(':scope > .mat-input--sm'));
    const rating =hit.el.classList.contains('mat-rating__cell') ? ' (in ' + String(hit.el.closest('.mat-rating').className).split(/\s+/).filter(c => /^mat-rating--/.test(c)).join('.') + ')' : '';
    results.push({
      path: path(hit.el) + rating, w: Math.round(hit.w), h: Math.round(hit.h), covered: hit.covered || '',
      text: (hit.el.textContent || hit.el.getAttribute('aria-label') || hit.el.getAttribute('title') || '').trim().replace(/\s+/g, ' ').slice(0, 30),
      exception: exceptionOf(hit.el, hit) || (compactEl && hit.w + .5 >= args.compactMin && hit.h + .5 >= args.compactMin && !hit.covered ? 'compact' : ''),
    });
  }
  return results;
}

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true }).catch(() => chromium.launch({ channel: 'chrome', headless: true }));
  const findings = new Map();   // signature -> { count, pages, text }
  const allowed = new Map();
  let measured = 0;

  function record(pageName, width, results) {
    for (const r of results) {
      const sig = `${r.path} ${r.w}x${r.h}${r.covered ? ' covered by ' + r.covered : ''}`;
      const bucket = r.exception ? allowed : findings;
      const key = r.exception ? r.exception + ': ' + sig : sig;
      const e = bucket.get(key) || { count: 0, pages: new Set(), text: r.text };
      e.count++; e.pages.add(`${pageName}@${width}`);
      bucket.set(key, e);
    }
  }
  const measureNow = async (page, scope) => {
    await page.evaluate(installTouchHit);
    return page.evaluate(measure, { candidates: CANDIDATES, min: MIN, compact: COMPACT, compactMin: COMPACT_MIN, scope });
  };

  async function auditPage(page, name, width) {
    record(name, width, await measureNow(page));
    // what is not there at first: the drawer of the menu, dialogs, context menus, the panels of the pickers
    const openers = await page.evaluate(() => {
      const out = [];
      const add = (sel, kind) => document.querySelectorAll(sel).forEach((el, i) => { if (el.checkVisibility() && !el.closest('dialog:not([open])')) { out.push({ kind, sel, i }); } });
      add('[data-nav-toggle], .mat-topbar__burger, [data-drawer-toggle]', 'drawer');
      add('[data-dialog-open]', 'dialog');
      add('[data-menu-toggle]', 'menu');
      add('.mat-picker__trigger', 'picker');
      add('[data-switcher], .mat-switcher', 'switcher');
      return out;
    });
    const done = new Set();
    for (const o of openers.slice(0, 60)) {
      const key = await page.evaluate(({ sel, i }) => { const el = document.querySelectorAll(sel)[i]; return el ? (el.getAttribute('data-dialog-open') || el.getAttribute('aria-controls') || el.id || el.className) : ''; }, o);
      const sig = o.kind + ':' + key;
      if (done.has(sig)) { continue; }
      done.add(sig);
      try {
        await page.evaluate(({ sel, i }) => { document.querySelectorAll(sel)[i].scrollIntoView({ block: 'center' }); }, o);
        await page.locator(o.sel).nth(o.i).click({ timeout: 2000 });
        await page.waitForTimeout(350);
        record(name + ' (' + o.kind + ' ' + String(key).slice(0, 20) + ')', width, await measureNow(page, o.kind === 'drawer' ? '.mat-nav' : o.kind === 'dialog' ? 'dialog[open]' : o.kind === 'picker' ? '.mat-picker__panel, dialog[open]' : ''));
        measured++;
        await page.keyboard.press('Escape');
        await page.waitForTimeout(150);
        // a drawer or a panel that Escape did not close: the page is loaded again for the next one
        if (await page.evaluate(() => !!document.querySelector('dialog[open], .mat-menu.is-open, .mat-picker.is-open, .is-nav-open, .mat-nav.is-open'))) { await page.reload({ waitUntil: 'networkidle' }); }
      } catch (e) { /* not clickable at this size: nothing to measure */ }
    }
  }

  async function run(width, urls, storageState) {
    const ctx = await browser.newContext({ viewport: { width, height: 844 }, isMobile: true, hasTouch: true, storageState });
    const page = await ctx.newPage();
    for (const url of urls) {
      await page.goto(BASE + url, { waitUntil: 'networkidle' });
      await page.waitForFunction(() => document.readyState === 'complete');
      await page.waitForTimeout(400);
      if (!await page.evaluate(() => matchMedia('(pointer: coarse)').matches)) { throw new Error('the context is not a touch screen: (pointer: coarse) is off'); }
      const t0 = Date.now();
      await auditPage(page, url, width);
      measured++;
      console.log(`  ${width} ${url} (${Date.now() - t0} ms)`);
    }
    await ctx.close();
  }

  // the Studio, when the site has one: the administrator is created the way run.js does it, and the links of the Studio are followed two steps from its start page
  async function studioUrls() {
    const name = process.env.STUDIO_NAME || 'admin';
    const password = process.env.STUDIO_PASSWORD || 'studio-dev';
    const ctx = await browser.newContext({ viewport: { width: 1360, height: 900 } });
    const page = await ctx.newPage();
    await page.goto(BASE + '/Studio/Login', { waitUntil: 'networkidle' });
    if (page.url().includes('/Studio/Setup')) {
      await page.fill('#Company', process.env.STUDIO_COMPANY || 'Mat');
      await page.click('[data-setup-next]');
      await page.locator(`.mat-choice:has(input[value="${process.env.STUDIO_TEMPLATE || 'demo'}"])`).click();
      await page.click('[data-setup-next]');
      await page.fill('#Name', name); await page.fill('#Password', password); await page.fill('#Repeat', password);
      await page.click('.mat-auth__card button[type="submit"]');
      await page.waitForURL('**/Studio');
      await ctx.clearCookies();
      await page.goto(BASE + '/Studio/Login', { waitUntil: 'networkidle' });
    }
    if (await page.locator('input[type="password"]').count() === 0) { await ctx.close(); return { urls: [], state: null }; }
    await page.fill('#Name', name); await page.fill('#Password', password);
    await page.click('.mat-auth__card button[type="submit"]');
    await page.waitForURL('**/Studio**');
    const state = await ctx.storageState();
    // one page of each kind is enough: the edit pages of two apps are the same markup
    const kinds = new Map([['/Studio', '/Studio']]);
    const collect = () => page.evaluate(() => [...document.querySelectorAll('a[href^="/Studio"]')].map(a => a.getAttribute('href')).filter(h => !/Logout|Download|\.zip|delete|Restore/i.test(h)));
    for (let hop = 0; hop < 2; hop++) {
      for (const u of [...kinds.values()]) {
        await page.goto(BASE + u, { waitUntil: 'networkidle' });
        for (const h of await collect()) { const path = h.split('#')[0]; if (!kinds.has(path.split('?')[0])) { kinds.set(path.split('?')[0], path); } }
      }
    }
    await ctx.close();
    return { urls: [...kinds.values()].slice(0, 60), state };
  }

  console.log('Touch audit: ' + BASE + ' (minimum ' + MIN + ' px)');
  const only = process.env.TOUCH_PAGES ? process.env.TOUCH_PAGES.split(',') : null;
  for (const width of [390, 768]) { await run(width, only || PAGES); }
  const studio = only ? { urls: [], state: null } : await studioUrls();
  if (studio.state) {
    for (const width of [390, 768]) { await run(width, studio.urls, studio.state); }
    console.log('Studio pages audited: ' + studio.urls.length);
  } else {
    console.log('The Studio is off on this site, or pages were chosen: its pages are not audited.');
  }
  await browser.close();

  const show = (map) => [...map.entries()].sort((a, b) => b[1].count - a[1].count).forEach(([sig, e]) => {
    console.log(`  ${String(e.count).padStart(4)} x ${sig}   "${e.text}"   e.g. ${[...e.pages].slice(0, 3).join(', ')}`);
  });
  if (allowed.size) {
    console.log('\nExceptions (' + [...allowed.values()].reduce((s, e) => s + e.count, 0) + ' elements):');
    show(allowed);
    for (const id of new Set([...allowed.keys()].map(k => k.split(':')[0]))) { console.log(`  ${id}: ${ALLOW[id]}`); }
  }
  console.log('\n' + (findings.size ? 'Findings (hit area under ' + MIN + ' px):' : 'No element has a hit area under ' + MIN + ' px.'));
  show(findings);
  console.log(`\n${measured} page views and openings measured, ${findings.size} kinds of finding.`);
  process.exit(findings.size ? 1 : 0);
})().catch(e => { console.error('FAILED to run:', e.message); process.exit(2); });
