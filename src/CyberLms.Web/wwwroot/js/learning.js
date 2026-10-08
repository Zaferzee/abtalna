/* Learning experience v2 (employee portal): journey fill, dashboard progress change, simulated acknowledgment in the preview,
   assessment submission summary and "calculating" state. Progressive enhancement only; no business rule lives here
   (the acknowledgment gate and scoring are enforced on the server). CSP: script-src 'self'. */
(function () {
  'use strict';
  var doc = document, root = doc.documentElement;
  var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var $ = function (s, c) { return (c || doc).querySelector(s); };
  var $$ = function (s, c) { return Array.prototype.slice.call((c || doc).querySelectorAll(s)); };
  var raf = window.requestAnimationFrame || function (f) { return setTimeout(f, 16); };
  var later = function (fn, ms) { raf(function () { raf(function () { ms ? setTimeout(fn, ms) : fn(); }); }); };

  // ---------- Journey: draw the progress track once the page is shown ----------
  var journeys = $$('[data-journey]');
  if (reduce) journeys.forEach(function (j) { j.classList.add('is-drawn'); });
  else later(function () { journeys.forEach(function (j) { j.classList.add('is-drawn'); }); }, 250);

  // ---------- Dashboard: animate the overall progress from the value seen last time ----------
  // Per-viewer convenience only (localStorage); the number shown always comes from the server.
  var hero = $('[data-progress-key]');
  if (hero) {
    var key = 'lx-progress-' + hero.getAttribute('data-progress-key'), now = parseFloat(hero.getAttribute('data-progress')), prev = null;
    try { prev = window.localStorage.getItem(key); prev = prev === null ? null : parseFloat(prev); } catch (e) { prev = null; }
    var ring = $('.ring-fg', hero);
    if (prev !== null && !isNaN(prev) && !isNaN(now) && ring && !reduce) {
      // start the ring at the previous value; ui.js then animates it to the current one
      ring.style.transition = 'none'; ring.style.strokeDasharray = prev + ' 100'; ring.getBoundingClientRect(); ring.style.transition = '';
      var delta = Math.round(now - prev), chip = $('[data-progress-delta]', hero);
      if (delta > 0 && chip) { chip.textContent = '+' + delta + '%'; chip.setAttribute('dir', 'ltr'); chip.hidden = false; }
    }
    try { window.localStorage.setItem(key, String(now)); } catch (e) { /* storage unavailable: no change indicator */ }
  }

  // ---------- Preview ("view as employee"): the simulated acknowledgment also advances the journey ----------
  $$('[data-ack-preview] [data-ack-sim]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var band = $('[data-preview-journey]'); if (!band) return;
      var j = $('[data-journey]', band), t = function (k) { return j.getAttribute('data-t-' + k) || ''; };
      var set = function (step, state, icon, sub) {
        var li = $('.j-step[data-step="' + step + '"]', j); if (!li) return;
        li.className = 'j-step j-' + state + ' is-changing';
        if (state === 'current') li.setAttribute('aria-current', 'step'); else li.removeAttribute('aria-current');
        var i = $('.j-ico', li); if (i) i.className = 'bi j-ico ' + (icon || i.getAttribute('data-icon'));
        var s = $('[data-sub]', li); if (s) s.textContent = sub;
      };
      set('content', 'done', 'bi-check-lg', t('done'));
      set('ack', 'done', 'bi-check-lg', t('done'));
      set('assessment', 'current', null, t('available'));
      var steps = $$('.j-step', j), cur = steps.findIndex(function (li) { return li.getAttribute('data-step') === 'assessment'; });
      if (cur < 0) { set('done', 'done', 'bi-check-lg', t('completed')); cur = steps.length - 1; j.classList.add('is-complete'); }
      j.style.setProperty('--jp', Math.round(100 * cur / Math.max(1, steps.length - 1)));
      var chip = $('[data-journey-status]', band); if (chip) chip.hidden = true;
    });
  });

  // ---------- Assessment: answered summary in the confirmation, then a calm "calculating" state ----------
  var quiz = $('[data-quiz]');
  if (quiz) {
    quiz.addEventListener('submit', function (e) {
      if (e.defaultPrevented) return;
      var steps = $$('.qstep', quiz), done = steps.filter(function (s) { return !!$('input:checked', s); }).length;
      if (!quiz.__confirmed) {
        var fmt = quiz.getAttribute('data-answered-format') || '';
        quiz.setAttribute('data-confirm', (quiz.getAttribute('data-confirm-base') || '') + ' ' + fmt.replace('{0}', done).replace('{1}', steps.length));
        return; // site.js shows the confirmation
      }
      quiz.classList.add('is-submitting');
      var box = $('[data-submitting]', quiz); if (box) box.hidden = false;
      $$('.quiz-bar, .qnav', doc).forEach(function (x) { x.style.opacity = '.55'; });
    });
  }

  // ---------- Back/forward cache: never show a stale loading state ----------
  window.addEventListener('pageshow', function (e) {
    if (!e.persisted) return;
    $$('.is-loading').forEach(function (b) { b.classList.remove('is-loading'); b.removeAttribute('aria-busy'); });
    if (quiz) { quiz.classList.remove('is-submitting'); var box = $('[data-submitting]', quiz); if (box) box.hidden = true; }
  });

  root.classList.add('lx');
})();
