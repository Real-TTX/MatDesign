/* ==========================================================================
   MatUi · behaviour
   Dependency-free. Everything is wired by delegated listeners and data-attributes, so server-rendered and dynamically added
   markup behave the same. The public API is window.MatUi (dialog, confirm, alert, prompt, toast, theme, accent).

   Data-attributes
     data-theme-toggle                      the 3-state button (system -> light -> dark); data-theme-save-url posts the choice
     data-theme-set="system|light|dark"     a button of a segmented control in the settings that chooses the theme (aria-pressed follows)
     data-nav-toggle / data-nav-close       open / close the menu drawer (phones)
     data-nav-branch="key"                  the parent of a submenu in the menu (aria-expanded; the choice is remembered per app)
     data-nav-set="id" / data-nav-panel="id"  the menu switcher: an entry of its menu shows the panel of items with the same id;
                                            the menu names the element with the switcher and the panels: data-nav-scope="id"
     data-scrollspy                         an index of #links to sections of the page ("On this page"): the link of the section in view is marked
     data-validation-summary                an alert in a form that lists the fields with an error, each a link to its field (hidden while there are none)
     data-output="id"                       a range input writes its value into the element with this id (the filled part of the track is drawn for every .mat-range)
     data-counter="id"                      a text field writes "length / maxlength" into the element with this id
     data-indeterminate                     a checkbox that starts in the indeterminate state
     data-password-toggle="id"              the eye button of a password field: shows and hides the password (aria-pressed)
     data-selectable                        a table with data-select-row checks, a data-select-all check in its header and a [data-bulk-bar] with [data-bulk-count]
     data-menu-toggle="id"                  open the context menu #id next to the button; data-menu-place="top|bottom"
     data-dialog-open="id" / data-dialog-close   open / close a <dialog class="mat-dialog">
     data-confirm="Question?"               ask in a dialog before a submit button, a form or a link goes ahead
     data-show-if="#id | name" data-show-value="a,b | * | true"   show a dependent field only when it is needed
     data-copy="text | #id"                 copy to the clipboard
     data-tabs                              a tab group (role=tablist buttons + role=tabpanel panels)
     data-dismiss                           close the alert around it
     data-view-switch="list|gallery"        switch between the two views of a list (remembered per data-view-key)
     data-shortcut="/"                      key that triggers the element (not while typing)
   ========================================================================== */
