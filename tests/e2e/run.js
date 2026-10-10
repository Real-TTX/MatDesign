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
    const { ctx, page } = await open(desktopOptions, '/Components/Detail?id=dialogs');
    await check('MatUi.confirm, alert and prompt are dialogs and return their results; a toast follows', async () => {
      await page.click('[data-doc-demo="dialogs"]');
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
      await page.click('[data-confirm]');
      await page.waitForSelector('dialog[open]');
      await page.click('dialog[open] [data-dialog-close]');
      await page.waitForTimeout(250);
      expect(await page.locator('dialog[open]').count(), 0, 'open dialogs');
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
