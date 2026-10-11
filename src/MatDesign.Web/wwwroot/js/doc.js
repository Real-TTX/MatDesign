/* The "Try it" buttons of the component pages. They call the public API of MatUi (window.MatUi); the markup of an example never carries these attributes. */
(function () {
  'use strict';

  async function run(demo) {
    var ui = window.MatUi;
    if (!ui) { return; }
    if (demo === 'toast') {
      ui.toast('Saved.', { tone: 'success' });
      ui.toast('Almost full: 92 % of the storage is used.', { tone: 'warning' });
      ui.toast('The mail server did not answer.', { tone: 'danger' });
      ui.toast('The backup is running.');
    } else if (demo === 'dialogs') {
      var ok = await ui.confirm('Restart the service?', { ok: 'Restart' });
      if (!ok) { return; }
      await ui.alert('The backup is running.', { title: 'Backup' });
      var name = await ui.prompt('Name of the folder', { value: 'New folder' });
      if (name !== null) { ui.toast('Created: ' + name, { tone: 'success' }); }
    }
  }

  // A control that applies its form as soon as it changes (the builder: a colour, an app).
  document.addEventListener('change', function (e) {
    var control = e.target.closest('[data-autosubmit]');
    if (control && control.form) { control.form.requestSubmit(); }
  });

  // Where the code of the examples is shown (beside them on a wide window, or always below): remembered in this browser.
  var KEY = 'matdesign.code';
  function showCode(where) {
    if (where === 'below') { document.documentElement.setAttribute('data-doc-code', 'below'); } else { document.documentElement.removeAttribute('data-doc-code'); }
    document.querySelectorAll('[data-doc-code]').forEach(function (b) { if (b.tagName === 'BUTTON') { b.setAttribute('aria-pressed', b.getAttribute('data-doc-code') === (where === 'below' ? 'below' : 'beside') ? 'true' : 'false'); } });
  }
  try { showCode(localStorage.getItem(KEY)); } catch (e) { showCode(null); }

  // "In a page": the frame of a group shows a page of the sample app; a press on a component outlines it there. The page is loaded when the panel is opened.
  function urlOf(view) { return '/Preview?app=matdesign&view=' + view; }

  // Loads a view into the frame (nothing happens when it is the one that is shown already) and calls then() when it is there.
  function go(frame, view, then) {
    if (frame.getAttribute('data-view') === view && frame.getAttribute('data-loaded') === 'true') { if (then) { then(); } return; }
    frame.setAttribute('data-view', view);
    frame.setAttribute('data-loaded', 'false');
    frame.onload = function () { frame.setAttribute('data-loaded', 'true'); if (then) { then(); } };
    frame.src = urlOf(view);
  }

  function outline(panel, button) {
    var frame = panel.querySelector('iframe');
    var doc = frame.contentDocument;
    if (!doc) { return; }
    var old = doc.getElementById('doc-mark');
    if (old) { old.remove(); }
    doc.querySelectorAll('dialog[open]').forEach(function (d) { d.close(); });
    if (!button) { return; }
    var open = button.getAttribute('data-context-open');
    if (open) { var trigger = doc.querySelector(open); if (trigger) { trigger.click(); } }
    var selector = button.getAttribute('data-context-mark');
    var style = doc.createElement('style');
    style.id = 'doc-mark';
    style.textContent = selector + ' { outline: 3px solid var(--mat-accent) !important; outline-offset: -3px; }';
    doc.head.appendChild(style);
    // bring the first one into view in the page of the frame (not scrollIntoView, which would move this page too)
    var first = doc.querySelector(selector);
    var scroller = doc.querySelector('.mat-main');
    if (first && scroller && !first.closest('.mat-nav, .mat-topbar, dialog')) {
      scroller.scrollTop += first.getBoundingClientRect().top - scroller.getBoundingClientRect().top - 90;
    }
  }

  function mark(panel, button) {
    var frame = panel.querySelector('iframe');
    var on = button.getAttribute('aria-pressed') !== 'true';
    panel.querySelectorAll('[data-context-mark]').forEach(function (b) { b.setAttribute('aria-pressed', 'false'); });
    if (!on) { outline(panel, null); return; }
    button.setAttribute('aria-pressed', 'true');
    var view = button.getAttribute('data-context-view');
    go(frame, view, function () { outline(panel, button); });
    panel.querySelector('[data-context-open-page]').setAttribute('href', urlOf(view));
  }

  document.addEventListener('toggle', function (e) {
    var panel = e.target;
    if (panel.matches && panel.matches('details[data-context]') && panel.open) {
      var frame = panel.querySelector('iframe');
      if (!frame.getAttribute('src')) { go(frame, panel.getAttribute('data-context-view')); }
    }
  }, true);

  document.addEventListener('click', function (e) {
    var button = e.target.closest('[data-doc-demo]');
    if (button) { run(button.getAttribute('data-doc-demo')); return; }

    var show = e.target.closest('[data-context-show]');
    if (show) {
      var panel = document.getElementById('context-' + show.getAttribute('data-context-show'));
      var target = panel && panel.querySelector('button[data-context-mark][data-context-component="' + show.getAttribute('data-context-component') + '"]');
      if (panel && target) {
        panel.open = true;
        panel.scrollIntoView({ block: 'start', behavior: 'smooth' });
        target.setAttribute('aria-pressed', 'false');
        mark(panel, target);
      }
      return;
    }

    var markButton = e.target.closest('button[data-context-mark]');
    if (markButton) { mark(markButton.closest('details[data-context]'), markButton); return; }

    var size = e.target.closest('button[data-context-size]');
    if (size) {
      var area = size.closest('details[data-context]');
      area.querySelectorAll('[data-context-size]').forEach(function (b) { b.setAttribute('aria-pressed', b === size ? 'true' : 'false'); });
      area.querySelector('iframe').classList.toggle('doc-frame--phone', size.getAttribute('data-context-size') === 'phone');
      return;
    }

    var more = e.target.closest('[data-doc-api-more]');
    if (more) {
      var list = more.closest('dd');
      var open = list.classList.toggle('is-open');
      more.setAttribute('aria-expanded', open ? 'true' : 'false');
      more.textContent = open ? 'fewer' : more.getAttribute('data-more');
      return;
    }

    var where = e.target.closest('button[data-doc-code]');
    if (where) {
      showCode(where.getAttribute('data-doc-code'));
      try { localStorage.setItem(KEY, where.getAttribute('data-doc-code')); } catch (err) { /* private mode: the choice just is not kept */ }
    }
  });
})();
