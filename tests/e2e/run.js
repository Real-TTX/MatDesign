// End-to-end checks of the behaviour of MatUi in a real browser (the installed Edge, or Chrome) against a running site.
//   cd tests/e2e && npm install && node run.js [http://localhost:8060]
// The unit tests read the script and the CSS; this is where the rules are seen to hold: a drawer that shows the logo, dialogs that fill a phone,
// the button order, the theme button, dependent fields, the keyboard. Exit code 1 when a check fails.
const { chromium } = require('playwright-core');

const BASE = (process.argv[2] || process.env.BASE || 'http://localhost:8060').replace(/\/$/, '');
const results = [];

async function check(name, fn) {
  try {
    await fn();
    results.push({ name, ok: true });
    console.log('  ok    ' + name);
  } catch (e) {
    results.push({ name, ok: false, why: e.message });
    console.log('  FAIL  ' + name + '\n        ' + e.message.split('\n')[0]);
  }
}

function expect(actual, expected, what) {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error(`${what}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}

function expectTrue(value, what) {
  if (!value) { throw new Error(what); }
}

// A computed colour ("rgb(1, 2, 3)", "rgba(...)" or "color(srgb 0.1 0.2 0.3)") as [r, g, b] in 0..255, and the WCAG contrast ratio of two of them.
function parseColour(text) {
  const n = (text.match(/-?\d*\.?\d+/g) || []).map(Number);
  return text.startsWith('color(') ? n.slice(0, 3).map(v => v * 255) : n.slice(0, 3);
}
function contrastOf(a, b) {
  const lum = ([r, g, bl]) => [r, g, bl].map(v => { const c = v / 255; return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4); }).reduce((s, v, i) => s + v * [0.2126, 0.7152, 0.0722][i], 0);
  const [hi, lo] = [lum(parseColour(a)), lum(parseColour(b))].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

// The accent of the page as the browser computes it (a variable cannot be compared with a computed colour): the fill, the text on it and the pressed fill.
function accentOf(page) {
  return page.evaluate(() => {
    const probe = (prop, value) => { const t = document.createElement('i'); t.style.setProperty(prop, value); document.body.appendChild(t); const c = getComputedStyle(t).getPropertyValue(prop); t.remove(); return c; };
    return { accent: probe('background-color', 'var(--mat-accent)'), onAccent: probe('color', 'var(--mat-on-accent)'), active: probe('background-color', 'var(--mat-accent-active)') };
  });
}

// What a click on the middle of the first element with this selector would hit: "seen" when it is the element or something inside it, else what covers it
// (a modal dialog is in the top layer, so whatever is outside of it is under it, whatever its z-index).
function seenAt(page, selector) {
  return page.evaluate((sel) => {
    const el = document.querySelector(sel);
    if (!el) { return 'no element ' + sel; }
    const r = el.getBoundingClientRect();
    const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
    return hit && el.contains(hit) ? 'seen' : 'covered by ' + (hit ? hit.tagName.toLowerCase() + '.' + String(hit.className) : 'nothing');
  }, selector);
}

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true }).catch(() => chromium.launch({ channel: 'chrome', headless: true }));
  const phoneOptions = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true };
  const desktopOptions = { viewport: { width: 1360, height: 900 } };
  const consoleErrors = [];

  async function open(options, url) {
    const ctx = await browser.newContext(options);
    const page = await ctx.newPage();
    page.on('pageerror', e => consoleErrors.push(url + ': ' + e.message));
    page.on('console', m => { if (m.type() === 'error') { consoleErrors.push(url + ': ' + m.text()); } });
    await page.goto(BASE + url, { waitUntil: 'networkidle' });
    // the components page is two megabytes: the network can be quiet for half a second while the document is still being read, and its script has not run yet
    await page.waitForFunction(() => document.readyState === 'complete');
    return { ctx, page };
  }

  console.log('MatUi in a browser: ' + BASE);

  // ---------------------------------------------------------------------------------------------------- the shell
  console.log('\nShell');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=list');
    await check('the menu is on the left of the content, the logo above the items', async () => {
      const nav = await page.locator('.mat-nav').boundingBox();
      const main = await page.locator('.mat-main').boundingBox();
      const logo = await page.locator('.mat-nav .mat-logo').boundingBox();
      const firstItem = await page.locator('.mat-nav__item').first().boundingBox();
      expectTrue(nav.x < main.x, 'the menu is not left of the content');
      expectTrue(logo.y < firstItem.y, 'the logo is not above the first item');
    });
    await check('the foot of the menu has the user, the theme button and below them the version', async () => {
      const user = await page.locator('.mat-user').boundingBox();
      const theme = await page.locator('.mat-theme').boundingBox();
      const version = await page.locator('.mat-nav__version').boundingBox();
      expectTrue(Math.abs(user.y - theme.y) < 20, 'the user and the theme button are not side by side');
      expectTrue(version.y > user.y + user.height - 1, 'the version is not below the user');
    });
    await check('the breadcrumb is above the page content', async () => {
      const crumbs = await page.locator('.mat-crumbs').boundingBox();
      const head = await page.locator('.mat-page__head').boundingBox();
      expectTrue(crumbs.y < head.y, 'the breadcrumb is not above the page head');
    });
    await check('the menu scrolls separately from the content', async () => {
      const overflow = await page.evaluate(() => ({
        page: document.documentElement.scrollHeight <= window.innerHeight + 1,
        nav: getComputedStyle(document.querySelector('.mat-nav__scroll')).overflowY,
        main: getComputedStyle(document.querySelector('.mat-main')).overflowY,
      }));
      expect(overflow.page, true, 'the page itself must not scroll');
      expect(['auto', 'scroll'].includes(overflow.nav), true, 'the menu does not scroll on its own');
      expect(['auto', 'scroll'].includes(overflow.main), true, 'the content does not scroll on its own');
    });
    await check('the theme button cycles system, light, dark, system and saves the choice', async () => {
      const state = () => page.evaluate(() => ({ attr: document.documentElement.getAttribute('data-theme'), state: document.querySelector('[data-theme-toggle]').getAttribute('data-theme-state') }));
      expect((await state()).state, 'system', 'start');
      await page.click('[data-theme-toggle]');
      expect((await state()), { attr: 'light', state: 'light' }, 'after one click');
      await page.click('[data-theme-toggle]');
      expect((await state()), { attr: 'dark', state: 'dark' }, 'after two clicks');
      await page.click('[data-theme-toggle]');
      expect((await state()), { attr: null, state: 'system' }, 'after three clicks (system means no attribute)');
    });
    await check('the user opens a context menu with Sign out as the last item; Escape closes it', async () => {
      await page.click('.mat-user');
      const items = await page.locator('#user-menu .mat-menu__item').allTextContents();
      expect(items.at(-1).trim(), 'Sign out', 'last item');
      expectTrue(await page.locator('#user-menu').isVisible(), 'the menu is not shown');
      await page.keyboard.press('Escape');
      expectTrue(!(await page.locator('#user-menu').isVisible()), 'Escape did not close the menu');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the list page
  console.log('\nList page');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=list');
    await check('the toolbar is above the table; search, filter and sort are three buttons', async () => {
      const toolbar = await page.locator('.mat-toolbar').boundingBox();
      const table = await page.locator('.mat-table-wrap').boundingBox();
      expectTrue(toolbar.y < table.y, 'the toolbar is below the table');
      expect((await page.locator('.mat-toolbar .mat-toolbar__btn').allTextContents()).map(t => t.trim().split(/\s+/)[0]), ['Search', 'Filter', 'Sort'], 'buttons');
    });
    await check('the pagination is directly under the table, the actions below it', async () => {
      const table = await page.locator('.mat-table-wrap').boundingBox();
      const pager = await page.locator('.mat-pager').boundingBox();
      const actions = await page.locator('.mat-list > .mat-actions').boundingBox();
      expectTrue(pager.y >= table.y + table.height - 1 && pager.y - (table.y + table.height) < 40, 'the pager is not directly under the table');
      expectTrue(actions.y > pager.y, 'the actions are not below the pager');
    });
    await check('a button opens a dialog, Apply sends a GET and the tool is shown as a chip', async () => {
      await page.click('[data-dialog-open="sample-filter"]');
      expectTrue(await page.locator('#sample-filter').evaluate(d => d.open), 'the filter dialog did not open');
      await page.selectOption('#sample-f-status', 'Failed');
      await Promise.all([page.waitForNavigation(), page.click('#sample-filter button[type=submit]')]);
      expectTrue(page.url().includes('status=Failed'), 'the address does not carry the filter: ' + page.url());
      expectTrue((await page.locator('.mat-chip').first().textContent()).includes('Failed'), 'no chip for the filter');
    });
    await check('the "/" key opens the search dialog, but not while typing in a field', async () => {
      await page.goto(BASE + '/Preview?app=matmail&view=list', { waitUntil: 'networkidle' });
      await page.keyboard.press('/');
      expectTrue(await page.locator('#sample-search').evaluate(d => d.open), 'the search dialog did not open');
      await page.fill('#sample-q', 'a/b');
      expect(await page.inputValue('#sample-q'), 'a/b', 'the slash was typed into the field');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- dialogs on a phone
  console.log('\nDialogs on a phone');
  {
    const { ctx, page } = await open(phoneOptions, '/Preview?app=matmail&view=list');
    await check('every dialog of the toolbar fills the screen, with the X at the top right', async () => {
      for (const id of ['sample-search', 'sample-filter', 'sample-sort']) {
        await page.click(`[data-dialog-open="${id}"]`);
        await page.waitForTimeout(350);
        const box = await page.locator('#' + id).boundingBox();
        expect({ x: Math.round(box.x), y: Math.round(box.y), w: Math.round(box.width), h: Math.round(box.height) }, { x: 0, y: 0, w: 390, h: 844 }, id + ' size');
        const close = await page.locator(`#${id} .mat-dialog__close`).boundingBox();
        expectTrue(close.x > 390 / 2 && close.y < 80, id + ': the X is not at the top right');
        await page.click(`#${id} .mat-dialog__close`);
        await page.waitForTimeout(200);
      }
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Preview?app=matmail&view=form');
    await check('a destructive confirmation is a dialog: Cancel first, a space, the red Delete last', async () => {
      await page.click('.mat-btn--danger[data-confirm]');
      await page.waitForTimeout(350);
      const buttons = await page.locator('dialog[open] .mat-dialog__foot .mat-btn').evaluateAll(b => b.map(x => ({ text: x.textContent.trim(), order: getComputedStyle(x).order })));
      expect(buttons.map(b => b.text), ['Cancel', 'Delete'], 'buttons');
      expectTrue(Number(buttons[1].order) > Number(buttons[0].order), 'Delete is not behind Cancel');
      const dialog = await page.locator('dialog[open]').boundingBox();
      expect([Math.round(dialog.width), Math.round(dialog.height)], [390, 844], 'size on a phone');
      await page.click('dialog[open] [data-dialog-close]');
    });
    await check('on a phone a toast is seen above a full-screen dialog, and its X works', async () => {
      await page.click('.mat-btn--danger[data-confirm]');
      await page.waitForTimeout(350);
      await page.evaluate(() => { MatUi.toast('Saved', { tone: 'success', timeout: 30000 }); });
      await page.waitForTimeout(450);
      expect(await seenAt(page, '.mat-toast'), 'seen', 'the toast above the sheet');
      await page.click('.mat-toast .mat-alert__close', { timeout: 3000 });
      expect(await page.locator('.mat-toast').count(), 0, 'toasts after its X');
      await page.click('dialog[open] [data-dialog-close]');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the phone drawer
  console.log('\nThe menu on a phone');
  {
    const { ctx, page } = await open(phoneOptions, '/Preview?app=matmail&view=list');
    const state = () => page.evaluate(() => {
      const shell = document.querySelector('.mat-app');
      const logo = document.querySelector('.mat-nav .mat-logo').getBoundingClientRect();
      const hit = document.elementFromPoint(logo.x + logo.width / 2, logo.y + logo.height / 2);
      return { open: shell.classList.contains('is-nav-open'), logoVisible: !!(hit && hit.closest('.mat-nav')) };
    });
    await check('the burger opens the drawer and the logo is visible above the items, not under the top bar', async () => {
      expect(await state(), { open: false, logoVisible: false }, 'closed');
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      expect(await state(), { open: true, logoVisible: true }, 'open');
    });
    await check('the X in the drawer closes it', async () => {
      await page.click('.mat-nav__close');
      await page.waitForTimeout(450);
      expect((await state()).open, false, 'open after X');
    });
    await check('Escape closes the drawer, and so does a click on the backdrop', async () => {
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      await page.keyboard.press('Escape');
      await page.waitForTimeout(300);
      expect((await state()).open, false, 'open after Escape');
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      await page.mouse.click(370, 400);
      await page.waitForTimeout(300);
      expect((await state()).open, false, 'open after a click on the backdrop');
    });
    await ctx.close();
    const desktop = await open(desktopOptions, '/Preview?app=matmail&view=list');
    await check('on a desktop there is no X and no burger', async () => {
      expect(await desktop.page.evaluate(() => [getComputedStyle(document.querySelector('.mat-nav__close')).display, getComputedStyle(document.querySelector('.mat-burger')).display]), ['none', 'none'], 'display');
    });
    await desktop.ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the form
  console.log('\nForms');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=form');
    await check('the buttons are under the content, in the order Save, Back, space, Delete', async () => {
      const card = await page.locator('.mat-card').first().boundingBox();
      const row = await page.locator('.mat-btn-row').boundingBox();
      expectTrue(row.y > card.y + card.height - 1, 'the buttons are not under the content');
      const texts = await page.locator('.mat-btn-row > *').evaluateAll(els => els.map(e => ({ t: e.textContent.trim(), o: Number(getComputedStyle(e).order) })).sort((a, b) => a.o - b.o).map(x => x.t));
      expect(texts, ['Save', 'Back', '', 'Delete'], 'visual order');
    });
    await check('a dependent field is shown only while it is needed', async () => {
      expectTrue(await page.locator('#f-mail').isVisible(), 'the e-mail field is hidden while the switch is on');
      await page.click('label.mat-switch');
      await page.waitForTimeout(200);
      expectTrue(!(await page.locator('#f-mail').isVisible()), 'the e-mail field is still shown with the switch off');
      await page.click('label.mat-switch');
      expectTrue(await page.locator('#f-mail').isVisible(), 'the e-mail field did not come back');
    });
    await check('validation is written by the toolkit, at the field, and the browser shows no bubble of its own', async () => {
      await page.fill('#f-title', '');
      await page.click('.mat-btn-row button[type=submit]');
      await page.waitForTimeout(200);
      expect((await page.locator('#f-title').evaluate(i => i.closest('.mat-field').classList.contains('is-invalid'))), true, 'is-invalid');
      expect((await page.locator('#f-title ~ .mat-error').textContent()).trim(), 'Please enter a title.', 'message');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the toolkit's own dialogs
  console.log('\nScript dialogs and toasts');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#dialogs');
    await check('MatUi.confirm, alert and prompt are dialogs and return their results; a toast follows', async () => {
      await page.click('#dialogs [data-doc-demo="dialogs"]');
      await page.waitForSelector('dialog[open] .mat-dialog__title');
      expect(await page.textContent('dialog[open] .mat-dialog__title'), 'Please confirm', 'confirm');
      await page.click('dialog[open] button[value="ok"]');
      await page.waitForTimeout(300);
      expect(await page.textContent('dialog[open] .mat-dialog__title'), 'Backup', 'alert');
      await page.click('dialog[open] button[value="ok"]');
      await page.waitForTimeout(300);
      await page.fill('dialog[open] input[name=value]', 'Photos');
      await page.click('dialog[open] button[value="ok"]');
      await page.waitForTimeout(400);
      expectTrue((await page.textContent('.mat-toasts')).includes('Created: Photos'), 'no toast with the result of the prompt');
    });
    await check('a click on Cancel in a confirmation returns false and nothing else happens', async () => {
      await page.click('#dialogs [data-confirm]');
      await page.waitForSelector('dialog[open]');
      await page.click('dialog[open] [data-dialog-close]');
      await page.waitForTimeout(250);
      expect(await page.locator('dialog[open]').count(), 0, 'open dialogs');
    });
    await check('a toast shown while a dialog is open is seen above it, and its X works', async () => {
      await page.evaluate(() => { document.querySelectorAll('.mat-toast').forEach(t => t.remove()); document.getElementById('dialogs').scrollIntoView(); });
      await page.click('[data-dialog-open="dlg-side"]');
      await page.waitForFunction(() => document.getElementById('dlg-side').getAnimations().length === 0);
      await page.evaluate(() => { MatUi.toast('Saved in the dialog', { tone: 'success', timeout: 30000 }); });
      await page.waitForTimeout(450);
      expect(await page.evaluate(() => !!document.querySelector('#dlg-side .mat-toasts .mat-toast')), true, 'the toast is inside the open dialog');
      expect(await seenAt(page, '.mat-toast'), 'seen', 'the toast above the dialog');
      await page.click('.mat-toast .mat-alert__close', { timeout: 3000 });
      expect(await page.locator('.mat-toast').count(), 0, 'toasts after its X');
      await page.click('#dlg-side .mat-dialog__close');
    });
    await check('the toasts follow the dialogs: into the topmost, back into the one below when it closes, onto the page after the last, and none is lost on the way', async () => {
      const host = () => page.evaluate(() => { const h = document.querySelector('.mat-toasts'); return h ? (h.parentElement.id || h.parentElement.tagName.toLowerCase()) : 'no host'; });
      await page.evaluate(() => { document.querySelectorAll('.mat-toast').forEach(t => t.remove()); document.getElementById('dialogs').scrollIntoView(); });
      await page.click('[data-dialog-open="dlg-side"]');
      await page.waitForFunction(() => document.getElementById('dlg-side').getAnimations().length === 0);
      await page.evaluate(() => { MatUi.confirm('Sure?'); });
      await page.waitForSelector('dialog.mat-dialog--sm[open]');
      await page.evaluate(() => { MatUi.toast('Over both dialogs', { timeout: 30000 }); });
      await page.waitForTimeout(450);
      expectTrue((await host()).startsWith('mat-dlg-'), 'the toasts are not in the topmost dialog: ' + await host());
      expect(await seenAt(page, '.mat-toast'), 'seen', 'the toast above the confirmation');
      await page.click('dialog.mat-dialog--sm[open] [data-dialog-close]');
      await page.waitForTimeout(300);
      expect(await host(), 'dlg-side', 'where the toasts are after the confirmation closed (it removes itself)');
      expect(await page.locator('.mat-toast').count(), 1, 'toasts after the confirmation closed');
      expect(await seenAt(page, '.mat-toast'), 'seen', 'the toast above the panel');
      await page.click('#dlg-side .mat-dialog__close');
      await page.waitForTimeout(250);
      expect(await host(), 'body', 'where the toasts are after the last dialog closed');
      expect(await page.locator('.mat-toast').count(), 1, 'toasts after the last dialog closed');
      expect(await seenAt(page, '.mat-toast'), 'seen', 'the toast on the page');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- settings, tabs, theme choice
  console.log('\nTabs and the theme choice');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=settings');
    await check('tabs: a click shows the panel, the arrow keys move between the tabs', async () => {
      expectTrue(await page.locator('#panel-general').isVisible(), 'the first panel is not shown');
      await page.click('#tab-look');
      expectTrue(await page.locator('#panel-look').isVisible() && !(await page.locator('#panel-general').isVisible()), 'the second panel is not shown alone');
      await page.keyboard.press('ArrowRight');
      expect(await page.evaluate(() => document.activeElement.id), 'tab-notify', 'focus after ArrowRight');
    });
    await check('the theme chosen in the settings (data-theme-set) is applied and the pressed button follows', async () => {
      await page.click('#tab-look');
      await page.click('[data-theme-set="dark"]');
      expect(await page.evaluate(() => document.documentElement.getAttribute('data-theme')), 'dark', 'data-theme');
      expect(await page.getAttribute('[data-theme-set="dark"]', 'aria-pressed'), 'true', 'aria-pressed of dark');
      expect(await page.getAttribute('[data-theme-set="system"]', 'aria-pressed'), 'false', 'aria-pressed of system');
    });
    await check('a preview frame does not save its theme for the whole site', async () => {
      expect(await page.evaluate(() => localStorage.getItem('mat-theme')), null, 'localStorage');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/');
    await check('on the site itself the choice is saved and a reload keeps it', async () => {
      await page.click('[data-theme-toggle]');
      await page.click('[data-theme-toggle]');
      expect(await page.evaluate(() => localStorage.getItem('mat-theme')), 'dark', 'saved');
      await page.reload({ waitUntil: 'networkidle' });
      expect(await page.evaluate(() => document.documentElement.getAttribute('data-theme')), 'dark', 'after a reload');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the texts the script writes, in two languages
  console.log('\nTexts in two languages');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=settings');
    const label = () => page.evaluate(() => document.querySelector('[data-theme-toggle]').getAttribute('aria-label'));
    await check('the label of the theme button is English on an English page, and the tooltip says the same', async () => {
      expect(await label(), 'Theme: System. Click for Light.', 'the label at the start');
      await page.click('[data-theme-toggle]');
      expect(await label(), 'Theme: Light. Click for Dark.', 'the label after one click');
      expect(await page.getAttribute('[data-theme-toggle]', 'title'), 'Theme: Light. Click for Dark.', 'the tooltip');
    });
    await check('on a German page it is German, whatever the server rendered', async () => {
      await page.evaluate(() => { document.documentElement.lang = 'de'; });
      await page.click('[data-theme-toggle]');
      expect(await label(), 'Design: Dunkel. Klicken für System.', 'the German label');
      await page.evaluate(() => MatUi.theme.set('light'));
      expect(await label(), 'Design: Hell. Klicken für Dunkel.', 'the German label of light');
    });
    await check('<html data-theme-label> replaces the whole text and data-theme-name-* the names, also with a "$" in them', async () => {
      await page.evaluate(() => {
        const r = document.documentElement;
        r.setAttribute('data-theme-label', '{0} / {1}');
        r.setAttribute('data-theme-name-light', 'Day');
        r.setAttribute('data-theme-name-dark', 'Night $&');
        MatUi.theme.set('light');
      });
      expect(await label(), 'Day / Night $&', 'the replaced label');
      await page.evaluate(() => { document.documentElement.removeAttribute('data-theme-label'); MatUi.theme.set('dark'); });
      expect(await label(), 'Design: Night $&. Klicken für System.', 'only the names replaced');
    });
    await check('the texts of the pickers follow the same rule: data-pk-<name> replaces AM, the placeholder of a typed time is a text too', async () => {
      await page.goto(BASE + '/Components#time-picker', { waitUntil: 'networkidle' });
      await page.evaluate(() => document.documentElement.setAttribute('data-pk-am', 'vorm.'));
      await page.click('#pk-time12-trigger');
      const column = '#pk-time12-trigger ~ .mat-picker__panel [data-time-col="ap"] .mat-timecol__item';
      expect((await page.locator(column).allTextContents()).map(t => t.trim()), ['vorm.', 'PM'], 'the AM and PM column');
      expect(await page.getAttribute('#pk-time12-trigger ~ .mat-picker__panel [data-time-entry]', 'placeholder'), 'hh:mm am', 'the placeholder of a typed time');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- colour schemes (COLOR-09)
  console.log('\nColour schemes');
  {
    // What a scheme may change; and what it never does.
    const SURFACES = ['--mat-bg', '--mat-bg-2', '--mat-surface', '--mat-surface-2', '--mat-surface-3', '--mat-glass', '--mat-nav-bg', '--mat-overlay', '--mat-border', '--mat-border-strong',
      '--mat-text', '--mat-text-muted', '--mat-text-faint', '--mat-danger-text', '--mat-success-text', '--mat-warning-text', '--mat-info-text', '--mat-shadow-1', '--mat-shadow-2', '--mat-shadow-3',
      '--mat-scroll-thumb', '--mat-accent-text-keep'];
    const ACCENTS = ['--mat-accent', '--mat-accent-hover', '--mat-accent-active', '--mat-accent-soft', '--mat-accent-soft-strong', '--mat-on-accent', '--mat-selection'];
    const BACKGROUND = {
      standard: { light: 'rgb(245, 244, 241)', dark: 'rgb(21, 21, 20)' },
      paper: { light: 'rgb(243, 238, 227)', dark: 'rgb(27, 24, 20)' },
      contrast: { light: 'rgb(255, 255, 255)', dark: 'rgb(0, 0, 0)' },
      oled: { light: 'rgb(245, 244, 241)', dark: 'rgb(0, 0, 0)' },
    };
    const sample = (scheme, theme) => `/Preview?app=matpaper&view=list${theme ? '&theme=' + theme : ''}${scheme ? '&scheme=' + scheme : ''}`;
    const look = page => page.evaluate(([surfaces, accents]) => {
      const root = getComputedStyle(document.documentElement);
      const resolve = name => { const probe = document.createElement('i'); probe.style.color = `var(${name})`; document.body.appendChild(probe); const c = getComputedStyle(probe).color; probe.remove(); return c; };
      return { bg: getComputedStyle(document.body).backgroundColor, tokens: surfaces.map(n => root.getPropertyValue(n).trim()), accents: [...accents.map(resolve), root.getPropertyValue('--mat-focus-ring').trim()] };
    }, [SURFACES, ACCENTS]);
    // the colour that is painted for a CSS colour (a computed colour can be written as oklab() or color()), as three numbers
    const painted = (page, colour) => page.evaluate(c => { const cv = document.createElement('canvas'); cv.width = 1; cv.height = 1; const g = cv.getContext('2d', { willReadFrequently: true }); g.fillStyle = c; g.fillRect(0, 0, 1, 1); return Array.from(g.getImageData(0, 0, 1, 1).data).slice(0, 3); }, colour);

    await check('every scheme changes the page background, in light and in dark (the standard look is the first row of each)', async () => {
      for (const scheme of ['standard', 'paper', 'contrast', 'oled']) {
        for (const theme of ['light', 'dark']) {
          const { ctx, page } = await open(desktopOptions, sample(scheme === 'standard' ? null : scheme, theme));
          expect((await look(page)).bg, BACKGROUND[scheme][theme], `${scheme} in ${theme}`);
          await ctx.close();
        }
      }
    });
    await check('oled in light mode is the standard look: every token that a scheme can set has the standard value', async () => {
      const standard = await open(desktopOptions, sample(null, 'light'));
      const oled = await open(desktopOptions, sample('oled', 'light'));
      expect((await look(oled.page)).tokens, (await look(standard.page)).tokens, 'the tokens of oled in light');
      await standard.ctx.close();
      await oled.ctx.close();
    });
    await check('a scheme never changes an accent token: the accent, its hover, its tints, the text on it and the focus ring are the same in every scheme and mode', async () => {
      for (const theme of ['light', 'dark']) {
        const base = await open(desktopOptions, sample(null, theme));
        const expected = (await look(base.page)).accents;
        await base.ctx.close();
        for (const scheme of ['paper', 'contrast', 'oled']) {
          const { ctx, page } = await open(desktopOptions, sample(scheme, theme));
          expect((await look(page)).accents, expected, `${scheme} in ${theme}`);
          await ctx.close();
        }
      }
    });
    await check('"system" follows the operating system: a scheme is its dark values while the system is dark (and oled is the standard look while it is light)', async () => {
      const dark = await open({ ...desktopOptions, colorScheme: 'dark' }, sample('paper', null));
      expect((await look(dark.page)).bg, BACKGROUND.paper.dark, 'paper, system dark');
      await dark.ctx.close();
      const oledDark = await open({ ...desktopOptions, colorScheme: 'dark' }, sample('oled', null));
      expect((await look(oledDark.page)).bg, BACKGROUND.oled.dark, 'oled, system dark');
      await oledDark.ctx.close();
      const light = await open({ ...desktopOptions, colorScheme: 'light' }, sample('oled', null));
      expect((await look(light.page)).bg, BACKGROUND.standard.light, 'oled, system light');
      await light.ctx.close();
      const paperLight = await open({ ...desktopOptions, colorScheme: 'light' }, sample('paper', null));
      expect((await look(paperLight.page)).bg, BACKGROUND.paper.light, 'paper, system light');
      await paperLight.ctx.close();
    });
    await check('the contrast scheme draws lines instead of shadows: a card has no shadow and a dark border, a menu has a line', async () => {
      const { ctx, page } = await open(desktopOptions, sample('contrast', 'light'));
      const card = await page.evaluate(() => { const s = getComputedStyle(document.querySelector('.mat-toolbar')); return { shadow: s.boxShadow, border: s.borderTopColor }; });
      expect(card, { shadow: 'none', border: 'rgb(92, 92, 92)' }, 'a card');
      const control = await page.evaluate(() => { const probe = document.createElement('input'); probe.className = 'mat-input'; document.body.appendChild(probe); const s = getComputedStyle(probe); const v = { border: s.borderTopColor }; probe.remove(); return v; });
      expect(control, { border: 'rgb(31, 31, 31)' }, 'the border of a control');
      expect(await page.evaluate(() => getComputedStyle(document.getElementById('user-menu')).boxShadow), 'rgb(31, 31, 31) 0px 0px 0px 1px', 'a menu has a line, not a shadow');
      await ctx.close();
    });
    await check('the numbers on the Builder page are what the browser draws: the link on a surface in paper is the colour of a link on paper', async () => {
      const numbers = await open(desktopOptions, '/Builder?app=matpaper&scheme=paper');
      const rows = await numbers.page.evaluate(() => [...document.querySelectorAll('section[aria-labelledby="scheme-contrast-title"] tbody tr')].map(r => ({ name: r.querySelector('td')?.textContent.trim(), hex: r.querySelector('code')?.textContent.trim() })).filter(r => r.name));
      await numbers.ctx.close();
      for (const [theme, label] of [['light', 'Link on a surface (light)'], ['dark', 'Link on a surface (dark)']]) {
        const wanted = rows.find(r => r.name.startsWith(label));
        expectTrue(wanted && /^#[0-9a-f]{6}$/.test(wanted.hex), `no row "${label}" in the report: ${JSON.stringify(rows.map(r => r.name))}`);
        const { ctx, page } = await open(desktopOptions, sample('paper', theme));
        const drawn = await page.evaluate(() => { const a = document.createElement('a'); a.href = '#'; a.textContent = 'x'; document.querySelector('.mat-page').appendChild(a); const c = getComputedStyle(a).color; a.remove(); return c; });
        const [r, g, b] = await painted(page, drawn);
        const [wr, wg, wb] = [1, 3, 5].map(i => parseInt(wanted.hex.slice(i, i + 2), 16));
        expectTrue(Math.abs(r - wr) <= 2 && Math.abs(g - wg) <= 2 && Math.abs(b - wb) <= 2, `${label}: the report says ${wanted.hex}, the browser draws ${drawn}`);
        await ctx.close();
      }
    });
    {
      const { ctx, page } = await open(desktopOptions, '/Foundations');
      await ctx.addInitScript(() => {
        // when the attribute is set: before the page has been parsed and before matui.js exists, so the boot script did it and nothing flashes
        // (an init script runs before the page has an <html>: the observer watches the document and the attribute of whatever element appears)
        new MutationObserver(() => {
          if (document.documentElement && document.documentElement.hasAttribute('data-scheme') && !window.__scheme) { window.__scheme = { ready: document.readyState, script: !!window.MatUi }; }
        }).observe(document, { attributes: true, subtree: true, attributeFilter: ['data-scheme'] });
      });
      await check('the choice of the colour page (data-scheme-set) is applied to the whole site at once, the pressed button follows and the choice is saved in the browser', async () => {
        expect(await page.getAttribute('[data-scheme-set="standard"]', 'aria-pressed'), 'true', 'standard is pressed at the start');
        await page.evaluate(() => { window.__events = []; document.documentElement.addEventListener('mat:scheme', e => window.__events.push(e.detail.scheme)); });
        await page.click('#schemes [data-scheme-set="paper"]');
        expect(await page.evaluate(() => document.documentElement.getAttribute('data-scheme')), 'paper', 'data-scheme');
        expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor), BACKGROUND.paper.light, 'the page background');
        expect(await page.getAttribute('#schemes [data-scheme-set="paper"]', 'aria-pressed'), 'true', 'paper is pressed');
        expect(await page.getAttribute('#schemes [data-scheme-set="standard"]', 'aria-pressed'), 'false', 'standard is not pressed');
        expect(await page.evaluate(() => localStorage.getItem('mat-scheme')), 'paper', 'saved');
        expect(await page.evaluate(() => window.__events), ['paper'], 'the event mat:scheme');
      });
      await check('after a reload the boot script has applied the saved scheme before the page is parsed, and the page starts in it', async () => {
        const fresh = await ctx.newPage();
        await fresh.goto(BASE + '/Foundations', { waitUntil: 'networkidle' });
        expect(await fresh.evaluate(() => window.__scheme), { ready: 'loading', script: false }, 'when data-scheme was first there');
        expect(await fresh.evaluate(() => getComputedStyle(document.body).backgroundColor), BACKGROUND.paper.light, 'the page background after a reload');
        expect(await fresh.getAttribute('[data-scheme-set="paper"]', 'aria-pressed'), 'true', 'the pressed button after a reload');
        await fresh.close();
      });
      await check('MatUi.scheme.get and set work, "standard" and null take the attribute away and forget the choice, anything else is the standard look', async () => {
        expect(await page.evaluate(() => MatUi.scheme.get()), 'paper', 'get');
        await page.evaluate(() => MatUi.scheme.set('oled'));
        expect(await page.evaluate(() => [MatUi.scheme.get(), document.documentElement.getAttribute('data-scheme'), localStorage.getItem('mat-scheme')]), ['oled', 'oled', 'oled'], 'oled');
        await page.evaluate(() => MatUi.scheme.set('standard'));
        expect(await page.evaluate(() => [MatUi.scheme.get(), document.documentElement.getAttribute('data-scheme'), localStorage.getItem('mat-scheme')]), ['standard', null, null], 'standard');
        await page.click('#schemes [data-scheme-set="contrast"]');
        await page.evaluate(() => MatUi.scheme.set(null));
        expect(await page.evaluate(() => [MatUi.scheme.get(), document.documentElement.getAttribute('data-scheme'), localStorage.getItem('mat-scheme')]), ['standard', null, null], 'null');
        await page.evaluate(() => document.documentElement.setAttribute('data-scheme', 'nonsense'));
        expect(await page.evaluate(() => MatUi.scheme.get()), 'standard', 'an unknown value is the standard look');
        expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor), BACKGROUND.standard.light, 'and looks like it');
        await page.evaluate(() => document.documentElement.removeAttribute('data-scheme'));
      });
      await check('data-scheme-save-url posts scheme=<name> with the anti-forgery token of the page, from the button or from the group around it', async () => {
        await page.evaluate(() => {
          document.body.insertAdjacentHTML('beforeend',
            '<form id="tok"><input type="hidden" name="__RequestVerificationToken" value="tok-123" /></form>' +
            '<div id="seg" data-scheme-save-url="/save-scheme-test"><button type="button" id="seg-contrast" data-scheme-set="contrast">High contrast</button><button type="button" id="seg-standard" data-scheme-set="standard">Standard</button></div>');
        });
        const posted = [];
        await page.route('**/save-scheme-test', route => { posted.push({ method: route.request().method(), body: route.request().postData() }); route.fulfill({ status: 200, body: 'ok' }); });
        await page.dispatchEvent('#seg-contrast', 'click');
        await page.dispatchEvent('#seg-standard', 'click');
        await page.waitForTimeout(300);
        expect(posted, [{ method: 'POST', body: 'scheme=contrast&__RequestVerificationToken=tok-123' }, { method: 'POST', body: 'scheme=standard&__RequestVerificationToken=tok-123' }], 'what was posted');
      });
      await ctx.close();
    }
    {
      const { ctx, page } = await open(desktopOptions, '/Preview?app=matpaper&view=settings');
      await check('a preview frame shows the chosen scheme but does not keep it for the whole site; the settings of the sample app have the control', async () => {
        await page.click('#tab-look');
        expect(await page.locator('[data-scheme-set]').count(), 4, 'the buttons of the control');
        await page.click('[data-scheme-set="contrast"]');
        expect(await page.evaluate(() => document.documentElement.getAttribute('data-scheme')), 'contrast', 'data-scheme');
        expect(await page.getAttribute('[data-scheme-set="contrast"]', 'aria-pressed'), 'true', 'pressed');
        expect(await page.evaluate(() => localStorage.getItem('mat-scheme')), null, 'localStorage');
      });
      await ctx.close();
    }
  }

  // ---------------------------------------------------------------------------------------------------- the builder
  console.log('\nBuilder');
  {
    const { ctx, page } = await open(desktopOptions, '/Builder?app=matmail');
    await check('choosing another app applies at once and the preview takes its name', async () => {
      await Promise.all([page.waitForNavigation(), page.selectOption('#b-app', 'matsplit')]);
      expectTrue(page.url().includes('app=matsplit'), 'the address does not carry the app: ' + page.url());
      expectTrue((await page.locator('iframe.doc-frame').first().getAttribute('src')).includes('app=matsplit'), 'the frame shows another app');
    });
    await check('a preset keeps the app and sets the accent; the report names the contrast', async () => {
      await page.click('a:has-text("Ocean")');
      await page.waitForLoadState('networkidle');
      expectTrue(page.url().includes('accent=%231a73e8'), 'the preset is not in the address: ' + page.url());
      expectTrue((await page.textContent('#contrast-title + .mat-pill')).includes('AA'), 'no verdict');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the menu
  console.log('\nThe menu: badges, submenus, switching');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=list');
    const branchState = () => page.evaluate(() => {
      const b = document.querySelector('[data-nav-branch="items"]');
      const sub = document.getElementById(b.getAttribute('aria-controls'));
      return { expanded: b.getAttribute('aria-expanded'), open: b.closest('.mat-nav__branch').classList.contains('is-open'), visible: getComputedStyle(sub).visibility };
    });
    await check('the branch with the current page is open when the page loads and its child is marked', async () => {
      expect(await branchState(), { expanded: 'true', open: true, visible: 'visible' }, 'branch');
      expect(await page.locator('.mat-nav__item--sub[aria-current="page"]').textContent().then(t => t.trim().replace(/\s+/g, ' ')), 'All items 12 items', 'current child');
    });
    await check('a parent opens and closes its children, and the closed children cannot be reached with the keyboard', async () => {
      await page.click('[data-nav-branch="items"]');
      await page.waitForTimeout(350);
      expect(await branchState(), { expanded: 'false', open: false, visible: 'hidden' }, 'closed');
      await page.click('[data-nav-branch="items"]');
      await page.waitForTimeout(350);
      expect((await branchState()).expanded, 'true', 'open again');
    });
    await check('the closed parent shows the sum of its children badges, the open one does not', async () => {
      const sum = page.locator('[data-nav-branch="items"] .mat-nav__badge--sum');
      expect(await sum.count(), 1, 'sum badges');
      expectTrue(await sum.isHidden(), 'the sum is shown while the branch is open');
      await page.click('[data-nav-branch="items"]');
      await page.waitForTimeout(350);
      expectTrue(await sum.isVisible(), 'the sum is not shown while the branch is closed');
      expect((await sum.textContent()).trim().split(/\s/)[0], '15', 'sum');
      await page.click('[data-nav-branch="items"]');
    });
    await check('what the user closed is remembered after a reload; the branch with the current page stays open', async () => {
      await page.click('[data-nav-branch="items"]');
      await page.goto(BASE + '/Preview?app=matmail&view=dashboard', { waitUntil: 'networkidle' });
      expect((await branchState()).expanded, 'false', 'closed branch after a reload on another page');
      await page.goto(BASE + '/Preview?app=matmail&view=list', { waitUntil: 'networkidle' });
      expect((await branchState()).expanded, 'true', 'the current page keeps its branch open');
    });
    await check('arrow keys walk the items; right opens a parent, left goes back to it', async () => {
      await page.click('[data-nav-branch="items"]');
      await page.focus('[data-nav-branch="items"]');
      await page.keyboard.press('ArrowRight');
      expect((await branchState()).expanded, 'true', 'opened by ArrowRight');
      await page.keyboard.press('ArrowRight');
      expect(await page.evaluate(() => document.activeElement.textContent.trim().replace(/\s+/g, ' ')), 'All items 12 items', 'first child');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.textContent.trim().split(/\s/)[0]), 'Needs', 'next child');
      await page.keyboard.press('ArrowLeft');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-nav-branch')), 'items', 'back to the parent');
      await page.keyboard.press('ArrowLeft');
      expect((await branchState()).expanded, 'false', 'closed by ArrowLeft');
      await page.keyboard.press('Home');
      expect(await page.evaluate(() => document.activeElement.textContent.trim()), 'Dashboard', 'Home');
    });
    await check('the switcher shows the current menu and a dot because Administration needs attention', async () => {
      expect((await page.textContent('.mat-switcher__name')).trim(), 'Work', 'name');
      expectTrue(await page.locator('.mat-switcher__dot').isVisible(), 'no dot');
    });
    await check('choosing Administration swaps the items without loading a page', async () => {
      const before = page.url();
      await page.click('.mat-switcher');
      await page.click('[data-nav-set="admin"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('.mat-switcher__name')).trim(), 'Administration', 'name');
      expect(page.url(), before, 'the address');
      expectTrue(await page.locator('[data-nav-panel="admin"]').isVisible() && !(await page.locator('[data-nav-panel="work"]').isVisible()), 'panels');
      expect(await page.getAttribute('[data-nav-set="admin"]', 'aria-checked'), 'true', 'checked');
      const items = await page.locator('[data-nav-panel="admin"] .mat-nav__item').allTextContents();
      expectTrue(items.some(t => t.includes('Users')) && items.some(t => t.includes('Audit log')), 'the items of the administration');
    });
    await check('a page of the administration is loaded with the administration menu shown', async () => {
      await page.click('[data-nav-panel="admin"] a.mat-nav__item[href*="area=users"]');
      await page.waitForLoadState('networkidle');
      expect((await page.textContent('.mat-switcher__name')).trim(), 'Administration', 'name after the page loaded');
      expect(await page.getAttribute('a[href*="area=users"].mat-nav__item', 'aria-current'), 'page', 'current item');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components#switcher');
    const dot = () => page.locator('#switch-demo .mat-switcher__dot').isVisible();
    await check('the dot on the switcher is there while the other menu needs attention and goes when it is shown', async () => {
      expectTrue(await dot(), 'no dot while Work is shown');
      await page.click('#switch-demo .mat-switcher');
      await page.click('#switch-demo-menu [data-nav-set="admin"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('#switch-demo .mat-switcher__name')).trim(), 'Administration', 'name');
      expectTrue(!(await dot()), 'the dot is still there while the menu that needs attention is shown');
      expectTrue(await page.locator('#switch-demo [data-nav-panel="admin"]').isVisible(), 'the items of the administration');
      expectTrue(await page.locator('.mat-nav [data-nav-panel="toolkit"]').isVisible(), 'the menu of the page was swapped by an example');
    });
    await ctx.close();
  }
  {
    // SHELL-02: a group heading can carry one small icon action at its right end; the row keeps its height
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=list&groupaction=1');
    await check('the action stands at the right end of the heading and the heading is not taller than one without an action', async () => {
      const row = await page.locator('.mat-nav__group--action').boundingBox();
      const action = await page.locator('.mat-nav__group-action').boundingBox();
      const title = await page.locator('.mat-nav__group--action .mat-nav__group-title').boundingBox();
      const plain = await page.locator('.mat-nav__scroll:not([hidden]) .mat-nav__group:not(.mat-nav__group--action)').first().boundingBox();
      const nav = await page.locator('.mat-nav').boundingBox();
      expectTrue(action.x > title.x + title.width - 1, 'the action is not after the title');
      expectTrue(action.x + action.width <= nav.x + nav.width + 1, 'the action sticks out of the menu');
      expectTrue(action.width >= 24 && action.height >= 24, 'the target is smaller than 24 px: ' + action.width + 'x' + action.height);
      expectTrue(Math.abs(action.y + action.height / 2 - (title.y + title.height / 2)) < 6, 'the action is not centred on the line of the title');
      if (plain) { expectTrue(Math.abs(row.height - plain.height) <= 6, 'the heading with an action is taller: ' + row.height + ' against ' + plain.height); }
    });
    await check('the action is a link with a name, it is reached by the Tab key and it leads to its page', async () => {
      expect(await page.getAttribute('.mat-nav__group-action', 'aria-label'), 'Manage lists', 'name');
      await page.focus('.mat-nav__group-action');
      expectTrue(await page.evaluate(() => document.activeElement.matches('.mat-nav__group-action:focus-visible')), 'no focus ring');
      await Promise.all([page.waitForURL(/area=lists/), page.click('.mat-nav__group-action')]);
    });
    await check('on its own page the action is marked like a current item: aria-current, the tint of the accent behind it, the accent on its icon, and no item is marked at the same time', async () => {
      const state = await page.evaluate(() => {
        const probe = (prop, value) => { const t = document.createElement('i'); t.style.setProperty(prop, value); document.body.appendChild(t); const c = getComputedStyle(t).getPropertyValue(prop); t.remove(); return c; };
        const a = document.querySelector('.mat-nav__group-action');
        const s = getComputedStyle(a);
        return {
          current: a.getAttribute('aria-current'), active: a.classList.contains('is-active'), bg: s.backgroundColor, colour: s.color,
          tint: probe('background-color', 'var(--mat-accent-soft-strong)'), accentText: probe('color', 'var(--mat-accent-text)'),
          items: document.querySelectorAll('.mat-nav__item[aria-current="page"]').length,
        };
      });
      expect(state.current, 'page', 'aria-current');
      expectTrue(state.active, 'no is-active class');
      expect(state.bg, state.tint, 'the tint behind the action');
      expect(state.colour, state.accentText, 'the colour of its icon');
      expect(state.items, 0, 'items marked as current at the same time');
    });
    await check('on another page the action is not marked', async () => {
      await page.goto(BASE + '/Preview?app=matmail&view=list&groupaction=1', { waitUntil: 'networkidle' });
      expectTrue(!(await page.getAttribute('.mat-nav__group-action', 'aria-current')), 'aria-current on a page that is not its own');
      expectTrue(!(await page.locator('.mat-nav__group-action').evaluate(a => a.classList.contains('is-active'))), 'is-active on a page that is not its own');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Preview?app=matmail&view=list&groupaction=1');
    await check('on a touch screen the action has a target of 44 px and the heading keeps its height', async () => {
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      const action = await page.locator('.mat-nav__group-action').boundingBox();
      const row = await page.locator('.mat-nav__group--action').boundingBox();
      const plain = await page.locator('.mat-nav__scroll:not([hidden]) .mat-nav__group:not(.mat-nav__group--action)').first().boundingBox();
      expectTrue(action.width >= 43.5 && action.height >= 43.5, 'the target is smaller than 44 px: ' + action.width + 'x' + action.height);
      if (plain) { expectTrue(Math.abs(row.height - plain.height) <= 6, 'the heading with an action is taller: ' + row.height + ' against ' + plain.height); }
    });
    await ctx.close();
  }
  {
    // the variant that opens a menu: documented in the components
    const { ctx, page } = await open(desktopOptions, '/Components#navmenu');
    const button = '#navmenu button.mat-nav__group-action';
    await check('a group action can open a menu: the popup is shown, the button says so, Escape closes it', async () => {
      expect(await page.getAttribute(button, 'aria-haspopup'), 'menu', 'haspopup');
      expect(await page.getAttribute(button, 'aria-expanded'), 'false', 'closed');
      await page.click(button);
      expect(await page.getAttribute(button, 'aria-expanded'), 'true', 'opened');
      expectTrue(await page.locator('#demo-folders-menu').isVisible(), 'the popup is not shown');
      await page.keyboard.press('Escape');
      expectTrue(await page.locator('#demo-folders-menu').isHidden(), 'the popup stays open after Escape');
    });
    await ctx.close();
  }
  {
    // SHELL-15: choices (the tenant) are a closed parent with a form button each, between the menus and the administration
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=list&switcher=bottom&tenants=1');
    const parent = '#nav-switch-menu [aria-controls="nav-switch-choices-tenants"]';
    await check('the tenants are a closed parent between the menus and the administration, and open to a form button each', async () => {
      await page.click('.mat-nav__foot .mat-switcher');
      const order = await page.evaluate(() => Array.from(document.querySelectorAll('#nav-switch-menu > *')).map(e => e.getAttribute('data-nav-set') || (e.hasAttribute('data-nav-group') ? 'group:' + e.getAttribute('aria-controls') : e.tagName)));
      expect(order, ['work', 'group:nav-switch-choices-tenants', 'DIV', 'admin'], 'order of the popup');
      expect(await page.getAttribute(parent, 'aria-expanded'), 'false', 'closed');
      expectTrue(!(await page.locator('#nav-switch-choices-tenants').isVisible()), 'the choices are visible while the parent is closed');
      await page.click(parent);
      expect(await page.getAttribute(parent, 'aria-expanded'), 'true', 'opened');
      const choices = await page.locator('#nav-switch-choices-tenants button').evaluateAll(els => els.map(e => e.textContent.trim() + ':' + e.getAttribute('aria-checked') + ':' + e.type));
      expect(choices, ['Muster GmbH:true:submit', 'Beispiel AG:false:submit', 'Dritte KG:false:submit'], 'the choices');
      expect(await page.locator('#nav-switch-choices-tenants form.mat-menu__form[method="post"]').count(), 3, 'forms');
      expectTrue(await page.locator('#nav-switch-menu').isVisible(), 'the popup closed while the parent was opened');
      const box = await page.locator('#nav-switch-menu').boundingBox();
      const switcher = await page.locator('.mat-nav__foot .mat-switcher').boundingBox();
      expectTrue(box.y + box.height <= switcher.y + 2, 'the popup covers the switcher it opened from');
    });
    await check('the arrow keys walk the menus, the choices and the administration; Escape closes the popup', async () => {
      await page.focus(parent);
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.textContent.trim()), 'Muster GmbH', 'first choice');
      await page.keyboard.press('ArrowDown'); await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.textContent.trim()), 'Dritte KG', 'third choice');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-nav-set')), 'admin', 'the administration is last');
      await page.keyboard.press('Escape');
      expectTrue(await page.locator('#nav-switch-menu').isHidden(), 'the popup stays open after Escape');
    });
    await ctx.close();
  }
  {
    // SHELL-15: an entry that is a link is the start page of its menu: the browser loads it, the script does not swap the items
    const { ctx, page } = await open(desktopOptions, '/Components#switcher');
    const entry = '#switch-pages-menu [data-nav-set="admin"]';
    await check('an entry of the switcher that is a link loads its page and does not swap the items', async () => {
      await page.click('#switch-pages-demo .mat-switcher');
      expect(await page.evaluate(sel => document.querySelector(sel).tagName, entry), 'A', 'the entry is a link');
      expect(await page.getAttribute(entry, 'role'), 'menuitemradio', 'role');
      await page.click(entry);
      await page.waitForTimeout(250);
      expectTrue(await page.locator('#switch-pages-menu').isHidden(), 'the popup stays open after the choice');
      expect((await page.textContent('#switch-pages-demo .mat-switcher__name')).trim(), 'Work', 'the items were swapped: the page that loads draws its menu');
      expect(await page.getAttribute('#switch-pages-menu [data-nav-set="work"]', 'aria-checked'), 'true', 'the checked entry stays until the page is loaded');
      expectTrue(page.url().endsWith('#'), 'the link was not followed: ' + page.url());
    });
    await check('a link in the popup of the switcher is chosen with the arrow keys and the Space key like a button', async () => {
      await page.click('#switch-pages-demo .mat-switcher');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-nav-set')), 'work', 'the first entry has the focus when the popup opens');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-nav-set')), 'admin', 'the entry that has the focus');
      await page.keyboard.press(' ');
      await page.waitForTimeout(250);
      expectTrue(await page.locator('#switch-pages-menu').isHidden(), 'the popup stays open after the Space key');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Preview?app=matmail&view=list');
    await check('on a phone a parent opens its children without closing the drawer; a link closes it', async () => {
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      // the branch of the current page is open: closing and opening it must leave the drawer where it is
      await page.click('[data-nav-branch="items"]');
      await page.waitForTimeout(350);
      expect(await page.evaluate(() => document.querySelector('.mat-app').classList.contains('is-nav-open')), true, 'drawer after closing a parent');
      await page.click('[data-nav-branch="items"]');
      await page.waitForTimeout(350);
      expect(await page.evaluate(() => document.querySelector('.mat-app').classList.contains('is-nav-open')), true, 'drawer after opening a parent');
      await page.click('.mat-nav__item--sub:has-text("Gallery")');
      await page.waitForLoadState('networkidle');
      expect(await page.evaluate(() => document.querySelector('.mat-app').classList.contains('is-nav-open')), false, 'drawer after following a link');
    });
    await check('the switcher works in the drawer: its popup is above the drawer', async () => {
      await page.goto(BASE + '/Preview?app=matmail&view=list', { waitUntil: 'networkidle' });
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      await page.click('.mat-switcher');
      await page.waitForTimeout(250);
      const above = await page.evaluate(() => { const m = document.getElementById('nav-switch-menu').getBoundingClientRect(); const hit = document.elementFromPoint(m.x + m.width / 2, m.y + 12); return !!(hit && hit.closest('#nav-switch-menu')); });
      expectTrue(above, 'the popup is hidden behind the drawer');
      await page.click('[data-nav-set="admin"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('.mat-switcher__name')).trim(), 'Administration', 'name');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the switcher at the foot, menus in groups (SHELL-15)
  console.log('\nThe switcher at the foot and menus in groups');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=dashboard&switcher=bottom&menus=tree');
    const popup = () => page.locator('#nav-switch-menu').boundingBox();
    await check('the switcher is at the foot directly above the user and not under the logo', async () => {
      expect(await page.locator('.mat-switcher').count(), 1, 'switchers');
      const sw = await page.locator('.mat-nav__foot .mat-switcher').boundingBox();
      const user = await page.locator('.mat-user').boundingBox();
      expectTrue(sw && user && sw.y + sw.height <= user.y + 1, 'the switcher is not above the user');
      expect(await page.locator('.mat-nav__brand ~ .mat-nav__switch').count(), 0, 'a switcher under the logo');
    });
    await check('its popup opens upwards and still does not cover the switcher after a group was opened or closed', async () => {
      await page.click('.mat-switcher');
      const btn = await page.locator('.mat-switcher').boundingBox();
      let p = await popup();
      expectTrue(p.y + p.height <= btn.y + 1, 'the popup is not above the button');
      expect(await page.getAttribute('[data-nav-group]', 'aria-expanded'), 'false', 'the group of another menu is closed');
      await page.click('[data-nav-group]');
      await page.waitForTimeout(150);
      p = await popup();
      expect(await page.getAttribute('[data-nav-group]', 'aria-expanded'), 'true', 'opened by the click');
      expectTrue(p.y + p.height <= btn.y + 1, 'the popup covers the button after a group was opened');
      expectTrue(await page.locator('#nav-switch-menu').isVisible(), 'the parent closed the popup');
    });
    await check('choosing a menu of a group swaps the items and names the parent above the menu', async () => {
      await page.click('[data-nav-set="people"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('.mat-switcher__name')).trim(), 'People', 'name');
      expect((await page.textContent('.mat-switcher__text small')).trim(), 'Administration', 'the parent');
      expectTrue(await page.locator('[data-nav-panel="people"]').isVisible() && !(await page.locator('[data-nav-panel="work"]').isVisible()), 'panels');
      expectTrue(!(await page.locator('#nav-switch-menu').isVisible()), 'the popup stays open after a menu was chosen');
      await page.click('.mat-switcher');
      await page.click('[data-nav-set="work"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('.mat-switcher__text small')).trim(), 'Menu', 'the label when the menu has no parent');
    });
    await check('a closed parent carries the dot of the menus inside it, an open one does not', async () => {
      await page.click('.mat-switcher');
      const dot = page.locator('[data-nav-group] .mat-nav__badge--sum');
      expect(await page.getAttribute('[data-nav-group]', 'aria-expanded'), 'true', 'the group stays open');
      expectTrue(!(await dot.isVisible()), 'a dot on an open parent');
      await page.click('[data-nav-group]');
      expectTrue(await dot.isVisible(), 'no dot on the closed parent');
      await page.keyboard.press('Escape');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=dashboard&switcher=bottom&menus=tree');
    const focused = () => page.evaluate(() => (document.activeElement.getAttribute('data-nav-set') || (document.activeElement.hasAttribute('data-nav-group') ? 'group' : '?')));
    const expanded = () => page.getAttribute('[data-nav-group]', 'aria-expanded');
    await check('the arrow keys walk the popup: a closed parent hides its menus, right opens it and goes in, left goes back and closes', async () => {
      await page.click('.mat-switcher');
      expect(await focused(), 'work', 'the first entry');
      await page.keyboard.press('ArrowDown');
      expect(await focused(), 'group', 'the parent');
      await page.keyboard.press('ArrowDown');
      expect(await focused(), 'work', 'wraps around: the menus of a closed parent are not walked');
      await page.keyboard.press('ArrowUp');
      await page.keyboard.press('ArrowRight');
      expect(await expanded(), 'true', 'opened by ArrowRight');
      await page.keyboard.press('ArrowRight');
      expect(await focused(), 'people', 'first menu of the group');
      await page.keyboard.press('ArrowDown');
      expect(await focused(), 'system', 'next menu');
      await page.keyboard.press('ArrowLeft');
      expect(await focused(), 'group', 'back to the parent');
      expect(await expanded(), 'false', 'closed by ArrowLeft');
      await page.keyboard.press('Escape');
      expectTrue(!(await page.locator('#nav-switch-menu').isVisible()), 'Escape does not close the popup');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=dashboard&area=backups&menus=tree');
    await check('a page of a menu in a group loads with the group open and the parent named above the menu', async () => {
      expect((await page.textContent('.mat-switcher__name')).trim(), 'System', 'name');
      expect((await page.textContent('.mat-switcher__text small')).trim(), 'Administration', 'the parent');
      await page.click('.mat-switcher');
      expect(await page.getAttribute('[data-nav-group]', 'aria-expanded'), 'true', 'the group of the current menu is open');
      expect(await page.getAttribute('[data-nav-set="system"]', 'aria-checked'), 'true', 'the current menu is checked');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=dashboard&switcher=both');
    await check('with the switcher at both places the two stay in step', async () => {
      expect(await page.locator('.mat-switcher').count(), 2, 'switchers');
      await page.locator('.mat-nav__foot .mat-switcher').click();
      await page.click('[data-nav-set="admin"]');
      await page.waitForTimeout(250);
      const names = await page.locator('.mat-switcher__name').allTextContents();
      expect(names.map(n => n.trim()).join('|'), 'Administration|Administration', 'both name the menu');
      const above = await page.evaluate(() => { const b = document.querySelector('.mat-nav__brand ~ .mat-nav__switch .mat-switcher').getBoundingClientRect(); return b.y < document.querySelector('.mat-nav__foot .mat-switcher').getBoundingClientRect().y; });
      expectTrue(above, 'the one under the logo is not above the one at the foot');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- a main action, the switcher at the end, highlighted actions in the bar
  console.log('\nMain action, switcher at the end, highlighted actions');
  {
    const { ctx, page } = await open(desktopOptions, '/Preview?app=matmail&view=list&action=1&switcher=end');
    await check('the main action is the first item of the menu, filled with the accent and readable on it', async () => {
      const state = await page.evaluate(() => {
        const items = Array.from(document.querySelectorAll('.mat-nav__scroll:not([hidden]) .mat-nav__item'));
        const a = items[0];
        const s = getComputedStyle(a);
        return { first: a.classList.contains('mat-nav__item--primary'), text: a.textContent.trim(), bg: s.backgroundColor, colour: s.color, iconColour: getComputedStyle(a.querySelector('.mat-icon')).color, filled: document.querySelectorAll('.mat-nav__item--primary').length, current: a.getAttribute('aria-current') };
      });
      const probes = await accentOf(page);
      expect([state.first, state.text, state.filled], [true, 'Add item', 1], 'the first item');
      expect(state.bg, probes.accent, 'background of the main action');
      expect(state.colour, probes.onAccent, 'text of the main action');
      expect(state.iconColour, probes.onAccent, 'icon of the main action');
      expectTrue(contrastOf(state.bg, state.colour) >= 4.5, 'the text of the main action is not readable on the accent: ' + contrastOf(state.bg, state.colour).toFixed(2));
      expect(state.current, null, 'the main action is not the current page on the list page (it leads to the form)');
    });
    await check('the switcher sits at the end of the menu, above the foot, and its popup opens upwards', async () => {
      const box = () => page.evaluate(() => {
        const r = (s) => { const b = document.querySelector(s).getBoundingClientRect(); return { top: Math.round(b.top), bottom: Math.round(b.bottom) }; };
        const kids = Array.from(document.querySelector('.mat-nav').children).map(c => c.className.split(' ')[0]);
        return { brand: r('.mat-nav__brand'), panel: r('.mat-nav__scroll:not([hidden])'), sw: r('.mat-nav__switch'), foot: r('.mat-nav__foot'), kids };
      });
      const b = await box();
      expect(b.kids, ['mat-nav__brand', 'mat-nav__scroll', 'mat-nav__scroll', 'mat-nav__switch', 'mat-nav__foot'], 'order of the blocks of the menu in the markup');
      expectTrue(b.brand.bottom <= b.panel.top + 1 && b.panel.bottom <= b.sw.top + 1 && b.sw.bottom <= b.foot.top + 1, 'the blocks are not stacked brand, items, switcher, foot: ' + JSON.stringify(b));
      await page.click('.mat-switcher');
      await page.waitForTimeout(250);
      const popup = await page.evaluate(() => { const m = document.getElementById('nav-switch-menu').getBoundingClientRect(); const s = document.querySelector('.mat-switcher').getBoundingClientRect(); return { popupBottom: Math.round(m.bottom), switcherTop: Math.round(s.top), popupTop: Math.round(m.top) }; });
      expectTrue(popup.popupBottom <= popup.switcherTop && popup.popupTop >= 0, 'the popup does not open upwards and inside the window: ' + JSON.stringify(popup));
      await page.click('[data-nav-set="admin"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('.mat-switcher__name')).trim(), 'Administration', 'name');
      expectTrue(await page.locator('[data-nav-panel="admin"]').isVisible() && !(await page.locator('[data-nav-panel="work"]').isVisible()), 'panels after choosing Administration');
      const after = await box();
      expectTrue(after.panel.bottom <= after.sw.top + 1 && after.sw.bottom <= after.foot.top + 1, 'the switcher moved when the menu was swapped: ' + JSON.stringify(after));
    });
    await check('on the page of the main action it stays filled, a step darker, and stays readable', async () => {
      await page.goto(BASE + '/Preview?app=matmail&view=form&action=1&switcher=end', { waitUntil: 'networkidle' });
      const probes = await accentOf(page);
      const state = await page.evaluate(() => { const a = document.querySelector('.mat-nav__item--primary'); const s = getComputedStyle(a); return { current: a.getAttribute('aria-current'), bg: s.backgroundColor, colour: s.color }; });
      expect(state.current, 'page', 'aria-current');
      expect(state.bg, probes.active, 'background of the main action on its own page');
      expectTrue(state.bg !== probes.accent, 'it looks the same on its own page');
      expectTrue(contrastOf(state.bg, state.colour) >= 4.5, 'the text is not readable on the darker fill: ' + contrastOf(state.bg, state.colour).toFixed(2));
    });
    await check('without the options the sample menu is as it was: the switcher under the logo, no filled item', async () => {
      await page.goto(BASE + '/Preview?app=matmail&view=list', { waitUntil: 'networkidle' });
      const kids = await page.evaluate(() => Array.from(document.querySelector('.mat-nav').children).map(c => c.className.split(' ')[0]));
      expect(kids, ['mat-nav__brand', 'mat-nav__switch', 'mat-nav__scroll', 'mat-nav__scroll', 'mat-nav__foot'], 'blocks');
      expect(await page.locator('.mat-nav__item--primary').count(), 0, 'filled items');
      expect(await page.getAttribute('.mat-switcher', 'data-menu-place'), 'bottom', 'the popup opens downwards under the logo');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Preview?app=matmail&view=list&switcher=bottom&menus=tree');
    await check('on a phone the switcher at the foot of the drawer opens its popup above the drawer, inside the screen', async () => {
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      await page.click('.mat-switcher');
      await page.click('[data-nav-group]');
      await page.waitForTimeout(250);
      const ok = await page.evaluate(() => {
        const m = document.getElementById('nav-switch-menu').getBoundingClientRect();
        const hit = document.elementFromPoint(m.x + m.width / 2, m.y + 12);
        return !!(hit && hit.closest('#nav-switch-menu')) && m.y >= 0 && m.bottom <= window.innerHeight;
      });
      expectTrue(ok, 'the popup is hidden behind the drawer or leaves the screen');
      await page.click('[data-nav-set="people"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('.mat-switcher__name')).trim(), 'People', 'name');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Preview?app=matmail&view=list&action=1&switcher=end');
    await check('on a phone the drawer has the main action first and the switcher above the foot, and the popup stays on the screen', async () => {
      await page.click('[data-nav-toggle]');
      await page.waitForTimeout(450);
      const b = await page.evaluate(() => {
        const r = (s) => { const x = document.querySelector(s).getBoundingClientRect(); return { top: Math.round(x.top), bottom: Math.round(x.bottom), left: Math.round(x.left), right: Math.round(x.right) }; };
        return { first: document.querySelector('.mat-nav__scroll:not([hidden]) .mat-nav__item').classList.contains('mat-nav__item--primary'), panel: r('.mat-nav__scroll:not([hidden])'), sw: r('.mat-nav__switch'), foot: r('.mat-nav__foot'), vh: innerHeight };
      });
      expect(b.first, true, 'the main action is the first item');
      expectTrue(b.panel.bottom <= b.sw.top + 1 && b.sw.bottom <= b.foot.top + 1 && b.foot.bottom <= b.vh + 1, 'the blocks of the drawer: ' + JSON.stringify(b));
      await page.click('.mat-switcher');
      await page.waitForTimeout(250);
      const popup = await page.evaluate(() => { const m = document.getElementById('nav-switch-menu').getBoundingClientRect(); const s = document.querySelector('.mat-switcher').getBoundingClientRect(); const hit = document.elementFromPoint(m.x + m.width / 2, m.y + 12); return { above: Math.round(m.bottom) <= Math.round(s.top), inside: m.top >= 0 && m.right <= innerWidth, seen: !!(hit && hit.closest('#nav-switch-menu')) }; });
      expect(popup, { above: true, inside: true, seen: true }, 'the popup on a phone');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components#switcher');
    await check('the example with a group: a parent opens its menus, choosing one names the parent', async () => {
      await page.click('#switch-tree-demo .mat-switcher');
      expect(await page.getAttribute('#switch-tree-menu [data-nav-group]', 'aria-expanded'), 'false', 'closed at first');
      await page.click('#switch-tree-menu [data-nav-group]');
      await page.click('#switch-tree-menu [data-nav-set="people"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('#switch-tree-demo .mat-switcher__name')).trim(), 'People', 'name');
      expect((await page.textContent('#switch-tree-demo .mat-switcher__text small')).trim(), 'Administration', 'the parent');
      expectTrue(await page.locator('#switch-tree-demo [data-nav-panel="people"]').isVisible(), 'the items of the menu');
    });
    await check('the example at the foot: the popup opens upwards', async () => {
      await page.click('#switch-foot-demo .mat-switcher');
      const btn = await page.locator('#switch-foot-demo .mat-switcher').boundingBox();
      const p = await page.locator('#switch-foot-menu').boundingBox();
      expectTrue(p.y + p.height <= btn.y + 1, 'the popup is not above the switcher');
      await page.click('#switch-foot-menu [data-nav-set="admin"]');
      await page.waitForTimeout(250);
      expect((await page.textContent('#switch-foot-demo .mat-switcher__name')).trim(), 'Administration', 'name');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- schedule (LIST-14)
  console.log('\nSchedule');
  // known dates and the ISO 8601 calendar week they are in (a unit test checks this list against .NET)
  const weekCases = [['2026-10-12', 42], ['2021-01-03', 53], ['2024-12-30', 1], ['2026-12-31', 53], ['2027-01-01', 53], ['2027-01-04', 1], ['2020-12-31', 53], ['2025-12-29', 1], ['2016-01-03', 53], ['2026-01-01', 1], ['2026-03-29', 13], ['2026-10-25', 43]];
  const norm = s => s.replace(/[   ]/g, ' ').replace(/\s+/g, ' ').trim();
  // a schedule of the page is shown (and a new one made) from its own state, so nothing depends on the day the check runs: the examples pin their clock with data-now
  const schedulePage = async (page) => {
    await page.evaluate(() => {
      window.__ranges = []; window.__picks = []; window.__days = [];
      document.addEventListener('mat:schedule-range', e => window.__ranges.push(Object.assign({ id: e.target.id }, e.detail)));
      document.addEventListener('mat:schedule-select', e => window.__picks.push({ id: e.target.id, event: e.detail }));
      document.addEventListener('mat:schedule-day', e => window.__days.push(Object.assign({ id: e.target.id }, e.detail)));
    });
    const title = sel => page.$eval(`${sel} .mat-schedule__periodtext`, e => e.textContent).then(norm);
    const go = (sel, date, view) => page.evaluate(([s, d, v]) => window.MatUi.schedule.go(document.querySelector(s), d, v), [sel, date, view || null]);
    const view = sel => page.getAttribute(sel, 'data-view');
    const pressed = sel => page.$$eval(`${sel} [data-sch-go]`, bs => bs.filter(b => b.getAttribute('aria-pressed') === 'true').map(b => b.getAttribute('data-sch-go')));
    const lastRange = () => page.evaluate(() => window.__ranges.at(-1));
    const active = () => page.evaluate(() => { const a = document.activeElement; return a ? (a.getAttribute('data-sch-day') || a.className) : null; });
    // a schedule that is made here: at the top of the content, from the events of the practice example
    const create = (id, attrs, copyEvents) => page.evaluate(([id, attrs, copyEvents]) => {
      const el = document.createElement('section');
      el.className = 'mat-schedule'; el.id = id; el.setAttribute('data-schedule', ''); el.setAttribute('aria-label', id);
      Object.entries(attrs).forEach(([k, v]) => el.setAttribute(k, v));
      if (copyEvents) { el.setAttribute('data-events', document.querySelector('#sched-practice').getAttribute('data-events')); }
      document.querySelector('.mat-with-toc__main').prepend(el);
      window.MatUi.schedule.init(el.parentNode);
    }, [id, attrs, !!copyEvents]);
    return { title, go, view, pressed, lastRange, active, create };
  };
  {
    const { ctx, page } = await open(desktopOptions, '/Components#schedule');
    const { title, go, view, pressed, lastRange, active, create } = await schedulePage(page);
    const practice = '#sched-practice', school = '#sched-school', project = '#sched-project';
    const next = sel => `${sel} .mat-schedule__next`, prev = sel => `${sel} .mat-schedule__prev`, today = sel => `${sel} .mat-schedule__today`;

    await check('the agenda lists the days that have events, each under a heading that stays; today is marked and the calendar week told', async () => {
      const s = await page.$eval(practice, root => {
        const heads = [...root.querySelectorAll('.mat-schedule__dayhead')];
        return {
          heads: heads.map(h => h.querySelector('span').textContent.trim()), level: [...new Set(heads.map(h => h.tagName))],
          sticky: heads.every(h => getComputedStyle(h).position === 'sticky'), today: heads.filter(h => h.querySelector('.mat-pill')).map(h => h.querySelector('span').textContent.trim()),
          kw: [...root.querySelectorAll('.mat-schedule__kw')].map(k => k.textContent.trim()), rows: root.querySelectorAll('.mat-schedule__event--row').length,
          wide: root.classList.contains('is-wide'), labelled: root.getAttribute('aria-label'),
        };
      });
      expect(s.heads.length, 5, 'days with events in the week');
      expectTrue(/^Monday,? 12 October$/.test(s.heads[0]), 'the first heading: ' + s.heads[0]);
      expect(s.level, ['H3'], 'the level of the headings');
      expect(s.sticky, true, 'the headings stay at the top (position: sticky)');
      expect(s.today.length, 1, 'the marked day');
      expectTrue(/^Wednesday,? 14 October$/.test(s.today[0]), 'today is Wednesday 14 October (the clock is pinned): ' + s.today[0]);
      expect(s.kw, ['Week 42'], 'the calendar week');
      expect(s.rows, 22, 'rows: the 21 events of the week, and the one that lasts two days on both of its days');
      expect(s.wide, true, 'a schedule of 685 px is wide');
      expect(s.labelled, 'Practice agenda', 'the name of the region');
      expectTrue(/^Week 42 · 12 ?[–-] ?18 Oct 2026$/.test(await title(practice)), 'the title names the week: ' + await title(practice));
      expect(await pressed(practice), ['agenda'], 'the pressed view');
    });
    await check('a tone is never the only sign: every event that has one has an icon, and a text (the status, or the name of the tone for a screen reader)', async () => {
      const rows = await page.$$eval(`${practice} .mat-schedule__event--row`, list => list.map(r => ({
        tone: [...r.classList].find(c => /--(accent|ok|warn|error|info|plain)$/.test(c)).split('--')[1], mark: !!r.querySelector('.mat-schedule__mark svg'),
        text: !!r.querySelector('.mat-pill') || !!r.querySelector('.mat-sr-only'),
      })));
      expect([...new Set(rows.map(r => r.tone))].sort(), ['accent', 'error', 'info', 'ok', 'plain', 'warn'], 'the tones of the example');
      expectTrue(rows.filter(r => r.tone !== 'plain').every(r => r.mark && r.text), 'a tone without an icon or a text');
      expectTrue(rows.filter(r => r.tone === 'plain').every(r => !r.mark), 'a plain event has no icon');
      expect(await page.locator(`${practice} .mat-schedule__event--row`, { hasText: 'J. Maier' }).locator('.mat-pill').textContent(), 'Cancelled', 'the status of the error');
    });
    await check('the views are switched by a row of buttons (aria-pressed): each draws what its name says', async () => {
      const shape = sel => page.$eval(sel, root => ({
        list: root.querySelectorAll('.mat-schedule__list').length, cols: root.querySelectorAll('.mat-schedule__col').length, rows: root.querySelectorAll('.mat-schedule__row:not(.mat-schedule__row--head)').length,
        months: root.querySelectorAll('.mat-schedule__month').length, band: root.querySelectorAll('.mat-schedule__band').length,
      }));
      const want = { agenda: { list: 1, cols: 0, rows: 0, months: 0, band: 0 }, day: { list: 0, cols: 1, rows: 0, months: 0, band: 0 }, week: { list: 0, cols: 7, rows: 0, months: 0, band: 1 }, month: { list: 0, cols: 0, rows: 5, months: 0, band: 0 }, year: { list: 0, cols: 0, rows: 0, months: 12, band: 0 } };
      for (const v of ['day', 'week', 'month', 'year', 'agenda']) {
        await page.click(`${practice} [data-sch-go="${v}"]`);
        expect(await view(practice), v, 'data-view after ' + v);
        expect(await pressed(practice), [v], 'the pressed button after ' + v);
        expect(await shape(practice), want[v], 'what ' + v + ' draws');
      }
    });
    await check('Previous and Next move by the unit of the view (a week, a day, a week, a month, a year), the range of what is visible is sent, and Today comes back', async () => {
      const cases = [
        ['agenda', /^Week 43 · 19 ?[–-] ?25 Oct 2026$/, '2026-10-19', '2026-10-25'], ['day', /^Thursday,? 15 October 2026$/, '2026-10-15', '2026-10-15'],
        ['week', /^Week 43 · 19 ?[–-] ?25 Oct 2026$/, '2026-10-19', '2026-10-25'], ['month', /^November 2026$/, '2026-10-26', '2026-12-06'], ['year', /^2027$/, '2027-01-01', '2027-12-31'],
      ];
      for (const [v, expected, from, to] of cases) {
        await go(practice, '2026-10-14', v);
        const before = await title(practice);
        await page.click(next(practice));
        const after = await title(practice);
        expectTrue(expected.test(after), `${v}: the title after Next is ${after}`);
        expect((({ id, from, to, view }) => ({ id, from, to, view }))(await lastRange()), { id: 'sched-practice', from, to, view: v }, `${v}: the range after Next`);
        await page.click(prev(practice));
        expect(await title(practice), before, `${v}: Previous goes back`);
        await page.click(next(practice));
        await page.click(today(practice));
        expect(await title(practice), before, `${v}: Today comes back`);
      }
      await go(practice, '2026-10-14', 'agenda');
    });
    await check('after a page a live region says the period and how many events it has, and the focus stays on the button that was used', async () => {
      await page.focus(next(practice));
      await page.keyboard.press('Enter');
      expect(norm(await page.textContent(`${practice} [role=status]`)).replace(/ ?[–-] ?/, '-'), 'Week 43 · 19-25 Oct 2026, 3 events', 'the live region');
      expect(await page.evaluate(() => document.activeElement.className.includes('mat-schedule__next')), true, 'the focus after Enter on Next');
      await page.click(prev(practice));
      expect(await page.evaluate(() => document.activeElement.className.includes('mat-schedule__prev')), true, 'the focus after a click on Previous');
      await go(practice, '2026-10-14', 'agenda');
    });
    await check('the calendar weeks are ISO 8601 for known dates (the end and the start of a year, a day that is a Sunday, the changes of the clock)', async () => {
      await create('sched-wk', { 'data-view': 'week', 'data-week-numbers': '', 'data-now': '2026-10-14T10:20' });
      for (const [date, week] of weekCases) {
        await go('#sched-wk', date);
        expectTrue((await title('#sched-wk')).startsWith(`Week ${week} · `), `${date} is in week ${week}, the title says ${await title('#sched-wk')}`);
      }
      // the week number is on the grid too, and a week that starts on Sunday has the number of the week that holds most of it
      await go('#sched-wk', '2026-10-14');
      expect(norm(await page.textContent('#sched-wk .mat-schedule__corner [aria-hidden]')), 'Wk 42', 'the number at the corner of the week');
      await create('sched-sun', { 'data-view': 'week', 'data-week-numbers': '', 'data-week-start': '0', 'data-date': '2026-10-14', 'data-now': '2026-10-14T10:20' });
      expectTrue(/^Week 42 · 11 ?[–-] ?17 Oct 2026$/.test(await title('#sched-sun')), 'a week from Sunday 11 to Saturday 17 October: ' + await title('#sched-sun'));
      expect(await page.$eval('#sched-sun .mat-schedule__dow', e => e.textContent), 'Sun', 'the first day of the week');
      expect(await page.$$eval(`${school} .mat-schedule__row:not(.mat-schedule__row--head) .mat-schedule__wk [aria-hidden]`, ns => ns.map(n => n.textContent)), ['40', '41', '42', '43', '44'], 'the weeks of October 2026 in the month');
    });
    await check('the arrow keys and PageUp and PageDown page, Shift with them pages by a year, and Escape closes what is open', async () => {
      await go(practice, '2026-10-14', 'agenda');
      await page.focus(`${practice} .mat-schedule__body`);
      await page.keyboard.press('ArrowRight');
      expectTrue((await title(practice)).startsWith('Week 43'), 'ArrowRight');
      await page.keyboard.press('ArrowLeft');
      expectTrue((await title(practice)).startsWith('Week 42'), 'ArrowLeft');
      await page.keyboard.press('PageDown');
      expectTrue((await title(practice)).startsWith('Week 43'), 'PageDown');
      await page.keyboard.press('PageUp');
      expectTrue((await title(practice)).startsWith('Week 42'), 'PageUp');
      await page.keyboard.press('Shift+PageDown');
      expectTrue((await title(practice)).includes('2027'), 'Shift+PageDown goes a year on: ' + await title(practice));
      await page.keyboard.press('Shift+PageUp');
      expectTrue((await title(practice)).startsWith('Week 42') && (await title(practice)).includes('2026'), 'Shift+PageUp goes a year back');
      expect(await page.evaluate(() => document.activeElement.className.includes('mat-schedule__body')), true, 'the focus stays on the body that was paged');
      await page.focus(prev(practice));
      await page.keyboard.press('ArrowRight');
      expectTrue((await title(practice)).startsWith('Week 43'), 'the arrow keys page from the buttons too');
      expect(await page.evaluate(() => document.activeElement.className.includes('mat-schedule__prev')), true, 'the focus stays on the button');
      await go(practice, '2026-10-14', 'agenda');
    });
    await check('the title opens a jump to a month and a year: the focus goes into it, a month is chosen, Escape and a click elsewhere close it', async () => {
      await go(practice, '2026-10-14', 'month');
      await page.click(`${practice} .mat-schedule__period`);
      expectTrue(await page.locator(`${practice} .mat-schedule__jump`).isVisible(), 'the jump is shown');
      expect(await page.getAttribute(`${practice} .mat-schedule__period`, 'aria-expanded'), 'true', 'aria-expanded');
      expect(await page.locator(`${practice} [data-sch-pick]`).count(), 12, 'months');
      expect(await page.evaluate(() => document.activeElement.getAttribute('aria-current') && document.activeElement.textContent.trim()), 'Oct', 'the focus is on the month that is shown');
      await page.click(`${practice} [data-sch-year="1"]`);
      expect(await page.textContent(`${practice} .mat-schedule__jumpyear`), '2027', 'the year of the jump');
      await page.click(`${practice} [data-sch-pick="1"]`);
      expect(await title(practice), 'February 2027', 'the month chosen');
      expectTrue(!(await page.locator(`${practice} .mat-schedule__jump`).isVisible()), 'the jump closes when a month is chosen');
      expect(await page.evaluate(() => document.activeElement.className.includes('mat-schedule__period')), true, 'the focus goes back to the title');
      await page.click(`${practice} .mat-schedule__period`);
      await page.keyboard.press('Escape');
      expectTrue(!(await page.locator(`${practice} .mat-schedule__jump`).isVisible()), 'Escape closes the jump');
      expect(await page.evaluate(() => document.activeElement.className.includes('mat-schedule__period')), true, 'the focus is back on the title after Escape');
      await page.click(`${practice} .mat-schedule__period`);
      await page.click('h1');
      expectTrue(!(await page.locator(`${practice} .mat-schedule__jump`).isVisible()), 'a click elsewhere closes the jump');
      await go(practice, '2026-10-14', 'year');
      await page.click(`${practice} .mat-schedule__period`);
      await page.click(`${practice} [data-sch-pick="4"]`);
      expect([await view(practice), await title(practice)], ['month', 'May 2026'], 'a month chosen in the year view opens that month');
      await go(practice, '2026-10-14', 'agenda');
    });
    await check('in the month grid the arrow keys move from day to day, Home and End to the ends of the week and PageDown to the next month; Enter opens the day', async () => {
      await go(practice, '2026-10-14', 'month');
      expect(await page.locator(`${practice} [data-sch-day][tabindex="0"]`).count(), 1, 'one tab stop in the grid');
      await page.focus(`${practice} [data-sch-day="2026-10-14"]`);
      await page.keyboard.press('ArrowRight');
      expect(await active(), '2026-10-15', 'ArrowRight');
      await page.keyboard.press('ArrowDown');
      expect(await active(), '2026-10-22', 'ArrowDown');
      await page.keyboard.press('Home');
      expect(await active(), '2026-10-19', 'Home');
      await page.keyboard.press('End');
      expect(await active(), '2026-10-25', 'End');
      expect(await page.locator(`${practice} [data-sch-day][tabindex="0"]`).count(), 1, 'still one tab stop');
      await page.keyboard.press('PageDown');
      expect(await title(practice), 'November 2026', 'PageDown turns the month');
      expect(await active(), '2026-11-25', 'the focus follows to the same day of the next month');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('Enter');
      expect(await view(practice), 'day', 'Enter on a day opens it');
      expectTrue(/^Thursday,? 26 November 2026$/.test(await title(practice)), 'the day: ' + await title(practice));
      expect((await page.evaluate(() => window.__days.at(-1))).date, '2026-11-26', 'mat:schedule-day');
      await go(practice, '2026-10-14', 'agenda');
    });
    await check('an event is a button that sends mat:schedule-select with the event, also from the keyboard; one with a url is a link that a listener can stop', async () => {
      await go(practice, '2026-10-14', 'agenda');
      const keller = page.locator(`${practice} .mat-schedule__event--row`, { hasText: 'Check-up, M. Keller' });
      expect(await keller.evaluate(e => e.tagName), 'BUTTON', 'an event without a url');
      await keller.click();
      let pick = await page.evaluate(() => window.__picks.at(-1));
      expect([pick.id, pick.event.id, pick.event.title, pick.event.tone, pick.event.location], ['sched-practice', 'p1', 'Check-up, M. Keller', 'ok', 'Room 1'], 'the event of a click');
      await page.locator(`${practice} .mat-schedule__event--row`, { hasText: 'Wound care' }).focus();
      await page.keyboard.press('Enter');
      expect((await page.evaluate(() => window.__picks.at(-1))).event.id, 'p8', 'Enter on an event');
      await page.locator(`${practice} .mat-schedule__event--row`, { hasText: 'Team meeting' }).first().focus();
      await page.keyboard.press(' ');
      expect((await page.evaluate(() => window.__picks.at(-1))).event.id, 'p5', 'Space on an event');
      await go(project, '2026-11-13', 'week');
      const link = page.locator(`${project} a.mat-schedule__event`);
      expect(await link.getAttribute('href'), '#sched-project', 'the url of the event');
      await page.evaluate(() => { window.__stop = e => e.preventDefault(); document.addEventListener('mat:schedule-select', window.__stop); });
      const hash = await page.evaluate(() => location.hash);
      await link.click();
      pick = await page.evaluate(() => window.__picks.at(-1));
      expect([pick.id, pick.event.id], ['sched-project', 't7'], 'the link was chosen');
      expect(await page.evaluate(() => location.hash), hash, 'a listener that stops the event keeps the page where it is');
      await page.evaluate(() => document.removeEventListener('mat:schedule-select', window.__stop));
      await link.click();
      expect(await page.evaluate(() => location.hash), '#sched-project', 'without a listener the link goes where it points');
    });
    await check('the week is a time grid: events that overlap stand side by side, one that does not is as wide as the column, a line shows the time now, an event of two days is a bar over two columns', async () => {
      await go(practice, '2026-10-12', 'day');
      const blocks = await page.$$eval(`${practice} .mat-schedule__event--block`, list => list.map(b => { const r = b.getBoundingClientRect(); return { t: b.getAttribute('title'), l: r.left, r: r.right, w: r.width }; }));
      const by = text => blocks.find(b => b.t.includes(text));
      const a = by('Blood test'), b = by('Hygiene training'), alone = by('Check-up, M. Keller');
      expectTrue(Math.abs(a.w - b.w) < 2 && a.r <= b.l + 3, `the two that overlap are side by side: ${JSON.stringify([a, b])}`);
      expectTrue(alone.w > a.w * 1.8, 'an event that overlaps none has the whole column');
      expect(await page.locator(`${practice} .mat-schedule__now`).count(), 0, 'no line of the time on another day than today');
      await go(practice, '2026-10-14', 'day');
      const line = await page.evaluate(() => {
        const now = document.querySelector('#sched-practice .mat-schedule__now').getBoundingClientRect();
        const lines = [...document.querySelectorAll('#sched-practice .mat-schedule__lines > div')].map(l => l.getBoundingClientRect());
        return (now.top + 1 - lines[0].top) / lines[0].height;
      });
      expectTrue(Math.abs(line - (3 + 20 / 60)) < 0.04, `the line of 10:20 is 3 1/3 hours below the first hour (07:00), it is ${line}`);
      await go(practice, '2026-10-14', 'week');
      const bar = await page.evaluate(() => {
        const bar = document.querySelector('#sched-practice .mat-schedule__event--bar').getBoundingClientRect();
        const head = document.querySelector('#sched-practice .mat-schedule__colhead').getBoundingClientRect();
        return { bar: bar.width, col: head.width, left: bar.left - head.left };
      });
      expectTrue(Math.abs(bar.bar - 2 * bar.col) < 6, `an event of two days is a bar of two columns: ${JSON.stringify(bar)}`);
      expect(await page.locator(`${practice} .mat-schedule__colhead.is-today`).count(), 1, 'today is marked in the head');
      expect(await page.locator(`${practice} .mat-schedule__col`).evaluateAll(cols => cols.map(c => c.getAttribute('role'))), Array(7).fill('group'), 'every column is a named group');
      await go(practice, '2026-10-14', 'agenda');
    });
    await check('the month shows its events in the cells: a long event is one bar over its days, a crowded day says how many more, and the week numbers are in the first column', async () => {
      await go(school, '2026-10-14', 'month');
      const bars = await page.$$eval(`${school} .mat-schedule__event--chip[data-sch-span]`, list => list.filter(b => /Klassenarbeitswoche|Herbstferien|Klassenfahrt/.test(b.textContent)).map(b => ({ t: b.textContent.trim().replace(/\s+/g, ' '), span: b.getAttribute('data-sch-span'), w: Math.round(b.getBoundingClientRect().width) })));
      expect(bars.map(b => b.span).sort(), ['3', '5', '5'], 'the span of the bars of 3 and 5 days');
      expectTrue(bars.every(b => b.w > 150), 'a bar is wider than a cell: ' + JSON.stringify(bars));
      await go(practice, '2026-10-14', 'month');
      const more = page.locator(`${practice} .mat-schedule__more`).first();
      expect((await more.textContent()).trim(), '+2 more', 'a crowded day says how many are not shown');
      await more.click();
      expect(await view(practice), 'day', '+n more opens the day');
      expectTrue(/^Monday,? 12 October 2026$/.test(await title(practice)), 'the day: ' + await title(practice));
      await go(practice, '2026-10-14', 'agenda');
    });
    await check('data-min and data-max stop the paging: the arrow is dimmed (aria-disabled), a click does nothing, and the days outside are dimmed in the month', async () => {
      await go(project, '2026-11-30', 'week');
      const at = await title(project);
      expect(await page.getAttribute(next(project), 'aria-disabled'), 'true', 'Next in the last week');
      await page.click(next(project), { force: true });
      expect(await title(project), at, 'the title after a click on a dimmed Next');
      await go(project, '2026-11-02', 'week');
      expect(await page.getAttribute(prev(project), 'aria-disabled'), 'false', 'Previous in the first week, which has the first of November');
      await page.click(prev(project));
      expectTrue((await title(project)).startsWith('Week 44'), 'the week that holds the first of November');
      expect(await page.getAttribute(prev(project), 'aria-disabled'), 'true', 'Previous in the week before the first one');
      await go(project, '2026-12-24', 'week');
      expectTrue((await title(project)).includes('30 Nov'), 'a date past the limit is brought back: ' + await title(project));
      await go(project, '2026-11-15', 'month');
      expect(await page.locator(`${project} .mat-schedule__cell.is-off`).count(), 12, 'the days of the grid that lie outside of November');
      await go(project, '2026-11-11', 'week');
    });
    await check('the year has twelve months with a tint per day by how many events it has; a month opens the month view', async () => {
      await go(school, '2026-10-14', 'year');
      const s = await page.$eval(school, root => ({
        months: root.querySelectorAll('.mat-schedule__month').length, l1: root.querySelectorAll('.mat-schedule__d.is-l1').length, l2: root.querySelectorAll('.mat-schedule__d.is-l2').length,
        today: root.querySelectorAll('.mat-schedule__d.is-today').length, hint: root.querySelector('.mat-schedule__hint').textContent, aria: root.querySelector('.mat-schedule__mini').getAttribute('aria-hidden'),
        name: root.querySelectorAll('.mat-schedule__monthname')[9].getAttribute('aria-label'),
      }));
      expect([s.months, s.l1 > 80, s.l2 > 0, s.today], [12, true, true, 1], 'months, days with one event, days with two, today');
      expect(s.name, 'Oktober 2026 öffnen, 7 Termine', 'the name of a month says its number of events');
      await page.click(`${school} .mat-schedule__monthname >> nth=9`);
      expect([await view(school), await title(school)], ['month', 'Oktober 2026'], 'the month opened from the year');
      await go(school, '2026-10-14', 'month');
    });
    await check('the texts and the formats follow the language: the school calendar has lang="de", the others are English', async () => {
      const s = await page.$eval(school, root => ({
        today: root.querySelector('.mat-schedule__today').textContent, views: [...root.querySelectorAll('[data-sch-go]')].map(b => b.textContent), wd: root.querySelector('.mat-schedule__wd').textContent,
        kw: root.querySelector('.mat-schedule__wk [aria-hidden]').textContent, prev: root.querySelector('.mat-schedule__prev').getAttribute('aria-label'), area: root.querySelector('.mat-schedule__mgrid').getAttribute('aria-label'),
      }));
      expect(s, { today: 'Heute', views: ['Monat', 'Agenda', 'Jahr'], wd: 'Mo', kw: 'KW', prev: 'Vorheriger Monat', area: 'Oktober 2026' }, 'the German schedule');
      await go(school, '2026-10-14', 'agenda');
      expectTrue(/^KW 42 · 12\.? ?[–-] ?18\. Okt\.? 2026$/.test(await title(school)), 'the title in German: ' + await title(school));
      expect(await page.$eval(`${school} .mat-schedule__day.is-today .mat-pill`, e => e.textContent), 'Heute', 'the mark of today');
      await go(school, '2026-10-14', 'month');
      expect(await page.$eval(`${practice} .mat-schedule__today`, e => e.textContent), 'Today', 'the English one');
    });
    await check('an empty period says so with a sentence of its own, and can still be paged', async () => {
      const empty = '#sched-empty';
      expect(await page.locator(`${empty} .mat-schedule__dayhead`).count(), 0, 'no day headings');
      expect(norm(await page.textContent(`${empty} .mat-empty__title`)), 'Nothing planned', 'the title');
      expect(norm(await page.textContent(`${empty} .mat-empty p:last-child`)), 'Nothing is booked this week. New appointments appear here as soon as they are made.', 'data-empty');
      await page.click(next(empty));
      expectTrue((await title(empty)).startsWith('Week 43') === false && /19 ?[–-] ?25 Oct 2026$/.test(await title(empty)), 'the next week: ' + await title(empty));
      await go(empty, '2026-10-14', 'agenda');
    });
    await check('without data-now the clock of the browser decides which day is today (the check asks the page what day it is, so it does not depend on the day it runs)', async () => {
      await create('sched-clock', { 'data-view': 'month' });
      const expected = await page.evaluate(() => { const d = new Date(); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); });
      expect(await page.$$eval('#sched-clock .mat-schedule__cell.is-today [data-sch-day]', bs => bs.map(b => b.getAttribute('data-sch-day'))), [expected], 'the day that is marked');
      expect(await page.$eval('#sched-clock .mat-schedule__period', b => b.getAttribute('title')).then(Boolean), true, 'the schedule is drawn');
      expect(await page.$eval('#sched-clock .mat-schedule__body', b => b.getAttribute('aria-label')), norm(await title('#sched-clock')), 'the region is named by the period');
      await go('#sched-clock', '2000-02-29', 'month');
      expect(await title('#sched-clock'), 'February 2000', 'a date far from today');
      await page.click('#sched-clock .mat-schedule__today');
      expect(await page.$$eval('#sched-clock .mat-schedule__cell.is-today [data-sch-day]', bs => bs.map(b => b.getAttribute('data-sch-day'))), [expected], 'Today comes back to the real today');
    });
    await check('an app that loads events is told what is visible; data-lazy shows a loading state until it answers', async () => {
      await page.evaluate(() => { window.__lazy = []; document.addEventListener('mat:schedule-range', e => { if (e.target.id === 'sched-lazy') { window.__lazy.push(e.detail); } }); });
      await create('sched-lazy', { 'data-lazy': '', 'data-view': 'week', 'data-date': '2026-03-04', 'data-now': '2026-03-04T10:00' });
      await page.waitForFunction(() => window.__lazy.length === 1, null, { timeout: 2000 });
      expect(await page.evaluate(() => window.__lazy[0]), { from: '2026-03-02', to: '2026-03-08', view: 'week', date: '2026-03-04' }, 'the first range');
      const busy = () => page.$eval('#sched-lazy', r => ({ cls: r.classList.contains('is-loading'), aria: r.querySelector('.mat-schedule__body').getAttribute('aria-busy'), spinner: !r.querySelector('.mat-schedule__busy').hidden }));
      expect(await busy(), { cls: true, aria: 'true', spinner: true }, 'the loading state before the events come');
      await page.evaluate(() => window.MatUi.schedule.setEvents(document.querySelector('#sched-lazy'), [{ id: 'x1', title: 'Loaded one', start: '2026-03-04T09:00', end: '2026-03-04T10:00', tone: 'ok' }]));
      expect(await busy(), { cls: false, aria: 'false', spinner: false }, 'the state after setEvents');
      expect(await page.locator('#sched-lazy .mat-schedule__event--block', { hasText: 'Loaded one' }).count(), 1, 'the event that was loaded is drawn');
      await page.click(next('#sched-lazy'));
      expect(await page.evaluate(() => window.__lazy.at(-1)), { from: '2026-03-09', to: '2026-03-15', view: 'week', date: '2026-03-11' }, 'the range after Next');
      expect((await busy()).aria, 'true', 'loading again after a page');
      await page.click('#sched-lazy [data-sch-go="agenda"]');
      expect(await page.evaluate(() => window.__lazy.length), 2, 'a view that shows the same days does not ask again');
      await page.click('#sched-lazy [data-sch-go="month"]');
      expect(await page.evaluate(() => window.__lazy.at(-1)), { from: '2026-02-23', to: '2026-04-05', view: 'month', date: '2026-03-11' }, 'the month asks for the weeks of its grid');
      await page.evaluate(() => window.MatUi.schedule.setLoading(document.querySelector('#sched-lazy'), false));
      expect((await busy()).cls, false, 'setLoading(false) ends the state without events');
      expect(await page.evaluate(() => window.MatUi.schedule.get(document.querySelector('#sched-lazy')).view), 'month', 'MatUi.schedule.get');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#schedule');
    const { title, go, view, pressed, create, active } = await schedulePage(page);
    // the schedule of a phone has the whole width of the content (358 px), unlike the examples, which sit in a box of the documentation
    await create('sched-ph', { 'data-view': 'agenda', 'data-date': '2026-10-14', 'data-now': '2026-10-14T10:20', 'data-week-numbers': '' }, true);
    const ph = '#sched-ph';
    const height = sel => page.$eval(sel, e => Math.round(e.getBoundingClientRect().height));
    await check('on a phone nothing scrolls sideways in any view, and the layout is the narrow one', async () => {
      expect(await page.$eval(ph, r => r.clientWidth), 358, 'the width of the schedule');
      expect(await page.$eval(ph, r => r.classList.contains('is-narrow')), true, 'narrow');
      for (const v of ['agenda', 'day', 'week', 'month', 'year']) {
        await go(ph, '2026-10-14', v);
        const s = await page.evaluate(() => ({ page: document.documentElement.scrollWidth, win: innerWidth, body: document.querySelector('#sched-ph .mat-schedule__body').scrollWidth - document.querySelector('#sched-ph .mat-schedule__body').clientWidth }));
        expectTrue(s.page <= s.win && s.body <= 1, `${v}: the page is ${s.page} px wide in ${s.win} px, the body overflows by ${s.body} px`);
      }
    });
    await check('on a phone a week is a row per day and a month is a compact grid with the chosen day listed under it', async () => {
      await go(ph, '2026-10-14', 'week');
      const week = await page.$eval(ph, r => ({ days: r.querySelectorAll('.mat-schedule__day').length, grid: r.querySelectorAll('.mat-schedule__grid').length, heads: r.querySelectorAll('h3.mat-schedule__dayhead').length }));
      expect(week, { days: 7, grid: 0, heads: 7 }, 'the week of a phone');
      await go(ph, '2026-10-14', 'month');
      const month = await page.$eval(ph, r => ({ cells: r.querySelectorAll('.mat-schedule__daybtn').length, dots: r.querySelectorAll('.mat-schedule__dot').length, list: r.querySelectorAll('.mat-schedule__daylist .mat-schedule__event--row').length, chips: r.querySelectorAll('.mat-schedule__chips').length }));
      expect(month.cells, 35, 'days');
      expectTrue(month.dots > 10 && month.chips === 0, 'dots, no events in the cells');
      expect(month.list, 6,'the events of the 14th are listed under the grid');
      await page.click(`${ph} [data-sch-day="2026-10-13"]`);
      expect(await page.$$eval(`${ph} .mat-schedule__daylist .mat-schedule__event--row`, rs => rs.length), 4, 'the day that was chosen is listed');
      expectTrue((await page.textContent(`${ph} .mat-schedule__daylist .mat-schedule__dayhead`)).includes('4 events'), 'the heading of the list counts');
      expect(await active(), '2026-10-13', 'the focus stays on the day');
      expect(await page.getAttribute(`${ph} [data-sch-day="2026-10-13"]`, 'aria-label'), 'Tuesday, 13 October 2026, 4 events', 'the name of a day');
      expect((await page.evaluate(() => window.__days.at(-1))).date, '2026-10-13', 'mat:schedule-day');
    });
    await check('on a phone the controls are 44 px high (the views, the arrows, Today, the title, a day, an event)', async () => {
      await go(ph, '2026-10-14', 'month');
      for (const sel of ['.mat-schedule__today', '.mat-schedule__prev', '.mat-schedule__next', '.mat-schedule__period', '[data-sch-go="week"]', '.mat-schedule__daybtn']) {
        expectTrue(await height(`${ph} ${sel}`) >= 44, `${sel} is ${await height(`${ph} ${sel}`)} px high`);
      }
      await go(ph, '2026-10-14', 'agenda');
      expectTrue(await height(`${ph} .mat-schedule__event--row`) >= 44, 'an event of the agenda');
      const wide = await page.$eval(`${ph} .mat-schedule__views`, v => [...v.children].map(b => Math.round(b.getBoundingClientRect().width)));
      expectTrue(wide.every(w => w >= 60), 'the five views fit in a row: ' + wide);
    });
    await check('a swipe to the left shows the next period, to the right the one before; a vertical pan does not page and the page does not move sideways', async () => {
      await go(ph, '2026-10-14', 'agenda');
      expect(await page.$eval(`${ph} .mat-schedule__body`, b => getComputedStyle(b).touchAction), 'pan-y pinch-zoom', 'touch-action of the body');
      await page.evaluate(() => document.querySelector('#sched-ph .mat-schedule__body').scrollIntoView({ block: 'start' }));
      const box = await page.locator(`${ph} .mat-schedule__body`).boundingBox();
      const cdp = await ctx.newCDPSession(page);
      const swipe = async (x0, y0, x1, y1) => {
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: x0, y: y0 }] });
        for (let i = 1; i <= 6; i++) { await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: x0 + (x1 - x0) * i / 6, y: y0 + (y1 - y0) * i / 6 }] }); }
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
        await page.waitForTimeout(120);
      };
      const y = Math.min(box.y + 160, 700);
      await swipe(320, y, 80, y + 10);
      expectTrue((await title(ph)).startsWith('Week 43'), 'a swipe to the left: ' + await title(ph));
      await swipe(60, y, 300, y - 10);
      expectTrue((await title(ph)).startsWith('Week 42'), 'a swipe to the right: ' + await title(ph));
      await swipe(200, y + 200, 190, y - 120);
      expectTrue((await title(ph)).startsWith('Week 42'), 'a vertical pan pages nothing: ' + await title(ph));
      await swipe(300, y, 280, y);
      expectTrue((await title(ph)).startsWith('Week 42'), 'a short move pages nothing');
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'the page did not move sideways');
      await go(ph, '2026-10-14', 'year');
      await swipe(320, 400, 60, 400);
      expect(await title(ph), '2027', 'a swipe in the year view turns the year');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open({ ...desktopOptions, reducedMotion: 'reduce' }, '/Components#schedule');
    await check('for a user who asked for less motion a page does not slide', async () => {
      await page.click('#sched-practice .mat-schedule__next');
      expect(await page.$eval('#sched-practice .mat-schedule__body', b => b.classList.contains('is-from-next') || b.classList.contains('is-from-prev')), false, 'the class of the slide');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components#schedule');
    await check('a page slides in from the side it was turned to, for a user who did not ask for less motion', async () => {
      await page.click('#sched-practice .mat-schedule__next');
      await page.waitForFunction(() => document.querySelector('#sched-practice .mat-schedule__body').classList.contains('is-from-next'), null, { timeout: 500 });
      await page.waitForFunction(() => !document.querySelector('#sched-practice .mat-schedule__body').classList.contains('is-from-next'), null, { timeout: 1500 });
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components#topbar');
    await check('the main action of the header bar is the one icon in the accent colour, a second one is secondary, with room between them and in line with the content edge', async () => {
      const state = await page.evaluate(() => {
        const bar = Array.from(document.querySelectorAll('.mat-topbar')).find(b => b.querySelector('.mat-topbar__actions .mat-btn--primary'));
        const buttons = Array.from(bar.querySelectorAll('.mat-topbar__actions .mat-btn'));
        const [second, main] = buttons;
        const sm = getComputedStyle(main), ss = getComputedStyle(second);
        const rects = buttons.map(b => b.getBoundingClientRect());
        const edge = bar.getBoundingClientRect().right - parseFloat(getComputedStyle(bar).paddingRight);
        return {
          count: buttons.length, classes: buttons.map(b => b.className.replace('mat-btn mat-btn--icon', '').replace('mat-btn--icon', '').replace('mat-btn', '').trim()),
          mainBg: sm.backgroundColor, mainColour: sm.color, secondBg: ss.backgroundColor, secondColour: ss.color, secondBorder: ss.borderTopWidth,
          gap: Math.round(rects[1].left - rects[0].right), mainToEdge: Math.round(edge - rects[1].right),
        };
      });
      const probe = (prop, value) => page.evaluate(([p, v]) => { const t = document.createElement('i'); t.style.setProperty(p, v); document.body.appendChild(t); const c = getComputedStyle(t).getPropertyValue(p); t.remove(); return c; }, [prop, value]);
      const accent = await accentOf(page);
      const surface = await probe('background-color', 'var(--mat-surface)');
      expect(state.classes, ['mat-btn--secondary', 'mat-btn--primary'], 'the buttons of the bar, the main action last');
      expect(state.mainBg, accent.accent, 'background of the main action');
      expect(state.secondBg, surface, 'background of the second action (a surface, not the accent)');
      expectTrue(state.secondBg !== state.mainBg, 'the second action has the colour of the main one');
      expectTrue(parseFloat(state.secondBorder) >= 1, 'the second action has no line');
      expectTrue(contrastOf(state.mainBg, state.mainColour) >= 4.5, 'the icon of the main action is not readable on the accent');
      expectTrue(contrastOf(state.secondBg, state.secondColour) >= 4.5, 'the icon of the second action is not readable on its surface');
      expectTrue(state.gap >= 8, 'the buttons touch: gap ' + state.gap + ' px');
      expectTrue(Math.abs(state.mainToEdge) <= 1, 'the main action is not in line with the edge of the content: ' + state.mainToEdge + ' px');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- long pages: the index
  console.log('\nOn this page: the index of a long page');
  {
    const { ctx, page } = await open(desktopOptions, '/Components');
    await check('the index sits beside the content and stays in view while the page scrolls', async () => {
      const before = await page.locator('.mat-toc-aside').boundingBox();
      await page.evaluate(() => { document.querySelector('.mat-main').scrollTop = 3000; });
      await page.waitForTimeout(300);
      const after = await page.locator('.mat-toc-aside').boundingBox();
      const main = await page.locator('.mat-with-toc__main').boundingBox();
      expectTrue(Math.abs(before.y - after.y) < 120, 'the index scrolled away: ' + before.y + ' -> ' + after.y);
      expectTrue(after.x > main.x + main.width - 1, 'the index is not beside the content');
    });
    await check('a click on a component jumps to it, below the top bar, and the index marks it', async () => {
      await page.click('.mat-toc-aside a[href="#table"]');
      await page.waitForTimeout(500);
      const top = await page.evaluate(() => document.getElementById('table').getBoundingClientRect().top);
      expectTrue(top >= 50 && top < 160, 'the section is not just below the top bar: ' + top);
      expect(await page.getAttribute('.mat-toc-aside a[href="#table"]', 'aria-current'), 'location', 'aria-current');
      expect(await page.locator('.mat-toc-aside .mat-toc__link.is-active').count(), 1, 'marked links');
    });
    await check('scrolling changes the marked link', async () => {
      await page.evaluate(() => { document.getElementById('dialogs').scrollIntoView(); });
      await page.waitForTimeout(500);
      expect(await page.getAttribute('.mat-toc-aside a[href="#dialogs"]', 'aria-current'), 'location', 'dialogs');
      expectTrue(!(await page.locator('.mat-toc-aside a[href="#table"]').getAttribute('aria-current')), 'table is still marked');
    });
    await check('the index can be scrolled by the user: it follows the page when the section changes, and does not jump back while it is scrolled', async () => {
      const top = () => page.evaluate(() => Math.round(document.querySelector('.mat-toc-aside').scrollTop));
      await page.evaluate(() => { const m = document.querySelector('.mat-main'); m.scrollTop = m.scrollHeight; });
      await page.waitForTimeout(600);
      const followed = await top();
      expectTrue(followed > 200, 'the index did not follow the page to the last section (scrollTop ' + followed + ')');
      const box = await page.locator('.mat-toc-aside').boundingBox();
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await page.mouse.wheel(0, -300);
      await page.waitForTimeout(400);
      const up = await top();
      expectTrue(up < followed - 100, 'the index jumped back when it was scrolled up: ' + followed + ' -> ' + up);
      await page.waitForTimeout(500);
      expectTrue(Math.abs((await top()) - up) < 4, 'the index moved again by itself after it was scrolled: ' + up + ' -> ' + (await top()));
    });
    await check('the example inside a component works: a dependent field of a form example', async () => {
      await page.evaluate(() => document.getElementById('form-layout').scrollIntoView());
      expectTrue(await page.locator('#fl-mail').isVisible(), 'the e-mail field is hidden while the switch is on');
      await page.click('#form-layout label.mat-switch');
      expectTrue(!(await page.locator('#fl-mail').isVisible()), 'the e-mail field is still shown');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components');
    await check('on a phone there is no index beside the content: the icon in the header bar opens it in a full-screen dialog and a link closes it', async () => {
      expectTrue(!(await page.locator('.mat-toc-aside').isVisible()), 'the index is shown beside the content on a phone');
      await page.click('.mat-topbar__index');
      await page.waitForTimeout(350);
      const box = await page.locator('#toc-dialog').boundingBox();
      expect([Math.round(box.width), Math.round(box.height)], [390, 844], 'size');
      await page.click('#toc-dialog a[href="#toolbar"]');
      await page.waitForTimeout(600);
      expect(await page.evaluate(() => document.getElementById('toc-dialog').open), false, 'dialog after the jump');
      const top = await page.evaluate(() => document.getElementById('toolbar').getBoundingClientRect().top);
      expectTrue(top >= 40 && top < 200, 'the section is not near the top: ' + top);
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- lists of rows (LIST-15)
  console.log('\nLists of rows');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#itemlist');
    // the toasts of the documentation (it reports the events of the lists) would cover what the checks press
    await page.addStyleTag({ content: '.mat-toasts { display: none; }' });
    // what the lists report is collected in the page, so that each check can look at it
    await page.evaluate(() => {
      window.__il = [];
      const note = (type, d) => window.__il.push({ type, action: d.action, key: d.item && d.item.getAttribute('data-item-key'), from: d.from, to: d.to, order: d.order, text: d.text, shown: d.shown, total: d.total });
      for (const type of ['mat:item-action', 'mat:reorder', 'mat:item-filter']) { document.addEventListener(type, e => note(type, e.detail)); }
    });
    const events = type => page.evaluate(t => window.__il.filter(e => e.type === t), type);
    const show = sel => page.locator(sel).first().scrollIntoViewIfNeeded();
    const slide = '#il-slide';
    const row = n => `${slide} > .mat-item:nth-child(${n})`;
    const rowState = sel => page.$eval(sel, li => {
      const end = li.querySelector('.mat-item__options--end'), start = li.querySelector('.mat-item__options--start');
      const side = end || start;
      return {
        end: li.classList.contains('is-open-end'), start: li.classList.contains('is-open-start'), open: li.classList.contains('is-open'),
        x: li.style.getPropertyValue('--mat-item-x'), width: side ? side.offsetWidth : 0,
        visibility: side ? getComputedStyle(side).visibility : '', inert: side ? side.hasAttribute('inert') : null,
        expanded: li.querySelector('.mat-item__more').getAttribute('aria-expanded'),
        moved: Math.round(li.querySelector('.mat-item__row').getBoundingClientRect().left - li.getBoundingClientRect().left),
      };
    });
    const active = () => page.evaluate(() => {
      const a = document.activeElement, li = a.closest && a.closest('.mat-item');
      return { cls: typeof a.className === 'string' ? a.className : '', key: li ? li.getAttribute('data-item-key') : null, action: a.getAttribute && a.getAttribute('data-item-action') };
    });
    // a drag of the row with the mouse, from its right part to the left (or the right)
    const drag = async (sel, dx) => {
      await show(sel);
      const box = await page.locator(sel + ' .mat-item__main').boundingBox();
      const y = box.y + box.height / 2, x0 = box.x + box.width - 80;
      await page.mouse.move(x0, y);
      await page.mouse.down();
      for (let i = 1; i <= 10; i += 1) { await page.mouse.move(x0 + dx * i / 10, y); await page.waitForTimeout(25); }
      return { up: async () => { await page.mouse.up(); await page.waitForTimeout(350); }, box };
    };

    // ---- the list and its rows
    await check('a row is at least 48 px high, the lines are drawn by the rows (none under the last one) and from the text when the list says so', async () => {
      await show('[aria-labelledby="ex-itemlist-2"]');
      const s = await page.evaluate(() => {
        const lists = [...document.querySelectorAll('[aria-labelledby="ex-itemlist-2"] .mat-itemlist')];
        const line = li => getComputedStyle(li, '::after').content;
        const first = lists[0].querySelector('.mat-item'), last = lists[0].querySelector('.mat-item:last-child');
        const inset = lists[1], none = lists[2];
        const left = li => { const a = getComputedStyle(li, '::after'); return parseFloat(a.left); };
        return {
          height: Math.round(first.getBoundingClientRect().height), line: line(first) !== 'none', lastLine: line(last),
          insetLeft: Math.round(left(inset.querySelector('.mat-item'))), fullLeft: Math.round(left(first)), noneColour: getComputedStyle(none.querySelector('.mat-item'), '::after').backgroundColor,
        };
      });
      expectTrue(s.height >= 48, 'a row is ' + s.height + ' px high');
      expect(s.line, true, 'the line under the first row');
      expect(s.lastLine, 'none', 'the line under the last row');
      expect([s.fullLeft, s.insetLeft], [0, 68], 'where the lines begin (full, from the text)');
      expect(s.noneColour, 'rgba(0, 0, 0, 0)', 'the lines of a list without lines');
    });
    await check('the grouped card is a rounded list with a border, and a selected row is a tint behind the whole row with no bar at a side', async () => {
      await show('[aria-labelledby="ex-itemlist-5"]');
      const s = await page.evaluate(() => {
        const card = document.querySelector('[aria-labelledby="ex-itemlist-3"] .mat-itemlist--inset');
        const sel = document.querySelector('[aria-labelledby="ex-itemlist-5"] .mat-item.is-selected');
        const c = getComputedStyle(card), r = getComputedStyle(sel);
        return {
          radius: c.borderTopLeftRadius, border: c.borderTopWidth, tint: r.backgroundColor, left: r.borderLeftWidth, right: r.borderRightWidth, shadow: r.boxShadow,
          weight: Number(getComputedStyle(sel.querySelector('.mat-item__title')).fontWeight), normal: Number(getComputedStyle(document.querySelector('[aria-labelledby="ex-itemlist-5"] .mat-item:first-child .mat-item__title')).fontWeight),
        };
      });
      expect([s.radius, s.border], ['8px', '1px'], 'the card');
      expectTrue(s.tint !== 'rgba(0, 0, 0, 0)' && s.tint !== 'transparent', 'the selected row has no tint');
      expect([s.left, s.right, s.shadow], ['0px', '0px', 'none'], 'a bar at a side');
      expectTrue(s.weight > s.normal, 'the selected title is not stronger');
    });
    await check('a row that is a link has its focus ring inside the row', async () => {
      await show('[aria-labelledby="ex-itemlist-1"]');
      await page.focus('[aria-labelledby="ex-itemlist-1"] .mat-item:first-child .mat-item__main');
      await page.keyboard.press('Shift+Tab');
      await page.keyboard.press('Tab');
      const ring = await page.$eval('[aria-labelledby="ex-itemlist-1"] .mat-item:first-child .mat-item__main', el => getComputedStyle(el).boxShadow);
      expectTrue(ring.includes('inset'), 'the focus ring is not inside the row: ' + ring);
    });

    // ---- sliding actions
    await check('a list that the script has taken over names the More button of each row, and the options are hidden, inert and not in the tab order', async () => {
      await show(slide);
      const s = await page.$eval(slide, l => ({
        ready: l.classList.contains('is-ready'),
        labels: [...l.querySelectorAll('.mat-item__more')].map(b => b.getAttribute('aria-label')),
        inert: [...l.querySelectorAll('.mat-item__options')].map(o => o.hasAttribute('inert')),
        visibility: [...l.querySelectorAll('.mat-item__options')].map(o => getComputedStyle(o).visibility),
        live: l.nextElementSibling && l.nextElementSibling.getAttribute('role'),
        expanded: l.querySelector('.mat-item__more').getAttribute('aria-expanded'),
      }));
      expect(s.ready, true, 'the list is ready');
      expect(s.labels, ['More actions for Jamie Doe', 'More actions for Sam Lee', 'More actions for Kim Park'], 'names of the More buttons');
      expect(s.inert, [true, true, true], 'inert options');
      expect(s.visibility, ['hidden', 'hidden', 'hidden'], 'visibility of the options');
      expect([s.live, s.expanded], ['status', 'false'], 'live region and state');
      await page.focus(row(1) + ' .mat-item__main');
      await page.keyboard.press('Tab');
      expectTrue((await active()).cls.includes('mat-item__more'), 'Tab does not go to the More button');
      await page.keyboard.press('Tab');
      const next = await active();
      expect([next.key, next.cls.includes('mat-item__main')], ['lunch', true], 'Tab skips the options and goes to the next row');
    });
    await check('a drag of the row with the mouse slides it open to the width of its options; the click that ends it follows no link; a press on the row closes it', async () => {
      const url = page.url();
      const d = await drag(row(2), -240);
      const mid = await page.$eval(row(2), li => ({ swiping: li.classList.contains('is-swiping'), x: li.style.getPropertyValue('--mat-item-x') }));
      expect(mid.swiping, true, 'swiping while the button is down');
      expectTrue(parseFloat(mid.x) < -100, 'the row did not follow the pointer: ' + mid.x);
      await d.up();
      const s = await rowState(row(2));
      expect([s.end, s.open, s.expanded, s.inert, s.visibility], [true, true, 'true', false, 'visible'], 'the open row');
      expect([s.x, s.moved], [`-${s.width}px`, -s.width], 'the row stands at the width of its options');
      expect(page.url(), url, 'the swipe followed the link');
      await page.mouse.click(d.box.x + 120, d.box.y + d.box.height / 2);
      await page.waitForTimeout(350);
      expect((await rowState(row(2))).open, false, 'the row is still open after a press on it');
      expect(page.url(), url, 'the press that closes followed the link');
    });
    await check('a short drag, or a drag the other way, leaves the row closed; a vertical move is not a swipe', async () => {
      let d = await drag(row(3), -40);
      await d.up();
      expect((await rowState(row(3))).open, false, 'a short drag');
      d = await drag(row(3), 120);
      await d.up();
      expect((await rowState(row(3))).open, false, 'a drag to the right (there are no options at the start)');
      await show(row(3));
      const box = await page.locator(row(3) + ' .mat-item__main').boundingBox();
      await page.mouse.move(box.x + box.width - 80, box.y + 10);
      await page.mouse.down();
      for (let i = 1; i <= 6; i += 1) { await page.mouse.move(box.x + box.width - 80 - i * 3, box.y + 10 + i * 8); }
      await page.mouse.up();
      expect((await rowState(row(3))).open, false, 'a vertical move');
    });
    await check('the More button opens the end side and focuses the first option; Escape closes the row and gives the focus back', async () => {
      await show(row(1));
      await page.click(row(1) + ' .mat-item__more');
      const s = await rowState(row(1));
      expect([s.end, s.expanded, s.inert, s.visibility], [true, 'true', false, 'visible'], 'the row after More');
      expect((await active()).action, 'archive', 'the first option has the focus');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(300);
      expect((await rowState(row(1))).open, false, 'Escape');
      expectTrue((await active()).cls.includes('mat-item__more'), 'the focus did not go back to the More button');
      expect((await rowState(row(1))).inert, true, 'the options are inert again');
    });
    await check('the arrow keys on a row open its side; tabbing out of an open row, or pressing elsewhere, closes it', async () => {
      await page.focus(row(1) + ' .mat-item__main');
      await page.keyboard.press('ArrowLeft');
      expect([(await rowState(row(1))).end, (await active()).action], [true, 'archive'], 'ArrowLeft');
      await page.keyboard.press('Tab');
      await page.keyboard.press('Tab');
      await page.keyboard.press('Tab');
      expect((await active()).key, 'lunch', 'the focus left the open row');
      expect((await rowState(row(1))).open, false, 'the row closes when the focus leaves it');
      await page.click(row(2) + ' .mat-item__more');
      expect((await rowState(row(2))).open, true, 'the second row is open');
      await page.click('#h-itemlist-actions', { position: { x: 5, y: 5 } });
      await page.waitForTimeout(300);
      expect((await rowState(row(2))).open, false, 'a press elsewhere');
    });
    await check('an action sends mat:item-action and closes the row; an action that asks first sends it when the question is answered', async () => {
      await page.click(row(2) + ' .mat-item__more');
      await page.click(row(2) + ' .mat-item__option[data-item-action="archive"]');
      await page.waitForTimeout(300);
      expect(await events('mat:item-action'), [{ type: 'mat:item-action', action: 'archive', key: 'lunch' }], 'the event of Archive');
      expect((await rowState(row(2))).open, false, 'the row after the action');
      await page.click(row(3) + ' .mat-item__more');
      await page.click(row(3) + ' .mat-item__option[data-item-action="delete"]');
      try {
        await page.waitForSelector('dialog[open]');
        expect(await page.locator('dialog[open]').count(), 1, 'the question');
        expect((await events('mat:item-action')).length, 1, 'the event before the answer');
        await page.click('dialog[open] .mat-dialog__foot .mat-btn--primary');
        await page.waitForFunction(() => !document.querySelector('dialog[open]'));
      } finally {
        // a question that is left open would block every check that follows
        await page.evaluate(() => document.querySelectorAll('dialog[open]').forEach(d => d.close()));
      }
      await page.waitForTimeout(350);
      expect((await events('mat:item-action')).map(e => e.action + ':' + e.key), ['archive:lunch', 'delete:notes'], 'the events');
      expect((await rowState(row(3))).open, false, 'the row after the answer');
    });
    await check('with actions on both sides the More button opens the end and closes it again; the Right arrow opens the start, the Left arrow closes it, and the focus goes back to the row', async () => {
      const both = n => `#il-both > .mat-item:nth-child(${n})`;
      await show(both(1));
      await page.click(both(1) + ' .mat-item__more');
      expect([(await rowState(both(1))).end, (await active()).action], [true, 'later'], 'first press');
      await page.click(both(1) + ' .mat-item__more');
      expect((await rowState(both(1))).open, false, 'second press');
      await page.focus(both(2) + ' .mat-item__main');
      await page.keyboard.press('ArrowRight');
      await page.waitForTimeout(350);
      const start = await rowState(both(2));
      expect([start.start, start.end, (await active()).action], [true, false, 'done'], 'ArrowRight');
      expectTrue(start.moved > 0, 'the row did not slide to the right');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(300);
      expect([(await rowState(both(2))).open, (await active()).cls.includes('mat-item__main')], [false, true], 'Escape closes the start side and the focus goes to the row');
      // among the options the arrows walk; from the last option of the start side the Tab key leads to the row, where the Left arrow closes the side
      await page.click(both(1) + ' .mat-item__more');
      await page.keyboard.press('ArrowRight');
      expect((await active()).action, 'remove', 'ArrowRight among the options');
      await page.keyboard.press('ArrowLeft');
      expect((await active()).action, 'later', 'ArrowLeft among the options');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(300);
      await page.focus(both(2) + ' .mat-item__main');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('Tab');
      expect((await active()).cls.includes('mat-item__main'), true, 'Tab from the option of the start side leads to the row');
      await page.keyboard.press('ArrowLeft');
      await page.waitForTimeout(300);
      expect((await rowState(both(2))).open, false, 'the Left arrow closes the start side');
    });
    await check('the texts of the lists come in German when the page is German', async () => {
      const s = await page.evaluate(() => {
        document.documentElement.lang = 'de';
        const ul = document.createElement('ul');
        ul.className = 'mat-itemlist'; ul.id = 'il-de'; ul.setAttribute('data-itemlist', ''); ul.setAttribute('data-reorder', '');
        ul.innerHTML = '<li class="mat-item mat-item--sliding"><div class="mat-item__row"><div class="mat-item__main"><span class="mat-item__title">Anna</span></div><div class="mat-item__options mat-item__options--end"><button type="button" class="mat-item__option">Löschen</button></div><div class="mat-item__aside"><button type="button" class="mat-item__handle">x</button></div></div></li><li class="mat-item"><div class="mat-item__main"><span class="mat-item__title">Ben</span></div><div class="mat-item__aside"><button type="button" class="mat-item__handle">y</button></div></li>';
        document.body.appendChild(ul);
        window.MatUi.refresh(document.body);
        const r = { more: ul.querySelector('.mat-item__more').getAttribute('aria-label'), handle: ul.querySelector('.mat-item__handle').getAttribute('aria-label') };
        ul.nextElementSibling.remove();
        ul.remove();
        document.documentElement.lang = 'en';
        return r;
      });
      expect(s, { more: 'Weitere Aktionen für Anna', handle: 'Anna verschieben' }, 'German names');
    });

    // ---- reordering
    const reorder = '#il-reorder';
    const handle = n => `${reorder} > .mat-item:nth-child(${n}) .mat-item__handle`;
    const titles = () => page.$$eval(reorder + ' .mat-item__title', t => t.map(x => x.textContent.trim()));
    const say = () => page.$eval(reorder + ' + [role=status]', s => s.textContent);
    await check('a handle drags a row: the others make room, the list is in the new order when it is dropped, and mat:reorder tells it', async () => {
      await show(reorder);
      const hb = await page.locator(handle(1)).boundingBox();
      const rb = await page.locator(`${reorder} > .mat-item:nth-child(2)`).boundingBox();
      const x = hb.x + hb.width / 2, y = hb.y + hb.height / 2;
      await page.mouse.move(x, y);
      await page.mouse.down();
      for (let i = 1; i <= 12; i += 1) { await page.mouse.move(x, y + rb.height * 2.2 * i / 12); }
      const mid = await page.evaluate(sel => ({
        dragging: document.querySelector(sel + ' > .mat-item:nth-child(1)').classList.contains('is-dragging'), reordering: document.querySelector(sel).classList.contains('is-reordering'),
        shifts: [...document.querySelectorAll(sel + ' > .mat-item')].map(li => li.style.getPropertyValue('--mat-item-dy')),
        shadow: getComputedStyle(document.querySelector(sel + ' > .mat-item:nth-child(1)')).boxShadow !== 'none',
      }), reorder);
      expect([mid.dragging, mid.reordering, mid.shadow], [true, true, true], 'while dragging');
      expect(mid.shifts.slice(1, 3).every(s => parseFloat(s) < 0) && mid.shifts.slice(3).every(s => parseFloat(s) === 0), true, 'the rows between make room: ' + mid.shifts.join(' | '));
      await page.mouse.up();
      await page.waitForTimeout(400);
      expect(await titles(), ['Commute', 'Focus', 'Morning', 'Evening', 'Night'], 'the new order');
      expect((await events('mat:reorder')).map(e => [e.key, e.from, e.to, e.order.join(',')]), [['morning', 0, 2, 'commute,focus,morning,evening,night']], 'the event');
      const clean = await page.evaluate(sel => [document.querySelector(sel).classList.contains('is-reordering'), [...document.querySelectorAll(sel + ' .mat-item')].some(li => li.classList.contains('is-dragging') || li.style.getPropertyValue('--mat-item-dy') !== '')], reorder);
      expect(clean, [false, false], 'nothing of the drag is left');
    });
    await check('with the keyboard Space picks a row up, the arrow keys move it, Space drops it and mat:reorder tells it; a live region says each step', async () => {
      await page.focus(handle(1));
      await page.keyboard.press('Space');
      expect(await page.getAttribute(handle(1), 'aria-pressed'), 'true', 'pressed');
      expect(await say(), 'Commute picked up, position 1 of 5. Arrow keys move it, Space drops it, Escape cancels.', 'what is said when a row is picked up');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      expect(await titles(), ['Focus', 'Morning', 'Commute', 'Evening', 'Night'], 'the order while it is held');
      expect(await say(), 'Commute, position 3 of 5', 'what is said when it moves');
      expect(await page.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Reorder Commute', 'the focus stays on the handle of the row');
      await page.keyboard.press('Space');
      expect(await say(), 'Commute dropped, position 3 of 5', 'what is said when it is dropped');
      expect((await events('mat:reorder')).map(e => e.key + ':' + e.from + '>' + e.to), ['morning:0>2', 'commute:0>2'], 'the events');
      expect(await page.getAttribute(handle(3), 'aria-pressed'), 'false', 'released');
    });
    await check('Escape puts a held row back and sends nothing; Home and End go to the ends', async () => {
      const before = (await events('mat:reorder')).length;
      await page.focus(handle(5));
      await page.keyboard.press('Space');
      await page.keyboard.press('ArrowUp');
      await page.keyboard.press('ArrowUp');
      expect((await titles()).indexOf('Night'), 2, 'Night moved up two places');
      await page.keyboard.press('Escape');
      expect(await titles(), ['Focus', 'Morning', 'Commute', 'Evening', 'Night'], 'the order after Escape');
      expect(await say(), 'Reordering cancelled, Night is back at position 5', 'what is said');
      expect((await events('mat:reorder')).length, before, 'an event was sent');
      await page.focus(handle(2));
      await page.keyboard.press('Space');
      await page.keyboard.press('End');
      expect((await titles()).at(-1), 'Morning', 'End');
      await page.keyboard.press('Home');
      expect((await titles())[0], 'Morning', 'Home');
      await page.keyboard.press('Enter');
      expect((await events('mat:reorder')).length, before + 1, 'one event for the move from the pick-up to the drop');
    });

    // ---- selecting and filtering
    await check('select-all selects the rows that are shown, the state in between is shown, and the bar says how many', async () => {
      await show('#il-select');
      await page.check('#il-select-all');
      const all = await page.evaluate(() => ({
        rows: [...document.querySelectorAll('#il-select .mat-item')].map(li => li.classList.contains('is-selected')),
        count: document.querySelector('#il-select').closest('[data-selectable]').querySelector('[data-bulk-count]').textContent, bar: !document.querySelector('#il-select').closest('[data-selectable]').querySelector('[data-bulk-bar]').hidden,
      }));
      expect(all, { rows: [true, true, true], count: '3', bar: true }, 'after select-all');
      await page.uncheck('#il-sel-2');
      const some = await page.evaluate(() => ({ state: document.getElementById('il-select-all').indeterminate, row: document.getElementById('il-sel-2').closest('.mat-item').classList.contains('is-selected'), count: document.querySelector('#il-select').closest('[data-selectable]').querySelector('[data-bulk-count]').textContent }));
      expect(some, { state: true, row: false, count: '2' }, 'after one is unchecked');
    });
    await check('a filter field hides the rows that do not contain its text and the letters with no row left, says how many are shown, and shows the empty row', async () => {
      await show('#il-contacts-q');
      await page.fill('#il-contacts-q', 'CARTER');
      const one = await page.evaluate(() => ({
        rows: [...document.querySelectorAll('#il-contacts .mat-item:not([hidden]) .mat-item__title')].map(t => t.textContent),
        groups: [...document.querySelectorAll('#il-contacts .mat-itemlist__group:not([hidden]) .mat-itemlist__divider')].map(t => t.textContent),
        empty: document.querySelector('#il-contacts .mat-itemlist__empty').hidden, live: document.querySelector('#il-contacts + [role=status]').textContent,
      }));
      expect(one, { rows: ['Ben Carter'], groups: ['B'], empty: true, live: '1 of 10 shown' }, 'for carter');
      expect((await events('mat:item-filter')).at(-1), { type: 'mat:item-filter', text: 'CARTER', shown: 1, total: 10 }, 'the event');
      await page.fill('#il-contacts-q', 'zzz');
      const none = await page.evaluate(() => ({ empty: document.querySelector('#il-contacts .mat-itemlist__empty').hidden, text: document.querySelector('#il-contacts .mat-itemlist__empty').textContent, live: document.querySelector('#il-contacts + [role=status]').textContent }));
      expect(none, { empty: false, text: 'Nothing matches', live: 'Nothing matches' }, 'for zzz');
      await page.fill('#il-contacts-q', '');
      expect(await page.evaluate(() => [document.querySelectorAll('#il-contacts .mat-item:not([hidden])').length, document.querySelector('#il-contacts .mat-itemlist__empty').hidden]), [10, true], 'after the field is cleared');
    });
    await check('the letter of a group stays at the top of the scrolling list until the next one pushes it away', async () => {
      await show('#il-contacts');
      const top = async () => page.evaluate(() => {
        const list = document.getElementById('il-contacts'), lt = list.getBoundingClientRect().top;
        return [...list.querySelectorAll('.mat-itemlist__divider')].map(d => [d.textContent, Math.round(d.getBoundingClientRect().top - lt)]).filter(p => p[1] <= 2).map(p => p[0]).pop();
      });
      await page.$eval('#il-contacts', l => { l.scrollTop = 0; });
      expect(await top(), 'A', 'at the start');
      await page.$eval('#il-contacts', l => { l.scrollTop = 200; });
      expect(await top(), 'B', 'after scrolling past the first group');
      const pinned = await page.$eval('#il-contacts', l => { const d = [...l.querySelectorAll('.mat-itemlist__divider')].find(x => x.textContent === 'B'); return Math.round(d.getBoundingClientRect().top - l.getBoundingClientRect().top); });
      expectTrue(pinned >= 0 && pinned <= 2, 'the letter is not at the top of the list: ' + pinned);
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#itemlist-actions');
    await check('on a phone the rows, the handle and the options are touch targets, nothing scrolls sideways, and a swipe with a finger slides a row open', async () => {
      const slide = '#il-slide', first = slide + ' > .mat-item:nth-child(1)';
      await page.locator(slide).scrollIntoViewIfNeeded();
      const s = await page.evaluate(() => ({
        row: Math.round(document.querySelector('#il-slide .mat-item__main').getBoundingClientRect().height),
        more: Math.round(document.querySelector('#il-slide .mat-item__more').getBoundingClientRect().width),
        handle: (r => [Math.round(r.width), Math.round(r.height)])(document.querySelector('#il-reorder .mat-item__handle').getBoundingClientRect()),
        option: Math.round(document.querySelector('#il-slide .mat-item__option').getBoundingClientRect().height),
        sideways: document.documentElement.scrollWidth > innerWidth,
      }));
      expectTrue(s.row >= 52 && s.more >= 44 && s.handle[0] >= 44 && s.handle[1] >= 44 && s.option >= 52, 'sizes: ' + JSON.stringify(s));
      expect(s.sideways, false, 'the page scrolls sideways');
      const box = await page.locator(first + ' .mat-item__main').boundingBox();
      const client = await ctx.newCDPSession(page);
      const touch = (type, x, y) => client.send('Input.dispatchTouchEvent', { type, touchPoints: type === 'touchEnd' ? [] : [{ x, y }] });
      const y = box.y + box.height / 2, x0 = box.x + box.width - 90;
      await touch('touchStart', x0, y);
      for (let i = 1; i <= 8; i += 1) { await touch('touchMove', x0 - i * 22, y); }
      await touch('touchEnd', 0, 0);
      await page.waitForTimeout(400);
      const o = await page.$eval(first, li => ({ open: li.classList.contains('is-open-end'), moved: Math.round(li.querySelector('.mat-item__row').getBoundingClientRect().left - li.getBoundingClientRect().left), width: li.querySelector('.mat-item__options--end').offsetWidth }));
      expect([o.open, o.moved], [true, -o.width], 'the row after a swipe');
      expectTrue(o.width <= 390 * 0.75, 'the options take more than three quarters of the phone: ' + o.width);
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the form controls
  console.log('\nForm controls');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#range');
    await check('a range writes its value next to it and fills the track up to it', async () => {
      await page.$eval('#rng-vol', el => { el.value = 70; el.dispatchEvent(new Event('input', { bubbles: true })); });
      expect((await page.textContent('#rng-vol-out')).trim(), '70', 'output');
      expect(await page.$eval('#rng-vol', el => el.style.getPropertyValue('--mat-range')), '70%', 'filled part');
      expect(await page.$eval('#rng-days', el => el.value), '28', 'a value on the grid of the steps');
    });
    await check('a counter shows the length and the limit', async () => {
      await page.fill('#spc-note', 'abc');
      expect((await page.textContent('#spc-note-count')).trim(), '3 / 120', 'counter');
    });
    await check('the eye shows and hides the password and says so with aria-pressed', async () => {
      await page.click('[data-password-toggle="spc-pw"]');
      expect(await page.$eval('#spc-pw', el => el.type), 'text', 'type while shown');
      expect(await page.getAttribute('[data-password-toggle="spc-pw"]', 'aria-pressed'), 'true', 'pressed');
      expectTrue(await page.locator('[data-password-toggle="spc-pw"] .mat-toggle-off').isVisible(), 'the crossed eye is not shown while the password is shown');
      await page.click('[data-password-toggle="spc-pw"]');
      expect(await page.$eval('#spc-pw', el => el.type), 'password', 'type while hidden');
    });
    await check('a check can start in between, and a label toggles the switch of a setting', async () => {
      expect(await page.$eval('#chk-all', el => el.indeterminate), true, 'indeterminate');
      const before = await page.$eval('#set-update', el => el.checked);
      await page.click('label[for="set-update"].mat-setting__text');
      expect(await page.$eval('#set-update', el => el.checked), !before, 'the switch of the setting');
    });
    await check('files dropped or chosen show their names in the drop zone', async () => {
      await page.setInputFiles('#spc-drop', { name: 'report.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4') });
      expect((await page.textContent('.mat-dropzone__files')).trim(), 'report.pdf', 'file names');
    });
    await check('a floating label moves up when the field has a value and stays in the field while it is empty', async () => {
      const label = () => page.$eval('label[for="flt-mail"]', el => getComputedStyle(el).transform);
      await page.evaluate(() => document.getElementById('flt-mail').blur());
      expect(await label(), 'none', 'label of an empty field');
      await page.fill('#flt-mail', 'a@b.c');
      await page.waitForTimeout(350);
      expectTrue((await label()) !== 'none', 'the label did not move up');
    });
    await check('the summary lists every field with an error as a link; the link moves the focus to the field', async () => {
      await page.evaluate(() => document.getElementById('validation').scrollIntoView());
      expectTrue(!(await page.locator('#validation [data-validation-summary]').isVisible()), 'the summary is shown before anything was sent');
      await page.click('#validation button[type=submit]');
      await page.waitForTimeout(250);
      expectTrue(await page.locator('#validation [data-validation-summary]').isVisible(), 'no summary');
      const items = await page.locator('#validation .mat-alert__list a').allTextContents();
      expect(items.length, 3, 'errors in the summary (name, e-mail, contact)');
      expectTrue(items[0].startsWith('Name: Please enter a name.'), 'the first entry: ' + items[0]);
      expect(await page.locator('#validation form .mat-field.is-invalid').count(), 3, 'fields of the form marked invalid');
      await page.click('#validation .mat-alert__list a >> nth=1');
      expect(await page.evaluate(() => document.activeElement.id), 'val-mail', 'the focus after a click on the second entry');
    });
    await check('a valid value takes its message away and a valid form is sent', async () => {
      await page.fill('#val-name', 'Alex');
      expect(await page.locator('#val-name').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), false, 'name after typing');
      await page.fill('#val-mail', 'alex@example.com');
      await page.check('#val-r1');
      await Promise.all([page.waitForNavigation(), page.click('#validation button[type=submit]')]);
      expectTrue(page.url().includes('name=Alex') && page.url().includes('mail=alex%40example.com'), 'the form was not sent: ' + page.url());
    });
    await check('a submit button with formnovalidate sends the form as it is, also where the check would fail, and writes no message', async () => {
      await page.evaluate(() => document.getElementById('validation').scrollIntoView());
      expect(await page.locator('#validation form .mat-field.is-invalid').count(), 0, 'fields with a message on the fresh page');
      await page.click('#validation button[type=submit]:not([formnovalidate])');
      await page.waitForTimeout(250);
      expect(await page.locator('#validation form .mat-field.is-invalid').count(), 3, 'fields with a message after Send with empty fields');
      await Promise.all([page.waitForNavigation(), page.click('#validation button[formnovalidate]')]);
      expectTrue(page.url().includes('draft=1'), 'the form was not sent by the button with formnovalidate: ' + page.url());
      expectTrue(!page.url().includes('name=Alex'), 'the page was not loaded again with the empty form: ' + page.url());
      expect(await page.locator('#validation form .mat-field.is-invalid').count(), 0, 'fields with a message after the draft was sent');
    });
    await check('a form with data-confirm sends the button that was pressed (name, value, formnovalidate) once the question is answered', async () => {
      await page.evaluate(() => {
        document.body.insertAdjacentHTML('beforeend', '<form class="mat-form" id="e2e-confirm-form" method="get" action="/Components" data-confirm="Really?" novalidate>' +
          '<input class="mat-input" name="q" required aria-label="q" /><button type="submit" class="mat-btn" name="act" value="del" formnovalidate>Delete</button></form>');
      });
      await page.click('#e2e-confirm-form button');
      await page.waitForSelector('dialog[open] button[value="ok"]');
      await Promise.all([page.waitForNavigation(), page.click('dialog[open] button[value="ok"]')]);
      expectTrue(page.url().includes('act=del'), 'the button that was pressed did not go along: ' + page.url());
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#form-grid');
    await check('on a phone the label of a horizontal form is above the field and the columns are one', async () => {
      const state = await page.evaluate(() => {
        const f = document.querySelector('#grid-h-name').closest('.mat-field');
        const label = document.querySelector('label[for="grid-h-name"]').getBoundingClientRect();
        const input = document.getElementById('grid-h-name').getBoundingClientRect();
        const cols = getComputedStyle(document.querySelector('.mat-form__grid--3')).gridTemplateColumns.split(' ').length;
        return { above: label.bottom <= input.top + 1, display: getComputedStyle(f).display, cols };
      });
      expect(state, { above: true, display: 'flex', cols: 1 }, 'form layout on a phone');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the code input
  console.log('\nCode input');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#code-input');
    await page.evaluate(() => {
      window.__otp = [];
      document.addEventListener('mat:otp-complete', e => window.__otp.push(e.target.querySelector('.mat-otp__input').id + ':' + e.detail.value));
    });
    const boxSel = id => `#${id} ~ .mat-otp__boxes .mat-otp__box`;
    const vals = id => page.$$eval(boxSel(id), els => els.map(e => e.value).join('|'));
    const real = id => page.$eval('#' + id, e => e.value);
    const active = () => page.evaluate(() => document.activeElement.id);
    const status = id => page.$eval('#' + id, el => { const s = el.closest('.mat-otp').querySelector('.mat-otp__status'); return s.hidden ? '' : s.textContent; });
    const live = id => page.$eval('#' + id, el => el.closest('.mat-otp').querySelector('[role=status]').textContent);
    const events = id => page.evaluate(id => window.__otp.filter(x => x.startsWith(id + ':')), id);
    const paste = (id, text) => page.$eval('#' + id, (el, text) => {
      const dt = new DataTransfer();
      dt.setData('text/plain', text);
      el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
    }, text);

    await check('the script draws one named box per character in a group named by the label and puts the real input out of the way', async () => {
      const s = await page.evaluate(() => {
        const input = document.getElementById('otp-totp');
        const group = input.nextElementSibling;
        const boxes = [...group.querySelectorAll('.mat-otp__box')];
        return {
          role: group.getAttribute('role'),
          label: document.getElementById(group.getAttribute('aria-labelledby')).textContent.replace(/\s+/g, ' ').trim(),
          names: boxes.map(b => b.getAttribute('aria-label')),
          autocomplete: boxes.map(b => b.getAttribute('autocomplete')).join(','),
          mode: boxes[0].getAttribute('inputmode'),
          font: Math.min(...boxes.map(b => parseFloat(getComputedStyle(b).fontSize))),
          stops: boxes.filter(b => b.tabIndex === 0).length,
          posting: boxes.some(b => b.name),
          real: { hidden: input.getAttribute('aria-hidden'), tab: input.tabIndex, opacity: getComputedStyle(input).opacity, autocomplete: input.getAttribute('autocomplete') },
        };
      });
      expect(s.role, 'group', 'role');
      expect(s.label, 'Authentication code *', 'the group is named by the label');
      expect(s.names, ['Digit 1 of 6', 'Digit 2 of 6', 'Digit 3 of 6', 'Digit 4 of 6', 'Digit 5 of 6', 'Digit 6 of 6'], 'the names of the boxes');
      expect(s.autocomplete, 'one-time-code,off,off,off,off,off', 'the SMS code is offered on the first box only');
      expect(s.mode, 'numeric', 'the keyboard of a phone');
      expectTrue(s.font >= 16, 'a phone would zoom into a box with type of ' + s.font + ' px');
      expect(s.stops, 1, 'boxes that are a tab stop');
      expect(s.posting, false, 'the boxes must not post a value of their own');
      expect(s.real, { hidden: 'true', tab: -1, opacity: '0', autocomplete: 'off' }, 'the real input');
    });
    await check('typing fills a box and moves on, Backspace clears and goes back, Delete clears; the real input posts the digits joined', async () => {
      await page.click('#otp-totp-1');
      await page.keyboard.type('123');
      expect(await vals('otp-totp'), '1|2|3|||', 'boxes after typing');
      expect(await real('otp-totp'), '123', 'the posted value');
      expect(await active(), 'otp-totp-4', 'the focus moves on');
      await page.keyboard.press('Backspace');
      expect(await vals('otp-totp'), '1|2||||', 'Backspace in an empty box clears the one before');
      expect(await active(), 'otp-totp-3', 'and goes back');
      await page.keyboard.type('9');
      await page.keyboard.press('ArrowLeft');
      await page.keyboard.press('Backspace');
      expect(await vals('otp-totp'), '1|2||||', 'Backspace in a filled box clears it');
      expect(await active(), 'otp-totp-3', 'and stays');
      await page.keyboard.press('ArrowLeft');
      await page.keyboard.press('Delete');
      expect(await vals('otp-totp'), '1|||||', 'Delete clears the box');
      expect(await active(), 'otp-totp-2', 'and stays');
      expect(await real('otp-totp'), '1', 'the posted value follows');
    });
    await check('a character that is not allowed is refused with a message under the boxes and to a screen reader; a space is skipped without a word', async () => {
      await page.keyboard.type('a');
      expect(await vals('otp-totp'), '1|||||', 'boxes after a letter');
      expect(await status('otp-totp'), 'Only digits are allowed.', 'the message under the boxes');
      expect(await live('otp-totp'), 'Only digits are allowed.', 'the live region');
      expect(await active(), 'otp-totp-2', 'the focus stays');
      await page.keyboard.type(' ');
      expect(await status('otp-totp'), '', 'a space says nothing');
      await page.keyboard.type('-');
      expect(await vals('otp-totp'), '1|||||', 'boxes after a space and a dash');
    });
    await check('the arrow keys, Home and End move between the boxes and a click selects the box, so that typing replaces it', async () => {
      await page.keyboard.press('Home');
      expect(await active(), 'otp-totp-1', 'Home');
      await page.keyboard.press('ArrowLeft');
      expect(await active(), 'otp-totp-1', 'ArrowLeft in the first box');
      await page.keyboard.press('ArrowRight');
      expect(await active(), 'otp-totp-2', 'ArrowRight');
      await page.keyboard.press('End');
      expect(await active(), 'otp-totp-6', 'End');
      await page.click('#otp-totp-1');
      expect(await page.$eval('#otp-totp-1', e => [e.selectionStart, e.selectionEnd]), [0, 1], 'the selection after a click');
      await page.keyboard.type('7');
      expect(await vals('otp-totp'), '7|||||', 'typing replaced the digit');
      expect(await active(), 'otp-totp-2', 'and moved on');
    });
    await check('the boxes are one stop for the Tab key: Tab leaves them, Shift+Tab comes back to the box where it was', async () => {
      expect(await page.$$eval(boxSel('otp-totp'), els => els.filter(b => b.tabIndex === 0).length), 1, 'boxes in the tab order');
      await page.focus('#otp-totp-3');
      await page.keyboard.press('Tab');
      expect(await page.evaluate(() => ({ inside: !!document.activeElement.closest('.mat-otp'), text: document.activeElement.textContent.trim() })), { inside: false, text: 'Verify' }, 'after Tab');
      await page.keyboard.press('Shift+Tab');
      expect(await active(), 'otp-totp-3', 'after Shift+Tab');
    });
    await check('the focus ring of a box is the accent ring (A11Y-01), and the real input hands the focus to a box (a label, a message of the validation)', async () => {
      await page.focus('#otp-totp-3');
      const ring = await page.$eval('#otp-totp-3', e => getComputedStyle(e).boxShadow);
      expectTrue(ring && ring !== 'none', 'a focused box has no ring');
      await page.click('label[for="otp-lg"]');
      expect(await active(), 'otp-lg-1', 'a click on the label');
      await page.evaluate(() => { document.getElementById('otp-md').focus(); });
      expect(await active(), 'otp-md-1', 'focus() on the real input');
    });

    await check('a pasted code is spread over the boxes, spaces and dashes are skipped, and the code is complete once', async () => {
      await page.click('#otp-md-3');
      await paste('otp-md-3', '123 456');
      expect(await vals('otp-md'), '1|2|3|4|5|6', 'boxes after the paste');
      expect(await real('otp-md'), '123456', 'the posted value');
      expect(await events('otp-md'), ['otp-md:123456'], 'the events of completion');
      await paste('otp-recovery-1', 'a1b2-c3d4');
      expect(await vals('otp-recovery'), 'A|1|B|2|C|3|D|4', 'a recovery code with a dash, converted to upper case');
      expect(await real('otp-recovery'), 'A1B2C3D4', 'the posted value');
      await page.evaluate(() => MatUi.otp.clear('otp-md'));
      await page.keyboard.type('12');
      await paste('otp-md-3', '34');
      expect(await vals('otp-md'), '1|2|3|4||', 'a part of a code goes in behind what is there');
      await paste('otp-md-1', 'x-9');
      expect(await status('otp-md'), 'Only digits are allowed.', 'a character in a pasted text is refused');
      expect(await vals('otp-md'), '9|2|3|4||', 'and the others are taken');
    });
    await check('a class of characters is the rule: [A-Z2-7] with data-case upper converts, refuses 1 and 8 and fills the boxes from the real input', async () => {
      expect(await vals('otp-base32'), 'J|B|S|W|Y|3|D|P', 'the value of the real input fills the boxes');
      await page.evaluate(() => MatUi.otp.clear('otp-base32'));
      await page.keyboard.type('a');
      expect(await vals('otp-base32'), 'A|||||||', 'a lower-case letter is converted');
      await page.keyboard.type('1');
      expect(await vals('otp-base32'), 'A|||||||', 'a 1 is refused');
      expect(await status('otp-base32'), 'That character is not allowed.', 'the message of a class');
      await page.keyboard.type('8');
      expect(await vals('otp-base32'), 'A|||||||', 'an 8 is refused');
      await page.keyboard.type('7');
      expect(await vals('otp-base32'), 'A|7||||||', 'a 7 is taken');
      await paste('otp-base32-2', 'jbsw y3dp');
      expect(await vals('otp-base32'), 'J|B|S|W|Y|3|D|P', 'a pasted key, converted, without the space');
      expect(await real('otp-base32'), 'JBSWY3DP', 'the posted value');
    });
    await check('text that arrives without a key (a phone keyboard, autofill of an SMS code, an input method) is taken the same way', async () => {
      // the box holds what the browser put there, and an input event says so
      const arrive = (id, value, inputType) => page.$eval('#' + id, (el, a) => { el.value = a.value; el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: a.inputType, data: a.value })); }, { value, inputType });
      await arrive('otp-lg-1', '7', 'insertText');
      expect(await vals('otp-lg'), '7|||||', 'one character from a keyboard');
      expect(await active(), 'otp-lg-2', 'the focus moves on');
      await arrive('otp-lg-1', '77', 'insertText');
      expect(await vals('otp-lg'), '7|||||', 'a second 7 typed into a box that kept its 7 is not a second box');
      await arrive('otp-lg-1', '', 'deleteContentBackward');
      expect(await vals('otp-lg'), '|||||', 'a deletion');
      await arrive('otp-lg-1', 'q', 'insertText');
      expect(await vals('otp-lg'), '|||||', 'a letter is refused');
      expect(await status('otp-lg'), 'Only digits are allowed.', 'with the message');
      await arrive('otp-lg-1', '135 790', 'insertReplacementText');
      expect(await vals('otp-lg'), '1|3|5|7|9|0', 'the whole code of an SMS, with a space, is spread over the boxes');
      expect(await real('otp-lg'), '135790', 'the posted value');
      expect(await events('otp-lg'), ['otp-lg:135790'], 'and the code is complete');
      // Backspace of a phone keyboard in an empty box arrives as beforeinput only
      await page.evaluate(() => MatUi.otp.clear('otp-lg'));
      await page.keyboard.type('24');
      await page.$eval('#otp-lg-3', el => el.dispatchEvent(new InputEvent('beforeinput', { bubbles: true, cancelable: true, inputType: 'deleteContentBackward' })));
      expect(await vals('otp-lg'), '2|||||', 'Backspace in an empty box clears the box before it');
      expect(await active(), 'otp-lg-2', 'and goes back');
      await page.evaluate(() => MatUi.otp.clear('otp-lg'));
    });
    await check('groups: a gap after every fourth box, or two groups of four with a dash', async () => {
      const seps = id => page.$eval('#' + id, el => [...el.nextElementSibling.children].map(c => c.classList.contains('mat-otp__sep') ? (c.classList.contains('mat-otp__sep--dash') ? '-' : '.') : 'b').join(''));
      expect(await seps('otp-recovery'), 'bbbb-bbbb', 'groups 4-4');
      expect(await seps('otp-base32'), 'bbbb.bbbb', 'groups 4');
      expect(await seps('otp-md'), 'bbbbbb', 'no groups');
      const dash = await page.$eval('#otp-recovery', el => { const s = el.nextElementSibling.querySelector('.mat-otp__sep--dash'); const r = s.getBoundingClientRect(); const d = getComputedStyle(s, '::before'); return { wide: r.width >= 12, line: d.height }; });
      expect(dash, { wide: true, line: '2px' }, 'the dash');
    });
    await check('data-mask draws dots: the boxes are password fields, the real input still posts the digits, and a PIN is not sent by itself', async () => {
      const url = page.url();
      await page.click('#otp-pin-1');
      await page.keyboard.type('1234');
      expect(await page.$$eval(boxSel('otp-pin'), els => els.map(e => e.type).join(',')), 'password,password,password,password', 'the type of the boxes');
      expect(await real('otp-pin'), '1234', 'the posted value');
      await page.waitForTimeout(400);
      expect(page.url(), url, 'a PIN has no data-submit: nothing was sent');
      expect(await events('otp-pin'), ['otp-pin:1234'], 'the code is complete');
      expect(await active(), 'otp-pin-4', 'the focus stays in the last box');
    });
    await check('completion fires mat:otp-complete with the value and tells a screen reader; it fires again for a changed code but not for the same one', async () => {
      await page.click('#otp-sm-1');
      await page.keyboard.type('123456');
      expect(await events('otp-sm'), ['otp-sm:123456'], 'after the sixth digit');
      expect(await live('otp-sm'), 'Code complete', 'the live region');
      expect(await page.$eval('#otp-sm', e => e.closest('.mat-otp').classList.contains('is-complete')), true, 'is-complete');
      await page.click('#otp-sm-3');
      await page.keyboard.type('9');
      expect(await events('otp-sm'), ['otp-sm:123456', 'otp-sm:129456'], 'a changed code');
      await page.click('#otp-sm-3');
      await page.keyboard.type('9');
      expect((await events('otp-sm')).length, 2, 'the same digit again is no change');
      await page.keyboard.press('Backspace');
      expect(await page.$eval('#otp-sm', e => e.closest('.mat-otp').classList.contains('is-complete')), false, 'no longer complete');
    });
    await check('a disabled code input cannot be focused; a read-only one can be walked but not changed', async () => {
      expect(await page.$$eval(boxSel('otp-off'), els => els.every(b => b.disabled)), true, 'the boxes of a disabled input');
      expect(await vals('otp-off'), '4|8|2|9|1|3', 'its value');
      expect(await page.$$eval(boxSel('otp-ro'), els => els.every(b => b.readOnly && !b.disabled)), true, 'the boxes of a read-only input');
      await page.click('#otp-ro-2');
      await page.keyboard.type('5');
      await page.keyboard.press('Backspace');
      await page.keyboard.press('Delete');
      expect(await vals('otp-ro'), '4|8|2|9|1|3', 'a read-only code does not change');
    });
    await check('a code that a server rejected is drawn red with aria-invalid on every box', async () => {
      const state = await page.evaluate(() => {
        const probe = document.createElement('i');
        probe.style.color = 'var(--mat-danger)';
        document.body.appendChild(probe);
        const danger = getComputedStyle(probe).color;
        probe.remove();
        const boxes = [...document.querySelectorAll('#otp-bad ~ .mat-otp__boxes .mat-otp__box')];
        return { danger, borders: boxes.map(b => getComputedStyle(b).borderTopColor === danger), invalid: boxes.map(b => b.getAttribute('aria-invalid')).join(',') };
      });
      expect(state.borders.every(Boolean), true, 'the border of every box is the danger colour');
      expect(state.invalid, 'true,true,true,true,true,true', 'aria-invalid');
      expect(await page.evaluate(() => document.getElementById('otp-bad').closest('.mat-otp').nextElementSibling.textContent.trim()), 'That code is not valid or has expired. Check the app and try again.', 'the message of the field');
    });
    await ctx.close();
  }
  {
    // the form validation and the application programming interface, on a page of their own (a complete code is sent by this field)
    const { ctx, page } = await open(desktopOptions, '/Components#code-input');
    const boxSel = id => `#${id} ~ .mat-otp__boxes .mat-otp__box`;
    const vals = id => page.$$eval(boxSel(id), els => els.map(e => e.value).join('|'));
    const active = () => page.evaluate(() => document.activeElement.id);
    const field = id => page.$eval('#' + id, el => { const f = el.closest('.mat-field'); return { invalid: f.classList.contains('is-invalid'), message: f.querySelector('.mat-error').textContent.trim() }; });
    const ariaInvalid = id => page.$$eval(boxSel(id), els => els.map(e => e.getAttribute('aria-invalid')).join(','));

    await check('required: a form sent with an empty code says so at the field, and the focus goes to the first box', async () => {
      await page.click('form:has(#otp-totp) button[type=submit]');
      expect(await field('otp-totp'), { invalid: true, message: 'Please enter the six digits of the code.' }, 'the field');
      expect(await active(), 'otp-totp-1', 'the focus');
      await page.waitForFunction(() => document.getElementById('otp-totp-1').getAttribute('aria-invalid') === 'true');
      expect(await ariaInvalid('otp-totp'), 'true,true,true,true,true,true', 'aria-invalid on the boxes');
    });
    await check('an incomplete code is not valid: the message of the pattern, the focus on the box where the code goes on, and the message goes when it is complete', async () => {
      await page.keyboard.type('482');
      expect(await field('otp-totp'), { invalid: true, message: 'Please enter the six digits of the code.' }, 'typing does not clear the message of an invalid field while the code is not valid');
      await page.click('form:has(#otp-totp) button[type=submit]');
      expect(await field('otp-totp'), { invalid: true, message: 'The code has six digits.' }, 'the message of the pattern');
      expect(await active(), 'otp-totp-4', 'the focus goes on where the code goes on');
      await page.click('#otp-pin-1');
      await page.keyboard.type('12');
      await page.click('form:has(#otp-pin) button[type=submit]');
      expect(await field('otp-pin'), { invalid: true, message: 'The PIN has four digits.' }, 'the PIN');
      await page.click('#otp-pin-3');
      await page.keyboard.type('34');
      expect(await field('otp-pin'), { invalid: false, message: '' }, 'the field once the PIN is complete');
      await page.waitForFunction(() => document.getElementById('otp-pin-1').getAttribute('aria-invalid') === null);
    });
    await check('the application programming interface: value sets without sending, setInvalid marks with a message, clear empties and focuses the first box', async () => {
      await page.evaluate(() => {
        window.__otp = [];
        document.addEventListener('mat:otp-complete', e => window.__otp.push(e.detail.value));
        window.__value = MatUi.otp.value('otp-totp', '135790');
      });
      expect(await page.evaluate(() => window.__value), '135790', 'value() returns the code');
      expect(await vals('otp-totp'), '1|3|5|7|9|0', 'the boxes follow');
      await page.waitForTimeout(400);
      expect(await page.evaluate(() => window.__otp), [], 'setting a code is not completing it: no event, nothing sent');
      expectTrue(page.url().indexOf('code=') < 0, 'the form was sent: ' + page.url());
      await page.evaluate(() => MatUi.otp.setInvalid('otp-totp', 'That code is not valid.'));
      expect(await field('otp-totp'), { invalid: true, message: 'That code is not valid.' }, 'the field');
      await page.waitForFunction(() => document.getElementById('otp-totp-6').getAttribute('aria-invalid') === 'true');
      await page.evaluate(() => MatUi.otp.setInvalid('otp-totp', ''));
      expect(await field('otp-totp'), { invalid: false, message: '' }, 'the mark taken away');
      await page.evaluate(() => MatUi.otp.setInvalid('otp-totp', 'Rejected again.'));
      await page.evaluate(() => MatUi.otp.clear('otp-totp'));
      expect(await vals('otp-totp'), '|||||', 'clear() empties the boxes');
      expect(await field('otp-totp'), { invalid: false, message: '' }, 'and takes the message away');
      expect(await active(), 'otp-totp-1', 'and focuses the first box');
      expect(await page.$eval('#otp-totp', e => e.value), '', 'the real input is empty');
      await page.keyboard.type('12');
      await page.evaluate(() => document.activeElement.blur());
      await page.evaluate(() => MatUi.otp.focus('otp-totp'));
      expect(await active(), 'otp-totp-3', 'focus() goes to the box where the code goes on');
    });
    await check('a complete code is sent by data-submit with the validation of the form, and an app can take over by cancelling the event', async () => {
      await page.evaluate(() => MatUi.otp.clear('otp-totp'));
      await page.evaluate(() => document.getElementById('otp-totp').closest('.mat-otp').addEventListener('mat:otp-complete', e => e.preventDefault(), { once: true }));
      await page.keyboard.type('111222');
      await page.waitForTimeout(500);
      expectTrue(page.url().indexOf('code=') < 0, 'an app that cancelled the event does not want the form sent: ' + page.url());
      await page.evaluate(() => MatUi.otp.clear('otp-totp'));
      await Promise.all([page.waitForNavigation(), page.keyboard.type('482913')]);
      expectTrue(page.url().includes('code=482913'), 'the form was not sent with the last digit: ' + page.url());
    });
    await check('data-submit="#id" clicks that button', async () => {
      await page.evaluate(() => { localStorage.removeItem('otp-clicked'); document.getElementById('otp-signin-go').addEventListener('click', () => localStorage.setItem('otp-clicked', '1')); });
      await page.click('#otp-signin-1');
      await Promise.all([page.waitForNavigation(), page.keyboard.type('246810')]);
      expectTrue(page.url().includes('signin=246810'), 'the form was not sent: ' + page.url());
      expect(await page.evaluate(() => localStorage.getItem('otp-clicked')), '1', 'the button was clicked');
    });
    await ctx.close();
  }
  {
    // markup that is added later, a form that is reset, another language and a code input outside a field
    const { ctx, page } = await open(desktopOptions, '/Components#code-input');
    await check('MatUi.refresh draws the boxes of markup added later: 12 boxes in three groups, in German, that a form reset puts back', async () => {
      await page.evaluate(() => {
        document.documentElement.lang = 'de';
        const host = document.createElement('div');
        host.id = 'otp-host';
        host.innerHTML = '<form class="mat-form" id="otp-dyn-form" method="get" action="/Components#code-input" novalidate>' +
          '<div class="mat-field"><label class="mat-label" for="otp-dyn">Schlüssel</label>' +
          '<div class="mat-otp" data-otp data-length="12" data-chars="hex" data-group="4-4-4"><input class="mat-otp__input" id="otp-dyn" name="dyn" value="abc" required /></div>' +
          '<span class="mat-error" aria-live="polite"></span></div>' +
          '<button type="submit" class="mat-btn mat-btn--primary" id="otp-dyn-go">Senden</button></form>' +
          '<div class="mat-otp" data-otp data-length="4" id="otp-bare"><input class="mat-otp__input" id="otp-bare-in" name="bare" /></div>';
        document.getElementById('code-input').appendChild(host);
        MatUi.refresh(host);
      });
      const s = await page.evaluate(() => {
        const boxes = [...document.querySelectorAll('#otp-dyn ~ .mat-otp__boxes .mat-otp__box')];
        return { count: boxes.length, first: boxes[0].getAttribute('aria-label'), last: boxes[11].getAttribute('aria-label'), values: boxes.map(b => b.value).join(''), seps: document.querySelectorAll('#otp-dyn ~ .mat-otp__boxes .mat-otp__sep--dash').length, ready: document.querySelector('#otp-dyn').closest('.mat-otp').classList.contains('is-ready') };
      });
      expect(s, { count: 12, first: 'Zeichen 1 von 12', last: 'Zeichen 12 von 12', values: 'abc', seps: 2, ready: true }, 'the boxes');
      await page.click('#otp-dyn-4');
      await page.keyboard.type('z');
      expect(await page.$eval('#otp-dyn', el => el.closest('.mat-otp').querySelector('.mat-otp__status').textContent), 'Es sind nur die Ziffern 0 bis 9 und die Buchstaben A bis F erlaubt.', 'the refusal in German');
      await page.click('#otp-dyn-go');
      expect(await page.$eval('#otp-dyn', el => el.closest('.mat-field').querySelector('.mat-error').textContent), 'Bitte alle 12 Zeichen eingeben.', 'an incomplete code without a pattern, in German');
      await page.keyboard.type('def0');
      expect(await page.$eval('#otp-dyn', e => e.value), 'abcdef0', 'typed after the message');
      await page.evaluate(() => document.getElementById('otp-dyn-form').reset());
      await page.waitForTimeout(100);
      expect(await page.$$eval('#otp-dyn ~ .mat-otp__boxes .mat-otp__box', els => els.map(b => b.value).join('')), 'abc', 'the boxes after a reset');
      expect(await page.$eval('#otp-dyn', e => e.value), 'abc', 'the real input after a reset');
    });
    await check('a code input outside a field is marked on the wrapper, and the message is said under the boxes', async () => {
      await page.evaluate(() => MatUi.otp.setInvalid('otp-bare-in', 'Not accepted.'));
      const s = await page.evaluate(() => ({ cls: document.getElementById('otp-bare').classList.contains('is-invalid'), text: document.querySelector('#otp-bare .mat-otp__status').textContent, aria: document.getElementById('otp-bare-in').getAttribute('aria-invalid'), border: getComputedStyle(document.querySelector('#otp-bare .mat-otp__box')).borderTopColor }));
      expect(s.cls && s.text === 'Not accepted.' && s.aria === 'true', true, 'the wrapper, the message and aria-invalid');
      await page.evaluate(() => { document.getElementById('otp-host').remove(); document.documentElement.lang = 'en'; });
    });
    await ctx.close();
  }
  {
    // without the script the real input is a text field
    const ctx = await browser.newContext({ ...desktopOptions, javaScriptEnabled: false });
    const page = await ctx.newPage();
    await page.goto(BASE + '/Components#code-input', { waitUntil: 'networkidle' });
    await check('without the script the real input is a visible text field with the look of a code', async () => {
      const s = await page.evaluate(() => {
        const i = document.getElementById('otp-totp');
        const c = getComputedStyle(i);
        return { boxes: document.querySelectorAll('#code-input .mat-otp__box').length, opacity: c.opacity, border: c.borderTopWidth, width: i.getBoundingClientRect().width > 100, mono: /mono|consolas|menlo|courier/i.test(c.fontFamily) };
      });
      expect(s, { boxes: 0, opacity: '1', border: '1px', width: true, mono: true }, 'the plain field');
      await page.fill('#otp-totp', '123456');
      expect(await page.inputValue('#otp-totp'), '123456', 'a code can be typed');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#code-input');
    await check('on a phone eight boxes fit the screen, are 44 px high and keep 16 px type, and a code of twelve fits too', async () => {
      await page.evaluate(() => {
        const host = document.createElement('div');
        host.id = 'otp-host';
        host.innerHTML = '<div class="mat-field"><label class="mat-label" for="otp-twelve">Twelve</label><div class="mat-otp mat-otp--lg" data-otp data-length="12" data-chars="hex" data-group="4-4-4"><input class="mat-otp__input" id="otp-twelve" name="twelve" /></div></div>';
        document.getElementById('code-input').appendChild(host);
        MatUi.refresh(host);
      });
      const measure = id => page.evaluate(id => {
        const wrap = document.getElementById(id).closest('.mat-otp');
        const boxes = [...wrap.querySelectorAll('.mat-otp__box')];
        const rects = boxes.map(b => b.getBoundingClientRect());
        const field = wrap.closest('.mat-field').getBoundingClientRect();
        return {
          count: boxes.length,
          inside: Math.max(...rects.map(r => r.right)) <= field.right + 0.5 && Math.min(...rects.map(r => r.left)) >= field.left - 0.5,
          onScreen: Math.max(...rects.map(r => r.right)) <= innerWidth,
          height: Math.round(Math.min(...rects.map(r => r.height))),
          width: Math.round(Math.min(...rects.map(r => r.width))),
          font: Math.min(...boxes.map(b => parseFloat(getComputedStyle(b).fontSize))),
          scrolls: document.documentElement.scrollWidth > innerWidth,
        };
      }, id);
      const eight = await measure('otp-recovery');
      expect({ count: eight.count, inside: eight.inside, onScreen: eight.onScreen, scrolls: eight.scrolls }, { count: 8, inside: true, onScreen: true, scrolls: false }, 'eight boxes with a dash');
      expectTrue(eight.height >= 44, 'the boxes are ' + eight.height + ' px high');
      expectTrue(eight.font >= 16, 'the type is ' + eight.font + ' px');
      expectTrue(eight.width >= 24, 'the boxes are ' + eight.width + ' px wide');
      const small = await measure('otp-sm');
      expectTrue(small.height >= 44, 'a small box is ' + small.height + ' px high on a touch screen');
      const twelve = await measure('otp-twelve');
      expect({ count: twelve.count, inside: twelve.inside, onScreen: twelve.onScreen, scrolls: twelve.scrolls }, { count: 12, inside: true, onScreen: true, scrolls: false }, 'twelve boxes in large');
      expectTrue(twelve.font >= 16, 'the type of twelve is ' + twelve.font + ' px');
    });
    await check('on a phone a tap selects a box and the code is typed and pasted as on a computer', async () => {
      await page.tap('#otp-recovery-1');
      await page.keyboard.type('a1b2c3d4');
      expect(await page.$eval('#otp-recovery', e => e.value), 'A1B2C3D4', 'the posted value');
      await page.tap('#otp-md-2');
      expect(await page.evaluate(() => document.activeElement.id), 'otp-md-2', 'the box that was tapped');
      expect(await page.$eval('#otp-md-2', e => e.selectionEnd - e.selectionStart <= 1), true, 'selection');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- content components
  console.log('\nContent components');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#accordion');
    await check('in an accordion with one name only one item is open; a single collapse opens on its own', async () => {
      const open = () => page.$$eval('details[name="acc-faq"]', items => items.map(d => d.open));
      expect(await open(), [true, false, false], 'items at the start');
      await page.click('details[name="acc-faq"]:nth-of-type(2) > summary');
      expect(await open(), [false, true, false], 'items after opening the second');
      await page.click('.mat-collapse > summary');
      expect(await page.$eval('.mat-collapse', d => d.open), true, 'the collapse');
    });
    await page.evaluate(() => document.getElementById('button-group').scrollIntoView());
    await check('a group of radios drawn as buttons selects one and moves with the arrow keys', async () => {
      await page.click('label[for="bg-month"]');
      expect(await page.$eval('#bg-month', el => el.checked), true, 'month after the click');
      await page.focus('#bg-month');
      await page.keyboard.press('ArrowLeft');
      expect(await page.$eval('#bg-week', el => el.checked), true, 'week after the arrow key');
    });
    await page.evaluate(() => document.getElementById('dropdown').scrollIntoView());
    await check('a dropdown opens next to its button, stays on the screen and Escape returns the focus to the button', async () => {
      await page.click('[data-menu-toggle="dd-menu"]');
      expectTrue(await page.locator('#dd-menu').isVisible(), 'the menu is not shown');
      expect(await page.getAttribute('[data-menu-toggle="dd-menu"]', 'aria-expanded'), 'true', 'aria-expanded');
      const box = await page.$eval('#dd-menu', el => { const r = el.getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth && r.top >= 0 && r.bottom <= innerHeight; });
      expect(box, true, 'the menu is inside the window');
      await page.keyboard.press('Escape');
      expectTrue(!(await page.locator('#dd-menu').isVisible()), 'the menu is still shown after Escape');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-menu-toggle')), 'dd-menu', 'the focus after Escape');
    });
    await page.evaluate(() => document.getElementById('popover').scrollIntoView());
    await check('a popover opens on a click and closes with a click outside', async () => {
      await page.click('[data-menu-toggle="pop-help"]');
      expectTrue(await page.locator('#pop-help').isVisible(), 'the popover is not shown');
      await page.click('#popover .doc-section__head, #popover h2, body', { position: { x: 5, y: 5 } });
      expectTrue(!(await page.locator('#pop-help').isVisible()), 'the popover is still shown after a click outside');
    });
    await page.evaluate(() => document.getElementById('table').scrollIntoView());
    await check('the check in the header selects every row, shows the bulk bar with the count and marks the rows', async () => {
      const box = '[data-selectable]:has(#tsel-all)';   // the lists of rows have a bar of their own on the same page
      expectTrue(!(await page.locator(box + ' [data-bulk-bar]').isVisible()), 'the bulk bar is shown before a row is selected');
      await page.check('#tsel-all');
      expect((await page.textContent(box + ' [data-bulk-count]')).trim(), '3', 'selected');
      expectTrue(await page.locator(box + ' [data-bulk-bar]').isVisible(), 'the bulk bar');
      expect(await page.locator(box + ' tr.is-selected').count(), 3, 'rows marked');
      await page.uncheck('#tsel-2');
      expect((await page.textContent(box + ' [data-bulk-count]')).trim(), '2', 'selected after one is taken away');
      expect(await page.$eval('#tsel-all', el => el.indeterminate), true, 'the header check shows the state in between');
      await page.uncheck('#tsel-1');
      await page.uncheck('#tsel-3');
      expectTrue(!(await page.locator(box + ' [data-bulk-bar]').isVisible()), 'the bulk bar is still shown with nothing selected');
    });
    await page.evaluate(() => document.getElementById('cards').scrollIntoView());
    await check('a card with data-select-item is marked when its check is ticked, like a row: a tint behind the whole card, no bar at one side, and the bar counts', async () => {
      const box = '[data-selectable]:has(#csel-1)';
      const look = (sel) => page.$eval(sel, c => { const s = getComputedStyle(c); return { bg: s.backgroundColor, widths: [s.borderTopWidth, s.borderRightWidth, s.borderBottomWidth, s.borderLeftWidth].join(' ') }; });
      expect(await page.locator(box + ' .is-selected').count(), 0, 'cards marked at the start');
      expectTrue(!(await page.locator(box + ' [data-bulk-bar]').isVisible()), 'the bulk bar is shown before a card is selected');
      await page.check('#csel-1');
      await page.check('#csel-3');
      expect((await page.textContent(box + ' [data-bulk-count]')).trim(), '2', 'selected');
      expectTrue(await page.locator(box + ' [data-bulk-bar]').isVisible(), 'the bulk bar');
      expect(await page.$$eval(box + ' .mat-card', cards => cards.map(c => c.classList.contains('is-selected'))), [true, false, true], 'cards marked');
      const on = await look(box + ' .mat-card.is-selected');
      const off = await look(box + ' .mat-card:not(.is-selected)');
      expectTrue(on.bg !== off.bg, 'a selected card has the background of one that is not: ' + on.bg);
      expect(on.widths, off.widths, 'border widths of a selected card (a tint, not a bar at one side)');
      await page.uncheck('#csel-1');
      await page.uncheck('#csel-3');
      expect(await page.locator(box + ' .is-selected').count(), 0, 'cards marked after both were taken away');
      expectTrue(!(await page.locator(box + ' [data-bulk-bar]').isVisible()), 'the bulk bar is still shown with nothing selected');
    });
    await check('a .mat-tile and any element with data-select-item are marked too, also in markup that a script added, and the check for all reaches them; other elements are left alone', async () => {
      await page.evaluate(() => {
        document.body.insertAdjacentHTML('beforeend', '<div id="e2e-sel" data-selectable><input type="checkbox" id="e2e-all" data-select-all aria-label="Select all" />' +
          '<div class="mat-tile" id="e2e-tile"><input type="checkbox" id="e2e-t1" data-select-row aria-label="Select the tile" /></div>' +
          '<ul><li data-select-item id="e2e-item"><input type="checkbox" id="e2e-t2" data-select-row aria-label="Select the item" /></li>' +
          '<li id="e2e-plain"><input type="checkbox" id="e2e-t3" data-select-row aria-label="Select the plain one" /></li></ul></div>');
      });
      const marked = () => page.evaluate(() => ['e2e-tile', 'e2e-item', 'e2e-plain'].map(id => document.getElementById(id).classList.contains('is-selected')));
      await page.check('#e2e-t1');
      expect(await marked(), [true, false, false], 'after the check of the tile');
      await page.check('#e2e-t2');
      expect(await marked(), [true, true, false], 'after the check of the item');
      await page.check('#e2e-t3');
      expect(await marked(), [true, true, false], 'after the check of an element that is not an item');
      await page.uncheck('#e2e-all');
      expect(await marked(), [false, false, false], 'after the check for all was taken away');
      await page.check('#e2e-all');
      expect(await marked(), [true, true, false], 'after the check for all');
      await page.evaluate(() => document.getElementById('e2e-sel').remove());
    });
    await check('the width of a progress bar comes from data-value, and a stack shows its parts side by side', async () => {
      const share = await page.$eval('.mat-meter--lg[aria-label="Large"] .mat-meter__fill', el => Math.round(100 * el.getBoundingClientRect().width / el.parentElement.getBoundingClientRect().width));
      expect(share, 75, 'width of the large bar in per cent');
    });
    await check('a side dialog sits at the right edge and closes with its X', async () => {
      await page.evaluate(() => document.getElementById('dialogs').scrollIntoView());
      // the animation is noted when it starts: on a page of two megabytes it can be over before a later look would find it
      await page.evaluate(() => { window.__side = []; document.getElementById('dlg-side').addEventListener('animationstart', e => window.__side.push(e.animationName)); });
      await page.click('[data-dialog-open="dlg-side"]');
      await page.waitForFunction(() => window.__side.length > 0);
      expect(await page.evaluate(() => window.__side), ['mat-panel-in-end'], 'the animation of the panel (not the one of the centred dialog)');
      await page.waitForFunction(() => document.getElementById('dlg-side').getAnimations().length === 0);
      const state = await page.$eval('#dlg-side', el => { const r = el.getBoundingClientRect(); return { open: el.open, atRight: Math.abs(r.right - innerWidth) <= 1, height: Math.round(r.height) === innerHeight, narrow: r.width < innerWidth }; });
      expect(state, { open: true, atRight: true, height: true, narrow: true }, 'side dialog');
      await page.click('#dlg-side .mat-dialog__close');
      expect(await page.$eval('#dlg-side', el => el.open), false, 'open after the X');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#dialogs');
    await check('on a phone the side dialog is full screen, like every dialog', async () => {
      await page.click('[data-dialog-open="dlg-side"]');
      const state = await page.$eval('#dlg-side', el => { const r = el.getBoundingClientRect(); return { width: Math.round(r.width) === innerWidth, height: Math.round(r.height) === innerHeight }; });
      expect(state, { width: true, height: true }, 'side dialog on a phone');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- number stepper (FORM-20)
  console.log('\nNumber stepper');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#number-stepper');
    const val = id => page.$eval(`#${id}`, el => el.value);
    const dec = id => page.locator(`.mat-stepper:has(#${id}) > .mat-stepper__btn--dec`);
    const inc = id => page.locator(`.mat-stepper:has(#${id}) > .mat-stepper__btn--inc`);
    const disabledState = async (id, kind) => page.getAttribute(`.mat-stepper:has(#${id}) > .mat-stepper__btn--${kind}`, 'aria-disabled');
    // what a person does: select the text, type, leave the field with the Tab key (the browser's change event follows)
    const typeIn = async (id, text) => {
      await page.focus('#' + id);
      await page.keyboard.press('Control+A');
      await page.keyboard.type(text);
      await page.keyboard.press('Tab');
    };
    const record = id => page.evaluate(i => {
      const input = document.getElementById(i);
      window.__stepper = { events: [], times: [], seen: [] };
      input.addEventListener('input', () => window.__stepper.events.push('input'));
      input.addEventListener('change', () => { window.__stepper.events.push('change'); window.__stepper.times.push(performance.now()); window.__stepper.seen.push(input.value); });
    }, id);
    const recorded = () => page.evaluate(() => window.__stepper);

    await page.evaluate(() => document.getElementById('number-stepper').scrollIntoView());
    await check('a stepper has a button for less and one for more, named for a screen reader and out of the Tab order, and the native spinners are gone', async () => {
      expect(await page.locator('.mat-stepper.is-ready').count(), 12, 'steppers that were wired');
      expect(await page.locator('.mat-stepper__btn').count(), 24, 'buttons');
      expect(await dec('stp-qty').getAttribute('aria-label'), 'Decrease Quantity', 'name of the button for less');
      expect(await inc('stp-qty').getAttribute('aria-label'), 'Increase Quantity', 'name of the button for more');
      expect(await inc('stp-qty').getAttribute('tabindex'), '-1', 'tabindex');
      expect(await inc('stp-qty').getAttribute('type'), 'button', 'type');
      expect(await page.$eval('#stp-qty', el => getComputedStyle(el).appearance), 'textfield', 'appearance of the field');
      const order = await page.$eval('#stp-qty', el => [...el.parentNode.children].map(c => c.className.replace(/ .*/, '')));
      expect(order.slice(0, 3), ['mat-stepper__btn', 'mat-input', 'mat-stepper__btn'], 'less, the field, more');
    });
    await check('a click changes the value by the step with an input and a change event, and a button at its limit is aria-disabled and keeps the focus', async () => {
      await record('stp-qty');
      await inc('stp-qty').click();
      expect(await val('stp-qty'), '4', 'after more');
      expect((await recorded()).events, ['input', 'change'], 'events of one press');
      await dec('stp-qty').click();
      await dec('stp-qty').click();
      expect(await val('stp-qty'), '2', 'after two times less');
      await dec('stp-qty').click();
      expect(await val('stp-qty'), '1', 'at the lowest value');
      expect(await disabledState('stp-qty', 'dec'), 'true', 'the button for less at the limit');
      expect(await disabledState('stp-qty', 'inc'), 'false', 'the button for more');
      const events = (await recorded()).events.length;
      // (Playwright counts aria-disabled as disabled and waits for it to end: the press is forced)
      await dec('stp-qty').click({ force: true });
      expect(await val('stp-qty'), '1', 'a press at the limit');
      expect((await recorded()).events.length, events, 'events of a press at the limit');
      // a button that has the focus (a screen reader activates it) keeps it at the limit
      await inc('stp-qty').focus();
      for (let i = 0; i < 12; i += 1) { await page.keyboard.press('Enter'); }
      expect(await val('stp-qty'), '10', 'after Enter on the button for more');
      expect(await disabledState('stp-qty', 'inc'), 'true', 'the button for more at the highest value');
      expect(await inc('stp-qty').evaluate(el => el === document.activeElement && !el.disabled), true, 'the focus and the disabled attribute');
    });
    await check('the arrow keys and Page up and down work in the field, with Shift and Page by ten steps, inside the limits', async () => {
      await page.focus('#stp-gb');
      expect(await val('stp-gb'), '20', 'storage at the start');
      await page.keyboard.press('ArrowUp');
      expect(await val('stp-gb'), '25', 'ArrowUp');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      expect(await val('stp-gb'), '15', 'ArrowDown twice');
      await page.keyboard.press('PageUp');
      expect(await val('stp-gb'), '65', 'PageUp is ten steps');
      await page.keyboard.press('Shift+ArrowUp');
      expect(await val('stp-gb'), '100', 'Shift and ArrowUp stop at the limit');
      expect(await disabledState('stp-gb', 'inc'), 'true', 'the button for more at 100');
      await page.keyboard.press('PageDown');
      expect(await val('stp-gb'), '50', 'PageDown');
      await page.keyboard.press('PageDown');
      await page.keyboard.press('PageDown');
      await page.keyboard.press('PageDown');
      await page.keyboard.press('PageDown');
      await page.keyboard.press('PageDown');
      expect(await val('stp-gb'), '0', 'PageDown stops at the lowest value');
      expect(await disabledState('stp-gb', 'dec'), 'true', 'the button for less at 0');
    });
    await check('Shift with a click is ten steps', async () => {
      await typeIn('stp-gb', '20');
      await inc('stp-gb').click({ modifiers: ['Shift'] });
      expect(await val('stp-gb'), '70', 'twenty plus ten steps of five');
      await dec('stp-gb').click({ modifiers: ['Shift'] });
      expect(await val('stp-gb'), '20', 'and back');
    });
    await check('holding a button repeats after a short wait, faster and faster, and stops when it is released', async () => {
      await typeIn('stp-gb', '0');
      await record('stp-gb');
      await inc('stp-gb').scrollIntoViewIfNeeded();
      const box = await inc('stp-gb').boundingBox();
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await page.mouse.down();
      await page.waitForTimeout(1600);
      await page.mouse.up();
      const held = await recorded();
      const gaps = held.times.slice(1).map((t, i) => t - held.times[i]);
      expectTrue(gaps.length >= 6, 'only ' + gaps.length + ' steps were made while the button was held');
      expectTrue(gaps[0] >= 380, 'the first repeat came after ' + Math.round(gaps[0]) + ' ms: no short wait');
      expectTrue(gaps[gaps.length - 1] < gaps[1] * 0.85, 'the steps do not get faster: ' + gaps.map(g => Math.round(g)).join(' '));
      const after = await val('stp-gb');
      await page.waitForTimeout(500);
      expect(await val('stp-gb'), after, 'the value after the button was released');
      expect(Number(after) % 5, 0, 'a value on the grid');
    });
    await check('what is typed is put inside the limits and on the grid before a listener hears of it', async () => {
      await page.evaluate(() => { const i = document.getElementById('stp-qty'); window.__seen = []; i.addEventListener('change', () => window.__seen.push(i.value)); });
      await typeIn('stp-qty', '99');
      expect(await val('stp-qty'), '10', 'a value above the limit');
      expect(await page.evaluate(() => window.__seen), ['10'], 'what a listener of change saw');
      await typeIn('stp-qty', '0');
      expect(await val('stp-qty'), '1', 'a value below the limit');
      await typeIn('stp-gb', '23');
      expect(await val('stp-gb'), '25', 'a value between two steps of five');
      await typeIn('stp-gb', '22');
      expect(await val('stp-gb'), '20', 'and the other way');
    });
    await check('decimals are exact: 0.1 plus 0.1 plus 0.1 is 0.3, data-precision keeps the decimals that are shown', async () => {
      await page.$eval('#stp-opacity', el => { el.value = '0.1'; });
      await inc('stp-opacity').click();
      await inc('stp-opacity').click();
      expect(await val('stp-opacity'), '0.3', 'three tenths');
      await inc('stp-opacity').click();
      expect(await val('stp-opacity'), '0.4', 'four tenths');
      for (let i = 0; i < 6; i += 1) { await inc('stp-opacity').click(); }
      expect(await val('stp-opacity'), '1', 'the highest value, without a trailing zero');
      expect(await disabledState('stp-opacity', 'inc'), 'true', 'the button for more at 1');
      await page.$eval('#stp-opacity', el => { el.value = '0.25'; el.dispatchEvent(new Event('change', { bubbles: true })); });
      expect(await val('stp-opacity'), '0.3', 'a typed 0.25 goes to the nearest tenth');
      expect(await val('stp-price'), '4.50', 'price at the start');
      await inc('stp-price').click();
      expect(await val('stp-price'), '4.75', 'a quarter more, two decimals');
      await dec('stp-price').click();
      expect(await val('stp-price'), '4.50', 'and back, with its trailing zero');
      await page.$eval('#stp-price', el => { el.value = '0.1'; });
      await dec('stp-price').click();
      expect(await val('stp-price'), '0.00', 'down to the lowest value');
      expect(await disabledState('stp-price', 'dec'), 'true', 'the button for less at 0');
      expect(await disabledState('stp-price', 'inc'), 'false', 'a field without an upper limit has no limit for more');
    });
    await check('the unit sits inside the field before the button for more and is read with the field', async () => {
      const state = await page.evaluate(() => {
        const input = document.getElementById('stp-gb');
        const unit = document.getElementById('stp-gb-unit');
        const ir = input.getBoundingClientRect(), ur = unit.getBoundingClientRect(), br = input.parentNode.querySelector('.mat-stepper__btn--inc').getBoundingClientRect();
        return { text: unit.textContent, inside: ur.left > ir.left + ir.width / 2 && ur.right <= ir.right, beforeButton: ur.right <= br.left, describedby: input.getAttribute('aria-describedby') };
      });
      expect(state, { text: 'GB', inside: true, beforeButton: true, describedby: 'stp-gb-unit' }, 'the unit');
    });
    await check('stacked arrows sit at the right of the field on a mouse and step like the buttons', async () => {
      const state = await page.evaluate(() => {
        const input = document.getElementById('stp-pages'), wrap = input.parentNode;
        const up = wrap.querySelector('.mat-stepper__btn--inc').getBoundingClientRect(), down = wrap.querySelector('.mat-stepper__btn--dec').getBoundingClientRect(), box = wrap.getBoundingClientRect();
        return { stacked: up.bottom <= down.top + 1 && up.top < down.top, right: Math.abs(box.right - up.right) <= 2, narrow: up.width < 40, height: Math.round(box.height) };
      });
      expect(state, { stacked: true, right: true, narrow: true, height: 40 }, 'stacked arrows');
      // the field that has the focus does not cover them
      await page.focus('#stp-pages');
      const covered = await page.evaluate(() => ['inc', 'dec'].filter(k => {
        const b = document.querySelector('#stp-pages').parentNode.querySelector('.mat-stepper__btn--' + k), r = b.getBoundingClientRect();
        return !b.contains(document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2));
      }));
      expect(covered, [], 'arrows covered by the focused field');
      await inc('stp-pages').click();
      expect(await val('stp-pages'), '3', 'after the arrow up');
      await dec('stp-pages').click();
      await dec('stp-pages').click();
      expect(await val('stp-pages'), '1', 'after two arrows down');
      expect(await disabledState('stp-pages', 'dec'), 'true', 'the arrow down at 1');
    });
    await check('the sizes follow the input: 32, 40 and 48 px high, the buttons as high as the field', async () => {
      const heights = await page.evaluate(() => ['stp-st-sm', 'stp-qty', 'stp-st-lg'].map(id => {
        const input = document.getElementById(id);
        return [Math.round(input.getBoundingClientRect().height), Math.round(input.parentNode.querySelector('.mat-stepper__btn--inc').getBoundingClientRect().height)];
      }));
      expect(heights, [[32, 32], [40, 40], [48, 48]], 'heights of the field and the button');
    });
    await check('a read-only stepper does not change and a disabled one has disabled buttons and is not sent', async () => {
      await inc('stp-ro').click({ force: true });
      await page.focus('#stp-ro');
      await page.keyboard.press('ArrowUp');
      expect(await val('stp-ro'), '4', 'read-only after a click and a key');
      expect(await disabledState('stp-ro', 'inc'), 'true', 'the button of a read-only field');
      expect(await page.$eval('.mat-stepper:has(#stp-off) > .mat-stepper__btn--inc', el => el.disabled), true, 'disabled button');
      await inc('stp-off').click({ force: true });
      expect(await val('stp-off'), '4', 'disabled after a click');
      const colours = await page.evaluate(() => ['stp-off', 'stp-qty'].map(id => getComputedStyle(document.getElementById(id).parentNode.querySelector('.mat-stepper__btn--inc')).backgroundColor));
      expectTrue(colours[0] !== colours[1], 'a disabled button looks like any other button');
    });
    await check('in a form: an empty required field gets its message and the border of the buttons follows, a press starts at the lowest value and takes the message away', async () => {
      await page.evaluate(() => document.getElementById('stp-seats').scrollIntoView({ block: 'center' }));
      await page.click('form[action="/Components#number-stepper"] button[type=submit]');
      await page.waitForTimeout(200);
      expect(await page.locator('#stp-seats').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field is marked');
      expect((await page.locator('#stp-seats').evaluate(el => el.closest('.mat-field').querySelector('.mat-error').textContent)).trim(), 'Please enter the number of seats.', 'the message');
      expect(await page.evaluate(() => document.activeElement.id), 'stp-seats', 'the focus');
      const colours = await page.evaluate(() => {
        const input = document.getElementById('stp-seats');
        return [getComputedStyle(input).borderTopColor, getComputedStyle(input.parentNode.querySelector('.mat-stepper__btn--inc')).borderTopColor];
      });
      expect(colours[0], colours[1], 'the border of the field and of the button');
      await inc('stp-seats').click();
      expect(await val('stp-seats'), '1', 'the first press of an empty field');
      expect(await page.locator('#stp-seats').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), false, 'the message after a valid value');
    });
    await check('a reset of the form puts the value back and the buttons follow', async () => {
      await page.$eval('#stp-nights', el => { el.value = '30'; el.dispatchEvent(new Event('input', { bubbles: true })); });
      expect(await disabledState('stp-nights', 'inc'), 'true', 'the button for more at 30');
      await page.$eval('#stp-nights', el => el.form.reset());
      await page.waitForTimeout(50);
      expect(await val('stp-nights'), '2', 'the value after the reset');
      expect(await disabledState('stp-nights', 'inc'), 'false', 'the button for more after the reset');
    });
    await check('the grid is the browser\'s: counted from min (else the value attribute), the largest value of the grid below max, a value outside the limits can only come back, step="any" adds one', async () => {
      // [attributes, presses (+ more, - less, P ten steps more, M ten steps less), the value after them, the buttons that cannot go further after them]
      const cases = [
        ['min="-5" max="5" step="1" value="0"', '-------', '-5', 'dec'],
        ['min="0" max="10" step="3" value="9"', '+', '9', 'inc'],
        ['min="0" max="10" step="3" value="10"', '+', '10', 'inc'],
        ['min="0" max="10" step="3" value="10"', '-', '9', 'inc'],
        ['min="0" max="1" step="0.1" value="0.7"', '+++', '1', 'inc'],
        ['min="0" max="1" step="0.01" value="0.07"', '+++', '0.1', ''],
        ['step="any" value="1.5"', '+', '2.5', ''],
        ['step="5" value="2"', '++', '12', ''],
        ['min="-1" max="1" step="0.5" value="-0.5"', '++', '0.5', ''],
        ['min="5" max="9" step="1"', '+', '5', 'dec'],
        ['min="5" max="9" step="1"', '-', '5', 'dec'],
        ['min="0" max="100" step="1" value="500"', '+', '500', 'inc'],
        ['min="0" max="100" step="1" value="500"', '-', '100', 'inc'],
        ['min="0" max="100" step="5" value="0"', 'P', '50', ''],
        ['min="0" max="100" step="5" value="35"', 'P-', '80', ''],
        ['min="0" step="0.25" value="0" data-precision="2"', '+', '0.25', ''],
      ];
      const found = await page.evaluate(cases => {
        const host = document.createElement('div');
        host.id = 'stp-grid';
        document.getElementById('number-stepper').appendChild(host);
        host.innerHTML = cases.map((c, n) => {
          const [attrs] = c;
          const data = /data-precision/.test(attrs) ? ' data-precision="2"' : '';
          return `<div class="mat-stepper" data-stepper${data}><input class="mat-input" type="number" id="stp-grid-${n}" name="g${n}" ${attrs.replace(/ ?data-precision="2"/, '')} /></div>`;
        }).join('');
        window.MatUi.refresh();
        return cases.map((c, n) => {
          const input = document.getElementById('stp-grid-' + n), wrap = input.parentNode;
          const inc = wrap.querySelector('.mat-stepper__btn--inc'), dec = wrap.querySelector('.mat-stepper__btn--dec');
          // a click without a press (detail 0) is what the keyboard and an assistive technology make
          const press = (b, shift) => b.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 0, shiftKey: shift }));
          c[1].split('').forEach(p => press(p === '+' || p === 'P' ? inc : dec, p === 'P' || p === 'M'));
          const limits = ['dec', 'inc'].filter(k => wrap.querySelector('.mat-stepper__btn--' + k).getAttribute('aria-disabled') === 'true').join(',');
          return [input.value, limits];
        });
      }, cases);
      cases.forEach((c, n) => {
        expect(found[n][0], c[2], `${c[0]}, presses ${c[1]}: the value`);
        expect(found[n][1], c[3], `${c[0]}, presses ${c[1]}: the buttons that cannot go further`);
      });
      await page.evaluate(() => document.getElementById('stp-grid').remove());
    });
    await check('MatUi.refresh makes the buttons of a stepper that came later, with the names in the language of the page', async () => {
      await page.evaluate(() => {
        document.documentElement.lang = 'de';
        const host = document.createElement('div');
        host.className = 'mat-field';
        host.id = 'stp-late';
        host.innerHTML = '<label class="mat-label" for="stp-late-n">Menge</label><div class="mat-stepper" data-stepper><input class="mat-input" type="number" id="stp-late-n" name="late" min="0" max="3" step="1" value="3" /></div>';
        document.getElementById('number-stepper').appendChild(host);
        window.MatUi.refresh();
      });
      expect(await page.getAttribute('#stp-late .mat-stepper__btn--dec', 'aria-label'), 'Menge verringern', 'German name of the button for less');
      expect(await page.getAttribute('#stp-late .mat-stepper__btn--inc', 'aria-label'), 'Menge erhöhen', 'German name of the button for more');
      expect(await page.getAttribute('#stp-late .mat-stepper__btn--inc', 'aria-disabled'), 'true', 'at the highest value');
      await page.evaluate(() => { document.documentElement.lang = 'en'; document.getElementById('stp-late').remove(); });
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#number-stepper');
    await check('on a phone every stepper has buttons of the size of a control (also the stacked one), a tap steps once and nothing is wider than the screen', async () => {
      await page.evaluate(() => document.getElementById('number-stepper').scrollIntoView());
      const state = await page.evaluate(() => {
        const out = { coarse: matchMedia('(pointer: coarse)').matches, overflow: document.documentElement.scrollWidth > innerWidth, small: [] };
        document.querySelectorAll('#number-stepper .mat-stepper__btn').forEach(b => {
          const r = b.getBoundingClientRect();
          const sm = b.closest('.mat-stepper').querySelector('.mat-input--sm');
          if (r.width < (sm ? 36 : 44) - 1 || r.height < (sm ? 36 : 44) - 1) { out.small.push(Math.round(r.width) + 'x' + Math.round(r.height)); }
        });
        out.stackedPosition = getComputedStyle(document.querySelector('#stp-pages ~ .mat-stepper__btn--inc')).position;
        return out;
      });
      expect(state, { coarse: true, overflow: false, small: [], stackedPosition: 'relative' }, 'steppers on a phone');
      await page.tap('.mat-stepper:has(#stp-qty) > .mat-stepper__btn--inc');
      expect(await page.$eval('#stp-qty', el => el.value), '4', 'after one tap');
      await page.tap('.mat-stepper:has(#stp-pages) > .mat-stepper__btn--inc');
      expect(await page.$eval('#stp-pages', el => el.value), '3', 'after one tap on the stacked one');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- dropdown with checkboxes (FORM-15)
  console.log('\nDropdown with checkboxes');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#checkbox-select');
    const panel = id => `#${id}-trigger ~ .mat-picker__panel`;
    const val = id => page.$eval(`#${id}`, el => el.value);
    const shown = async id => (await page.textContent(`#${id}-trigger .mat-picker__value`)).replace(/\s+/g, ' ').trim();
    const all = id => page.locator(`${panel(id)} .mat-picker__all`);
    const allState = id => page.getAttribute(`${panel(id)} .mat-picker__all`, 'aria-checked');
    const allLabel = async id => (await all(id).textContent()).trim();
    const active = () => page.evaluate(() => document.activeElement.classList.contains('mat-picker__all') ? 'all' : document.activeElement.getAttribute('data-value'));
    const countChanges = id => page.evaluate(i => { window.__changes = 0; document.getElementById(i).addEventListener('change', () => { window.__changes += 1; }); }, id);

    await page.evaluate(() => document.getElementById('checkbox-select').scrollIntoView());
    await check('Select all is the first row, a checkbox in three states: the field starts with two days ticked, so the box is in between', async () => {
      expect(await shown('cbs-days'), 'Monday, Wednesday', 'the field names one or two entries');
      await page.click('#cbs-days-trigger');
      expectTrue(await page.locator(panel('cbs-days')).isVisible(), 'the panel is not shown');
      expect(await all('cbs-days').getAttribute('role'), 'checkbox', 'role of the row');
      expect(await allState('cbs-days'), 'mixed', 'state with some entries ticked');
      expect(await allLabel('cbs-days'), 'Select all', 'its text');
      expect(await page.evaluate(sel => { const a = document.querySelector(sel + ' .mat-picker__all'); return a.nextElementSibling.classList.contains('mat-picker__options') && !a.closest('.mat-picker__options'); }, panel('cbs-days')), true, 'the row stands above the list');
      expect(await page.locator(`${panel('cbs-days')} .mat-picker__option .mat-picker__desc`).count(), 0, 'plain entries have no description');
    });
    await check('a click on Select all ticks every entry with one change event, says All selected, and takes them all away when all are ticked', async () => {
      await countChanges('cbs-days');
      await all('cbs-days').click();
      expect(await val('cbs-days'), 'mon, tue, wed, thu, fri, sat, sun', 'the value in the order of the entries');
      expect(await allState('cbs-days'), 'true', 'the box with everything ticked');
      expect(await shown('cbs-days'), 'All selected', 'the field');
      expect(await page.evaluate(() => window.__changes), 1, 'change events of one click');
      expect(await page.locator(`${panel('cbs-days')} .mat-picker__option[aria-selected="true"]`).count(), 7, 'entries ticked');
      expect(await active(), 'all', 'the focus stays on the row');
      await all('cbs-days').click();
      expect(await val('cbs-days'), '', 'the value after the second click');
      expect(await allState('cbs-days'), 'false', 'the box with nothing ticked');
      expect(await shown('cbs-days'), 'Choose days', 'the field shows its placeholder');
    });
    await check('ticking entries updates the box and the field: names for one or two, the count from three, All selected for every one', async () => {
      await page.click(`${panel('cbs-days')} [data-value="mon"]`);
      expect(await shown('cbs-days'), 'Monday', 'one');
      expect(await allState('cbs-days'), 'mixed', 'the box with one of seven');
      await page.click(`${panel('cbs-days')} [data-value="tue"]`);
      expect(await shown('cbs-days'), 'Monday, Tuesday', 'two');
      await page.click(`${panel('cbs-days')} [data-value="wed"]`);
      expect(await shown('cbs-days'), '3 selected', 'three');
      await all('cbs-days').click();
      expect(await val('cbs-days'), 'mon, tue, wed, thu, fri, sat, sun', 'a click on a box in between ticks the rest');
      await page.click(`${panel('cbs-days')} [data-value="sun"]`);
      expect(await shown('cbs-days'), '6 selected', 'six of seven');
      await page.keyboard.press('Escape');
    });
    await check('keys: the arrow keys, Home and End walk Select all and the entries, Space ticks, Escape gives the focus back to the field', async () => {
      await page.$eval('#cbs-days', el => { el.value = 'mon, wed'; });
      await page.evaluate(() => window.MatUi.refresh());
      await page.focus('#cbs-days-trigger');
      await page.keyboard.press('ArrowDown');
      expect(await active(), 'mon', 'the focus on opening is on the first ticked entry');
      await page.keyboard.press('Home');
      expect(await active(), 'all', 'Home goes to Select all');
      await page.keyboard.press('ArrowDown');
      expect(await active(), 'mon', 'ArrowDown');
      await page.keyboard.press('ArrowDown');
      expect(await active(), 'tue', 'ArrowDown again');
      await page.keyboard.press('Space');
      expect(await val('cbs-days'), 'mon, tue, wed', 'Space ticks the entry');
      expect(await active(), 'tue', 'the focus stays on the entry');
      await page.keyboard.press('Space');
      expect(await val('cbs-days'), 'mon, wed', 'Space unticks it');
      await page.keyboard.press('End');
      expect(await active(), 'sun', 'End goes to the last entry');
      await page.keyboard.press('ArrowDown');
      expect(await active(), 'sun', 'ArrowDown at the end');
      await page.keyboard.press('ArrowUp');
      expect(await active(), 'sat', 'ArrowUp');
      await page.keyboard.press('Home');
      await page.keyboard.press('Space');
      expect(await val('cbs-days'), 'mon, tue, wed, thu, fri, sat, sun', 'Space on Select all with some ticked');
      await page.keyboard.press('Space');
      expect(await val('cbs-days'), '', 'Space on Select all with all ticked');
      await page.keyboard.press('Escape');
      expectTrue(!(await page.locator(panel('cbs-days')).isVisible()), 'Escape did not close the panel');
      expect(await page.evaluate(() => document.activeElement.id), 'cbs-days-trigger', 'the focus after Escape');
    });
    await check('with a search Select all means what is shown, its box looks at what is shown, and the others stay as they are', async () => {
      await page.evaluate(() => document.getElementById('cbs-countries').scrollIntoView({ block: 'center' }));
      await page.click('#cbs-countries-trigger');
      const search = page.locator(`${panel('cbs-countries')} input[type=search]`);
      await page.click(`${panel('cbs-countries')} [data-value="at"]`);
      expect(await val('cbs-countries'), 'at', 'Austria ticked');
      await search.fill('an');
      expect(await page.locator(`${panel('cbs-countries')} .mat-picker__option:not([hidden])`).count(), 5, 'entries shown for an');
      expect(await allLabel('cbs-countries'), 'Select all found', 'its text while a search narrows the list');
      expect(await allState('cbs-countries'), 'false', 'none of the shown entries is ticked');
      await all('cbs-countries').click();
      expect(await val('cbs-countries'), 'at, fr, de, nl, pl, ch', 'the shown entries are ticked, Austria stays');
      expect(await allState('cbs-countries'), 'true', 'all that are shown are ticked');
      await search.fill('');
      expect(await allLabel('cbs-countries'), 'Select all', 'its text without a search');
      expect(await allState('cbs-countries'), 'mixed', 'six of eleven');
      expect(await shown('cbs-countries'), '6 selected', 'the field');
      await search.fill('an');
      await all('cbs-countries').click();
      expect(await val('cbs-countries'), 'at', 'a second click takes away only what is shown');
      expect(await shown('cbs-countries'), 'Austria', 'the field');
      await search.fill('zzz');
      expectTrue(!(await all('cbs-countries').isVisible()), 'Select all is shown with nothing to select');
      expectTrue(await page.locator(`${panel('cbs-countries')} .mat-picker__empty`).isVisible(), 'no message that nothing was found');
      await search.fill('');
      await all('cbs-countries').click();
      expect(await shown('cbs-countries'), 'All selected', 'everything without a search');
      expect(await val('cbs-countries'), 'at, be, dk, fr, de, it, nl, pl, es, se, ch', 'all eleven');
      await page.click(`${panel('cbs-countries')} [data-picker-action]`);
      await page.click('#cbs-countries-trigger ~ .mat-picker__clear');
      expect(await val('cbs-countries'), '', 'the value after Clear');
    });
    await check('the arrow keys and Select all skip an entry that cannot be chosen, which keeps its state', async () => {
      await page.evaluate(() => document.getElementById('cbs-channels').scrollIntoView({ block: 'center' }));
      await page.click('#cbs-channels-trigger');
      expect(await allState('cbs-channels'), 'mixed', 'one of three that can be chosen');
      await all('cbs-channels').click();
      expect(await val('cbs-channels'), 'mail, push, chat', 'the entry that cannot be chosen is not ticked');
      expect(await allState('cbs-channels'), 'true', 'all that can be chosen');
      expect(await shown('cbs-channels'), '3 selected', 'the field');
      await page.keyboard.press('Home');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowDown');
      expect(await active(), 'chat', 'the arrow keys go from push to chat');
      await page.keyboard.press('ArrowDown');
      expect(await active(), 'chat', 'and do not enter the entry that cannot be chosen');
      expect(await page.locator(`${panel('cbs-channels')} [data-value="sms"]`).getAttribute('aria-disabled'), 'true', 'aria-disabled of the entry');
      await page.keyboard.press('Escape');
    });
    await check('in a form a dropdown with nothing ticked gets its message at the field and Select all clears it', async () => {
      await page.evaluate(() => document.getElementById('cbs-req').scrollIntoView({ block: 'center' }));
      await page.click('form[action="/Components#checkbox-select"] button[type=submit]');
      await page.waitForTimeout(200);
      expect(await page.locator('#cbs-req').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field is marked');
      expect((await page.locator('#cbs-req').evaluate(el => el.closest('.mat-field').querySelector('.mat-error').textContent)).trim(), 'Please choose at least one day.', 'the message');
      expect(await page.evaluate(() => document.activeElement.id), 'cbs-req-trigger', 'the focus');
      await page.click('#cbs-req-trigger');
      await all('cbs-req').click();
      expect(await page.locator('#cbs-req').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), false, 'the message after the choice');
      await page.keyboard.press('Escape');
    });
    await check('the texts of Select all and of the field follow the language of the page', async () => {
      await page.evaluate(() => { document.documentElement.lang = 'de'; });
      await page.evaluate(() => document.getElementById('cbs-days').scrollIntoView({ block: 'center' }));
      await page.click('#cbs-days-trigger');
      expect(await allLabel('cbs-days'), 'Alle auswählen', 'the row in German');
      await all('cbs-days').click();
      expect(await shown('cbs-days'), 'Alle gewählt', 'the field in German');
      await page.keyboard.press('Escape');
      await page.evaluate(() => { document.documentElement.lang = 'en'; });
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#checkbox-select');
    await check('on a phone the dropdown with checkboxes fills the screen, Select all is as tall as a control and a tap ticks everything', async () => {
      await page.evaluate(() => document.getElementById('cbs-countries').scrollIntoView({ block: 'center' }));
      await page.tap('#cbs-countries-trigger');
      const state = await page.evaluate(() => {
        const panel = document.querySelector('#cbs-countries-trigger ~ .mat-picker__panel');
        const r = panel.getBoundingClientRect(), a = panel.querySelector('.mat-picker__all').getBoundingClientRect();
        return { position: getComputedStyle(panel).position, full: Math.round(r.width) === innerWidth && Math.round(r.height) === innerHeight, tall: a.height >= 44, overflow: document.documentElement.scrollWidth > innerWidth };
      });
      expect(state, { position: 'fixed', full: true, tall: true, overflow: false }, 'the panel on a phone');
      await page.tap('#cbs-countries-trigger ~ .mat-picker__panel .mat-picker__all');
      expect((await page.textContent('#cbs-countries-trigger .mat-picker__value')).trim(), 'All selected', 'the field after a tap on Select all');
      expect(await page.getAttribute('#cbs-countries-trigger ~ .mat-picker__panel .mat-picker__all', 'aria-checked'), 'true', 'the box');
      await page.tap('#cbs-countries-trigger ~ .mat-picker__panel [data-picker-action]');
      expectTrue(!(await page.locator('#cbs-countries-trigger ~ .mat-picker__panel').isVisible()), 'Done did not close the panel');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- pickers
  console.log('\nPickers');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#rich-select');
    const panelOf = id => page.locator(`#${id}-trigger ~ .mat-picker__panel`);
    const valueOf = id => page.$eval(`#${id}`, el => el.value);
    const shown = async id => (await page.textContent(`#${id}-trigger .mat-picker__value`)).replace(/\s+/g, ' ').trim();

    await check('a picker panel is drawn at its place from the first frame on and only fades in (it does not move or grow into place)', async () => {
      for (const id of ['pk-plan', 'pk-date', 'pk-period', 'pk-time', 'pk-colour']) {
        await page.evaluate(sel => document.querySelector(sel).scrollIntoView({ block: 'center' }), `#${id}-trigger`);
        await page.waitForTimeout(300);
        await page.evaluate(sel => {
          window.__frames = [];
          const t0 = performance.now();
          (function tick() {
            const panel = document.querySelector(sel + ' ~ .mat-picker__panel');
            if (panel && !panel.hidden) { const r = panel.getBoundingClientRect(); window.__frames.push({ x: r.x, y: r.y, w: r.width, h: r.height }); }
            if (performance.now() - t0 < 700) { requestAnimationFrame(tick); }
          })();
        }, `#${id}-trigger`);
        await page.click(`#${id}-trigger`);
        await page.waitForTimeout(800);
        const frames = await page.evaluate(() => window.__frames);
        expectTrue(frames.length > 3, id + ': no frames of the panel');
        const last = frames[frames.length - 1];
        const off = Math.max(...frames.map(f => Math.abs(f.x - last.x) + Math.abs(f.y - last.y) + Math.abs(f.w - last.w) + Math.abs(f.h - last.h)));
        expectTrue(off < 1.5, `${id}: the panel was ${off.toFixed(1)} px away from its place on an earlier frame`);
        await page.keyboard.press('Escape');
      }
    });

    await check('a select with details shows the chosen option in the field, opens a panel with descriptions and tags, and a click chooses and closes', async () => {
      expectTrue((await shown('pk-plan')).startsWith('Team'), 'the field does not show the chosen plan: ' + await shown('pk-plan'));
      expect(await page.getAttribute('#pk-plan-trigger', 'aria-haspopup'), 'listbox', 'aria-haspopup');
      await page.click('#pk-plan-trigger');
      expectTrue(await panelOf('pk-plan').isVisible(), 'the panel is not shown');
      expect(await page.getAttribute('#pk-plan-trigger', 'aria-expanded'), 'true', 'aria-expanded');
      expect(await page.locator('#pk-plan-trigger ~ .mat-picker__panel .mat-picker__desc').count(), 3, 'descriptions');
      expect(await page.locator('#pk-plan-trigger ~ .mat-picker__panel .mat-picker__option[aria-selected="true"]').count(), 1, 'chosen options');
      await page.click('#pk-plan-trigger ~ .mat-picker__panel [data-value="business"]');
      expect(await valueOf('pk-plan'), 'business', 'the posted value');
      expect(await shown('pk-plan'), 'Business', 'the field after the choice');
      expectTrue(!(await panelOf('pk-plan').isVisible()), 'the panel is still shown');
      expect(await page.evaluate(() => document.activeElement.id), 'pk-plan-trigger', 'the focus after the choice');
    });
    await check('a select works with the keyboard: arrows open and move, Enter chooses, Escape closes and gives the focus back', async () => {
      await page.focus('#pk-plan-trigger');
      await page.keyboard.press('ArrowDown');
      expectTrue(await panelOf('pk-plan').isVisible(), 'ArrowDown did not open the panel');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-value')), 'business', 'the focus is on the chosen option');
      await page.keyboard.press('ArrowUp');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-value')), 'team', 'ArrowUp');
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-plan'), 'team', 'Enter chose the option');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('Escape');
      expectTrue(!(await panelOf('pk-plan').isVisible()), 'Escape did not close the panel');
      expect(await page.evaluate(() => document.activeElement.id), 'pk-plan-trigger', 'the focus after Escape');
    });
    await check('several choices stay open, are joined by the separator, are searched, counted and cleared', async () => {
      await page.evaluate(() => document.getElementById('rich-select').scrollIntoView());
      expect(await shown('pk-apps'), 'Mail, Files', 'the field at the start');
      await page.click('#pk-apps-trigger');
      expectTrue(await page.evaluate(() => document.activeElement.type === 'search'), 'the search field has no focus on a wide screen');
      await page.click('#pk-apps-trigger ~ .mat-picker__panel [data-value="calendar"]');
      expect(await valueOf('pk-apps'), 'mail, files, calendar', 'the value after a third choice');
      expect(await shown('pk-apps'), '3 selected', 'the field counts above two choices');
      expectTrue(await panelOf('pk-apps').isVisible(), 'the panel closed after a choice of several');
      await page.fill('#pk-apps-trigger ~ .mat-picker__panel input[type=search]', 'photo');
      expect(await page.locator('#pk-apps-trigger ~ .mat-picker__panel .mat-picker__option:not([hidden])').count(), 1, 'options after the search');
      await page.fill('#pk-apps-trigger ~ .mat-picker__panel input[type=search]', 'zzz');
      expectTrue(await page.locator('#pk-apps-trigger ~ .mat-picker__panel .mat-picker__empty').isVisible(), 'no message that nothing was found');
      await page.click('#pk-apps-trigger ~ .mat-picker__panel [data-picker-action]');
      expectTrue(!(await panelOf('pk-apps').isVisible()), 'Done did not close the panel');
      await page.click('#pk-apps-trigger ~ .mat-picker__clear');
      expect(await valueOf('pk-apps'), '', 'the value after Clear');
      expect(await shown('pk-apps'), 'Choose apps', 'the placeholder after Clear');
    });
    await check('a required select writes its message at the field and the focus goes to its button', async () => {
      await page.evaluate(() => document.getElementById('pk-req-trigger').scrollIntoView({ block: 'center' }));
      await page.click('form[action="/Components#rich-select"] button[type=submit]');
      await page.waitForTimeout(200);
      expect(await page.locator('#pk-req').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field is marked invalid');
      expect((await page.locator('#pk-req').evaluate(el => el.closest('.mat-field').querySelector('.mat-error').textContent)).trim(), 'Please choose a server.', 'the message');
      expect(await page.getAttribute('#pk-req-trigger', 'aria-invalid'), 'true', 'aria-invalid of the button');
      expect(await page.evaluate(() => document.activeElement.id), 'pk-req-trigger', 'the focus');
      await page.click('#pk-req-trigger');
      await page.click('#pk-req-trigger ~ .mat-picker__panel [data-value="vps"]');
      expect(await page.locator('#pk-req').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), false, 'the message after a choice');
    });

    // ---- tags
    const chips = id => page.$$eval(`#${id} ~ .mat-tagfield .mat-chip`, els => els.map(e => e.getAttribute('data-tag')).join('|'));
    const status = id => page.$eval(`#${id} ~ .mat-picker__status`, el => el.hidden ? '' : el.textContent.trim());
    await page.evaluate(() => document.getElementById('tag-input').scrollIntoView());
    await check('a tag input draws the saved tags as chips and posts them joined by a comma', async () => {
      expect(await chips('pk-tags'), 'finance|urgent', 'chips');
      expect(await valueOf('pk-tags'), 'finance, urgent', 'the posted value');
      expect(await page.locator('#pk-tags ~ .mat-tagfield .mat-chip__x').first().getAttribute('aria-label'), 'Remove tag finance', 'the name of a remove button');
      expect(await page.getAttribute('#pk-tags-entry', 'placeholder'), '', 'no placeholder while there are tags');
    });
    await check('Enter and a comma add a tag, a tag that is there is refused with a message, Backspace removes the last one', async () => {
      await page.click('#pk-tags-entry');
      await page.keyboard.type('Planning');
      await page.keyboard.press('Enter');
      await page.keyboard.type('review,');
      expect(await chips('pk-tags'), 'finance|urgent|Planning|review', 'chips after Enter and a comma');
      expect(await valueOf('pk-tags'), 'finance, urgent, Planning, review', 'the posted value');
      await page.keyboard.type('FINANCE');
      await page.keyboard.press('Enter');
      expect(await chips('pk-tags'), 'finance|urgent|Planning|review', 'a tag twice');
      expect(await status('pk-tags'), 'FINANCE is already there', 'the message');
      expect(await page.inputValue('#pk-tags-entry'), 'FINANCE', 'the text stays so that it can be corrected');
      await page.fill('#pk-tags-entry', '');
      await page.keyboard.press('Backspace');
      expect(await chips('pk-tags'), 'finance|urgent|Planning', 'Backspace in the empty field');
      expect(await status('pk-tags'), 'Removed review', 'the message');
    });
    await check('an X removes a tag and the focus stays in the field; pasted text is split at commas and line breaks', async () => {
      await page.click('#pk-tags ~ .mat-tagfield .mat-chip[data-tag="urgent"] .mat-chip__x');
      expect(await chips('pk-tags'), 'finance|Planning', 'after the X');
      expect(await page.evaluate(() => document.activeElement.id), 'pk-tags-entry', 'the focus');
      await page.evaluate(() => {
        const dt = new DataTransfer();
        dt.setData('text', 'alpha, beta\ngamma, Planning');
        document.getElementById('pk-tags-entry').dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
      });
      expect(await chips('pk-tags'), 'finance|Planning|alpha|beta|gamma', 'chips after the paste (Planning was there)');
      expect(await status('pk-tags'), 'Planning is already there', 'the message about the one that was refused');
      expect(await page.inputValue('#pk-tags-entry'), 'Planning', 'only the refused part stays in the text field');
    });
    await check('the arrow keys walk the remove buttons and Backspace removes the one in focus', async () => {
      await page.fill('#pk-tags-entry', '');
      await page.click('#pk-tags-entry');
      await page.keyboard.press('ArrowLeft');
      expect(await page.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Remove tag gamma', 'the last remove button');
      await page.keyboard.press('ArrowLeft');
      expect(await page.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Remove tag beta', 'the one before');
      await page.keyboard.press('Backspace');
      expect(await chips('pk-tags'), 'finance|Planning|alpha|gamma', 'after Backspace on a button');
      expect(await page.evaluate(() => document.activeElement.classList.contains('mat-chip__x')), true, 'the focus stays on a remove button');
      await page.keyboard.press('End');
    });
    await check('what is typed becomes a tag when the field is left', async () => {
      await page.click('#pk-tags-entry');
      await page.keyboard.type('last');
      await page.keyboard.press('Tab');
      expect(await chips('pk-tags'), 'finance|Planning|alpha|gamma|last', 'the typed text');
      expect(await valueOf('pk-tags'), 'finance, Planning, alpha, gamma, last', 'the posted value');
    });

    await page.evaluate(() => document.getElementById('pk-labels').scrollIntoView({ block: 'center' }));
    const suggestions = id => page.locator(`#${id} ~ .mat-picker__panel .mat-picker__option:not([hidden])`);
    await check('suggestions: the list opens under the field, narrows while typing, the arrow keys and Enter take one and a taken one is not offered again', async () => {
      await page.click('#pk-labels-entry');
      expectTrue(await page.locator('#pk-labels ~ .mat-picker__panel').isVisible(), 'the list is not shown');
      expect(await page.getAttribute('#pk-labels-entry', 'aria-expanded'), 'true', 'aria-expanded');
      expect(await suggestions('pk-labels').count(), 5, 'the suggestions: Mail is taken');
      await page.keyboard.type('ca');
      expect(await suggestions('pk-labels').allTextContents(), ['Calendar'], 'the list for "ca"');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.getElementById(document.getElementById('pk-labels-entry').getAttribute('aria-activedescendant')).textContent.trim()), 'Calendar', 'the marked suggestion');
      await page.keyboard.press('Enter');
      expect(await chips('pk-labels'), 'Mail|Calendar', 'chips after Enter on a suggestion');
      expect(await page.inputValue('#pk-labels-entry'), '', 'the text after the choice');
      expect(await suggestions('pk-labels').count(), 4, 'Calendar is not offered again');
      await page.waitForTimeout(400);   // the list fades in with a small scale: measure when it has settled
      const field = await page.locator('#pk-labels ~ .mat-tagfield').boundingBox();
      const panel = await page.locator('#pk-labels ~ .mat-picker__panel').boundingBox();
      expectTrue(Math.abs(panel.width - field.width) < 3 && panel.y >= field.y + field.height - 2, 'the list is not under the field with its width');
      await page.keyboard.press('Escape');
      expectTrue(!(await page.locator('#pk-labels ~ .mat-picker__panel').isVisible()), 'Escape did not close the list');
    });
    await check('the limit: at five tags the sixth is refused with a message', async () => {
      await page.click('#pk-labels ~ .mat-tagfield');
      for (const name of ['Files', 'Photos', 'Notes']) { await page.click(`#pk-labels ~ .mat-picker__panel .mat-picker__option[data-value="${name}"]`); }
      expect(await chips('pk-labels'), 'Mail|Calendar|Files|Photos|Notes', 'five tags');
      await page.keyboard.type('Backups');
      await page.keyboard.press('Enter');
      expect(await chips('pk-labels'), 'Mail|Calendar|Files|Photos|Notes', 'the sixth');
      expect(await status('pk-labels'), 'At most 5 tags', 'the message');
      await page.keyboard.press('Escape');
    });

    await page.evaluate(() => document.getElementById('pk-topics').scrollIntoView({ block: 'center' }));
    await check('only from the list: other text is refused, a required field writes its message and takes the focus into the field', async () => {
      await page.click('form[action="/Components#tag-input"] button[type=submit]');
      await page.waitForTimeout(200);
      expect(await page.locator('#pk-topics').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field is marked invalid');
      expect((await page.locator('#pk-topics').evaluate(el => el.closest('.mat-field').querySelector('.mat-error').textContent)).trim(), 'Please add at least one topic.', 'the message');
      expect(await page.evaluate(() => document.activeElement.id), 'pk-topics-entry', 'the focus');
      expect(await page.getAttribute('#pk-topics-entry', 'aria-invalid'), 'true', 'aria-invalid of the text field');
      await page.keyboard.type('zzz');
      expect(await suggestions('pk-topics').count(), 0, 'suggestions for "zzz"');
      expectTrue(await page.locator('#pk-topics ~ .mat-picker__panel .mat-picker__empty').isVisible(), 'no message that nothing was found');
      await page.keyboard.press('Enter');
      expect(await chips('pk-topics'), '', 'a tag that is not a suggestion');
      expect(await status('pk-topics'), 'Choose one of the suggestions', 'the message');
      await page.fill('#pk-topics-entry', 'sec');
      await page.keyboard.press('Enter');
      expect(await chips('pk-topics'), 'Security', 'the first match is taken by Enter');
      expect(await page.locator('#pk-topics').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), false, 'the message after a tag was added');
      expect(await valueOf('pk-topics'), 'Security', 'the posted value');
    });
    await check('a disabled input locks the field, a read-only one has no text field', async () => {
      const removable = await page.locator('#pk-tags ~ .mat-tagfield .mat-chip__x').count();
      expectTrue(removable > 0, 'no remove buttons before the lock');
      await page.evaluate(() => { document.getElementById('pk-labels').disabled = true; document.getElementById('pk-tags').readOnly = true; window.MatUi.refresh(); });
      expect(await page.locator('#pk-labels ~ .mat-tagfield .mat-chip__x').count(), 0, 'remove buttons of a disabled field');
      expect(await page.locator('#pk-labels-entry').isDisabled(), true, 'the text field of a disabled field');
      expect(await page.locator('#pk-tags ~ .mat-tagfield .mat-chip__x').count(), 0, 'remove buttons of a read-only field');
      expectTrue(!(await page.locator('#pk-tags-entry').isVisible()), 'the text field of a read-only field');
      await page.evaluate(() => { document.getElementById('pk-labels').disabled = false; document.getElementById('pk-tags').readOnly = false; window.MatUi.refresh(); });
      expect(await page.locator('#pk-tags ~ .mat-tagfield .mat-chip__x').count(), removable, 'remove buttons after the lock is gone');
    });

    await page.evaluate(() => document.getElementById('date-picker').scrollIntoView());
    await check('a date: shown in the language of the page, posted as ISO; a click on a day chooses it; days outside the limits cannot be chosen', async () => {
      expect(await shown('pk-date'), '10 Oct 2026', 'the field');
      const panel = '#pk-date-trigger ~ .mat-picker__panel';
      await page.click('#pk-date-trigger');
      expect(await page.locator(panel + ' .mat-cal__month').count(), 1, 'months of a single date');
      expect(await page.locator(panel + ' [data-date="2026-10-04"]').isDisabled(), true, 'a day before data-min');
      expect(await page.locator(panel + ' [data-date="2026-10-05"]').isDisabled(), false, 'the first day of data-min');
      expect(await page.locator(panel + ' [data-cal-nav="-1"]').isDisabled(), true, 'the arrow to the months before data-min');
      await page.click(panel + ' [data-cal-nav="1"]');
      expect((await page.textContent(panel + ' .mat-cal__title')).trim(), 'November 2026', 'the month after the arrow');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-cal-nav')), '1', 'the focus stays on the arrow');
      await page.click(panel + ' [data-cal-nav="1"]');
      expect(await page.locator(panel + ' [data-cal-nav="1"]').isDisabled(), true, 'the arrow to the months after data-max');
      await page.click(panel + ' [data-cal-nav="-1"]');
      await page.click(panel + ' [data-cal-nav="-1"]');
      await page.click(panel + ' [data-date="2026-10-15"]');
      expect(await valueOf('pk-date'), '2026-10-15', 'the posted value');
      expect(await shown('pk-date'), '15 Oct 2026', 'the field after the choice');
      expectTrue(!(await panelOf('pk-date').isVisible()), 'the panel is still shown');
    });
    await check('a calendar is a grid for the keyboard: arrows, page keys, Enter', async () => {
      await page.focus('#pk-date-trigger');
      await page.keyboard.press('Enter');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-date')), '2026-10-15', 'the focus is on the chosen day');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-date')), '2026-10-23', 'a day right, a week down');
      await page.keyboard.press('PageDown');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-date')), '2026-11-23', 'a month on');
      expect((await page.textContent('#pk-date-trigger ~ .mat-picker__panel .mat-cal__title')).trim(), 'November 2026', 'the month shown');
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-date'), '2026-11-23', 'Enter chose the day');
    });
    await check('a date can be cleared and starts empty', async () => {
      await page.click('#pk-date-trigger ~ .mat-picker__clear');
      expect(await valueOf('pk-date'), '', 'the value');
      expect(await shown('pk-date'), 'Choose a date', 'the placeholder');
      expectTrue(await page.locator('#pk-birth-trigger ~ .mat-picker__clear').isHidden(), 'a field without a value has a Clear button');
    });
    await check('a period shows two months; the second click completes it; an earlier second click starts again', async () => {
      await page.click('#pk-period-trigger');
      expect(await page.locator('#pk-period-trigger ~ .mat-picker__panel .mat-cal__month').count(), 2, 'months of a period');
      const day = d => `#pk-period-trigger ~ .mat-picker__panel [data-date="${d}"]`;
      await page.click(day('2026-10-25'));
      await page.hover(day('2026-10-28'));
      expectTrue(await page.locator(day('2026-10-27')).evaluate(el => el.classList.contains('is-in-range')), 'no preview of the days up to the pointer');
      await page.click(day('2026-10-22'));
      expect(await valueOf('pk-period'), '2026-10-12..2026-10-16', 'a click before the start only begins again');
      await page.click(day('2026-10-24'));
      expect(await valueOf('pk-period'), '2026-10-22..2026-10-24', 'the period');
      expect(await shown('pk-period'), '22 Oct 2026 – 24 Oct 2026', 'the field');
      expectTrue(!(await panelOf('pk-period').isVisible()), 'the period did not close the panel');
    });
    await check('flexible dates are chosen with the period and applied with the button', async () => {
      await page.click('#pk-flex-trigger');
      const apply = page.locator('#pk-flex-trigger ~ .mat-picker__panel [data-picker-action="apply"]');
      expect(await page.locator('#pk-flex-trigger ~ .mat-picker__panel [data-cal-flex][aria-checked="true"]').getAttribute('data-cal-flex'), '2', 'the chosen flexibility');
      await page.click('#pk-flex-trigger ~ .mat-picker__panel [data-cal-flex="3"]');
      expect(await valueOf('pk-flex'), '2026-11-02..2026-11-06~2', 'the value is not changed before Apply');
      await apply.click();
      expect(await valueOf('pk-flex'), '2026-11-02..2026-11-06~3', 'the value after Apply');
      expectTrue((await shown('pk-flex')).includes('± 3 days'), 'the field does not say the flexibility: ' + await shown('pk-flex'));
      await page.click('#pk-flex-trigger');
      await page.click('#pk-flex-trigger ~ .mat-picker__panel [data-picker-action="cancel"]');
      expect(await valueOf('pk-flex'), '2026-11-02..2026-11-06~3', 'Cancel keeps the value');
    });

    await page.evaluate(() => document.getElementById('time-picker').scrollIntoView());
    await check('a time can be typed: 1445 and Enter is 14:45', async () => {
      expect(await shown('pk-time'), '09:30', 'the field');
      await page.click('#pk-time-trigger');
      expectTrue(await page.evaluate(() => document.activeElement.hasAttribute('data-time-entry')), 'the field to type in has no focus on a wide screen');
      await page.keyboard.type('1445');
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-time'), '14:45', 'the posted value');
      expect(await shown('pk-time'), '14:45', 'the field');
      expectTrue(!(await panelOf('pk-time').isVisible()), 'the panel is still shown');
    });
    await check('a time is chosen in columns and applied; Cancel keeps the old time; the minutes follow the step', async () => {
      await page.click('#pk-time-trigger');
      const panel = '#pk-time-trigger ~ .mat-picker__panel';
      expect(await page.locator(`${panel} [data-time-col="m"] .mat-timecol__item`).allTextContents(), ['00', '15', '30', '45'], 'minutes of a step of 15');
      await page.click(`${panel} [data-time-col="h"] [data-v="8"]`);
      await page.click(`${panel} [data-time-col="m"] [data-v="15"]`);
      expect(await page.$eval(`${panel} [data-time-entry]`, el => el.value), '08:15', 'the field to type in follows the columns');
      expect(await valueOf('pk-time'), '14:45', 'nothing is posted before Apply');
      await page.click(`${panel} [data-picker-action="cancel"]`);
      expect(await valueOf('pk-time'), '14:45', 'Cancel');
      await page.click('#pk-time-trigger');
      await page.click(`${panel} [data-time-col="h"] [data-v="8"]`);
      await page.click(`${panel} [data-picker-action="apply"]`);
      expect(await valueOf('pk-time'), '08:45', 'Apply');
    });
    await check('with 12 hours the field shows AM and PM but posts 24 hours', async () => {
      expectTrue((await shown('pk-time12')).includes('02:45'), 'the field: ' + await shown('pk-time12'));
      await page.click('#pk-time12-trigger');
      const panel = '#pk-time12-trigger ~ .mat-picker__panel';
      expect(await page.locator(`${panel} [data-time-col="ap"] .mat-timecol__item`).allTextContents(), ['AM', 'PM'], 'the AM and PM column');
      await page.click(`${panel} [data-time-col="ap"] [data-v="am"]`);
      await page.click(`${panel} [data-picker-action="apply"]`);
      expect(await valueOf('pk-time12'), '02:45', 'the posted value');
    });
    await check('a time range: From and To, the length is told, an end before the start is the next day', async () => {
      expect(await shown('pk-hours'), '09:00 – 17:30', 'the field');
      await page.click('#pk-hours-trigger');
      const panel = '#pk-hours-trigger ~ .mat-picker__panel';
      expect((await page.textContent(`${panel} .mat-picker__status`)).trim(), '8 h 30 min', 'the length');
      await page.click(`${panel} [data-time-part="to"]`);
      await page.fill(`${panel} [data-time-entry]`, '0730');
      expectTrue((await page.textContent(`${panel} .mat-picker__status`)).includes('next day'), 'no hint that the end is the next day: ' + await page.textContent(`${panel} .mat-picker__status`));
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-hours'), '09:00..07:30', 'the range');
    });

    await page.evaluate(() => document.getElementById('colour-picker').scrollIntoView());
    await check('a colour: a swatch is chosen with one click; the field shows the chip and the hex value', async () => {
      expect(await shown('pk-colour'), '#B4235F', 'the field');
      expect(await page.locator('#pk-colour-trigger .mat-picker__swatch').count(), 1, 'the chip');
      await page.click('#pk-colour-trigger');
      await page.click('#pk-colour-trigger ~ .mat-picker__panel [data-hex="#16a34a"]');
      expect(await valueOf('pk-colour'), '#16a34a', 'the posted value');
      expectTrue(!(await panelOf('pk-colour').isVisible()), 'a swatch did not close the panel');
    });
    await check('a free colour: the hex value is typed, the area and the hue move it, and the text colour with the best contrast is told', async () => {
      await page.click('#pk-colour-trigger');
      const panel = '#pk-colour-trigger ~ .mat-picker__panel';
      await page.fill(`${panel} .mat-colour__hex`, '#112233');
      expect(await valueOf('pk-colour'), '#16a34a', 'nothing is posted before Apply');
      expectTrue((await page.textContent(`${panel} .mat-colour__contrast`)).includes('White text'), 'no contrast of white text on a dark colour');
      await page.fill(`${panel} .mat-colour__hex`, '#fff');
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-colour'), '#ffffff', 'Enter takes the typed colour (three digits become six)');
      await page.click('#pk-colour-trigger');
      await page.focus(`${panel} .mat-colour__area`);
      await page.keyboard.press('ArrowDown');
      expect(await page.$eval(`${panel} .mat-colour__hex`, el => el.value), '#fcfcfc', 'the brightness one step down');
      await page.click(`${panel} [data-picker-action="apply"]`);
      expect(await valueOf('pk-colour'), '#fcfcfc', 'Apply');
      await page.click('#pk-colour-trigger ~ .mat-picker__clear');
      expect(await valueOf('pk-colour'), '', 'Clear');
    });
    await check('a picker in a dialog is not cut off, and Escape closes the picker first and the dialog second', async () => {
      await page.evaluate(() => {
        const d = document.createElement('dialog');
        d.className = 'mat-dialog mat-dialog--sm';
        d.id = 'pk-dlg';
        d.setAttribute('aria-label', 'Picker in a dialog');
        d.innerHTML = '<div class="mat-dialog__form"><div class="mat-dialog__body"><div class="mat-field"><label class="mat-label" for="pk-in-dlg">Day</label>' +
          '<div class="mat-picker" data-picker="date"><input class="mat-picker__input" id="pk-in-dlg" name="day" value="2026-10-10"></div></div></div></div>';
        document.body.appendChild(d);
        window.MatUi.refresh(d);
        window.MatUi.dialog.open(d);
      });
      await page.click('#pk-in-dlg-trigger');
      const box = await page.$eval('#pk-in-dlg-trigger ~ .mat-picker__panel', el => { const r = el.getBoundingClientRect(); return { inWindow: r.left >= 0 && r.right <= innerWidth && r.top >= 0 && r.bottom <= innerHeight, visible: r.width > 100 && r.height > 100 }; });
      expect(box, { inWindow: true, visible: true }, 'the panel in the dialog');
      const top = await page.evaluate(() => { const r = document.querySelector('#pk-in-dlg-trigger ~ .mat-picker__panel').getBoundingClientRect(); const el = document.elementFromPoint(r.left + r.width / 2, r.top + 40); return !!el && !!el.closest('.mat-picker__panel'); });
      expect(top, true, 'the panel is above the dialog');
      await page.keyboard.press('Escape');
      expect(await page.$eval('#pk-dlg', el => el.open), true, 'the dialog is still open after the first Escape');
      expectTrue(!(await page.locator('#pk-in-dlg-trigger ~ .mat-picker__panel').isVisible()), 'the picker is still open');
      await page.keyboard.press('Escape');
      expect(await page.$eval('#pk-dlg', el => el.open), false, 'the second Escape closes the dialog');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#date-picker');
    await check('on a phone a picker is a full-screen sheet with an X; one month for a period; the X closes it', async () => {
      await page.click('#pk-period-trigger');
      const panel = '#pk-period-trigger ~ .mat-picker__panel';
      const state = await page.$eval(panel, el => { const r = el.getBoundingClientRect(); return { width: Math.round(r.width) === innerWidth, height: Math.round(r.height) === innerHeight, x: getComputedStyle(el.querySelector('.mat-picker__head')).display !== 'none' }; });
      expect(state, { width: true, height: true, x: true }, 'the sheet');
      expect(await page.locator(`${panel} .mat-cal__month`).count(), 1, 'months on a phone');
      expect(await page.evaluate(() => document.documentElement.classList.contains('mat-lock')), true, 'the page behind does not scroll');
      await page.click(`${panel} [data-picker-close]`);
      expectTrue(!(await page.locator(panel).isVisible()), 'the X did not close the sheet');
      expect(await page.evaluate(() => document.documentElement.classList.contains('mat-lock')), false, 'the page is free again');
    });
    await check('on a phone the time picker does not pop up the keyboard: the chosen hour has the focus, not the field to type in', async () => {
      await page.evaluate(() => document.getElementById('time-picker').scrollIntoView());
      await page.click('#pk-time-trigger');
      expect(await page.evaluate(() => document.activeElement.classList.contains('mat-timecol__item')), true, 'the focus');
      await page.click('#pk-time-trigger ~ .mat-picker__panel [data-picker-close]');
    });
    await check('on a phone the suggestions of a tag input are a dropdown under the field, not a full-screen sheet, and the chips wrap inside the field', async () => {
      await page.evaluate(() => document.getElementById('pk-labels').scrollIntoView({ block: 'center' }));
      await page.click('#pk-labels-entry');
      const panel = '#pk-labels ~ .mat-picker__panel';
      expectTrue(await page.locator(panel).isVisible(), 'the list is not shown');
      await page.waitForTimeout(400);   // the list fades in with a small scale: measure when it has settled
      const state = await page.evaluate(() => {
        const f = document.querySelector('#pk-labels ~ .mat-tagfield').getBoundingClientRect();
        const p = document.querySelector('#pk-labels ~ .mat-picker__panel').getBoundingClientRect();
        return { sheet: Math.round(p.height) >= innerHeight, widthOk: Math.abs(p.width - f.width) < 3, beside: p.top >= f.bottom - 2 || p.bottom <= f.top + 2, inside: p.left >= 0 && p.right <= innerWidth, noLock: !document.documentElement.classList.contains('mat-lock'), overflow: document.documentElement.scrollWidth > innerWidth };
      });
      expect(state, { sheet: false, widthOk: true, beside: true, inside: true, noLock: true, overflow: false }, 'the list');
      await page.keyboard.press('Escape');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- calendar (FORM-18)
  // The checks do not depend on the day they run: what is about today is computed from the clock of the machine, the rest happens in March 2030.
  console.log('\nCalendar');
  const calPad = n => String(n).padStart(2, '0');
  const calIso = d => `${d.getFullYear()}-${calPad(d.getMonth() + 1)}-${calPad(d.getDate())}`;
  const calDate = s => { const [y, m, d] = s.split('-').map(Number); return new Date(y, m - 1, d); };
  const calPlus = n => { const d = new Date(); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + n); return d; };
  // puts markup into the page (below the examples) and wires it, like an app that adds a calendar later
  const calPut = (page, html) => page.evaluate(h => {
    let box = document.getElementById('e2e-box');
    if (!box) { box = document.createElement('div'); box.id = 'e2e-box'; document.querySelector('.mat-main').appendChild(box); }
    box.innerHTML = h;
    window.MatUi.refresh(box);
  }, html);
  const calField = (id, attrs, label = 'Day', extra = '') => `<div class="mat-field"><label class="mat-label" for="${id}">${label}</label><div class="mat-calendar" data-calendar ${attrs}><input class="mat-calendar__input" id="${id}" name="${id}" ${extra} /></div></div>`;
  const calLog = (page, id) => page.evaluate(i => {
    window.__events = [];
    const el = document.getElementById(i);
    ['input', 'change'].forEach(t => el.addEventListener(t, () => window.__events.push(t + ':' + el.value)));
  }, id);
  const calEvents = page => page.evaluate(() => window.__events);
  const calGo = (page, id, day) => page.evaluate(([i, d]) => window.MatUi.calendar.go(document.getElementById(i), d), [id, day]);
  const calDays = (page, root) => page.$$eval(`${root} .mat-cal__day`, els => els.map(b => ({ date: b.getAttribute('data-date'), off: b.disabled, cls: b.className, label: b.getAttribute('aria-label'), tab: b.tabIndex, marks: b.querySelectorAll('.mat-cal__mark').length })));
  // the first day that can be chosen, looking at this month and at the next two
  const calFirstOpen = async (page, root) => {
    for (let i = 0; i < 3; i += 1) {
      const open = (await calDays(page, root)).filter(d => !d.off)[0];
      if (open) { return open.date; }
      await page.click(`${root} [data-cal-nav="1"]`);
    }
    return null;
  };
  const calTitle = (page, root) => page.$eval(`${root} .mat-cal__title`, el => el.textContent.trim());
  const calActive = page => page.evaluate(() => document.activeElement.getAttribute('data-date') || document.activeElement.getAttribute('data-cal-month') || document.activeElement.getAttribute('data-cal-year') || document.activeElement.className);
  {
    const { ctx, page } = await open(desktopOptions, '/Components#calendar');
    const calRoot = '#e2e-box .mat-calendar';

    await check('a calendar names itself after its label, shows one month from Monday with today marked, and posts nothing before a choice', async () => {
      await calPut(page, calField('e2e-c1', '', 'Day of the visit'));
      expect(await page.getAttribute(calRoot, 'aria-label'), 'Day of the visit', 'the name of the group');
      expect(await page.getAttribute(calRoot, 'role'), 'group', 'the role');
      expect(await page.locator(`${calRoot} .mat-cal__month`).count(), 1, 'months');
      expect((await page.$$eval(`${calRoot} .mat-cal__dow`, els => els.map(e => e.textContent.trim()))).join(' '), 'Mon Tue Wed Thu Fri Sat Sun', 'the week days');
      expect(await page.$$eval(`${calRoot} .mat-cal__day.is-today`, els => els.map(e => e.getAttribute('data-date'))), [calIso(calPlus(0))], 'today');
      expect(await page.$eval('#e2e-c1', el => el.value), '', 'the value');
      expect((await calDays(page, calRoot)).filter(d => d.tab === 0).length, 1, 'days that the Tab key stops at');
      expectTrue(/\d{4}$/.test(await page.getAttribute(`${calRoot} .mat-cal__day.is-today`, 'aria-label')), 'a day is named with its full date');
    });

    await check('data-min="today" and data-max take the past and the far future away, closed weekdays are disabled, and a click posts one ISO date with input and change', async () => {
      await calPut(page, calField('e2e-c2', 'data-min="today" data-max="+20d" data-disabled-weekdays="0,6"'));
      await calLog(page, 'e2e-c2');
      const today = calPlus(0), last = calPlus(20);
      const wanted = d => { const x = calDate(d); return x < today || x > last || x.getDay() === 0 || x.getDay() === 6; };
      const wrong = [];
      for (let step = 0; step < 3; step += 1) {
        (await calDays(page, calRoot)).forEach(d => { if (d.off !== wanted(d.date)) { wrong.push(d.date); } });
        if (await page.locator(`${calRoot} [data-cal-nav="1"]`).isDisabled()) { break; }
        await page.click(`${calRoot} [data-cal-nav="1"]`);
      }
      expect(wrong, [], 'days with the wrong state');
      expect(await page.locator(`${calRoot} [data-cal-nav="1"]`).isDisabled(), true, 'no way to a month after data-max');
      await calGo(page, 'e2e-c2', calIso(today));
      expect(await page.locator(`${calRoot} [data-cal-nav="-1"]`).isDisabled(), true, 'no way to a month before data-min');
      const first = await calFirstOpen(page, calRoot);
      expectTrue(first !== null && calDate(first) >= today, 'a day that can be chosen: ' + first);
      await page.click(`${calRoot} [data-date="${first}"]`);
      expect(await page.$eval('#e2e-c2', el => el.value), first, 'the posted value');
      expect(await calEvents(page), [`input:${first}`, `change:${first}`], 'the events of the real input');
      expect(await page.getAttribute(`${calRoot} [data-date="${first}"]`, 'aria-selected'), 'true', 'aria-selected');
      await page.click(`${calRoot} [data-date="${first}"]`);
      expect(await page.$eval('#e2e-c2', el => el.value), first, 'a second click on the day keeps it (no event twice)');
      expect((await calEvents(page)).length, 2, 'events after the second click');
    });

    await check('a limit can be a word or a distance: tomorrow, +3d and -1w', async () => {
      await calPut(page, calField('e2e-c3', 'data-min="+3d" data-max="+3m"'));
      // the first day that can be chosen is three days from now (in this month, or else in the next one, to which the arrow leads)
      expect(await calFirstOpen(page, calRoot), calIso(calPlus(3)), 'the first day that can be chosen');
      await calPut(page, calField('e2e-c3', 'data-min="tomorrow" data-max="today"'));
      expect((await calDays(page, calRoot)).filter(d => !d.off).length, 0, 'tomorrow after today leaves no day');
      await calPut(page, calField('e2e-c3', 'data-min="-1w" data-max="today"'));
      const open = (await calDays(page, calRoot)).filter(d => !d.off).map(d => d.date);
      expectTrue(open.every(d => calDate(d) >= calPlus(-7) && calDate(d) <= calPlus(0)), 'days outside the week behind today are open: ' + open.join(','));
    });

    await check('a period: the first click is the start, the pointer previews, the second completes it, a click before the start begins again', async () => {
      await calPut(page, calField('e2e-c4', 'data-mode="range" data-clearable'));
      await calLog(page, 'e2e-c4');
      await calGo(page, 'e2e-c4', '2030-03-01');
      const day = d => `${calRoot} [data-date="${d}"]`;
      expect((await page.textContent(`${calRoot} .mat-picker__status`)).trim(), 'Choose the start date', 'the line before the start');
      await page.click(day('2030-03-12'));
      expect((await page.textContent(`${calRoot} .mat-picker__status`)).trim(), 'Choose the end date', 'the line after the start');
      await page.hover(day('2030-03-15'));
      expect(await page.$$eval(`${calRoot} .is-in-range`, els => els.map(e => e.getAttribute('data-date'))), ['2030-03-13', '2030-03-14'], 'the preview up to the pointer');
      expect(await page.$eval('#e2e-c4', el => el.value), '', 'nothing is posted before both ends are chosen');
      await page.click(day('2030-03-15'));
      expect(await page.$eval('#e2e-c4', el => el.value), '2030-03-12..2030-03-15', 'the period');
      expect((await page.textContent(`${calRoot} .mat-picker__status`)).replace(/\s+/g, ' ').trim(), '12 Mar 2030 – 15 Mar 2030 · 4 days', 'the line under the month');
      expect(await page.$$eval(`${calRoot} .is-selected`, els => els.map(e => e.getAttribute('data-date'))), ['2030-03-12', '2030-03-15'], 'the ends');
      await page.click(day('2030-03-05'));
      expect(await page.$eval('#e2e-c4', el => el.value), '', 'a day before the start begins again');
      await page.click(day('2030-03-07'));
      expect(await page.$eval('#e2e-c4', el => el.value), '2030-03-05..2030-03-07', 'the new period');
      expect((await calEvents(page)).filter(e => e.startsWith('change')), ['change:2030-03-12..2030-03-15', 'change:', 'change:2030-03-05..2030-03-07'], 'a change event for every change of the value (an unfinished period is an empty value)');
      await page.click(`${calRoot} [data-cal-clear]`);
      expect(await page.$eval('#e2e-c4', el => el.value), '', 'Clear');
      expect(await page.$$eval(`${calRoot} .is-selected`, els => els.length), 0, 'nothing is marked after Clear');
    });

    await check('a period does not run across a taken day but runs across closed weekdays', async () => {
      await calPut(page, calField('e2e-c5', 'data-mode="range" data-disabled="2030-03-14" data-disabled-weekdays="0,6"'));
      await calGo(page, 'e2e-c5', '2030-03-01');
      const day = d => `${calRoot} [data-date="${d}"]`;
      await page.click(day('2030-03-11'));
      await page.click(day('2030-03-15'));
      expect(await page.$eval('#e2e-c5', el => el.value), '', 'the day behind a taken day only starts a new period');
      expect((await page.textContent(`${calRoot} .mat-picker__status`)).trim(), 'Choose the end date', 'the line');
      await page.click(day('2030-03-18'));
      expect(await page.$eval('#e2e-c5', el => el.value), '2030-03-15..2030-03-18', 'a period across a weekend');
      expect(await page.locator(day('2030-03-14')).isDisabled(), true, 'the taken day');
      expect(await page.locator(day('2030-03-16')).isDisabled(), true, 'a Saturday');
    });

    await check('several days: a click adds or removes, the value is sorted, the count is told and data-max-dates refuses one more', async () => {
      await calPut(page, calField('e2e-c6', 'data-mode="multiple" data-max-dates="3"', 'Days', 'value="2030-03-20"'));
      await calLog(page, 'e2e-c6');
      const day = d => `${calRoot} [data-date="${d}"]`;
      expect((await page.textContent(`${calRoot} .mat-picker__status`)).trim(), '1 day chosen', 'the start (the value was read)');
      await page.click(day('2030-03-05'));
      await page.click(day('2030-03-12'));
      expect(await page.$eval('#e2e-c6', el => el.value), '2030-03-05,2030-03-12,2030-03-20', 'sorted');
      expect((await page.textContent(`${calRoot} .mat-picker__status`)).trim(), '3 days chosen', 'the count');
      await page.click(day('2030-03-25'));
      expect(await page.$eval('#e2e-c6', el => el.value), '2030-03-05,2030-03-12,2030-03-20', 'the fourth day is refused');
      expect((await page.textContent(`${calRoot} .mat-picker__status`)).trim(), 'At most 3 days', 'the message');
      await page.click(day('2030-03-12'));
      expect(await page.$eval('#e2e-c6', el => el.value), '2030-03-05,2030-03-20', 'a chosen day is taken away by a second click');
      expect(await page.getAttribute(day('2030-03-12'), 'aria-selected'), 'false', 'aria-selected after that');
      expect((await calEvents(page)).length, 6, 'input and change for each of the three changes');
    });

    await check('days and periods in data-disabled and the weekdays can be changed with MatUi.calendar.setDisabled', async () => {
      await calPut(page, calField('e2e-c7', 'data-disabled="2030-03-14,2030-03-20..2030-03-22"'));
      await calGo(page, 'e2e-c7', '2030-03-01');
      const off = async () => (await calDays(page, calRoot)).filter(d => d.off).map(d => d.date);
      expect(await off(), ['2030-03-14', '2030-03-20', '2030-03-21', '2030-03-22'], 'what the attribute says');
      await page.evaluate(() => window.MatUi.calendar.setDisabled(document.getElementById('e2e-c7'), { dates: ['2030-03-05'], weekdays: [0, 6] }));
      expect((await off()).filter(d => calDate(d).getDay() !== 0 && calDate(d).getDay() !== 6), ['2030-03-05'], 'the days after setDisabled (the weekends apart)');
      expect((await off()).length, 11, 'one day and the ten days of the weekends of March 2030');
      await page.evaluate(() => window.MatUi.calendar.setDisabled(document.getElementById('e2e-c7'), { dates: null, weekdays: '', max: '2030-03-10' }));
      expect((await calDays(page, calRoot)).filter(d => !d.off).map(d => d.date).at(-1), '2030-03-10', 'data-max through the API');
    });

    await check('days with events carry dots (up to three) and say how many in their name, and the agenda lists the events of the chosen day', async () => {
      const events = JSON.stringify({
        '2030-03-05': [{ title: 'Dentist', time: '09:00', end: '09:45' }, { title: 'Team lunch', time: '12:30' }, 'Hand in the form'],
        '2030-03-12': [{ title: 'One' }, { title: 'Two' }, { title: 'Three' }, { title: 'Four', time: '07:00' }],
        '2030-03-20': [{ title: 'Backup check', time: '08:00' }],
      });
      await calPut(page, calField('e2e-c8', `data-agenda data-events='${events}'`, 'Day', 'value="2030-03-05"'));
      const days = Object.fromEntries((await calDays(page, calRoot)).map(d => [d.date, d]));
      expect([days['2030-03-05'].marks, days['2030-03-12'].marks, days['2030-03-20'].marks, days['2030-03-21'].marks], [3, 3, 1, 0], 'dots');
      expectTrue(days['2030-03-12'].label.endsWith(', 4 events') && days['2030-03-20'].label.endsWith(', 1 event'), 'the names of the days: ' + days['2030-03-12'].label);
      const agenda = () => page.$eval(`${calRoot} .mat-calendar__agenda`, el => ({ title: el.querySelector('.mat-calendar__agenda-title').textContent.trim(), items: [...el.querySelectorAll('.mat-calendar__event')].map(li => li.innerText.replace(/\s+/g, ' ').trim()), none: !!el.querySelector('.mat-calendar__none') }));
      expect((await agenda()).items, ['All day Hand in the form', '09:00 – 09:45 Dentist', '12:30 Team lunch'], 'the events of the chosen day in the order of the time (a day long event first)');
      expectTrue(/March 2030/.test((await agenda()).title), 'the agenda names the day: ' + (await agenda()).title);
      await page.click(`${calRoot} [data-date="2030-03-12"]`);
      expect((await agenda()).items, ['All day One', 'All day Two', 'All day Three', '07:00 Four'], 'the events of the next day (the ones without a time first, in the order they were given)');
      await page.click(`${calRoot} [data-date="2030-03-13"]`);
      expect(await agenda(), { title: (await agenda()).title, items: [], none: true }, 'a day without events');
      expect((await page.textContent(`${calRoot} .mat-calendar__none`)).trim(), 'Nothing on this day', 'the text');
      await page.evaluate(() => window.MatUi.calendar.setEvents(document.getElementById('e2e-c8'), [{ date: '2030-03-13', title: 'Added later', time: '10:15' }]));
      expect((await agenda()).items, ['10:15 Added later'], 'the agenda after setEvents');
      const after = Object.fromEntries((await calDays(page, calRoot)).map(d => [d.date, d.marks]));
      expect([after['2030-03-13'], after['2030-03-05']], [1, 0], 'the dots after setEvents (the old events are replaced)');
    });

    await check('week numbers are ISO weeks in a column of their own, and the first weekday can be Sunday', async () => {
      await calPut(page, calField('e2e-c9', 'data-week-numbers'));
      await calGo(page, 'e2e-c9', '2030-03-01');
      // the ISO week by the rule of the Thursday, written down again here
      const iso = s => { const d = calDate(s); const t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate())); t.setUTCDate(t.getUTCDate() + 4 - (t.getUTCDay() || 7)); return Math.ceil(((t - Date.UTC(t.getUTCFullYear(), 0, 1)) / 86400000 + 1) / 7); };
      const weeks = await page.$$eval(`${calRoot} .mat-cal__wk`, els => els.map(e => +e.textContent));
      expect(weeks, ['2030-03-01', '2030-03-04', '2030-03-11', '2030-03-18', '2030-03-25'].map(iso), 'the weeks of March 2030');
      expect(await page.$$eval(`${calRoot} .mat-cal__dow`, els => els[0].textContent.trim()), 'Wk', 'the heading of the column');
      await calPut(page, calField('e2e-c10', 'data-week-start="0"'));
      expect((await page.$$eval(`${calRoot} .mat-cal__dow`, els => els.map(e => e.textContent.trim()))).join(' '), 'Sun Mon Tue Wed Thu Fri Sat', 'the week starts on Sunday');
    });

    await check('the title leads to the months and the years: a date of birth is a few clicks, Escape goes back, limits disable what is outside', async () => {
      await calPut(page, calField('e2e-c11', 'data-min="2029-05-10" data-max="2031-09-20"'));
      await calGo(page, 'e2e-c11', '2030-03-15');
      expect(await calTitle(page, calRoot), 'March 2030', 'the title');
      await page.click(`${calRoot} [data-cal-level="months"]`);
      expect(await page.locator(`${calRoot} [data-cal-month]`).count(), 12, 'months');
      expect(await page.getAttribute(`${calRoot} [data-cal-month="2"]`, 'aria-pressed'), 'true', 'the month that is shown');
      expect(await calActive(page), '2', 'the focus is on that month');
      await page.click(`${calRoot} [data-cal-nav="1"]`);
      expect(await calTitle(page, calRoot), '2031', 'the year after the arrow');
      expect(await page.$$eval(`${calRoot} [data-cal-month]`, els => els.filter(e => e.disabled).map(e => e.getAttribute('data-cal-month')).join(',')), '9,10,11', 'the months after data-max');
      await page.click(`${calRoot} [data-cal-level="years"]`);
      expect(await page.locator(`${calRoot} [data-cal-year]`).count(), 12, 'years');
      expect(await page.$$eval(`${calRoot} [data-cal-year]`, els => els.filter(e => e.disabled).map(e => e.getAttribute('data-cal-year')).join(',')), '2028,2032,2033,2034,2035,2036,2037,2038,2039', 'the years outside the limits (twelve years from 2028)');
      await page.click(`${calRoot} [data-cal-year="2029"]`);
      expect(await page.$$eval(`${calRoot} [data-cal-month]`, els => els.filter(e => e.disabled).map(e => e.getAttribute('data-cal-month')).join(',')), '0,1,2,3', 'the months before data-min');
      await page.click(`${calRoot} [data-cal-month="7"]`);
      expect(await calTitle(page, calRoot), 'August 2029', 'back at the days');
      expect(await page.evaluate(() => /^2029-08-/.test(document.activeElement.getAttribute('data-date') || '')), true, 'the focus is on a day of that month');
      await page.keyboard.press('Escape');
      await page.click(`${calRoot} [data-cal-level="months"]`);
      await page.keyboard.press('Escape');
      expect(await page.locator(`${calRoot} [data-cal-month]`).count(), 0, 'Escape in the months goes back to the days');
      expect(await page.locator(`${calRoot} .mat-cal__day`).count() > 27, true, 'the days are there');
    });

    await check('the months and the years are walked with the arrow keys and the page keys', async () => {
      await calGo(page, 'e2e-c11', '2030-03-15');
      await page.click(`${calRoot} [data-cal-level="months"]`);
      await page.keyboard.press('ArrowRight');
      expect(await calActive(page), '3', 'a month on');
      await page.keyboard.press('ArrowDown');
      expect(await calActive(page), '6', 'a row down');
      await page.keyboard.press('PageDown');
      expect(await calTitle(page, calRoot), '2031', 'a year on');
      await page.keyboard.press('Enter');
      expect(await calTitle(page, calRoot), 'July 2031', 'Enter takes the month (the same cell stays the focus after the page key)');
    });

    await check('a calendar is a grid for the keyboard: arrows, Home and End, Page up and down (with Shift the year), disabled days are stepped over', async () => {
      await calPut(page, calField('e2e-c12', 'data-disabled-weekdays="0,6" data-disabled="2030-03-19"'));
      await calGo(page, 'e2e-c12', '2030-03-15');
      await page.click(`${calRoot} [data-date="2030-03-15"]`);
      await page.keyboard.press('ArrowRight');
      expect(await calActive(page), '2030-03-18', 'Friday and one to the right: the weekend is stepped over');
      await page.keyboard.press('ArrowLeft');
      expect(await calActive(page), '2030-03-15', 'and back');
      await page.keyboard.press('ArrowDown');
      expect(await calActive(page), '2030-03-22', 'a week down');
      await page.keyboard.press('ArrowUp');
      await page.keyboard.press('ArrowUp');
      expect(await calActive(page), '2030-03-08', 'two weeks up');
      await page.keyboard.press('ArrowDown');
      await page.keyboard.press('ArrowRight');
      expect(await calActive(page), '2030-03-18', 'Friday the 15th to the Monday');
      await page.keyboard.press('ArrowRight');
      expect(await calActive(page), '2030-03-20', 'the taken Tuesday is stepped over too');
      await page.keyboard.press('End');
      expect(await calActive(page), '2030-03-22', 'End: the last day of the week that can be chosen (Friday)');
      await page.keyboard.press('Home');
      expect(await calActive(page), '2030-03-18', 'Home: the first one (Monday)');
      await page.keyboard.press('PageDown');
      expect(await calTitle(page, calRoot), 'April 2030', 'a month on');
      expect(await calActive(page), '2030-04-18', 'the same day number');
      await page.keyboard.press('Shift+PageUp');
      expect(await calTitle(page, calRoot), 'April 2029', 'a year back');
      await page.keyboard.press('Enter');
      expect(await page.$eval('#e2e-c12', el => el.value), '2029-04-18', 'Enter chose the day');
    });

    await check('Today goes to the month of today and the focus to today (or the next day that can be chosen)', async () => {
      await calPut(page, calField('e2e-c13', 'data-min="today"'));
      await calGo(page, 'e2e-c13', '2030-03-15');
      await page.click(`${calRoot} [data-cal-today]`);
      const today = calPlus(0);
      expect(await calTitle(page, calRoot), new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric' }).format(today), 'the month');
      expect(await calActive(page), calIso(today), 'the focus');
    });

    await check('a required calendar writes its message at the field, takes the focus to a day and forgets the choice on reset', async () => {
      await calPut(page, `<form class="mat-form" id="e2e-form" method="get" action="/Components#calendar" novalidate>${calField('e2e-c14', 'data-min="today"', 'Day', 'required data-msg-required="Please choose a day."').replace('</div></div>', '</div><span class="mat-error" aria-live="polite"></span></div>')}<button type="submit" class="mat-btn mat-btn--primary" id="e2e-send">Continue</button><button type="reset" class="mat-btn mat-btn--secondary" id="e2e-reset">Reset</button></form>`);
      const calmBorder = await page.$eval(calRoot, el => getComputedStyle(el).borderTopColor);
      await page.click('#e2e-send');
      await page.waitForTimeout(150);
      expect(await page.$eval('#e2e-c14', el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field is marked');
      expect((await page.$eval('#e2e-c14', el => el.closest('.mat-field').querySelector('.mat-error').textContent)).trim(), 'Please choose a day.', 'the message');
      expectTrue(await page.evaluate(() => !!document.activeElement.getAttribute('data-date')), 'the focus is on a day');
      expectTrue((await page.$eval(calRoot, el => getComputedStyle(el).borderTopColor)) !== calmBorder, 'the line of the calendar turns red');
      const first = await calFirstOpen(page, calRoot);
      await page.click(`${calRoot} [data-date="${first}"]`);
      expect(await page.$eval('#e2e-c14', el => el.closest('.mat-field').classList.contains('is-invalid')), false, 'the message after a choice');
      await page.click('#e2e-reset');
      await page.waitForTimeout(100);
      expect(await page.$eval('#e2e-c14', el => el.value), '', 'the value after Reset');
      expect(await page.$$eval(`${calRoot} .is-selected`, els => els.length), 0, 'no day is marked after Reset');
    });

    await check('the date picker takes the same limits (a distance from today) and the same jump to a month and a year', async () => {
      await page.evaluate(() => {
        const box = document.getElementById('e2e-box');
        box.innerHTML = '<div class="mat-field"><label class="mat-label" for="e2e-dp">Delivery</label><div class="mat-picker" data-picker="date" data-min="+3d" data-max="+400d"><input class="mat-picker__input" id="e2e-dp" name="dp" /></div></div>';
        window.MatUi.refresh(box);
      });
      await page.click('#e2e-dp-trigger');
      const panel = '#e2e-dp-trigger ~ .mat-picker__panel';
      const open = (await page.$$eval(`${panel} .mat-cal__day`, els => els.filter(b => !b.disabled).map(b => b.getAttribute('data-date'))));
      expectTrue(open.length === 0 || open[0] >= calIso(calPlus(3)), 'the first day that can be chosen is not before three days from now: ' + open[0]);
      expect(await page.locator(`${panel} [data-cal-level="months"]`).count(), 1, 'the title is a button');
      await page.click(`${panel} [data-cal-level="months"]`);
      await page.click(`${panel} [data-cal-level="years"]`);
      const target = calPlus(300);
      for (let i = 0; i < 3 && (await page.locator(`${panel} [data-cal-year="${target.getFullYear()}"]`).count()) === 0; i += 1) { await page.click(`${panel} [data-cal-nav="1"]`); }
      await page.click(`${panel} [data-cal-year="${target.getFullYear()}"]`);
      await page.click(`${panel} [data-cal-month="${target.getMonth()}"]`);
      const iso = calIso(target);
      await page.click(`${panel} [data-date="${calIso(new Date(target.getFullYear(), target.getMonth(), 15))}"]`);
      expect(await page.$eval('#e2e-dp', el => el.value), calIso(new Date(target.getFullYear(), target.getMonth(), 15)), 'the posted value after a jump (' + iso + ')');
      expectTrue(!(await page.locator(panel).isVisible()), 'the panel is closed');
    });

    await check('the examples of the page are alive: the events example shows its agenda and the rental example posts a period', async () => {
      await page.evaluate(() => document.getElementById('cal-events').closest('.mat-field').scrollIntoView({ block: 'center' }));
      expect(await page.locator('#cal-events').evaluate(el => el.closest('.mat-calendar').querySelectorAll('.mat-calendar__event').length), 3, 'the events of the 14th');
      expect(await page.locator('#cal-days').evaluate(el => el.closest('.mat-calendar').querySelector('.mat-picker__status').textContent.trim()), '2 days chosen', 'the days off');
    });

    await check('a calendar names its days, has no second Tab stop inside the grid and puts the real input out of the way', async () => {
      await calPut(page, calField('e2e-c15', 'data-min="today"'));
      const tabStops = await page.$$eval(`${calRoot} .mat-cal__day`, els => els.filter(b => b.tabIndex === 0).length);
      expect(tabStops, 1, 'Tab stops');
      const input = await page.$eval('#e2e-c15', el => { const s = getComputedStyle(el); return { opacity: s.opacity, pointer: s.pointerEvents, hidden: el.getAttribute('aria-hidden'), tab: el.tabIndex }; });
      expect(input, { opacity: '0', pointer: 'none', hidden: 'true', tab: -1 }, 'the real input');
      await page.focus('#e2e-c15');
      expectTrue(await page.evaluate(() => !!document.activeElement.getAttribute('data-date')), 'a label that focuses the input lands on a day');
    });

    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#calendar');
    const calRoot = '#e2e-box .mat-calendar';
    await check('on a phone a calendar fits the screen, its days are 44 px high, two months are one above the other and a click works with a touch', async () => {
      await calPut(page, calField('e2e-p1', 'data-mode="range" data-months="2"') + calField('e2e-p2', 'data-agenda data-week-numbers'));
      const sizes = await page.evaluate(() => {
        const cal = document.querySelector('#e2e-box .mat-field:first-child .mat-calendar').getBoundingClientRect();
        const day = document.querySelector('#e2e-box .mat-field:first-child .mat-cal__day').getBoundingClientRect();
        const months = [...document.querySelectorAll('#e2e-box .mat-field:first-child .mat-cal__month')].map(m => m.getBoundingClientRect());
        return { day: Math.round(day.height), within: cal.left >= 0 && cal.right <= innerWidth, stacked: months.length === 2 && months[1].top > months[0].bottom - 1, overflow: document.documentElement.scrollWidth > innerWidth };
      });
      expect(sizes, { day: 44, within: true, stacked: true, overflow: false }, 'the calendar on a phone');
      await calGo(page, 'e2e-p1', '2030-03-01');
      await page.tap('#e2e-box .mat-field:first-child [data-date="2030-03-12"]');
      await page.tap('#e2e-box .mat-field:first-child [data-date="2030-04-02"]');
      expect(await page.$eval('#e2e-p1', el => el.value), '2030-03-12..2030-04-02', 'a period over two months');
    });
    await check('on a phone the title leads to the months and the arrows are touch targets', async () => {
      const box = await page.$eval('#e2e-box .mat-field:last-child .mat-cal__head [data-cal-nav="1"]', el => { const r = el.getBoundingClientRect(); return { w: Math.round(r.width), h: Math.round(r.height) }; });
      expectTrue(box.w >= 36 && box.h >= 36, `the arrow is ${box.w} x ${box.h} px`);
      await page.tap('#e2e-box .mat-field:last-child [data-cal-level="months"]');
      expect(await page.locator('#e2e-box .mat-field:last-child [data-cal-month]').count(), 12, 'months');
      const cell = await page.$eval('#e2e-box .mat-field:last-child [data-cal-month="0"]', el => Math.round(el.getBoundingClientRect().height));
      expectTrue(cell >= 44, `a month is ${cell} px high`);
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- appointment picker (FORM-18)
  console.log('\nAppointment picker');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#slot-picker');
    // the availability of the checks: from tomorrow on, a free time in the morning and the afternoon, two taken ones, a day now and then fully booked or closed
    const slotData = (from, to) => {
      const data = {};
      for (let n = from; n <= to; n += 1) {
        const d = calPlus(n);
        if (n % 5 === 0) { continue; }   // closed
        data[calIso(d)] = n % 7 === 0
          ? { free: [], taken: ['09:00', '09:15'] }
          : { free: ['09:00', '09:15', '09:30', '10:00', '14:00', '14:15', '17:30'], taken: ['09:45', '14:30'] };
      }
      return data;
    };
    const slotPut = (attrs, data, id = 'e2e-s1', extra = '') => calPut(page, `<div class="mat-field"><label class="mat-label" for="${id}">Appointment</label><div class="mat-picker" data-picker="slot" ${data === null ? '' : `data-slots='${JSON.stringify(data)}'`} ${attrs}><input class="mat-picker__input" id="${id}" name="${id}" ${extra} /></div></div>`);
    const panel = (id = 'e2e-s1') => `#${id}-trigger ~ .mat-picker__panel`;
    const shown = async (id = 'e2e-s1') => (await page.textContent(`#${id}-trigger .mat-picker__value`)).replace(/\s+/g, ' ').trim();
    const valueOf = (id = 'e2e-s1') => page.$eval(`#${id}`, el => el.value);
    const monthDays = async (id = 'e2e-s1') => calDays(page, panel(id));
    const fullDay = d => new Intl.DateTimeFormat('en-GB', { dateStyle: 'full' }).format(d);
    // shows the month of a day in the open panel (forwards or backwards, whatever is needed)
    const gotoDay = async (id, d) => {
      for (let i = 0; i < 14 && (await page.locator(`${panel(id)} [data-date="${calIso(d)}"]`).count()) === 0; i += 1) {
        const shownMonth = (await page.$eval(`${panel(id)} .mat-cal__day`, el => el.getAttribute('data-date'))).slice(0, 7);
        await page.click(`${panel(id)} [data-cal-nav="${calIso(d).slice(0, 7) > shownMonth ? 1 : -1}"]`);
      }
    };
    const dayOf = async (id, n) => { const d = calPlus(n); await gotoDay(id, d); return d; };

    await check('the appointment picker shows a month: days with a free time are enabled and have a dot, the others are disabled and struck through, the past is not offered', async () => {
      const data = slotData(1, 60);
      await slotPut('data-min="today" data-duration="15"', data);
      expect(await shown(), 'Choose an appointment', 'the placeholder');
      await page.click('#e2e-s1-trigger');
      expectTrue(await page.locator(panel()).isVisible(), 'the panel is not shown');
      const wrong = [];
      const firstMonth = await monthDays();
      expectTrue(/free appointments?/.test(firstMonth.find(d => d.marks === 1).label), 'the name of a free day says it: ' + firstMonth.find(d => d.marks === 1).label);
      expectTrue(/no free appointment/.test(firstMonth.find(d => d.off && /is-unavailable/.test(d.cls)).label), 'the name of a day without one says it');
      for (let step = 0; step < 3; step += 1) {
        (await monthDays()).forEach(d => {
          const free = !!data[d.date] && data[d.date].free.length > 0;
          const past = calDate(d.date) < calPlus(0);
          if (d.off !== !free) { wrong.push(`${d.date} off=${d.off}`); }
          if ((d.marks === 1) !== free) { wrong.push(`${d.date} dot=${d.marks}`); }
          if (!past && !free && !/is-unavailable/.test(d.cls)) { wrong.push(`${d.date} not struck`); }
          if (past && /is-unavailable/.test(d.cls)) { wrong.push(`${d.date} (past) struck`); }
        });
        await page.click(`${panel()} [data-cal-nav="1"]`);
      }
      expect(wrong, [], 'days in the wrong state');
      await page.keyboard.press('Escape');
    });

    await check('a day shows its times: the free ones are buttons, the taken ones are disabled and say "Taken", in groups, with the length of an appointment', async () => {
      const data = slotData(1, 60);
      await slotPut('data-min="today" data-duration="15" data-group', data);
      await page.click('#e2e-s1-trigger');
      const d = await dayOf('e2e-s1', 1);
      const iso = calIso(d);
      await page.click(`${panel()} [data-date="${iso}"]`);
      const heading = (await page.textContent(`${panel()} .mat-slots__heading`)).trim();
      expect(heading, fullDay(d), 'the heading of the times');
      expect((await page.textContent(`${panel()} .mat-slots__hint`)).trim(), 'Appointments of 15 min', 'the length');
      expect(await page.$$eval(`${panel()} .mat-slots__group-title`, els => els.map(e => e.textContent.trim())), ['Morning', 'Afternoon', 'Evening'], 'the groups');
      const slots = await page.$$eval(`${panel()} .mat-slot`, els => els.map(b => ({ t: b.getAttribute('data-time'), off: b.disabled, note: (b.querySelector('.mat-slot__note') || {}).textContent || '', text: b.textContent.replace(/\s+/g, ' ').trim() })));
      const expected = data[iso];
      expect(slots.filter(s => !s.off).map(s => s.t), expected.free, 'the free times');
      expect(slots.filter(s => s.off).map(s => s.t), expected.taken, 'the taken times');
      expectTrue(slots.filter(s => s.off).every(s => s.note === 'Taken' && s.text.includes('Taken')), 'a taken time says Taken in words');
      expect(slots.map(s => s.t), [...expected.free, ...expected.taken].sort(), 'in the order of the time');
      const decoration = await page.$eval(`${panel()} .mat-slot:disabled .mat-slot__time`, el => getComputedStyle(el).textDecorationLine);
      expect(decoration, 'line-through', 'a taken time is struck through besides');
      const afternoon = await page.$$eval(`${panel()} .mat-slots__group`, gs => gs.map(g => g.querySelector('.mat-slots__group-title').textContent.trim() + ':' + [...g.querySelectorAll('.mat-slot')].map(b => b.getAttribute('data-time')).join(',')));
      expect(afternoon, ['Morning:09:00,09:15,09:30,09:45,10:00', 'Afternoon:14:00,14:15,14:30', 'Evening:17:30'], 'which time is in which group');
      await page.keyboard.press('Escape');
    });

    await check('a taken time cannot be chosen, Apply waits for a free one, and the choice is posted as one value with its end', async () => {
      const data = slotData(1, 60);
      await slotPut('data-min="today" data-duration="15"', data);
      await calLog(page, 'e2e-s1');
      await page.click('#e2e-s1-trigger');
      const d = await dayOf('e2e-s1', 1);
      await page.click(`${panel()} [data-date="${calIso(d)}"]`);
      expect(await page.locator(`${panel()} [data-picker-action="apply"]`).isDisabled(), true, 'Apply before a choice');
      expect((await page.textContent(`${panel()} .mat-slots__summary`)).trim(), 'Choose a day and a time', 'the line before a choice');
      await page.locator(`${panel()} .mat-slot[data-time="09:45"]`).click({ force: true, timeout: 1000 }).catch(() => { /* a disabled button cannot be pressed: that is the point */ });
      expect(await page.locator(`${panel()} .mat-slot[aria-pressed="true"]`).count(), 0, 'a taken time is not chosen');
      expect(await page.locator(`${panel()} [data-picker-action="apply"]`).isDisabled(), true, 'Apply after a click on a taken time');
      await page.click(`${panel()} .mat-slot[data-time="09:30"]`);
      expect(await page.getAttribute(`${panel()} .mat-slot[data-time="09:30"]`, 'aria-pressed'), 'true', 'the chosen time');
      expect((await page.textContent(`${panel()} .mat-slots__summary`)).trim(), `${fullDay(d)} · 09:30 – 09:45`, 'the line above the buttons');
      expect(await valueOf(), '', 'nothing is posted before Apply');
      await page.click(`${panel()} [data-picker-action="apply"]`);
      expect(await valueOf(), `${calIso(d)}T09:30..09:45`, 'the posted value');
      expect(await calEvents(page), [`input:${calIso(d)}T09:30..09:45`, `change:${calIso(d)}T09:30..09:45`], 'input and change');
      expectTrue(!(await page.locator(panel()).isVisible()), 'the panel closed');
      expectTrue((await shown()).endsWith('09:30 – 09:45') && /\d{4}/.test(await shown()), 'the field shows the choice: ' + await shown());
      expect(await page.evaluate(() => document.activeElement.id), 'e2e-s1-trigger', 'the focus is back on the field');
      // reopened: the choice is there
      await page.click('#e2e-s1-trigger');
      expect(await page.getAttribute(`${panel()} .mat-slot[data-time="09:30"]`, 'aria-pressed'), 'true', 'the chosen time when it is opened again');
      expect(await page.getAttribute(`${panel()} [data-date="${calIso(d)}"]`, 'aria-selected'), 'true', 'the chosen day');
      await page.keyboard.press('Escape');
    });

    await check('without data-duration only the start is posted, and Cancel and Escape keep the old value', async () => {
      const data = slotData(1, 60);
      await slotPut('data-min="today" data-clearable', data, 'e2e-s2', `value="${calIso(calPlus(2))}T10:00"`);
      expectTrue((await shown('e2e-s2')).endsWith('10:00'), 'the value that was there is shown: ' + await shown('e2e-s2'));
      await page.click('#e2e-s2-trigger');
      expect(await page.locator(`${panel('e2e-s2')} .mat-slots__hint`).count(), 0, 'no length without data-duration');
      await page.click(`${panel('e2e-s2')} .mat-slot[data-time="14:15"]`);
      await page.click(`${panel('e2e-s2')} [data-picker-action="cancel"]`);
      expect(await valueOf('e2e-s2'), `${calIso(calPlus(2))}T10:00`, 'Cancel');
      await page.click('#e2e-s2-trigger');
      await page.click(`${panel('e2e-s2')} .mat-slot[data-time="14:15"]`);
      await page.keyboard.press('Escape');
      expect(await valueOf('e2e-s2'), `${calIso(calPlus(2))}T10:00`, 'Escape');
      await page.click('#e2e-s2-trigger');
      await page.click(`${panel('e2e-s2')} .mat-slot[data-time="14:15"]`);
      await page.click(`${panel('e2e-s2')} [data-picker-action="apply"]`);
      expect(await valueOf('e2e-s2'), `${calIso(calPlus(2))}T14:15`, 'the start only');
      await page.click('#e2e-s2-trigger ~ .mat-picker__clear');
      expect(await valueOf('e2e-s2'), '', 'Clear');
      expect(await shown('e2e-s2'), 'Choose an appointment', 'the placeholder after Clear');
    });

    await check('a click outside closes the panel without a choice', async () => {
      const data = slotData(1, 60);
      await slotPut('data-min="today"', data);
      await page.click('#e2e-s1-trigger');
      expectTrue(await page.locator(panel()).isVisible(), 'not open');
      await page.mouse.click(5, 450);
      expectTrue(!(await page.locator(panel()).isVisible()), 'a click outside did not close the panel');
      expect(await valueOf(), '', 'no value');
    });

    await check('data-min="+3d" and data-max take days away, and data-unavailable="hide" makes days and taken times absent', async () => {
      const data = slotData(1, 60);
      await slotPut('data-min="+3d" data-max="+40d" data-unavailable="hide"', data);
      await page.click('#e2e-s1-trigger');
      const lastOpen = calPlus(40), firstOpen = calPlus(3);
      const wrong = [];
      for (let step = 0; step < 3; step += 1) {
        for (const d of await monthDays()) {
          const x = calDate(d.date), free = !!data[d.date] && data[d.date].free.length > 0, within = x >= firstOpen && x <= lastOpen;
          if (d.off !== !(free && within)) { wrong.push(`${d.date} off=${d.off}`); }
          if (d.off && !/is-absent/.test(d.cls)) { wrong.push(`${d.date} is drawn`); }
          if (!d.off && /is-absent/.test(d.cls)) { wrong.push(`${d.date} is absent`); }
        }
        if (await page.locator(`${panel()} [data-cal-nav="1"]`).isDisabled()) { break; }
        await page.click(`${panel()} [data-cal-nav="1"]`);
      }
      expect(wrong, [], 'days in the wrong state');
      const gone = await page.$eval(`${panel()} .mat-cal__day.is-absent`, el => ({ visibility: getComputedStyle(el).visibility, hidden: el.getAttribute('aria-hidden') }));
      expect(gone, { visibility: 'hidden', hidden: 'true' }, 'an absent day');
      const d = await dayOf('e2e-s1', 6);
      await page.click(`${panel()} [data-date="${calIso(d)}"]`).catch(async () => { await page.click(`${panel()} .mat-cal__day:not(:disabled)`); });
      expect(await page.locator(`${panel()} .mat-slot:disabled`).count(), 0, 'taken times are not listed');
      await page.keyboard.press('Escape');
    });

    await check('a time that starts before now plus data-min-notice is never offered, and Next free appointment finds the first one that is', async () => {
      // a time every half hour for today and the next two days
      const data = {};
      for (let n = 0; n <= 2; n += 1) {
        data[calIso(calPlus(n))] = { free: Array.from({ length: 48 }, (_, i) => `${calPad(Math.floor(i / 2))}:${i % 2 ? '30' : '00'}`) };
      }
      await slotPut('data-min="today" data-min-notice="2h"', data);
      const first = (now) => {
        const earliest = now + 2 * 3600000;
        for (let n = 0; n <= 2; n += 1) {
          for (let i = 0; i < 48; i += 1) {
            const d = calPlus(n); d.setHours(Math.floor(i / 2), i % 2 ? 30 : 0);
            if (d.getTime() >= earliest) { return `${calIso(d)}T${calPad(d.getHours())}:${calPad(d.getMinutes())}`; }
          }
        }
        return null;
      };
      const before = first(Date.now());
      await page.click('#e2e-s1-trigger');
      await page.click(`${panel()} [data-slots-next]`);
      const after = first(Date.now());
      const chosen = await page.evaluate(sel => { const day = document.querySelector(sel + ' .mat-cal__day[aria-selected="true"]'); const slot = document.querySelector(sel + ' .mat-slot[aria-pressed="true"]'); return day && slot ? day.getAttribute('data-date') + 'T' + slot.getAttribute('data-time') : null; }, panel());
      expectTrue(chosen === before || chosen === after, `the first free time is ${before}, the picker chose ${chosen}`);
      expect(await page.evaluate(() => document.activeElement.classList.contains('mat-slot')), true, 'the focus is on the chosen time');
      // the times of the day that was chosen: none before the notice (for today: before now plus two hours)
      const day = chosen.slice(0, 10);
      const listed = await page.$$eval(`${panel()} .mat-slot`, els => els.map(b => b.getAttribute('data-time')));
      const earliestMs = Date.now() + 2 * 3600000;
      expectTrue(listed.every(t => { const d = calDate(day); d.setHours(+t.slice(0, 2), +t.slice(3)); return d.getTime() >= earliestMs - 5000; }), 'a time before the notice is listed: ' + listed.join(','));
      if (day === calIso(calPlus(0))) { expectTrue(listed.length < 48, 'today shows fewer times than a whole day'); }
      await page.click(`${panel()} [data-picker-action="apply"]`);
      expect(await valueOf(), chosen, 'Apply books it');
    });

    await check('Next free appointment goes to a later month and says so when there is nothing', async () => {
      const far = calPlus(75);
      await slotPut('data-min="today"', { [calIso(far)]: ['11:15', '11:30'] });
      await page.click('#e2e-s1-trigger');
      await page.click(`${panel()} [data-slots-next]`);
      expect(await page.$$eval(`${panel()} .mat-cal__day[aria-selected="true"]`, els => els.map(e => e.getAttribute('data-date'))), [calIso(far)], 'the day in a later month');
      expect(await page.$$eval(`${panel()} .mat-slot[aria-pressed="true"]`, els => els.map(e => e.getAttribute('data-time'))), ['11:15'], 'the first time of it');
      expect((await page.textContent(`${panel()} .mat-picker__title, ${panel()} .mat-cal__title`)).trim(), new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric' }).format(far), 'the month is shown');
      await page.keyboard.press('Escape');
      await slotPut('data-min="today"', {});
      await page.click('#e2e-s1-trigger');
      await page.click(`${panel()} [data-slots-next]`);
      expect((await page.textContent(`${panel()} .mat-slots__status`)).trim(), 'No free appointment found', 'the message');
      expect(await page.locator(`${panel()} [data-picker-action="apply"]`).isDisabled(), true, 'nothing to apply');
      await page.keyboard.press('Escape');
    });

    await check('the keyboard: arrows walk the days over the ones that are not free, Enter moves to the times, arrows walk them like a grid, Space chooses, Escape closes', async () => {
      const data = slotData(1, 60);
      await slotPut('data-min="today" data-duration="15"', data);
      await page.focus('#e2e-s1-trigger');
      await page.keyboard.press('Enter');
      expectTrue(await page.locator(panel()).isVisible(), 'Enter did not open the panel');
      expectTrue(await page.evaluate(() => !!document.activeElement.getAttribute('data-date')), 'the focus is on a day');
      const d = await dayOf('e2e-s1', 1);
      await page.evaluate(i => document.querySelector(`#e2e-s1-trigger ~ .mat-picker__panel [data-date="${i}"]`).focus(), calIso(d));
      // the next day that is free, found the slow way
      const freeAfter = n => { for (let k = n + 1; k < 60; k += 1) { const e = data[calIso(calPlus(k))]; if (e && e.free.length) { return calPlus(k); } } return null; };
      const want = freeAfter(1);
      await page.keyboard.press('ArrowRight');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-date')), calIso(want), 'ArrowRight lands on the next day with a free time');
      await page.keyboard.press('Enter');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-time')), '09:00', 'Enter on a day moves the focus to its first free time');
      expect((await page.$$eval(`${panel()} .mat-slot`, els => els.length)) > 5, true, 'the times are listed');
      await page.keyboard.press('ArrowRight');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-time')), '09:15', 'ArrowRight: the next free time');
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-time')), '10:00', 'the taken time 09:45 is skipped');
      await page.keyboard.press('ArrowDown');
      const at = await page.evaluate(() => document.activeElement.getAttribute('data-time'));
      expectTrue(at > '10:00', 'ArrowDown goes to a later row: ' + at);
      await page.keyboard.press('ArrowUp');
      const back = await page.evaluate(() => document.activeElement.getAttribute('data-time'));
      expectTrue(back < at && back >= '10:00', `ArrowUp goes to the row above (${at} to ${back})`);
      await page.evaluate(() => document.querySelector('#e2e-s1-trigger ~ .mat-picker__panel .mat-slot[data-time="10:00"]').focus());
      await page.keyboard.press('Home');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-time')), '09:00', 'Home');
      await page.keyboard.press('End');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-time')), '17:30', 'End');
      await page.keyboard.press('Space');
      expect(await page.getAttribute(`${panel()} .mat-slot[data-time="17:30"]`, 'aria-pressed'), 'true', 'Space chooses a time');
      await page.keyboard.press('Escape');
      expectTrue(!(await page.locator(panel()).isVisible()), 'Escape did not close the panel');
      expect(await valueOf(), '', 'Escape does not choose');
      expect(await page.evaluate(() => document.activeElement.id), 'e2e-s1-trigger', 'the focus is back on the field');
    });

    await check('the availability can be asked for per month: mat:slots-needed carries the month and the picker, the panel says it is loading, setSlots answers and the days appear', async () => {
      await slotPut('data-min="today" data-duration="30"', null);
      await page.evaluate(() => {
        window.__asked = [];
        document.querySelector('#e2e-s1').closest('.mat-picker').addEventListener('mat:slots-needed', e => { e.stopPropagation(); window.__asked.push({ month: e.detail.month, from: e.detail.from, to: e.detail.to, own: e.detail.picker === document.querySelector('#e2e-s1').closest('.mat-picker') }); });
      });
      await page.click('#e2e-s1-trigger');
      const month = calIso(calPlus(0)).slice(0, 7);
      const lastDay = calPad(new Date(calPlus(0).getFullYear(), calPlus(0).getMonth() + 1, 0).getDate());
      expect(await page.evaluate(() => window.__asked), [{ month, from: month + '-01', to: `${month}-${lastDay}`, own: true }], 'the question');
      expect((await page.textContent(`${panel()} .mat-slots__status`)).trim(), 'Loading the free appointments …', 'the loading state');
      expect(await page.getAttribute(`${panel()} .mat-slots`, 'aria-busy'), 'true', 'aria-busy');
      expect((await monthDays()).every(d => d.off), true, 'no day can be chosen while it loads');
      expect((await monthDays()).every(d => !/is-unavailable/.test(d.cls)), true, 'and none is struck through before it is known');
      expect(await page.locator(`${panel()} [data-slots-next]`).count(), 1, 'the shortcut is there');
      // the answer: three days of this month (or, at the end of the month, the days that are left)
      const days = [];
      for (let n = 1; n <= 6 && days.length < 3; n += 1) { if (calIso(calPlus(n)).slice(0, 7) === month) { days.push(calIso(calPlus(n))); } }
      await page.evaluate(([m, d]) => window.MatUi.picker.setSlots(document.querySelector('#e2e-s1').closest('.mat-picker'), Object.fromEntries(d.map(x => [x, ['09:00', '09:30']])), m), [month, days]);
      expect(await page.evaluate(() => window.__asked.length), 1, 'the month is asked for once');
      expect((await page.textContent(`${panel()} .mat-slots__status`)).trim(), '', 'the loading state is gone');
      expect(await page.getAttribute(`${panel()} .mat-slots`, 'aria-busy'), 'false', 'aria-busy after the answer');
      expect((await monthDays()).filter(d => !d.off).map(d => d.date), days, 'the days with a free time');
      // the next month is asked for when it is shown, and an empty answer for it ends the loading as well
      await page.click(`${panel()} [data-cal-nav="1"]`);
      const next = calIso(new Date(calPlus(0).getFullYear(), calPlus(0).getMonth() + 1, 1)).slice(0, 7);
      expect((await page.evaluate(() => window.__asked)).map(a => a.month), [month, next], 'the second question');
      await page.evaluate(m => window.MatUi.picker.setSlots(document.querySelector('#e2e-s1').closest('.mat-picker'), {}, m), next);
      expect((await page.textContent(`${panel()} .mat-slots__status`)).trim(), '', 'an empty answer ends the loading');
      expect((await monthDays()).every(d => d.off), true, 'a month without a free time has no day to choose');
      await page.click(`${panel()} [data-cal-nav="-1"]`);
      expect(await page.evaluate(() => window.__asked.length), 2, 'a month that is known is not asked for again');
      expect((await monthDays()).filter(d => !d.off).map(d => d.date), days, 'the days of the first month are still there');
      await page.keyboard.press('Escape');
    });

    await check('when the answer is a failure the panel says so and Try again asks for the month once more', async () => {
      await slotPut('data-min="today"', null, 'e2e-s3');
      await page.evaluate(() => {
        window.__asked = [];
        document.querySelector('#e2e-s3').closest('.mat-picker').addEventListener('mat:slots-needed', e => { e.stopPropagation(); window.__asked.push(e.detail.month); });
      });
      await page.click('#e2e-s3-trigger');
      const month = calIso(calPlus(0)).slice(0, 7);
      await page.evaluate(m => window.MatUi.picker.slotsFailed(document.querySelector('#e2e-s3').closest('.mat-picker'), m), month);
      expectTrue((await page.textContent(`${panel('e2e-s3')} .mat-slots__status`)).includes('The appointments could not be loaded.'), 'the message');
      await page.click(`${panel('e2e-s3')} [data-slots-retry]`);
      expect(await page.evaluate(() => window.__asked), [month, month], 'the month is asked for again');
      expectTrue((await page.textContent(`${panel('e2e-s3')} .mat-slots__status`)).includes('Loading'), 'loading again');
      await page.evaluate(m => window.MatUi.picker.setSlots(document.querySelector('#e2e-s3').closest('.mat-picker'), { [m + '-28']: ['08:00'] }, m), month);
      expect((await page.textContent(`${panel('e2e-s3')} .mat-slots__status`)).trim(), '', 'the message is gone after an answer');
      await page.keyboard.press('Escape');
    });

    await check('Next free appointment asks for the following months one after the other when the data is lazy', async () => {
      await slotPut('data-min="today"', null, 'e2e-s4');
      const target = calPlus(70);
      await page.evaluate(([iso]) => {
        window.__asked = [];
        const wrap = document.querySelector('#e2e-s4').closest('.mat-picker');
        wrap.addEventListener('mat:slots-needed', e => {
          e.stopPropagation();
          window.__asked.push(e.detail.month);
          setTimeout(() => window.MatUi.picker.setSlots(wrap, e.detail.month === iso.slice(0, 7) ? { [iso]: ['08:45', '09:00'] } : {}, e.detail.month), 20);
        });
      }, [calIso(target)]);
      await page.click('#e2e-s4-trigger');
      await page.click(`${panel('e2e-s4')} [data-slots-next]`);
      await page.waitForFunction(sel => !!document.querySelector(sel + ' .mat-slot[aria-pressed="true"]'), panel('e2e-s4'), { timeout: 5000 });
      expect(await page.$$eval(`${panel('e2e-s4')} .mat-cal__day[aria-selected="true"]`, els => els.map(e => e.getAttribute('data-date'))), [calIso(target)], 'the day');
      const asked = await page.evaluate(() => window.__asked);
      expectTrue(asked[0] === calIso(calPlus(0)).slice(0, 7) && asked.at(-1) === calIso(target).slice(0, 7) && new Set(asked).size === asked.length, 'the months are asked for in order, each once: ' + asked.join(' '));
      await page.click(`${panel('e2e-s4')} [data-picker-action="apply"]`);
      expect(await valueOf('e2e-s4'), `${calIso(target)}T08:45`, 'Apply');
    });

    await check('the examples of the page ask the script of the site, which answers: days appear, Next free appointment chooses a time on a weekday and the value has the end', async () => {
      await page.evaluate(() => document.getElementById('pk-slot-trigger').scrollIntoView({ block: 'center' }));
      await page.click('#pk-slot-trigger');
      await page.waitForFunction(() => document.querySelector('#pk-slot-trigger ~ .mat-picker__panel .mat-slots').getAttribute('aria-busy') === 'false' && !!document.querySelector('#pk-slot-trigger ~ .mat-picker__panel .mat-cal__day:not(:disabled)'), null, { timeout: 5000 });
      await page.click('#pk-slot-trigger ~ .mat-picker__panel [data-slots-next]');
      const p = '#pk-slot-trigger ~ .mat-picker__panel';
      const day = await page.getAttribute(`${p} .mat-cal__day[aria-selected="true"]`, 'data-date');
      expectTrue(![0, 6].includes(calDate(day).getDay()), 'the practice is closed at weekends: ' + day);
      await page.click(`${p} [data-picker-action="apply"]`);
      expectTrue(/^\d{4}-\d\d-\d\dT\d\d:\d\d\.\.\d\d:\d\d$/.test(await valueOf('pk-slot')), 'the value: ' + await valueOf('pk-slot'));
      await page.click('#pk-slot-trigger ~ .mat-picker__clear');
      expect(await valueOf('pk-slot'), '', 'Clear');
    });

    await check('a required appointment writes its message at the field and the focus goes to its button', async () => {
      await page.evaluate(() => document.getElementById('pk-slot-req-trigger').scrollIntoView({ block: 'center' }));
      await page.click('form[action="/Components#slot-picker"] button[type=submit]');
      await page.waitForTimeout(200);
      expect(await page.locator('#pk-slot-req').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field is marked invalid');
      expect((await page.locator('#pk-slot-req').evaluate(el => el.closest('.mat-field').querySelector('.mat-error').textContent)).trim(), 'Please choose an appointment.', 'the message');
      expect(await page.evaluate(() => document.activeElement.id), 'pk-slot-req-trigger', 'the focus');
    });

    await check('a free appointment can be chosen with a data-slots that has the times as objects too and a duration over an hour', async () => {
      await slotPut('data-min="today" data-duration="90"', { [calIso(calPlus(2))]: { free: [{ time: '09:00' }, '23:00'], taken: [{ time: '09:30' }] } }, 'e2e-s5');
      await page.click('#e2e-s5-trigger');
      const d = await dayOf('e2e-s5', 2);
      await page.click(`${panel('e2e-s5')} [data-date="${calIso(d)}"]`);
      expect((await page.textContent(`${panel('e2e-s5')} .mat-slots__hint`)).trim(), 'Appointments of 1 h 30 min', 'the length of 90 minutes');
      await page.click(`${panel('e2e-s5')} .mat-slot[data-time="23:00"]`);
      await page.click(`${panel('e2e-s5')} [data-picker-action="apply"]`);
      expect(await valueOf('e2e-s5'), `${calIso(d)}T23:00..00:30`, 'an end after midnight is the next day, as in a time range');
    });

    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#slot-picker');
    const days = n => { const d = new Date(); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + n); return d; };
    const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    await check('on a phone the appointment picker is a full-screen sheet with an X, the month above the times, 44 px targets, and the page behind does not scroll', async () => {
      const data = {};
      for (let n = 1; n <= 40; n += 1) { data[iso(days(n))] = { free: ['09:00', '09:15', '09:30', '10:00', '14:00', '14:15', '17:30'], taken: ['09:45'] }; }
      await page.evaluate(d => {
        const box = document.createElement('div');
        box.id = 'e2e-box';
        document.querySelector('.mat-main').appendChild(box);
        box.innerHTML = `<div class="mat-field"><label class="mat-label" for="e2e-ps">Appointment</label><div class="mat-picker" data-picker="slot" data-min="today" data-duration="15" data-group data-slots='${JSON.stringify(d)}'><input class="mat-picker__input" id="e2e-ps" name="ps" /></div></div>`;
        window.MatUi.refresh(box);
        box.scrollIntoView({ block: 'center' });
      }, data);
      await page.tap('#e2e-ps-trigger');
      const p = '#e2e-ps-trigger ~ .mat-picker__panel';
      const state = await page.$eval(p, el => { const r = el.getBoundingClientRect(); return { width: Math.round(r.width) === innerWidth, height: Math.round(r.height) === innerHeight, x: getComputedStyle(el.querySelector('.mat-picker__head')).display !== 'none' }; });
      expect(state, { width: true, height: true, x: true }, 'the sheet');
      expect(await page.evaluate(() => document.documentElement.classList.contains('mat-lock')), true, 'the page behind does not scroll');
      const layout = await page.evaluate(sel => {
        const cal = document.querySelector(sel + ' .mat-slots__cal').getBoundingClientRect(), day = document.querySelector(sel + ' .mat-slots__day').getBoundingClientRect();
        const first = [...document.querySelectorAll(sel + ' .mat-cal__day:not(:disabled)')][0].getBoundingClientRect();
        return { above: day.top >= cal.bottom - 1, sameWidth: Math.abs(day.width - cal.width) < 2, dayHeight: Math.round(first.height), overflow: document.documentElement.scrollWidth > innerWidth };
      }, p);
      expect(layout, { above: true, sameWidth: true, dayHeight: 44, overflow: false }, 'the layout of the sheet');
      await page.tap(`${p} .mat-cal__day:not(:disabled) >> nth=1`);
      await page.waitForTimeout(100);
      const sizes = await page.$$eval(`${p} .mat-slot`, els => els.map(b => Math.round(b.getBoundingClientRect().height)));
      expectTrue(sizes.length > 6 && sizes.every(h => h >= 44), 'the times are at least 44 px high: ' + sizes.join(','));
      const view = await page.evaluate(sel => { const body = document.querySelector(sel + ' .mat-picker__body'), apply = document.querySelector(sel + ' [data-picker-action="apply"]').getBoundingClientRect(), heading = document.querySelector(sel + ' .mat-slots__heading').getBoundingClientRect(); return { applyVisible: apply.bottom <= innerHeight && apply.top >= 0, headingVisible: heading.top >= 0 && heading.bottom <= innerHeight, scrolled: body.scrollTop > 0 }; }, p);
      expect(view, { applyVisible: true, headingVisible: true, scrolled: true }, 'after a day the times are brought into view and the buttons stay in reach');
      await page.tap(`${p} .mat-slot:not(:disabled) >> nth=2`);
      expectTrue(!(await page.locator(`${p} [data-picker-action="apply"]`).isDisabled()), 'Apply after a choice');
      await page.tap(`${p} [data-picker-close]`);
      expectTrue(!(await page.locator(p).isVisible()), 'the X did not close the sheet');
      expect(await page.$eval('#e2e-ps', el => el.value), '', 'the X does not choose');
      expect(await page.evaluate(() => document.documentElement.classList.contains('mat-lock')), false, 'the page is free again');
    });
    await ctx.close();
  }
  {
    // the language of the page: the texts and the names of the months come in German when the page says lang="de"
    const ctx = await browser.newContext({ ...desktopOptions, locale: 'de-DE' });
    const page = await ctx.newPage();
    page.on('pageerror', e => consoleErrors.push('/Components (de): ' + e.message));
    await page.route('**/Components', async route => {
      const response = await route.fetch();
      await route.fulfill({ response, body: (await response.text()).replace('<html lang="en"', '<html lang="de"') });
    });
    await page.goto(BASE + '/Components', { waitUntil: 'networkidle' });
    await check('in German the calendar and the appointment picker speak German: days, months, buttons, placeholder, Taken', async () => {
      await calPut(page, calField('e2e-de1', 'data-min="today" data-clearable data-week-numbers') + `<div class="mat-field"><label class="mat-label" for="e2e-de2">Termin</label><div class="mat-picker" data-picker="slot" data-min="today" data-group data-slots='${JSON.stringify({ [calIso(calPlus(1))]: { free: ['09:00', '14:00'], taken: ['09:15'] } })}'><input class="mat-picker__input" id="e2e-de2" name="de2" /></div></div>`);
      const root = '#e2e-box .mat-calendar';
      const dow = d => new Intl.DateTimeFormat('de-DE', { weekday: 'short' }).format(new Date(2024, 0, d));   // 1 January 2024 is a Monday
      expect((await page.$$eval(`${root} .mat-cal__dow`, els => els.map(e => e.textContent.trim()))).slice(0, 3), ['KW', dow(1), dow(2)], 'the week days');
      expect(await calTitle(page, root), new Intl.DateTimeFormat('de-DE', { month: 'long', year: 'numeric' }).format(calPlus(0)), 'the month in German');
      expect((await page.textContent(`${root} [data-cal-today]`)).trim(), 'Heute', 'Today');
      expect((await page.textContent(`${root} [data-cal-clear]`)).trim(), 'Löschen', 'Clear');
      expect(await page.getAttribute(`${root} [data-cal-nav="-1"]`, 'aria-label'), 'Vorheriger Monat', 'the arrow');
      await page.click(`${root} [data-cal-level="months"]`);
      expect(await page.getAttribute(`${root} [data-cal-nav="1"]`, 'aria-label'), 'Nächstes Jahr', 'the arrow of the years');
      expect((await page.textContent(`${root} [data-cal-month="2"]`)).trim().toLowerCase().startsWith('mär'), true, 'a month');
      expect((await page.textContent('#e2e-de2-trigger .mat-picker__value')).trim(), 'Termin wählen', 'the placeholder');
      await page.click('#e2e-de2-trigger');
      const p = '#e2e-de2-trigger ~ .mat-picker__panel';
      expect((await page.textContent(`${p} [data-slots-next]`)).trim(), 'Nächster freier Termin', 'the shortcut');
      expect((await page.textContent(`${p} .mat-slots__empty`)).trim(), 'Tag wählen, um die freien Termine zu sehen', 'the hint before a day');
      await page.click(`${p} [data-date="${calIso(calPlus(1))}"]`);
      expect((await page.textContent(`${p} .mat-slot:disabled .mat-slot__note`)).trim(), 'Belegt', 'a taken time');
      expect(await page.$$eval(`${p} .mat-slots__group-title`, els => els.map(e => e.textContent.trim())), ['Vormittag', 'Nachmittag'], 'the groups');
      await page.click(`${p} .mat-slot[data-time="14:00"]`);
      expect((await page.textContent(`${p} .mat-slots__summary`)).trim(), `${new Intl.DateTimeFormat('de-DE', { dateStyle: 'full' }).format(calPlus(1))} · 14:00`, 'the line above the buttons');
      await page.click(`${p} [data-picker-action="apply"]`);
      expect(await page.$eval('#e2e-de2', el => el.value), `${calIso(calPlus(1))}T14:00`, 'the posted value is the same in every language');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- carousel (LIST-13)
  console.log('\nCarousel');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#carousel');
    const tour = 'section[aria-label="A tour of the toolkit"]';
    const state = sel => page.$eval(sel, c => {
      const slides = [...c.querySelectorAll('.mat-carousel__slide')];
      const dots = [...c.querySelectorAll('.mat-carousel__dot')];
      return {
        slides: slides.length, dots: dots.length,
        inert: slides.map(s => s.hasAttribute('inert')).join(','),
        current: dots.findIndex(d => d.getAttribute('aria-current') === 'true'),
        prev: c.querySelector('.mat-carousel__prev').getAttribute('aria-disabled'),
        next: c.querySelector('.mat-carousel__next').getAttribute('aria-disabled'),
        live: c.querySelector('[role=status]').textContent.trim(),
        left: Math.round(c.querySelector('.mat-carousel__track').scrollLeft),
      };
    });
    await page.evaluate(() => document.getElementById('carousel').scrollIntoView());

    await check('a carousel names its slides, adds Previous, a dot per page and Next, and only the slide in view can be reached with the Tab key', async () => {
      const s = await state(tour);
      expect(s.slides, 4, 'slides');
      expect(s.dots, 4, 'dots');
      expect(s.inert, 'false,true,true,true', 'inert slides');
      expect(s.current, 0, 'current dot');
      expect(s.prev, 'true', 'Previous on the first page');
      expect(s.next, 'false', 'Next on the first page');
      expect(await page.getAttribute(`${tour} .mat-carousel__slide`, 'aria-label'), '1 of 4', 'the name of a slide');
      expect(await page.getAttribute(`${tour} .mat-carousel__slide`, 'aria-roledescription'), 'slide', 'the role of a slide');
      expect(await page.getAttribute(`${tour} .mat-carousel__dot >> nth=1`, 'aria-label'), 'Show 2', 'the name of a dot');
      expect(await page.getAttribute(`${tour} .mat-carousel__prev`, 'aria-label'), 'Previous', 'the name of Previous');
    });
    await check('Next, a dot and the keys move the strip, the current dot follows and a screen reader is told', async () => {
      await page.click(`${tour} .mat-carousel__next`);
      await page.waitForTimeout(700);
      let s = await state(tour);
      expect(s.current, 1, 'the dot after Next');
      expect(s.inert, 'true,false,true,true', 'inert slides after Next');
      expect(s.prev, 'false', 'Previous after Next');
      expect(s.live, 'Showing 2 of 4', 'the live region');
      await page.click(`${tour} .mat-carousel__dot >> nth=3`);
      await page.waitForTimeout(700);
      s = await state(tour);
      expect(s.current, 3, 'the last dot');
      expect(s.next, 'true', 'Next on the last page');
      expect(await page.evaluate(sel => document.activeElement === document.querySelector(sel + ' .mat-carousel__dot:nth-child(4)'), tour), true, 'the focus stays on the dot');
      await page.focus(`${tour} .mat-carousel__track`);
      await page.keyboard.press('Home');
      await page.waitForTimeout(700);
      expect((await state(tour)).current, 0, 'Home');
      await page.keyboard.press('ArrowRight');
      await page.waitForTimeout(700);
      expect((await state(tour)).current, 1, 'ArrowRight');
      await page.keyboard.press('ArrowLeft');
      await page.waitForTimeout(700);
      expect((await state(tour)).current, 0, 'ArrowLeft');
      await page.keyboard.press('End');
      await page.waitForTimeout(700);
      expect((await state(tour)).current, 3, 'End');
    });
    await check('Next on the last page does nothing without data-loop, and the button keeps the focus that is on it', async () => {
      await page.focus(`${tour} .mat-carousel__next`);
      await page.keyboard.press('Enter');
      await page.waitForTimeout(500);
      expect((await state(tour)).current, 3, 'still the last page');
      expect(await page.evaluate(sel => document.activeElement === document.querySelector(sel + ' .mat-carousel__next'), tour), true, 'the focus');
    });

    const apps = 'section[aria-label="Apps of the family"]';
    await page.evaluate(() => document.querySelector('section[aria-label="Apps of the family"]').scrollIntoView({ block: 'center' }));
    await check('several slides at once: three fit on a wide screen, the dots are pages of them', async () => {
      const s = await state(apps);
      expect(s.slides, 5, 'slides');
      expect(s.dots, 2, 'pages');
      expect(s.inert, 'false,false,false,true,true', 'the three in view can be reached');
      await page.click(`${apps} .mat-carousel__dot >> nth=1`);
      await page.waitForTimeout(700);
      const t = await state(apps);
      expect(t.current, 1, 'the second page');
      expect(t.inert, 'true,true,false,false,false', 'the last three, the strip ends at the last slide');
      expect(t.next, 'true', 'Next on the last page');
    });

    const news = 'section[aria-label="News"]';
    await page.evaluate(() => document.querySelector('section[aria-label="News"]').scrollIntoView({ block: 'center' }));
    await check('a show that moves by itself has a Pause button, waits for the pointer, and stops for good after a click', async () => {
      // a copy of the example with a short interval, so that the check does not wait six seconds
      await page.evaluate(() => {
        const src = document.querySelector('section[aria-label="News"]');
        const copy = src.cloneNode(true);
        copy.setAttribute('aria-label', 'Fast news');
        copy.setAttribute('data-autoplay', '500');
        copy.querySelector('.mat-carousel__bar')?.remove();
        copy.querySelector('[role=status]')?.remove();
        delete copy._matCarousel;
        copy.className = 'mat-carousel';
        src.parentNode.insertBefore(copy, src);
        window.MatUi.carousel.init(copy.parentNode);
      });
      const fast = 'section[aria-label="Fast news"]';
      await page.mouse.move(0, 0);   // the pointer of an earlier click may rest over the copy, and a show waits for the pointer
      expect(await page.getAttribute(`${fast} .mat-carousel__toggle`, 'aria-label'), 'Pause the slide show', 'the button while it plays');
      // it moves by itself: some page other than the first is shown within two seconds
      await page.waitForFunction(sel => [...document.querySelectorAll(sel + ' .mat-carousel__dot')].findIndex(d => d.getAttribute('aria-current') === 'true') > 0, fast, { timeout: 2500 });
      expect((await state(fast)).live, '', 'a moving show says nothing to a screen reader');
      // the pointer is inside: it waits (a scroll that had begun is let to end first)
      await page.hover(`${fast} .mat-carousel__track`);
      await page.waitForTimeout(700);
      const held = (await state(fast)).current;
      await page.waitForTimeout(1300);
      expect((await state(fast)).current, held, 'the show moved while the pointer was inside');
      // a click on Pause stops it for good, also when the pointer has left
      await page.click(`${fast} .mat-carousel__toggle`);
      expect(await page.getAttribute(`${fast} .mat-carousel__toggle`, 'aria-label'), 'Start the slide show', 'the button after Pause');
      await page.mouse.move(0, 0);
      await page.waitForTimeout(700);
      const stopped = (await state(fast)).current;
      await page.waitForTimeout(1300);
      expect((await state(fast)).current, stopped, 'the show moved after Pause');
      expect(await page.evaluate(() => document.querySelector('section[aria-label="Fast news"]')._matCarousel.playing), false, 'the show is still marked as playing');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open({ ...desktopOptions, reducedMotion: 'reduce' }, '/Components#carousel');
    await check('for a user who asked for less motion a show does not start by itself, and the button can start it', async () => {
      const news = 'section[aria-label="News"]';
      await page.evaluate(() => document.querySelector('section[aria-label="News"]').scrollIntoView({ block: 'center' }));
      await page.waitForTimeout(500);
      expect(await page.getAttribute(`${news} .mat-carousel__toggle`, 'aria-label'), 'Start the slide show', 'the button');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#carousel');
    await check('on a phone one slide fills the strip, every card has a dot and the page does not scroll sideways', async () => {
      const apps = 'section[aria-label="Apps of the family"]';
      await page.evaluate(() => document.querySelector('section[aria-label="Apps of the family"]').scrollIntoView({ block: 'center' }));
      await page.waitForTimeout(300);
      const s = await page.$eval(apps, c => {
        const track = c.querySelector('.mat-carousel__track').getBoundingClientRect();
        const slide = c.querySelector('.mat-carousel__slide').getBoundingClientRect();
        return {
          dots: c.querySelectorAll('.mat-carousel__dot').length, oneAtATime: Math.abs(slide.width - track.width) < 2,
          inert: [...c.querySelectorAll('.mat-carousel__slide')].map(x => x.hasAttribute('inert')).join(','),
          sideways: document.documentElement.scrollWidth > innerWidth,
          dotSize: Math.round(c.querySelector('.mat-carousel__dot').getBoundingClientRect().width),
        };
      });
      expect(s, { dots: 5, oneAtATime: true, inert: 'false,true,true,true,true', sideways: false, dotSize: 36 }, 'the phone');
      await page.click(`${apps} .mat-carousel__next`);
      await page.waitForTimeout(700);
      expect(await page.$eval(apps, c => [...c.querySelectorAll('.mat-carousel__dot')].findIndex(d => d.getAttribute('aria-current') === 'true')), 1, 'the page after Next');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- slider and range slider (FORM-17)
  console.log('\nSlider and range slider');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#slider');
    await page.evaluate(() => document.getElementById('slider').scrollIntoView());
    await page.evaluate(() => {
      window.__events = [];
      for (const type of ['input', 'change']) {
        document.addEventListener(type, e => { if (e.target.classList && e.target.classList.contains('mat-slider__input')) { window.__events.push(type + ':' + e.target.id); } });
      }
    });
    const events = () => page.evaluate(() => window.__events.splice(0));
    // what a slider looks like at the moment: the value, the thumb of the input, its bubble
    const info = id => page.$eval('#' + id, input => {
      const w = input.closest('.mat-slider');
      w.scrollIntoView({ block: 'center', behavior: 'instant' });
      const thumb = w.querySelectorAll('.mat-slider__thumb')[[...w.querySelectorAll('.mat-slider__input')].indexOf(input)];
      const rail = w.querySelector('.mat-slider__rail').getBoundingClientRect();
      const t = thumb.getBoundingClientRect();
      const bubble = thumb.querySelector('.mat-slider__bubble');
      return {
        value: input.value, min: +input.min, max: +input.max, valuetext: input.getAttribute('aria-valuetext'), name: input.getAttribute('aria-label'),
        railLeft: rail.left, railWidth: rail.width, railY: rail.top + rail.height / 2, thumbX: t.left + t.width / 2, thumbW: Math.round(t.width), thumbH: Math.round(t.height),
        bubble: getComputedStyle(bubble).opacity, bubbleText: bubble.textContent, top: thumb.classList.contains('is-top'), z: getComputedStyle(thumb).zIndex, key: thumb.classList.contains('is-key'),
        fill: [w.style.getPropertyValue('--mat-slider-from'), w.style.getPropertyValue('--mat-slider-to')],
      };
    });
    const value = id => page.$eval('#' + id, el => el.value);
    const at = async (id, v) => { const i = await info(id); return i.railLeft + (i.railWidth * (v - i.min)) / (i.max - i.min); };
    const drag = async (id, to, steps = 6) => {
      const i = await info(id);
      await page.mouse.move(i.thumbX, i.railY);
      await page.mouse.down();
      await page.mouse.move(await at(id, to), i.railY, { steps });
    };

    await check('a slider is dressed by the script and keeps its native input: a thumb with a bubble, labels at the ends, the value as text', async () => {
      const s = await page.$eval('#sl-vol', input => {
        const w = input.closest('.mat-slider');
        input.focus();
        return {
          ready: w.classList.contains('is-ready'), thumbs: w.querySelectorAll('.mat-slider__thumb').length, ends: [...w.querySelectorAll('.mat-slider__end')].map(e => e.textContent),
          opacity: getComputedStyle(input).opacity, focus: document.activeElement === input, valuetext: input.getAttribute('aria-valuetext'),
          out: document.getElementById('sl-vol-out').textContent, railHidden: w.querySelector('.mat-slider__rail').getAttribute('aria-hidden'),
        };
      });
      expect(s, { ready: true, thumbs: 1, ends: ['0 %', '100 %'], opacity: '0', focus: true, valuetext: '40 %', out: '40 %', railHidden: 'true' }, 'the slider');
      expect((await info('sl-vol')).fill, ['0%', '40%'], 'the filled part');
    });

    await check('dragging the thumb with the mouse sets the value, sends input while it moves and one change at the end, and the bubble shows while it is dragged', async () => {
      await events();
      await drag('sl-vol', 60);
      await page.waitForTimeout(250);
      const during = await info('sl-vol');
      expect({ value: during.value, bubble: during.bubble, text: during.bubbleText }, { value: '60', bubble: '1', text: '60 %' }, 'while dragging');
      await page.mouse.up();
      const ev = await events();
      expectTrue(ev.filter(e => e === 'input:sl-vol').length >= 2, 'input is sent while the thumb moves: ' + ev.join());
      expect(ev.filter(e => e.startsWith('change')), ['change:sl-vol'], 'one change at the end');
      const after = await info('sl-vol');
      expect({ valuetext: after.valuetext, out: await page.textContent('#sl-vol-out'), fill: after.fill }, { valuetext: '60 %', out: '60 %', fill: ['0%', '60%'] }, 'after the drag');
    });

    await check('a press on the track moves the thumb there; a press on the thumb beside its centre moves nothing', async () => {
      const g = await info('sl-vol');
      await page.mouse.click(await at('sl-vol', 10), g.railY);
      expect(await value('sl-vol'), '10', 'after a press on the track');
      const k = await info('sl-vol');
      await page.mouse.move(k.thumbX + 8, k.railY);
      await page.mouse.down();
      await page.mouse.up();
      expect(await value('sl-vol'), '10', 'a press on the thumb');
    });

    await check('keys: the arrows move by a step, Page Up and Page Down by a tenth, Home and End to the ends, and every key sends input and change', async () => {
      await page.focus('#sl-vol');
      await events();
      const steps = [['ArrowRight', '11'], ['ArrowUp', '12'], ['ArrowLeft', '11'], ['ArrowDown', '10'], ['PageUp', '20'], ['PageDown', '10'], ['End', '100'], ['Home', '0']];
      for (const [key, expected] of steps) {
        await page.keyboard.press(key);
        expect(await value('sl-vol'), expected, key);
      }
      const ev = await events();
      expect([ev.filter(e => e.startsWith('input')).length, ev.filter(e => e.startsWith('change')).length], [8, 8], 'input and change per key');
      // a tenth of the scale, but at least one step
      await page.focus('#sl-bright');
      await page.keyboard.press('PageUp');
      expect(await value('sl-bright'), '75', 'Page Up on 0..100 in steps of 5');
      await page.focus('#sl-keep');
      await page.keyboard.press('PageUp');
      expect(await value('sl-keep'), '21', 'Page Up on 7..28 in steps of 7: one step');
      await page.keyboard.press('End');
      await page.keyboard.press('ArrowRight');
      expect(await value('sl-keep'), '28', 'nothing beyond the maximum');
    });

    await check('the bubble shows with the focus (the ring only for the keyboard) and stays where data-bubble="always" says so', async () => {
      await page.evaluate(() => document.activeElement.blur());
      await page.waitForTimeout(250);
      expect([(await info('sl-vol')).bubble, (await info('sl-bright')).bubble], ['0', '1'], 'the bubbles at rest');
      await page.focus('#sl-vol');
      await page.keyboard.press('Tab');
      await page.waitForTimeout(250);
      const b = await info('sl-bright');
      expect([b.key, (await info('sl-vol')).bubble, (await info('sl-vol')).key], [true, '0', false], 'the keyboard moved the focus to the next slider');
      const ring = await page.$eval('#sl-bright', input => getComputedStyle(input.closest('.mat-slider').querySelector('.mat-slider__thumb'), '::before').boxShadow);
      expectTrue(ring.includes('3px'), 'the ring of the focus: ' + ring);
    });

    await check('data-format reaches the bubble, the labels, the output and what a screen reader says; marks and labels follow data-ticks', async () => {
      const s = await page.$eval('#sl-disk', input => {
        const w = input.closest('.mat-slider');
        return {
          valuetext: input.getAttribute('aria-valuetext'), bubble: w.querySelector('.mat-slider__bubble').textContent, out: document.getElementById('sl-disk-out').textContent,
          ticks: w.querySelectorAll('.mat-slider__tick').length, on: w.querySelectorAll('.mat-slider__tick.is-on').length, labels: [...w.querySelectorAll('.mat-slider__tick-label')].map(l => l.textContent),
        };
      });
      expect(s, { valuetext: '40 GB', bubble: '40 GB', out: '40 GB', ticks: 10, on: 4, labels: ['10 GB', '40 GB', '70 GB', '100 GB'] }, 'the storage slider');
      await page.focus('#sl-disk');
      await page.keyboard.press('ArrowRight');
      expect(await page.textContent('#sl-disk-out'), '50 GB', 'the output follows');
    });

    await check('a disabled slider cannot be moved with the pointer or focused, and is dimmed', async () => {
      const g = await info('sl-off');
      await page.mouse.click(await at('sl-off', 10), g.railY);
      const s = await page.$eval('#sl-off', input => { input.focus(); return { value: input.value, focused: document.activeElement === input, opacity: getComputedStyle(input.closest('.mat-slider')).opacity }; });
      expect(s, { value: '70', focused: false, opacity: '0.5' }, 'the disabled slider');
    });

    await check('a range slider is one track with two thumbs; each input is named from the label, says its value with its unit and the output reads From 20 € to 80 €', async () => {
      const a = await info('sl-price-from'), b = await info('sl-price-to');
      expect([a.name, b.name, a.valuetext, b.valuetext], ['Price from', 'Price to', '20 €', '80 €'], 'names and values');
      const s = await page.$eval('#sl-price-from', el => {
        const w = el.closest('.mat-slider');
        return { role: w.getAttribute('role'), label: w.getAttribute('aria-label'), thumbs: w.querySelectorAll('.mat-slider__thumb').length, rails: w.querySelectorAll('.mat-slider__rail').length, out: document.getElementById('sl-price-out').textContent };
      });
      expect(s, { role: 'group', label: 'Price', thumbs: 2, rails: 1, out: 'From 20 € to 80 €' }, 'the wrapper');
      expect(a.fill, ['10%', '40%'], 'the filled part is between the thumbs');
    });

    await check('a press on the track moves the nearest thumb', async () => {
      const g = await info('sl-price-from');
      await page.mouse.click(await at('sl-price-from', 40), g.railY);
      expect([await value('sl-price-from'), await value('sl-price-to')], ['40', '80'], 'nearer to the left thumb');
      await page.mouse.click(await at('sl-price-from', 100), g.railY);
      expect([await value('sl-price-from'), await value('sl-price-to')], ['40', '100'], 'nearer to the right thumb');
    });

    await check('the thumbs cannot cross: a thumb that is dragged past the other stops a gap before it, and the two bubbles become one', async () => {
      await drag('sl-price-from', 190, 10);
      await page.waitForTimeout(250);
      expect([await value('sl-price-from'), await value('sl-price-to')], ['90', '100'], 'from stops at to minus the gap of 10');
      const s = await page.$eval('#sl-price-from', el => {
        const w = el.closest('.mat-slider'), [from, to] = [...w.querySelectorAll('.mat-slider__bubble')];
        return { merged: w.classList.contains('is-merged'), from: getComputedStyle(from).opacity, to: getComputedStyle(to).opacity, text: to.textContent };
      });
      expect(s, { merged: true, from: '0', to: '1', text: '90 € – 100 €' }, 'one bubble for both');
      await page.mouse.up();
      expect(await page.textContent('#sl-price-out'), 'From 90 € to 100 €', 'the output');
    });

    await check('the keys of a range slider stop at the other thumb too; Home on the right thumb goes to the left one, not past it', async () => {
      const g = await info('sl-price-from');
      await page.mouse.click(await at('sl-price-from', 150), g.railY);
      expect(await value('sl-price-to'), '150', 'the right thumb went to the press');
      await page.focus('#sl-price-to');
      await page.keyboard.press('Home');
      expect([await value('sl-price-from'), await value('sl-price-to')], ['90', '100'], 'Home on the right thumb: the left one plus the gap');
      await page.keyboard.press('End');
      await page.focus('#sl-price-from');
      await page.keyboard.press('End');
      expect([await value('sl-price-from'), await value('sl-price-to')], ['190', '200'], 'End on the left thumb: the right one minus the gap');
      await page.keyboard.press('Home');
      expect(await value('sl-price-from'), '0', 'Home on the left thumb');
      expect((await info('sl-price-from')).valuetext, '0 €', 'what a screen reader says');
    });

    await check('a bubble at an end stays inside the slider', async () => {
      const s = await page.$eval('#sl-price-from', el => {
        const w = el.closest('.mat-slider'), box = w.getBoundingClientRect(), [from, to] = [...w.querySelectorAll('.mat-slider__bubble')].map(b => b.getBoundingClientRect());
        return { left: from.left >= box.left - 1, right: to.right <= box.right + 1, merged: w.classList.contains('is-merged') };
      });
      expect(s, { left: true, right: true, merged: false }, 'bubbles at 0 and 200');
    });

    // a range slider without a gap, added after the page was drawn
    await page.evaluate(() => {
      document.getElementById('sl-price-from').closest('.mat-field').insertAdjacentHTML('afterend',
        '<div class="mat-field" id="sl-dyn"><label class="mat-label" for="sl-dyn-from">Weight</label>' +
        '<div class="mat-slider mat-slider--range" data-slider data-format="{0} kg"><input class="mat-slider__input" type="range" id="sl-dyn-from" name="wf" min="0" max="100" value="50" />' +
        '<input class="mat-slider__input" type="range" id="sl-dyn-to" name="wt" min="0" max="100" value="50" /></div></div>');
      window.MatUi.refresh(document.getElementById('sl-dyn'));
    });
    await page.evaluate(() => document.getElementById('sl-dyn').scrollIntoView({ block: 'center' }));

    await check('MatUi.refresh picks up a slider that was added later; its names come from the label', async () => {
      const s = await page.$eval('#sl-dyn-from', el => ({ ready: el.closest('.mat-slider').classList.contains('is-ready'), from: el.getAttribute('aria-label'), to: document.getElementById('sl-dyn-to').getAttribute('aria-label') }));
      expect(s, { ready: true, from: 'Weight from', to: 'Weight to' }, 'the new slider');
    });

    await check('with both thumbs at one value a press beside them takes the one on that side and the thumb that moved last is on top', async () => {
      const g = await info('sl-dyn-from');
      await page.mouse.click(await at('sl-dyn-from', 20), g.railY);
      expect([await value('sl-dyn-from'), await value('sl-dyn-to')], ['20', '50'], 'a press left of both');
      await page.mouse.click(await at('sl-dyn-from', 80), g.railY);
      expect([await value('sl-dyn-from'), await value('sl-dyn-to')], ['20', '80'], 'a press right of both');
      await drag('sl-dyn-from', 100, 8);
      await page.mouse.up();
      const a = await info('sl-dyn-from'), b = await info('sl-dyn-to');
      expect([a.value, b.value, a.top, b.top, a.z, b.z], ['80', '80', true, false, '2', '1'], 'the left thumb reached the right one: it is on top');
      // both at 80: the thumbs are pressed and the first movement says which one is meant
      await page.mouse.move(a.thumbX, a.railY);
      await page.mouse.down();
      await page.mouse.move(a.thumbX - 60, a.railY, { steps: 6 });
      await page.mouse.up();
      expect([Number(await value('sl-dyn-from')) < 80, await value('sl-dyn-to')], [true, '80'], 'a movement to the left takes the left thumb');
      const from = await value('sl-dyn-from');
      await drag('sl-dyn-to', 0, 8);
      await page.mouse.up();
      expect([await value('sl-dyn-from'), await value('sl-dyn-to'), (await info('sl-dyn-to')).top], [from, from, true], 'the right thumb stops at the left one and is on top now');
      const c = await info('sl-dyn-to');
      await page.mouse.move(c.thumbX, c.railY);
      await page.mouse.down();
      await page.mouse.move(c.thumbX + 40, c.railY, { steps: 6 });
      await page.mouse.up();
      expect([await value('sl-dyn-from'), Number(await value('sl-dyn-to')) > Number(from)], [from, true], 'a movement to the right takes the right thumb');
    });

    await check('MatUi.slider.update reads a changed scale again: the labels at the ends and the thumbs follow', async () => {
      await page.evaluate(() => {
        for (const id of ['sl-dyn-from', 'sl-dyn-to']) { document.getElementById(id).max = '50'; }
        window.MatUi.slider.update(document.getElementById('sl-dyn-from').closest('.mat-slider'));
      });
      const s = await page.$eval('#sl-dyn-from', el => {
        const w = el.closest('.mat-slider');
        return { ends: [...w.querySelectorAll('.mat-slider__end')].map(e => e.textContent), to: document.getElementById('sl-dyn-to').value, at: getComputedStyle(w.querySelectorAll('.mat-slider__thumb')[1]).left };
      });
      expect(s.ends, ['0 kg', '50 kg'], 'the labels at the ends');
      expectTrue(Number(s.to) <= 50, 'the value is inside the new scale: ' + s.to);
    });

    await check('the names are German on a German page, and the bubble is readable in light and dark', async () => {
      const s = await page.evaluate(() => {
        document.documentElement.lang = 'de';
        document.getElementById('sl-dyn').insertAdjacentHTML('afterend',
          '<div class="mat-field" id="sl-de"><label class="mat-label" for="sl-de-from">Preis</label><div class="mat-slider mat-slider--range" data-slider>' +
          '<input class="mat-slider__input" type="range" id="sl-de-from" name="df" value="20" /><input class="mat-slider__input" type="range" id="sl-de-to" name="dt" value="60" /></div></div>');
        window.MatUi.refresh(document.getElementById('sl-de'));
        document.documentElement.lang = 'en';
        return [document.getElementById('sl-de-from').getAttribute('aria-label'), document.getElementById('sl-de-to').getAttribute('aria-label')];
      });
      expect(s, ['Preis von', 'Preis bis'], 'German names');
      const contrast = theme => page.evaluate(t => {
        document.documentElement.setAttribute('data-theme', t);
        const rgb = c => c.match(/[\d.]+/g).slice(0, 3).map(Number);
        const lum = c => { const [r, g, b] = rgb(c).map(v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
        const b = getComputedStyle(document.querySelector('#sl-bright').closest('.mat-slider').querySelector('.mat-slider__bubble'));
        const [l1, l2] = [lum(b.color), lum(b.backgroundColor)].sort((x, y) => y - x);
        return (l1 + 0.05) / (l2 + 0.05);
      }, theme);
      for (const theme of ['light', 'dark']) { expectTrue((await contrast(theme)) >= 4.5, 'the bubble in ' + theme); }
      await page.evaluate(() => document.documentElement.removeAttribute('data-theme'));
    });

    await check('the values go into the form and a reset puts the markup back: the thumbs, the bubbles, the output and the words for a screen reader are drawn again', async () => {
      await page.evaluate(() => document.getElementById('sl-f-from').scrollIntoView({ block: 'center' }));
      await page.focus('#sl-f-from');
      await page.keyboard.press('End');
      await page.keyboard.press('ArrowLeft');
      await page.focus('#sl-f-rating');
      await page.keyboard.press('End');
      const sent = await page.$eval('#sl-f-from', el => Object.fromEntries(new FormData(el.form)));
      expect(sent, { price_from: '65', price_to: '80', rating: '5' }, 'what the form sends');
      expect(await page.textContent('#sl-f-price-out'), 'From 65 € to 80 €', 'the output');
      await page.click('form:has(#sl-f-from) button[type=reset]');
      await page.waitForTimeout(150);
      const s = await page.$eval('#sl-f-from', el => {
        const f = el.form, w = id => document.getElementById(id).closest('.mat-slider');
        return {
          sent: Object.fromEntries(new FormData(f)), price: [w('sl-f-from').style.getPropertyValue('--mat-slider-from'), w('sl-f-from').style.getPropertyValue('--mat-slider-to')], rating: w('sl-f-rating').style.getPropertyValue('--mat-slider-to'),
          out: document.getElementById('sl-f-price-out').textContent, out2: document.getElementById('sl-f-rating-out').textContent, text: document.getElementById('sl-f-rating').getAttribute('aria-valuetext'),
        };
      });
      expect(s, { sent: { price_from: '20', price_to: '80', rating: '3' }, price: ['10%', '40%'], rating: '50%', out: 'From 20 € to 80 €', out2: '3 / 5', text: '3 / 5' }, 'after the reset');
    });

    await check('a slider in a dialog is placed right when the dialog opens: the thumbs, the bubbles, no sideways scroll', async () => {
      await page.evaluate(() => document.querySelector('[data-dialog-open="sl-dialog"]').scrollIntoView({ block: 'center' }));
      await page.click('[data-dialog-open="sl-dialog"]');
      await page.waitForTimeout(450);
      const from = await info('sl-d-from'), to = await info('sl-d-to');
      expectTrue(from.railWidth > 100, 'the rail has a width: ' + from.railWidth);
      expectTrue(Math.abs(from.thumbX - (from.railLeft + from.railWidth * 0.1)) < 1.5 && Math.abs(to.thumbX - (to.railLeft + to.railWidth * 0.7)) < 1.5, 'the thumbs are at 10% and 70% of the rail');
      expect([from.bubble, to.bubble, await page.$eval('#sl-d-from', el => el.closest('.mat-slider').classList.contains('is-merged'))], ['1', '1', false], 'two bubbles');
      expect(await page.$eval('#sl-dialog .mat-dialog__body', b => b.scrollWidth <= b.clientWidth + 1), true, 'no sideways scroll in the dialog');
      await page.keyboard.press('Escape');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#slider');
    await page.evaluate(() => document.getElementById('slider').scrollIntoView());
    await check('on a phone the thumb is a target of 44 px (a small slider 36 px), and nothing scrolls sideways, also with every thumb at an end (MOBILE-04)', async () => {
      const sizes = await page.evaluate(() => {
        const size = id => { const t = document.getElementById(id).closest('.mat-slider').querySelector('.mat-slider__thumb').getBoundingClientRect(); return [Math.round(t.width), Math.round(t.height)]; };
        return { sm: size('sl-sm'), md: size('sl-md'), lg: size('sl-lg'), touch: getComputedStyle(document.querySelector('#sl-md').closest('.mat-slider').querySelector('.mat-slider__rail')).touchAction };
      });
      expect(sizes, { sm: [36, 36], md: [44, 44], lg: [52, 52], touch: 'pan-y' }, 'targets');
      for (const edge of ['min', 'max']) {
        await page.evaluate(which => document.querySelectorAll('#slider .mat-slider__input:not(:disabled)').forEach((input, n) => {
          input.value = (which === 'min') === (n % 2 === 0) ? input.min : input.max;
          input.dispatchEvent(new Event('input', { bubbles: true }));
        }), edge);
        const widths = await page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth, thumbs: [...document.querySelectorAll('#slider .mat-slider__thumb')].filter(t => t.getBoundingClientRect().right > innerWidth || t.getBoundingClientRect().left < 0).length }));
        expect(widths, { page: 390, window: 390, thumbs: 0 }, 'width with the thumbs at ' + edge);
      }
    });
    await check('a tap on the track moves the nearest thumb, a touch drag follows the finger with the bubble above it, and one change is sent when the finger lifts', async () => {
      await page.evaluate(() => document.getElementById('sl-price-from').scrollIntoView({ block: 'center' }));
      await page.evaluate(() => {
        for (const [id, v] of [['sl-price-from', 20], ['sl-price-to', 80]]) { const i = document.getElementById(id); i.value = v; i.dispatchEvent(new Event('input', { bubbles: true })); }
        window.__events = [];
        for (const type of ['input', 'change']) { document.addEventListener(type, e => { if (e.target.id && e.target.id.startsWith('sl-price')) { window.__events.push(type + ':' + e.target.id); } }); }
      });
      const info2 = id => page.$eval('#' + id, input => {
        const w = input.closest('.mat-slider'), rail = w.querySelector('.mat-slider__rail').getBoundingClientRect();
        const t = w.querySelectorAll('.mat-slider__thumb')[[...w.querySelectorAll('.mat-slider__input')].indexOf(input)];
        const r = t.getBoundingClientRect();
        return { value: input.value, left: rail.left, width: rail.width, y: rail.top + rail.height / 2, x: r.left + r.width / 2, bubble: getComputedStyle(t.querySelector('.mat-slider__bubble')).opacity };
      });
      const g = await info2('sl-price-from');
      await page.touchscreen.tap(g.left + g.width * 0.6, g.y);
      expect([await page.$eval('#sl-price-from', el => el.value), await page.$eval('#sl-price-to', el => el.value)], ['20', '120'], 'a tap nearer to the right thumb moves it');
      const cdp = await ctx.newCDPSession(page);
      const touch = (type, x, y) => cdp.send('Input.dispatchTouchEvent', { type, touchPoints: type === 'touchEnd' ? [] : [{ x, y }] });
      await page.evaluate(() => { window.__events.length = 0; });
      const f = await info2('sl-price-from');
      await touch('touchStart', f.x, f.y);
      for (let k = 1; k <= 6; k++) { await touch('touchMove', f.x + k * 18, f.y); }
      await page.waitForTimeout(250);
      // the bubble of the thumb that is touched is shown (or the one that two near thumbs share), with the value in it
      const mid = await page.$eval('#sl-price-from', input => {
        const w = input.closest('.mat-slider');
        return [...w.querySelectorAll('.mat-slider__bubble')].filter(b => getComputedStyle(b).opacity === '1').map(b => b.textContent).join('|');
      });
      expectTrue(mid.includes(await page.$eval('#sl-price-from', el => el.value) + ' €'), 'the bubble while the finger is down: ' + mid);
      await touch('touchEnd');
      const end = await info2('sl-price-from');
      expectTrue(Number(end.value) > 20 && Number(end.value) <= 120 - 10, 'the thumb followed the finger: ' + end.value);
      expectTrue(Math.abs(end.x - (f.x + 108)) < f.width / 40 + 4, `the thumb is under the finger: ${end.x} against ${f.x + 108}`);
      const ev = await page.evaluate(() => window.__events.splice(0));
      expect(ev.filter(e => e.startsWith('change')), ['change:sl-price-from'], 'one change');
      expectTrue(ev.filter(e => e.startsWith('input')).length >= 2, 'input while the finger moves');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the version of the toolkit and its changelog
  console.log('\nThe version of the toolkit');
  {
    const { ctx, page } = await open(desktopOptions, '/Changelog');
    const api = async url => (await page.request.get(BASE + url)).json();
    await check('the script, the manifest and the API say the same version, and the first line of the stylesheet names it', async () => {
      const served = await api('/api/version');
      expectTrue(/^\d+\.\d+\.\d+$/.test(served.matui), 'the version is not three numbers: ' + served.matui);
      expect(await page.evaluate(() => window.MatUi.version), served.matui, 'MatUi.version');
      expect((await api('/ui/manifest.json')).version, served.matui, 'the manifest');
      const css = await (await page.request.get(BASE + '/ui/matui.css')).text();
      expectTrue(css.startsWith(`/* MatUi ${served.matui} (`), 'the first line of matui.css: ' + css.split('\n')[0]);
      expectTrue(css.split('\n')[0].includes(' - build '), 'the build is not in the first line');
    });
    await check('the changelog page lists the versions with their sections and shows what is not released yet', async () => {
      expect((await page.textContent('h1')).trim(), 'Changelog', 'h1');
      expectTrue(await page.locator('#unreleased').isVisible(), 'no Unreleased');
      expectTrue((await page.locator('.mat-card h3').allTextContents()).some(t => t.startsWith('Version ')), 'no version');
      expectTrue((await page.locator('.doc-changes__name').allTextContents()).includes('Added'), 'no section Added');
      // the toolkit classes in the text of an entry are code
      expectTrue(await page.locator('.doc-changes code').count() > 5, 'the `code` of the entries is not drawn as code');
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'the page scrolls sideways');
    });
    await check('what an update takes with it: a version that is older, the current one and one that is no version', async () => {
      const current = (await api('/api/version')).matui;
      await page.fill('#since', '0.0.1');
      await page.click('form button[type=submit]');
      await page.waitForLoadState('networkidle');
      expectTrue((await page.textContent('.mat-alert--info, .mat-alert--warning')).includes('newer version'), 'no summary of the newer versions');
      await page.fill('#since', current);
      await page.click('form button[type=submit]');
      await page.waitForLoadState('networkidle');
      expectTrue((await page.textContent('.mat-alert--success')).includes('up to date'), 'the current version is not up to date');
      await page.fill('#since', 'banana');
      await page.click('form button[type=submit]');
      await page.waitForLoadState('networkidle');
      expectTrue((await page.textContent('.mat-error')).includes('three numbers'), 'no message for something that is not a version');
      expect(await page.getAttribute('#since', 'value'), 'banana', 'the input is kept');
    });
    await check('the changelog as data answers for a version, and the page is in the menu', async () => {
      const since = await api('/api/changelog?since=0.0.1');
      expect(since.upToDate, false, 'upToDate');
      expectTrue(since.releases.length > 0, 'no versions after 0.0.1');
      expectTrue(since.releases.every(r => typeof r.version === 'string' && typeof r.breaking === 'boolean' && Array.isArray(r.migration)), 'the shape of a version');
      expect((await page.request.get(BASE + '/api/changelog?since=old')).status(), 400, 'a bad version');
      expectTrue((await page.locator('.mat-nav a[href="/Changelog"]').getAttribute('aria-current')) === 'page', 'the page is not the open item of the menu');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Download');
    await check('the download page names the version and where to read what changed, and the archive carries its manifest', async () => {
      const version = (await (await page.request.get(BASE + '/api/version')).json()).matui;
      const text = await page.textContent('main');
      expectTrue(text.includes(`MatUi ${version}`), 'the version is not on the page');
      for (const address of ['/Changelog', '/ui/CHANGELOG.md', '/ui/manifest.json', '/api/changelog']) { expectTrue(await page.locator(`a[href^="${address}"]`).count() > 0, 'no link to ' + address); }
      const manifest = await (await page.request.get(BASE + '/ui/manifest.json')).json();
      expect(manifest.version, version, 'the manifest');
      for (const file of manifest.files.slice(0, 3)) {
        const body = await (await page.request.get(BASE + file.url)).text();
        expect(require('crypto').createHash('sha256').update(body, 'utf8').digest('hex'), file.sha256, 'the hash of ' + file.name);
      }
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Changelog');
    await check('on a phone the changelog is one column and the form fits', async () => {
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'the page scrolls sideways');
      const form = await page.locator('form[action="/Changelog"] .mat-btn--primary').boundingBox();
      expectTrue(form && form.x >= 0 && form.x + form.width <= 390, 'the button leaves the screen');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the select: create what is missing, suggestions (FORM-16)
  console.log('\nSelect: create and suggestions');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#rich-select');
    const panelOf = id => `#${id}-trigger ~ .mat-picker__panel`;
    const search = id => `${panelOf(id)} input[type=search]`;
    const createRow = id => `${panelOf(id)} .mat-picker__create`;
    const valueOf = id => page.$eval(`#${id}`, el => el.value);
    const shown = async id => (await page.textContent(`#${id}-trigger .mat-picker__value`)).replace(/\s+/g, ' ').trim();
    const names = async id => (await page.locator(`${panelOf(id)} .mat-picker__option:not([hidden]) .mat-picker__name`).allTextContents()).map(t => t.trim());
    const live = id => page.$eval(`#${id}-trigger ~ [role=status].mat-sr-only`, el => el.textContent);
    const open1 = async id => { await page.evaluate(i => document.getElementById(i + '-trigger').scrollIntoView({ block: 'center' }), id); if (!(await page.locator(panelOf(id)).isVisible())) { await page.click(`#${id}-trigger`); } };
    await page.evaluate(() => { window.__changes = []; document.addEventListener('change', e => { if (e.target.id) { window.__changes.push(e.target.id + '=' + e.target.value); } }); });

    await check('a select with data-create-url always has the search field, and offers Create only for a text that is not the name of an option', async () => {
      await open1('pk-create');
      expect(await page.evaluate(() => document.activeElement.type), 'search', 'the search field has the focus on a wide screen');
      expectTrue(await page.locator(createRow('pk-create')).isHidden(), 'the create row is shown with nothing typed');
      await page.fill(search('pk-create'), 'Web');
      expectTrue(await page.locator(createRow('pk-create')).isVisible(), 'no create row for "Web", which is only the start of a name');
      await page.fill(search('pk-create'), 'WEBSITE');
      expectTrue(await page.locator(createRow('pk-create')).isHidden(), 'a create row for the name of an option, written in other letters');
      await page.fill(search('pk-create'), '');
      expectTrue(await page.locator(createRow('pk-create')).isHidden(), 'the create row is shown with nothing typed');
      expect(await page.locator(`${panelOf('pk-create')} .mat-picker__option[data-value]:not([hidden])`).count(), 3, 'the options with nothing typed');
    });
    await check('a click on Create posts name=<text> with the answer taken as a new option that is chosen: the field shows it, the panel closes, the change event fires and it is announced', async () => {
      await page.fill(search('pk-create'), '  Intranet ');
      expect((await page.textContent(createRow('pk-create'))).trim(), 'Create "Intranet"', 'the row');
      expect(await page.getAttribute(createRow('pk-create'), 'role'), 'option', 'the row is an option of the list');
      const [request] = await Promise.all([page.waitForRequest('**/api/demo/create'), page.click(createRow('pk-create'))]);
      expect([request.method(), request.postData(), request.headers()['content-type'], request.headers().accept], ['POST', 'name=Intranet', 'application/x-www-form-urlencoded;charset=UTF-8', 'application/json'], 'the request');
      await page.waitForFunction(() => document.getElementById('pk-create').value !== '');
      const value = await valueOf('pk-create');
      expectTrue(/^n-[0-9a-f]{8}$/.test(value), 'the key from the server: ' + value);
      expect(await shown('pk-create'), 'Intranet', 'the field');
      expectTrue(!(await page.locator(panelOf('pk-create')).isVisible()), 'the panel is still shown');
      expect(await page.evaluate(() => document.activeElement.id), 'pk-create-trigger', 'the focus');
      expect(await live('pk-create'), 'Created "Intranet"', 'the announcement');
      expect(await page.evaluate(() => window.__changes.filter(c => c.startsWith('pk-create='))), ['pk-create=' + value], 'the change event of the real input');
      const option = page.locator(`${panelOf('pk-create')} .mat-picker__option[data-value="${value}"]`);
      expect([await option.count(), (await option.locator('.mat-picker__name').textContent()).trim(), (await option.locator('.mat-picker__desc').textContent()).trim(), await option.getAttribute('role')], [1, 'Intranet', 'Created just now', 'option'], 'the new option');
    });
    await check('a name that exists (any case) has no Create row; Enter takes the first match, or the exact one when another name contains it, and never creates a second one', async () => {
      await open1('pk-create');
      await page.fill(search('pk-create'), 'intranet');
      expectTrue(await page.locator(createRow('pk-create')).isHidden(), 'a create row for the option that was just made');
      await page.fill(search('pk-create'), 'back');
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-create'), 'backoffice', 'Enter chose the first match');
      await page.evaluate(() => MatUi.picker.addOption('#pk-create', { value: 'web', name: 'Web' }));
      await open1('pk-create');
      await page.fill(search('pk-create'), 'web');
      expect(await names('pk-create'), ['Website', 'Web'], 'both names contain the text');
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-create'), 'web', 'Enter chose the exact name, not the first that contains it');
      expect(await page.evaluate(() => window.__changes.filter(c => c.startsWith('pk-create=')).length), 3, 'three changes so far, no new option');
    });
    await check('a refusal keeps the panel open, shows the message of the answer at the row, and typing takes it away; nothing is chosen', async () => {
      await open1('pk-create');
      await page.fill(search('pk-create'), 'error');
      const before = await valueOf('pk-create');
      await page.click(createRow('pk-create'));
      await page.waitForSelector(`${panelOf('pk-create')} .mat-picker__create-msg:not([hidden])`);
      expect((await page.textContent(`${panelOf('pk-create')} .mat-picker__create-msg`)).trim(), 'This demo refuses the name "error" on purpose, to show what a failure looks like.', 'the message of the server');
      expect(await page.getAttribute(`${panelOf('pk-create')} .mat-picker__create-msg`, 'aria-live'), 'polite', 'the message is read aloud');
      expectTrue(await page.locator(panelOf('pk-create')).isVisible(), 'the panel closed');
      expect(await valueOf('pk-create'), before, 'the value after a refusal');
      expect(await page.getAttribute(createRow('pk-create'), 'aria-busy'), null, 'the row is not busy any more');
      expectTrue(await page.locator(createRow('pk-create')).isVisible(), 'the row went away: the user cannot try again');
      await page.fill(search('pk-create'), 'erro');
      expectTrue(await page.locator(`${panelOf('pk-create')} .mat-picker__create-msg`).isHidden(), 'the message stays after typing');
    });
    await check('no answer at all gives the default text, in the language of the page', async () => {
      await page.route('**/api/demo/create', route => route.abort());
      try {
        await open1('pk-create');
        await page.fill(search('pk-create'), 'Intranet 2');
        await page.click(createRow('pk-create'));
        await page.waitForSelector(`${panelOf('pk-create')} .mat-picker__create-msg:not([hidden])`);
        expect((await page.textContent(`${panelOf('pk-create')} .mat-picker__create-msg`)).trim(), 'It could not be created. Please try again.', 'the default text');
      } finally {
        await page.unroute('**/api/demo/create');
      }
      await page.keyboard.press('Escape');
    });
    await check('the row is busy while the request runs and cannot be pressed twice; the anti-forgery token of the page goes along', async () => {
      await page.evaluate(() => document.body.insertAdjacentHTML('beforeend', '<input type="hidden" name="__RequestVerificationToken" value="tok-9" />'));
      const posts = [];
      await page.route('**/api/demo/create', async route => { posts.push(route.request().postData()); await new Promise(r => setTimeout(r, 700)); await route.continue(); });
      try {
        await open1('pk-create');
        await page.fill(search('pk-create'), 'Slow one');
        await page.click(createRow('pk-create'));
        expect(await page.getAttribute(createRow('pk-create'), 'aria-busy'), 'true', 'aria-busy while it runs');
        expectTrue(/Creating/.test(await page.textContent(createRow('pk-create'))), 'the busy text: ' + await page.textContent(createRow('pk-create')));
        await page.evaluate(sel => { const r = document.querySelector(sel); r.click(); r.click(); }, createRow('pk-create'));
        await page.waitForFunction(() => document.getElementById('pk-create').value.startsWith('n-'));
        await page.waitForTimeout(200);
        expect(posts, ['name=Slow+one&__RequestVerificationToken=tok-9'], 'one request, with the token');
      } finally {
        await page.unroute('**/api/demo/create');
        await page.evaluate(() => document.querySelectorAll('input[name=__RequestVerificationToken]').forEach(i => i.remove()));
      }
    });
    await check('with the keyboard: Arrow down reaches the Create row like an option and Enter creates; Enter in the search creates where nothing matches', async () => {
      await open1('pk-create');
      await page.fill(search('pk-create'), 'Zebra');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.classList.contains('mat-picker__create')), true, 'the focus is on the Create row');
      const first = await valueOf('pk-create');
      await page.keyboard.press('Enter');
      await page.waitForFunction(v => document.getElementById('pk-create').value !== v, first);
      expect(await shown('pk-create'), 'Zebra', 'created with the keyboard');
      await open1('pk-create');
      await page.fill(search('pk-create'), 'Quokka');
      const second = await valueOf('pk-create');
      await page.keyboard.press('Enter');
      await page.waitForFunction(v => document.getElementById('pk-create').value !== v, second);
      expect(await shown('pk-create'), 'Quokka', 'Enter in the search creates');
    });

    await check('suggestions: the closed field says nothing yet; opening fetches them; they stand under "Suggestions", the whole list under "All"; the closed field then counts them', async () => {
      expect(await shown('pk-suggest'), 'Choose tags', 'before the panel was opened');
      await open1('pk-suggest');
      await page.waitForSelector(`${panelOf('pk-suggest')} .mat-picker__group:not([hidden])`);
      const group = `${panelOf('pk-suggest')} .mat-picker__group`;
      expect((await page.locator(`${group} .mat-picker__group-head`).textContent()).trim(), 'Suggestions', 'the heading');
      expect(await page.getAttribute(group, 'role'), 'group', 'the group');
      expect((await page.locator(`${group} .mat-picker__name`).allTextContents()).map(t => t.trim()), ['Finance', 'Contracts', 'Invoices 2026'], 'the suggested entries');
      expect((await page.textContent(`${panelOf('pk-suggest')} .mat-picker__group-head--all`)).trim(), 'All', 'the heading of the list');
      expect(await page.locator(`${panelOf('pk-suggest')} .mat-picker__option[data-value]:not(.mat-picker__option--suggest)`).count(), 5, 'the list is all there');
      await page.keyboard.press('Escape');
      expect(await shown('pk-suggest'), '3 suggestions', 'the closed field');
      expect(await page.$eval('#pk-suggest-trigger .mat-picker__value', el => [el.classList.contains('is-placeholder'), el.classList.contains('is-suggest')]), [true, true], 'marked as a suggestion');
      const colour = await page.$eval('#pk-suggest-trigger .mat-picker__value', el => getComputedStyle(el).color);
      const link = await page.evaluate(() => { const a = document.createElement('a'); a.href = '#'; document.body.appendChild(a); const c = getComputedStyle(a).color; a.remove(); return c; });
      expect(colour, link, 'the colour of a link');
    });
    await check('suggestions are not shown while something is searched, and not once something is chosen; the suggested option is that option', async () => {
      await open1('pk-suggest');
      await page.fill(search('pk-suggest'), 'fam');
      expectTrue(await page.locator(`${panelOf('pk-suggest')} .mat-picker__group`).isHidden(), 'suggestions while searching');
      expectTrue(await page.locator(`${panelOf('pk-suggest')} .mat-picker__group-head--all`).isHidden(), 'the heading All while searching');
      await page.fill(search('pk-suggest'), '');
      expectTrue(await page.locator(`${panelOf('pk-suggest')} .mat-picker__group`).isVisible(), 'suggestions after the search was emptied');
      await page.click(`${panelOf('pk-suggest')} .mat-picker__group [data-value="contracts"]`);
      expect(await valueOf('pk-suggest'), 'contracts', 'the suggestion is the option');
      expectTrue(await page.locator(`${panelOf('pk-suggest')} .mat-picker__group`).isHidden(), 'suggestions after a choice');
      expectTrue(await page.locator(panelOf('pk-suggest')).isVisible(), 'the panel of several choices stays open');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-value')), 'contracts', 'the focus is on the option itself');
      expect(await shown('pk-suggest'), 'Contracts', 'the closed field');
    });
    await check('a suggested name that is not an option creates it: added, chosen, the panel stays open for several choices and the search starts again', async () => {
      await page.click(`${panelOf('pk-suggest')} [data-picker-action]`);
      await page.evaluate(() => MatUi.picker.set('#pk-suggest', ''));
      await open1('pk-suggest');
      const [request] = await Promise.all([page.waitForRequest('**/api/demo/create'), page.click(`${panelOf('pk-suggest')} .mat-picker__group [data-suggest-name]`)]);
      expect(request.postData(), 'name=Invoices+2026', 'the request');
      await page.waitForFunction(() => /n-/.test(document.getElementById('pk-suggest').value));
      expectTrue(/^n-[0-9a-f]{8}$/.test(await valueOf('pk-suggest')), 'one new key: ' + await valueOf('pk-suggest'));
      expectTrue(await page.locator(panelOf('pk-suggest')).isVisible(), 'the panel closed');
      expect(await page.inputValue(search('pk-suggest')), '', 'the search');
      expect(await shown('pk-suggest'), 'Invoices 2026', 'the field');
      await page.click(`${panelOf('pk-suggest')} .mat-picker__option[data-value="finance"]:not(.mat-picker__option--suggest)`);
      expectTrue(/^finance, n-[0-9a-f]{8}$/.test(await valueOf('pk-suggest')), 'the keys stay in the order of the list: ' + await valueOf('pk-suggest'));
      expect(await shown('pk-suggest'), 'Finance, Invoices 2026', 'the field');
      await page.click(`${panelOf('pk-suggest')} [data-picker-action]`);
    });
    await check('the arrow keys skip suggestions that are hidden, a suggested name that was created is that option, and Enter in an empty search never creates a suggested name', async () => {
      await open1('pk-suggest');
      // two values are chosen, so there are no suggestions: Home stays on the list
      await page.focus(`${panelOf('pk-suggest')} .mat-picker__option[data-value="family"]:not(.mat-picker__option--suggest)`);
      await page.keyboard.press('Home');
      expect(await page.evaluate(() => [document.activeElement.getAttribute('data-value'), document.activeElement.classList.contains('mat-picker__option--suggest')]), ['finance', false], 'Home');
      await page.evaluate(() => MatUi.picker.set('#pk-suggest', ''));
      // the name that was created from the suggestion is an option now, and so it is suggested as the option
      expect(await page.locator(`${panelOf('pk-suggest')} .mat-picker__group [data-suggest-name]`).count(), 0, 'the created name is still a suggestion to create');
      expect(await page.locator(`${panelOf('pk-suggest')} .mat-picker__group .mat-picker__option`).count(), 3, 'three suggestions: two options and the one that was created');
      // typed: the Create row first, then the first match and not a suggestion that is not shown
      await page.fill(search('pk-suggest'), 'fam');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.classList.contains('mat-picker__create')), true, 'the Create row is first');
      await page.keyboard.press('ArrowDown');
      expect(await page.evaluate(() => document.activeElement.getAttribute('data-value')), 'family', 'then the first match');
      // an empty search and Enter: the first suggestion, an option that is there
      await page.fill(search('pk-suggest'), '');
      await page.focus(search('pk-suggest'));
      const options = await page.locator(`${panelOf('pk-suggest')} .mat-picker__option[data-value]:not(.mat-picker__option--suggest)`).count();
      await page.keyboard.press('Enter');
      expect(await valueOf('pk-suggest'), 'finance', 'Enter chose the first suggestion');
      expect(await page.locator(`${panelOf('pk-suggest')} .mat-picker__option[data-value]:not(.mat-picker__option--suggest)`).count(), options, 'nothing was created');
      await page.click(`${panelOf('pk-suggest')} [data-picker-action]`);
    });
    await check('suggestions without create: only options that are there; a click chooses and closes; a name that is not an option is ignored', async () => {
      await open1('pk-folder');
      await page.waitForSelector(`${panelOf('pk-folder')} .mat-picker__group:not([hidden])`);
      expect((await page.locator(`${panelOf('pk-folder')} .mat-picker__group .mat-picker__name`).allTextContents()).map(t => t.trim()), ['Documents', 'Photos'], 'the suggestions');
      expect(await page.locator(`${panelOf('pk-folder')} .mat-picker__create`).count(), 0, 'no create row without data-create-url');
      await page.click(`${panelOf('pk-folder')} .mat-picker__group [data-value="photos"]`);
      expect(await valueOf('pk-folder'), 'photos', 'the value');
      expectTrue(!(await page.locator(panelOf('pk-folder')).isVisible()), 'the panel is still shown');
      const counted = await page.evaluate(() => MatUi.picker.suggest('#pk-folder', [{ value: 'receipts' }, { value: 'nothing' }, { name: 'A new one' }, { name: 'MANUALS' }, { value: 'receipts' }, 'text', null]));
      expect(counted, 2, 'receipts and the name of manuals; a missing option and a new name are ignored');
    });
    await check('in a form: required with the message at the field; the row has the wording of the field; a created option is a real one and takes the message away', async () => {
      await page.evaluate(() => document.getElementById('pk-corr-trigger').scrollIntoView({ block: 'center' }));
      await page.click('form[action="/Components#pk-corr"] button[type=submit]');
      await page.waitForTimeout(200);
      expect(await page.locator('#pk-corr').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field is invalid');
      expect((await page.locator('#pk-corr').evaluate(el => el.closest('.mat-field').querySelector('.mat-error').textContent)).trim(), 'Please choose or create a correspondent.', 'the message');
      await page.click('#pk-corr-trigger');
      await page.fill(search('pk-corr'), 'Tax office');
      expect((await page.textContent(createRow('pk-corr'))).trim(), 'Create correspondent "Tax office"', 'the wording of the field');
      await page.click(createRow('pk-corr'));
      await page.waitForFunction(() => document.getElementById('pk-corr').value !== '');
      expect(await page.locator('#pk-corr').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), false, 'the message after the choice');
      expect(await shown('pk-corr'), 'Tax office', 'the field');
    });
    await check('from code: set chooses a value and fires change, addOption makes an option from text (never markup), suggest shows what a page fetched', async () => {
      await page.evaluate(() => { window.__changes = []; MatUi.picker.set('#pk-corr', 'family'); });
      expect([await valueOf('pk-corr'), await shown('pk-corr'), await page.evaluate(() => window.__changes)], ['family', 'Family', ['pk-corr=family']], 'set');
      await page.evaluate(() => MatUi.picker.set(document.getElementById('pk-corr').closest('.mat-picker'), null));
      expect(await shown('pk-corr'), '3 suggestions', 'set to nothing: nothing is chosen and the field has been asked for suggestions (the ones of the form above)');
      const made = await page.evaluate(() => { const o = MatUi.picker.addOption('#pk-corr', { value: 'x1', name: '<b>Bold</b> & co', description: '<i>d</i>', tags: ['<u>t</u>'] }); return { html: o.innerHTML.includes('<b>'), name: o.querySelector('.mat-picker__name').textContent, tag: o.querySelector('.mat-tag').textContent, again: MatUi.picker.addOption('#pk-corr', { value: 'x1', name: 'other' }) === o }; });
      expect(made, { html: false, name: '<b>Bold</b> & co', tag: '<u>t</u>', again: true }, 'addOption');
      await page.evaluate(() => MatUi.picker.set('#pk-corr', 'x1'));
      expect(await shown('pk-corr'), '<b>Bold</b> & co', 'the new option is shown as text');
      await page.evaluate(() => MatUi.picker.set('#pk-corr', ''));
      expect(await page.evaluate(() => MatUi.picker.suggest('#pk-corr', { items: [{ value: 'contracts' }, { name: 'Brand new' }, { name: 'family' }] })), 3, 'suggest takes { items } and names that exist or can be created');
      expect(await shown('pk-corr'), '3 suggestions', 'the closed field');
      expect(await page.evaluate(() => MatUi.picker.suggest('#pk-corr', [{ value: 'contracts' }])), 1, 'suggest again replaces the old ones');
      expect(await shown('pk-corr'), '1 suggestion', 'singular');
      expect(await page.evaluate(() => MatUi.picker.suggest('#pk-corr', [])), 0, 'no suggestions');
      expect(await shown('pk-corr'), 'Choose a correspondent', 'the placeholder is back');
    });
    // the two refusals above are on purpose; the browser writes a request that failed into the console as an error
    for (let i = consoleErrors.length - 1; i >= 0; i--) {
      if (consoleErrors[i].startsWith('/Components#rich-select: ') && /status of 422|ERR_FAILED/.test(consoleErrors[i])) { consoleErrors.splice(i, 1); }
    }
    await check('the texts are English or German by lang and each can be replaced: data-create-label, data-pk-<name> on the field or on html', async () => {
      await page.evaluate(() => { document.documentElement.lang = 'de'; MatUi.picker.suggest('#pk-corr', [{ value: 'contracts' }, { value: 'family' }]); });
      expect(await shown('pk-corr'), '2 Vorschläge', 'German count');
      await page.evaluate(() => MatUi.picker.suggest('#pk-corr', [{ value: 'contracts' }]));
      expect(await shown('pk-corr'), '1 Vorschlag', 'German singular');
      await page.evaluate(id => document.getElementById(id + '-trigger').scrollIntoView({ block: 'center' }), 'pk-create');
      await page.click('#pk-create-trigger');
      await page.fill(search('pk-create'), 'Neu');
      expect((await page.textContent(createRow('pk-create'))).trim(), 'Anlegen: "Neu"', 'the German row');
      await page.evaluate(() => document.getElementById('pk-create').closest('.mat-picker').setAttribute('data-pk-create', 'Neu anlegen: {0}'));
      await page.fill(search('pk-create'), 'Neu2');
      expect((await page.textContent(createRow('pk-create'))).trim(), 'Neu anlegen: Neu2', 'the text of the field');
      await page.evaluate(() => { document.documentElement.lang = 'en'; document.documentElement.setAttribute('data-pk-suggestions', 'Tips'); });
      await page.evaluate(() => MatUi.picker.suggest('#pk-corr', [{ value: 'contracts' }]));
      await page.keyboard.press('Escape');
      await page.click('#pk-corr-trigger');
      expect((await page.locator(`${panelOf('pk-corr')} .mat-picker__group-head`).first().textContent()).trim(), 'Tips', 'the text of html');
      await page.evaluate(() => document.documentElement.removeAttribute('data-pk-suggestions'));
      await page.keyboard.press('Escape');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#rich-select');
    await check('on a phone the panel with the Create row is a full-screen sheet; the keyboard does not pop up until the field is touched; the row can be pressed', async () => {
      await page.evaluate(() => document.getElementById('pk-create-trigger').scrollIntoView({ block: 'center' }));
      await page.click('#pk-create-trigger');
      const panel = '#pk-create-trigger ~ .mat-picker__panel';
      const sheet = await page.$eval(panel, el => { const r = el.getBoundingClientRect(); return { full: Math.round(r.width) === innerWidth && Math.round(r.height) === innerHeight }; });
      expect(sheet, { full: true }, 'the sheet');
      expectTrue(await page.evaluate(() => document.activeElement.type !== 'search'), 'the search field has the focus on a phone');
      await page.fill(panel + ' input[type=search]', 'Phone project');
      await page.click(panel + ' .mat-picker__create');
      await page.waitForFunction(() => document.getElementById('pk-create').value.startsWith('n-'));
      expect((await page.textContent('#pk-create-trigger .mat-picker__value')).trim(), 'Phone project', 'the field');
      expectTrue(!(await page.locator(panel).isVisible()), 'the sheet is still shown');
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'no sideways scroll');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- coloured marks (COLOR-10)
  console.log('\nColoured marks');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#pills');
    const swatchOf = sel => page.$eval(sel, el => el.style.getPropertyValue('--mat-swatch').trim());
    const dotOf = sel => page.$eval(sel, el => { const s = getComputedStyle(el, '::before'); return { content: s.content, width: s.width, height: s.height, radius: s.borderTopLeftRadius, background: s.backgroundColor }; });
    await check('a tag with data-colour gets a dot of that colour before its text; the colour is written into --mat-swatch (any case, #rgb too) and the text stays muted', async () => {
      expect(await swatchOf('.mat-tag[data-colour="#dc2626"]'), '#dc2626', 'the swatch');
      expect(await swatchOf('.mat-tag[data-colour="#fff"]'), '#ffffff', 'a short colour is written out');
      expect(await page.$eval('.mat-tag[data-colour="#dc2626"]', el => el.classList.contains('is-coloured')), true, 'the state class');
      const red = await dotOf('.mat-tag[data-colour="#dc2626"]');
      expect(red, { content: '""', width: '10px', height: '10px', radius: '50%', background: 'rgb(220, 38, 38)' }, 'the dot');
      const plain = await page.$eval('.mat-tag:not([data-colour])', el => ({ dot: getComputedStyle(el, '::before').content, colour: getComputedStyle(el).color }));
      expect(plain.dot, 'none', 'a tag without a colour has no dot');
      const text = await page.$eval('.mat-tag[data-colour="#dc2626"]', el => getComputedStyle(el).color);
      expect(text, plain.colour, 'the text of a coloured tag is the colour of any tag: it is never derived from the colour');
      const white = await dotOf('.mat-tag[data-colour="#fff"]');
      expect(white.background, 'rgb(255, 255, 255)', 'a white dot');
    });
    await check('a dot has 10 px, a colour or a neutral grey; an icon takes the colour; a ring in the colour of the text keeps white and black dots visible in both modes', async () => {
      const dot = await page.$eval('.mat-dot[data-colour="#d97706"]', el => { const s = getComputedStyle(el); return { w: s.width, h: s.height, r: s.borderTopLeftRadius, bg: s.backgroundColor, ring: s.boxShadow }; });
      expect([dot.w, dot.h, dot.r, dot.bg], ['10px', '10px', '50%', 'rgb(217, 119, 6)'], 'the dot');
      expectTrue(/inset/.test(dot.ring), 'the ring: ' + dot.ring);
      const neutral = await page.$eval('.mat-dot:not([data-colour])', el => [getComputedStyle(el).backgroundColor, getComputedStyle(document.documentElement).getPropertyValue('--mat-text-faint').trim()]);
      expectTrue(neutral[0] === 'rgb(133, 130, 122)', 'the neutral dot is the faint grey: ' + neutral[0]);
      const icon = await page.$eval('.mat-icon[data-colour="#7c3aed"]', el => ({ colour: getComputedStyle(el).color, stroke: getComputedStyle(el).stroke }));
      expect(icon.colour, 'rgb(124, 58, 237)', 'the icon is in that colour');
      for (const theme of ['light', 'dark']) {
        await page.evaluate(t => document.documentElement.setAttribute('data-theme', t), theme);
        const black = await page.$eval('.mat-tag[data-colour="#000000"]', el => { const s = getComputedStyle(el, '::before'); return [s.backgroundColor, s.boxShadow]; });
        expect(black[0], 'rgb(0, 0, 0)', 'a black dot in ' + theme);
        const ring = black[1].match(/rgba?\([^)]*\)|color\([^)]*\)/)[0];
        expectTrue(ring.length > 0 && !/ 0\)$/.test(ring), 'the ring in ' + theme + ': ' + black[1]);
      }
      await page.evaluate(() => document.documentElement.removeAttribute('data-theme'));
    });
    await check('what is not #rgb or #rrggbb is ignored (no dot, no class, nothing inherited) and the table of the unit test gives the same answers here', async () => {
      const table = [['#abc', true], ['#ABC', true], ['#aBc', true], ['#aabbcc', true], ['#AABBCC', true], ['#d6336c', true], ['#000', true], ['#fff', true],
        ['abc', false], ['aabbcc', false], ['#ab', false], ['#abcd', false], ['#abcde', false], ['#aabbccd', false], ['#aabbccdd', false], ['#ggg', false], ['#12345g', false],
        ['red', false], ['rgb(0, 0, 0)', false], ['', false], [' #abc', false], ['#abc ', false], ['url(#abc)', false]];
      const answers = await page.evaluate(values => {
        const box = document.createElement('div');
        box.innerHTML = values.map(([v]) => '<span class="mat-tag" data-colour="' + v.replace(/"/g, '&quot;') + '">x</span>').join('');
        // inside something that has a colour of its own: an invalid mark must not take it over
        box.setAttribute('style', '--mat-swatch: #ff00ff');
        document.querySelector('.mat-page').appendChild(box);
        MatUi.refresh(box);
        const out = [...box.children].map(el => [el.classList.contains('is-coloured'), el.style.getPropertyValue('--mat-swatch').trim(), getComputedStyle(el, '::before').content]);
        box.remove();
        return out;
      }, table);
      table.forEach(([value, valid], i) => {
        const [coloured, swatch, content] = answers[i];
        expect([coloured, content === '""'], [valid, valid], `"${value}" is ${valid ? '' : 'not '}a colour`);
        if (valid) {
          const h = value.slice(1).toLowerCase();
          expect(swatch, '#' + (h.length === 3 ? [...h].map(c => c + c).join('') : h), `the swatch of "${value}"`);
        } else {
          expect(swatch, 'initial', `"${value}" does not take the colour of what is around it`);
        }
      });
    });
    await check('markup that is added later needs MatUi.refresh(scope); a colour that changes or goes away is followed', async () => {
      const steps = await page.evaluate(() => {
        const box = document.createElement('div');
        box.innerHTML = '<span class="mat-tag" id="late-tag" data-colour="#123456">Late</span>';
        document.querySelector('.mat-page').appendChild(box);
        const tag = document.getElementById('late-tag');
        const state = () => [tag.classList.contains('is-coloured'), tag.style.getPropertyValue('--mat-swatch').trim()];
        const before = state();
        MatUi.refresh(box);
        const after = state();
        tag.setAttribute('data-colour', '#abc');
        MatUi.refresh(box);
        const changed = state();
        tag.setAttribute('data-colour', 'nope');
        MatUi.refresh(box);
        const ignored = state();
        const other = document.createElement('span');
        other.className = 'mat-tag';
        other.setAttribute('data-colour', '#fedcba');
        box.appendChild(other);
        MatUi.refresh(box);
        const had = [other.classList.contains('is-coloured'), other.style.getPropertyValue('--mat-swatch').trim()];
        other.removeAttribute('data-colour');
        MatUi.refresh(box);
        const gone = [other.classList.contains('is-coloured'), other.style.getPropertyValue('--mat-swatch').trim()];
        box.remove();
        return { before, after, changed, ignored, had, gone };
      });
      expect(steps, { before: [false, ''], after: [true, '#123456'], changed: [true, '#aabbcc'], ignored: [false, 'initial'], had: [true, '#fedcba'], gone: [false, ''] }, 'the steps');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components#rich-select');
    await check('an option of a picker with data-colour has the dot in the list and in the closed field; addOption and the answer of the create endpoint take a colour', async () => {
      const dotIn = sel => page.$eval(sel, el => getComputedStyle(el, '::before').backgroundColor);
      await page.evaluate(() => document.getElementById('pk-create-trigger').scrollIntoView({ block: 'center' }));
      await page.click('#pk-create-trigger');
      expect(await dotIn('#pk-create-trigger ~ .mat-picker__panel [data-value="website"]'), 'rgb(37, 99, 235)', 'the dot of the option in the list');
      await page.click('#pk-create-trigger ~ .mat-picker__panel [data-value="website"]');
      expect(await page.$eval('#pk-create-trigger .mat-picker__value .mat-dot', el => getComputedStyle(el).backgroundColor), 'rgb(37, 99, 235)', 'the dot in the closed field');
      expect((await page.textContent('#pk-create-trigger .mat-picker__value')).trim(), 'Website', 'and the name, the dot is no text');
      expect(await page.getAttribute('#pk-create-trigger .mat-picker__value .mat-dot', 'aria-hidden'), 'true', 'the dot is decoration');
      await page.evaluate(() => MatUi.picker.addOption('#pk-create', { value: 'c-red', name: 'Red thing', colour: '#ff0000' }));
      await page.click('#pk-create-trigger');
      expect(await dotIn('#pk-create-trigger ~ .mat-picker__panel [data-value="c-red"]'), 'rgb(255, 0, 0)', 'addOption with a colour');
      await page.route('**/api/demo/create', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ value: 'c-green', name: 'Green thing', colour: '#00ff00' }) }));
      try {
        await page.fill('#pk-create-trigger ~ .mat-picker__panel input[type=search]', 'Green thing');
        await page.click('#pk-create-trigger ~ .mat-picker__panel .mat-picker__create');
        await page.waitForFunction(() => document.getElementById('pk-create').value === 'c-green');
      } finally {
        await page.unroute('**/api/demo/create');
      }
      expect(await page.$eval('#pk-create-trigger .mat-picker__value .mat-dot', el => getComputedStyle(el).backgroundColor), 'rgb(0, 255, 0)', 'a created option with the colour of the answer');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- layout and utilities
  console.log('\nLayout and utilities');
  {
    const { ctx, page } = await open(desktopOptions, '/Layout#grid-cols');
    const rowWidths = () => page.$$eval('#grid-cols .doc-example:first-of-type .mat-cols', rows => rows.map(r => [...r.children].map(c => c.getBoundingClientRect().width)));
    await check('the twelve columns divide a row: halves, thirds, 8 and 4, quarters', async () => {
      const rows = await rowWidths();
      expect(rows.map(r => r.length), [2, 3, 2, 4], 'cells per row');
      expectTrue(Math.abs(rows[0][0] - rows[0][1]) < 1.5, 'the two halves are not equal');
      expectTrue(Math.abs(rows[1][0] - rows[1][2]) < 1.5, 'the thirds are not equal');
      expectTrue(rows[2][0] / rows[2][1] > 1.8 && rows[2][0] / rows[2][1] < 2.3, `8 to 4 is not about 2 to 1: ${rows[2][0]} / ${rows[2][1]}`);
      expectTrue(Math.abs(rows[3][0] - rows[3][3]) < 1.5, 'the quarters are not equal');
    });
    await check('on a wide screen the responsive cells are four in a row; an offset moves a column; the order class changes the picture', async () => {
      const responsive = await page.$$eval('#grid-cols .doc-example:nth-of-type(2) .mat-cols > *', cells => cells.map(c => Math.round(c.getBoundingClientRect().top)));
      expect(new Set(responsive).size, 1, 'rows of the four responsive cells');
      const offset = await page.$eval('#grid-cols .mat-col-start-4', el => { const r = el.getBoundingClientRect(), p = el.parentElement.getBoundingClientRect(); return Math.round((r.left - p.left) / p.width * 100); });
      expectTrue(offset > 20 && offset < 30, 'the offset column does not start a quarter in: ' + offset + ' %');
      const order = await page.$eval('#grid-cols .mat-order-last', el => { const sibling = el.nextElementSibling || el.previousElementSibling; return el.getBoundingClientRect().left > sibling.getBoundingClientRect().left; });
      expect(order, true, 'the cell with mat-order-last is on the right');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Utilities');
    await check('utilities set their property from the scale: padding, an auto margin that centres, a gap, truncation', async () => {
      expect(await page.$eval('#u-spacing .mat-p-4', el => getComputedStyle(el).paddingLeft), '16px', 'mat-p-4');
      const centred = await page.$eval('#u-spacing .mat-mx-auto', el => { const r = el.getBoundingClientRect(), p = el.parentElement.getBoundingClientRect(); return Math.abs((r.left - p.left) - (p.right - r.right)) < 1.5; });
      expect(centred, true, 'mat-mx-auto mat-w-50 is centred');
      expect(await page.$eval('#u-spacing .mat-gap-6', el => getComputedStyle(el).columnGap), '24px', 'mat-gap-6');
      const cut = await page.$eval('#u-text .mat-text-truncate', el => ({ ellipsis: getComputedStyle(el).textOverflow, cut: el.scrollWidth > el.clientWidth }));
      expect(cut, { ellipsis: 'ellipsis', cut: true }, 'mat-text-truncate');
    });
    await check('the width classes show and hide: below 900 px only, from 900 px, and a column that becomes a row', async () => {
      expect(await page.$eval('#u-display .mat-d-md-none', el => getComputedStyle(el).display), 'none', 'mat-d-md-none on a wide screen');
      expect(await page.$eval('#u-display .mat-d-md-block', el => getComputedStyle(el).display), 'block', 'mat-d-md-block on a wide screen');
      expect(await page.$eval('#u-flex .mat-flex-md-row', el => getComputedStyle(el).flexDirection), 'row', 'mat-flex-md-row on a wide screen');
      await page.setViewportSize({ width: 700, height: 900 });
      expect(await page.$eval('#u-display .mat-d-md-none', el => getComputedStyle(el).display), 'block', 'mat-d-md-none on a narrow screen');
      expect(await page.$eval('#u-display .mat-d-md-block', el => getComputedStyle(el).display), 'none', 'mat-d-md-block on a narrow screen');
      expect(await page.$eval('#u-flex .mat-flex-md-row', el => getComputedStyle(el).flexDirection), 'column', 'the same box on a narrow screen');
    });
    await check('tints bring a readable text colour; the text colours are the status colours', async () => {
      await page.setViewportSize({ width: 1360, height: 900 });
      const same = await page.$eval('#u-colour .mat-tint-danger', el => getComputedStyle(el).color !== getComputedStyle(document.body).color && getComputedStyle(el).backgroundColor !== 'rgba(0, 0, 0, 0)');
      expect(same, true, 'mat-tint-danger sets background and colour');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Layout#grid-cols');
    const share = sel => page.$$eval(sel, rs => rs.map(r => [...r.children].map(c => Math.round(c.getBoundingClientRect().width / r.getBoundingClientRect().width * 100))));
    await check('on a phone mat-col-12 mat-col-sm-6 mat-col-lg-3 is one column, and a class without a breakpoint keeps its share', async () => {
      const responsive = await share('#grid-cols .doc-example:nth-of-type(2) .mat-cols');
      expect(responsive.flat().every(w => w >= 99), true, 'the cells that change with the screen: ' + JSON.stringify(responsive));
      const fixed = await share('#grid-cols .doc-example:first-of-type .mat-cols');
      expectTrue(fixed[0][0] < 55, 'mat-col-6 is not half of the row on a phone: ' + JSON.stringify(fixed[0]));
    });
    await check('on a phone the index of the page is an icon in the header bar that opens a dialog', async () => {
      await page.click('.mat-topbar__index');
      expectTrue(await page.locator('#toc-dialog[open]').count() === 1, 'the index dialog is not open');
      expect(await page.locator('#toc-dialog a.mat-toc__link').count() >= 3, true, 'links in the dialog');
      await page.click('#toc-dialog [data-dialog-close]');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the header bar with icons, the footer
  console.log('\nHeader bar and footer');
  {
    const { ctx, page } = await open(desktopOptions, '/Components');
    await check('the icons of the header bar are at its right end, after the breadcrumb, and each has a name and a tooltip (SHELL-17, ICON-03)', async () => {
      const bar = page.locator('#topbar .mat-topbar').first();
      await bar.scrollIntoViewIfNeeded();
      const box = await bar.boundingBox();
      const crumbs = await bar.locator('.mat-crumbs').boundingBox();
      const actions = await bar.locator('.mat-topbar__actions').boundingBox();
      expectTrue(actions.x >= crumbs.x + crumbs.width - 1, 'the icons start before the breadcrumb ends');
      expectTrue(box.x + box.width - (actions.x + actions.width) < 40, 'the icons are not at the right end of the bar: ' + (box.x + box.width - (actions.x + actions.width)) + ' px from it');
      const buttons = bar.locator('.mat-topbar__actions .mat-btn');
      expectTrue(await buttons.count() >= 2, 'fewer than two icon buttons');
      for (let i = 0; i < await buttons.count(); i++) {
        const b = buttons.nth(i);
        expectTrue((await b.getAttribute('aria-label') || '').length > 0, 'an icon button without a name');
        expectTrue((await b.getAttribute('data-tip') || '').length > 0, 'an icon button without a tooltip');
        expectTrue((await b.textContent()).trim().length === 0, 'an icon button that has text');
      }
    });
    await check('a count on an icon sits at its corner and is hidden from screen readers, the name of the button says it', async () => {
      const button = page.locator('#topbar .mat-topbar__actions .mat-btn', { has: page.locator('.mat-topbar__badge:not(.mat-topbar__badge--dot)') }).first();
      const badge = button.locator('.mat-topbar__badge');
      expect(await badge.getAttribute('aria-hidden'), 'true', 'the badge is hidden from screen readers');
      expectTrue((await button.getAttribute('aria-label')).includes(await badge.textContent()), 'the name of the button does not say the count');
      const b = await button.boundingBox();
      const c = await badge.boundingBox();
      expectTrue(c.x + c.width <= b.x + b.width + 1 && c.y >= b.y - 1, 'the badge is not at the top right corner of its button');
    });
    await check('the footer comes after the content, lines up with it, and is at the bottom of a short page (SHELL-18)', async () => {
      // a window taller than the page makes it a short page
      await page.setViewportSize({ width: 1360, height: 2600 });
      await page.goto(BASE + '/Rules/Detail?id=SHELL-18', { waitUntil: 'networkidle' });
      const footer = page.locator('.mat-main > footer.mat-footer');
      expect(await footer.count(), 1, 'footers after the content');
      expect(await page.locator('.mat-main > main.mat-page + footer.mat-footer').count(), 1, 'the footer is not right after the main');
      const f = await footer.boundingBox();
      const h1 = await page.locator('main h1').boundingBox();
      const text = await footer.locator('.mat-footer__text').boundingBox();
      expectTrue(Math.abs(text.x - h1.x) < 1.5, 'the footer text does not line up with the title: ' + text.x + ' / ' + h1.x);
      const links = await footer.locator('.mat-footer__links').boundingBox();
      const main = await page.locator('main.mat-page').boundingBox();
      expectTrue(links.x + links.width > main.x + main.width - 40, 'the links are not at the right end');
      const viewport = page.viewportSize().height;
      expectTrue(Math.abs(f.y + f.height - viewport) < 2, 'a short page does not end with the footer at the window bottom: ' + (f.y + f.height) + ' / ' + viewport);
      expect(await footer.evaluate(el => getComputedStyle(el).position), 'static', 'the footer is fixed over the content');
      await page.setViewportSize(desktopOptions.viewport);
    });
    await check('the footer of a page without a menu is centred under the card and the card stays centred (SHELL-11)', async () => {
      await page.goto(BASE + '/Preview?app=matmail&view=auth', { waitUntil: 'networkidle' });
      const card = await page.locator('.mat-auth__card').boundingBox();
      const footer = await page.locator('.mat-auth > .mat-footer').boundingBox();
      const viewport = page.viewportSize();
      expectTrue(footer.y >= card.y + card.height, 'the footer is not under the card');
      expectTrue(Math.abs(footer.y + footer.height - viewport.height) < 2, 'the footer is not at the bottom: ' + (footer.y + footer.height) + ' / ' + viewport.height);
      expectTrue(Math.abs((card.x + card.width / 2) - viewport.width / 2) < 2, 'the card is not centred');
      const links = await page.locator('.mat-auth > .mat-footer .mat-footer__links').boundingBox();
      expectTrue(Math.abs((links.x + links.width / 2) - viewport.width / 2) < 2, 'the links are not centred');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components');
    await check('on a phone the icons stay inside the bar, the breadcrumb gives way, nothing scrolls sideways (MOBILE-01)', async () => {
      const bar = page.locator('#topbar .mat-topbar').first();
      await bar.scrollIntoViewIfNeeded();
      const box = await bar.boundingBox();
      const actions = await bar.locator('.mat-topbar__actions').boundingBox();
      const crumbs = await bar.locator('.mat-crumbs').boundingBox();
      expectTrue(actions.x + actions.width <= box.x + box.width + 1, 'the icons leave the bar');
      expectTrue(actions.x >= crumbs.x + crumbs.width - 1, 'the icons run over the breadcrumb');
      const widths = await page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth }));
      expectTrue(widths.page <= widths.window, 'the page scrolls sideways');
    });
    await check('on a phone the two sides of the footer wrap and stay inside the window', async () => {
      await page.goto(BASE + '/Rules/Detail?id=SHELL-18', { waitUntil: 'networkidle' });
      const footer = page.locator('.mat-main > footer.mat-footer');
      await footer.scrollIntoViewIfNeeded();
      const f = await footer.boundingBox();
      const text = await footer.locator('.mat-footer__text').boundingBox();
      const links = await footer.locator('.mat-footer__links').boundingBox();
      expectTrue(f.x >= 0 && f.x + f.width <= 391, 'the footer is wider than the window');
      expectTrue(links.y >= text.y + text.height - 1, 'the links did not wrap below the text');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the toolbar with a search field, the index icon, the selected states
  console.log('\nToolbar field, index icon, selected states');
  {
    const { ctx, page } = await open(desktopOptions, '/Rules');
    await check('the search is a field in the toolbar at its left, Filter and Sort stand together at its right end (LIST-03)', async () => {
      const bar = await page.locator('.mat-toolbar').boundingBox();
      const field = await page.locator('.mat-toolbar__search input').boundingBox();
      const end = await page.locator('.mat-toolbar__end').boundingBox();
      expectTrue(field.x - bar.x < 20, 'the field is not at the left of the toolbar: ' + (field.x - bar.x) + ' px');
      expectTrue(bar.x + bar.width - (end.x + end.width) < 20, 'the buttons are not at the right end: ' + (bar.x + bar.width - (end.x + end.width)) + ' px');
      expect((await page.locator('.mat-toolbar__end .mat-toolbar__btn').allTextContents()).map(t => t.trim().split(/\s+/)[0]), ['Filter', 'Sort'], 'the buttons at the right end');
      expect(await page.locator('[data-dialog-open$="-search"]').count(), 0, 'a search button as well as the field');
      expect(await page.locator('.mat-toolbar__search .mat-icon').count(), 1, 'the icon in the field');
      expectTrue(Math.abs(field.height - end.height) < 3, 'the field and the buttons differ in height: ' + field.height + ' / ' + end.height);
    });
    await check('Enter sends the search as a GET together with the filter in effect, the field keeps its term and only the filter is a chip', async () => {
      await page.click('[data-dialog-open="rules-filter"]');
      await page.selectOption('#rules-f-area', 'LIST');
      await Promise.all([page.waitForNavigation(), page.click('#rules-filter button[type=submit]')]);
      await page.fill('.mat-toolbar__search input', 'toolbar');
      await Promise.all([page.waitForNavigation(), page.press('.mat-toolbar__search input', 'Enter')]);
      const url = new URL(page.url());
      expect(url.searchParams.get('q'), 'toolbar', 'the search in the address');
      expect(url.searchParams.get('area'), 'LIST', 'the filter in the address');
      expect(await page.inputValue('.mat-toolbar__search input'), 'toolbar', 'the term in the field');
      expect(await page.locator('.mat-chip').count(), 1, 'chips');
    });
    await check('the slash puts the cursor in the search field, and is a character while one is typing', async () => {
      await page.goto(BASE + '/Rules', { waitUntil: 'networkidle' });
      await page.keyboard.press('/');
      expect(await page.evaluate(() => document.activeElement.id), 'rules-q', 'the focused element');
      expect(await page.inputValue('#rules-q'), '', 'the slash was typed into the field');
      await page.keyboard.type('a/b');
      expect(await page.inputValue('#rules-q'), 'a/b', 'the text');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components#toolbar');
    await check('the toolbar example with the field is live: Filter and Sort sit at the right end and open their dialogs', async () => {
      const field = page.locator('#tb2-q');
      await field.scrollIntoViewIfNeeded();
      expect(await field.evaluate(el => !!el.closest('.mat-toolbar__search') && !!el.closest('.mat-toolbar')), true, 'the field is in the toolbar');
      const bar = await page.locator('#tb2-q').locator('xpath=ancestor::div[contains(@class,"mat-toolbar")][1]').boundingBox();
      const end = await page.locator('.mat-toolbar__end').boundingBox();
      expectTrue(bar.x + bar.width - (end.x + end.width) < 20, 'the buttons are not at the right end');
      await page.click('[data-dialog-open="tb2-sort"]');
      expectTrue(await page.locator('#tb2-sort').evaluate(d => d.open), 'the sort dialog did not open');
      await page.click('#tb2-sort [data-dialog-close]');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Rules');
    await check('on a phone the search field takes the first row and Filter and Sort stand right-aligned in the next, nothing scrolls sideways', async () => {
      const field = await page.locator('.mat-toolbar__search input').boundingBox();
      const end = await page.locator('.mat-toolbar__end').boundingBox();
      const bar = await page.locator('.mat-toolbar').boundingBox();
      expectTrue(field.width > 390 - 60, 'the field is narrow: ' + field.width);
      expectTrue(end.y >= field.y + field.height - 1, 'the buttons are not below the field');
      expectTrue(bar.x + bar.width - (end.x + end.width) < 20, 'the buttons are not at the right end of the toolbar: ' + (bar.x + bar.width - (end.x + end.width)));
      const widths = await page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth }));
      expectTrue(widths.page <= widths.window, 'the page scrolls sideways');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components');
    await check('from 1100 px the index is beside the content and there is no icon for it in the header bar', async () => {
      expectTrue(await page.locator('.mat-toc-aside').isVisible(), 'the index is not beside the content');
      expectTrue(!(await page.locator('.mat-topbar__index').isVisible()), 'the index icon is drawn next to the index');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open({ viewport: { width: 1000, height: 800 } }, '/Components');
    await check('below 1100 px the icon sits at the right end of the header bar, stays in reach when the page is scrolled and opens the index in a dialog', async () => {
      expectTrue(!(await page.locator('.mat-toc-aside').isVisible()), 'the index is beside the content');
      const icon = page.locator('.mat-topbar__index');
      expectTrue(await icon.isVisible(), 'no index icon');
      expect(await icon.getAttribute('aria-label'), 'On this page, jump to a section', 'the name');
      expect(await icon.getAttribute('data-tip'), 'On this page', 'the tooltip');
      expect(await page.locator('.mat-main .mat-topbar .mat-toc-bar, .mat-page .mat-toc-bar').count(), 0, 'a button in the content');
      const bar = await page.locator('.mat-main > .mat-topbar').boundingBox();
      const box = await icon.boundingBox();
      expectTrue(bar.x + bar.width - (box.x + box.width) < 40, 'the icon is not at the right end of the bar');
      await page.evaluate(() => document.getElementById('toolbar').scrollIntoView());
      await page.waitForTimeout(300);
      const after = await icon.boundingBox();
      expectTrue(after.y >= 0 && after.y < 60, 'the icon scrolled away: ' + after.y);
      await icon.click();
      await page.waitForTimeout(350);
      expectTrue(await page.locator('#toc-dialog').evaluate(d => d.open), 'the dialog did not open');
      await page.click('#toc-dialog a[href="#table"]');
      await page.waitForTimeout(500);
      expect(await page.evaluate(() => document.getElementById('toc-dialog').open), false, 'the dialog after the jump');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components');
    const clear = 'rgba(0, 0, 0, 0)';
    await check('the current item of the menu is a tint behind the whole item: no bar at one edge (COLOR-08)', async () => {
      const nav = await page.evaluate(() => {
        const el = document.querySelector('.mat-nav__item[aria-current="page"], .mat-nav__item.is-active');
        const cs = getComputedStyle(el);
        const before = getComputedStyle(el, '::before');
        return { bg: cs.backgroundColor, shadow: cs.boxShadow, radius: cs.borderTopLeftRadius, before: before.content };
      });
      expectTrue(nav.bg !== clear, 'the current item has no tint');
      expect(nav.shadow, 'none', 'a shadow at the current item');
      expectTrue(nav.before === 'none' || nav.before === 'normal', 'a bar is drawn for the current item: ' + nav.before);
      expectTrue(parseFloat(nav.radius) > 0, 'the current item is not rounded');
    });
    await check('the entry of the index in view is a tint too, and so is a selected vertical tab; the tabs of a row are square under a line', async () => {
      await page.evaluate(() => document.getElementById('toolbar').scrollIntoView());
      await page.waitForTimeout(400);
      const toc = await page.evaluate(() => { const el = document.querySelector('.mat-toc-aside .mat-toc__link.is-active'); const cs = getComputedStyle(el); return { bg: cs.backgroundColor, shadow: cs.boxShadow }; });
      expectTrue(toc.bg !== clear, 'the entry in view has no tint');
      expect(toc.shadow, 'none', 'a shadow at the entry in view');
      const tab = await page.evaluate(() => {
        const el = document.querySelector('.mat-tabs--vertical .mat-tab[aria-selected="true"], .mat-tabs--vertical .mat-tab.is-active');
        const cs = getComputedStyle(el);
        return { bg: cs.backgroundColor, right: cs.borderRightWidth, radius: cs.borderTopRightRadius };
      });
      expectTrue(tab.bg !== clear, 'the selected vertical tab has no tint');
      expect(tab.right, '0px', 'a border at one side of the vertical tab');
      expectTrue(parseFloat(tab.radius) > 0, 'the vertical tab is not rounded');
      const row = await page.evaluate(() => { const el = document.querySelector('.mat-tabs:not(.mat-tabs--pills):not(.mat-tabs--vertical) .mat-tab'); return getComputedStyle(el).borderTopLeftRadius; });
      expect(row, '0px', 'the corner of a tab in a row with an underline');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the code of the examples, the group in a page
  console.log('\nCode beside the examples, the group in a page');
  {
    const { ctx, page } = await open({ viewport: { width: 1920, height: 1000 } }, '/Components#buttons');
    await check('on a wide window the code is beside the example, coloured, and an example that needs the width has it below', async () => {
      const live = await page.locator('#ex-buttons-1').locator('xpath=ancestor::div[@role="group"][1]').locator('.doc-example__live').boundingBox();
      const code = await page.locator('#src-buttons-1').locator('xpath=..').boundingBox();
      expectTrue(code.x >= live.x + live.width - 2, 'the code is not beside the example: ' + code.x + ' / ' + (live.x + live.width));
      expectTrue(Math.abs(code.y - live.y) < 4, 'the code is not level with the example: ' + code.y + ' / ' + live.y);
      expectTrue(code.height > 40, 'the code has no height');
      const colours = await page.evaluate(() => {
        const tag = document.querySelector('#src-buttons-1 .mat-code__tag');
        return [getComputedStyle(tag).color, getComputedStyle(document.querySelector('#src-buttons-1')).color];
      });
      expectTrue(colours[0] !== colours[1], 'the tag has the colour of the text');
      const wideLive = await page.locator('#ex-toolbar-1').locator('xpath=ancestor::div[@role="group"][1]').locator('.doc-example__live').boundingBox();
      const wideCode = await page.locator('#src-toolbar-1').boundingBox();
      expectTrue(wideCode.y >= wideLive.y + wideLive.height - 2, 'the code of a toolbar is not below it');
    });
    await check('the code is never behind a click: there is no disclosure for it, and Copy puts exactly the code on the clipboard', async () => {
      expect(await page.locator('.doc-example__code, .doc-code details').count(), 0, 'disclosures around the code');
      await ctx.grantPermissions(['clipboard-read', 'clipboard-write']);
      await page.click('[data-copy="#src-buttons-1"]');
      await page.waitForTimeout(200);
      const copied = await page.evaluate(() => navigator.clipboard.readText());
      const shown = await page.evaluate(() => document.getElementById('src-buttons-1').textContent.trim());
      expect(copied.split('\r').join('').trim(), shown, 'the clipboard');
      expectTrue(copied.startsWith('<div class="mat-row">') && !copied.includes('mat-code__'), 'the copy is not the plain markup');
    });
    await check('"Below it" puts every code under its example and is kept after a reload', async () => {
      expectTrue(await page.locator('.doc-view').isVisible(), 'the choice is not offered on a wide window');
      await page.click('button[data-doc-code="below"]');
      const live = await page.locator('#ex-buttons-1').locator('xpath=ancestor::div[@role="group"][1]').locator('.doc-example__live').boundingBox();
      const code = await page.locator('#src-buttons-1').boundingBox();
      expectTrue(code.y >= live.y + live.height - 2, 'the code is not below the example');
      await page.reload({ waitUntil: 'networkidle' });
      expect(await page.getAttribute('button[data-doc-code="below"]', 'aria-pressed'), 'true', 'the choice after a reload');
      await page.click('button[data-doc-code="beside"]');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(desktopOptions, '/Components#buttons');
    await check('on a laptop window the code is below the example and the choice is not offered', async () => {
      const live = await page.locator('#ex-buttons-1').locator('xpath=ancestor::div[@role="group"][1]').locator('.doc-example__live').boundingBox();
      const code = await page.locator('#src-buttons-1').boundingBox();
      expectTrue(code.y >= live.y + live.height - 2, 'the code is not below the example');
      expectTrue(!(await page.locator('.doc-view').isVisible()), 'the choice is offered where it does nothing');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open({ viewport: { width: 1920, height: 1000 } }, '/Components#group-forms');
    const frameOf = async (group = 'group-forms') => (await page.locator('#context-' + group + ' iframe').elementHandle()).contentFrame();
    await check('the page of a group is not loaded until its panel is opened, then it is a real page with the shell', async () => {
      expect(await page.locator('#context-group-forms iframe').getAttribute('src'), null, 'the frame before it is opened');
      await page.click('#context-group-forms > summary');
      await page.waitForFunction(() => { const f = document.querySelector('#context-group-forms iframe'); return f && f.getAttribute('data-loaded') === 'true'; });
      const inside = await (await frameOf()).evaluate(() => ({ nav: !!document.querySelector('.mat-nav'), field: !!document.querySelector('.mat-field'), footer: !!document.querySelector('.mat-footer'), title: document.querySelector('h1').textContent }));
      expect(inside, { nav: true, field: true, footer: true, title: 'Edit item' }, 'the page in the frame');
    });
    await check('a press on a component outlines it in the page and a second press takes the outline away', async () => {
      await page.click('#context-group-forms [data-context-component="fields"]');
      const outlined = async () => (await frameOf()).evaluate(() => getComputedStyle(document.querySelector('.mat-field')).outlineStyle);
      expect(await outlined(), 'solid', 'the outline of a field');
      expect(await page.getAttribute('#context-group-forms [data-context-component="fields"]', 'aria-pressed'), 'true', 'pressed');
      await page.click('#context-group-forms [data-context-component="fields"]');
      expect(await outlined(), 'none', 'the outline after the second press');
    });
    await check('the window of the page can be a phone: the frame is 390 px wide and the page in it is the phone layout', async () => {
      await page.click('#context-group-forms [data-context-size="phone"]');
      const box = await page.locator('#context-group-forms iframe').boundingBox();
      expect(Math.round(box.width), 390, 'the width of the frame');
      expectTrue(await (await frameOf()).evaluate(() => innerWidth) <= 390, 'the page in the frame is wider than a phone');
      await page.click('#context-group-forms [data-context-size="desktop"]');
    });
    await check('"Show in a page" at a component opens the panel of its group and outlines the component there', async () => {
      await page.goto(BASE + '/Components#group-lists-and-data', { waitUntil: 'networkidle' });
      await page.waitForFunction(() => document.readyState === 'complete');
      await page.click('#toolbar [data-context-show]');
      await page.waitForFunction(() => { const f = document.querySelector('#context-group-lists-and-data iframe'); return f && f.getAttribute('data-loaded') === 'true'; });
      await page.waitForTimeout(300);
      expectTrue(await page.evaluate(() => document.getElementById('context-group-lists-and-data').open), 'the panel is not open');
      expect(await page.getAttribute('#context-group-lists-and-data [data-context-component="toolbar"]', 'aria-pressed'), 'true', 'the mark of the toolbar');
      expect(await (await frameOf('group-lists-and-data')).evaluate(() => getComputedStyle(document.querySelector('.mat-toolbar')).outlineStyle), 'solid', 'the outline of the toolbar');
    });
    await check('a component of another page loads that page first: the gallery is shown in the gallery view, a dialog opens before it is outlined', async () => {
      await page.click('#context-group-lists-and-data [data-context-component="gallery"]');
      await page.waitForFunction(() => document.querySelector('#context-group-lists-and-data iframe').getAttribute('data-view') === 'gallery' && document.querySelector('#context-group-lists-and-data iframe').getAttribute('data-loaded') === 'true');
      await page.waitForTimeout(300);
      expect(await (await frameOf('group-lists-and-data')).evaluate(() => getComputedStyle(document.querySelector('.mat-gallery')).outlineStyle), 'solid', 'the outline of the gallery');
      expectTrue((await page.getAttribute('#context-group-lists-and-data [data-context-open-page]', 'href')).includes('view=gallery'), 'the link to the page is still the table');
      await page.goto(BASE + '/Components#group-feedback', { waitUntil: 'networkidle' });
      await page.waitForFunction(() => document.readyState === 'complete');
      await page.click('#dialogs [data-context-show]');
      await page.waitForFunction(() => { const f = document.querySelector('#context-group-feedback iframe'); return f && f.getAttribute('data-loaded') === 'true'; });
      await page.waitForTimeout(500);
      expect(await (await frameOf('group-feedback')).evaluate(() => { const d = document.querySelector('dialog.mat-dialog[open]'); return d ? getComputedStyle(d).outlineStyle : 'no dialog'; }), 'solid', 'the dialog that was opened');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components');
    await check('on a phone the code is below the example, the classes are left out, and the page of a group fits the screen', async () => {
      const live = await page.locator('#ex-buttons-1').locator('xpath=ancestor::div[@role="group"][1]').locator('.doc-example__live').boundingBox();
      const code = await page.locator('#src-buttons-1').boundingBox();
      expectTrue(code.y >= live.y + live.height - 2, 'the code is not below the example');
      expectTrue(!(await page.locator('#buttons .doc-api').isVisible()), 'the classes take room on a phone');
      await page.evaluate(() => document.getElementById('context-group-actions').scrollIntoView());
      await page.click('#context-group-actions > summary');
      await page.waitForTimeout(600);
      const frame = await page.locator('#context-group-actions iframe').boundingBox();
      expectTrue(frame.width <= 390 && frame.x >= 0, 'the frame is wider than the screen: ' + frame.width);
      const widths = await page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth }));
      expectTrue(widths.page <= widths.window, 'the page scrolls sideways');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the icon library (optional files of their own)
  console.log('\nIcon library');
  {
    const { ctx, page } = await open(desktopOptions, '/Foundations/IconLibrary');
    await check('the library is a gallery with a search field in the toolbar, the categories beside it and the icons drawn from the sprite of the page', async () => {
      expect(await page.locator('.doc-icons .doc-icon').count(), 96, 'icons on the first page');
      expect(await page.locator('.mat-toolbar .mat-toolbar__search input').count(), 1, 'search field in the toolbar');
      expectTrue(await page.locator('.mat-toc-aside').isVisible(), 'the categories are not beside the list');
      expectTrue(!(await page.locator('.mat-topbar__index').isVisible()), 'the index icon is shown although the categories are beside the list');
      const sizes = await page.locator('.doc-icons .doc-icon .mat-icon').first().boundingBox();
      expectTrue(sizes.width > 16 && sizes.height > 16, 'the icon has no size: ' + JSON.stringify(sizes));
      const painted = await page.evaluate(() => { const s = document.querySelector('.doc-icons .doc-icon svg'); return getComputedStyle(s).stroke; });
      expectTrue(painted !== 'none', 'the icon is not drawn with a stroke');
    });
    await check('a category narrows the list, an icon can be in several, and the search finds by name and by what the icon is for', async () => {
      await page.click('.mat-toc-aside a:has-text("Lighting")');
      await page.waitForLoadState('networkidle');
      expectTrue(page.url().includes('category=lighting'), 'url: ' + page.url());
      expect(await page.textContent('.doc-iconlib__intro h2'), 'Lighting', 'the title of the category');
      expectTrue((await page.locator('.doc-icons .doc-icon').count()) > 20, 'too few lighting icons');
      expectTrue((await page.locator('.mat-toc-aside a.is-active').textContent()).includes('Lighting'), 'the category is not marked');
      await page.goto(BASE + '/Foundations/IconLibrary?q=heating', { waitUntil: 'networkidle' });
      expectTrue((await page.locator('.doc-icon[data-iconlib="radiator"]').count()) === 1, 'the radiator is not found by "heating"');
      await page.goto(BASE + '/Foundations/IconLibrary?q=thermometer&category=sensors', { waitUntil: 'networkidle' });
      expectTrue((await page.locator('.doc-icon[data-iconlib="thermometer"]').count()) === 1, 'the thermometer is not in the sensors');
    });
    await check('an icon opens a dialog with its categories, the markup and the file to copy, and a file to download', async () => {
      await page.goto(BASE + '/Foundations/IconLibrary?q=radiator', { waitUntil: 'networkidle' });
      await page.click('.doc-icon[data-iconlib="radiator"]');
      await page.waitForSelector('#iconlib-dialog[open]');
      expect(await page.textContent('#iconlib-dialog-title'), 'radiator', 'the title of the dialog');
      expect(await page.getAttribute('#iconlib-dialog use[data-iconlib-big]', 'href'), '#mat-il-radiator', 'the big preview');
      const big = await page.locator('#iconlib-dialog .doc-iconlib__big').boundingBox();
      expectTrue(big.width > 48, 'the preview is small: ' + big.width);
      expectTrue((await page.locator('#iconlib-dialog [data-iconlib-cats] a').count()) >= 2, 'the categories of the icon are not listed');
      expectTrue((await page.textContent('#iconlib-dialog [data-iconlib-tags]')).includes('heating'), 'the tags are not shown');
      expectTrue((await page.textContent('#iconlib-dialog [data-iconlib-origin]')).includes('MatDesign'), 'the origin is not shown');
      expectTrue((await page.getAttribute('#iconlib-dialog [data-iconlib-use]', 'data-copy')).includes('#mat-il-radiator'), 'the markup to copy');
      await page.waitForFunction(() => document.querySelector('#iconlib-dialog [data-iconlib-file]').getAttribute('data-copy').includes('xmlns'));
      const file = await page.getAttribute('#iconlib-dialog [data-iconlib-file]', 'data-copy');
      expectTrue(file.startsWith('<svg') && file.includes('stroke="currentColor"') && file.includes('<rect'), 'the file to copy: ' + file.slice(0, 80));
      expect(await page.getAttribute('#iconlib-dialog [data-iconlib-download]', 'href'), '/ui/iconlib/svg/radiator.svg', 'the download');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(200);
      // another icon replaces what the dialog showed
      await page.goto(BASE + '/Foundations/IconLibrary?q=lamp', { waitUntil: 'networkidle' });
      await page.click('.doc-icon[data-iconlib="lamp"]');
      await page.waitForSelector('#iconlib-dialog[open]');
      expect(await page.textContent('#iconlib-dialog-title'), 'lamp', 'the next icon');
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Foundations/IconLibrary?category=climate');
    await check('on a phone the categories are an icon in the header bar that opens a dialog, and nothing scrolls sideways', async () => {
      expectTrue(!(await page.locator('.mat-toc-aside').isVisible()), 'the categories take room beside the list');
      expectTrue(await page.locator('.mat-topbar__index').isVisible(), 'no index icon in the header bar');
      expect(await page.getAttribute('.mat-topbar__index', 'aria-label'), 'Categories, choose one', 'the name of the icon');
      const widths = await page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth }));
      expectTrue(widths.page <= widths.window, 'the page scrolls sideways');
      await page.click('.mat-topbar__index');
      await page.waitForSelector('#iconlib-cats-dialog[open]');
      await page.click('#iconlib-cats-dialog a:has-text("Garden & outdoor")');
      await page.waitForLoadState('networkidle');
      expectTrue(page.url().includes('category=outdoor'), 'url: ' + page.url());
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the Studio
  // It needs the administrator (STUDIO_NAME, default admin, and STUDIO_PASSWORD, default studio-dev: the password has to be 10 characters or more). A Studio that has no
  // administrator yet is set up here, through the setup assistant, which is checked on the way. A site with the Studio off is not a failure here.
  console.log('\nStudio');
  {
    const name = process.env.STUDIO_NAME || 'admin';
    const password = process.env.STUDIO_PASSWORD || 'studio-dev';
    const { ctx, page } = await open(desktopOptions, '/Studio/Login');
    if (page.url().includes('/Studio/Setup')) {
      await check('a Studio without an administrator leads to the setup assistant: a card without a menu, a name and a password typed twice', async () => {
        expect(await page.textContent('.mat-auth__card h1'), 'Set up the Studio', 'the title');
        expect(await page.locator('.mat-nav').count(), 0, 'menus on the setup page');
        expect(await page.locator('.mat-auth__card input[type="password"]').count(), 2, 'password fields');
        expect(await page.inputValue('#Name'), 'admin', 'the suggested name');
      });
      // the assistant has three steps (stage 14 of docs/PRODUCT.md): the company, the template the site starts from, the administrator. STUDIO_COMPANY (default Mat) is the company
      // and STUDIO_TEMPLATE (default demo) the template: the checks of this file are about the Mat apps, so the default is the demo.
      const company = process.env.STUDIO_COMPANY || 'Mat';
      const template = process.env.STUDIO_TEMPLATE || 'demo';
      await check('the assistant shows one step at a time: the company first, and Next does not leave it while the company is empty', async () => {
        expect(await page.locator('[data-setup-step]').count(), 3, 'steps');
        expect(await page.locator('[data-setup-step]:not([hidden])').count(), 1, 'steps on show');
        expectTrue(await page.isVisible('#Company'), 'the company field is not shown');
        expect(await page.isVisible('#Name'), false, 'the administrator step is shown too early');
        expect(await page.isVisible('[data-setup-back]'), false, 'Back on the first step');
        expect(await page.locator('[data-setup-progress] li.is-active').count(), 1, 'steps marked as current');
        await page.click('[data-setup-next]');
        expectTrue(await page.locator('.mat-field:has(#Company).is-invalid').count() > 0, 'the empty company is not marked');
        expectTrue(await page.isVisible('#Company'), 'left the first step with no company');
        // Enter in the field does not send an unfinished form either
        await page.press('#Company', 'Enter');
        expectTrue(page.url().includes('/Studio/Setup'), 'left the setup: ' + page.url());
        expectTrue(await page.isVisible('#Company'), 'Enter left the first step with no company');
      });
      await check('Next leads to the two templates as cards, Back returns, and the right choice leads to the administrator', async () => {
        await page.fill('#Company', company);
        await page.click('[data-setup-next]');
        expectTrue(await page.isVisible('.mat-choice'), 'the templates are not shown');
        expect(await page.locator('.mat-choice').count(), 2, 'template cards');
        expect(await page.locator('.mat-choice__text strong').allTextContents(), ['Demo', 'Blank'], 'the names of the templates');
        expectTrue((await page.textContent('[data-setup-step="2"]')).includes('The Mat apps as a sample, to look around and change.'), 'no description of the demo');
        expectTrue((await page.textContent('[data-setup-step="2"]')).includes('Start with your own brand and no apps.'), 'no description of the blank template');
        expect(await page.isVisible('#Company'), false, 'the first step is still shown');
        await page.click('[data-setup-back]');
        expectTrue(await page.isVisible('#Company'), 'Back did not return to the company');
        expect(await page.inputValue('#Company'), company, 'the company was lost on the way back');
        await page.click('[data-setup-next]');
        await page.locator(`.mat-choice:has(input[value="${template}"])`).click();
        await page.click('[data-setup-next]');
        expectTrue(await page.isVisible('#Name'), 'the administrator step is not shown');
        expectTrue(await page.isVisible('[data-setup-submit]'), 'no Create button on the last step');
        expect(await page.isVisible('[data-setup-next]'), false, 'Next on the last step');
      });
      await check('a password that is too short, or typed differently the second time, is refused at its field and leads nowhere', async () => {
        await page.fill('#Name', name);
        await page.fill('#Password', 'short');
        await page.fill('#Repeat', 'short');
        await page.click('.mat-auth__card button[type="submit"]');
        expectTrue((await page.locator('.mat-field.is-invalid').count()) > 0, 'the short password is not marked');
        expectTrue(page.url().includes('/Studio/Setup'), 'left the setup: ' + page.url());
        await page.fill('#Password', 'a first long password');
        await page.fill('#Repeat', 'a second long password');
        await page.click('.mat-auth__card button[type="submit"]');
        await page.waitForLoadState('networkidle');
        expectTrue((await page.textContent('.mat-auth__card')).includes('The two passwords are not the same.'), 'no message about the two passwords');
        expectTrue(page.url().includes('/Studio/Setup'), 'left the setup: ' + page.url());
      });
      await check('the right input creates the administrator, signs them in and leads into the Studio; afterwards the assistant is gone', async () => {
        await page.fill('#Name', name);
        await page.fill('#Password', password);
        await page.fill('#Repeat', password);
        await page.click('.mat-auth__card button[type="submit"]');
        await page.waitForURL('**/Studio');
        expectTrue((await page.textContent('body')).includes('You are signed in as ' + name), 'no welcome after the setup');
        await ctx.clearCookies();
        await page.goto(BASE + '/Studio/Setup', { waitUntil: 'networkidle' });
        expectTrue(page.url().includes('/Studio/Login'), 'the assistant is still there: ' + page.url());
      });
      await page.goto(BASE + '/Studio/Login', { waitUntil: 'networkidle' });
    }

    if (await page.locator('input[type="password"]').count() === 0) {
      console.log('  --    the Studio is off on this site (no data directory): its checks are skipped');
      await ctx.close();
    } else {
      await check('a visitor is led from a Studio page to the sign-in, which is a card without a menu', async () => {
        await page.goto(BASE + '/Studio/Activity', { waitUntil: 'networkidle' });
        expectTrue(page.url().includes('/Studio/Login?returnUrl='), 'not led to the sign-in: ' + page.url());
        expect(await page.locator('.mat-nav').count(), 0, 'menus on the sign-in page');
        expect(await page.locator('.mat-auth__card input[type="password"]').count(), 1, 'password fields');
      });
      await check('a wrong password stays on the page and says so; the right name and password lead to where the visitor wanted to go', async () => {
        await page.fill('#Name', name);
        await page.fill('#Password', 'not the password');
        await page.click('.mat-auth__card button[type="submit"]');
        await page.waitForLoadState('networkidle');
        expectTrue((await page.textContent('.mat-auth__card')).includes('The name or the password is not right.'), 'no message');
        await page.fill('#Name', name);
        await page.fill('#Password', password);
        await page.click('.mat-auth__card button[type="submit"]');
        await page.waitForURL('**/Studio/Activity');
        expect(await page.textContent('main h1'), 'Activity', 'the page after the sign-in');
      });
      await check('the Studio is a third menu behind the switcher, and the user is the administrator', async () => {
        // the site has the switcher at the foot of the menu, above the user
        await page.click('.mat-nav__foot .mat-switcher');
        expect(await page.locator('#nav-switch-menu [data-nav-set]').evaluateAll(els => els.map(e => e.getAttribute('data-nav-set'))), ['design', 'toolkit', 'studio'], 'menus in the switcher');
        await page.keyboard.press('Escape');
        expect(await page.textContent('.mat-user__name'), 'Admin', 'the user');
        const names = await page.locator('.mat-nav__scroll:not([hidden]) .mat-nav__item').evaluateAll(els => els.map(e => { const c = e.cloneNode(true); c.querySelectorAll('.mat-nav__badge').forEach(b => b.remove()); return c.textContent.trim(); }));
        expect(names, ['Overview', 'Apps', 'Company', 'Brand', 'Rules', 'Components', 'Activity', 'Checks', 'Releases', 'Versions'], 'items of the Studio menu');
      });
      await check('choosing the Studio in the switcher loads its overview (a start page); the other menus swap the items', async () => {
        await page.goto(BASE + '/Rules', { waitUntil: 'networkidle' });
        expect(await page.locator('.mat-nav [data-nav-panel="studio"]').count(), 0, 'the page of the site has no panel of the Studio');
        await page.click('.mat-nav__foot .mat-switcher');
        expect(await page.evaluate(() => document.querySelector('#nav-switch-menu [data-nav-set="studio"]').tagName), 'A', 'the Studio is a link');
        expect(await page.evaluate(() => document.querySelector('#nav-switch-menu [data-nav-set="toolkit"]').tagName), 'BUTTON', 'the Toolkit is a button');
        await Promise.all([page.waitForURL('**/Studio'), page.click('#nav-switch-menu [data-nav-set="studio"]')]);
        expect((await page.textContent('.mat-switcher__name')).trim(), 'Studio', 'the menu of the page that loaded');
        expectTrue(await page.locator('.mat-nav [data-nav-panel="studio"]').isVisible(), 'the Studio menu is shown');
        expect(await page.getAttribute('#nav-switch-menu [data-nav-set="studio"]', 'aria-checked'), 'true', 'checked');
        // from there a menu without a start page is still swapped without a request
        await page.click('.mat-nav__foot .mat-switcher');
        await page.click('#nav-switch-menu [data-nav-set="toolkit"]');
        await page.waitForTimeout(250);
        expect(new URL(page.url()).pathname, '/Studio', 'the address after swapping');
        expectTrue(await page.locator('.mat-nav [data-nav-panel="toolkit"]').isVisible(), 'the toolkit menu is shown');
        await page.goto(BASE + '/Studio/Activity', { waitUntil: 'networkidle' });   // where the next checks expect to be
      });
      await check('the activity list has its toolbar above it and the sort dialog opens', async () => {
        const bar = await page.locator('.mat-toolbar').boundingBox();
        const below = await page.locator('.mat-empty, .mat-table-wrap').first().boundingBox();
        expectTrue(bar.y < below.y, 'the toolbar is not above the list');
        await page.click('[data-dialog-open="activity-sort"]');
        expect(await page.locator('#activity-sort[open]').count(), 1, 'sort dialog');
        await page.click('#activity-sort [data-dialog-close]');
      });

      await check('the checks page lists the checks with their result, has the toolbar above the findings, and the menu says that all pass', async () => {
        await page.goto(BASE + '/Studio/Checks', { waitUntil: 'networkidle' });
        expect(await page.textContent('#result-title'), 'All checks pass', 'the result');
        expect(await page.locator('section[aria-labelledby="result-title"] tbody tr').count() >= 10, true, 'the checks listed');
        expect(await page.locator('section[aria-labelledby="result-title"] .mat-pill--error').count(), 0, 'checks that fail');
        const bar = await page.locator('.mat-toolbar').boundingBox();
        const table = await page.locator('section[aria-labelledby="findings-title"] .mat-table-wrap, section[aria-labelledby="findings-title"] .mat-empty').first().boundingBox();
        expectTrue(bar.y < table.y, 'the toolbar is not above the findings');
        expectTrue((await page.locator('a.mat-nav__item[href="/Studio/Checks"]').textContent()).includes('all checks pass'), 'the badge of the menu item');
        await page.click('[data-dialog-open="findings-filter"]');
        expect(await page.locator('#findings-filter[open]').count(), 1, 'filter dialog');
        await page.click('#findings-filter [data-dialog-close]');
      });
      // an app is created, refused, saved, seen on the site and in the toolkit, and deleted again (the ids are letters only, so the name has no digits)
      const letters = Array.from({ length: 6 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join('');
      const appId = 'qa' + letters;
      const appName = 'Qa' + letters;
      const setPicker = (id, value) => page.evaluate(([i, v]) => { const el = document.getElementById(i); el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }, [id, value]);
      await check('the apps list has its toolbar above the table and the New app button below it', async () => {
        await page.goto(BASE + '/Studio/Apps', { waitUntil: 'networkidle' });
        const bar = await page.locator('.mat-toolbar').boundingBox();
        const table = await page.locator('.mat-table-wrap').boundingBox();
        const add = await page.locator('a.mat-btn--primary', { hasText: 'New app' }).boundingBox();
        expectTrue(bar.y < table.y, 'the toolbar is not above the table');
        expectTrue(add.y > table.y + table.height - 1, 'the New app button is not below the table');
      });
      await check('an accent that white text cannot carry is refused with the reason, and what was typed stays', async () => {
        await page.click('a.mat-btn--primary:has-text("New app")');
        await page.waitForURL('**/Studio/Apps/Edit');
        await page.fill('#Input_Name', appName);
        await page.fill('#Input_Id', appId);
        await page.fill('#Input_Tagline', 'Made by the browser check');
        await setPicker('Input_Accent', '#ffe066');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForLoadState('networkidle');
        const summary = await page.textContent('[data-validation-summary]');
        expectTrue(summary.includes('White text reaches only'), 'no reason in the summary: ' + summary.trim().slice(0, 120));
        expect(await page.inputValue('#Input_Name'), appName, 'the name that was typed');
        expect(await page.locator('#Input_OnAccent').evaluate(el => el.closest('.mat-field').classList.contains('is-invalid')), true, 'the field with the problem');
      });
      await check('a dark text colour and a darker link save it, the list says so in a toast, and the Check button saves nothing', async () => {
        await setPicker('Input_OnAccent', '#0b1020');
        await setPicker('Input_AccentText', '#7a5c00');
        await page.click('.mat-btn-row button:has-text("Check")');
        await page.waitForLoadState('networkidle');
        expectTrue((await page.textContent('main')).includes('All checks pass.'), 'Check does not say that all pass');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForURL('**/Studio/Apps');
        await page.waitForSelector('.mat-toast');
        expectTrue((await page.textContent('.mat-toast')).includes(appName + ' was added.'), 'no toast: ' + await page.textContent('.mat-toasts'));
        // the list shows 20 apps a page: the search finds the new one
        await page.goto(BASE + '/Studio/Apps?q=' + appId, { waitUntil: 'networkidle' });
        expectTrue((await page.textContent('.mat-table')).includes(appName), 'the app is not in the list');
      });
      await check('the new app is on the site and its colours are in the toolkit files', async () => {
        await page.goto(BASE + '/Identity/App?id=' + appId, { waitUntil: 'networkidle' });
        expect(await page.textContent('main h1'), appName, 'the page of the app');
        const css = await (await page.request.get(BASE + '/ui/css/matui.apps.css')).text();
        expectTrue(css.includes('[data-app="' + appId + '"] { --mat-app-accent: #ffe066; --mat-app-on-accent: #0b1020; --mat-app-accent-text: #7a5c00; }'), 'the colours are not in matui.apps.css');
        const apps = await (await page.request.get(BASE + '/api/apps')).json();
        expectTrue(apps.some(a => a.id === appId), 'the app is not in the API');
      });
      await check('Delete asks in a dialog, the dialog can be cancelled, and the app is gone with its colours afterwards', async () => {
        await page.goto(BASE + '/Studio/Apps/Edit?id=' + appId, { waitUntil: 'networkidle' });
        expect(await page.locator('#Input_Id').evaluate(el => el.readOnly), true, 'the id of a saved app is read-only');
        await page.click('.mat-btn--danger[data-confirm]');
        expect(await page.locator('dialog[open]').count(), 1, 'the confirmation dialog');
        await page.click('dialog[open] [data-dialog-close]');
        expectTrue(page.url().includes('/Studio/Apps/Edit'), 'Cancel left the page');
        await page.click('.mat-btn--danger[data-confirm]');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForURL('**/Studio/Apps');
        const css = await (await page.request.get(BASE + '/ui/css/matui.apps.css')).text();
        expectTrue(!css.includes('data-app="' + appId + '"'), 'the colours of the deleted app are still in the toolkit');
        expect((await page.request.get(BASE + '/Identity/App?id=' + appId)).status(), 404, 'the page of the deleted app');
      });
      // a rule is added, refused, saved, seen in the rulebook, and deleted again (a number of three digits that is not in use)
      const ruleId = 'FORM-' + (900 + Math.floor(Math.random() * 99));
      await check('a rule with an id of another area is refused with the reason, and what was typed stays', async () => {
        await page.goto(BASE + '/Studio/Rules', { waitUntil: 'networkidle' });
        const bar = await page.locator('.mat-toolbar').boundingBox();
        const table = await page.locator('.mat-table-wrap').boundingBox();
        expectTrue(bar.y < table.y, 'the toolbar is not above the table');
        await page.click('a.mat-btn--primary:has-text("New rule")');
        await page.waitForURL('**/Studio/Rules/Edit');
        await page.selectOption('#Input_Area', 'FORM');
        await page.fill('#Input_Id', 'LIST-' + ruleId.slice(5));
        await page.fill('#Input_Title', 'A rule from the browser check');
        await page.fill('#Input_Statement', 'The browser check adds this rule and deletes it again, so nothing stays in the rulebook.');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForLoadState('networkidle');
        expectTrue((await page.textContent('[data-validation-summary]')).includes('starts with FORM-'), 'no reason in the summary');
        expect(await page.inputValue('#Input_Title'), 'A rule from the browser check', 'the title that was typed');
      });
      await check('the right id saves it, the Check button saves nothing, and the rule is in the rulebook with its toast', async () => {
        await page.fill('#Input_Id', ruleId);
        await page.click('.mat-btn-row button:has-text("Check")');
        await page.waitForLoadState('networkidle');
        expectTrue((await page.textContent('main')).includes('All checks pass.'), 'Check does not say that all pass');
        expectTrue((await page.textContent('main')).includes('How it reads'), 'no preview');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForURL('**/Studio/Rules');
        await page.waitForSelector('.mat-toast');
        expectTrue((await page.textContent('.mat-toast')).includes(ruleId + ' was added.'), 'no toast');
        await page.goto(BASE + '/Rules/Detail?id=' + ruleId, { waitUntil: 'networkidle' });
        expectTrue((await page.textContent('main h1')).includes('A rule from the browser check'), 'the rule page');
        const api = await (await page.request.get(BASE + '/api/rules')).json();
        expectTrue(api.some(r => r.id === ruleId), 'the rule is not in the API');
      });
      await check('deleting the rule asks in a dialog and takes it out of the rulebook', async () => {
        await page.goto(BASE + '/Studio/Rules/Edit?id=' + ruleId, { waitUntil: 'networkidle' });
        expect(await page.locator('#Input_Id').evaluate(el => el.readOnly), true, 'the id of a saved rule is read-only');
        await page.click('.mat-btn--danger[data-confirm]');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForURL('**/Studio/Rules');
        expect((await page.request.get(BASE + '/Rules/Detail?id=' + ruleId)).status(), 404, 'the page of the deleted rule');
      });
      // a component with an example: refused for a class the toolkit does not have, corrected, saved, seen live in its group and index on the page, deleted again
      const compLetters = Array.from({ length: 6 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join('');
      const compId = 'qa-' + compLetters;
      const compName = 'Qa ' + compLetters;
      await check('a component whose markup uses a class the toolkit does not have is refused with the reason, and its example is shown live next to the form', async () => {
        await page.goto(BASE + '/Studio/Components', { waitUntil: 'networkidle' });
        const bar = await page.locator('.mat-toolbar').boundingBox();
        const table = await page.locator('.mat-table-wrap').boundingBox();
        expectTrue(bar.y < table.y, 'the toolbar is not above the table');
        await page.click('a.mat-btn--primary:has-text("New component")');
        await page.waitForURL('**/Studio/Components/Edit');
        await page.fill('#Input_Name', compName);
        await page.fill('#Input_Id', compId);
        await page.selectOption('#Input_Group', 'Content');
        await page.fill('#Input_Summary', 'A component that the browser check adds and deletes again.');
        await page.fill('#Input_Rules', 'BUTTON-01');
        await page.fill('#Input_Examples_0_Title', 'A pill');
        await page.fill('#Input_Examples_0_Html', '<span class="mat-pill nopeclass">Online</span>');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForLoadState('networkidle');
        expectTrue((await page.textContent('[data-validation-summary]')).includes("the toolkit has no class 'nopeclass'"), 'no reason in the summary');
        expect(await page.inputValue('#Input_Name'), compName, 'the name that was typed');
        const form = await page.locator('form.mat-form').boundingBox();
        const preview = await page.locator('#preview-title').boundingBox();
        expectTrue(preview.x > form.x + form.width - 1, 'the preview is not beside the form');
        expect(await page.locator('.doc-example__live .mat-pill').count(), 1, 'the example drawn live');
      });
      await check('examples are added and removed without saving, and the corrected component is saved with its toast', async () => {
        await page.fill('#Input_Examples_0_Html', '<span class="mat-pill mat-pill--ok">Online</span>');
        await page.click('button:has-text("Add an example")');
        await page.waitForLoadState('networkidle');
        expect(await page.locator('textarea[name$=".Html"]').count(), 2, 'example blocks after Add');
        expect(await page.inputValue('#Input_Examples_0_Html'), '<span class="mat-pill mat-pill--ok">Online</span>', 'the markup stays');
        await page.click('button[aria-label="Remove example 2"]');
        await page.waitForLoadState('networkidle');
        expect(await page.locator('textarea[name$=".Html"]').count(), 1, 'example blocks after Remove');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForURL('**/Studio/Components');
        await page.waitForSelector('.mat-toast');
        expectTrue((await page.textContent('.mat-toast')).includes(compName + ' was added.'), 'no toast');
      });
      await check('the component is on its page, live, in its group and in the index of the page', async () => {
        await page.goto(BASE + '/Components', { waitUntil: 'networkidle' });
        await page.waitForFunction(() => document.readyState === 'complete');
        expect(await page.locator('#' + compId + ' .mat-pill--ok').count(), 1, 'the example drawn live on the page');
        expect(await page.locator('.mat-toc-aside a[href="#' + compId + '"]').count(), 1, 'the link in the index');
        expectTrue((await page.locator('#group-content').evaluate((el, id) => !!el.querySelector('#' + id), compId)), 'the component is not in the group Content');
        await page.click('.mat-toc-aside a[href="#' + compId + '"]');
        await page.waitForFunction((id) => document.querySelector('.mat-toc-aside a[href="#' + id + '"]').classList.contains('is-active'), compId);
      });
      await check('deleting the component asks in a dialog and takes it off its page', async () => {
        await page.goto(BASE + '/Studio/Components/Edit?id=' + compId, { waitUntil: 'networkidle' });
        await page.click('.mat-btn--danger[data-confirm]');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForURL('**/Studio/Components');
        const html = await (await page.request.get(BASE + '/Components')).text();
        expectTrue(!html.includes('id="' + compId + '"'), 'the component is still on its page');
      });
      // the history of an app: two revisions, what changed between them, and a restore that asks first
      const histLetters = Array.from({ length: 6 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join('');
      const histId = 'qh' + histLetters;
      const histName = 'Qh' + histLetters;
      await check('a changed app has a history with its revisions, and the page says what changed between the last two', async () => {
        await page.goto(BASE + '/Studio/Apps/Edit', { waitUntil: 'networkidle' });
        await page.fill('#Input_Name', histName);
        await page.fill('#Input_Id', histId);
        await page.fill('#Input_Tagline', 'First tagline');
        await page.evaluate(() => { const el = document.getElementById('Input_Accent'); el.value = '#0f766e'; el.dispatchEvent(new Event('input', { bubbles: true })); });
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForURL('**/Studio/Apps');
        await page.goto(BASE + '/Studio/Apps/Edit?id=' + histId, { waitUntil: 'networkidle' });
        expect(await page.locator('a.mat-btn:has-text("History")').count(), 1, 'the History button of an app that has revisions');
        await page.fill('#Input_Tagline', 'Second tagline');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForURL('**/Studio/Apps');
        await page.goto(BASE + '/Studio/Apps/Edit?id=' + histId, { waitUntil: 'networkidle' });
        await page.click('a.mat-btn:has-text("History")');
        await page.waitForURL('**/Studio/History**');
        expect(await page.locator('section[aria-labelledby="revisions-title"] tbody tr').count(), 2, 'revisions');
        expect(await page.locator('.mat-pill:has-text("Current")').count(), 1, 'the current revision');
        const diff = await page.textContent('section[aria-labelledby="changes-title"] .mat-table-wrap');
        expectTrue(diff.includes('tagline') && diff.includes('First tagline') && diff.includes('Second tagline'), 'the diff does not show the tagline: ' + diff.trim().slice(0, 160));
        expect(await page.locator('.doc-diff__line--add').count(), 1, 'lines that are new');
        expect(await page.locator('.doc-diff__line--del').count(), 1, 'lines that are gone');
      });
      await check('restoring the first revision asks in a dialog, can be cancelled, and brings the first tagline back with a toast', async () => {
        await page.click('button[aria-label="Restore revision 1"]');
        expect(await page.locator('dialog[open]').count(), 1, 'the confirmation dialog');
        await page.click('dialog[open] [data-dialog-close]');
        expect(await page.locator('section[aria-labelledby="revisions-title"] tbody tr').count(), 2, 'Cancel changed nothing');
        await page.click('button[aria-label="Restore revision 1"]');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForSelector('.mat-toast');
        expectTrue((await page.textContent('.mat-toast')).includes('was restored as revision 3'), 'no toast: ' + await page.textContent('.mat-toasts'));
        expect(await page.locator('section[aria-labelledby="revisions-title"] tbody tr').count(), 3, 'revisions after the restore');
        const apps = await (await page.request.get(BASE + '/api/apps')).json();
        expect(apps.find(a => a.id === histId).tagline, 'First tagline', 'the tagline on the site');
      });
      await check('the activity links the item to its history, and the app is deleted again', async () => {
        await page.goto(BASE + '/Studio/Activity?q=' + histId, { waitUntil: 'networkidle' });
        expect(await page.locator('a[href*="/Studio/History?kind=apps"]').count() > 0, true, 'a link to the history in the activity');
        await page.goto(BASE + '/Studio/Apps/Edit?id=' + histId, { waitUntil: 'networkidle' });
        await page.click('.mat-btn--danger[data-confirm]');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForURL('**/Studio/Apps');
        expect((await page.request.get(BASE + '/Identity/App?id=' + histId)).status(), 404, 'the page of the deleted app');
      });
      // a release: the form refuses a version that is not three numbers; the pipeline runs through, the run shows its five stages, and the release is served
      // (the release stays: a release never changes, so the development volume grows by one release for each run of these checks)
      await check('the releases page has its toolbar above the list and the form refuses a version that is not three numbers', async () => {
        await page.goto(BASE + '/Studio/Releases', { waitUntil: 'networkidle' });
        const bar = await page.locator('.mat-toolbar').boundingBox();
        const below = await page.locator('section[aria-labelledby="releases-title"] .mat-table-wrap, section[aria-labelledby="releases-title"] .mat-empty').first().boundingBox();
        expectTrue(bar.y < below.y, 'the toolbar is not above the list');
        await page.click('a.mat-btn--primary:has-text("New release")');
        await page.waitForURL('**/Studio/Releases/New');
        expectTrue(/^\d+\.\d+\.\d+$/.test(await page.inputValue('#Version')), 'the suggested version: ' + await page.inputValue('#Version'));
        expect(await page.locator('#stages-title').count(), 1, 'the list of what the pipeline does');
        await page.fill('#Version', 'abc');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForLoadState('networkidle');
        expectTrue(/three numbers/i.test(await page.textContent('main')), 'no reason for the version (the toolkit says it in the browser, the server in the same words)');
        expect(await page.inputValue('#Version'), 'abc', 'the version that was typed');
      });
      await check('the pipeline asks first, runs the five stages, publishes, and the release is served with its files', async () => {
        await page.goto(BASE + '/Studio/Releases/New', { waitUntil: 'networkidle' });
        const version = await page.inputValue('#Version');
        await page.fill('#Note', 'Made by the browser check.');
        await page.click('.mat-btn-row button.mat-btn--primary');
        expect(await page.locator('dialog[open]').count(), 1, 'the confirmation dialog');
        await page.click('dialog[open] [data-dialog-close]');
        expectTrue(page.url().endsWith('/Studio/Releases/New'), 'Cancel left the page');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForURL('**/Studio/Releases/Run?id=*');
        expectTrue((await page.textContent('main .mat-alert--success')).includes('Version ' + version + ' is released.'), 'the run did not release ' + version + ': ' + (await page.textContent('main')).slice(0, 200));
        expect(await page.locator('section[aria-labelledby^="stage-"]').count(), 5, 'stages');
        expect(await page.locator('section[aria-labelledby^="stage-"] .mat-pill--ok').count(), 5, 'stages that succeeded');
        const css = await page.request.get(BASE + '/releases/' + version + '/matui.css');
        expect(css.status(), 200, 'matui.css of the release');
        expectTrue((await css.text()).startsWith('/* MatUi ' + version + ' ('), 'the bundle names the release');
        const latest = await (await page.request.get(BASE + '/releases/latest.json')).json();
        expect(latest.version, version, 'latest.json');
        expectTrue(latest.files.some(f => f.path === 'matui-' + version + '.zip' && f.sha256.length === 64), 'the archive with its hash in the manifest');
        await page.goto(BASE + '/Studio/Releases', { waitUntil: 'networkidle' });
        expectTrue((await page.textContent('section[aria-labelledby="releases-title"] tbody tr')).includes(version), 'the release is first in the list');
        expect(await page.locator('section[aria-labelledby="releases-title"] .mat-pill:has-text("Latest")').count(), 1, 'the latest release');
      });
      // the versions: the latest release is taken over for an app, the list says that it is current, and the record is forgotten again
      await check('the version of an app is taken over from the latest release, shown as current in the list, and forgotten again behind a dialog', async () => {
        await page.goto(BASE + '/Studio/Versions', { waitUntil: 'networkidle' });
        const bar = await page.locator('.mat-toolbar').boundingBox();
        const table = await page.locator('.mat-table-wrap').boundingBox();
        expectTrue(bar.y < table.y, 'the toolbar is not above the table');
        const latest = (await (await page.request.get(BASE + '/releases/latest.json')).json()).version;
        await page.goto(BASE + '/Studio/Versions/Edit?id=matmail', { waitUntil: 'networkidle' });
        await page.click('button:has-text("Use the latest")');
        await page.waitForURL('**/Studio/Versions');
        await page.waitForSelector('.mat-toast');
        expectTrue((await page.textContent('.mat-toast')).includes('MatMail uses ' + latest), 'no toast: ' + await page.textContent('.mat-toasts'));
        await page.goto(BASE + '/Studio/Versions?q=MatMail', { waitUntil: 'networkidle' });
        const row = await page.textContent('.mat-table tbody tr');
        expectTrue(row.includes(latest) && row.includes('Latest'), 'the row of MatMail: ' + row.replace(/\s+/g, ' ').trim());
        await page.goto(BASE + '/Studio/Versions/Edit?id=matmail', { waitUntil: 'networkidle' });
        await page.click('.mat-btn--danger[data-confirm]');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForURL('**/Studio/Versions');
        await page.goto(BASE + '/Studio/Versions?q=MatMail', { waitUntil: 'networkidle' });
        expectTrue((await page.textContent('.mat-table tbody tr')).includes('Not written down'), 'MatMail is still written down');
      });
      await check('Sign out is the last entry of the user menu, ends the session and leads back to the site', async () => {
        await page.click('.mat-user');
        const items = await page.locator('#user-menu .mat-menu__item').allTextContents();
        expect(items[items.length - 1].trim(), 'Sign out', 'last entry');
        await page.click('#user-menu button.mat-menu__item');
        await page.waitForURL(BASE + '/');
        await page.goto(BASE + '/Studio', { waitUntil: 'networkidle' });
        expectTrue(page.url().includes('/Studio/Login'), 'still signed in: ' + page.url());
      });
      await ctx.close();

      const phone = await open(phoneOptions, '/Studio/Login');
      await check('on a phone the sign-in and the Studio do not scroll sideways', async () => {
        const wide = () => phone.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth);
        expectTrue(await wide(), 'the sign-in page is wider than the window');
        await phone.page.fill('#Name', name);
        await phone.page.fill('#Password', password);
        await phone.page.click('.mat-auth__card button[type="submit"]');
        await phone.page.waitForURL('**/Studio');
        expectTrue(await wide(), 'the Studio is wider than the window');
      });
      await check('every page of the Studio fits a phone (MOBILE-01): no sideways scroll, and the menu opens as a drawer with the Studio in it', async () => {
        const wide = () => phone.page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth }));
        const urls = ['/Studio/Apps', '/Studio/Apps/Edit?id=matmail', '/Studio/Rules', '/Studio/Rules/Edit?id=SHELL-01', '/Studio/Components', '/Studio/Components/Edit?id=buttons', '/Studio/Components/Edit?id=pills',
          '/Studio/Activity', '/Studio/Checks', '/Studio/Releases', '/Studio/Releases/New', '/Studio/Versions', '/Studio/Versions/Edit?id=matmail'];
        for (const url of urls) {
          await phone.page.goto(BASE + url, { waitUntil: 'networkidle' });
          const size = await wide();
          expectTrue(size.page <= size.window, `${url} is ${size.page} px wide in a window of ${size.window} px`);
        }

        await phone.page.click('.mat-burger');
        expect(await phone.page.locator('.mat-app.is-nav-open').count(), 1, 'the drawer');
        expectTrue(await phone.page.locator('.mat-nav__scroll:not([hidden]) a.mat-nav__item[href="/Studio/Releases"]').isVisible(), 'the Studio menu in the drawer');
      });
      await phone.ctx.close();
    }
  }

  // ---------------------------------------------------------------------------------------------------- no page is wider than a phone
  console.log('\nPhone width');
  for (const url of ['/Components', '/Layout', '/Utilities', '/Download', '/Shell']) {
    const { ctx, page } = await open(phoneOptions, url);
    await check(`${url} does not scroll sideways on a phone (MOBILE-01)`, async () => {
      const widths = await page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth }));
      expect(widths.page <= widths.window, true, `the page is ${widths.page} px wide in a window of ${widths.window} px`);
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- groups in a table (LIST-16)
  console.log('\nGroups in a table');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#table-groups');
    const table = '#grp-hosts';
    const group = n => `${table} tbody.mat-group >> nth=${n}`;
    const within = (n, sel) => `${group(n)} >> ${sel}`;
    await page.evaluate(() => document.getElementById('table-groups').scrollIntoView());

    await check('a group is open or closed as the markup says, and its toggle says so', async () => {
      expect(await page.$eval(group(0), g => g.classList.contains('is-collapsed')), false, 'the first group');
      expect(await page.$eval(group(1), g => g.classList.contains('is-collapsed')), true, 'the second group');
      expect(await page.getAttribute(within(1, '[data-group-toggle]'), 'aria-expanded'), 'false', 'aria-expanded of the closed group');
      expect(await page.isVisible(within(0, '.mat-group__item >> nth=0')), true, 'a row of the open group');
      expect(await page.isVisible(within(1, '.mat-group__item >> nth=0')), false, 'a row of the closed group');
    });
    await check('the heading opens and closes its group with the mouse, Enter and Space, and aria-expanded follows', async () => {
      await page.click(within(1, '[data-group-toggle]'));
      expect(await page.isVisible(within(1, '.mat-group__item >> nth=0')), true, 'opened by a click');
      expect(await page.getAttribute(within(1, '[data-group-toggle]'), 'aria-expanded'), 'true', 'aria-expanded after opening');
      await page.focus(within(0, '[data-group-toggle]'));
      await page.keyboard.press('Enter');
      expect(await page.isVisible(within(0, '.mat-group__item >> nth=0')), false, 'closed by Enter');
      await page.keyboard.press('Space');
      expect(await page.isVisible(within(0, '.mat-group__item >> nth=0')), true, 'opened by Space');
    });
    await check('the button in the first column header opens every group when all are closed, and closes every group otherwise', async () => {
      await page.click(`${table} [data-groups-set]`);
      expect(await page.$$eval(`${table} tbody.mat-group.is-collapsed`, g => g.length), 2, 'all closed');
      await page.click(`${table} [data-groups-set]`);
      expect(await page.$$eval(`${table} tbody.mat-group.is-collapsed`, g => g.length), 0, 'all open');
    });
    await check('what the user chose is remembered, except in a group with data-group-fixed', async () => {
      await page.click(within(0, '[data-group-toggle]'));
      await page.click('#grp-files tbody.mat-group >> nth=0 >> [data-group-toggle]');
      await page.reload();
      await page.evaluate(() => document.getElementById('table-groups').scrollIntoView());
      expect(await page.$eval(group(0), g => g.classList.contains('is-collapsed')), true, 'the closed group stays closed after a reload');
      expect(await page.$eval('#grp-files tbody.mat-group >> nth=0', g => g.classList.contains('is-collapsed')), false, 'the fixed group is as drawn');
    });
    await check('the add row is the last row of its group and the line of the group ends on it', async () => {
      await page.click(within(0, '[data-group-toggle]'));
      const rows = await page.$$eval(`${table} tbody.mat-group:first-of-type tr`, trs => trs.map(t => t.className));
      expect(rows[rows.length - 1], 'mat-group__add', 'the last row');
      expect(rows[0], 'mat-group__head', 'the first row');
      const line = await page.$eval(`${table} tbody.mat-group:first-of-type .mat-group__add td`, td => {
        const before = getComputedStyle(td, '::before');
        return { position: before.position, bottom: before.bottom, height: td.getBoundingClientRect().height };
      });
      expect(line.position, 'absolute', 'the line is drawn');
      expect(Math.abs(parseFloat(line.bottom) - line.height / 2) < 2, true, 'the line ends at the middle of the last row: ' + line.bottom + ' of ' + line.height);
    });
    await ctx.close();

    const phone = await open(phoneOptions, '/Components#table-groups');
    await phone.page.evaluate(() => document.getElementById('table-groups').scrollIntoView());
    await check('on a phone the rows of a group are cards, without the line, with their labels, and the add row is a card with its button', async () => {
      const s = await phone.page.$eval('#grp-hosts tbody.mat-group >> nth=0', g => {
        const item = g.querySelector('.mat-group__item td');
        const add = g.querySelector('.mat-group__add td');
        return {
          itemDisplay: getComputedStyle(item.closest('tr')).display,
          label: getComputedStyle(item, '::before').content,
          linePosition: getComputedStyle(item, '::before').position,
          after: getComputedStyle(item, '::after').content,
          addAlign: getComputedStyle(add).justifyContent,
          sideways: document.documentElement.scrollWidth > innerWidth,
        };
      });
      expect(s.itemDisplay, 'block', 'a row is a card');
      expect(s.label, '"Host"', 'the first cell has its label');
      expect(s.linePosition, 'static', 'no line');
      expect(s.after, 'none', 'no branch');
      expect(s.addAlign, 'flex-start', 'the add button at the start');
      expect(s.sideways, false, 'sideways scroll');
    });
    await phone.ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- component properties (SPACE-06)
  // The properties have no value in the toolkit: an unset one changes nothing (tests/e2e/properties-before-after.js compares with the CSS from before, once), a set one changes its control
  // and nothing else. A sample of the components page keeps this check small; the dump is the computed style of what the properties touch.
  {
    const STYLE = ['borderTopLeftRadius', 'borderTopRightRadius', 'borderBottomLeftRadius', 'borderBottomRightRadius', 'minWidth', 'minHeight', 'width', 'height', 'paddingTop', 'paddingRight', 'paddingBottom',
      'paddingLeft', 'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth', 'rowGap', 'columnGap', 'fontSize', 'fontWeight', 'top', 'left', 'marginBottom'];
    const SAMPLE = ['buttons', 'button-group', 'fields', 'input-groups', 'cards', 'pills', 'tabs', 'table', 'alerts', 'choices', 'menu', 'navmenu', 'dialogs'];
    // what the properties of the sample touch, one line per element: its place, its class, the values and the size it takes
    const dumpSample = (page) => page.evaluate(({ props, sample }) => {
      const out = {};
      const take = (el, i) => {
        const cs = getComputedStyle(el);
        const r = el.getBoundingClientRect();
        out[i + ' ' + el.tagName.toLowerCase() + '.' + String(el.className).replace(/\s+/g, '.')] = props.map(p => cs[p]).join(',') + ' | ' + r.width.toFixed(1) + 'x' + r.height.toFixed(1);
        if (el.classList.contains('mat-switch__track')) { const a = getComputedStyle(el, '::after'); out[i + ' after'] = [a.top, a.left, a.width, a.height, a.transform].join(','); }
      };
      const els = [...document.querySelectorAll('.mat-topbar, .mat-topbar *, .mat-nav__item')];
      for (const id of sample) { els.push(...document.querySelectorAll('#' + id + ' .doc-example__live, #' + id + ' .doc-example__live *')); }
      els.forEach((el, i) => take(el, i));
      return out;
    }, { props: STYLE, sample: SAMPLE });
    const firstDifference = (a, b) => Object.keys(a).filter(k => a[k] !== b[k]).slice(0, 3).map(k => k + ': ' + a[k] + ' <> ' + b[k]);

    for (const [name, options] of [['desktop', desktopOptions], ['phone', phoneOptions]]) {
      const { ctx, page } = await open(options, '/Components');
      await check(`component properties, ${name}: every property set to its documented default gives the computed styles of none set`, async () => {
        const props = await page.evaluate(async () => (await (await fetch('/api/properties')).json()));
        expectTrue(props.length >= 40 && props.length <= 75, 'the number of properties: ' + props.length);
        const unset = await dumpSample(page);
        expectTrue(Object.keys(unset).length > 150, 'the sample has elements: ' + Object.keys(unset).length);
        // used with another default in one place (the small button, the card on a phone, the menu items that are buttons): left out of this comparison
        const another = ['--mat-btn-radius', '--mat-card-pad', '--mat-nav-item-font-weight'];
        await page.evaluate((list) => list.forEach(p => document.documentElement.style.setProperty(p.name, p.fallback)), props.filter(p => !another.includes(p.name)));
        const explicit = await dumpSample(page);
        expect(firstDifference(unset, explicit), [], 'computed styles that differ');
        expect(Object.keys(explicit).length, Object.keys(unset).length, 'the number of elements');
        await page.evaluate((list) => list.forEach(p => document.documentElement.style.removeProperty(p.name)), props);
      });
      await ctx.close();
    }

    {
      const { ctx, page } = await open(desktopOptions, '/Components');
      const radii = (sel) => page.$$eval(sel, els => [...new Set(els.map(e => getComputedStyle(e).borderTopLeftRadius + '/' + getComputedStyle(e).borderBottomRightRadius))]);
      await check('--mat-btn-radius: 0 on :root squares every button, of every size and in a group, and no other control changes', async () => {
        const outsideGroups = '.mat-btn:not(.mat-input-group > .mat-btn)'; // a button in an input group takes the radius of the field next to it
        const before = { btn: await radii(outsideGroups), input: await radii('.mat-input'), card: await radii('.mat-card'), tag: await radii('.mat-tag') };
        expectTrue(before.btn.length > 1, 'buttons have more than one radius to start with (the small ones): ' + before.btn);
        await page.evaluate(() => document.documentElement.style.setProperty('--mat-btn-radius', '0'));
        expect(await radii(outsideGroups), ['0px/0px'], 'every button is square');
        expect(await radii('.mat-input'), before.input, 'inputs are as before');
        expect(await radii('.mat-card'), before.card, 'cards are as before');
        expect(await radii('.mat-tag'), before.tag, 'tags are as before');
        await page.evaluate(() => document.documentElement.style.removeProperty('--mat-btn-radius'));
        expect(await radii(outsideGroups), before.btn, 'removing it brings the old radii back');
      });

      await check('a property set on one element changes the controls inside it and not those of the same kind outside', async () => {
        const pad = (i) => page.$$eval('.doc-example__live .mat-card__body', (els, n) => getComputedStyle(els[n]).paddingLeft, i);
        const outside = await pad(1);
        const inside = await pad(0);
        expectTrue(parseFloat(outside) > 4, 'a card has room to start with: ' + outside);
        await page.evaluate(() => document.querySelector('.doc-example__live .mat-card').style.setProperty('--mat-card-pad', '3px'));
        expect(await pad(0), '3px', 'the card with the property');
        expect(await pad(1), outside, 'another card');
        await page.evaluate(() => document.querySelector('.doc-example__live .mat-card').style.removeProperty('--mat-card-pad'));
        expect(await pad(0), inside, 'unset again');
      });

      await check('the heights of the controls follow their properties (the floor of the touch screen is the default)', async () => {
        const h = (sel) => page.$eval(sel, e => Math.round(e.getBoundingClientRect().height));
        const start = { btn: await h('#buttons .mat-btn:not(.mat-btn--sm):not(.mat-btn--lg):not(.mat-btn--icon)'), input: await h('#fields .mat-input:not(.mat-input--sm):not(.mat-input--lg)') };
        expect(start, { btn: 40, input: 40 }, 'the default heights with a mouse');
        await page.evaluate(() => { const s = document.documentElement.style; s.setProperty('--mat-btn-height', '52px'); s.setProperty('--mat-input-height', '56px'); });
        expect(await h('#buttons .mat-btn:not(.mat-btn--sm):not(.mat-btn--lg):not(.mat-btn--icon)'), 52, 'button');
        expect(await h('#fields .mat-input:not(.mat-input--sm):not(.mat-input--lg)'), 56, 'input');
        // the icon-only button stays square, the small and the large size keep theirs
        const icon = await page.$eval('#buttons .mat-btn--icon:not(.mat-btn--sm)', e => { const r = e.getBoundingClientRect(); return [Math.round(r.width), Math.round(r.height)]; });
        expect(icon, [52, 52], 'an icon button is square');
        expect(await h('#buttons .mat-btn--sm:not(.mat-btn--icon)'), 32, 'the small button keeps its height');
        await page.evaluate(() => { const s = document.documentElement.style; s.removeProperty('--mat-btn-height'); s.removeProperty('--mat-input-height'); });
      });

      await check('the switch follows its width, height and knob: the knob stays centred and travels the whole way', async () => {
        const knob = () => page.$eval('#choices .mat-switch input:not(:disabled) + .mat-switch__track', t => {
          const a = getComputedStyle(t, '::after');
          const on = t.previousElementSibling.checked;
          const travel = new DOMMatrix(a.transform).m41;
          return { w: t.getBoundingClientRect().width, h: t.getBoundingClientRect().height, top: parseFloat(a.top), left: parseFloat(a.left), size: parseFloat(a.width), travel, on };
        });
        await page.evaluate(() => { const s = document.documentElement.style; s.setProperty('--mat-switch-width', '64px'); s.setProperty('--mat-switch-height', '32px'); s.setProperty('--mat-switch-thumb', '24px'); });
        const k = await knob();
        expect([k.w, k.h, k.size, k.top, k.left], [64, 32, 24, 4, 4], 'the track, the knob and its distance from the edge');
        // switched on, it has moved by the width less the height, so the gap at the right is the gap at the left
        await page.$eval('#choices .mat-switch input:not(:disabled)', i => { i.checked = true; });
        const on = await knob();
        expect(on.travel, 32, 'the way of the knob');
        expect(on.w - (on.left + on.travel + on.size), on.left, 'the gap at the right end equals the gap at the left');
        await page.$eval('#choices .mat-switch input:not(:disabled)', i => { i.checked = false; });
        await page.evaluate(() => { const s = document.documentElement.style; ['width', 'height', 'thumb'].forEach(p => s.removeProperty('--mat-switch-' + p)); });
      });

      await check('the properties of a table reach its cells and the properties of a tab reach the line under it', async () => {
        await page.evaluate(() => { const s = document.documentElement.style; s.setProperty('--mat-table-pad-x', '30px'); s.setProperty('--mat-tabs-indicator-width', '5px'); });
        expect(await page.$eval('#table .mat-table td', e => getComputedStyle(e).paddingLeft), '30px', 'the cell');
        expect(await page.$eval('#table .mat-table th', e => getComputedStyle(e).paddingRight), '30px', 'the heading');
        expect(await page.$eval('#tabs .mat-tab', e => getComputedStyle(e).borderBottomWidth), '5px', 'the line under a tab');
        await page.evaluate(() => { const s = document.documentElement.style; s.removeProperty('--mat-table-pad-x'); s.removeProperty('--mat-tabs-indicator-width'); });
      });

      await check('the page of a component lists its properties, and the tokens page lists all of them by component', async () => {
        const rows = await page.$$eval('#props-buttons ~ .mat-table-wrap tbody tr, #buttons .doc-example[aria-labelledby="props-buttons"] tbody tr', trs => trs.map(tr => [...tr.cells].map(c => c.textContent.trim())));
        expectTrue(rows.length >= 5, 'the buttons page has its rows: ' + rows.length);
        expect(rows.find(r => r[0] === '--mat-btn-radius').slice(1, 3), ['length', 'var(--mat-radius)'], 'the radius of a button: type and default');
        expect(await page.$$eval('#meter [id^="props-"]', e => e.length), 0, 'a component without properties has no table');
        const tokens = await open(desktopOptions, '/Foundations/Tokens');
        const groups = await tokens.page.$$eval('#component-properties h3', e => e.length);
        const names = await tokens.page.$$eval('#component-properties tbody tr td:first-child', e => e.map(x => x.textContent.trim()));
        expectTrue(groups >= 14, 'the groups: ' + groups);
        expect(names.length, (await page.evaluate(async () => (await (await fetch('/api/properties')).json()).length)), 'every property is listed once');
        expect(await tokens.page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'no sideways scroll');
        await tokens.ctx.close();
      });
      await ctx.close();
    }

    // on a phone the default heights are the floor of 44 px, and a card keeps its smaller side space
    {
      const { ctx, page } = await open(phoneOptions, '/Components');
      await check('on a touch screen the default control heights are 44 px and a card has its side space of 20 px (nothing set)', async () => {
        const h = (sel) => page.$eval(sel, e => Math.round(e.getBoundingClientRect().height));
        expect(await h('#buttons .mat-btn:not(.mat-btn--sm):not(.mat-btn--lg):not(.mat-btn--icon)'), 44, 'button');
        expect(await h('#fields .mat-input:not(.mat-input--sm):not(.mat-input--lg)'), 44, 'input');
        expect(await page.$eval('#cards .mat-card__body', e => getComputedStyle(e).paddingLeft), '20px', 'card side space on a phone');
      });
      await ctx.close();
    }
  }

  // ---------------------------------------------------------------------------------------------------- the brand editor of the Studio (COLOR-11)
  // It needs the administrator like the Studio block above (STUDIO_NAME / STUDIO_PASSWORD). The brand is reset at the end, so the files of the site are what they were.
  console.log('\nBrand editor');
  {
    const name = process.env.STUDIO_NAME || 'admin';
    const password = process.env.STUDIO_PASSWORD || 'studio-dev';
    const { ctx, page } = await open(desktopOptions, '/Studio/Login');
    if (await page.locator('input[type="password"]').count() === 0) {
      console.log('  --    the Studio is off on this site (no data directory): the brand checks are skipped');
      await ctx.close();
    } else {
      await page.fill('#Name', name);
      await page.fill('#Password', password);
      await page.click('.mat-auth__card button[type="submit"]');
      await page.waitForURL('**/Studio**');
      const css = async () => (await page.request.get(BASE + '/ui/matui.css')).text();
      const preview = (prop) => page.evaluate((p) => getComputedStyle(document.querySelector('[data-brand-preview]')).getPropertyValue(p).trim(), prop);
      const setValue = async (id, value) => { await page.fill('#' + id, value); };
      const save = async () => { await page.click('.mat-btn-row button.mat-btn--primary'); await page.waitForLoadState('networkidle'); };

      await check('the brand page has the shell, one h1, a row for every token in its group, a picker for a colour and the preview beside the form', async () => {
        await page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        expect(await page.locator('main h1').count(), 1, 'h1');
        expect(await page.textContent('main h1'), 'Brand', 'the title');
        expect(await page.locator('a.mat-nav__item.is-active[href="/Studio/Brand"]').count(), 1, 'the menu item');
        const rows = await page.locator('[data-brand-row]').count();
        expectTrue(rows > 60, 'rows: ' + rows);
        expect(await page.locator('#light-mat-bg-picker').count(), 1, 'a colour has a picker');
        expect(await page.locator('#light-mat-radius-picker').count(), 0, 'a length has none');
        expect(await page.locator('form [data-brand-preview]').count(), 0, 'the preview is not in the form');
        const sizes = await page.evaluate(() => ({ form: document.querySelector('.doc-brand__main').getBoundingClientRect().right, side: document.querySelector('.doc-brand__side').getBoundingClientRect().left }));
        expectTrue(sizes.side >= sizes.form, 'the preview is beside the form on a wide window');
        expectTrue((await page.locator('[data-brand-summary]').textContent()).includes('Passes'), 'the summary of the toolkit own values');
      });
      await check('a value typed in a field is on the preview at once: a radius, a font size, a font stack', async () => {
        const before = await page.evaluate(() => getComputedStyle(document.querySelector('[data-brand-preview] .mat-btn--primary')).borderTopLeftRadius);
        expect(before, '6px', 'the radius of the toolkit');
        await setValue('light-mat-radius', '14px');
        expect(await preview('--mat-radius'), '14px', 'the property on the preview');
        expect(await page.evaluate(() => getComputedStyle(document.querySelector('[data-brand-preview] .mat-btn--primary')).borderTopLeftRadius), '14px', 'a button of the preview');
        expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--mat-radius').trim()), '6px', 'the page itself did not change');
        await setValue('light-mat-fs-md', '1.125rem');
        expect(await page.evaluate(() => getComputedStyle(document.querySelector('.doc-brand-preview__text')).fontSize), '18px', 'the body text of the preview');
        await setValue('light-mat-font', 'Georgia, serif');
        expectTrue((await page.evaluate(() => getComputedStyle(document.querySelector('.doc-brand-preview__text')).fontFamily)).startsWith('Georgia'), 'the font');
      });
      await check('a colour: the picker and the field stay in step, the preview wears it, and nothing in the preview has a style attribute in the markup', async () => {
        await setValue('light-mat-bg', '#ffeecc');
        expect(await page.inputValue('#light-mat-bg-picker'), '#ffeecc', 'the picker follows the field');
        expect(await page.evaluate(() => getComputedStyle(document.querySelector('[data-brand-preview]')).backgroundColor), 'rgb(255, 238, 204)', 'the background of the preview');
        await page.fill('#light-mat-bg-picker', '#cceeff');
        expect(await page.inputValue('#light-mat-bg'), '#cceeff', 'the field follows the picker');
        // an alpha is kept when the colour is changed with the picker
        await setValue('light-mat-border', 'rgba(10, 20, 30, 0.4)');
        await page.fill('#light-mat-border-picker', '#336699');
        expect(await page.inputValue('#light-mat-border'), 'rgba(51, 102, 153, 0.4)', 'the alpha stays');
        const styled = await page.evaluate(() => document.querySelectorAll('[data-brand-preview] [style]').length);
        expectTrue(styled === 0, 'a descendant of the preview has a style attribute');
      });
      await check('the contrast is worked out while typing: a text colour under AA fails and says so, a good one passes again', async () => {
        await setValue('light-mat-text', '#cccccc');
        await page.waitForFunction(() => document.querySelector('[data-brand-summary]').textContent.includes('below AA'));
        const row = page.locator('[data-pairing="0"] [data-brand-result="light"]');
        expectTrue((await row.textContent()).includes('Fails'), 'the first pairing: ' + await row.textContent());
        expectTrue((await page.textContent('[data-brand-summary]')).includes('Text on the page'), 'the summary names the pairing');
        // the same pairing in dark is not affected by the light value
        expectTrue((await page.locator('[data-pairing="0"] [data-brand-result="dark"]').textContent()).includes('Passes'), 'dark');
        await setValue('light-mat-text', '#111111');
        await setValue('light-mat-bg', '#ffffff');
        await page.waitForFunction(() => document.querySelector('[data-brand-summary]').textContent.includes('Passes'));
      });
      await check('the dark button shows the dark values on the preview, and a value that is not of its kind is marked and ignored', async () => {
        await page.click('[data-brand-mode="dark"]');
        expect(await page.getAttribute('[data-brand-mode="dark"]', 'aria-pressed'), 'true', 'pressed');
        expect(await page.evaluate(() => getComputedStyle(document.querySelector('[data-brand-preview]')).backgroundColor), 'rgb(21, 21, 20)', 'the dark page of the toolkit');
        await setValue('dark-mat-bg', '#000000');
        expect(await page.evaluate(() => getComputedStyle(document.querySelector('[data-brand-preview]')).backgroundColor), 'rgb(0, 0, 0)', 'the dark value');
        await page.click('[data-brand-mode="light"]');
        expect(await page.evaluate(() => getComputedStyle(document.querySelector('[data-brand-preview]')).backgroundColor), 'rgb(255, 255, 255)', 'back to light');
        await setValue('light-mat-radius', 'big');
        expect(await page.getAttribute('#light-mat-radius', 'aria-invalid'), 'true', 'marked');
        expect(await preview('--mat-radius'), '6px', 'not applied: the toolkit value stays');
        await setValue('light-mat-radius', '14px');
        expect(await page.getAttribute('#light-mat-radius', 'aria-invalid'), null, 'cleared');
      });
      await check('the reset button of a token empties its fields and the preview goes back', async () => {
        await page.click('[data-brand-row][data-token="--mat-radius"] [data-brand-reset]');
        expect(await page.inputValue('#light-mat-radius'), '', 'the field');
        expect(await preview('--mat-radius'), '6px', 'the preview');
      });
      await check('a field and a button are reached with the keyboard, and Enter in the preview does not submit the editor', async () => {
        await page.focus('#light-mat-radius');
        await page.keyboard.type('10px');
        expect(await preview('--mat-radius'), '10px', 'typed');
        await page.keyboard.press('Tab');
        expectTrue(await page.evaluate(() => document.activeElement.hasAttribute('data-brand-reset')), 'Tab leads on to the reset button');
        await page.focus('#bp-name');
        await page.keyboard.press('Enter');
        expectTrue(page.url().endsWith('/Studio/Brand'), 'the editor was submitted: ' + page.url());
        await page.click('[data-brand-row][data-token="--mat-radius"] [data-brand-reset]');
      });
      await check('a failing contrast is refused on save with the numbers, at the page, and nothing reaches matui.css', async () => {
        await page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        await setValue('light-mat-text', '#c8c8c8');
        await save();
        expectTrue(page.url().endsWith('/Studio/Brand'), 'url ' + page.url());
        const alert = await page.textContent('[data-validation-summary]');
        expectTrue(alert.includes('Please check the brand.') && alert.includes('Text on the page (light)') && alert.includes(':1, needs 4.50:1'), 'the message: ' + alert.trim().slice(0, 200));
        expect(await page.inputValue('#light-mat-text'), '#c8c8c8', 'the form keeps what was typed');
        expectTrue(!(await css()).includes('#c8c8c8'), 'the colour is in matui.css');
      });
      await check('a radius over 24 px is refused at its field, with the reason', async () => {
        await page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        await setValue('light-mat-radius', '40px');
        await save();
        const field = await page.locator('.mat-field.is-invalid:has(#light-mat-radius) .mat-error').textContent();
        expectTrue(field.includes('above the most'), 'the message: ' + field);
        expectTrue(!(await css()).includes('--mat-radius: 40px'), 'in matui.css');
      });
      await check('a good brand is saved, shows a toast, and reaches /ui/matui.css: light on :root, dark twice and identical', async () => {
        await page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        await setValue('Input_Company', 'Acme Anvils');
        await setValue('light-mat-radius', '12px');
        await setValue('light-mat-bg', '#fafaf7');
        await setValue('dark-mat-bg', '#0c0c0b');
        await save();
        await page.waitForSelector('.mat-toast');
        expectTrue((await page.textContent('.mat-toast')).includes('The brand was saved.'), 'the toast');
        expect(await page.inputValue('#Input_Company'), 'Acme Anvils', 'the company');
        expect(await page.inputValue('#light-mat-radius'), '12px', 'the saved radius');
        const text = await css();
        expectTrue(text.includes(':root {\n  --mat-bg: #fafaf7;\n  --mat-radius: 12px;\n}'), 'light block');
        const dark = ':root[data-theme="dark"] {\n  --mat-bg: #0c0c0b;\n}';
        const system = '@media (prefers-color-scheme: dark) {\n  :root:not([data-theme="light"]):not([data-theme="dark"]) {\n    --mat-bg: #0c0c0b;\n  }\n}';
        expectTrue(text.includes(dark), 'dark block');
        expectTrue(text.includes(system), 'system dark block');
      });
      await check('the site wears the brand: the radius of a button on a page of the Studio is the brand radius', async () => {
        await page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--mat-radius').trim()), '12px', 'the token');
        expect(await page.evaluate(() => getComputedStyle(document.querySelector('.mat-btn--primary')).borderTopLeftRadius), '12px', 'a button');
      });
      await check('the history of the brand lists the revisions and a restore of the first brings the first values back', async () => {
        await page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        await setValue('light-mat-radius', '16px');
        await save();
        await page.click('a.mat-btn:has-text("History")');
        await page.waitForURL('**/Studio/History**');
        // the volume may hold revisions of earlier runs: the two newest are the ones of this check
        const numbers = (await page.locator('section[aria-labelledby="revisions-title"] tbody tr td:first-child .doc-rule-id').allTextContents()).map(Number).sort((a, b) => b - a);
        expectTrue(numbers.length >= 2, 'revisions: ' + numbers);
        const diff = await page.textContent('section[aria-labelledby="changes-title"] .mat-table-wrap');
        expectTrue(diff.includes('light.--mat-radius') && diff.includes('12px') && diff.includes('16px'), 'the diff: ' + diff.trim().slice(0, 200));
        await page.click(`button[aria-label="Restore revision ${numbers[1]}"]`);
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForSelector('.mat-toast');
        expect(await page.locator('section[aria-labelledby="revisions-title"] tbody tr').count(), numbers.length + 1, 'revisions after the restore');
        const text = await css();
        expectTrue(text.includes('--mat-radius: 12px;') && !text.includes('--mat-radius: 16px;'), 'the restored radius in matui.css');
      });
      await check('Reset to built-in asks first and takes the brand out of matui.css again', async () => {
        await page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        await page.click('.mat-btn--danger[data-confirm]');
        await page.click('dialog[open] button[value="ok"]');
        await page.waitForSelector('.mat-toast');
        expectTrue(!(await css()).includes('Brand set in the Studio'), 'the brand is still in matui.css');
        expect(await page.inputValue('#Input_Company'), 'Mat', 'the built-in company');
      });
      await ctx.close();

      const phone = await open(phoneOptions, '/Studio/Login');
      await phone.page.fill('#Name', name);
      await phone.page.fill('#Password', password);
      await phone.page.click('.mat-auth__card button[type="submit"]');
      await phone.page.waitForURL('**/Studio**');
      await check('on a phone the brand page does not scroll sideways, its rows are cards with their labels and the inputs are 44 px high', async () => {
        await phone.page.goto(BASE + '/Studio/Brand', { waitUntil: 'networkidle' });
        const size = await phone.page.evaluate(() => ({ page: document.documentElement.scrollWidth, window: innerWidth }));
        expectTrue(size.page <= size.window, `the page is ${size.page} px wide in a window of ${size.window} px`);
        const row = await phone.page.evaluate(() => {
          const tr = document.querySelector('[data-brand-row]');
          const input = tr.querySelector('input.mat-input');
          const picker = tr.querySelector('input[type="color"]');
          return { display: getComputedStyle(tr).display, label: getComputedStyle(tr.querySelector('td:nth-child(3)'), '::before').content, inputH: input.getBoundingClientRect().height, pickerH: picker.getBoundingClientRect().height };
        });
        expect(row.display, 'block', 'a row is a card');
        expect(row.label, '"Light"', 'the label of a cell');
        expectTrue(row.inputH >= 43.5 && row.pickerH >= 43.5, `targets: ${row.inputH} and ${row.pickerH} px`);
        await phone.page.fill('#light-mat-radius', '14px');
        const radius = await phone.page.evaluate(() => getComputedStyle(document.querySelector('[data-brand-preview]')).getPropertyValue('--mat-radius').trim());
        expect(radius, '14px', 'the preview on a phone');
      });
      await phone.ctx.close();
    }
  }

  // ---------------------------------------------------------------------------------------------------- rating (FORM-22)
  console.log('\nRating');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#rating');
    await page.evaluate(() => document.getElementById('rating').scrollIntoView());
    const group = id => `fieldset.mat-rating:has(#${id})`;
    // where the pointer goes to be on a place of the row: a fraction (0 to 1) of the width of the shapes, in the middle of their height
    const spot = async (id, fraction) => {
      await page.locator(`${group(id)} .mat-rating__stars`).scrollIntoViewIfNeeded();
      const b = await page.locator(`${group(id)} .mat-rating__stars`).boundingBox();
      return { x: b.x + b.width * fraction, y: b.y + b.height / 2 };
    };
    const state = id => page.evaluate(sel => {
      const g = document.querySelector(sel);
      const on = g.querySelector('.mat-rating__input:checked');
      const v = g.querySelector('.mat-rating__value');
      return { checked: on ? on.id : null, fill: g.style.getPropertyValue('--mat-rating-fill'), text: v ? v.textContent : null, previewing: g.classList.contains('is-previewing') };
    }, group(id));
    await page.evaluate(() => {
      window.__rt = [];
      document.addEventListener('mat:rating-change', e => window.__rt.push({ id: e.target.querySelector('.mat-rating__input').name, value: e.detail.value, same: e.detail.rating === e.target }));
    });
    const events = () => page.evaluate(() => window.__rt);

    await check('every rating is wired: the radios are invisible but stay in place and focusable, the shapes are drawn, a display has role img and a label in words', async () => {
      expect(await page.locator('.mat-rating.is-ready').count(), 17, 'ratings that were wired');
      const radio = await page.evaluate(() => {
        const i = document.getElementById('rt-support-3'), r = i.getBoundingClientRect(), s = getComputedStyle(i);
        i.focus();
        return { w: Math.round(r.width), h: Math.round(r.height), display: s.display, focused: document.activeElement === i };
      });
      expectTrue(radio.w <= 2 && radio.h <= 2, 'the radio is one pixel');
      expect(radio.display === 'none', false, 'display none');
      expect(radio.focused, true, 'a radio that cannot be focused');
      expect(await page.locator('#rating .mat-rating:not(.mat-rating--readonly) .mat-rating__stars').count(), 8, 'rows of shapes');
      expect(await page.locator('#rating .mat-rating--readonly[role="img"]').count(), 9, 'displays with role img');
      expect(await page.getAttribute('#rating .mat-rating:not(.mat-rating--readonly) .mat-rating__stars', 'aria-hidden'), 'true', 'the shapes are hidden from a screen reader');
      expect(await page.locator(`${group('rt-support-1')} .mat-rating__cell`).count(), 10, 'five cells in each of the two layers');
    });

    await check('the pointer previews the value it is on, with the text beside the shapes, and the preview goes when it leaves', async () => {
      const at = await spot('rt-support-1', 0.7);
      await page.mouse.move(at.x, at.y);
      expect(await state('rt-support-1'), { checked: null, fill: '80%', text: '4', previewing: true }, 'hovering the fourth star');
      const edge = await spot('rt-support-1', 0.02);
      await page.mouse.move(edge.x, edge.y);
      expect((await state('rt-support-1')).fill, '20%', 'the first star at its left edge');
      await page.mouse.move(5, 5);
      expect(await state('rt-support-1'), { checked: null, fill: '0%', text: 'Not rated', previewing: false }, 'after leaving');
      expect((await events()).length, 0, 'a preview is not a choice');
    });

    await check('a click chooses the value, checks the radio, and tells the page', async () => {
      const at = await spot('rt-support-1', 0.3);
      await page.mouse.click(at.x, at.y);
      await page.mouse.move(5, 5);
      expect(await state('rt-support-1'), { checked: 'rt-support-2', fill: '40%', text: '2', previewing: false }, 'after a click on the second star');
      expect(await events(), [{ id: 'support', value: 2, same: true }], 'mat:rating-change');
      // a click on the chosen one does not clear it where nothing may be cleared
      await page.mouse.click(at.x, at.y);
      expect((await state('rt-support-1')).checked, 'rt-support-2', 'not clearable');
      expect((await events()).length, 1, 'no second event');
    });

    await check('the arrow keys move and choose like radios, the ring is around the shapes, Tab leaves on one stop', async () => {
      await page.focus('#rt-support-2');
      await page.keyboard.press('ArrowRight');
      expect(await state('rt-support-1'), { checked: 'rt-support-3', fill: '60%', text: '3', previewing: false }, 'ArrowRight');
      await page.keyboard.press('ArrowLeft');
      await page.keyboard.press('ArrowLeft');
      expect((await state('rt-support-1')).checked, 'rt-support-1', 'ArrowLeft twice');
      expect((await events()).slice(-1)[0], { id: 'support', value: 1, same: true }, 'the last event');
      const ring = await page.evaluate(() => getComputedStyle(document.querySelector('fieldset.mat-rating:has(#rt-support-1) .mat-rating__stars')).boxShadow);
      expectTrue(ring && ring !== 'none', 'no focus ring around the shapes');
      // five radios are one stop of the Tab key
      await page.keyboard.press('Tab');
      expect(await page.evaluate(() => !!document.activeElement.closest('fieldset.mat-rating:has(#rt-support-1)')), false, 'Tab leaves the group after one stop');
      await page.keyboard.press('Shift+Tab');
      expect(await page.evaluate(() => document.activeElement.id), 'rt-support-1', 'Shift+Tab comes back to the chosen radio');
    });

    await check('with data-clearable a click on the chosen value clears it (to the radio "no rating"), the keys reach it too', async () => {
      expect((await state('rt-overall-1')).checked, 'rt-overall-3', 'it starts at 3');
      const at = await spot('rt-overall-1', 0.5);
      await page.mouse.click(at.x, at.y);
      await page.mouse.move(5, 5);
      expect(await state('rt-overall-1'), { checked: 'rt-overall-0', fill: '0%', text: 'Not rated', previewing: false }, 'cleared');
      expect((await events()).slice(-1)[0], { id: 'overall', value: 0, same: true }, 'the event of the clearing');
      await page.mouse.click(at.x, at.y);
      await page.mouse.move(5, 5);
      expect((await state('rt-overall-1')).checked, 'rt-overall-3', 'chosen again');
    });

    await check('half steps: the left half of a shape is the half value, the right half the whole one', async () => {
      expect(await state('rt-taste-1'), { checked: 'rt-taste-3_5', fill: '70%', text: '3.5', previewing: false }, 'it starts at 3.5');
      const left = await spot('rt-taste-1', 1.25 / 5);
      await page.mouse.move(left.x, left.y);
      expect((await state('rt-taste-1')).text, '1.5', 'preview of the left half of the second star');
      await page.mouse.click(left.x, left.y);
      expect(await state('rt-taste-1'), { checked: 'rt-taste-1_5', fill: '30%', text: '1.5', previewing: false }, 'chosen');
      const right = await spot('rt-taste-1', 1.75 / 5);
      await page.mouse.click(right.x, right.y);
      await page.mouse.move(5, 5);
      expect(await state('rt-taste-1'), { checked: 'rt-taste-2', fill: '40%', text: '2', previewing: false }, 'the right half');
      await page.focus('#rt-taste-2');
      await page.keyboard.press('ArrowRight');
      expect((await state('rt-taste-1')).checked, 'rt-taste-2_5', 'a key moves by a half');
    });

    await check('the filled part is the share of the row: the shapes it covers are the ones of the value, in the accent', async () => {
      const r = await page.evaluate(() => {
        const g = document.querySelector('fieldset.mat-rating:has(#rt-support-1)');
        const fill = g.querySelector('.mat-rating__layer--fill'), empty = g.querySelector('.mat-rating__layer--empty');
        const st = g.querySelector('.mat-rating__stars').getBoundingClientRect(), f = fill.getBoundingClientRect(), e = empty.getBoundingClientRect();
        return { sameBox: Math.abs(f.width - e.width) < 1 && Math.abs(f.left - e.left) < 1, boxWidth: Math.round(st.width), cell: Math.round(g.querySelector('.mat-rating__cell').getBoundingClientRect().width), fillColour: getComputedStyle(fill).color, emptyFill: getComputedStyle(empty.querySelector('svg')).fill, filledFill: getComputedStyle(fill.querySelector('svg')).fill };
      });
      expect(r.sameBox, true, 'both layers cover the same box');
      expect(r.boxWidth, r.cell * 5, 'five equal cells');
      expect(r.fillColour, (await accentOf(page)).accent, 'the filled shapes are the accent');
      expect(r.emptyFill, 'none', 'an empty shape is an outline');
      expect(r.filledFill !== 'none', true, 'a filled shape is filled');
    });

    await check('a display is partly filled by its value, says it in words (with the count) and writes the number beside it', async () => {
      const d = await page.evaluate(() => Array.from(document.querySelectorAll('#rating .mat-rating--readonly')).map(e => ({
        fill: e.style.getPropertyValue('--mat-rating-fill'), label: e.getAttribute('aria-label'), value: (e.querySelector('.mat-rating__value') || {}).textContent || null, count: (e.querySelector('.mat-rating__count') || {}).textContent || null,
      })));
      expect(d[0], { fill: '90%', label: 'Rated 4.5 out of 5, 128 ratings', value: '4.5', count: '(128)' }, 'the average');
      expect(d[1], { fill: '74%', label: 'Rated 3.7 out of 5, 1,284 ratings', value: '3.7', count: '(1,284)' }, 'a value that is not a half');
      expect(d[2].label, 'Rated 5 out of 5, 1 rating', 'one rating');
      expect(d[3], { fill: '0%', label: 'Not rated', value: 'Not rated', count: null }, 'no value');
      expect(d[4], { fill: '70%', label: 'Rated 3.5 out of 5', value: null, count: null }, 'hearts without a number');
      expect(d[5].fill, '70%', 'seven of ten');
      expect(d[5].label, 'Rated 7 out of 10', 'ten dots');
      // a display is not interactive
      expect(await page.locator('#rating .mat-rating--readonly input').count(), 0, 'radios in a display');
    });

    await check('a group that a fieldset disables ignores the pointer', async () => {
      const before = await state('rt-closed-1');
      const at = await spot('rt-closed-1', 0.9);
      await page.mouse.move(at.x, at.y);
      expect((await state('rt-closed-1')).previewing, false, 'no preview');
      await page.mouse.click(at.x, at.y);
      expect(await state('rt-closed-1'), before, 'no choice');
      expect(await page.evaluate(() => getComputedStyle(document.querySelector('fieldset.mat-rating:has(#rt-closed-1) .mat-rating__stars')).opacity), '0.5', 'dimmed');
    });

    await check('in a form: required with the message at the field, the chosen value is posted, the message goes with a choice', async () => {
      await page.evaluate(() => {
        const f = document.querySelector('form:has(#rt-req-1)');
        window.__rtSent = [];
        f.addEventListener('submit', e => { const sent = !e.defaultPrevented; e.preventDefault(); if (sent) { window.__rtSent.push(new FormData(f).get('rating')); } });
      });
      await page.click('form:has(#rt-req-1) button[type="submit"]');
      expect(await page.evaluate(() => window.__rtSent), [], 'nothing is sent without a rating');
      expect(await page.textContent('form:has(#rt-req-1) .mat-error'), 'Please choose a rating.', 'the message');
      expect(await page.evaluate(() => document.querySelector('fieldset:has(#rt-req-1)').classList.contains('is-invalid')), true, 'the field is marked');
      expect(await page.evaluate(() => document.activeElement.name), 'rating', 'the focus is in the group');
      const empty = await page.evaluate(() => getComputedStyle(document.querySelector('fieldset:has(#rt-req-1) .mat-rating__layer--empty')).color);
      expect(contrastOf(empty, 'rgb(255, 255, 255)') > 1.5, true, 'the empty shapes have a colour of their own');
      const at = await spot('rt-req-1', 0.7);
      await page.mouse.click(at.x, at.y);
      expect(await page.textContent('form:has(#rt-req-1) .mat-error'), '', 'the message goes with the choice');
      await page.click('form:has(#rt-req-1) button[type="submit"]');
      expect(await page.evaluate(() => window.__rtSent), ['4'], 'the value that is posted');
    });

    await check('a form that is reset puts the shapes back', async () => {
      await page.evaluate(() => {
        const f = document.querySelector('form:has(#rt-req-1)');
        f.reset();
      });
      await page.waitForTimeout(100);
      expect(await state('rt-req-1'), { checked: null, fill: '0%', text: 'Not rated', previewing: false }, 'after the reset');
    });

    await check('the API reads and sets a value, a display is redrawn, new ratings are wired by refresh and the texts follow the language of the page', async () => {
      const r = await page.evaluate(() => {
        const taste = document.querySelector('fieldset:has(#rt-taste-1)');
        const out = {};
        out.set = MatUi.rating.set(taste, 4.5);
        out.get = MatUi.rating.get(taste);
        out.checked = taste.querySelector('input:checked').id;
        out.cleared = MatUi.rating.set(taste, 0);
        out.after = MatUi.rating.get(taste);
        const shown = document.querySelector('#rating .mat-rating--readonly');
        MatUi.rating.set(shown, 2);
        out.shown = [shown.style.getPropertyValue('--mat-rating-fill'), shown.getAttribute('aria-label')];
        document.documentElement.lang = 'de';
        const host = document.createElement('div');
        host.innerHTML = '<span class="mat-rating mat-rating--readonly" data-rating data-value="4.5" data-count="2" data-show-value>4.5</span><span class="mat-rating mat-rating--readonly" data-rating data-value="3" aria-label="Eigener Text">3</span>';
        document.body.appendChild(host);
        MatUi.refresh(host);
        out.de = [host.children[0].getAttribute('aria-label'), host.children[0].querySelector('.mat-rating__value').textContent, host.children[1].getAttribute('aria-label')];
        document.documentElement.lang = 'en';
        host.remove();
        return out;
      });
      expect(r.set, 4.5, 'set returns the value');
      expect(r.get, 4.5, 'get');
      expect(r.checked, 'rt-taste-4_5', 'the radio');
      expect(r.cleared, 0, 'a clear');
      expect(r.after, 0, 'get after');
      expect(r.shown, ['40%', 'Rated 2 out of 5, 128 ratings'], 'a display is redrawn');
      expect(r.de, ['Bewertet mit 4,5 von 5, 2 Bewertungen', '4,5', 'Eigener Text'], 'German, and a label of the author');
    });

    await check('no horizontal scroll on the page and the shapes sit in the dark theme as they do in the light one', async () => {
      await page.evaluate(() => document.documentElement.setAttribute('data-theme', 'dark'));
      const dark = await page.evaluate(() => getComputedStyle(document.querySelector('fieldset.mat-rating:has(#rt-support-1) .mat-rating__layer--fill')).color);
      await page.evaluate(() => document.documentElement.setAttribute('data-theme', 'light'));
      const light = await page.evaluate(() => getComputedStyle(document.querySelector('fieldset.mat-rating:has(#rt-support-1) .mat-rating__layer--fill')).color);
      expectTrue(dark !== light, 'the accent has a dark value');
      await page.evaluate(() => document.documentElement.removeAttribute('data-theme'));
    });
    await ctx.close();

    const phone = await open(phoneOptions, '/Components#rating');
    const pg = phone.page;
    await pg.evaluate(() => document.getElementById('rating').scrollIntoView());
    await check('on a phone: the targets are 44 px (36 px small), ten dots fit the screen, nothing scrolls sideways', async () => {
      const m = await pg.evaluate(() => {
        const h = id => Math.round(document.querySelector(`fieldset.mat-rating:has(#${id}) .mat-rating__cell`).getBoundingClientRect().height);
        const nps = document.querySelector('fieldset.mat-rating:has(#rt-nps-1) .mat-rating__stars').getBoundingClientRect();
        const cell = document.querySelector('fieldset.mat-rating:has(#rt-nps-1) .mat-rating__cell').getBoundingClientRect();
        return { md: h('rt-support-1'), sm: h('rt-nps-1'), lg: h('rt-heart-1'), npsRight: Math.round(nps.right), npsCell: Math.round(cell.width), width: innerWidth, sideways: document.documentElement.scrollWidth > innerWidth };
      });
      expectTrue(m.md >= 44, `targets are ${m.md} px high`);
      expectTrue(m.sm >= 36, `small targets are ${m.sm} px high`);
      expectTrue(m.lg >= 44, `large targets are ${m.lg} px high`);
      expectTrue(m.npsRight <= m.width, 'ten dots run over the edge');
      expect(m.sideways, false, 'sideways scroll');
    });
    await check('a tap chooses the value and shows no preview (a touch has no hover)', async () => {
      const b = await pg.locator('fieldset.mat-rating:has(#rt-support-1) .mat-rating__stars').boundingBox();
      await pg.touchscreen.tap(b.x + b.width * 0.7, b.y + b.height / 2);
      const s = await pg.evaluate(() => {
        const g = document.querySelector('fieldset.mat-rating:has(#rt-support-1)');
        return { checked: g.querySelector('input:checked').id, previewing: g.classList.contains('is-previewing'), fill: g.style.getPropertyValue('--mat-rating-fill') };
      });
      expect(s, { checked: 'rt-support-4', previewing: false, fill: '80%' }, 'after a tap');
    });
    await phone.ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- tree view (LIST-17)
  console.log('\nTree view');
  {
    // helpers that live in the page: an item is found by the text of its label
    const injectTree = page => page.evaluate(() => {
      window.__log = [];
      ['mat:tree-select', 'mat:tree-toggle', 'mat:tree-load', 'mat:tree-filter'].forEach(type => document.addEventListener(type, e => {
        const d = e.detail || {};
        window.__log.push({ type, name: d.item ? d.item.querySelector('.mat-tree__label').textContent.trim() : '', open: d.open, values: d.values, value: d.value, shown: d.shown, total: d.total });
      }));
      const labelOf = i => (i.querySelector(':scope > .mat-tree__row .mat-tree__label') || { textContent: '' }).textContent.trim();
      window.__tr = {
        item: (id, name) => [...document.querySelectorAll('#' + id + ' .mat-tree__item')].find(i => labelOf(i) === name),
        info(id, name) {
          const i = this.item(id, name);
          if (!i) { return null; }
          return {
            role: i.getAttribute('role'), level: +i.getAttribute('aria-level'), size: +i.getAttribute('aria-setsize'), pos: +i.getAttribute('aria-posinset'),
            expanded: i.getAttribute('aria-expanded'), selected: i.getAttribute('aria-selected'), checked: i.getAttribute('aria-checked'), hidden: i.hidden,
            tab: i.tabIndex, busy: i.getAttribute('aria-busy'), loading: i.classList.contains('is-loading'), lazy: i.hasAttribute('data-lazy'),
            names: (i.getAttribute('aria-labelledby') || '').split(' ').map(x => (document.getElementById(x) || { textContent: '?' }).textContent.trim()),
            shown: i.getClientRects().length > 0, level0: i.style.getPropertyValue('--mat-tree-level'),
            group: (i.querySelector(':scope > ul') || { getAttribute: () => null }).getAttribute('role'),
          };
        },
        focus(id, name) { this.item(id, name).focus(); },
        active() {
          const a = document.activeElement;
          const i = a && a.closest ? a.closest('.mat-tree__item') : null;
          return i && a === i ? labelOf(i) : (a ? a.tagName.toLowerCase() + '.' + a.className : 'nothing');
        },
        mark(id, name, part) {
          document.querySelectorAll('[data-e2e]').forEach(n => n.removeAttribute('data-e2e'));
          const i = this.item(id, name);
          (i.querySelector(':scope > .mat-tree__row ' + (part === 'row' ? '.mat-tree__label' : '.mat-tree__' + part))).setAttribute('data-e2e', 'x');
        },
        live(id) { return document.getElementById(id).nextElementSibling.textContent; },
      };
    });
    const info = (page, id, name) => page.evaluate(([i, n]) => window.__tr.info(i, n), [id, name]);
    const active = page => page.evaluate(() => window.__tr.active());
    const focusItem = (page, id, name) => page.evaluate(([i, n]) => window.__tr.focus(i, n), [id, name]);
    const press = async (page, ...keys) => { for (const k of keys) { await page.keyboard.press(k); } };
    const clickPart = async (page, id, name, part) => { await page.evaluate(([i, n, p]) => window.__tr.mark(i, n, p), [id, name, part]); await page.click('[data-e2e="x"]'); };
    const logOf = (page, type) => page.evaluate(t => window.__log.filter(l => l.type === t), type);
    const FILES = 'tr-files', AREAS = 'tr-areas', LAZY = 'tr-lazy';
    const fresh = async (options, url) => {
      const r = await open(options, url);
      await r.page.evaluate(() => { localStorage.removeItem('mat-tree'); });
      await r.page.reload();
      await r.page.waitForFunction(() => document.readyState === 'complete');
      await r.page.evaluate(() => document.getElementById('tree').scrollIntoView());
      await injectTree(r.page);
      return r;
    };

    const { ctx, page } = await fresh(desktopOptions, '/Components#tree');

    await check('the tree has the roles and the numbers of the ARIA tree pattern, with a name for every item', async () => {
      expect(await page.getAttribute(`#${FILES}`, 'role'), 'tree', 'the role of the tree');
      expect(await page.getAttribute(`#${FILES}`, 'aria-label'), 'Files', 'the name of the tree');
      expect(await info(page, FILES, 'Documents'), { role: 'treeitem', level: 1, size: 3, pos: 1, expanded: 'true', selected: 'false', checked: null, hidden: false, tab: -1, busy: null, loading: false, lazy: false, names: ['Documents', '6'], shown: true, level0: '0', group: 'group' }, 'the first item');
      const q2 = await info(page, FILES, 'Q2 report.pdf');
      expect([q2.level, q2.size, q2.pos, q2.expanded, q2.selected, q2.level0], [4, 2, 2, null, 'true', '3'], 'a leaf on the fourth level: no aria-expanded, the one that was chosen in the markup');
      expect((await info(page, FILES, 'Contracts')).expanded, 'false', 'a closed folder');
      expect((await info(page, FILES, '2026')).expanded, 'true', 'data-expanded opens a folder');
      expect((await info(page, FILES, 'Archive')).expanded, null, 'an item without children has no aria-expanded');
      expect((await info(page, FILES, 'Archive')).names, ['Archive'], 'the name is the label');
    });

    await check('the whole tree is one tab stop, on the chosen item, and the buttons in the rows of the others are not in the tab order', async () => {
      expect(await page.$$eval(`#${FILES} li[tabindex="0"]`, l => l.length), 1, 'items with tabindex 0');
      expect((await info(page, FILES, 'Q2 report.pdf')).tab, 0, 'the chosen item has it');
      expect(await page.$$eval(`#${LAZY} > li:nth-child(n+2) .mat-tree__actions button[tabindex="0"]`, b => b.length), 0, 'no action button of a row without the stop is in the tab order');
      await page.focus('#tr-files-q');
      await page.keyboard.press('Tab');
      expect(await active(page), 'Q2 report.pdf', 'Tab from the field lands on the tree');
      await page.keyboard.press('Tab');
      expect(await page.evaluate(() => document.activeElement.textContent.trim()), 'Open all', 'the next Tab leaves the tree');
      await page.keyboard.press('Shift+Tab');
      expect(await active(page), 'Q2 report.pdf', 'Shift+Tab comes back to the same item');
    });

    await check('Up, Down, Left and Right walk the items that are shown, open and close folders and go to the parent and the first child', async () => {
      await focusItem(page, FILES, 'Q2 report.pdf');
      await press(page, 'ArrowDown');
      expect(await active(page), '2025', 'Down');
      await press(page, 'ArrowUp');
      expect(await active(page), 'Q2 report.pdf', 'Up');
      await press(page, 'ArrowLeft');
      expect(await active(page), '2026', 'Left on a leaf goes to the parent');
      await press(page, 'ArrowLeft');
      expect((await info(page, FILES, '2026')).expanded, 'false', 'Left on an open folder closes it');
      expect((await info(page, FILES, 'Q1 report.pdf')).shown, false, 'what is below it is not shown');
      await press(page, 'ArrowDown');
      expect(await active(page), '2025', 'Down skips the closed folder');
      await press(page, 'ArrowUp', 'ArrowRight');
      expect((await info(page, FILES, '2026')).expanded, 'true', 'Right on a closed folder opens it');
      await press(page, 'ArrowRight');
      expect(await active(page), 'Q1 report.pdf', 'Right on an open folder goes to its first child');
      await press(page, 'Home');
      expect(await active(page), 'Documents', 'Home');
      await press(page, 'End');
      expect(await active(page), 'Archive', 'End');
      await press(page, 'ArrowDown');
      expect(await active(page), 'Archive', 'Down at the end stays');
    });

    await check('* opens every folder of the level, and the first letters of a name go to the next item that begins with them', async () => {
      await focusItem(page, FILES, 'Contracts');
      await page.keyboard.press('*');
      expect((await info(page, FILES, 'Contracts')).expanded, 'true', '* opened Contracts, a sibling of Reports');
      await focusItem(page, FILES, 'Documents');
      await page.keyboard.press('p');
      expect(await active(page), 'Photos', 'p');
      await page.waitForTimeout(800);
      await page.keyboard.press('a');
      expect(await active(page), 'Archive', 'a, after a pause: a new word');
      await page.waitForTimeout(800);
      await page.keyboard.press('n');
      expect(await active(page), 'Notes.txt', 'n wraps around to the next one');
      await page.waitForTimeout(800);
      await page.keyboard.type('q2', { delay: 40 });
      expect(await active(page), 'Q2 report.pdf', 'q2 typed together is one word');
    });

    await check('Enter and Space choose one item and send mat:tree-select with what is chosen', async () => {
      await page.evaluate(() => { window.__log.length = 0; });
      await focusItem(page, FILES, 'Notes.txt');
      await press(page, 'Enter');
      expect([(await info(page, FILES, 'Notes.txt')).selected, (await info(page, FILES, 'Q2 report.pdf')).selected], ['true', 'false'], 'Enter chose Notes and let go of Q2');
      await focusItem(page, FILES, 'Q1 report.pdf');
      await press(page, 'Space');
      expect((await info(page, FILES, 'Q1 report.pdf')).selected, 'true', 'Space');
      expect((await logOf(page, 'mat:tree-select')).map(l => l.values), [['Notes.txt'], ['Q1 report.pdf']], 'the events say what is chosen');
      expect(await page.evaluate(() => MatUi.tree.get(document.getElementById('tr-files')).values), ['Q1 report.pdf'], 'MatUi.tree.get');
      expect(await page.$$eval(`#${FILES} li[tabindex="0"]`, l => l.length), 1, 'still one tab stop');
    });

    await check('a click on the chevron opens and closes without choosing, a click on the row chooses, and the focus leaves a folder that closes over it', async () => {
      await page.evaluate(() => { window.__log.length = 0; });
      await clickPart(page, FILES, 'Reports', 'toggle');
      expect((await info(page, FILES, 'Reports')).expanded, 'false', 'closed by the chevron');
      expect((await info(page, FILES, 'Q1 report.pdf')).selected, 'true', 'the chosen item is as it was');
      expect((await logOf(page, 'mat:tree-toggle')).map(l => [l.name, l.open]), [['Reports', false]], 'mat:tree-toggle');
      await clickPart(page, FILES, 'Reports', 'toggle');
      await focusItem(page, FILES, 'Q1 report.pdf');
      await clickPart(page, FILES, 'Documents', 'toggle');
      expect(await active(page), 'Documents', 'the focus was inside Documents and goes to it');
      await clickPart(page, FILES, 'Documents', 'toggle');
      await clickPart(page, FILES, 'Contracts', 'row');
      expect((await info(page, FILES, 'Contracts')).selected, 'true', 'a click on the row chooses (a folder can be chosen)');
      expect((await info(page, FILES, 'Contracts')).expanded, 'true', 'and does not open or close it');
    });

    await check('a chosen row is a tint behind the whole row with a stronger label and no bar at its side', async () => {
      const s = await page.$eval(`#${FILES}`, tree => {
        const sel = tree.querySelector('[aria-selected="true"] > .mat-tree__row');
        const other = tree.querySelector('[aria-selected="false"] > .mat-tree__row');
        const r = sel.getBoundingClientRect(), t = tree.getBoundingClientRect();
        const cs = getComputedStyle(sel);
        return {
          bg: cs.backgroundColor, otherBg: getComputedStyle(other).backgroundColor, weight: getComputedStyle(sel.querySelector('.mat-tree__label')).fontWeight,
          left: cs.borderLeftWidth, shadow: cs.boxShadow, full: Math.abs(r.width - t.width) < 2,
        };
      });
      expectTrue(s.bg !== 'rgba(0, 0, 0, 0)' && s.bg !== s.otherBg, 'the chosen row has a tint: ' + s.bg + ' against ' + s.otherBg);
      expect(+s.weight >= 600, true, 'a stronger label: ' + s.weight);
      expect(s.left, '0px', 'no bar at the side');
      expect(s.full, true, 'the tint reaches both edges of the tree');
    });

    await ctx.close();
    const second = await fresh(desktopOptions, '/Components#tree');
    const p2 = second.page;

    await check('a filter hides what does not match, keeps and opens the folders above a match, counts the items that are shown and says the matches', async () => {
      await p2.fill('#tr-files-q', 'report');
      const names = ['Documents', 'Contracts', 'Reports', '2026', 'Q1 report.pdf', 'Q2 report.pdf', '2025', 'Annual report.pdf', 'Notes.txt', 'Photos', 'Archive'];
      const shown = {};
      for (const n of names) { shown[n] = (await info(p2, FILES, n)).shown; }
      expect(shown, { Documents: true, Contracts: false, Reports: true, '2026': true, 'Q1 report.pdf': true, 'Q2 report.pdf': true, '2025': true, 'Annual report.pdf': true, 'Notes.txt': false, Photos: false, Archive: false }, 'what is shown');
      expect((await info(p2, FILES, '2025')).expanded, 'true', 'the folder that holds a match is open');
      const reports = await info(p2, FILES, 'Reports');
      expect([reports.size, reports.pos], [1, 1], 'Reports is the only item shown in Documents');
      expect(await p2.evaluate(() => window.__tr.live('tr-files')), '4 matches', 'the live region');
      expect(await p2.evaluate(() => window.__tr.info('tr-files', 'Q1 report.pdf').tab !== undefined), true);
      await p2.fill('#tr-files-q', 'lease');
      expect(await p2.evaluate(() => window.__tr.live('tr-files')), '1 match', 'one match');
      expect((await info(p2, FILES, 'Contracts')).expanded, 'true', 'Contracts holds it');
      expect((await info(p2, FILES, '2025')).expanded, 'false', 'what held the first match is closed again');
      await p2.fill('#tr-files-q', 'zzz');
      expect(await p2.isVisible('#tr-files + span + .mat-tree__empty'), true, 'the text for no match');
      expect(await p2.textContent('#tr-files + span + .mat-tree__empty'), 'No matches', 'its text');
      expect(await p2.evaluate(() => window.__tr.live('tr-files')), 'No matches', 'the live region says it too');
      await p2.fill('#tr-files-q', '');
      expect((await info(p2, FILES, 'Contracts')).shown, true, 'all items are back');
      expect([(await info(p2, FILES, 'Contracts')).expanded, (await info(p2, FILES, '2025')).expanded], ['false', 'false'], 'every folder is as it was before the filter');
      expect(await p2.isVisible('#tr-files + span + .mat-tree__empty'), false, 'the text for no match is gone');
      const events = await logOf(p2, 'mat:tree-filter');
      expect(events.map(e => e.shown), [4, 1, 0, 0], 'mat:tree-filter');
    });

    await check('the texts that the script writes follow the language of the page', async () => {
      await p2.evaluate(() => { document.documentElement.lang = 'de'; });
      await p2.fill('#tr-files-q', 'report');
      expect(await p2.evaluate(() => window.__tr.live('tr-files')), '4 Treffer', 'German');
      await p2.fill('#tr-files-q', 'zzz');
      expect(await p2.textContent('#tr-files + span + .mat-tree__empty'), 'No matches', 'the text of the page stays, once it is there');
      await p2.fill('#tr-files-q', '');
      await p2.evaluate(() => { document.documentElement.lang = 'en'; });
    });

    await check('what the user opened and closed is remembered per tree and item, and the tree is as drawn for what was never touched', async () => {
      await clickPart(p2, FILES, 'Photos', 'toggle');
      await clickPart(p2, FILES, 'Documents', 'toggle');
      await p2.reload();
      await p2.waitForFunction(() => document.readyState === 'complete');
      await injectTree(p2);
      expect([(await info(p2, FILES, 'Photos')).expanded, (await info(p2, FILES, 'Documents')).expanded], ['true', 'false'], 'Photos opened and Documents closed stay so');
      expect((await info(p2, FILES, 'Reports')).expanded, 'true', 'Reports was not touched: as drawn');
      expect(await p2.evaluate(() => Object.keys(JSON.parse(localStorage.getItem('mat-tree'))).sort()), ['matdesign:demo-files:Documents', 'matdesign:demo-files:Photos'], 'saved per tree key and item');
      await p2.evaluate(() => { localStorage.removeItem('mat-tree'); });
    });

    await check('with several chosen a parent is checked, in between or unchecked by its items, and checking it checks them all', async () => {
      await p2.reload();
      await p2.waitForFunction(() => document.readyState === 'complete');
      await injectTree(p2);
      const states = async () => p2.evaluate(() => ['Ground floor', 'Kitchen', 'Living room', 'Hallway', 'First floor', 'Study', 'Outside'].map(n => window.__tr.info('tr-areas', n).checked));
      expect(await states(), ['mixed', 'true', 'true', 'false', 'false', 'false', 'false'], 'as the markup says: two of three, and the folder in between');
      const box = await p2.$eval('#tr-areas > li:first-child > .mat-tree__row', row => ({ on: getComputedStyle(row.querySelector('.mat-tree__check-on')).display, mixed: getComputedStyle(row.querySelector('.mat-tree__check-mixed')).display, bg: getComputedStyle(row.querySelector('.mat-tree__check')).backgroundColor }));
      expect([box.on, box.mixed], ['none', 'block'], 'a dash, not a check, in the box in between');
      await clickPart(p2, AREAS, 'Hallway', 'check');
      expect((await states())[0], 'true', 'the third item makes the folder checked');
      await clickPart(p2, AREAS, 'Ground floor', 'check');
      expect(await states(), ['false', 'false', 'false', 'false', 'false', 'false', 'false'], 'a click on a checked folder unchecks all of it');
      await clickPart(p2, AREAS, 'Ground floor', 'check');
      expect(await states(), ['true', 'true', 'true', 'true', 'false', 'false', 'false'], 'and a click on an unchecked one checks all of it');
      await focusItem(p2, AREAS, 'Study');
      await press(p2, 'Space');
      expect([(await info(p2, AREAS, 'Study')).checked, (await info(p2, AREAS, 'First floor')).checked], ['true', 'mixed'], 'Space ticks the item and the folder is in between');
      await clickPart(p2, AREAS, 'Study', 'row');
      expect([(await info(p2, AREAS, 'Study')).checked, (await info(p2, AREAS, 'First floor')).checked], ['false', 'false'], 'a click on the row ticks and unticks too');
      expect(await p2.evaluate(() => MatUi.tree.get(document.getElementById('tr-areas')).values), ['Ground floor', 'Kitchen', 'Living room', 'Hallway'], 'MatUi.tree.get');
      expect((await logOf(p2, 'mat:tree-select')).pop().values, ['Ground floor', 'Kitchen', 'Living room', 'Hallway'], 'mat:tree-select');
      expect(await p2.evaluate(() => window.__tr.live('tr-areas')), '3 selected', 'the live region counts the items, not the folders');
    });

    await check('an item with data-lazy loads its children when it opens: a loading state, mat:tree-load, then the items with their level', async () => {
      await p2.evaluate(() => { window.__log.length = 0; });
      await clickPart(p2, LAZY, 'Tools', 'toggle');
      const during = await info(p2, LAZY, 'Tools');
      expect([during.loading, during.busy, during.expanded], [true, 'true', 'true'], 'loading');
      expect(await p2.evaluate(() => getComputedStyle(document.querySelector('#tr-lazy > li:first-child > .mat-tree__row > .mat-tree__toggle'), '::after').animationName), 'mat-spin', 'a spinner in the place of the chevron');
      expect((await logOf(p2, 'mat:tree-load')).map(l => [l.name, l.value]), [['Tools', 'tools']], 'mat:tree-load');
      await p2.waitForFunction(() => window.__tr.item('tr-lazy', 'Hand tools'));
      const after = await info(p2, LAZY, 'Tools');
      expect([after.loading, after.busy, after.lazy, after.group], [false, null, false, 'group'], 'loaded');
      const hand = await info(p2, LAZY, 'Hand tools');
      expect([hand.level, hand.size, hand.pos, hand.lazy, hand.expanded, hand.level0], [2, 3, 1, true, 'false', '1'], 'a child that is lazy itself');
      await clickPart(p2, LAZY, 'Hand tools', 'toggle');
      await p2.waitForFunction(() => window.__tr.item('tr-lazy', 'Hammers'));
      expect((await info(p2, LAZY, 'Hammers')).level, 3, 'one level deeper');
      expect(await p2.$$eval('#tr-lazy li[tabindex="0"]', l => l.length), 1, 'still one tab stop');
    });

    await check('a load that fails closes the item and can be tried again; an answer without items makes it a leaf; the buttons of a row are in the tab order only for the row with the stop', async () => {
      await p2.evaluate(() => {
        window.__answer = null;
        document.getElementById('tr-lazy').addEventListener('mat:tree-load', e => { e.stopPropagation(); MatUi.tree.setChildren(e.detail.item, window.__answer); });
      });
      await clickPart(p2, LAZY, 'Garden', 'toggle');
      const failed = await info(p2, LAZY, 'Garden');
      expect([failed.expanded, failed.lazy, failed.loading], ['false', true, false], 'closed again and still lazy');
      expect(await p2.evaluate(() => window.__tr.live('tr-lazy')), 'Garden could not be loaded', 'a screen reader is told');
      await p2.evaluate(() => { window.__answer = ''; });
      await clickPart(p2, LAZY, 'Garden', 'toggle');
      const empty = await info(p2, LAZY, 'Garden');
      expect([empty.expanded, empty.lazy], [null, false], 'no children: a leaf');
      expect(await p2.evaluate(() => window.__tr.live('tr-lazy')), 'Garden is empty', 'and it is said');
      await focusItem(p2, LAZY, 'Tools');
      expect(await p2.$$eval('#tr-lazy > li:first-child > .mat-tree__row .mat-tree__actions button', b => b.map(x => x.tabIndex)), [0, 0], 'the buttons of the row that has the stop');
      expect(await p2.$$eval('#tr-lazy > li:nth-child(2) > .mat-tree__row .mat-tree__actions button', b => b.map(x => x.tabIndex)), [-1, -1], 'the buttons of another row');
      await press(p2, 'Tab');
      expect(await p2.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Add to Tools', 'Tab goes into the actions of the row');
      await press(p2, 'ArrowDown');
      expect(await p2.evaluate(() => window.__tr.active() !== 'Hand tools'), true, 'the keys of the tree do not run on a button of the actions');
    });

    await check('a tree of links: Enter follows the link of the item, a click on the row opens a folder, and the current page is the chosen item', async () => {
      expect((await info(p2, 'tr-nav', 'Installation')).selected, 'true', 'the current item');
      await clickPart(p2, 'tr-nav', 'Settings', 'row');
      expect((await info(p2, 'tr-nav', 'Settings')).expanded, 'true', 'data-tree-toggle="row" opens it');
      expect(await p2.evaluate(() => document.querySelector('#tr-nav a').tabIndex), -1, 'the link is reached through the item');
      await p2.evaluate(() => { window.__hits = 0; document.querySelector('#tr-nav a[href]').addEventListener('click', e => { e.preventDefault(); window.__hits += 1; }); });
      await focusItem(p2, 'tr-nav', 'Installation');
      await press(p2, 'Enter');
      expect(await p2.evaluate(() => window.__hits), 1, 'Enter clicked the link');
    });

    await check('the API opens, closes and chooses, and a page can add trees later', async () => {
      const r = await p2.evaluate(() => {
        const t = MatUi.tree, el = document.getElementById('tr-files');
        t.collapse(window.__tr.item('tr-files', 'Reports'));
        const closed = window.__tr.info('tr-files', 'Reports').expanded;
        t.expand(window.__tr.item('tr-files', 'Q1 report.pdf'));
        const opened = window.__tr.info('tr-files', 'Reports').expanded;
        t.select(window.__tr.item('tr-files', 'Archive'));
        const chosen = t.get(el).values;
        t.setAll(el, false);
        const none = [...el.querySelectorAll('.mat-tree__item[aria-expanded="true"]')].length;
        t.setAll(el, true);
        const box = document.createElement('div');
        box.innerHTML = '<ul class="mat-tree" id="tr-late" data-tree aria-label="Late"><li class="mat-tree__item"><div class="mat-tree__row"><span class="mat-tree__label">One</span></div></li></ul>';
        document.body.appendChild(box);
        MatUi.refresh(box);
        return { closed, opened, chosen, none, late: document.getElementById('tr-late').getAttribute('role'), lateItem: box.querySelector('li').getAttribute('role') };
      });
      expect(r, { closed: 'false', opened: 'true', chosen: ['Archive'], none: 0, late: 'tree', lateItem: 'treeitem' }, 'what the API did');
    });

    await second.ctx.close();

    const dark = await fresh({ ...desktopOptions, colorScheme: 'dark' }, '/Components#tree');
    // the colours of the chosen row, of a row that is not, and of what is behind the tree, as the browser computes them
    const surfaces = page => page.$eval('#' + FILES, tree => {
      const backdrop = el => { for (let e = el; e; e = e.parentElement) { const c = getComputedStyle(e).backgroundColor; if (c !== 'rgba(0, 0, 0, 0)' && c !== 'transparent') { return c; } } return 'rgb(255, 255, 255)'; };
      const sel = tree.querySelector('[aria-selected="true"] > .mat-tree__row'), other = tree.querySelector('[aria-selected="false"] > .mat-tree__row');
      return {
        label: getComputedStyle(sel.querySelector('.mat-tree__label')).color, other: getComputedStyle(other.querySelector('.mat-tree__label')).color,
        selBg: getComputedStyle(sel).backgroundColor, bgOther: getComputedStyle(other).backgroundColor, under: backdrop(tree),
      };
    });
    const alphaOf = text => { const n = (text.match(/-?\d*\.?\d+/g) || []).map(Number); return n.length > 3 ? n[3] : 1; };
    const over = (top, base) => { const a = alphaOf(top), t = parseColour(top), b = parseColour(base); return `rgb(${[0, 1, 2].map(i => Math.round(t[i] * a + b[i] * (1 - a))).join(', ')})`; };
    await check('in the dark theme the tree has its tint and its text keeps the contrast of AA on it (tokens only)', async () => {
      const s = await surfaces(dark.page);
      const c = contrastOf(s.label, over(s.selBg, s.under));
      expectTrue(c >= 4.5, `the label on the chosen row is ${c.toFixed(2)}:1 (${s.label} on ${s.selBg} over ${s.under})`);
      const o = contrastOf(s.other, s.under);
      expectTrue(o >= 4.5, `the label on the surface is ${o.toFixed(2)}:1`);
      expect(s.bgOther, 'rgba(0, 0, 0, 0)', 'a row that is not chosen has no fill');
      expectTrue(alphaOf(s.selBg) > 0.05, 'the chosen row has a tint: ' + s.selBg);
    });
    await dark.ctx.close();

    const phone = await fresh(phoneOptions, '/Components#tree');
    const pp = phone.page;
    await check('on a phone a row is 44 px high, the chevron can be hit, a long label wraps and nothing scrolls sideways', async () => {
      await pp.evaluate(() => { window.__tr.item('tr-files', 'Notes.txt').querySelector('.mat-tree__label').textContent = 'A very long name for a note that goes on and on and on, notes-from-the-meeting-with-everybody-about-the-budget-of-next-year.txt'; });
      const s = await pp.$eval('#tr-files', tree => {
        const rows = [...tree.querySelectorAll('.mat-tree__row')].filter(r => r.getClientRects().length > 0);
        const toggle = tree.querySelector('.mat-tree__toggle').getBoundingClientRect();
        const notes = [...tree.querySelectorAll('.mat-tree__row')].find(r => r.textContent.includes('very long')).getBoundingClientRect();
        return { heights: rows.map(r => Math.round(r.getBoundingClientRect().height)), toggleH: Math.round(toggle.height), toggleW: Math.round(toggle.width), notesH: Math.round(notes.height), overflow: tree.scrollWidth > tree.clientWidth, page: document.documentElement.scrollWidth > innerWidth };
      });
      expectTrue(s.heights.length >= 8 && s.heights.every(h => h >= 44), 'rows of at least 44 px: ' + s.heights.join(', '));
      expectTrue(s.toggleH >= 44 && s.toggleW >= 28, `the chevron is ${s.toggleW} x ${s.toggleH} px`);
      expectTrue(s.notesH > 44, 'the long label wraps over more lines: ' + s.notesH + ' px');
      expect([s.overflow, s.page], [false, false], 'sideways scroll of the tree and of the page');
      // the actions of a row are always there on a touch screen
      const actions = await pp.$eval('#tr-lazy', t => getComputedStyle(t.querySelector('.mat-tree__actions')).opacity);
      expect(actions, '1', 'the actions are shown without hover');
    });
    await check('every tree of the page fits a phone: the examples do not scroll sideways', async () => {
      const w = await pp.evaluate(() => ['tr-files', 'tr-areas', 'tr-lazy', 'tr-nav'].map(id => { const t = document.getElementById(id); return t.scrollWidth <= t.clientWidth; }));
      expect(w, [true, true, true, true], 'no tree is wider than its box');
    });
    await phone.ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- file upload (FORM-21)
  console.log('\nFile upload');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#file-upload');
    const up = id => `.mat-upload:has(#${id})`;
    const names = id => page.$$eval(`${up(id)} .mat-upload__name`, els => els.map(e => e.textContent));
    const inputNames = id => page.$eval('#' + id, el => [...el.files].map(f => f.name));
    const issues = id => page.$$eval(`${up(id)} .mat-upload__issues li`, els => els.map(e => e.textContent.replace(/\u00a0/g, ' ').trim()));
    const browse = id => `${up(id)} .mat-upload__browse`;
    // what a person drags: files of a name, a type and a size (bytes), put in a DataTransfer; the events are the ones of a real drag
    const dragEvent = (id, type, specs) => page.evaluate(({ id, type, specs }) => {
      const dt = new DataTransfer();
      (specs || []).forEach(([name, mime, size]) => dt.items.add(new File([new Uint8Array(size || 10)], name, { type: mime, lastModified: 1700000000000 })));
      const target = document.getElementById(id).closest('.mat-upload');
      const e = new DragEvent(type, { dataTransfer: dt, bubbles: true, cancelable: true });
      target.dispatchEvent(e);
      return e.defaultPrevented;
    }, { id, type, specs });
    const dropFiles = (id, specs) => dragEvent(id, 'drop', specs);
    // events that the page hears on the wrapper and on the field
    const listen = id => page.evaluate(i => {
      const wrap = document.getElementById(i).closest('.mat-upload');
      window.__up = { changes: [], refused: [], fieldEvents: [], fieldFiles: [] };
      wrap.addEventListener('mat:upload-change', e => window.__up.changes.push({ source: e.detail.source, files: e.detail.files.map(f => f.name), added: e.detail.added.map(f => f.name), removed: e.detail.removed.map(f => f.name) }));
      wrap.addEventListener('mat:upload-refused', e => window.__up.refused.push(e.detail.refused.map(r => r.reason + ':' + r.name)));
      document.getElementById(i).addEventListener('change', e => { window.__up.fieldEvents.push('change'); window.__up.fieldFiles.push([...e.target.files].map(f => f.name)); });
      document.getElementById(i).addEventListener('input', () => window.__up.fieldEvents.push('input'));
    }, id);
    const heard = () => page.evaluate(() => window.__up);
    const pdf = (name, size) => ({ name, mimeType: 'application/pdf', buffer: Buffer.alloc(size || 100, 1) });
    const choose = async (selector, files, how) => {
      const [chooser] = await Promise.all([page.waitForEvent('filechooser'), how()]);
      await chooser.setFiles(files);
      return chooser;
    };

    await page.evaluate(() => document.getElementById('file-upload').scrollIntoView());
    await check('an upload is wired: the real input is out of the Tab order and a screen reader, one button is named by the label and by what it says, and the limits are in the zone', async () => {
      expect(await page.locator('.mat-upload.is-ready').count(), 7, 'uploads that were wired');
      const s = await page.evaluate(() => {
        const input = document.getElementById('up-docs'), button = document.querySelector('.mat-upload:has(#up-docs) .mat-upload__browse');
        const name = ['up-docs-label', 'up-docs-title'].map(i => document.getElementById(i).textContent).join(' ');
        return {
          tab: input.tabIndex, hidden: input.getAttribute('aria-hidden'), opacity: getComputedStyle(input).opacity, events: getComputedStyle(input).pointerEvents,
          labelled: button.getAttribute('aria-labelledby'), type: button.type, described: button.getAttribute('aria-describedby'), name,
          limits: document.getElementById('up-docs-limits').textContent.replace(/\u00a0/g, ' '), title: document.getElementById('up-docs-title').textContent,
          stops: [...document.querySelectorAll('.mat-upload:has(#up-docs) button, .mat-upload:has(#up-docs) input')].filter(e => e.tabIndex >= 0).length,
        };
      });
      expect(s.tab, -1, 'tabindex of the real input');
      expect(s.hidden, 'true', 'aria-hidden of the real input');
      expect(s.opacity, '0', 'the real input is invisible');
      expect(s.events, 'none', 'the real input takes no pointer');
      expect(s.labelled, 'up-docs-label up-docs-title', 'the button is named by the label and by its text');
      expect(s.type, 'button', 'type of the button');
      expect(s.described.split(' ').slice(0, 2), ['up-docs-limits', 'up-docs-hint'], 'the button is told the limits and the hint');
      expect(s.name, 'Documents Drop files here or choose them', 'name of the button');
      expect(s.limits, 'PDF, PNG, JPG · up to 5 files · up to 10 MB each', 'limits');
      expect(s.stops, 1, 'stops of the Tab key in the zone');
    });
    await check('the zone opens the dialog with a click, Enter and Space, and what is chosen is listed and held by the real input; the next choice is added to the list', async () => {
      await listen('up-docs');
      await choose(null, [pdf('contract.pdf', 2048), { name: 'scan.png', mimeType: 'image/png', buffer: Buffer.alloc(1536, 1) }], () => page.click(`${up('up-docs')} .mat-upload__zone`, { position: { x: 30, y: 30 } }));
      expect(await names('up-docs'), ['contract.pdf', 'scan.png'], 'the list after a click on the zone (not on the button)');
      expect(await inputNames('up-docs'), ['contract.pdf', 'scan.png'], 'the files of the real input');
      expect(await page.$$eval(`${up('up-docs')} .mat-upload__meta`, els => els.map(e => e.textContent.replace(/\u00a0/g, ' '))), ['2 KB', '1.5 KB'], 'the sizes');
      await page.focus(browse('up-docs'));
      await choose(null, [pdf('invoice.pdf', 300)], () => page.keyboard.press('Enter'));
      expect(await names('up-docs'), ['contract.pdf', 'scan.png', 'invoice.pdf'], 'the list after Enter');
      await choose(null, [pdf('second.pdf', 300)], () => page.keyboard.press('Space'));
      expect(await names('up-docs'), ['contract.pdf', 'scan.png', 'invoice.pdf', 'second.pdf'], 'the list after Space');
      expect(await inputNames('up-docs'), ['contract.pdf', 'scan.png', 'invoice.pdf', 'second.pdf'], 'the files of the real input hold all of them');
      const h = await heard();
      // the page hears one change per choice, and the files of the field are already the whole list when it does
      expect(h.fieldEvents.filter(e => e === 'change').length, 3, 'change events of the field');
      expect(h.fieldFiles.at(-1), ['contract.pdf', 'scan.png', 'invoice.pdf', 'second.pdf'], 'the files that a listener sees');
      expect(h.changes.map(c => c.source), ['picker', 'picker', 'picker'], 'sources');
      expect(h.changes[1].added, ['invoice.pdf'], 'what the second choice added');
    });
    await check('a drag over the control tints the zone and draws a solid line all around it, and the text says what happens; leaving ends it', async () => {
      expect(await dragEvent('up-letter', 'dragenter', [['a.pdf', 'application/pdf']]), true, 'the drag is allowed to end here');
      const over = await page.evaluate(() => {
        const zone = document.querySelector('.mat-upload:has(#up-letter) .mat-upload__zone'), cs = getComputedStyle(zone);
        return { drag: zone.closest('.mat-upload').classList.contains('is-drag'), style: cs.borderTopStyle, sides: [cs.borderTopWidth, cs.borderRightWidth, cs.borderBottomWidth, cs.borderLeftWidth], colours: new Set([cs.borderTopColor, cs.borderRightColor, cs.borderBottomColor, cs.borderLeftColor]).size, bg: cs.backgroundColor, text: document.getElementById('up-letter-title').textContent };
      });
      expect(over.drag, true, 'is-drag');
      expect(over.style, 'solid', 'the line is solid');
      expect(over.sides, ['2px', '2px', '2px', '2px'], 'the line on every side');
      expect(over.colours, 1, 'one colour on every side');
      expect(over.text, 'Drop to add', 'the text of a drag');
      expect(await dragEvent('up-letter', 'dragleave', [['a.pdf', 'application/pdf']]), false, 'a leave');
      const left = await page.evaluate(() => ({ drag: document.querySelector('.mat-upload:has(#up-letter)').classList.contains('is-drag'), text: document.getElementById('up-letter-title').textContent, style: getComputedStyle(document.querySelector('.mat-upload:has(#up-letter) .mat-upload__zone')).borderTopStyle }));
      expect(left, { drag: false, text: 'Drop a file here or choose one', style: 'dashed' }, 'after leaving');
      // a drag that carries no file (text, a link) is left alone
      expect(await page.evaluate(() => { const e = new DragEvent('dragenter', { dataTransfer: new DataTransfer(), bubbles: true, cancelable: true }); document.getElementById('up-letter').closest('.mat-upload').dispatchEvent(e); return [e.defaultPrevented, document.querySelector('.mat-upload:has(#up-letter)').classList.contains('is-drag')]; }), [false, false], 'a drag without files');
    });
    await check('a drop is checked for type, size, number and duplicates: every refusal is named with its reason in words, the others are added, and the page hears of both', async () => {
      await page.reload();
      await page.waitForFunction(() => document.readyState === 'complete');
      await listen('up-docs');
      expect(await dropFiles('up-docs', [['a.pdf', 'application/pdf', 1000], ['virus.exe', 'application/x-msdownload', 1000], ['huge.pdf', 'application/pdf', 12 * 1048576], ['b.jpg', 'image/jpeg', 2000], ['a.pdf', 'application/pdf', 1000]]), true, 'the drop is handled');
      expect(await names('up-docs'), ['a.pdf', 'b.jpg'], 'the files that were added');
      expect(await inputNames('up-docs'), ['a.pdf', 'b.jpg'], 'the files of the real input');
      expect(await issues('up-docs'), [
        'virus.exe: this type of file is not accepted. Allowed: PDF, PNG, JPG.',
        'huge.pdf: the file is too large (12 MB). The limit is 10 MB.',
        'a.pdf: this file has already been added.',
      ], 'the reasons');
      // every file of the example has a name; the refusals sit in a live region and carry an icon, never a colour alone
      expect(await page.$eval(`${up('up-docs')} .mat-upload__status`, el => [el.getAttribute('role'), el.getAttribute('aria-live'), el.querySelectorAll('.mat-icon').length]), ['status', 'polite', 3], 'the live region of the refusals');
      await dropFiles('up-docs', [['c.pdf', 'application/pdf'], ['d.pdf', 'application/pdf'], ['e.pdf', 'application/pdf'], ['f.pdf', 'application/pdf']]);
      expect(await names('up-docs'), ['a.pdf', 'b.jpg', 'c.pdf', 'd.pdf', 'e.pdf'], 'five at most');
      expect(await issues('up-docs'), ['f.pdf: not added. At most 5 files are allowed.'], 'the number');
      const h = await heard();
      expect(h.refused, [['type:virus.exe', 'size:huge.pdf', 'duplicate:a.pdf'], ['count:f.pdf']], 'mat:upload-refused');
      expect(h.changes.map(c => [c.source, c.added]), [['drop', ['a.pdf', 'b.jpg']], ['drop', ['c.pdf', 'd.pdf', 'e.pdf']]], 'mat:upload-change');
      // a drop tells the form like a choice in the dialog does
      expect(h.fieldEvents, ['input', 'change', 'input', 'change'], 'the events of the field');
      expect(h.fieldFiles.at(-1), ['a.pdf', 'b.jpg', 'c.pdf', 'd.pdf', 'e.pdf'], 'what the form sees');
      // the next thing that is added or taken away clears the old message
      await page.click(`${up('up-docs')} .mat-upload__item >> nth=0 >> .mat-upload__remove`);
      expect(await issues('up-docs'), [], 'the message is gone after a change');
    });
    await check('a file is removed with its button, which is named by the file: the list and the real input follow, the focus goes to the next button, and the last one hands it back to the zone', async () => {
      expect(await names('up-docs'), ['b.jpg', 'c.pdf', 'd.pdf', 'e.pdf'], 'the list after the first removal');
      expect(await inputNames('up-docs'), ['b.jpg', 'c.pdf', 'd.pdf', 'e.pdf'], 'the real input after the first removal');
      expect(await page.getAttribute(`${up('up-docs')} .mat-upload__remove >> nth=1`, 'aria-label'), 'Remove c.pdf', 'name of the button');
      expect(await page.getAttribute(`${up('up-docs')} .mat-upload__remove >> nth=1`, 'title'), 'Remove c.pdf', 'tooltip of the button');
      await page.focus(`${up('up-docs')} .mat-upload__item >> nth=1 >> .mat-upload__remove`);
      await page.keyboard.press('Enter');
      expect(await names('up-docs'), ['b.jpg', 'd.pdf', 'e.pdf'], 'the list after a removal with the keyboard');
      expect(await page.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Remove d.pdf', 'the focus is on the button of the file that moved up');
      await page.keyboard.press('Space');
      await page.keyboard.press('Space');
      expect(await page.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Remove b.jpg', 'the focus goes to the one before when the last is gone');
      await page.keyboard.press('Enter');
      expect(await names('up-docs'), [], 'an empty list');
      expect(await page.evaluate(() => document.activeElement.classList.contains('mat-upload__browse')), true, 'the focus is back on the zone');
      expect(await inputNames('up-docs'), [], 'the real input is empty');
      expect(await page.$eval(`${up('up-docs')} .mat-upload__list`, el => el.hidden), true, 'the empty list is not drawn');
      const h = await heard();
      expect(h.changes.slice(-1)[0], { source: 'remove', files: [], added: [], removed: ['b.jpg'] }, 'the last event');
      expect(await page.$eval(`${up('up-docs')} .mat-sr-only`, el => el.textContent), 'b.jpg removed', 'what a screen reader is told');
    });
    await check('without multiple a new file replaces the old one and the rest of a drop is refused; the list follows the real input', async () => {
      await dropFiles('up-letter', [['one.pdf', 'application/pdf', 500], ['two.pdf', 'application/pdf', 500]]);
      expect(await names('up-letter'), ['one.pdf'], 'one file');
      expect(await issues('up-letter'), ['two.pdf: not added. Only one file is allowed.'], 'the second is refused');
      await dropFiles('up-letter', [['three.docx', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 500]]);
      expect(await names('up-letter'), ['three.docx'], 'replaced');
      expect(await inputNames('up-letter'), ['three.docx'], 'the real input');
      await dropFiles('up-letter', [['big.pdf', 'application/pdf', 3 * 1048576]]);
      expect(await names('up-letter'), ['three.docx'], 'a refused file does not replace the one that is there');
      expect(await issues('up-letter'), ['big.pdf: the file is too large (3 MB). The limit is 2 MB.'], 'size');
    });
    await check('a required field says so at the field when the form is sent without a file, the focus lands on the zone, and the message goes when a file is chosen; a reset empties the list', async () => {
      await page.click(`${up('up-letter')} .mat-upload__remove`);
      expect(await inputNames('up-letter'), [], 'no file');
      await page.click('form[action="/Components#file-upload"] button[type=submit]');
      await page.waitForTimeout(300);
      const s = await page.evaluate(() => ({
        error: document.querySelector('.mat-field:has(#up-letter) .mat-error').textContent, invalid: document.getElementById('up-letter').getAttribute('aria-invalid'),
        field: document.querySelector('.mat-field:has(#up-letter)').classList.contains('is-invalid'), focus: document.activeElement.classList.contains('mat-upload__browse') && document.activeElement.closest('.mat-upload').contains(document.getElementById('up-letter')),
        line: getComputedStyle(document.querySelector('.mat-upload:has(#up-letter) .mat-upload__zone')).borderTopColor, plain: getComputedStyle(document.querySelector('.mat-upload:has(#up-docs) .mat-upload__zone')).borderTopColor,
      }));
      expect(s.error, 'Please choose your application letter.', 'the message of the form');
      expect(s.invalid, 'true', 'aria-invalid');
      expect(s.field, true, 'the field is marked');
      expect(s.focus, true, 'the focus is on the zone');
      expectTrue(s.line !== s.plain, 'the zone has the colour of an error: ' + s.line);
      await dropFiles('up-letter', [['letter.pdf', 'application/pdf', 100]]);
      expect(await page.$eval('.mat-field:has(#up-letter) .mat-error', el => el.textContent), '', 'the message is gone with a file');
      await page.evaluate(() => document.querySelector('form[action="/Components#file-upload"]').reset());
      await page.waitForTimeout(50);
      expect(await names('up-letter'), [], 'a reset empties the list');
      expect(await inputNames('up-letter'), [], 'and the real input');
    });
    await check('a form that is sent holds exactly the files of the list: removing a file takes it out of what the form posts', async () => {
      const posted = await page.evaluate(async () => {
        await window.MatUi.upload.add('up-docs', []);
        const form = document.createElement('form');
        document.getElementById('up-docs').closest('.mat-field').appendChild(form);
        window.MatUi.upload.clear('up-docs');
        const dt = new DataTransfer();
        ['x.pdf', 'y.pdf', 'z.pdf'].forEach(n => dt.items.add(new File([new Uint8Array(5)], n, { type: 'application/pdf', lastModified: 1700000000000 })));
        window.MatUi.upload.add('up-docs', dt.files);
        window.MatUi.upload.remove('up-docs', 'y.pdf');
        const body = new FormData();
        [...document.getElementById('up-docs').files].forEach(f => body.append('docs', f));
        form.remove();
        return body.getAll('docs').map(f => f.name);
      });
      expect(posted, ['x.pdf', 'z.pdf'], 'the files in the body');
      await page.evaluate(() => window.MatUi.upload.clear('up-docs'));
    });
    await check('an image has a thumbnail that is a real picture, an object URL that is released with the file, and a file of another kind has an icon; data-preview="none" turns it off', async () => {
      await page.evaluate(async () => {
        const c = document.createElement('canvas'); c.width = 20; c.height = 10; c.getContext('2d').fillRect(0, 0, 20, 10);
        const blob = await new Promise(r => c.toBlob(r));
        const dt = new DataTransfer();
        dt.items.add(new File([blob], 'pic.png', { type: 'image/png', lastModified: 1700000000000 }));
        dt.items.add(new File([new Uint8Array(5)], 'doc.pdf', { type: 'application/pdf', lastModified: 1700000000000 }));
        window.MatUi.upload.add('up-docs', dt.files);
      });
      const s = await page.evaluate(async () => {
        const img = document.querySelector('.mat-upload:has(#up-docs) .mat-upload__thumb img');
        await img.decode();
        const items = [...document.querySelectorAll('.mat-upload:has(#up-docs) .mat-upload__thumb')];
        return { src: img.src.slice(0, 5), width: img.naturalWidth, alt: img.getAttribute('alt'), url: img.src, icons: items.map(t => t.querySelectorAll('img').length + '/' + t.querySelectorAll('svg').length) };
      });
      expect([s.src, s.width, s.alt, s.icons], ['blob:', 20, '', ['1/0', '0/1']], 'thumbnails');
      expect(await page.evaluate(u => fetch(u).then(r => r.ok ? 'alive' : 'gone'), s.url), 'alive', 'the object URL works while the file is in the list');
      await page.evaluate(() => { window.__revoked = []; const revoke = URL.revokeObjectURL; URL.revokeObjectURL = u => { window.__revoked.push(u); revoke.call(URL, u); }; });
      await page.click(`${up('up-docs')} .mat-upload__item >> nth=0 >> .mat-upload__remove`);
      expect(await page.evaluate(() => window.__revoked), [s.url], 'the object URL is released with the file');
      await page.evaluate(() => { document.getElementById('up-docs').closest('.mat-upload').setAttribute('data-preview', 'none'); window.MatUi.upload.clear('up-docs'); });
      await page.evaluate(async () => {
        const dt = new DataTransfer();
        dt.items.add(new File([new Uint8Array(5)], 'again.png', { type: 'image/png', lastModified: 1700000000001 }));
        window.MatUi.upload.add('up-docs', dt.files);
      });
      expect(await page.locator(`${up('up-docs')} .mat-upload__thumb img`).count(), 0, 'no thumbnail with data-preview="none"');
      await page.evaluate(() => { document.getElementById('up-docs').closest('.mat-upload').removeAttribute('data-preview'); window.MatUi.upload.clear('up-docs'); });
    });
    await check('paste adds the images of the clipboard only where data-paste is written, with a name that has the date; text and other files are left alone or refused in words', async () => {
      await listen('up-photos');
      await page.focus(browse('up-photos'));
      const paste = (id, files) => page.evaluate(({ id, files }) => {
        const dt = new DataTransfer();
        files.forEach(([name, mime]) => dt.items.add(new File([new Uint8Array(30)], name, { type: mime })));
        const e = new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true });
        document.querySelector('.mat-upload:has(#' + id + ') .mat-upload__browse').dispatchEvent(e);
        return e.defaultPrevented;
      }, { id, files });
      expect(await paste('up-photos', [['image.png', 'image/png']]), true, 'a pasted image is taken');
      const added = await names('up-photos');
      expect(added.length, 1, 'one image');
      expectTrue(/^image-\d{8}-\d{6}\.png$/.test(added[0]), 'the name of a pasted image: ' + added[0]);
      expect(await paste('up-photos', []), false, 'text on the clipboard is left to the browser');
      expect(await paste('up-photos', [['notes.pdf', 'application/pdf']]), true, 'a file that is not an image is handled');
      expect(await issues('up-photos'), ['Only images can be pasted here.'], 'and refused in words');
      expect(await names('up-photos'), added, 'nothing was added');
      expect(await paste('up-docs', [['image.png', 'image/png']]), false, 'an upload without data-paste ignores a paste');
      expect(await names('up-docs'), [], 'and takes nothing');
      expect((await heard()).changes.map(c => c.source), ['paste'], 'source');
      await page.evaluate(() => window.MatUi.upload.clear('up-photos'));
    });
    await check('an app that uploads by itself draws progress as a progressbar with a name, done and a failure in words, and a new try clears them', async () => {
      await dropFiles('up-self', [['one.bin', 'application/octet-stream', 2000], ['two.bin', 'application/octet-stream', 3000], ['three.bin', 'application/octet-stream', 4000]]);
      expect(await page.evaluate(() => [MatUi.upload.setProgress('up-self', 0, 42), MatUi.upload.setProgress('up-self', 'two.bin', 100), MatUi.upload.setError('up-self', 2, 'The server did not take this file.'), MatUi.upload.setProgress('up-self', 9, 5)]), [true, true, true, false], 'the answers of the API');
      const s = await page.evaluate(() => [...document.querySelectorAll('.mat-upload:has(#up-self) .mat-upload__item')].map(li => ({
        cls: li.className.replace('mat-upload__item', '').trim(), meta: li.querySelector('.mat-upload__meta').textContent.replace(/\u00a0/g, ' '),
        bar: li.querySelector('.mat-upload__bar').hidden ? null : [li.querySelector('.mat-upload__bar').getAttribute('role'), li.querySelector('.mat-upload__bar').getAttribute('aria-valuenow'), li.querySelector('.mat-upload__bar').getAttribute('aria-label'), getComputedStyle(li.querySelector('.mat-upload__fill')).width, li.querySelector('.mat-upload__bar').getBoundingClientRect().width],
        problem: li.querySelector('.mat-upload__problem').hidden ? null : li.querySelector('.mat-upload__problem').textContent.trim(),
        icon: li.querySelectorAll('.mat-upload__problem svg, .mat-upload__state svg').length,
      })));
      expect(s[0].cls, 'is-uploading', 'state of the first');
      expect(s[0].meta, '2 KB · 42 %', 'text of the first');
      expect(s[0].bar.slice(0, 3), ['progressbar', '42', 'one.bin'], 'the progressbar');
      expectTrue(Math.abs(parseFloat(s[0].bar[3]) / s[0].bar[4] - 0.42) < 0.03, 'the fill is 42 % of the bar: ' + s[0].bar[3] + ' of ' + s[0].bar[4]);
      expect([s[1].cls, s[1].meta, s[1].bar, s[1].icon], ['is-done', '2.9 KB · Uploaded', null, 1], 'done, in words with an icon');
      expect([s[2].cls, s[2].problem, s[2].bar, s[2].icon], ['is-error', 'The server did not take this file.', null, 1], 'the failure, in words with an icon');
      expect(await page.$eval(`${up('up-self')} .mat-upload__remove >> nth=2`, el => el.getAttribute('aria-describedby') === el.closest('.mat-upload__item').querySelector('.mat-upload__problem').id), true, 'the remove button of a failed file is told why');
      await page.evaluate(() => { MatUi.upload.setError('up-self', 2); });
      expect(await page.$eval(`${up('up-self')} .mat-upload__problem >> nth=2`, el => el.textContent.trim()), 'Upload failed', 'the default message');
      await page.evaluate(() => { MatUi.upload.setProgress('up-self', 2); MatUi.upload.setProgress('up-self', 1); });
      expect(await page.$$eval(`${up('up-self')} .mat-upload__item`, lis => lis.map(li => li.className.replace('mat-upload__item', '').trim())), ['is-uploading', '', ''], 'a new try takes progress and error away');
      await page.evaluate(() => MatUi.upload.clear('up-self'));
    });
    await check('the Tab key passes the zone once (never the real input), goes on through the remove buttons and back; a disabled input disables the button and the remove buttons', async () => {
      await dropFiles('up-docs', [['k.pdf', 'application/pdf'], ['l.pdf', 'application/pdf']]);
      const at = () => page.evaluate(() => { const a = document.activeElement; return [a.classList.contains('mat-upload__remove') ? 'remove' : a.classList.contains('mat-upload__browse') ? 'browse' : 'other', a.getAttribute('aria-label'), document.querySelector('.mat-upload:has(#up-docs)').contains(a)]; });
      await page.focus(browse('up-docs'));
      await page.keyboard.press('Tab');
      expect(await at(), ['remove', 'Remove k.pdf', true], 'after one Tab');
      await page.keyboard.press('Tab');
      expect((await at())[1], 'Remove l.pdf', 'after two');
      await page.keyboard.press('Shift+Tab');
      await page.keyboard.press('Shift+Tab');
      expect((await at()).slice(0, 1), ['browse'], 'back on the zone');
      await page.keyboard.press('Shift+Tab');
      expect((await at())[2], false, 'one more Shift+Tab leaves the control: the real input is not a stop');
      await page.evaluate(() => { document.getElementById('up-docs').disabled = true; });
      await page.waitForTimeout(50);
      expect(await page.$eval(browse('up-docs'), el => el.disabled), true, 'the button follows the input');
      expect(await page.$$eval(`${up('up-docs')} .mat-upload__remove`, els => els.map(e => e.disabled)), [true, true], 'the remove buttons');
      expect(await page.$eval(up('up-docs'), el => el.classList.contains('is-disabled')), true, 'is-disabled');
      await page.evaluate(() => { document.getElementById('up-docs').disabled = false; });
      await page.waitForTimeout(50);
      expect(await page.$eval(browse('up-docs'), el => el.disabled), false, 'and enabled again');
      await page.evaluate(() => MatUi.upload.clear('up-docs'));
    });
    await check('a disabled upload has a disabled button and takes no drop; a drop elsewhere on the page is left alone', async () => {
      expect(await page.$eval(browse('up-off'), el => el.disabled), true, 'the button');
      expect(await dropFiles('up-off', [['x.pdf', 'application/pdf']]), true, 'the drop is cancelled so that the browser does not open the file');
      expect(await names('up-off'), [], 'and takes nothing');
      expect(await dragEvent('up-off', 'dragenter', [['x.pdf', 'application/pdf']]), true, 'a drag');
      expect(await page.$eval(up('up-off'), el => el.classList.contains('is-drag')), false, 'is not tinted');
      expect(await page.evaluate(() => { const e = new DragEvent('drop', { dataTransfer: (() => { const d = new DataTransfer(); d.items.add(new File(['x'], 'x.txt')); return d; })(), bubbles: true, cancelable: true }); document.querySelector('h1').dispatchEvent(e); return e.defaultPrevented; }), false, 'a drop on the page');
    });
    await check('the texts are German on a German page, the sizes follow the language, and a touch or a drag says it in German too', async () => {
      await page.evaluate(() => {
        document.documentElement.lang = 'de';
        const host = document.createElement('div');
        host.id = 'up-de-host';
        host.innerHTML = '<div class="mat-field"><label class="mat-label" for="up-de">Dateien</label><div class="mat-upload" data-upload data-max-files="3" data-max-size="1.5MB"><input class="mat-upload__input" type="file" id="up-de" name="de" multiple accept=".pdf" /></div></div>';
        document.getElementById('file-upload').appendChild(host);
        MatUi.refresh(host);
      });
      expect(await page.textContent('#up-de-title'), 'Dateien hierher ziehen oder auswählen', 'the zone');
      expect((await page.textContent('#up-de-limits')).replace(/\u00a0/g, ' '), 'PDF · bis zu 3 Dateien · je bis zu 1,5 MB', 'the limits, with a decimal comma');
      await dropFiles('up-de', [['bild.png', 'image/png'], ['gross.pdf', 'application/pdf', 2 * 1048576], ['gut.pdf', 'application/pdf', 100]]);
      expect(await issues('up-de'), ['bild.png: Dieser Dateityp wird nicht angenommen. Erlaubt: PDF.', 'gross.pdf: Die Datei ist zu groß (2 MB). Das Limit ist 1,5 MB.'], 'the refusals');
      expect(await page.getAttribute('#up-de-title >> xpath=ancestor::div[contains(@class,"mat-upload")]//button[contains(@class,"mat-upload__remove")]', 'aria-label'), 'gut.pdf entfernen', 'the remove button');
      expect(await page.getAttribute(`${up('up-de')} .mat-upload__list`, 'aria-label'), 'Ausgewählte Dateien', 'the list');
      await dragEvent('up-de', 'dragenter', [['a.pdf', 'application/pdf']]);
      expect(await page.textContent('#up-de-title'), 'Zum Hinzufügen loslassen', 'while dragging');
      await dragEvent('up-de', 'dragleave', []);
      await page.evaluate(() => { document.documentElement.setAttribute('data-up-refusedtype', 'Nein: {0}'); });
      await dropFiles('up-de', [['x.png', 'image/png']]);
      expect(await issues('up-de'), ['Nein: x.png'], 'a text that the app changed with an attribute of html');
      await page.evaluate(() => { document.documentElement.lang = 'en'; document.documentElement.removeAttribute('data-up-refusedtype'); document.getElementById('up-de-host').remove(); });
    });
    await check('a file name is shown as text, never as markup, and a dollar sign in it survives', async () => {
      await dropFiles('up-self', [['<img src=x onerror=alert(1)>$&.bin', 'application/octet-stream', 10]]);
      const s = await page.evaluate(() => ({ name: document.querySelector('.mat-upload:has(#up-self) .mat-upload__name').textContent, images: document.querySelectorAll('.mat-upload:has(#up-self) .mat-upload__name img').length, label: document.querySelector('.mat-upload:has(#up-self) .mat-upload__remove').getAttribute('aria-label') }));
      expect(s, { name: '<img src=x onerror=alert(1)>$&.bin', images: 0, label: 'Remove <img src=x onerror=alert(1)>$&.bin' }, 'the name');
      await page.evaluate(() => MatUi.upload.clear('up-self'));
    });
    await check('light and dark: the text of the zone, the limits and the messages are readable on what they sit on', async () => {
      await dropFiles('up-docs', [['x.exe', 'application/x-msdownload']]);
      await page.evaluate(() => { MatUi.upload.add('up-self', [new File([new Uint8Array(4)], 'bad.bin', { type: 'application/octet-stream' })]); MatUi.upload.setError('up-self', 0, 'The server did not take this file.'); });
      const measure = () => page.evaluate(() => {
        // the background that is seen: the translucent tints of the ancestors laid over the first opaque one
        const bg = el => {
          const layers = [];
          for (let e = el; e; e = e.parentElement) {
            const n = (getComputedStyle(e).backgroundColor.match(/-?\d*\.?\d+/g) || []).map(Number);
            if (n.length < 3 || n[3] === 0) { continue; }
            layers.push([n[0], n[1], n[2], n.length > 3 ? n[3] : 1]);
            if (layers[layers.length - 1][3] === 1) { break; }
          }
          let out = [255, 255, 255];
          layers.reverse().forEach(([r, g, b, a]) => { out = [r * a + out[0] * (1 - a), g * a + out[1] * (1 - a), b * a + out[2] * (1 - a)]; });
          return 'rgb(' + out.map(Math.round).join(', ') + ')';
        };
        const pick = sel => { const el = document.querySelector(sel); return { colour: getComputedStyle(el).color, bg: bg(el) }; };
        return { title: pick('.mat-upload:has(#up-docs) .mat-upload__title'), limits: pick('.mat-upload:has(#up-docs) .mat-upload__limits'), issue: pick('.mat-upload:has(#up-docs) .mat-upload__issues li'), problem: pick('.mat-upload:has(#up-self) .mat-upload__problem'), name: pick('.mat-upload:has(#up-self) .mat-upload__name') };
      });
      const modes = {};
      for (const mode of ['light', 'dark']) {
        await page.evaluate(m => MatUi.theme.set(m), mode);
        await page.waitForTimeout(300);
        modes[mode] = await measure();
      }
      for (const [mode, m] of Object.entries(modes)) {
        for (const [what, c] of Object.entries(m)) { expectTrue(contrastOf(c.colour, c.bg) >= 4.5, `${what} in ${mode}: ${c.colour} on ${c.bg} has a contrast of ${contrastOf(c.colour, c.bg).toFixed(2)}`); }
      }
      expectTrue(modes.light.title.bg !== modes.dark.title.bg, 'the zone changes with the theme');
      await page.evaluate(() => { MatUi.theme.set('system'); MatUi.upload.clear('up-docs'); MatUi.upload.clear('up-self'); });
    });
    await ctx.close();
  }
  {
    const { ctx, page } = await open(phoneOptions, '/Components#file-upload');
    await check('on a phone the zone says Tap to choose, the targets are as large as a control, a long name wraps and nothing is wider than the screen', async () => {
      await page.evaluate(() => document.getElementById('up-docs').scrollIntoView({ block: 'center' }));
      expect(await page.textContent('#up-docs-title'), 'Tap to choose files', 'the text on a touch screen');
      expect(await page.textContent('#up-letter-title'), 'Tap to choose a file', 'one file');
      await page.evaluate(() => {
        const dt = new DataTransfer();
        dt.items.add(new File([new Uint8Array(20)], 'a-very-long-name-for-a-file-that-keeps-going-on-and-on-and-on-and-on-until-it-is-longer-than-a-phone.pdf', { type: 'application/pdf', lastModified: 1 }));
        dt.items.add(new File([new Uint8Array(20)], 'virus-with-a-long-name-that-the-zone-does-not-accept-because-it-is-not-a-pdf-nor-an-image.exe', { type: 'application/x-msdownload', lastModified: 1 }));
        document.getElementById('up-docs').closest('.mat-upload').dispatchEvent(new DragEvent('drop', { dataTransfer: dt, bubbles: true, cancelable: true }));
      });
      const s = await page.evaluate(() => {
        const box = sel => { const r = document.querySelector(sel).getBoundingClientRect(); return [Math.round(r.width), Math.round(r.height)]; };
        const wrap = document.querySelector('.mat-upload:has(#up-docs)').getBoundingClientRect(), item = document.querySelector('.mat-upload:has(#up-docs) .mat-upload__item').getBoundingClientRect();
        return {
          coarse: matchMedia('(pointer: coarse)').matches, overflow: document.documentElement.scrollWidth > innerWidth, remove: box('.mat-upload:has(#up-docs) .mat-upload__remove'), browse: box('.mat-upload:has(#up-docs) .mat-upload__browse'),
          inside: item.right <= wrap.right + 1 && item.left >= wrap.left - 1, issue: document.querySelector('.mat-upload:has(#up-docs) .mat-upload__issues li').getBoundingClientRect().right <= wrap.right + 1,
        };
      });
      expect(s.coarse, true, 'a touch screen');
      expect(s.overflow, false, 'sideways scroll');
      expectTrue(s.remove[0] >= 44 && s.remove[1] >= 44, 'the remove button is ' + s.remove.join('x'));
      expectTrue(s.browse[1] >= 44, 'the button of the zone is ' + s.browse.join('x'));
      expect([s.inside, s.issue], [true, true], 'the row and the message stay inside the control');
    });
    await check('a tap on the zone opens the dialog, and the compact zone puts its limits on a second line', async () => {
      await page.evaluate(() => document.getElementById('up-avatar').scrollIntoView({ block: 'center' }));
      const [chooser] = await Promise.all([page.waitForEvent('filechooser'), page.tap('.mat-upload:has(#up-avatar) .mat-upload__zone')]);
      await chooser.setFiles([{ name: 'me.png', mimeType: 'image/png', buffer: Buffer.alloc(100, 1) }]);
      expect(await page.$$eval('.mat-upload:has(#up-avatar) .mat-upload__name', els => els.map(e => e.textContent)), ['me.png'], 'the file that was chosen');
      const rows = await page.evaluate(() => {
        const zone = document.querySelector('.mat-upload:has(#up-avatar) .mat-upload__zone');
        return { title: document.getElementById('up-avatar-title').getBoundingClientRect().top, limits: document.getElementById('up-avatar-limits').getBoundingClientRect().top, overflow: zone.scrollWidth > zone.clientWidth };
      });
      expectTrue(rows.limits > rows.title, 'the limits are below the text on a phone');
      expect(rows.overflow, false, 'the compact zone does not overflow');
    });
    await ctx.close();
  }

  // ---------------------------------------------------------------------------------------------------- the company and the template (stage 14, demo path; blank.js is the blank one)
  // It needs the administrator like the Studio block above. The tagline is changed and put back, and the template is asked about and not switched, so the site is what it was.
  console.log('\nCompany');
  {
    const name = process.env.STUDIO_NAME || 'admin';
    const password = process.env.STUDIO_PASSWORD || 'studio-dev';
    const { ctx, page } = await open(desktopOptions, '/Studio/Login');
    if (await page.locator('input[type="password"]').count() === 0) {
      console.log('  --    the Studio is off on this site (no data directory): the company checks are skipped');
      await ctx.close();
    } else {
      await page.fill('#Name', name);
      await page.fill('#Password', password);
      await page.click('.mat-auth__card button[type="submit"]');
      await page.waitForURL('**/Studio**');
      let original = '';
      await check('the company page has the shell, one h1, the names of the tenant, its menu item and the two templates', async () => {
        await page.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
        expect(await page.locator('main h1').count(), 1, 'h1');
        expect(await page.textContent('main h1'), 'Company', 'the title');
        expect(await page.locator('a.mat-nav__item.is-active[href="/Studio/Company"]').count(), 1, 'the menu item');
        expectTrue((await page.inputValue('#Input_Company')).length > 1, 'no company');
        expectTrue((await page.inputValue('#Input_SiteName')).length > 1, 'no site name');
        original = await page.inputValue('#Input_Tagline');
        expect(await page.locator('[data-template]').count(), 2, 'template cards');
        expect(await page.locator('[data-template] .mat-pill', { hasText: 'In use' }).count(), 1, 'the template in use');
      });
      await check('a change of the tagline is saved, shows in the menu of every page and in the history; saving the old one puts it back', async () => {
        await page.fill('#Input_Tagline', 'A tagline from the browser checks');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForLoadState('networkidle');
        await page.goto(BASE + '/', { waitUntil: 'networkidle' });
        expect(await page.textContent('.mat-nav__tagline'), 'A tagline from the browser checks', 'the tagline in the menu');
        await page.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
        expectTrue(await page.locator('a.mat-btn', { hasText: 'History' }).count() > 0, 'no History button once a revision exists');
        await page.fill('#Input_Tagline', original);
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForLoadState('networkidle');
        await page.goto(BASE + '/', { waitUntil: 'networkidle' });
        expect(await page.textContent('.mat-nav__tagline'), original, 'the tagline after it was put back');
      });
      await check('a name that is too short is refused at its field and saved nowhere', async () => {
        await page.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
        const before = await page.inputValue('#Input_Company');
        await page.fill('#Input_Company', 'A');
        await page.click('.mat-btn-row button.mat-btn--primary');
        await page.waitForLoadState('networkidle');
        expectTrue((await page.locator('.mat-field.is-invalid').count()) > 0, 'the company is not marked');
        await page.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
        expect(await page.inputValue('#Input_Company'), before, 'the company was saved');
      });
      await check('switching the template asks first and says what is hidden and that nothing is deleted; cancelling changes nothing', async () => {
        await page.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
        await page.click('[data-template-switch]');
        await page.waitForSelector('dialog[open]');
        const text = await page.textContent('dialog[open]');
        expectTrue(text.includes('Nothing is deleted'), 'the question does not say that nothing is deleted: ' + text);
        await page.keyboard.press('Escape');
        await page.waitForFunction(() => !document.querySelector('dialog[open]'));
        expect(await page.locator('[data-template] .mat-pill', { hasText: 'In use' }).count(), 1, 'the template in use after Cancel');
        const apps = await (await page.request.get(BASE + '/api/apps')).json();
        expectTrue(apps.length > 5, 'the apps after Cancel: ' + apps.length);
      });
      await ctx.close();
    }
  }

  // ---------------------------------------------------------------------------------------------------- the end
  console.log('');
  await check('no script error and no error in the console on any page above', async () => {
    expect(consoleErrors, [], 'console errors');
  });

  await browser.close();
  const failed = results.filter(r => !r.ok);
  console.log(`\n${results.length - failed.length} of ${results.length} checks passed.`);
  process.exit(failed.length === 0 ? 0 : 1);
})().catch(e => { console.error('FAILED to run:', e.message); process.exit(2); });
