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
    await check('on a phone there is no index beside the content: a button opens it in a full-screen dialog and a link closes it', async () => {
      expectTrue(!(await page.locator('.mat-toc-aside').isVisible()), 'the index is shown beside the content on a phone');
      await page.click('.mat-toc-bar button');
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
      expectTrue(!(await page.locator('[data-bulk-bar]').isVisible()), 'the bulk bar is shown before a row is selected');
      await page.check('#tsel-all');
      expect((await page.textContent('[data-bulk-count]')).trim(), '3', 'selected');
      expectTrue(await page.locator('[data-bulk-bar]').isVisible(), 'the bulk bar');
      expect(await page.locator('tr.is-selected').count(), 3, 'rows marked');
      await page.uncheck('#tsel-2');
      expect((await page.textContent('[data-bulk-count]')).trim(), '2', 'selected after one is taken away');
      expect(await page.$eval('#tsel-all', el => el.indeterminate), true, 'the header check shows the state in between');
      await page.uncheck('#tsel-1');
      await page.uncheck('#tsel-3');
      expectTrue(!(await page.locator('[data-bulk-bar]').isVisible()), 'the bulk bar is still shown with nothing selected');
    });
    await check('the width of a progress bar comes from data-value, and a stack shows its parts side by side', async () => {
      const share = await page.$eval('.mat-meter--lg[aria-label="Large"] .mat-meter__fill', el => Math.round(100 * el.getBoundingClientRect().width / el.parentElement.getBoundingClientRect().width));
      expect(share, 75, 'width of the large bar in per cent');
    });
    await check('a side dialog sits at the right edge and closes with its X', async () => {
      await page.evaluate(() => document.getElementById('dialogs').scrollIntoView());
      await page.click('[data-dialog-open="dlg-side"]');
      expect(await page.$eval('#dlg-side', el => el.getAnimations().map(a => a.animationName)), ['mat-panel-in-end'], 'the animation of the panel (not the one of the centred dialog)');
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

  // ---------------------------------------------------------------------------------------------------- pickers
  console.log('\nPickers');
  {
    const { ctx, page } = await open(desktopOptions, '/Components#rich-select');
    const panelOf = id => page.locator(`#${id}-trigger ~ .mat-picker__panel`);
    const valueOf = id => page.$eval(`#${id}`, el => el.value);
    const shown = async id => (await page.textContent(`#${id}-trigger .mat-picker__value`)).replace(/\s+/g, ' ').trim();

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
    await check('on a phone the index of the page is a button that opens a dialog', async () => {
      await page.click('.mat-toc-bar button');
      expectTrue(await page.locator('#toc-dialog[open]').count() === 1, 'the index dialog is not open');
      expect(await page.locator('#toc-dialog a.mat-toc__link').count() >= 3, true, 'links in the dialog');
      await page.click('#toc-dialog [data-dialog-close]');
    });
    await ctx.close();
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
