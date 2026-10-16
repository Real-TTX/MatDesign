// End-to-end checks of the white label build (docs/PRODUCT.md, stage 15; rule CODE-09) in a real browser against a running site.
//   cd tests/e2e && npm install && node white-label.js [http://localhost:8060]
// Part 1: the Studio page (/Studio/Releases/WhiteLabel): live validation, the table of before and after, the trial build, saving, a phone.
// Part 2: the behaviour of the renamed toolkit. The browser is put behind the renamer of the site (POST /Studio/Releases/WhiteLabel/rename, the function a release uses): every page, stylesheet, script and
//         sprite the browser asks for is renamed on the way, and the address of a file (acmeui.css) is mapped back to the file the site has (matui.css). Then the real pages are used: a dialog opens and closes,
//         a select picker opens and chooses, the theme button toggles, a tab switches, a toast shows, the number stepper steps, a tree, a rating and an upload work. No console error, no class, attribute,
//         id or storage key of the old prefix in the DOM.
// Part 3: the renamed toolkit looks the same: screenshots of the same places of the components page with the original files and with the renamed ones (light, dark, 390 px), compared pixel by pixel.
// The site has to have the Studio on with an administrator (STUDIO_NAME / STUDIO_PASSWORD, default admin / studio-dev; the setup assistant is used when there is none).
const { chromium } = require('playwright-core');
const crypto = require('crypto');
const { annotate } = require('./annotate');

const BASE = (process.argv[2] || process.env.BASE || 'http://localhost:8060').replace(/\/$/, '');
const PREFIX = 'acme';
const NAME = 'AcmeUi';
const STEM = NAME.toLowerCase();
const results = [];

