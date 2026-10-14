/* The white label page of the Studio (/Studio/Releases/WhiteLabel): while a prefix and a name are typed it says what is wrong with them (the same rules as MatDesign.Core.Studio.WhiteLabel.Validate,
   which holds them again on the server) and writes what the names of the toolkit become into the table of examples. No inline script, no inline style. */
(function () {
  'use strict';
  var form = document.querySelector('[data-white-label]');
  if (!form) { return; }

  var prefixInput = form.querySelector('[data-wl-prefix]');
  var nameInput = form.querySelector('[data-wl-name]');
  var reserved = (form.getAttribute('data-reserved') || '').split(',');
  var SOURCE_NAME = 'MatUi';

  function prefixProblem(prefix) {
    if (prefix.length < 2 || prefix.length > 8) { return 'A prefix is 2 to 8 characters.'; }
    if (!/^[a-z][a-z0-9]*$/.test(prefix)) { return 'A prefix is lower-case letters and digits and starts with a letter, like acme.'; }
    if (reserved.indexOf(prefix) >= 0) { return prefix + ' is taken: it is the start of attributes and state classes the browser and the toolkit use (data-, aria-, is-, has-, ...).'; }
    if (prefix.indexOf('xml') === 0) { return 'A prefix cannot start with xml: that is reserved for XML.'; }
    if (prefix === 'matui') { return 'matui is the lower-case name of the toolkit: choose your own prefix.'; }
    return '';
  }

  function nameProblem(name, prefix) {
    if (name.length < 3 || name.length > 24) { return 'A name is 3 to 24 letters.'; }
    if (!/^[A-Z][A-Za-z]*$/.test(name)) { return 'A name is letters only and starts with a capital letter, like AcmeUi.'; }
    if (name === SOURCE_NAME && prefix !== 'mat') { return 'MatUi is the name of the toolkit itself: give the product your own name (or keep the prefix mat, which renames nothing).'; }
    if (name !== SOURCE_NAME && (name.toLowerCase() === 'matui' || name.toLowerCase() === 'mat')) { return 'That name only differs from the toolkit\'s own in its capitals, or is its first three letters: it would be taken for it.'; }
    return '';
  }

  function show(input, message) {
    // a custom validity keeps the toolkit's own check (FORM-05) from clearing the message when it sees the typing, and stops the submit with the same words
    input.setCustomValidity(message);
    var field = input.closest('.mat-field');
    var error = field && field.querySelector('.mat-error');
    if (error) { error.textContent = message; }
    if (field) { field.classList.toggle('is-invalid', message.length > 0); }
    if (message) { input.setAttribute('aria-invalid', 'true'); } else { input.removeAttribute('aria-invalid'); }
  }

  // the examples are the same fixed names the server writes: the rules, applied to each
  function rename(text, prefix, name) {
    return text
      .replace(/(^|[^A-Za-z0-9_$-])--mat-/g, '$1--' + prefix + '-')
      .replace(/(^|[^A-Za-z0-9_$-])data-mat-/g, '$1data-' + prefix + '-')
      .replace(/(^|[^A-Za-z0-9_$-])mat-/g, '$1' + prefix + '-')
      .replace(/(^|[^A-Za-z0-9_$-])mat:/g, '$1' + prefix + ':')
      .replace(/(^|[^A-Za-z0-9_$])MatUi(?![A-Za-z0-9_])/g, '$1' + name)
      .replace(/(^|[^A-Za-z0-9_$-])matui(?![A-Za-z0-9_])/g, '$1' + name.toLowerCase());
  }

  function update(touched) {
    var prefix = prefixInput.value.trim().toLowerCase();
    var name = nameInput.value.trim();
    var prefixMessage = prefixProblem(prefix);
    var nameMessage = nameProblem(name, prefix);
    if (touched || prefixInput.dataset.touched) { show(prefixInput, prefixMessage); }
    if (touched || nameInput.dataset.touched) { show(nameInput, nameMessage); }
    var ok = !prefixMessage && !nameMessage;
    form.querySelectorAll('[data-wl-after]').forEach(function (cell) {
      var before = cell.getAttribute('data-wl-after');
      cell.textContent = ok ? rename(before, prefix, name) : before;
    });
  }

  prefixInput.addEventListener('input', function () { prefixInput.dataset.touched = '1'; update(false); });
  nameInput.addEventListener('input', function () { nameInput.dataset.touched = '1'; update(false); });
  update(false);
})();
