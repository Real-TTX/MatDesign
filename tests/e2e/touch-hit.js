// The one way the touch checks measure a target (MOBILE-04), shared by touch-audit.js and the 'Touch targets' block of run.js.
// `await page.evaluate(installTouchHit)` puts `window.__touchHit(element)` in the page. It returns the EFFECTIVE hit area of the element, not the drawn mark:
//   - an input that is only a proxy (invisible: a switch) stands for the label that wraps it; the hidden twin of a control that the script draws (a code, a picker, a slider,
//     a rating, the file field of an upload) is not measured: what is drawn instead is;
//   - the cell around the check of a table row, the row of a field in a list and the field of tags forward a tap to their input (matui.js), so they count with it;
//   - the element is scrolled into view, a point inside it that really reaches it (or its label) is found, and from there the script walks outwards, a pixel at a time, as
//     long as `document.elementFromPoint` still names the element, its label or something inside them: a padding on a label, a ::before or ::after that reaches beyond the
//     mark and a stretched link all count, a tap that would fall between two things does not;
//   - the shape of a rating (`.mat-rating__cell`) is a target of its own though the row of shapes receives the tap: its cell is measured.
// Result: { el, w, h, covered }: the element that stands for the target, the width and height of the area in px, and, when no point of the element reaches it, what covers it.
function installTouchHit() {
  const MAX_WALK = 90;
  function union(els) {
    let l = Infinity, t = Infinity, r = -Infinity, b = -Infinity;
    for (const e of els) {
      const q = e.getBoundingClientRect();
      if (q.width < 1 && q.height < 1) { continue; }
      l = Math.min(l, q.left); t = Math.min(t, q.top); r = Math.max(r, q.right); b = Math.max(b, q.bottom);
    }
    return { w: r - l, h: b - t };
  }
  function describe(el) {
    const cls = String(el.getAttribute('class') || '').split(/\s+/).filter(Boolean).slice(0, 2).map(c => '.' + c).join('');
    return el.tagName.toLowerCase() + cls;
  }
  window.__touchHit = function (original, min) {
    min = min || 44;
    let el = original;
    const own = getComputedStyle(el);
    const r0 = el.getBoundingClientRect();
    const proxy = own.opacity === '0' || own.pointerEvents === 'none' || (r0.width < 3 && r0.height < 3);
    // the file input of an upload is hidden: the drop zone (a button of its own) is what is pressed
    if (proxy && el.matches('.mat-upload input[type="file"]')) { return null; }
    if (proxy) {
      // only a label that wraps the input is the target (a switch); the caption of a field whose input the script hides (a code, a picker, a slider, a rating) is not:
      // what the script draws instead (the boxes, the trigger, the thumbs, the shapes) is measured as itself
      const lab = el.closest('label');
      if (!lab) { return null; }
      el = lab;
      // the label of a radio of a rating is as invisible as the radio
      const lr = lab.getBoundingClientRect();
      if (lr.width < 3 && lr.height < 3) { return null; }
    }
    // (aria-hidden does not hide a target from a finger: the shapes of a rating are drawn in an aria-hidden row)
    if (!el.checkVisibility({ checkVisibilityCSS: true }) || el.closest('[inert], [hidden]')) { return null; }
    el.scrollIntoView({ block: 'center', inline: 'center' });
    const rect = el.getBoundingClientRect();
    if (rect.width < 1 || rect.height < 1) { return null; }
    if (rect.right < 0 || rect.bottom < 0 || rect.left > window.innerWidth || rect.top > window.innerHeight) { return null; }

    if (el.classList.contains('mat-rating__cell')) { return { el, w: rect.width, h: rect.height }; }

    // what stands for a control besides its labels: the cell around the check of a table row, the row of a field in a list and the field of tags forward a tap to it (matui.js)
    const holder = el.matches('input, select, textarea') ? el.closest('.mat-table__select, .mat-item__main, .mat-tagfield') : null;
    const owners = [el, ...(el.labels ? [...el.labels] : []), ...(el.closest('label') ? [el.closest('label')] : []), ...(holder ? [holder] : [])];
    const owned = (hit) => !!hit && owners.some(o => o.contains(hit));
    const box = union(owners);
    let w = box.w, h = box.h;
    if (w >= min && h >= min) { return { el, w, h }; }

    // a point that reaches the element: the middle of it, or the nearest of a grid
    const cx = rect.left + rect.width / 2, cy = rect.top + rect.height / 2;
    const pts = [];
    for (let i = 0; i < 5; i++) { for (let j = 0; j < 5; j++) { pts.push([rect.left + rect.width * (i + .5) / 5, rect.top + rect.height * (j + .5) / 5]); } }
    pts.sort((a, b) => Math.hypot(a[0] - cx, a[1] - cy) - Math.hypot(b[0] - cx, b[1] - cy));
    const inside = (p) => p[0] >= 0 && p[1] >= 0 && p[0] < window.innerWidth && p[1] < window.innerHeight;
    const start = pts.find(p => inside(p) && owned(document.elementFromPoint(p[0], p[1])));
    if (!start) {
      const p = pts.find(inside);
      const hit = p ? document.elementFromPoint(p[0], p[1]) : null;
      // nothing but the page behind it: it is clipped away (the header of a table that is drawn as cards is only there for a screen reader)
      if (!hit || hit === document.documentElement || hit === document.body) { return null; }
      return { el, w, h, covered: hit ? describe(hit) : 'nothing' };
    }
    const walk = (dx, dy) => {
      for (let step = 1; step <= MAX_WALK; step++) {
        const x = start[0] + dx * step, y = start[1] + dy * step;
        if (x < 0 || y < 0 || x >= window.innerWidth || y >= window.innerHeight) { return step; }
        if (!owned(document.elementFromPoint(x, y))) { return step - 1; }
      }
      return MAX_WALK;
    };
    w = Math.max(w, walk(-1, 0) + walk(1, 0) + 1);
    h = Math.max(h, walk(0, -1) + walk(0, 1) + 1);
    return { el, w, h };
  };
}

module.exports = { installTouchHit };
