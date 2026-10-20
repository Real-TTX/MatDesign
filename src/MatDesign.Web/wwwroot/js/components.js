/* The pages of the components (LIST-18). Everything here is an enhancement: without it the overview is a page of links with a GET form for the search, and the page of a component has plain
   links for the neighbours.
   - the overview (/Components): the filter works on the tiles while the person types (the same text as the server: the name, the summary and data-keys), says how many are left in the live region of the toolbar,
     shows the empty state and keeps the search in the address (replaceState: the entry of the overview has it when the browser goes back); an old address of a component (the overview with the id of the component as its hash) is sent on
     to the page of the component; a group the filter leaves empty is hidden with its link in the index;
   - the page of a component (/Components/{id}): the arrow keys walk to the previous and the next component, a click on them replaces the entry of the history instead of adding one, so that
     "All components" is one step back to the overview, which the browser shows as it was left (the search, the place on the page). */
(function () {
  'use strict';

  var OVERVIEW = '/components';

  function fold(text) { return String(text).normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase(); }
  function all(selector, scope) { return Array.prototype.slice.call((scope || document).querySelectorAll(selector)); }
  function plainClick(e) { return !e.defaultPrevented && e.button === 0 && !e.metaKey && !e.ctrlKey && !e.shiftKey && !e.altKey; }

  // ---------------------------------------------------------------------------------------------------- the overview
  var root = document.querySelector('[data-components]');
  if (root) {
    var tiles = all('[data-component]', root);
    var groups = all('[data-components-group]', root);
    var input = document.getElementById('cmp-q');
    var form = document.getElementById('cmp-form');
    var info = root.querySelector('.mat-toolbar__info');
    var empty = root.querySelector('[data-components-empty]');
    var wanted = root.getAttribute('data-group') || '';
    var urlTimer = 0;

    // what the server looks at (ComponentOverview.SearchText): the name, the summary and the keys (group, id, ids of the rules), folded; kept on the tile
    var haystack = function (tile) {
      if (tile._hay === undefined) {
        var name = tile.querySelector('.doc-tile__name'), summary = tile.querySelector('.doc-tile__summary');
        tile._hay = fold([name ? name.textContent : '', summary ? summary.textContent : '', tile.getAttribute('data-keys') || ''].join(' '));
      }
      return tile._hay;
    };

    var apply = function () {
      var words = fold(input ? input.value : '').split(/\s+/).filter(Boolean);
      var shown = 0;
      tiles.forEach(function (tile) {
        var section = tile.closest('[data-components-group]');
        var hay = haystack(tile);
        var match = (!wanted || (section && section.getAttribute('data-components-group') === wanted)) && words.every(function (w) { return hay.indexOf(w) >= 0; });
        tile.hidden = !match;
        if (match) { shown += 1; }
      });
      groups.forEach(function (section) {
        var any = !!section.querySelector('[data-component]:not([hidden])');
        section.hidden = !any;
        var anchor = section.id;
        all('[data-components-index="' + anchor + '"]').forEach(function (item) {
          item.hidden = !any;
          var link = item.querySelector('a');
          // the index of the page is marked by the scroll spy from the position of its targets: a hidden heading has none, so its link goes with it
          if (link) { if (any) { link.setAttribute('href', '#' + anchor); } else { link.removeAttribute('href'); } }
        });
      });
      if (info) { info.textContent = shown === tiles.length ? tiles.length + ' components' : shown + ' of ' + tiles.length + ' components'; }
      if (empty) { empty.hidden = shown > 0; }
      window.clearTimeout(urlTimer);
      urlTimer = window.setTimeout(function () {
        var url = new URL(window.location.href);
        var text = input ? input.value.trim() : '';
        if (text) { url.searchParams.set('q', text); } else { url.searchParams.delete('q'); }
        try { window.history.replaceState(window.history.state, '', url.pathname + url.search + url.hash); } catch (e) { /* the address stays as it is */ }
      }, 200);
    };

    if (input) { input.addEventListener('input', apply); }
    if (form) {
      form.addEventListener('submit', function (e) {
        // the Filter dialog sends the form for real (the server draws the page with the group); Enter in the field only filters
        var dialog = document.getElementById('cmp-filter');
        if (dialog && dialog.open) { return; }
        e.preventDefault();
        apply();
      });
    }
    apply();

    // The shell scrolls its content area, not the window, so the browser does not bring the place on the page back when the entry is shown again (a step back from the page of
    // a component): the place is kept in the entry of the history itself, the way the search is kept in its address.
    var scroller = document.querySelector('.mat-main');
    if (scroller) {
      var scrollTimer = 0;
      var remember = function () {
        try { window.history.replaceState(Object.assign({}, window.history.state, { componentsScroll: scroller.scrollTop }), ''); } catch (e) { /* the place is not kept */ }
      };
      var saved = window.history.state && window.history.state.componentsScroll;
      if (saved > 0) { scroller.scrollTop = saved; }
      scroller.addEventListener('scroll', function () { window.clearTimeout(scrollTimer); scrollTimer = window.setTimeout(remember, 150); }, { passive: true });
      window.addEventListener('pagehide', remember);
    }

    // An old address is the overview with the id of a component as its hash (or the id of a heading, an example or a source of it): it is the page of that component now.
    // A hash that starts with group- stays: it is a heading of this page.
    var idOf = function (hash) {
      var id = '';
      try { id = decodeURIComponent(hash.replace(/^#/, '')); } catch (e) { return ''; }
      if (!id) { return ''; }
      var known = {};
      tiles.forEach(function (tile) { known[tile.getAttribute('data-component')] = true; });
      if (known[id]) { return id; }
      var part = /^(?:h|props)-(.+)$/.exec(id) || /^(?:ex|src)-(.+)-\d+$/.exec(id);
      return part && known[part[1]] ? part[1] : '';
    };
    var forward = function () {
      var id = idOf(window.location.hash);
      if (id) { window.location.replace('/Components/' + encodeURIComponent(id)); }
    };
    forward();
    window.addEventListener('hashchange', forward);
  }

  // ---------------------------------------------------------------------------------------------------- the page of a component
  var pager = document.querySelector('[data-components-pager]');
  if (pager) {
    var walk = function (href) { window.location.replace(href); };

    // The keys are for the page, not for a control of it: not while a field, a picker, a tree, a schedule, a menu, a dialog or a control of an example has the focus, and not
    // when the script of the toolkit has taken the key already (it runs first and says so with preventDefault).
    var HANDS_OFF = 'input, textarea, select, [contenteditable], [role="slider"], [role="tree"], [role="treeitem"], [role="grid"], [role="listbox"], [role="menu"], [role="menubar"], [role="tablist"], ' +
      '[role="radiogroup"], [role="toolbar"], [role="combobox"], [role="spinbutton"], [role="textbox"], .doc-example__live, .doc-context, .mat-picker, .mat-menu, .mat-nav, .mat-topbar, .mat-seg, ' +
      '.mat-tabs, .mat-schedule, .mat-calendar, .mat-carousel, pre, [data-pager-keys="off"]';
    var canWalk = function (e) {
      if (e.defaultPrevented || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey || e.isComposing) { return false; }
      if (document.querySelector('dialog[open]')) { return false; }
      var target = e.target;
      if (target && target.nodeType === 1 && target !== document.body && target !== document.documentElement && target.closest(HANDS_OFF)) { return false; }
      return true;
    };
    document.addEventListener('keydown', function (e) {
      if ((e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') || !canWalk(e)) { return; }
      var link = document.querySelector('[data-components-pager] a[rel="' + (e.key === 'ArrowLeft' ? 'prev' : 'next') + '"]');
      if (!link) { return; }
      e.preventDefault();
      walk(link.href);
    });
    document.addEventListener('click', function (e) {
      var link = e.target.closest && e.target.closest('[data-components-pager] a[rel]');
      if (link && plainClick(e)) { e.preventDefault(); walk(link.href); }
    });
  }

  // "All components": one step back when the entry before this one is the overview (the browser then shows it as it was left: the search, the place on the page);
  // otherwise the link is followed. The entries of the history are read from the Navigation API where there is one, else from the referrer.
  function isOverview(address) {
    try {
      var url = new URL(address, window.location.href);
      return url.origin === window.location.origin && url.pathname.replace(/\/$/, '').toLowerCase() === OVERVIEW;
    } catch (e) { return false; }
  }
  function cameFromOverview() {
    var nav = window.navigation;
    if (nav && nav.entries && nav.currentEntry && typeof nav.currentEntry.index === 'number') {
      var entries = nav.entries();
      var before = entries[nav.currentEntry.index - 1];
      return !!before && isOverview(before.url);
    }
    return window.history.length > 1 && isOverview(document.referrer || '');
  }
  document.addEventListener('click', function (e) {
    var back = e.target.closest && e.target.closest('[data-components-back]');
    if (back && plainClick(e) && cameFromOverview()) { e.preventDefault(); window.history.back(); }
  });
})();