async function check(name, fn) {
  try {
    await fn();
    results.push({ name, ok: true });
    console.log('  ok    ' + name);
  } catch (e) {
    results.push({ name, ok: false, why: e.message });
    console.log('  FAIL  ' + name + '\n        ' + e.message.split('\n')[0]);
    annotate(__filename, name + ': ' + e.message.split('\n')[0]);
  }
}
function expect(actual, expected, what) {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) { throw new Error(`${what}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`); }
}
function expectTrue(value, what) { if (!value) { throw new Error(what); } }
// a selector written with the names of the source, with the prefix of the copy
const R = text => text.replace(/mat-/g, PREFIX + '-');
// a click that sends a form: wait for the page that answers (the trial build takes a second, and the old page is quiet in the meantime)
const press = (p, selector) => Promise.all([p.waitForNavigation({ waitUntil: 'load' }), p.click(selector)]);

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true }).catch(() => chromium.launch({ channel: 'chrome', headless: true }));
  const consoleErrors = [];
  const failedRequests = [];
  const desktop = { viewport: { width: 1360, height: 900 }, reducedMotion: 'reduce' };
  const phone = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, reducedMotion: 'reduce' };
  const name = process.env.STUDIO_NAME || 'admin';
  const password = process.env.STUDIO_PASSWORD || 'studio-dev';

  function watch(page, label) {
    page.on('pageerror', e => consoleErrors.push(label + ': ' + e.message));
    page.on('console', m => { if (m.type() === 'error') { consoleErrors.push(label + ': ' + m.text()); } });
    page.on('response', r => { if (r.status() >= 400 && r.url().startsWith(BASE) && !r.url().includes('favicon')) { failedRequests.push(label + ': ' + r.status() + ' ' + r.url()); } });
  }

  console.log('White label build in a browser: ' + BASE);
  const admin = await browser.newContext(desktop);
  const page = await admin.newPage();
  watch(page, 'studio');
  await page.goto(BASE + '/Studio/Login', { waitUntil: 'networkidle' });
  if (page.url().includes('/Studio/Setup')) {
    // the assistant has three steps: the company, the template (the demo: these checks are about the Mat apps) and the administrator
    await page.fill('#Company', process.env.STUDIO_COMPANY || 'Mat');
    await page.click('[data-setup-next]');
    await page.locator(`.mat-choice:has(input[value="${process.env.STUDIO_TEMPLATE || 'demo'}"])`).click();
    await page.click('[data-setup-next]');
    await page.fill('#Name', name);
    await page.fill('#Password', password);
    await page.fill('#Repeat', password);
    await page.click('.mat-auth__card button[type="submit"]');
    await page.waitForURL('**/Studio');
  } else if (await page.locator('input[type="password"]').count() === 0) {
    console.log('  --    the Studio is off on this site (no data directory): the white label checks are skipped');
    await browser.close();
    process.exit(0);
  } else {
    await page.fill('#Name', name);
    await page.fill('#Password', password);
    await page.click('.mat-auth__card button[type="submit"]');
    await page.waitForURL('**/Studio**');
  }

  // ---------------------------------------------------------------------------------------------------- the page of the setting
  console.log('\nThe setting in the Studio');
  const field = id => page.locator('.mat-field:has(#' + id + ')');
  await page.goto(BASE + '/Studio/Releases/WhiteLabel', { waitUntil: 'networkidle' });
  // a setting of an earlier run is taken away, so that the page starts as it does for a company that has none
  if (await page.locator('button[formaction="?handler=Reset"]').count() > 0) {
    await page.click('button[formaction="?handler=Reset"]');
    await Promise.all([page.waitForNavigation({ waitUntil: 'load' }), page.click('dialog[open] button[value="ok"]')]);
  }
  await check('the page has the shell, one h1, the two fields, the table of examples and a place in the menu', async () => {
    expect(await page.locator('main h1').count(), 1, 'h1');
    expect(await page.textContent('main h1'), 'White label', 'the title');
    expect(await page.locator('a.mat-nav__item.is-active[href="/Studio/Releases/WhiteLabel"]').count(), 1, 'the menu item');
    expect(await page.locator('#Input_Prefix').inputValue(), 'mat', 'the prefix of the toolkit');
    expect(await page.locator('#Input_Name').inputValue(), 'MatUi', 'the name of the toolkit');
    expect(await page.locator('[data-wl-examples] tbody tr').count() >= 10, true, 'examples');
    expect(await page.locator('[style]').count(), 0, 'inline styles');
  });
  await check('what is typed is checked while it is typed: the reason is at the field and goes when the value is right', async () => {
    await page.fill('#Input_Prefix', 'data');
    expectTrue((await field('Input_Prefix').textContent()).includes('is taken'), 'no message for a reserved prefix: ' + await field('Input_Prefix').textContent());
    expect(await field('Input_Prefix').getAttribute('class'), 'mat-field is-invalid', 'the field is marked');
    await page.fill('#Input_Prefix', 'a');
    expectTrue((await field('Input_Prefix').textContent()).includes('2 to 8'), 'no message for a prefix that is too short');
    await page.fill('#Input_Prefix', 'Acme');
    expect(await field('Input_Prefix').getAttribute('class'), 'mat-field', 'a capital is turned into lower case: no complaint');
    await page.fill('#Input_Name', 'acmeui');
    expectTrue((await field('Input_Name').textContent()).includes('capital'), 'no message for a name without a capital');
    await page.fill('#Input_Name', 'MatUi');
    expectTrue((await field('Input_Name').textContent()).includes('name of the toolkit itself'), 'MatUi with another prefix');
    await page.fill('#Input_Name', 'AcmeUi');
    expect(await field('Input_Name').getAttribute('class'), 'mat-field', 'the name is right');
  });
  await check('the table of examples shows what the names become and follows the typing', async () => {
    const after = () => page.$$eval('[data-wl-examples] [data-wl-after]', cells => Object.fromEntries(cells.map(c => [c.getAttribute('data-wl-after'), c.textContent])));
    const a = await after();
    expect(a['mat-btn mat-btn--primary'], 'acme-btn acme-btn--primary', 'a class');
    expect(a['--mat-radius'], '--acme-radius', 'a custom property');
    expect(a['window.MatUi'], 'window.AcmeUi', 'the global');
    expect(a['matui.css'], 'acmeui.css', 'a file');
    expect(a['matui-icons.svg'], 'acmeui-icons.svg', 'the sprite');
    expect(a['mat:theme'], 'acme:theme', 'an event');
    expect(a['data-mat-sprite'], 'data-acme-sprite', 'a data attribute');
    expect(a["localStorage 'mat-theme'"], "localStorage 'acme-theme'", 'a storage key');
    expect(a['#mat-i-check'], '#acme-i-check', 'a sprite id');
    await page.fill('#Input_Prefix', 'zz');
    await page.fill('#Input_Name', 'Zed');
    expect((await after())['mat-btn mat-btn--primary'], 'zz-btn zz-btn--primary', 'after typing another prefix');
    expect((await after())['matui.css'], 'zed.css', 'after typing another name');
    await page.fill('#Input_Prefix', PREFIX);
    await page.fill('#Input_Name', NAME);
  });
  await check('Check a trial build runs the renamer and every check on the files of the next release, shows them and counts, and saves nothing', async () => {
    await press(page, 'button[formaction="?handler=Check"]');
    const text = await page.textContent('main');
    expectTrue(text.includes('The trial build passes.'), 'no verdict: ' + text.slice(0, 200));
    expect(await page.locator('[data-wl-checks] .mat-pill--ok').count(), 9, 'checks that pass');
    expect(await page.locator('[data-wl-findings]').count(), 0, 'findings');
    expectTrue((await page.textContent('[data-wl-counts]')).includes('acmeui.css'), 'the counts per file');
    expectTrue(text.includes('Not set'), 'the setting was saved by a check');
    expect(await page.locator('#Input_Prefix').inputValue(), PREFIX, 'what was typed stays');
  });
  await check('a setting that is not valid is refused by the server with the reason, and is not saved', async () => {
    await page.fill('#Input_Prefix', 'aria');
    // the page would not let a reserved prefix go (its custom validity): what the server says to a form that was sent anyway is looked at here
    await page.evaluate(() => { document.querySelectorAll('[data-white-label] input').forEach(i => i.setCustomValidity('')); });
    await press(page, '.mat-btn-row button.mat-btn--primary');
    expectTrue((await page.textContent('[data-validation-summary]')).includes('taken'), 'no summary');
    expectTrue((await page.textContent('main')).includes('Not set'), 'it was saved');
    await page.fill('#Input_Prefix', PREFIX);
  });
  await check('Save keeps the setting, with a place in the history, and the page says it is in force', async () => {
    await press(page, '.mat-btn-row button.mat-btn--primary');
    expectTrue((await page.textContent('main')).includes('In force'), 'not in force');
    expect(await page.locator('#Input_Prefix').inputValue(), PREFIX, 'the prefix');
    expect(await page.locator('.mat-btn-row a[href*="kind=whitelabel"]').count(), 1, 'the history button');
    await page.goto(BASE + '/Studio/History?kind=whitelabel&id=whitelabel', { waitUntil: 'networkidle' });
    expect(await page.locator('main h1').count(), 1, 'h1 of the history');
    expectTrue((await page.textContent('main')).includes('acme- / AcmeUi'), 'the revision is not named');
  });
  await check('the Studio overview, the list of releases and the checks lead to the page, and the check of the setting passes', async () => {
    await page.goto(BASE + '/Studio/Releases', { waitUntil: 'networkidle' });
    expect(await page.locator('a[href="/Studio/Releases/WhiteLabel"]').count() >= 2, true, 'links to the page (menu and button)');
    await page.goto(BASE + '/Studio/Checks', { waitUntil: 'networkidle' });
    expectTrue((await page.textContent('main')).includes('The white label setting builds'), 'the check is not listed');
    expect(await page.locator('main .mat-pill--error').count(), 0, 'checks that fail');
  });
  {
    const ctx = await browser.newContext({ ...phone, storageState: await admin.storageState() });
    const p = await ctx.newPage();
    watch(p, 'studio phone');
    await p.goto(BASE + '/Studio/Releases/WhiteLabel', { waitUntil: 'networkidle' });
    await check('on a phone the page does not scroll sideways, the fields and buttons fill the width and the table has its labels', async () => {
      expect(await p.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'sideways scroll');
      await press(p, 'button[formaction="?handler=Check"]');
      expect(await p.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'sideways scroll after a check');
      expect(await p.locator('[data-wl-checks] td:not([data-label])').count(), 0, 'cells without a label');
      const input = await p.locator('#Input_Prefix').boundingBox();
      expectTrue(input.width > 250 && input.height >= 36, 'the field is ' + input.width + 'x' + input.height);
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the renamed toolkit in the browser
  console.log('\nThe renamed toolkit');
  const cache = new Map();
  async function renamed(text) {
    const key = crypto.createHash('sha1').update(text).digest('hex');
    if (!cache.has(key)) {
      const r = await admin.request.post(`${BASE}/Studio/Releases/WhiteLabel/rename?prefix=${PREFIX}&name=${NAME}`, { data: text, headers: { 'content-type': 'text/plain; charset=utf-8' } });
      if (!r.ok()) { throw new Error('the renamer answered ' + r.status()); }
      cache.set(key, await r.text());
    }
    return cache.get(key);
  }
  // the browser asks for acmeui.css, the site has matui.css; every text that comes back is renamed
  async function behindTheRenamer(p) {
    await p.route(url => url.origin === new URL(BASE).origin, async route => {
      const request = route.request();
      if (request.method() !== 'GET') { return route.continue(); }
      const res = await route.fetch({ url: request.url().split(STEM).join('matui') });
      const type = res.headers()['content-type'] || '';
      if (!/text\/html|text\/css|javascript|image\/svg|application\/json/.test(type)) { return route.fulfill({ response: res }); }
      const headers = { ...res.headers() };
      delete headers['content-length'];
      delete headers['content-encoding'];
      return route.fulfill({ status: res.status(), headers, body: await renamed(await res.text()) });
    });
  }
  async function renamedPage(options, url) {
    const ctx = await browser.newContext({ ...options, storageState: await admin.storageState() });
    const p = await ctx.newPage();
    watch(p, 'renamed ' + url);
    await behindTheRenamer(p);
    await p.goto(BASE + url, { waitUntil: 'networkidle' });
    await p.waitForFunction(() => document.readyState === 'complete');
    return { ctx, page: p };
  }

  {
    const { ctx, page: p } = await renamedPage(desktop, '/Components#dialogs');
    await check('the page runs the renamed files: the global is AcmeUi, there is no MatUi, and nothing of the old prefix is left in the DOM, the styles or the storage', async () => {
      const s = await p.evaluate(({ prefix, name }) => {
        const all = [...document.querySelectorAll('*')];
        return {
          oldGlobal: typeof window.MatUi, newGlobal: typeof window[name], version: window[name] && window[name].version,
          oldClasses: all.filter(e => [...e.classList].some(c => /^mat-/.test(c))).length, newClasses: all.filter(e => [...e.classList].some(c => c.startsWith(prefix + '-'))).length,
          oldAttributes: all.filter(e => [...e.attributes].some(a => /^data-mat-/.test(a.name))).length,
          oldIds: all.filter(e => /^mat-/.test(e.id)).length,
          oldProperty: getComputedStyle(document.documentElement).getPropertyValue('--mat-bg').trim(), newProperty: getComputedStyle(document.documentElement).getPropertyValue('--' + prefix + '-bg').trim(),
          sprite: !!document.querySelector('svg.' + prefix + '-sprite symbol[id^="' + prefix + '-i-"]'), uses: [...document.querySelectorAll('use')].filter(u => /#mat-/.test(u.getAttribute('href') || '')).length,
          sheets: [...document.styleSheets].map(sh => sh.href).filter(Boolean).map(h => h.replace(location.origin, '')),
          scripts: [...document.scripts].map(sc => sc.src).filter(Boolean).map(h => h.replace(location.origin, '')),
        };
      }, { prefix: PREFIX, name: NAME });
      expect([s.oldGlobal, s.newGlobal], ['undefined', 'object'], 'the global');
      expectTrue(/^\d+\.\d+\.\d+/.test(s.version), 'version ' + s.version);
      expect([s.oldClasses, s.oldAttributes, s.oldIds, s.uses], [0, 0, 0, 0], 'names of the old prefix in the DOM (classes, attributes, ids, icons)');
      expectTrue(s.newClasses > 1000, 'classes of the new prefix: ' + s.newClasses);
      expect(s.oldProperty, '', 'a custom property of the old prefix');
      expectTrue(s.newProperty.length > 0, 'the token --' + PREFIX + '-bg has no value');
      expect(s.sprite, true, 'the sprite is inline with the new ids');
      expectTrue(s.sheets.some(h => h.includes('/ui/' + STEM + '.css')) && s.scripts.some(h => h.includes('/ui/' + STEM + '.js')), 'the page does not load the renamed files: ' + JSON.stringify([s.sheets, s.scripts]));
    });
    await check('the theme button toggles the theme and the choice is saved under the new name', async () => {
      const state = () => p.evaluate(() => ({ attr: document.documentElement.getAttribute('data-theme'), state: document.querySelector('[data-theme-toggle]').getAttribute('data-theme-state') }));
      expect((await state()).state, 'system', 'start');
      await p.click('[data-theme-toggle]');
      expect(await state(), { attr: 'light', state: 'light' }, 'after one click');
      await p.click('[data-theme-toggle]');
      expect(await state(), { attr: 'dark', state: 'dark' }, 'after two');
      expect(await p.evaluate(() => [localStorage.getItem('acme-theme'), localStorage.getItem('mat-theme')]), ['dark', null], 'where the choice is kept');
      await p.click('[data-theme-toggle]');
      expect((await state()).state, 'system', 'back to the system');
      expect(await p.evaluate(() => Object.keys(localStorage).filter(k => k.startsWith('mat-'))), [], 'keys of the old prefix');
    });
    await check('a dialog opens from its button and closes with its X and with Escape', async () => {
      await p.evaluate(() => document.getElementById('dialogs').scrollIntoView());
      const opener = p.locator('#dialogs [data-dialog-open]').first();
      const id = await opener.getAttribute('data-dialog-open');
      await opener.click();
      expect(await p.evaluate(i => document.getElementById(i).open, id), true, 'open after the click');
      expect(await p.locator(`#${id} .acme-dialog__title`).count() >= 1 || await p.locator(`dialog[open] .acme-dialog__title`).count() >= 1, true, 'the title has the new class');
      await p.click(`#${id} [data-dialog-close]`);
      expect(await p.evaluate(i => document.getElementById(i).open, id), false, 'closed by the X');
      await opener.click();
      await p.keyboard.press('Escape');
      expect(await p.evaluate(i => document.getElementById(i).open, id), false, 'closed by Escape');
    });
    await check('a script dialog and a toast: AcmeUi.confirm returns what was chosen and the toast has the new classes', async () => {
      await p.evaluate(() => { document.getElementById('dialogs').scrollIntoView(); window.__answer = window.AcmeUi.confirm({ title: 'Please confirm', message: 'Sure?' }); });
      await p.waitForSelector('dialog[open] .acme-dialog__title');
      await p.click('dialog[open] button[value="ok"]');
      expect(await p.evaluate(() => window.__answer), true, 'the answer');
      await p.evaluate(() => window.AcmeUi.toast('Saved', { tone: 'success', timeout: 30000 }));
      expect(await p.locator('.acme-toasts .acme-toast--success').count(), 1, 'the toast');
      await p.click('.acme-toast .acme-alert__close');
      expect(await p.locator('.acme-toast').count(), 0, 'toasts after its X');
    });
    await check('a tab switches the panel and a select picker opens, chooses and closes', async () => {
      const tabs = p.locator('[data-tabs]').first();
      await tabs.scrollIntoViewIfNeeded();
      const tab = tabs.locator('[role=tab]').nth(1);
      await tab.click();
      expect(await tab.getAttribute('aria-selected'), 'true', 'the second tab');
      expect(await tabs.locator('[role=tab]').first().getAttribute('aria-selected'), 'false', 'the first tab');
      const picker = p.locator(R('.mat-picker[data-picker="select"]:not([data-multiple])')).first();
      await picker.scrollIntoViewIfNeeded();
      await picker.locator(R('.mat-picker__trigger')).click();
      const panel = picker.locator(R('.mat-picker__panel'));
      expect(await panel.evaluate(el => el.hidden), false, 'the panel is open');
      expect(await picker.locator(R('.mat-picker__trigger')).getAttribute('aria-expanded'), 'true', 'expanded');
      const option = panel.locator(R('.mat-picker__option:not([aria-disabled="true"])')).nth(1);
      const wanted = await option.getAttribute('data-value');
      await option.click();
      expect(await picker.locator(R('.mat-picker__input')).inputValue(), wanted, 'the value that was chosen');
      expect(await panel.evaluate(el => el.hidden), true, 'the panel closes');
    });
    await check('the number stepper steps with its buttons and tells the page with the same events', async () => {
      await p.evaluate(() => document.getElementById('number-stepper').scrollIntoView());
      const inc = p.locator(R('.mat-stepper:has(#stp-qty) > .mat-stepper__btn--inc'));
      const before = Number(await p.inputValue('#stp-qty'));
      await p.evaluate(() => { window.__events = []; const i = document.getElementById('stp-qty'); i.addEventListener('input', () => window.__events.push('input')); i.addEventListener('change', () => window.__events.push('change')); });
      await inc.click();
      expect(Number(await p.inputValue('#stp-qty')), before + 1, 'after more');
      expect(await p.evaluate(() => window.__events), ['input', 'change'], 'events');
      expect(await p.locator(R('.mat-stepper.is-ready')).count() >= 6, true, 'steppers that were wired');
    });
    await check('a tree opens and closes a branch and chooses an item, and sends its event under the new name', async () => {
      const tree = p.locator(R('.mat-tree[data-tree]')).first();
      await tree.scrollIntoViewIfNeeded();
      const toggle = tree.locator(R('.mat-tree__toggle')).first();
      const item = toggle.locator(R('xpath=ancestor::*[contains(@class, "mat-tree__item")][1]'));
      const open = () => item.getAttribute('aria-expanded');
      const was = await open();
      await toggle.click();
      expectTrue(await open() !== was, 'the branch did not change: ' + was);
      await toggle.click();
      expect(await open(), was, 'back as it was');
      await p.evaluate(() => { window.__tree = []; document.addEventListener('acme:tree-select', e => window.__tree.push(e.type)); });
      await tree.locator(R('.mat-tree__label')).first().click();
      expectTrue((await p.evaluate(() => window.__tree)).length >= 1, 'no acme:tree-select');
    });
    await check('a rating is chosen by a click and tells the page, and an upload takes a file', async () => {
      await p.evaluate(() => document.getElementById('rating').scrollIntoView());
      const stars = p.locator(R('fieldset.mat-rating:has(#rt-support-1) .mat-rating__stars'));
      const b = await stars.boundingBox();
      await p.evaluate(() => { window.__rating = []; document.addEventListener('acme:rating-change', e => window.__rating.push(e.detail.value)); });
      await p.mouse.click(b.x + b.width * 0.3, b.y + b.height / 2);
      expect(await p.evaluate(() => window.__rating), [2], 'the value');
      await p.evaluate(() => document.getElementById('file-upload').scrollIntoView());
      await p.setInputFiles(R('.mat-upload:has(#up-docs) .mat-upload__input'), [{ name: 'a.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(100, 1) }]);
      expect(await p.$$eval(R('.mat-upload:has(#up-docs) .mat-upload__name'), els => els.map(e => e.textContent)), ['a.pdf'], 'the file');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the same look
  console.log('\nThe look of the renamed toolkit');
  {
    const helper = await admin.newPage();
    await helper.goto('about:blank');
    // the number of pixels that differ between two screenshots, decoded in the browser
    const difference = (a, b) => helper.evaluate(async ([x, y]) => {
      const load = src => new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = () => rej(new Error('picture')); i.src = 'data:image/png;base64,' + src; });
      const [ia, ib] = await Promise.all([load(x), load(y)]);
      if (ia.width !== ib.width || ia.height !== ib.height) { return { size: [ia.width, ia.height, ib.width, ib.height], exact: -1, loud: -1, total: 0 }; }
      const read = img => { const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const g = c.getContext('2d'); g.drawImage(img, 0, 0); return g.getImageData(0, 0, c.width, c.height).data; };
      const da = read(ia), db = read(ib);
      // a pixel is "different" when a colour channel is off by more than 4 of 255 ("loud"); a smaller difference is the anti-aliasing of an edge, which the very same page does not repeat exactly either
      // (the original against the original differs there too), and it is counted on its own ("exact")
      let exact = 0, loud = 0, box = null;
      for (let i = 0; i < da.length; i += 4) {
        const m = Math.max(Math.abs(da[i] - db[i]), Math.abs(da[i + 1] - db[i + 1]), Math.abs(da[i + 2] - db[i + 2]), Math.abs(da[i + 3] - db[i + 3]));
        if (m > 0) {
          exact++;
          if (m > 4) {
            loud++;
            const px = (i / 4) % ia.width, py = Math.floor(i / 4 / ia.width);
            box = box ? [Math.min(box[0], px), Math.min(box[1], py), Math.max(box[2], px), Math.max(box[3], py)] : [px, py, px, py];
          }
        }
      }
      return { size: [ia.width, ia.height], exact, loud, total: ia.width * ia.height, box };
    }, [a.toString('base64'), b.toString('base64')]);

    // The documentation shows the names of the classes and of the product as text. The original page gets the same text as the renamed one (the text nodes go through the same rules, the classes and the
    // styles are not touched), so that both lay out the same lines: what is left to differ is the toolkit. The sample app (it says nothing about class names) is compared as it is.
    const normalizeText = p => p.evaluate(({ prefix, name }) => {
      const rename = text => text
        .replace(/(^|[^A-Za-z0-9_$-])--mat-/g, '$1--' + prefix + '-')
        .replace(/(^|[^A-Za-z0-9_$-])data-mat-/g, '$1data-' + prefix + '-')
        .replace(/(^|[^A-Za-z0-9_$-])mat-/g, '$1' + prefix + '-')
        .replace(/(^|[^A-Za-z0-9_$-])mat:/g, '$1' + prefix + ':')
        .replace(/(^|[^A-Za-z0-9_$])MatUi(?![A-Za-z0-9_])/g, '$1' + name)
        .replace(/(^|[^A-Za-z0-9_$-])matui(?![A-Za-z0-9_])/g, '$1' + name.toLowerCase());
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      let node;
      while ((node = walker.nextNode())) { const text = rename(node.nodeValue); if (text !== node.nodeValue) { node.nodeValue = text; } }
    }, { prefix: PREFIX, name: NAME });
    const scenes = ['/Preview?app=matmail&view=list', '/Preview?app=matmail&view=form', '/Preview?app=matpaper&view=settings'];
    const looks = [
      ['light', { ...desktop, colorScheme: 'light' }],
      ['dark', { ...desktop, colorScheme: 'dark' }],
      ['390 px', { ...phone, colorScheme: 'light' }],
    ];
    const places = ['#rating', '#number-stepper', '#file-upload', '#dialogs', '#switcher', '#itemlist', '#schedule'];
    let compared = 0, exactTotal = 0, loudTotal = 0, shots = 0;
    const settle = p => p.evaluate(() => document.fonts.ready).then(() => p.waitForTimeout(250));
    const shot = (target) => target.screenshot({ animations: 'disabled', caret: 'hide' });
    async function compare(a, b, what) {
      const d = await difference(a, b);
      shots++;
      compared += d.total;
      exactTotal += Math.max(d.exact, 0);
      loudTotal += Math.max(d.loud, 0);
      // a few pixels on ONE edge (all in one column or one row) are the anti-aliasing of a border that sits on a fraction of a pixel: Chrome on the Linux runner
      // draws it a little louder now and then (4 pixels of the left line of an example, x 295, y 558-631). A change of the toolkit (a colour, a space, a radius) changes hundreds.
      const edgeNoise = d.box && d.loud <= 8 && (d.box[0] === d.box[2] || d.box[1] === d.box[3]);
      if (edgeNoise) { return; }
      expect(d.loud, 0, `${what}: pixels that differ by more than 4 levels, of ${d.total} (${JSON.stringify(d.size)}${d.box ? ', at x ' + d.box[0] + '-' + d.box[2] + ', y ' + d.box[1] + '-' + d.box[3] : ''})`);
    }
    for (const [label, options] of looks) {
      for (const scene of scenes) {
        await check(`${label}: ${scene} looks the same with the renamed toolkit as with the original, pixel for pixel`, async () => {
          const original = await browser.newContext({ ...options, storageState: await admin.storageState() });
          const op = await original.newPage();
          watch(op, 'original ' + scene);
          await op.goto(BASE + scene, { waitUntil: 'networkidle' });
          const copy = await renamedPage(options, scene);
          await normalizeText(op);
          await settle(op); await settle(copy.page);
          await compare(await shot(op), await shot(copy.page), scene);
          await original.close();
          await copy.ctx.close();
        });
      }

      await check(`${label}: the components page looks the same, in seven places and in the live part of every third example, pixel for pixel`, async () => {
        const original = await browser.newContext({ ...options, storageState: await admin.storageState() });
        const op = await original.newPage();
        watch(op, 'original components ' + label);
        await op.goto(BASE + '/Components', { waitUntil: 'networkidle' });
        await op.waitForFunction(() => document.readyState === 'complete');
        const copy = await renamedPage(options, '/Components');
        await normalizeText(op);
        for (const p of [op, copy.page]) { await p.evaluate(() => { document.documentElement.style.scrollBehavior = 'auto'; }); }
        const problems = [];
        for (const place of places) {
          for (const p of [op, copy.page]) { await p.evaluate(sel => document.querySelector(sel).scrollIntoView({ block: 'start', behavior: 'instant' }), place); await settle(p); }
          try { await compare(await shot(op), await shot(copy.page), place); } catch (e) { problems.push(e.message); }
        }

        const count = await op.locator('.doc-example__live').count();
        expect(await copy.page.locator('.doc-example__live').count(), count, 'live examples on the renamed page');
        expectTrue(count > 150, 'live examples: ' + count);
        for (let i = 0; i < count; i += 3) {
          const [x, y] = [op.locator('.doc-example__live').nth(i), copy.page.locator('.doc-example__live').nth(i)];
          if (!(await x.isVisible()) || !(await y.isVisible())) { continue; }
          try { await compare(await shot(x), await shot(y), 'example ' + i); } catch (e) { problems.push(e.message); }
        }

        expect(problems, [], 'places and examples that differ');
        await original.close();
        await copy.ctx.close();
      });
    }
    console.log(`        ${shots} screenshots (light, dark, 390 px): ${loudTotal} of ${compared.toLocaleString('en-US')} pixels differ by more than 4 levels; ${exactTotal} pixels are not exactly equal (anti-aliasing noise of at most 4 levels)`);
    await helper.close();
  }

  // ---------------------------------------------------------------------------------------------------- the end
  // the setting is taken away again: the site's own files never changed, and a later release is made without it
  await page.goto(BASE + '/Studio/Releases/WhiteLabel', { waitUntil: 'networkidle' });
  if (await page.locator('button[formaction="?handler=Reset"]').count() > 0) {
    await page.click('button[formaction="?handler=Reset"]');
    await Promise.all([page.waitForNavigation({ waitUntil: 'load' }), page.click('dialog[open] button[value="ok"]')]);
  }
  console.log('');
  await check('no script error and no error in the console on any page above, and no file that was asked for is missing', async () => {
    expect(consoleErrors, [], 'console errors');
    expect(failedRequests, [], 'requests that failed');
  });

  await browser.close();
  const failed = results.filter(r => !r.ok);
  console.log(`\n${results.length - failed.length} of ${results.length} checks passed.`);
  process.exit(failed.length === 0 ? 0 : 1);
})().catch(e => { console.error('FAILED to run:', e.message); annotate(__filename, 'FAILED to run: ' + e.message.split('\n')[0]); process.exit(2); });
