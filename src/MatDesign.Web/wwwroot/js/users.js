/* The people of the Studio (docs/STUDIO.md, "People and roles"): the Generate button next to a password field fills it with a random password, in the browser, so that the
   administrator can copy it (the Copy button is the toolkit's data-copy) and hand it over. Four groups of four characters from an alphabet without 0, O, 1, l and I, drawn with
   crypto.getRandomValues (about 78 bits). Without the script the administrator types a password. The password is never sent anywhere but with the form. */
(function () {
  'use strict';

  var alphabet = 'abcdefghjkmnpqrstuvwxyz23456789';

  function generate() {
    var out = [];
    var limit = 256 - (256 % alphabet.length);
    var pool = new Uint8Array(64);
    var used = pool.length;
    function next() {
      for (;;) {
        if (used >= pool.length) { window.crypto.getRandomValues(pool); used = 0; }
        var b = pool[used++];
        if (b < limit) { return alphabet.charAt(b % alphabet.length); }
      }
    }
    for (var g = 0; g < 4; g++) {
      var group = '';
      for (var i = 0; i < 4; i++) { group += next(); }
      out.push(group);
    }
    return out.join('-');
  }

  document.addEventListener('click', function (e) {
    var button = e.target.closest && e.target.closest('[data-generate-password]');
    if (!button) { return; }
    var input = document.querySelector(button.getAttribute('data-generate-password'));
    if (!input) { return; }
    input.value = generate();
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.focus();
    input.select();
  });
})();
