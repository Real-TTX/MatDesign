// End-to-end checks of a BLANK start (stage 14 of docs/PRODUCT.md) in a real browser: a second site, started on its own port with an empty data directory and
//   MATDESIGN_TEMPLATE=blank MATDESIGN_COMPANY=Acme
// The assistant is walked through its three steps by the real form, the shell says Acme and none of the Mat apps is anywhere, an app is added in the Studio and seen on the
// site, and the Company page switches to the demo and back. The run.js next to it is the demo path against the usual site; this one is the smaller blank path.
//   MATDESIGN_DATA=<an empty directory> MATDESIGN_TEMPLATE=blank MATDESIGN_COMPANY=Acme dotnet run --project src/MatDesign.Web --no-build --urls http://localhost:8189
//   cd tests/e2e && node blank.js http://localhost:8189
// STUDIO_NAME / STUDIO_PASSWORD (default admin / studio-dev) are the administrator it creates. SHOTS=<directory> also writes screenshots of what it sees.
const { chromium } = require('playwright-core');
const { annotate } = require('./annotate');

const BASE = (process.argv[2] || process.env.BASE || 'http://localhost:8189').replace(/\/$/, '');
const SHOTS = process.env.SHOTS || '';
const NAME = process.env.STUDIO_NAME || 'admin';
const PASSWORD = process.env.STUDIO_PASSWORD || 'studio-dev';
const COMPANY = process.env.BLANK_COMPANY || 'Acme';
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
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error(`${what}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}

function expectTrue(value, what) {
  if (!value) { throw new Error(what); }
}

// What the site must not say for a company that is not Mat: the name of the demo and the apps of its family, in the text of a page (not in the code of an example).
const MAT_WORDS = ['MatDesign', 'Mat apps', 'Mat family', 'Mat app', 'MatMail', 'Mattiverse', 'MatPaper', 'MatDo', 'Matdo', 'Matcad', 'MatDock', 'MatOS', 'MatSplit', 'Matmon', 'Matthix', 'MatCMS', 'MatFile', 'MatSMTP', 'MatIdentity'];

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true }).catch(() => chromium.launch({ channel: 'chrome', headless: true }));
  const desktop = { viewport: { width: 1360, height: 900 } };
  const phone = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true };
  const consoleErrors = [];

  async function open(options, url) {
    const ctx = await browser.newContext(options);
    const page = await ctx.newPage();
    page.on('pageerror', e => consoleErrors.push(url + ': ' + e.message));
    if (process.env.DEBUG404) { page.on('response', r => { if (r.status() >= 400) { console.log('        ' + r.status() + ' ' + r.url() + ' (page ' + page.url() + ')'); } }); }
    page.on('console', m => { if (m.type() === 'error') { consoleErrors.push(url + ': ' + m.text()); } });
    await page.goto(BASE + url, { waitUntil: 'networkidle' });
    await page.waitForFunction(() => document.readyState === 'complete');
    return { ctx, page };
  }

  const shot = async (page, file) => { if (SHOTS) { await page.screenshot({ path: `${SHOTS}/${file}.png`, fullPage: false }); } };
  const visibleText = (page) => page.evaluate(() => document.querySelector('main').innerText + ' ' + document.querySelector('.mat-nav').innerText + ' ' + (document.querySelector('.mat-footer') || { innerText: '' }).innerText);
  const sayMat = async (page) => { const text = await visibleText(page); return MAT_WORDS.filter(w => text.includes(w)); };

  console.log('A blank start in a browser: ' + BASE);

  // ---------------------------------------------------------------------------------------------------- the assistant
  console.log('\nSetup assistant');
  const { ctx, page } = await open(desktop, '/Studio/Login');
  await check('a Studio without an administrator leads to the assistant, which already speaks for the company the environment names', async () => {
    expectTrue(page.url().includes('/Studio/Setup'), 'not at the setup: ' + page.url());
    expect(await page.textContent('.mat-auth__card h1'), 'Set up the Studio', 'the title');
    expect(await page.locator('.mat-nav').count(), 0, 'menus on the setup page');
    expect(await page.inputValue('#Company'), COMPANY, 'the company of the environment');
    expect(await page.locator('html').getAttribute('data-app'), 'this-site', 'the app of the page');
    expect(await page.locator('.mat-choice input:checked').getAttribute('value'), 'blank', 'the template of the environment');
    await shot(page, 'setup-1-company');
  });
  await check('the three steps are one at a time, the second has the two templates as cards, and Back keeps what was typed', async () => {
    expect(await page.locator('[data-setup-step]:not([hidden])').count(), 1, 'steps on show');
    await page.click('[data-setup-next]');
    expect(await page.locator('.mat-choice').count(), 2, 'template cards');
    expect(await page.locator('.mat-choice__text small').allTextContents(), ['A set of sample apps, to look around and change.', 'Start with your own brand and no apps.'], 'the descriptions');
    await shot(page, 'setup-2-template');
    await page.click('[data-setup-back]');
    expect(await page.inputValue('#Company'), COMPANY, 'the company after Back');
    await page.fill('#Company', COMPANY + ' Industries');
    await page.click('[data-setup-next]');
    await page.locator('.mat-choice:has(input[value="blank"])').click();
    await page.click('[data-setup-next]');
    expectTrue(await page.isVisible('#Name'), 'no administrator step');
    await shot(page, 'setup-3-administrator');
  });
  await check('the administrator is created, signed in, and the site is the company\'s: its own name, its own colour, no Mat app', async () => {
    await page.fill('#Name', NAME);
    await page.fill('#Password', PASSWORD);
    await page.fill('#Repeat', PASSWORD);
    await page.click('[data-setup-submit]');
    await page.waitForURL('**/Studio');
    expectTrue((await page.textContent('body')).includes('You are signed in as ' + NAME), 'no welcome');
    expect(await page.textContent('.mat-nav__name'), COMPANY + ' Design', 'the name under the logo');
    expect(await page.textContent('.mat-nav__tagline'), `Identity of the ${COMPANY} apps`, 'the tagline');
    expectTrue((await page.textContent('.mat-nav__version')).startsWith(COMPANY + ' Design '), 'the version line: ' + await page.textContent('.mat-nav__version'));
    expect(await page.title(), 'Studio · ' + COMPANY + ' Design', 'the title of the page');
    expect(await page.locator('.mat-nav').innerText().then(t => MAT_WORDS.filter(w => t.includes(w))), [], 'words of the demo in the menu of the Studio');
    await shot(page, 'studio-start');
  });
  await ctx.close();

  // ---------------------------------------------------------------------------------------------------- the site
  console.log('\nThe site of the company');
  {
    const { ctx: c, page: p } = await open(desktop, '/');
    await check('the start page says the company, offers the first app and lists none of the Mat family', async () => {
      expect(await p.textContent('main h1'), COMPANY + ' Design', 'the title');
      expectTrue((await p.textContent('.doc-intro')).includes(`How the ${COMPANY} apps are meant to look.`), 'the intro');
      expectTrue((await p.textContent('.mat-footer__text')).includes(`${COMPANY} Design · the design and identity of the ${COMPANY} apps`), 'the footer');
      expect(await p.locator('.doc-app').count(), 0, 'app cards');
      expectTrue(await p.locator('.mat-empty:has-text("No apps yet")').count() > 0, 'no empty state');
      expectTrue(await p.locator('.mat-empty a.mat-btn--primary[href="/Studio/Apps/Edit"]').count() > 0, 'no way to add the first app');
      expect(await sayMat(p), [], 'words of the demo');
      await shot(p, 'home-blank-light');
      await p.locator('#family-title').scrollIntoViewIfNeeded();
      await shot(p, 'home-blank-apps-empty');
    });
    await check('the menu has no audit and the audit page is not there; the palette and the apps are', async () => {
      expect(await p.locator('a.mat-nav__item[href="/Identity/Audit"]').count(), 0, 'Audit in the menu');
      expect((await p.request.get(BASE + '/Identity/Audit', { failOnStatusCode: false })).status(), 404, 'the audit page');
      expect(await p.locator('a.mat-nav__item[href="/Identity/Palette"]').count(), 1, 'Palette in the menu');
      expect(await p.locator('a.mat-nav__item[href="/Identity"]').count(), 1, 'Apps in the menu');
    });
    await check('the pages about the company say nothing of another one: apps, palette, colour, patterns, builder, shell, download, and a frame of the sample app', async () => {
      const problems = [];
      for (const url of ['/Identity', '/Identity/Palette', '/Foundations', '/Patterns', '/Patterns/Detail?id=list', '/Builder', '/Shell', '/Download', '/Preview?view=list']) {
        await p.goto(BASE + url, { waitUntil: 'networkidle' });
        const said = await sayMat(p);
        if (said.length) { problems.push(url + ': ' + said.join(', ')); }
        if (await p.locator('main h1, h1').count() !== 1) { problems.push(url + ': h1 count ' + await p.locator('h1').count()); }
      }
      expect(problems, [], 'pages that speak of Mat');
    });
    await check('the accent of the site is the neutral blue and the logo has it', async () => {
      await p.goto(BASE + '/', { waitUntil: 'networkidle' });
      const colour = await p.evaluate(() => getComputedStyle(document.querySelector('.mat-nav__brand .mat-logo')).backgroundColor);
      expect(colour, 'rgb(37, 99, 235)', 'the logo');
    });
    await p.emulateMedia({ colorScheme: 'dark' });
    await p.goto(BASE + '/', { waitUntil: 'networkidle' });
    await shot(p, 'home-blank-dark');
    await c.close();

    const { ctx: m, page: q } = await open(phone, '/');
    await check('on a phone the start page of the company does not scroll sideways', async () => {
      expect(await q.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'sideways scroll');
      await shot(q, 'home-blank-phone');
    });
    await m.close();
  }

  // ---------------------------------------------------------------------------------------------------- an app in the Studio
  console.log('\nThe first app');
  const { ctx: s, page: sp } = await open(desktop, '/Studio/Login');
  await sp.fill('#Name', NAME);
  await sp.fill('#Password', PASSWORD);
  await sp.click('.mat-auth__card button[type="submit"]');
  await sp.waitForURL('**/Studio**');
  const setPicker = (id, value) => sp.evaluate(([i, v]) => { const el = document.getElementById(i); el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }, [id, value]);
  await check('the apps page of the Studio is an empty state with the way out, and the company page is in the menu', async () => {
    await sp.goto(BASE + '/Studio/Apps', { waitUntil: 'networkidle' });
    expectTrue(await sp.locator('.mat-empty:has-text("No apps yet")').count() > 0, 'no empty state');
    expect(await sp.locator('a.mat-nav__item[href="/Studio/Company"]').count(), 1, 'Company in the Studio menu');
    await shot(sp, 'studio-apps-empty');
  });
  await check('an app is created and is on the start page, in the list, in the palette and in the CSS of the toolkit', async () => {
    await sp.goto(BASE + '/Studio/Apps/Edit', { waitUntil: 'networkidle' });
    await sp.fill('#Input_Name', 'Acmemail');
    await sp.fill('#Input_Id', 'acmemail');
    await sp.fill('#Input_Tagline', 'Mail for the people of Acme');
    await setPicker('Input_Accent', '#0f766e');
    await sp.click('.mat-btn-row button.mat-btn--primary');
    await sp.waitForLoadState('networkidle');
    if (await sp.locator('[data-validation-summary]:not([hidden])').count() > 0) { throw new Error('the app was refused: ' + (await sp.locator('[data-validation-summary]').textContent()).trim().slice(0, 140)); }
    await sp.goto(BASE + '/', { waitUntil: 'networkidle' });
    expect(await sp.locator('.doc-app').count(), 1, 'app cards');
    expect(await sp.textContent('.doc-app__name'), 'Acmemail', 'the app on the start page');
    expect(await sp.locator('.mat-empty:has-text("No apps yet")').count(), 0, 'the empty state is still there');
    await sp.goto(BASE + '/Identity', { waitUntil: 'networkidle' });
    expectTrue((await sp.textContent('table.mat-table')).includes('Acmemail'), 'the app is not in the list');
    await sp.goto(BASE + '/Identity/Palette', { waitUntil: 'networkidle' });
    expectTrue((await sp.textContent('main')).includes('Acmemail'), 'the app is not on the palette page');
    const css = await (await sp.request.get(BASE + '/ui/matui.css')).text();
    expectTrue(css.includes('[data-app="acmemail"] { --mat-app-accent: #0f766e; }'), 'the colour of the app is not in the CSS');
    expectTrue(css.includes('[data-app="this-site"] { --mat-app-accent: #2563eb; }'), 'the colour of the site is not in the CSS');
    await shot(sp, 'palette-one-app');
  });

  // ---------------------------------------------------------------------------------------------------- the words of a blank site (IDENT-09)
  // The toolkit's own texts (the rules, the documentation of the components, the changelog) are written in the words of the Mat family and are read in the company's words
  // through one map (TenantWords); the icon of the site is the company's colour. A visitor of a blank site must not read Mat anywhere.
  console.log('\nThe words of a blank site');
  {
    const { ctx: c, page: p } = await open(desktop, '/Rules');
    const read = async (url) => {
      await p.goto(BASE + url, { waitUntil: 'networkidle' });
      const title = await p.title();
      return { title, said: (await sayMat(p)).concat(MAT_WORDS.filter(w => title.includes(w))) };
    };
    await check('the rules page and a rule say the company and no word of the Mat family; the title is the company\'s', async () => {
      const list = await read('/Rules');
      expect(list.title, 'Rulebook · ' + COMPANY + ' Design', 'the title of the rules page');
      expect(list.said, [], 'words of the Mat family on the rules page');
      const sources = await p.evaluate(() => [...document.querySelectorAll('table.mat-table tbody td[data-label="Source"]')].map(td => td.textContent.trim()));
      expectTrue(sources.length > 0 && sources.every(s => ['Owner', 'Toolkit', 'Reference app', 'Studio'].includes(s)), 'the sources of the rules: ' + [...new Set(sources)].join(', '));
      await shot(p, 'rules-blank');
      const identity = await read('/Rules/Detail?id=IDENT-09');
      expect(identity.said, [], 'words of the Mat family on the rule about names');
      expectTrue((await p.textContent('main')).includes('A company that starts blank sees its own name and none of the sample apps.'), 'the sentence of IDENT-09');
      const version = await read('/Rules/Detail?id=SHELL-06');
      expect(version.said, [], 'words of the Mat family on SHELL-06');
      expectTrue((await p.textContent('main')).includes('Acmemail 1.4.212-20261010'), 'the example name is an app of the company');
      await shot(p, 'rule-blank');
    });
    await check('a component page, the overview, the changelog and the icon library say no word of the Mat family; the examples are the company\'s', async () => {
      const problems = [];
      for (const url of ['/Components', '/Components/breadcrumb', '/Components/topbar', '/Components/footer', '/Components/colour-picker', '/Components/typography', '/Changelog', '/Foundations/IconLibrary', '/Patterns', '/Layout', '/Utilities', '/Download', '/Rules/Changes']) {
        const said = (await read(url)).said;
        if (said.length) { problems.push(url + ': ' + said.join(', ')); }
      }
      expect(problems, [], 'pages that speak of the Mat family');
      await p.goto(BASE + '/Components/breadcrumb', { waitUntil: 'networkidle' });
      expect(await p.locator('.doc-example__live .mat-crumbs li:first-child').first().innerText(), 'Acmemail', 'the first crumb of the example');
      await shot(p, 'component-blank');
      await p.goto(BASE + '/Components/typography', { waitUntil: 'networkidle' });
      const alts = await p.evaluate(() => [...document.querySelectorAll('.doc-example__live img')].map(i => i.alt));
      expectTrue(alts.length >= 3 && alts.every(a => a.startsWith('The ' + COMPANY + ' Design logo')), 'the alt texts of the sample picture: ' + alts.join(' | '));
      await p.goto(BASE + '/Components/colour-picker', { waitUntil: 'networkidle' });
      expectTrue((await p.locator('.doc-example__live [data-swatches]').first().getAttribute('data-swatches')).includes(':Indigo'), 'the swatches of the picker are named by colour');
    });
    await check('the icon of the site is the glyph in the accent of the company, in the link and as a file; an icon of the library says it was drawn for the toolkit', async () => {
      await p.goto(BASE + '/', { waitUntil: 'networkidle' });
      expect(await p.getAttribute('link[rel="icon"]', 'href'), '/favicon.svg', 'the link of the icon');
      const svg = await (await p.request.get(BASE + '/favicon.svg')).text();
      expectTrue(svg.includes('fill=\"#2563eb\"') && !svg.toLowerCase().includes('#d6336c') && svg.includes('aria-label=\"' + COMPANY + ' Design\"'), 'the file: ' + svg.slice(0, 160));
      // the file is looked at as the picture of the examples (a page that is only an svg makes the browser ask for /favicon.ico, which the site does not have)
      await p.goto(BASE + '/Components/typography', { waitUntil: 'networkidle' });
      if (SHOTS) { await p.locator('.doc-example__live img').first().screenshot({ path: SHOTS + '/favicon-blank.png' }); }
      await p.goto(BASE + '/Foundations/IconLibrary?q=radiator', { waitUntil: 'networkidle' });
      await p.locator('.doc-icon[data-iconlib=\"radiator\"]').first().click();
      await p.waitForSelector('dialog#iconlib-dialog[open]');
      expect(await p.textContent('#iconlib-dialog [data-iconlib-origin]'), 'Drawn for the toolkit', 'the origin line of the dialog');
      await p.keyboard.press('Escape');
    });
    await check('the shell skeleton is shown for the site\'s own app: its name, its tagline, its glyph, nothing of an example app', async () => {
      await p.goto(BASE + '/Shell', { waitUntil: 'networkidle' });
      const code = await p.textContent('#skeleton');
      expectTrue(code.includes('<span class=\"mat-nav__name\">' + COMPANY + ' Design</span><span class=\"mat-nav__tagline\">Identity of the ' + COMPANY + ' apps</span>'), 'the brand of the skeleton');
      expect(['MatMail', 'matmail', 'Mail gateway', 'mat-i-mail\"'].filter(w => code.includes(w)), [], 'what the skeleton keeps of the example app');
    });
    await check('the search finds nothing of the Mat family by its names, in the popup and in the answer of the API', async () => {
      const found = [];
      for (const q of ['MatMail', 'Mattiverse', 'MatPaper', 'Mat apps', 'Mat family', 'MatDesign', 'Matthix']) {
        const answer = await (await p.request.get(BASE + '/api/search?limit=50&q=' + encodeURIComponent(q))).json();
        for (const g of answer.groups) { for (const i of g.items) { const said = MAT_WORDS.filter(w => (i.title + ' ' + i.text + ' ' + i.href).includes(w)); if (said.length) { found.push(q + ' -> ' + i.title + ': ' + said.join(', ')); } } }
      }
      expect(found, [], 'results that name the family');
      await p.goto(BASE + '/', { waitUntil: 'networkidle' });
      await p.focus('#site-search');
      await p.keyboard.type('MatMail', { delay: 20 });
      await p.waitForFunction(() => { const s = document.querySelector('#site-search').closest('.mat-search')._matSearch; return s.q === 'MatMail' && (s.mode === 'results' || s.mode === 'empty'); });
      const options = await p.evaluate(() => [...document.querySelectorAll('.mat-search__panel [role=option]')].map(o => o.innerText));
      // the last row (All results for what was typed) says the words of the visitor, not the site's
      expect(options.filter(o => !o.startsWith('All results for') && MAT_WORDS.some(w => o.includes(w))), [], 'options of the popup that name the family');
      await shot(p, 'search-matmail-blank');
    });
    await c.close();
  }

  // ---------------------------------------------------------------------------------------------------- the search of the site (SHELL-20, FORM-23)
  console.log('\nSearch');
  {
    const { ctx: c, page: p } = await open(desktop, '/');
    await check('the header bar of the blank site has the search: it finds the toolkit and the app of the company, no app of the Mat family, and says nothing of another company', async () => {
      const answer = async (q) => (await p.request.get(BASE + '/api/search?limit=50&q=' + encodeURIComponent(q))).json();
      expect((await answer('acmemail')).groups.filter(g => g.kind === 'app').map(g => g.items.map(i => i.title)), [['Acmemail']], 'the app of the company is found');
      expect((await answer('matpaper')).groups.filter(g => g.kind === 'app'), [], 'no app of the family');
      expect((await answer('button')).groups[0].items[0].href, '/Components/buttons', 'the toolkit is the same');
      await p.focus('#site-search');
      await p.keyboard.type('acme', { delay: 20 });
      await p.waitForFunction(() => { const s = document.querySelector('#site-search').closest('.mat-search')._matSearch; return s.q === 'acme' && s.mode === 'results'; });
      const text = await p.evaluate(() => document.querySelector('.mat-search__panel').innerText);
      expectTrue(text.includes('Acmemail'), 'the app is not in the popup: ' + text.slice(0, 200));
      expect(MAT_WORDS.filter(w => text.includes(w)), [], 'words of the demo in the popup');
      expect(await p.evaluate(() => [document.querySelector('label[for="site-search"]').textContent, document.querySelector('#site-search').placeholder]), ['Search the site', 'Search'], 'the words of the field');
      await shot(p, 'search-blank');
    });
    await c.close();
  }

  // ---------------------------------------------------------------------------------------------------- the company page: to the demo and back
  console.log('\nCompany page');
  // a switch is a post and a redirect: wait until the site shows it (the page is quiet for a moment before the answer comes)
  const until = async (what, fn) => { for (let i = 0; i < 40; i++) { if (await fn()) { return; } await sp.waitForTimeout(250); } throw new Error('never happened: ' + what); };
  const apiApps = async () => (await (await sp.request.get(BASE + '/api/apps')).json()).map(a => a.id);
  await check('the company page shows the blank template in use and asks before it switches', async () => {
    await sp.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
    expect(await sp.textContent('main h1'), 'Company', 'the title');
    expect(await sp.inputValue('#Input_Company'), COMPANY + ' Industries', 'the company');
    expect(await sp.locator('[data-template="blank"] .mat-pill', { hasText: 'In use' }).count(), 1, 'the template in use');
    expectTrue(await sp.locator('#Input_Accent').count() === 1, 'no accent for a blank site');
    await shot(sp, 'company-blank');
    await sp.click('[data-template-switch]');
    await sp.waitForSelector('dialog[open]');
    const question = await sp.textContent('dialog[open]');
    expectTrue(question.includes('Nothing is deleted') && question.includes('demo template'), 'the question: ' + question.trim().slice(0, 200));
    await sp.waitForTimeout(400);
    await shot(sp, 'company-switch-question');
    await sp.keyboard.press('Escape');
    await sp.waitForFunction(() => !document.querySelector('dialog[open]'));
    expect(await sp.locator('[data-template="blank"] .mat-pill', { hasText: 'In use' }).count(), 1, 'the template after Cancel');
  });
  await check('switching to the demo brings the Mat apps and the audit back, keeps the company\'s app and the names', async () => {
    await sp.click('[data-template-switch]');
    await sp.waitForSelector('dialog[open]');
    await sp.click('dialog[open] button[value="ok"]');
    await until('the demo apps', async () => (await apiApps()).includes('mattiverse'));
    await sp.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
    expect(await sp.locator('[data-template="demo"] .mat-pill', { hasText: 'In use' }).count(), 1, 'the template in use');
    const apps = (await (await sp.request.get(BASE + '/api/apps')).json()).map(a => a.id);
    expectTrue(apps.includes('mattiverse') && apps.includes('matdesign') && apps.includes('acmemail'), 'the apps: ' + apps.join(','));
    await sp.goto(BASE + '/', { waitUntil: 'networkidle' });
    // the template changes what is built in; the names of the company are kept (docs/STUDIO.md, Company and templates), so the site is still called what the company named it
    expect(await sp.textContent('.mat-nav__name'), COMPANY + ' Design', 'the demo layer under the name of the company');
    expect(await sp.locator('a.mat-nav__item[href="/Identity/Audit"]').count(), 1, 'Audit in the menu');
    expect(await sp.locator('#Input_Accent').count(), 0, 'accent on the demo');
    await shot(sp, 'home-demo-light');
  });
  await check('switching back hides them again and the company\'s own app stays', async () => {
    await sp.goto(BASE + '/Studio/Company', { waitUntil: 'networkidle' });
    await sp.click('[data-template-switch]');
    await sp.waitForSelector('dialog[open]');
    await sp.click('dialog[open] button[value="ok"]');
    await until('the Mat apps hidden', async () => !(await apiApps()).includes('mattiverse'));
    const apps = await apiApps();
    expect(apps, ['acmemail'], 'the apps');
    await sp.goto(BASE + '/', { waitUntil: 'networkidle' });
    expect(await sp.textContent('.mat-nav__name'), COMPANY + ' Design', 'the name of the site');
    expect(await sayMat(sp), [], 'words of the demo');
  });
  await check('the history of the company lists the revisions and the checks pass', async () => {
    await sp.goto(BASE + '/Studio/History?kind=tenant&id=tenant', { waitUntil: 'networkidle' });
    expectTrue((await sp.locator('main table tbody tr').count()) >= 2, 'revisions in the history');
    await sp.goto(BASE + '/Studio/Checks', { waitUntil: 'networkidle' });
    expectTrue((await sp.textContent('a.mat-nav__item[href="/Studio/Checks"]')).includes('all checks pass'), 'the badge of Checks');
    expectTrue((await sp.textContent('main')).includes('The company is valid'), 'the company check is not listed');
  });
  await check('a release names the company', async () => {
    await sp.goto(BASE + '/Studio/Releases/New', { waitUntil: 'networkidle' });
    await sp.click('button[data-confirm-ok="Run it"]');
    await sp.waitForSelector('dialog[open]');
    await sp.click('dialog[open] button[value="ok"]');
    await sp.waitForURL('**/Studio/Releases/Run**');
    const log = await sp.textContent('main');
    expectTrue(log.includes(COMPANY + ' Design: '), 'the changelog header does not name the site: ' + log.slice(0, 200));
    const latest = await (await sp.request.get(BASE + '/releases/latest.json')).json();
    expect(latest.company, COMPANY + ' Industries', 'the company of the release');
    const design = await (await sp.request.get(`${BASE}/releases/${latest.version}/design.json`)).json();
    expect(design.tenant.siteName, COMPANY + ' Design', 'design.json');
    const styleguide = await (await sp.request.get(`${BASE}/releases/${latest.version}/styleguide.html`)).text();
    expectTrue(styleguide.includes(`The design system of ${COMPANY} Industries.`), 'the styleguide');
  });
  await s.close();

  console.log('');
  await check('no script error and no error in the console on any page above', async () => {
    expect(consoleErrors, [], 'console errors');
  });

  await browser.close();
  const failed = results.filter(r => !r.ok);
  console.log(`\n${results.length - failed.length} of ${results.length} checks passed.`);
  process.exit(failed.length === 0 ? 0 : 1);
})().catch(e => { console.error('FAILED to run:', e.message); annotate(__filename, 'FAILED to run: ' + e.message.split('\n')[0]); process.exit(2); });
