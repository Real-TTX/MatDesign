/* The editors of the brand and of the overrides of an app (docs/STUDIO.md, "Brand" and stage 12). The same script serves four kinds of pages, and does only these things while a value is typed:
   - keeps a colour picker and the text field next to it in step (a colour token has both);
   - applies the unsaved values to the previews [data-brand-preview] through the CSSOM (style.setProperty): the page itself never changes and no markup carries a style.
     A preview without data-prop-group is the sample of the brand page: it gets every token (the typed one, else the toolkit's own) in the mode it shows. A preview with data-prop-group="btn"
     is the real control of the documentation: it gets the component properties of that group, and the tokens that were typed on the page (an app's overrides), and it follows the mode of the page;
   - checks a component property against its type and limits (data-kind, data-min, data-max) and tells that a height under 44 px is for a mouse only. The preview does what the written CSS does: a touch
     screen (no fine pointer) ignores a touch-target height under 44 px;
   - works out the contrast of the pairings that do not depend on the accent (the list of pairings comes from the server as data-pairings, the same list the checks use) and fills the table and the summary.
     The server checks again on Check and on Save, with the accent of every app. */
(function () {
  'use strict';

  var root = document.querySelector('[data-brand-editor]');
  if (!root) { return; }

  var pairings = JSON.parse(root.getAttribute('data-pairings') || '[]');
  var accent = JSON.parse(root.getAttribute('data-accent') || '{}');
  var previews = [].slice.call(root.querySelectorAll('[data-brand-preview]'));
  var summary = root.querySelector('[data-brand-summary]');
  var mode = 'light';
  var applied = new Map();
  var TOUCH_PX = 44;

  // a pointer that can hit a small target: the same question the written CSS asks with (pointer: fine)
  var finePointer = !window.matchMedia || window.matchMedia('(pointer: fine)').matches;

  // ---- colours -------------------------------------------------------------------------------------------------------------------------------

  var HEX = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i;
  var RGB = /^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*(\d*\.?\d+%?)\s*)?\)$/i;

  function parseColour(text) {
    var t = String(text || '').trim();
    var m;
    if (HEX.test(t)) {
      var h = t.slice(1);
      if (h.length === 3) { h = h.split('').map(function (c) { return c + c; }).join(''); }
      return { r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16), a: 1 };
    }
    m = RGB.exec(t);
    if (!m) { return null; }
    var r = +m[1], g = +m[2], b = +m[3];
    var a = 1;
    if (m[4] !== undefined) { a = m[4].slice(-1) === '%' ? parseFloat(m[4]) / 100 : parseFloat(m[4]); }
    if (r > 255 || g > 255 || b > 255 || a < 0 || a > 1) { return null; }
    return { r: r, g: g, b: b, a: a };
  }

  function hex2(n) { var s = Math.round(n).toString(16); return s.length < 2 ? '0' + s : s; }
  function toHex(c) { return '#' + hex2(c.r) + hex2(c.g) + hex2(c.b); }

  // a colour painted over an opaque background, like the browser does it (sRGB)
  function over(fg, bg) { return { r: fg.r * fg.a + bg.r * (1 - fg.a), g: fg.g * fg.a + bg.g * (1 - fg.a), b: fg.b * fg.a + bg.b * (1 - fg.a), a: 1 }; }

  function lin(v) { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }
  function luminance(c) { return 0.2126 * lin(c.r) + 0.7152 * lin(c.g) + 0.0722 * lin(c.b); }
  function ratio(a, b) {
    var x = luminance(a), y = luminance(b);
    return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
  }

  // ---- values by their kind ---------------------------------------------------------------------------------------------------------------------

  var LENGTH = /^(-?\d*\.?\d+)(px|rem|em)$/;
  var NUMBER = /^\d*\.?\d+$/;
  var DURATION = /^(\d*\.?\d+)(ms|s)$/;
  var BEZIER = /^cubic-bezier\(\s*-?\d*\.?\d+\s*,\s*-?\d*\.?\d+\s*,\s*-?\d*\.?\d+\s*,\s*-?\d*\.?\d+\s*\)$/i;
  var EASINGS = ['ease', 'ease-in', 'ease-out', 'ease-in-out', 'linear'];
  var FONT = /^[A-Za-z0-9 ,.'"_-]+$/;

  // true when the text could be a value of the kind (the server decides about the limits); the preview ignores what is not
  function plausible(kind, text) {
    switch (kind) {
      case 'colour': return parseColour(text) !== null;
      case 'length': return text === '0' || LENGTH.test(text);
      case 'number': return NUMBER.test(text);
      case 'duration': return DURATION.test(text);
      case 'fontstack': return FONT.test(text) && text.length >= 3;
      default: return EASINGS.indexOf(text) >= 0 || BEZIER.test(text);
    }
  }

  // a length in px (rem and em count 16 px), a duration in ms, a number as it is; null for anything else
  function amount(kind, text) {
    var m;
    if (kind === 'length') {
      if (text === '0') { return 0; }
      m = LENGTH.exec(text);
      return m ? parseFloat(m[1]) * (m[2] === 'px' ? 1 : 16) : null;
    }
    if (kind === 'duration') {
      m = DURATION.exec(text);
      return m ? parseFloat(m[1]) * (m[2] === 's' ? 1000 : 1) : null;
    }
    return kind === 'number' && NUMBER.test(text) ? parseFloat(text) : null;
  }

  function trimNumber(n) { return String(Math.round(n * 100) / 100); }

  // what is wrong with a value of a component property that is a value of its kind: outside the least and the most it takes
  function rangeProblem(row, text) {
    var value = amount(row.kind, text);
    if (value === null || (row.min === null && row.max === null)) { return ''; }
    var unit = row.kind === 'length' ? ' px' : row.kind === 'duration' ? ' ms' : '';
    if (row.min !== null && value < row.min) { return trimNumber(value) + unit + ' is below the least this property takes, ' + trimNumber(row.min) + unit + '.'; }
    if (row.max !== null && value > row.max) { return trimNumber(value) + unit + ' is above the most this property takes, ' + trimNumber(row.max) + unit + '.'; }
    return '';
  }

  // ---- the rows -----------------------------------------------------------------------------------------------------------------------------------

  function number(el, name) { var v = el.getAttribute(name); return v === null || v === '' ? null : parseFloat(v); }

  function readRow(el) {
    var row = {
      el: el, name: el.getAttribute('data-token') || el.getAttribute('data-prop'), kind: el.getAttribute('data-kind'),
      light: el.getAttribute('data-light'), dark: el.getAttribute('data-dark'),
      group: el.getAttribute('data-group'), min: number(el, 'data-min'), max: number(el, 'data-max'), touch: el.getAttribute('data-touch') === 'true',
      input: {}, picker: {}
    };
    ['light', 'dark'].forEach(function (m) {
      row.input[m] = el.querySelector('[data-brand-input="' + m + '"]');
      row.picker[m] = el.querySelector('[data-brand-picker="' + m + '"]');
    });
    if (el.hasAttribute('data-prop-row')) { row.input.light = el.querySelector('[data-prop-input]'); }
    return row;
  }

  // the tokens that shape the page (the preview gets them), the tokens inside a colour scheme (only kept in order) and the properties of components
  var rows = [].slice.call(root.querySelectorAll('[data-brand-row]')).map(readRow);
  var schemeRows = [].slice.call(root.querySelectorAll('[data-brand-scheme-row]')).map(readRow);
  var propRows = [].slice.call(root.querySelectorAll('[data-prop-row]')).map(readRow);

  function defaultOf(row, m) { return m === 'dark' && row.dark ? row.dark : row.light; }

  function typedOf(row, m) { return (m === 'dark' && row.input.dark ? row.input.dark : row.input.light).value.trim(); }

  // what a mode shows: the typed value when it is plausible, else the toolkit's own; a token without a dark value has the light one in both
  function effective(m) {
    var values = {};
    rows.forEach(function (row) {
      var typed = typedOf(row, m);
      var base = m === 'dark' && row.dark ? row.dark : row.light;
      values[row.name] = typed && plausible(row.kind, typed) ? typed : base;
    });
    return values;
  }

  // only what was typed and could be a value (the tokens of an app: the rest of the page is the brand's)
  function typedValues(m) {
    var values = {};
    rows.forEach(function (row) {
      var typed = typedOf(row, m);
      if (typed && plausible(row.kind, typed)) { values[row.name] = typed; }
    });
    return values;
  }

  function syncPicker(row, m) {
    var input = row.input[m], picker = row.picker[m];
    if (!input || !picker) { return; }
    var c = parseColour(input.value.trim() || defaultOf(row, m));
    if (c) { picker.value = toHex(c); }
  }

  // the mark and the message of a field: the server's message stays until the field is edited; a message of this script goes when the value is fine
  function mark(row, input, message, edited) {
    var field = input.closest('.mat-field');
    var error = field && field.querySelector('.mat-error');
    if (edited && field) {
      field.classList.remove('is-invalid');
      if (error) { error.textContent = ''; }
    }
    // a custom validity keeps the toolkit's own handler (it clears a field that checks out) from clearing the mark, and stops a submit with the message
    input.setCustomValidity(message);
    if (message) {
      input.setAttribute('aria-invalid', 'true');
      if (field) { field.classList.add('is-invalid'); }
      if (error) { error.textContent = message; }
    } else if (edited) {
      input.removeAttribute('aria-invalid');
    }
  }

  function validate(row, m, edited) {
    var input = row.input[m];
    if (!input) { return; }
    var text = input.value.trim();
    var message = '';
    if (text !== '' && !plausible(row.kind, text)) { message = 'This is not a value of this token\'s kind.'; }
    else if (text !== '' && row.el.hasAttribute('data-prop-row')) { message = rangeProblem(row, text); }
    mark(row, input, message, edited);

    // a touch-target height under 44 px: valid, and for a mouse only (the written CSS puts it inside (pointer: fine))
    var note = row.el.querySelector('[data-prop-note]');
    if (note) {
      var px = text && !message ? amount(row.kind, text) : null;
      note.hidden = !(row.touch && px !== null && px < TOUCH_PX);
    }
  }

  // ---- the previews ---------------------------------------------------------------------------------------------------------------------------

  function setOn(preview, name, value) {
    preview.style.setProperty(name, value);
    var list = applied.get(preview) || [];
    list.push(name);
    applied.set(preview, list);
  }

  function clear(preview) {
    (applied.get(preview) || []).forEach(function (name) { preview.style.removeProperty(name); });
    applied.set(preview, []);
  }

  // the properties of one component group: what was typed, a value of the kind, within the limits; a touch target under 44 px only where a mouse is
  function applyProperties(preview, group) {
    propRows.forEach(function (row) {
      if (row.group !== group) { return; }
      var typed = row.input.light.value.trim();
      if (!typed || !plausible(row.kind, typed) || rangeProblem(row, typed)) { return; }
      if (row.touch && !finePointer && amount(row.kind, typed) < TOUCH_PX) { return; }
      setOn(preview, row.name, typed);
    });
  }

  function applyPreview(preview) {
    clear(preview);
    var group = preview.getAttribute('data-prop-group');
    if (group) {
      var typed = typedValues('light');
      Object.keys(typed).forEach(function (name) { setOn(preview, name, typed[name]); });
      applyProperties(preview, group);
      return;
    }
    var derived = accent[mode] || {};
    Object.keys(derived).forEach(function (name) { setOn(preview, name, derived[name]); });
    var values = effective(mode);
    Object.keys(values).forEach(function (name) { setOn(preview, name, values[name]); });
    setOn(preview, 'color-scheme', mode);
  }

  function applyPreviews() { previews.forEach(applyPreview); }

  // ---- contrast ----------------------------------------------------------------------------------------------------------------------------------

  function opaque(values, token, dark) {
    var c = parseColour(values[token]);
    return over(c, dark ? { r: 0, g: 0, b: 0, a: 1 } : { r: 255, g: 255, b: 255, a: 1 });
  }

  function contrastOf(pairing, values, dark) {
    var bg = pairing.over
      ? over(parseColour(values[pairing.background]), opaque(values, pairing.over, dark))
      : opaque(values, pairing.background, dark);
    var fg = over(parseColour(values[pairing.foreground]), bg);
    return ratio(fg, bg);
  }

  function updateContrast() {
    if (pairings.length === 0 || !root.querySelector('[data-brand-contrast]')) { return; }
    var failing = [];
    ['light', 'dark'].forEach(function (m) {
      var values = effective(m);
      pairings.forEach(function (pairing, i) {
        var cell = root.querySelector('[data-pairing="' + i + '"] [data-brand-result="' + m + '"]');
        if (!cell) { return; }
        var value = contrastOf(pairing, values, m === 'dark');
        var advice = pairing.advisory || pairing.advisoryUnlessContrast;
        var passes = value >= pairing.required;
        cell.querySelector('[data-ratio]').textContent = value.toFixed(2) + ':1';
        var status = cell.querySelector('[data-status]');
        status.textContent = passes ? 'Passes' : advice ? 'Below advice' : 'Fails';
        status.className = 'mat-pill ' + (passes ? 'mat-pill--ok' : advice ? 'mat-pill--warn' : 'mat-pill--error');
        if (!passes && !advice) {
          failing.push(pairing.name.replace('{mode}', m) + ': ' + value.toFixed(2) + ':1, needs ' + pairing.required.toFixed(1) + ':1');
        }
      });
    });
    if (!summary) { return; }
    summary.textContent = '';
    var p = document.createElement('p');
    var pill = document.createElement('span');
    pill.className = 'mat-pill ' + (failing.length === 0 ? 'mat-pill--ok' : 'mat-pill--error');
    pill.textContent = failing.length === 0 ? 'Passes' : 'Fails';
    p.appendChild(pill);
    p.appendChild(document.createTextNode(failing.length === 0
      ? ' Every required pairing reaches AA, in light and dark.'
      : ' ' + failing.length + (failing.length === 1 ? ' required pairing' : ' required pairings') + ' below AA:'));
    summary.appendChild(p);
    if (failing.length > 0) {
      var list = document.createElement('ul');
      list.className = 'doc-list';
      failing.slice(0, 8).forEach(function (text) { var li = document.createElement('li'); li.textContent = text; list.appendChild(li); });
      if (failing.length > 8) { var more = document.createElement('li'); more.textContent = 'and ' + (failing.length - 8) + ' more'; list.appendChild(more); }
      summary.appendChild(list);
    }
  }

  function update() {
    applyPreviews();
    updateContrast();
  }

  // ---- events ------------------------------------------------------------------------------------------------------------------------------------

  function wireTokens(row, onChange) {
    ['light', 'dark'].forEach(function (m) {
      var input = row.input[m], picker = row.picker[m];
      if (!input) { return; }
      input.addEventListener('input', function () { syncPicker(row, m); validate(row, m, true); onChange(); });
      if (picker) {
        picker.addEventListener('input', function () {
          var current = parseColour(input.value.trim() || defaultOf(row, m));
          var alpha = current ? current.a : 1;
          var c = parseColour(picker.value);
          input.value = alpha < 1 ? 'rgba(' + c.r + ', ' + c.g + ', ' + c.b + ', ' + alpha + ')' : picker.value;
          validate(row, m, true);
          onChange();
        });
      }
      syncPicker(row, m);
    });

    var reset = row.el.querySelector('[data-brand-reset]');
    if (reset) {
      reset.addEventListener('click', function () {
        ['light', 'dark'].forEach(function (m) {
          if (row.input[m]) { row.input[m].value = ''; validate(row, m, true); syncPicker(row, m); }
        });
        onChange();
      });
    }
  }

  rows.forEach(function (row) { wireTokens(row, update); });
  schemeRows.forEach(function (row) { wireTokens(row, function () {}); });

  propRows.forEach(function (row) {
    var input = row.input.light;
    input.addEventListener('input', function () { validate(row, 'light', true); applyPreviews(); });
    var reset = row.el.querySelector('[data-prop-reset]');
    if (reset) {
      reset.addEventListener('click', function () { input.value = ''; validate(row, 'light', true); applyPreviews(); });
    }
  });

  [].slice.call(root.querySelectorAll('[data-brand-mode]')).forEach(function (button) {
    button.addEventListener('click', function () {
      mode = button.getAttribute('data-brand-mode');
      [].slice.call(root.querySelectorAll('[data-brand-mode]')).forEach(function (b) { b.setAttribute('aria-pressed', b === button ? 'true' : 'false'); });
      applyPreviews();
    });
  });

  // the preview of the brand page starts in the mode the page is in
  var theme = document.documentElement.getAttribute('data-theme');
  if (theme === 'dark' || (!theme && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
    mode = 'dark';
    [].slice.call(root.querySelectorAll('[data-brand-mode]')).forEach(function (b) { b.setAttribute('aria-pressed', b.getAttribute('data-brand-mode') === 'dark' ? 'true' : 'false'); });
  }

  rows.concat(schemeRows).forEach(function (row) { ['light', 'dark'].forEach(function (m) { validate(row, m); }); });
  propRows.forEach(function (row) { validate(row, 'light'); });
  update();
})();
