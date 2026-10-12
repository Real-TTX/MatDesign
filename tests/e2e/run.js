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
        // the site has the switcher at both places: the one under the logo
        await page.click('.mat-nav__brand ~ .mat-nav__switch .mat-switcher');
        expect(await page.locator('#nav-switch-menu [data-nav-set]').evaluateAll(els => els.map(e => e.getAttribute('data-nav-set'))), ['design', 'toolkit', 'studio'], 'menus in the switcher');
        await page.keyboard.press('Escape');
        expect(await page.textContent('.mat-user__name'), 'Admin', 'the user');
        const names = await page.locator('.mat-nav__scroll:not([hidden]) .mat-nav__item').evaluateAll(els => els.map(e => { const c = e.cloneNode(true); c.querySelectorAll('.mat-nav__badge').forEach(b => b.remove()); return c.textContent.trim(); }));
        expect(names, ['Overview', 'Apps', 'Rules', 'Components', 'Activity', 'Checks', 'Releases', 'Versions'], 'items of the Studio menu');
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
