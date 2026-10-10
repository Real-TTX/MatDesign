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

  document.addEventListener('click', function (e) {
    var button = e.target.closest('[data-doc-demo]');
    if (button) { run(button.getAttribute('data-doc-demo')); }
  });
})();
