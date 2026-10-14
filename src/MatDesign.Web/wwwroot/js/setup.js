/* The setup assistant of the Studio (docs/STUDIO.md, "Setup"): one form in three steps (your company, where to start, the administrator).
   Without this script all three steps are on the page one under the other and the form is sent as it is. With it, one step is shown at a time:
   Next checks the required fields of the step it leaves, Back goes one step back, Enter in a field goes to the next step instead of sending a
   form that is not finished, and the bar above shows where you are. The server checks everything again on the last step. */
(function () {
  'use strict';

  var form = document.querySelector('form[data-setup]');
  if (!form) { return; }

  var steps = Array.prototype.slice.call(form.querySelectorAll('[data-setup-step]'));
  var bars = Array.prototype.slice.call(form.querySelectorAll('[data-setup-progress] li'));
  var back = form.querySelector('[data-setup-back]');
  var next = form.querySelector('[data-setup-next]');
  var submit = form.querySelector('[data-setup-submit]');
  var current = Math.min(steps.length, Math.max(1, parseInt(form.getAttribute('data-setup-first'), 10) || 1));

  function show(n, focus) {
    current = n;
    steps.forEach(function (step, i) { step.hidden = i + 1 !== n; });
    bars.forEach(function (bar, i) {
      bar.classList.toggle('is-done', i + 1 < n);
      bar.classList.toggle('is-active', i + 1 === n);
      if (i + 1 === n) { bar.setAttribute('aria-current', 'step'); } else { bar.removeAttribute('aria-current'); }
    });
    back.hidden = n === 1;
    next.hidden = n === steps.length;
    submit.hidden = n !== steps.length;
    if (focus) {
      var target = steps[n - 1].querySelector('.is-invalid input, input:checked, input:not([type="radio"])');
      if (target) { target.focus(); }
    }
  }

  // the first required field of a step that is empty; its error is written where the toolkit writes one
  function check(step) {
    var wrong = null;
    Array.prototype.forEach.call(step.querySelectorAll('input[required]'), function (input) {
      var field = input.closest('.mat-field');
      var ok = input.value.trim().length > 0;
      if (field) {
        field.classList.toggle('is-invalid', !ok);
        var error = field.querySelector('.mat-error');
        if (error) { error.textContent = ok ? '' : (input.getAttribute('data-msg-required') || 'Please fill this in.'); }
      }
      if (!ok && !wrong) { wrong = input; }
    });
    return wrong;
  }

  function forward() {
    var wrong = check(steps[current - 1]);
    if (wrong) { wrong.focus(); return; }
    show(current + 1, true);
  }

  next.addEventListener('click', forward);
  back.addEventListener('click', function () { show(current - 1, true); });

  // Enter in a field of a step that is not the last one goes on; it never sends an unfinished form. The toolkit checks a form on submit in the capture phase of the document,
  // so this listens one step earlier, on the window, and the toolkit's own check runs on the last step only.
  window.addEventListener('submit', function (e) {
    if (e.target === form && current < steps.length) {
      e.preventDefault();
      e.stopPropagation();
      forward();
    }
  }, true);

  show(current, form.querySelector('.is-invalid') !== null || current === 1);
}());
