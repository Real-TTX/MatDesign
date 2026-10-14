/* The assets page of the Studio (docs/STUDIO.md, "Brand assets"). It does one thing: it shows each uploaded font as a specimen. Every [data-specimen] is loaded from the address the
   site serves it from (data-src) under a temporary family name of its own (mat-specimen-N), so that the specimen shows the file itself and not whatever the stylesheet says about the
   family; the weight and the style come from data-weight and data-style. The family is set through the CSSOM (no inline style in the markup). A font the browser cannot read says so
   in words next to the specimen. Without the script the specimen is plain text in the page's font and the state says nothing more than "Loading ...". */
(function () {
  'use strict';

  var specimens = document.querySelectorAll('[data-specimen]');
  if (!specimens.length) { return; }

  function say(specimen, text, state) {
    var note = specimen.parentNode && specimen.parentNode.querySelector('[data-specimen-state]');
    if (note) { note.textContent = text; }
    specimen.setAttribute('data-font-state', state);
  }

  if (!window.FontFace || !document.fonts) {
    Array.prototype.forEach.call(specimens, function (s) { say(s, 'This browser cannot show the font.', 'unsupported'); });
    return;
  }

  Array.prototype.forEach.call(specimens, function (specimen, index) {
    var family = 'mat-specimen-' + (index + 1);
    var face = new FontFace(family, 'url("' + specimen.getAttribute('data-src') + '")', {
      weight: specimen.getAttribute('data-weight') || '400',
      style: specimen.getAttribute('data-style') || 'normal',
      display: 'swap',
    });
    face.load().then(function (loaded) {
      document.fonts.add(loaded);
      specimen.style.fontFamily = '"' + family + '", sans-serif';
      specimen.style.fontWeight = specimen.getAttribute('data-weight') || '400';
      specimen.style.fontStyle = specimen.getAttribute('data-style') || 'normal';
      say(specimen, 'Loaded', 'loaded');
    }, function () {
      say(specimen, 'The browser could not read this font file.', 'error');
    });
  });
})();