(function () {
  'use strict';

  var root = document.documentElement;
  var doc = document;

  // ---------------------------------------------------------------------------------------------------- helpers
  function $(sel, ctx) { return (ctx || doc).querySelector(sel); }
  function $$(sel, ctx) { return Array.prototype.slice.call((ctx || doc).querySelectorAll(sel)); }
  function esc(s) { return String(s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function label(attr, fallback) { return root.getAttribute(attr) || fallback; }
  function icon(name) {
    var sprite = root.getAttribute('data-mat-sprite') || '';
    return '<svg class="mat-icon" aria-hidden="true" focusable="false"><use href="' + sprite + '#mat-i-' + name + '"></use></svg>';
  }
  var uid = 0;
  function nextId(prefix) { uid += 1; return prefix + '-' + uid; }

  // ------------------------------------------------------------------------------- scrollbars only while scrolling
  var scrollTimers = new WeakMap();
  doc.addEventListener('scroll', function (e) {
    var el = e.target === doc ? root : e.target;
    if (!el || !el.classList) { return; }
    el.classList.add('is-scrolling');
    clearTimeout(scrollTimers.get(el));
    scrollTimers.set(el, setTimeout(function () { el.classList.remove('is-scrolling'); }, 900));
  }, true);

  // ---------------------------------------------------------------------------------------------------- theme
  // system -> light -> dark -> system. "system" means no data-theme attribute: the CSS follows the OS.
  var THEMES = ['system', 'light', 'dark'];
  var THEME_NAMES = { system: 'System', light: 'Light', dark: 'Dark' };

  function currentTheme() {
    var t = root.getAttribute('data-theme');
    return t === 'light' || t === 'dark' ? t : 'system';
  }

  function applyTheme(theme, persist) {
    if (theme === 'light' || theme === 'dark') { root.setAttribute('data-theme', theme); } else { root.removeAttribute('data-theme'); theme = 'system'; }
    if (persist !== false && !root.hasAttribute('data-preview')) { try { localStorage.setItem('mat-theme', theme); } catch (e) { /* ignore */ } }
    $$('[data-theme-toggle]').forEach(function (btn) {
      var next = THEMES[(THEMES.indexOf(theme) + 1) % THEMES.length];
      btn.setAttribute('data-theme-state', theme);
      var text = 'Theme: ' + THEME_NAMES[theme] + '. Click for ' + THEME_NAMES[next] + '.';
      btn.setAttribute('aria-label', text);
      btn.setAttribute('title', text);
    });
    $$('[data-theme-set]').forEach(function (btn) { btn.setAttribute('aria-pressed', btn.getAttribute('data-theme-set') === theme ? 'true' : 'false'); });
    root.dispatchEvent(new CustomEvent('mat:theme', { detail: { theme: theme } }));
  }

  function cycleTheme(btn) {
    chooseTheme(btn, THEMES[(THEMES.indexOf(currentTheme()) + 1) % THEMES.length]);
  }

  // The 3-state button cycles; a segmented control in the account settings chooses (data-theme-set="light"). Both save the same way.
  function chooseTheme(btn, theme) {
    if (THEMES.indexOf(theme) < 0) { return; }
    applyTheme(theme);
    var url = btn && btn.getAttribute('data-theme-save-url');
    if (url) {
      var body = new URLSearchParams({ theme: theme });
      var token = $('input[name="__RequestVerificationToken"]');
      if (token) { body.set('__RequestVerificationToken', token.value); }
      fetch(url, { method: 'POST', body: body, credentials: 'same-origin' }).catch(function () { /* the choice stays in this browser */ });
    }
  }

  // A free colour: the on-accent colour (white or dark) is the one with the better contrast, like the toolkit does for the apps.
  function luminance(hex) {
    var v = [1, 3, 5].map(function (i) { var c = parseInt(hex.substr(i, 2), 16) / 255; return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4); });
    return 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2];
  }
  function customAccent(hex) {
    if (!/^#[0-9a-fA-F]{6}$/.test(hex || "")) { return false; }
    var l = luminance(hex);
    var onWhite = 1.05 / (l + 0.05), onInk = (l + 0.05) / (luminance("#0b1020") + 0.05);
    root.style.setProperty("--mat-custom-accent", hex);
    root.style.setProperty("--mat-custom-on-accent", onWhite >= 4.5 || onWhite >= onInk ? "#ffffff" : "#0b1020");
    root.setAttribute("data-accent", "custom");
    root.dispatchEvent(new CustomEvent("mat:accent", { detail: { accent: "custom", color: hex } }));
    return true;
  }

  function setAccent(name) {
    if (name) { root.setAttribute('data-accent', name); } else { root.removeAttribute('data-accent'); }
    try { if (name) { localStorage.setItem('mat-accent', name); } else { localStorage.removeItem('mat-accent'); } } catch (e) { /* ignore */ }
    root.dispatchEvent(new CustomEvent('mat:accent', { detail: { accent: name || null } }));
  }

  // ---------------------------------------------------------------------------------------------- menu drawer (phones)
  function app() { return $('.mat-app'); }
  var drawerOpener = null;

  function setNav(open, opener) {
    var shell = app();
    if (!shell) { return; }
    shell.classList.toggle('is-nav-open', open);
    root.classList.toggle('mat-lock', open);
    $$('[data-nav-toggle]').forEach(function (b) { b.setAttribute('aria-expanded', open ? 'true' : 'false'); });
    var nav = $('.mat-nav', shell);
    if (open) {
      drawerOpener = opener || doc.activeElement;
      var first = nav && $('.mat-nav__scroll:not([hidden]) .mat-nav__item', nav);
      if (first) { first.focus({ preventScroll: true }); }
    } else if (drawerOpener && drawerOpener.focus) {
      drawerOpener.focus({ preventScroll: true });
      drawerOpener = null;
    }
  }

  // ------------------------------------------------------------------------------ menu: submenus and menu sets (SHELL-13, SHELL-15)
  // A parent item is a button (data-nav-branch="key", aria-expanded) that opens its children. The branch with the current page is open
  // (server side: is-open + is-current); what the user opened or closed is remembered per app. A menu set is a panel of items
  // (data-nav-panel="id"); the switcher under the logo (data-nav-set="id" in its menu) shows one panel at a time.
  var BRANCH_KEY = 'mat-nav-branches';

  function branchStore() {
    try { return JSON.parse(localStorage.getItem(BRANCH_KEY) || '{}') || {}; } catch (e) { return {}; }
  }

  function branchId(btn) { return (root.getAttribute('data-app') || '') + ':' + btn.getAttribute('data-nav-branch'); }

  function setBranch(btn, open, remember) {
    var branch = btn.closest('.mat-nav__branch');
    if (!branch) { return; }
    if (!open && branch.contains(doc.activeElement) && doc.activeElement !== btn) { btn.focus({ preventScroll: true }); }
    branch.classList.toggle('is-open', open);
    btn.setAttribute('aria-expanded', open ? 'true' : 'false');
    if (remember) {
      var store = branchStore();
      store[branchId(btn)] = open;
      try { localStorage.setItem(BRANCH_KEY, JSON.stringify(store)); } catch (e) { /* ignore */ }
    }
  }

  function restoreBranches() {
    var store = branchStore();
    $$('[data-nav-branch]').forEach(function (btn) {
      var saved = store[branchId(btn)];
      // the branch of the current page stays open whatever was saved
      if (typeof saved === 'boolean' && !btn.closest('.mat-nav__branch').classList.contains('is-current')) { setBranch(btn, saved, false); }
    });
  }

  // The popup of the switcher names the element that holds the switcher and the panels (data-nav-scope="id"), so a menu in a
  // documentation example never touches the menu of the page.
  function setNavSet(id, popup) {
    var menu = popup || $('.mat-menu[data-nav-scope]');
    var scope = menu && doc.getElementById(menu.getAttribute('data-nav-scope'));
    if (!scope) { return; }
    var panels = $$('[data-nav-panel]', scope);
    if (!panels.some(function (p) { return p.getAttribute('data-nav-panel') === id; })) { return; }
    panels.forEach(function (p) { p.hidden = p.getAttribute('data-nav-panel') !== id; });
    var chosen = null;
    var buttons = $$('[data-nav-set]', menu);
    buttons.forEach(function (b) {
      var on = b.getAttribute('data-nav-set') === id;
      b.setAttribute('aria-checked', on ? 'true' : 'false');
      if (on) { chosen = b; }
    });
    var switcher = $('.mat-switcher', scope);
    if (switcher && chosen) {
      var name = $('.mat-switcher__name', switcher);
      if (name) { name.textContent = chosen.getAttribute('data-nav-title') || ''; }
      var use = $('.mat-switcher__icon use', switcher);
      var from = $('use', chosen);
      if (use && from) { use.setAttribute('href', from.getAttribute('href')); }
      var dot = $('.mat-switcher__dot', switcher);
      if (dot) { dot.hidden = !buttons.some(function (b) { return b !== chosen && b.hasAttribute('data-nav-attention'); }); }
    }
    var shown = $('[data-nav-panel]:not([hidden])', scope);
    if (shown) { shown.scrollTop = 0; }
    root.dispatchEvent(new CustomEvent('mat:navset', { detail: { set: id } }));
  }

  // ------------------------------------------------------------------------------------------ on this page: scrollspy (SHELL-16)
  // An index (data-scrollspy) of links to sections of the page; the link of the section that is at the top of the screen is marked
  // (is-active, aria-current="location"). It works for several indexes at once (the one beside the content and the one in the dialog).
  var spyPending = false;

  function scrollContainer(el) {
    for (var p = el.parentElement; p; p = p.parentElement) {
      var o = getComputedStyle(p).overflowY;
      if ((o === 'auto' || o === 'scroll') && p.scrollHeight > p.clientHeight) { return p; }
    }
    return null;
  }

  function keepInView(box, el) {
    var b = box.getBoundingClientRect(), r = el.getBoundingClientRect();
    if (r.top < b.top) { box.scrollTop -= b.top - r.top + 8; } else if (r.bottom > b.bottom) { box.scrollTop += r.bottom - b.bottom + 8; }
  }

  function updateSpy() {
    spyPending = false;
    $$('[data-scrollspy]').forEach(function (nav) {
      var entries = [];
      $$('a[href^="#"]', nav).forEach(function (a) {
        var target = doc.getElementById(decodeURIComponent(a.getAttribute('href').slice(1)));
        if (target) { entries.push({ a: a, target: target }); }
      });
      if (entries.length === 0) { return; }
      var targets = entries.map(function (e) { return e.target; }).filter(function (t, i, all) { return all.indexOf(t) === i; });
      targets.sort(function (a, b) { return a.compareDocumentPosition(b) & 4 ? -1 : 1; });
      var box = scrollContainer(targets[0]);
      var top = box ? box.getBoundingClientRect().top : 0;
      var line = top + (parseFloat(getComputedStyle(root).getPropertyValue('--mat-topbar-h')) || 56) + 24;
      var current = targets[0];
      targets.forEach(function (t) { if (t.getBoundingClientRect().top <= line) { current = t; } });
      // at the very end of the page the last section is the current one even when it is too short to reach the line
      if (box && box.scrollTop + box.clientHeight >= box.scrollHeight - 2) { current = targets[targets.length - 1]; }
      entries.forEach(function (e) {
        var on = e.target === current;
        e.a.classList.toggle('is-active', on);
        if (on) { e.a.setAttribute('aria-current', 'location'); } else { e.a.removeAttribute('aria-current'); }
        if (on) { var aside = e.a.closest('.mat-toc-aside'); if (aside) { keepInView(aside, e.a); } }
      });
    });
  }

  function scheduleSpy() {
    if (!spyPending) { spyPending = true; window.requestAnimationFrame(updateSpy); }
  }

  // Arrow keys inside the menu: up and down walk the visible items, right opens a parent (or goes to its first child), left closes it (or goes to the parent).
  function navKeys(e) {
    var panel = e.target.closest('.mat-nav__scroll');
    if (!panel || e.ctrlKey || e.metaKey || e.altKey) { return false; }
    var keys = ['ArrowDown', 'ArrowUp', 'Home', 'End', 'ArrowRight', 'ArrowLeft'];
    if (keys.indexOf(e.key) < 0) { return false; }
    var items = $$('.mat-nav__item', panel).filter(function (el) { return !el.closest('.mat-nav__branch:not(.is-open)') || !el.classList.contains('mat-nav__item--sub'); });
    var current = e.target.closest('.mat-nav__item');
    var i = items.indexOf(current);
    if (i < 0) { return false; }
    var target = null;
    if (e.key === 'ArrowDown') { target = items[Math.min(items.length - 1, i + 1)]; }
    else if (e.key === 'ArrowUp') { target = items[Math.max(0, i - 1)]; }
    else if (e.key === 'Home') { target = items[0]; }
    else if (e.key === 'End') { target = items[items.length - 1]; }
    else if (e.key === 'ArrowRight' && current.hasAttribute('data-nav-branch')) {
      if (current.getAttribute('aria-expanded') !== 'true') { setBranch(current, true, true); }
      else { target = $('.mat-nav__sub .mat-nav__item', current.closest('.mat-nav__branch')); }
    } else if (e.key === 'ArrowLeft') {
      if (current.hasAttribute('data-nav-branch') && current.getAttribute('aria-expanded') === 'true') { setBranch(current, false, true); }
      else if (current.classList.contains('mat-nav__item--sub')) { target = $('[data-nav-branch]', current.closest('.mat-nav__branch')); }
    } else { return false; }
    e.preventDefault();
    if (target) { target.focus(); }
    return true;
  }

  // ------------------------------------------------------------------------------------------------ context menus
  var openMenu = null;

  function closeMenu() {
    if (!openMenu) { return; }
    var m = openMenu;
    openMenu = null;
    m.menu.hidden = true;
    m.btn.setAttribute('aria-expanded', 'false');
    if (m.restoreFocus) { m.btn.focus({ preventScroll: true }); }
  }

  function placeMenu(menu, btn, place) {
    menu.style.left = '0px'; menu.style.top = '0px'; menu.style.bottom = 'auto'; menu.style.minWidth = '';
    var r = btn.getBoundingClientRect();
    var vw = window.innerWidth, vh = window.innerHeight;
    var w = Math.max(menu.offsetWidth, Math.min(r.width, 320)), h = menu.offsetHeight;
    var below = place === 'bottom' || (place !== 'top' && r.top < h + 16);
    var top = below ? Math.min(r.bottom + 6, vh - h - 8) : Math.max(8, r.top - h - 6);
    var left = Math.min(Math.max(8, r.left), vw - w - 8);
    menu.style.minWidth = Math.max(menu.offsetWidth, Math.min(r.width, 320)) + 'px';
    menu.style.left = left + 'px';
    menu.style.top = Math.max(8, top) + 'px';
    menu.style.transformOrigin = (below ? 'top' : 'bottom') + ' left';
  }

  function toggleMenu(btn) {
    var menu = doc.getElementById(btn.getAttribute('data-menu-toggle'));
    if (!menu) { return; }
    if (openMenu && openMenu.menu === menu) { closeMenu(); return; }
    closeMenu();
    menu.hidden = false;
    placeMenu(menu, btn, btn.getAttribute('data-menu-place'));
    btn.setAttribute('aria-expanded', 'true');
    openMenu = { menu: menu, btn: btn, restoreFocus: false };
    var first = $('.mat-menu__item', menu);
    if (first) { first.focus({ preventScroll: true }); openMenu.restoreFocus = true; }
  }

  // ---------------------------------------------------------------------------------------------------- dialogs
  var openDialogs = 0;

  function dialogEl(ref) { return typeof ref === 'string' ? doc.getElementById(ref) : ref; }

  function syncLock() { root.classList.toggle('mat-lock', openDialogs > 0 || (app() && app().classList.contains('is-nav-open'))); }

  function openDialog(ref, opener) {
    var d = dialogEl(ref);
    if (!d || d.open) { return d; }
    d._matOpener = opener || doc.activeElement;
    if (typeof d.showModal === 'function') { d.showModal(); } else { d.setAttribute('open', ''); }
    openDialogs += 1;
    syncLock();
    var target = $('[autofocus]', d) || $('input:not([type=hidden]):not([disabled]), select, textarea', d);
    if (target) { target.focus({ preventScroll: true }); if (target.select && target.type === 'search') { target.select(); } }
    d.dispatchEvent(new CustomEvent('mat:dialog-open', { bubbles: true }));
    return d;
  }

  function closeDialog(ref, result) {
    var d = dialogEl(ref);
    if (!d || !d.open) { return; }
    if (result !== undefined) { d.returnValue = result; }
    if (typeof d.close === 'function') { d.close(result); } else { d.removeAttribute('open'); d.dispatchEvent(new Event('close')); }
  }

  // every <dialog class="mat-dialog">: restore focus and the scroll lock when it closes, whoever closed it (button, Esc, script)
  doc.addEventListener('close', function (e) {
    var d = e.target;
    if (!d || !d.classList || !d.classList.contains('mat-dialog')) { return; }
    openDialogs = Math.max(0, openDialogs - 1);
    syncLock();
    var op = d._matOpener;
    d._matOpener = null;
    if (op && op.focus && doc.contains(op)) { op.focus({ preventScroll: true }); }
  }, true);

  // A click on the backdrop closes a dialog that is not a data-entry form; on a phone there is no backdrop.
  doc.addEventListener('click', function (e) {
    var d = e.target;
    if (d && d.classList && d.classList.contains('mat-dialog') && d.open && !d.hasAttribute('data-static')) {
      var r = d.getBoundingClientRect();
      if (e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom) { closeDialog(d, 'cancel'); }
    }
  });

  function buildDialog(opts) {
    var id = nextId('mat-dlg');
    var d = doc.createElement('dialog');
    d.className = 'mat-dialog mat-dialog--sm';
    d.id = id;
    d.setAttribute('role', opts.role || 'dialog');
    d.setAttribute('aria-labelledby', id + '-t');
    d.setAttribute('aria-describedby', id + '-b');
    d.setAttribute('data-static', '');
    d.innerHTML =
      '<form method="dialog" class="mat-dialog__form">' +
      '<div class="mat-dialog__head">' + (opts.icon ? icon(opts.icon) : '') +
      '<h2 class="mat-dialog__title" id="' + id + '-t">' + esc(opts.title || '') + '</h2>' +
      '<button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-dialog__close" data-dialog-close aria-label="' + esc(label('data-dlg-close', 'Close')) + '">' + icon('x') + '</button></div>' +
      '<div class="mat-dialog__body" id="' + id + '-b">' + opts.body + '</div>' +
      '<div class="mat-dialog__foot"><div class="mat-btn-row mat-btn-row--plain">' + opts.buttons + '</div></div>' +
      '</form>';
    doc.body.appendChild(d);
    return d;
  }

  function show(opts) {
    return new Promise(function (resolve) {
      var d = buildDialog(opts);
      var done = false;
      function finish(value) {
        if (done) { return; }
        done = true;
        resolve(value);
      }
      d.addEventListener('close', function () {
        var v = d.returnValue;
        var out = opts.result ? opts.result(d, v) : v === 'ok';
        finish(out);
        setTimeout(function () { d.remove(); }, 0);
      });
      d.addEventListener('submit', function (ev) { ev.preventDefault(); closeDialog(d, 'ok'); });
      openDialog(d);
    });
  }

  function confirmDialog(message, options) {
    var o = options || {};
    var okText = o.ok || label('data-dlg-ok', 'OK');
    var cancelText = o.cancel || label('data-dlg-cancel', 'Cancel');
    // destructive: Cancel first, the destructive action at the end (positive -> negative); otherwise OK first.
    var buttons = o.danger
      ? '<button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>' + esc(cancelText) + '</button><span class="mat-btn-row__spacer"></span>' +
        '<button type="submit" class="mat-btn mat-btn--danger mat-btn--solid" value="ok">' + esc(okText) + '</button>'
      : '<button type="submit" class="mat-btn mat-btn--primary" value="ok" autofocus>' + esc(okText) + '</button>' +
        '<button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>' + esc(cancelText) + '</button>';
    return show({ title: o.title || label('data-dlg-confirm', 'Please confirm'), icon: o.danger ? 'alert' : 'help-circle', role: 'alertdialog', body: '<p>' + esc(message) + '</p>', buttons: buttons });
  }

  function alertDialog(message, options) {
    var o = options || {};
    return show({
      title: o.title || label('data-dlg-notice', 'Notice'), icon: o.icon || 'info', role: 'alertdialog',
      body: '<p>' + esc(message) + '</p>',
      buttons: '<button type="submit" class="mat-btn mat-btn--primary" value="ok" autofocus>' + esc(o.ok || label('data-dlg-ok', 'OK')) + '</button>',
      result: function () { return undefined; }
    });
  }

  function promptDialog(message, options) {
    var o = options || {};
    var id = nextId('mat-in');
    return show({
      title: o.title || label('data-dlg-input', 'Input'), icon: 'edit',
      body: '<div class="mat-field"><label class="mat-label" for="' + id + '">' + esc(message) + '</label>' +
            '<input class="mat-input" id="' + id + '" name="value" value="' + esc(o.value || '') + '" ' + (o.placeholder ? 'placeholder="' + esc(o.placeholder) + '"' : '') + ' autocomplete="off" autofocus></div>',
      buttons: '<button type="submit" class="mat-btn mat-btn--primary" value="ok">' + esc(o.ok || label('data-dlg-ok', 'OK')) + '</button>' +
               '<button type="button" class="mat-btn mat-btn--secondary" data-dialog-close>' + esc(o.cancel || label('data-dlg-cancel', 'Cancel')) + '</button>',
      result: function (d, v) { var i = $('input[name=value]', d); return v === 'ok' && i ? i.value : null; }
    });
  }

  // data-confirm="..." on a button, a link or a form
  var confirmed = new WeakSet();
  function guardConfirm(e, el, proceed) {
    if (confirmed.has(el)) { return; }
    var msg = el.getAttribute('data-confirm');
    if (!msg) { return; }
    e.preventDefault();
    e.stopPropagation();
    var danger = el.hasAttribute('data-confirm-danger') || el.classList.contains('mat-btn--danger');
    confirmDialog(msg, { danger: danger, ok: el.getAttribute('data-confirm-ok') || undefined, title: el.getAttribute('data-confirm-title') || undefined }).then(function (ok) {
      if (!ok) { return; }
      // proceed() dispatches synchronously, so the mark only has to live that long: the next activation asks again.
      confirmed.add(el);
      try { proceed(); } finally { confirmed.delete(el); }
    });
  }

  // ----------------------------------------------------------------------------------------------------- toasts
  function toast(message, options) {
    var o = options || {};
    var host = $('.mat-toasts');
    if (!host) { host = doc.createElement('div'); host.className = 'mat-toasts'; host.setAttribute('role', 'status'); host.setAttribute('aria-live', 'polite'); doc.body.appendChild(host); }
    var t = doc.createElement('div');
    t.className = 'mat-toast' + (o.tone ? ' mat-toast--' + o.tone : '');
    t.innerHTML = icon(o.tone === 'danger' ? 'x-circle' : o.tone === 'success' ? 'check-circle' : o.tone === 'warning' ? 'alert' : 'info') +
      '<div class="mat-alert__body">' + esc(message) + '</div>' +
      '<button type="button" class="mat-alert__close" aria-label="' + esc(label('data-dlg-close', 'Close')) + '">' + icon('x') + '</button>';
    host.appendChild(t);
    function remove() { if (t.parentNode) { t.parentNode.removeChild(t); } }
    $('.mat-alert__close', t).addEventListener('click', remove);
    setTimeout(remove, o.timeout || 6000);
  }

  // ----------------------------------------------------------------------------------------- dependent form fields
  function controllerValue(ctl) {
    if (!ctl) { return ''; }
    if (ctl.type === 'checkbox') { return ctl.checked ? 'true' : 'false'; }
    if (ctl.type === 'radio') { var on = $('input[name="' + ctl.name + '"]:checked'); return on ? on.value : ''; }
    return ctl.value;
  }

  function findController(spec, scope) {
    if (!spec) { return null; }
    if (spec.charAt(0) === '#' || spec.charAt(0) === '.' || spec.charAt(0) === '[') { return $(spec, scope) || $(spec); }
    return $('[name="' + spec + '"]', scope) || $('[name="' + spec + '"]');
  }

  function refreshDependents(scope) {
    $$('[data-show-if]', scope).forEach(function (el) {
      var ctl = findController(el.getAttribute('data-show-if'), el.closest('form') || undefined);
      if (!ctl) { return; }
      var value = controllerValue(ctl);
      var want = el.getAttribute('data-show-value');
      var show;
      if (want === null || want === '*') { show = value !== '' && value !== 'false'; } else { show = want.split(',').map(function (s) { return s.trim(); }).indexOf(value) !== -1; }
      el.hidden = !show;
      // a hidden field posts nothing and does not block the form
      $$('input, select, textarea', el).forEach(function (i) { i.disabled = !show; });
    });
  }

  // ---------------------------------------------------------------------------------------------------- validation
  // Browser bubbles are off (novalidate): the message goes to the reserved .mat-error line of the field.
  function fieldOf(input) { return input.closest('.mat-field'); }
  function showError(input, msg) {
    var f = fieldOf(input);
    if (!f) { return; }
    f.classList.toggle('is-invalid', !!msg);
    var e = $('.mat-error', f);
    if (e) { e.textContent = msg || ''; }
    input.setAttribute('aria-invalid', msg ? 'true' : 'false');
  }
  function messageFor(input) {
    var v = input.validity;
    if (v.valueMissing) { return input.getAttribute('data-msg-required') || label('data-msg-required', 'Please fill in this field.'); }
    if (v.typeMismatch) { return input.getAttribute('data-msg-type') || label('data-msg-type', 'Please enter a valid value.'); }
    if (v.patternMismatch) { return input.getAttribute('data-msg-pattern') || input.title || label('data-msg-pattern', 'The format is not valid.'); }
    if (v.tooShort || v.tooLong || v.rangeUnderflow || v.rangeOverflow) { return input.getAttribute('data-msg-range') || input.validationMessage; }
    return input.validationMessage;
  }
  function labelOf(input) {
    var l = input.id ? $('label[for="' + input.id.replace(/"/g, '\\"') + '"]', doc) : null;
    var f = fieldOf(input);
    var text = l ? l.textContent : (f && $('legend, .mat-label', f) ? $('legend, .mat-label', f).textContent : input.name);
    return (text || '').replace(/\*/g, '').trim();
  }
  // The summary at the top of a form (FORM-03): an alert with a link to every field that has an error. Server-rendered markup of the same shape works without the script.
  function summarize(form, bad) {
    var box = $('[data-validation-summary]', form);
    if (!box) { return; }
    var list = $('.mat-alert__list', box);
    if (list) {
      list.textContent = '';
      bad.forEach(function (i) {
        var li = doc.createElement('li');
        var a = doc.createElement('a');
        a.href = '#' + i.id;
        a.textContent = labelOf(i) + ': ' + messageFor(i);
        li.appendChild(a);
        list.appendChild(li);
      });
    }
    box.hidden = bad.length === 0;
  }
  doc.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form.matches || !form.matches('form.mat-form, form[data-validate]') || e.defaultPrevented) { return; }
    var bad = $$('input, select, textarea', form).filter(function (i) { return !i.disabled && !i.checkValidity(); });
    $$('input, select, textarea', form).forEach(function (i) { if (!bad.includes(i)) { showError(i, ''); } });
    // a group of radio buttons has one message
    bad = bad.filter(function (i, n) { return i.type !== 'radio' || !i.name || bad.findIndex(function (o) { return o.type === 'radio' && o.name === i.name; }) === n; });
    summarize(form, bad);
    if (bad.length) {
      e.preventDefault();
      bad.forEach(function (i) { showError(i, messageFor(i)); });
      bad[0].focus();
    }
  }, true);
  doc.addEventListener('input', function (e) { var i = e.target; if (i.closest && i.closest('.mat-field.is-invalid') && i.checkValidity()) { showError(i, ''); } });

  // ------------------------------------------------------------------------------------------------ form helpers (matui.forms.css)
  // range: the filled part of the track and an <output> (data-output="id"); counter: "12 / 80" (data-counter="id"); indeterminate checks;
  // password: the eye button (data-password-toggle="id"); dropzone: dragging files onto a label that holds a file input.
  function updateRange(input) {
    var min = parseFloat(input.min) || 0, max = isNaN(parseFloat(input.max)) ? 100 : parseFloat(input.max);
    input.style.setProperty('--mat-range', (max === min ? 0 : ((parseFloat(input.value) - min) / (max - min)) * 100) + '%');
    var out = input.getAttribute('data-output') && doc.getElementById(input.getAttribute('data-output'));
    if (out) { out.textContent = input.value; }
  }
  function updateCounter(input) {
    var out = doc.getElementById(input.getAttribute('data-counter'));
    if (out) { out.textContent = input.value.length + (input.maxLength > 0 ? ' / ' + input.maxLength : ''); }
  }
  function updateFiles(input) {
    var zone = input.closest('.mat-dropzone');
    var list = zone && $('.mat-dropzone__files', zone);
    if (list) { list.textContent = Array.prototype.map.call(input.files || [], function (f) { return f.name; }).join(', '); }
  }
  function initForms(scope) {
    $$('input[data-indeterminate]', scope).forEach(function (i) { i.indeterminate = true; });
    $$('input.mat-range', scope).forEach(updateRange);
    $$('[data-counter]', scope).forEach(updateCounter);
    $$('[data-selectable]', scope).forEach(updateSelection);
  }
  doc.addEventListener('input', function (e) {
    var t = e.target;
    if (t.matches && t.matches('input.mat-range')) { updateRange(t); }
    if (t.matches && t.matches('[data-counter]')) { updateCounter(t); }
  });
  doc.addEventListener('change', function (e) {
    var t = e.target;
    if (t.matches && t.matches('.mat-dropzone input[type=file]')) { updateFiles(t); }
  });
  ['dragenter', 'dragover'].forEach(function (name) {
    doc.addEventListener(name, function (e) { var z = e.target.closest && e.target.closest('.mat-dropzone'); if (z) { e.preventDefault(); z.classList.add('is-drag'); } });
  });
  ['dragleave', 'drop'].forEach(function (name) {
    doc.addEventListener(name, function (e) {
      var z = e.target.closest && e.target.closest('.mat-dropzone');
      if (!z) { return; }
      e.preventDefault();
      z.classList.remove('is-drag');
      if (name === 'drop' && e.dataTransfer && e.dataTransfer.files.length) {
        var input = $('input[type=file]', z);
        if (input) { input.files = e.dataTransfer.files; input.dispatchEvent(new Event('change', { bubbles: true })); }
      }
    });
  });

  // ------------------------------------------------------------------------------------------------ selectable rows of a table
  // An element with data-selectable holds a table with a checkbox per row (data-select-row), one in the header (data-select-all) and a bulk bar
  // ([data-bulk-bar] with [data-bulk-count]) that is shown only while rows are selected. The row of a selected check is marked (is-selected).
  function updateSelection(box) {
    var rows = $$('input[data-select-row]', box);
    var all = $('input[data-select-all]', box);
    var selected = rows.filter(function (r) { return r.checked; }).length;
    rows.forEach(function (r) { var tr = r.closest('tr'); if (tr) { tr.classList.toggle('is-selected', r.checked); } });
    if (all) { all.checked = rows.length > 0 && selected === rows.length; all.indeterminate = selected > 0 && selected < rows.length; }
    var bar = $('[data-bulk-bar]', box);
    if (bar) {
      bar.hidden = selected === 0;
      var count = $('[data-bulk-count]', bar);
      if (count) { count.textContent = String(selected); }
    }
  }
  doc.addEventListener('change', function (e) {
    var t = e.target;
    var box = t.closest && t.closest('[data-selectable]');
    if (!box || !t.matches) { return; }
    if (t.matches('input[data-select-all]')) {
      $$('input[data-select-row]', box).forEach(function (r) { r.checked = t.checked; });
      updateSelection(box);
    } else if (t.matches('input[data-select-row]')) {
      updateSelection(box);
    }
  });

  // ------------------------------------------------------------------------------------------------------- tabs
  function selectTab(tab, focus) {
    var group = tab.closest('[data-tabs]');
    if (!group) { return; }
    $$('[role=tab]', group).forEach(function (t) {
      var on = t === tab;
      t.setAttribute('aria-selected', on ? 'true' : 'false');
      t.tabIndex = on ? 0 : -1;
      var panel = doc.getElementById(t.getAttribute('aria-controls'));
      if (panel) { panel.hidden = !on; }
    });
    if (focus) { tab.focus(); }
  }

  // --------------------------------------------------------------------------------------------- list / gallery view
  function setView(btn) {
    var scope = btn.closest('[data-view-scope]') || doc;
    var view = btn.getAttribute('data-view-switch');
    $$('[data-view-switch]', scope).forEach(function (b) { b.setAttribute('aria-pressed', b === btn ? 'true' : 'false'); });
    $$('[data-view]', scope).forEach(function (v) { v.hidden = v.getAttribute('data-view') !== view; });
    var key = btn.getAttribute('data-view-key');
    if (key) { try { localStorage.setItem('mat-view-' + key, view); } catch (e) { /* ignore */ } }
  }

  // ---------------------------------------------------------------------------------------------------- clipboard
  function copy(text, el) {
    function done() { if (!el) { return; } el.classList.add('is-done'); var old = el.getAttribute('data-tip'); el.setAttribute('data-tip', label('data-copied', 'Copied')); setTimeout(function () { el.classList.remove('is-done'); if (old) { el.setAttribute('data-tip', old); } else { el.removeAttribute('data-tip'); } }, 1400); }
    if (navigator.clipboard && window.isSecureContext) { navigator.clipboard.writeText(text).then(done); return; }
    var ta = doc.createElement('textarea'); ta.value = text; ta.style.position = 'fixed'; ta.style.opacity = '0'; doc.body.appendChild(ta); ta.select();
    try { doc.execCommand('copy'); done(); } catch (e) { /* ignore */ }
    ta.remove();
  }

  // ----------------------------------------------------------------------------------------- delegated click handling
  doc.addEventListener('click', function (e) {
    var t = e.target;
    if (!t.closest) { return; }

    var themeBtn = t.closest('[data-theme-toggle]');
    if (themeBtn) { cycleTheme(themeBtn); return; }
    var themeSet = t.closest('[data-theme-set]');
    if (themeSet) { chooseTheme(themeSet, themeSet.getAttribute('data-theme-set')); return; }

    var branchBtn = t.closest('[data-nav-branch]');
    if (branchBtn) { setBranch(branchBtn, branchBtn.getAttribute('aria-expanded') !== 'true', true); return; }

    var navToggle = t.closest('[data-nav-toggle]');
    if (navToggle) { setNav(!(app() && app().classList.contains('is-nav-open')), navToggle); return; }
    // a link in the drawer closes it; a parent of a submenu only opens its children
    if (t.closest('[data-nav-close]') || t.classList.contains('mat-backdrop') || (t.closest('.mat-nav__item:not([data-nav-branch])') && app() && app().classList.contains('is-nav-open'))) { setNav(false); }

    var menuBtn = t.closest('[data-menu-toggle]');
    if (menuBtn) { e.preventDefault(); toggleMenu(menuBtn); return; }
    if (openMenu && !t.closest('.mat-menu')) { closeMenu(); }
    else if (openMenu && t.closest('.mat-menu__item')) { setTimeout(closeMenu, 0); }

    var setBtn = t.closest('[data-nav-set]');
    if (setBtn) { setNavSet(setBtn.getAttribute('data-nav-set'), setBtn.closest('.mat-menu')); return; }

    // a link to a section of the page inside a dialog (the index on a phone) closes the dialog and lets the browser jump
    var jump = t.closest('dialog a[href^="#"]');
    if (jump) { closeDialog(jump.closest('dialog'), 'jump'); setTimeout(scheduleSpy, 50); return; }

    var opener = t.closest('[data-dialog-open]');
    if (opener) { e.preventDefault(); openDialog(opener.getAttribute('data-dialog-open'), opener); return; }
    var closer = t.closest('[data-dialog-close]');
    if (closer) { var d = closer.closest('dialog'); if (d) { e.preventDefault(); closeDialog(d, 'cancel'); } return; }

    var cf = t.closest('[data-confirm]');
    if (cf && !cf.matches('form')) {
      guardConfirm(e, cf, function () {
        if (cf.tagName === 'A') { window.location.href = cf.href; }
        else if (cf.form && cf.type === 'submit') { cf.form.requestSubmit(cf); }
        else { cf.click(); }
      });
      if (e.defaultPrevented) { return; }
    }

    var pw = t.closest('[data-password-toggle]');
    if (pw) {
      var field = doc.getElementById(pw.getAttribute('data-password-toggle'));
      if (field) {
        var reveal = field.type === 'password';
        field.type = reveal ? 'text' : 'password';
        pw.setAttribute('aria-pressed', reveal ? 'true' : 'false');
      }
      return;
    }

    // a link of the summary of a form moves the focus to the field instead of only scrolling
    var summaryLink = t.closest('[data-validation-summary] a[href^="#"]');
    if (summaryLink) {
      var target = doc.getElementById(summaryLink.getAttribute('href').slice(1));
      if (target) { e.preventDefault(); target.focus(); }
      return;
    }

    var cp = t.closest('[data-copy]');
    if (cp) {
      var spec = cp.getAttribute('data-copy');
      var src = spec.charAt(0) === '#' ? $(spec) : null;
      copy(src ? (src.value !== undefined && src.tagName !== 'DIV' ? src.value : src.textContent.trim()) : spec, cp);
      return;
    }

    var tab = t.closest('[role=tab]');
    if (tab && tab.closest('[data-tabs]')) { selectTab(tab); return; }

    var dismiss = t.closest('[data-dismiss]');
    if (dismiss) { var al = dismiss.closest('.mat-alert, .mat-toast'); if (al) { al.remove(); } return; }

    var view = t.closest('[data-view-switch]');
    if (view) { setView(view); }
  });

  doc.addEventListener('submit', function (e) {
    var form = e.target;
    if (form.matches && form.matches('form[data-confirm]')) { guardConfirm(e, form, function () { form.requestSubmit(); }); }
  }, true);

  doc.addEventListener('change', function (e) {
    if (e.target.closest && e.target.closest('form, body')) { refreshDependents(e.target.closest('form') || undefined); }
  });

  // ---------------------------------------------------------------------------------------------------- keyboard
  doc.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
      if (openMenu) { openMenu.restoreFocus = true; closeMenu(); return; }
      var shell = app();
      if (shell && shell.classList.contains('is-nav-open')) { setNav(false); }
      return;
    }

    if (openMenu && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      var items = $$('.mat-menu__item', openMenu.menu);
      var i = items.indexOf(doc.activeElement);
      e.preventDefault();
      items[(i + (e.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length].focus();
      return;
    }

    if (!openMenu && e.target.closest && navKeys(e)) { return; }

    var tab = e.target.closest && e.target.closest('[role=tab]');
    if (tab && (e.key === 'ArrowRight' || e.key === 'ArrowLeft' || e.key === 'Home' || e.key === 'End')) {
      var tabs = $$('[role=tab]', tab.closest('[data-tabs]'));
      var idx = tabs.indexOf(tab);
      var to = e.key === 'Home' ? 0 : e.key === 'End' ? tabs.length - 1 : (idx + (e.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
      e.preventDefault();
      selectTab(tabs[to], true);
      return;
    }

    // shortcuts such as "/" for the search dialog; never while typing
    var tag = (e.target.tagName || '').toLowerCase();
    if (e.ctrlKey || e.metaKey || e.altKey || tag === 'input' || tag === 'textarea' || tag === 'select' || e.target.isContentEditable) { return; }
    var sc = $$('[data-shortcut]').filter(function (el) { return el.getAttribute('data-shortcut') === e.key; })[0];
    if (sc && !sc.disabled) { e.preventDefault(); sc.click(); }
  });

  window.addEventListener('resize', function () { closeMenu(); scheduleSpy(); });
  doc.addEventListener('scroll', function (e) { if (openMenu && !e.target.closest) { closeMenu(); } scheduleSpy(); }, true);
  window.addEventListener('hashchange', scheduleSpy);

  // ------------------------------------------------------------------------------------------------------- init
  // a meter is filled by data: <div class="mat-meter__fill" data-value="63"> (no inline style in the markup)
  function fillMeters(scope) {
    $$(".mat-meter__fill[data-value]", scope).forEach(function (m) { m.style.setProperty("--mat-meter", Math.max(0, Math.min(100, parseFloat(m.getAttribute("data-value")) || 0)) + "%"); });
  }

  function init() {
    scheduleSpy();
    initForms();
    // the saved state of the submenus is applied without animating them
    root.classList.add('mat-nav-init');
    restoreBranches();
    window.requestAnimationFrame(function () { window.requestAnimationFrame(function () { root.classList.remove('mat-nav-init'); }); });
    fillMeters();
    applyTheme(currentTheme(), false);
    refreshDependents();
    var custom = root.getAttribute('data-custom-accent');
    if (custom) { customAccent(custom); }
    $$('[data-toast]').forEach(function (el) { toast(el.getAttribute('data-toast'), { tone: el.getAttribute('data-tone') || undefined }); el.remove(); });
    $$('[data-view-key]').forEach(function (b) {
      var saved = null;
      try { saved = localStorage.getItem('mat-view-' + b.getAttribute('data-view-key')); } catch (e) { /* ignore */ }
      if (saved && b.getAttribute('data-view-switch') === saved) { setView(b); }
    });
    $$('dialog[data-autoopen]').forEach(function (d) { openDialog(d); });
    if (!doc.querySelector('.mat-backdrop') && app()) {
      var bd = doc.createElement('div'); bd.className = 'mat-backdrop'; bd.setAttribute('data-nav-close', ''); app().appendChild(bd);
    }
  }

  if (doc.readyState === 'loading') { doc.addEventListener('DOMContentLoaded', init); } else { init(); }

  window.MatUi = {
    version: '1',
    dialog: { open: openDialog, close: closeDialog },
    confirm: confirmDialog,
    alert: alertDialog,
    prompt: promptDialog,
    toast: toast,
    theme: { get: currentTheme, set: function (t) { applyTheme(t); } },
    accent: { set: setAccent, custom: customAccent },
    scrollspy: { update: updateSpy },
    nav: { set: setNavSet, branch: function (key, open) { var b = $('[data-nav-branch="' + key + '"]'); if (b) { setBranch(b, open !== false, true); } } },
    refresh: function (scope) { refreshDependents(scope); fillMeters(scope); initForms(scope); }
  };
})();
