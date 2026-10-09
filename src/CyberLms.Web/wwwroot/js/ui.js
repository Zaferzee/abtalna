/* Presentation-layer interactions for the redesigned UI (motion, navigation, assessment wizard, branding live preview).
   Progressive enhancement only: every page works without this file. No inline scripts are needed (CSP: script-src 'self'). */
(function () {
  'use strict';
  var doc = document, root = doc.documentElement, body = doc.body;
  var rtl = root.dir === 'rtl';
  var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  root.classList.add('js');
  var $ = function (s, c) { return (c || doc).querySelector(s); };
  var $$ = function (s, c) { return Array.prototype.slice.call((c || doc).querySelectorAll(s)); };
  var raf = window.requestAnimationFrame || function (f) { return setTimeout(f, 16); };
  var later = function (fn) { raf(function () { raf(fn); }); };

  // ---------- Sidebar (mobile off-canvas) ----------
  $$('[data-sidebar-toggle]').forEach(function (b) {
    b.addEventListener('click', function () { body.classList.toggle('sb-open'); b.setAttribute('aria-expanded', body.classList.contains('sb-open')); });
  });
  $$('[data-sidebar-close]').forEach(function (b) { b.addEventListener('click', function () { body.classList.remove('sb-open'); }); });
  doc.addEventListener('keydown', function (e) { if (e.key === 'Escape') body.classList.remove('sb-open'); });

  // ---------- Count-up numbers ----------
  function countUp(el) {
    var target = parseFloat(el.getAttribute('data-count'));
    if (isNaN(target)) return;
    var dec = (el.getAttribute('data-count').split('.')[1] || '').length > 0 && target % 1 !== 0 ? 1 : 0;
    if (reduce || target === 0) { el.textContent = target.toFixed(dec); return; }
    var start = null, dur = Math.min(1400, 600 + target * 8);
    el.textContent = (0).toFixed(dec);
    function step(ts) {
      if (!start) start = ts;
      var p = Math.min(1, (ts - start) / dur), e = 1 - Math.pow(1 - p, 3);
      el.textContent = (target * e).toFixed(dec);
      if (p < 1) raf(step); else el.textContent = target.toFixed(dec);
    }
    raf(step);
  }

  // ---------- Rings, meters, donuts, stacked bars (animate to their value) ----------
  function fill(scope) {
    $$('[data-ring]', scope).forEach(function (c) { c.style.strokeDasharray = parseFloat(c.getAttribute('data-ring')) + ' 100'; });
    $$('[data-w]', scope).forEach(function (s) { s.style.width = parseFloat(s.getAttribute('data-w')) + '%'; });
    $$('[data-seg]', scope).forEach(function (c) { c.style.strokeDasharray = parseFloat(c.getAttribute('data-seg')) + ' 100'; });
    $$('[data-count]', scope).forEach(countUp);
  }
  if (reduce) fill(doc); else later(function () { fill(doc); });

  // ---------- Reveal on scroll ----------
  var revealEls = $$('.reveal-scroll');
  if (revealEls.length) {
    if (reduce || !('IntersectionObserver' in window)) revealEls.forEach(function (el) { el.classList.add('in'); });
    else {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (en) { if (en.isIntersecting) { en.target.classList.add('in'); io.unobserve(en.target); } });
      }, { rootMargin: '0px 0px -8% 0px' });
      revealEls.forEach(function (el) { io.observe(el); });
    }
  }

  // ---------- Reading: progress bar + generated table of contents ----------
  var bar = $('.read-progress');
  if (bar) {
    var upd = function () {
      var h = doc.documentElement.scrollHeight - window.innerHeight;
      bar.style.transform = 'scaleX(' + (h > 0 ? Math.min(1, window.scrollY / h) : 1) + ')';
    };
    window.addEventListener('scroll', upd, { passive: true }); upd();
  }
  var tocBox = $('[data-toc]'), src = $('[data-toc-source]');
  if (tocBox && src) {
    var heads = $$('h1, h2, h3', src).filter(function (h) { return h.textContent.trim().length > 0; });
    if (heads.length < 2) tocBox.hidden = true;
    else {
      var ol = $('ol', tocBox);
      heads.forEach(function (h, i) {
        if (!h.id) h.id = 'sec-' + (i + 1);
        var li = doc.createElement('li'); if (h.tagName === 'H3') li.className = 'lvl-3';
        var a = doc.createElement('a'); a.href = '#' + h.id; a.textContent = h.textContent.trim(); a.setAttribute('dir', 'auto');
        li.appendChild(a); ol.appendChild(li);
      });
      tocBox.hidden = false;
      if ('IntersectionObserver' in window) {
        var links = $$('a', ol), seen = {};
        var tio = new IntersectionObserver(function (entries) {
          entries.forEach(function (en) { seen[en.target.id] = en.isIntersecting; });
          var first = heads.filter(function (h) { return seen[h.id]; })[0];
          if (first) links.forEach(function (l) { l.classList.toggle('active', l.getAttribute('href') === '#' + first.id); });
        }, { rootMargin: '-10% 0px -65% 0px' });
        heads.forEach(function (h) { tio.observe(h); });
      }
    }
  }

  // ---------- Acknowledgment: the confirm checkbox enables the button ----------
  $$('[data-ack-check]').forEach(function (cb) {
    var btn = $('[data-ack-submit]', cb.form);
    if (!btn) return;
    var sync = function () { btn.disabled = !cb.checked; };
    cb.addEventListener('change', sync); sync();
  });

  // ---------- Acknowledgment gate in the preview: simulate the acknowledgment and unlock the assessment (nothing is sent) ----------
  $$('[data-ack-preview]').forEach(function (panel) {
    var cb = $('[data-ack-sim-check]', panel), btn = $('[data-ack-sim]', panel);
    if (!cb || !btn) return;
    var sync = function () { btn.disabled = !cb.checked; };
    cb.addEventListener('change', sync); sync();
    btn.addEventListener('click', function () {
      if (!cb.checked) return;
      panel.classList.add('is-done', 'just-done');
      var t = $('[data-ack-title]', panel); if (t) t.textContent = panel.getAttribute('data-done-title');
      var ic = $('.ack-icon i', panel); if (ic) ic.className = 'bi bi-patch-check-fill';
      cb.disabled = true; btn.hidden = true;
      var ins = $('[data-ack-instructions]', panel); if (ins) ins.hidden = true;
      $$('[data-ack-locked]').forEach(function (ns) {
        ns.classList.remove('is-locked'); ns.classList.add('is-unlocking');
        var i = $('[data-ns-icon]', ns); if (i) i.className = 'bi bi-ui-checks';
        $$('[data-ns-locked], [data-lock-msg]', ns).forEach(function (x) { x.hidden = true; });
        $$('[data-ns-open]', ns).forEach(function (x) { x.hidden = false; });
      });
      $$('[data-ack-locked-step]').forEach(function (li) { li.classList.remove('is-locked'); var l = $('.step-lock', li); if (l) l.remove(); });
    });
  });

  // ---------- Assessment wizard (one question per step; without JS every question is shown) ----------
  var quiz = $('[data-quiz]');
  if (quiz) {
    var steps = $$('.qstep', quiz), cur = 0;
    var intro = $('[data-quiz-intro]'), stage = $('[data-quiz-stage]');
    var nav = $('[data-qnav]'), prog = $('[data-q-progress]'), count = $('[data-q-count]'), countFmt = count ? count.getAttribute('data-format') : '{0} / {1}';
    var prev = $('[data-q-prev]'), next = $('[data-q-next]'), submit = $('[data-q-submit]'), note = $('.unanswered-note');
    var answered = function (s) { return !!$('input:checked', s); };
    steps.forEach(function (s, i) {
      var b = doc.createElement('button'); b.type = 'button'; b.textContent = i + 1;
      b.setAttribute('aria-label', (nav.getAttribute('data-label') || '{0}').replace('{0}', i + 1));
      b.addEventListener('click', function () { go(i); });
      nav.appendChild(b);
    });
    function refresh() {
      var done = steps.filter(answered).length;
      $$('button', nav).forEach(function (b, i) {
        b.classList.toggle('answered', answered(steps[i])); b.classList.toggle('current', i === cur);
        if (i === cur) b.setAttribute('aria-current', 'step'); else b.removeAttribute('aria-current');
      });
      if (prog) prog.style.width = (steps.length ? done * 100 / steps.length : 0) + '%';
      if (count) count.textContent = countFmt.replace('{0}', done).replace('{1}', steps.length);
      prev.disabled = cur === 0;
      var last = cur === steps.length - 1;
      next.hidden = last; submit.hidden = !last;
      if (note && done === steps.length) note.classList.remove('show');
    }
    function go(i, back) {
      if (i < 0 || i >= steps.length) return;
      var dirBack = back === undefined ? i < cur : back;
      steps[cur].classList.remove('is-active', 'is-back');
      cur = i;
      steps[cur].classList.toggle('is-back', dirBack);
      steps[cur].classList.add('is-active');
      refresh();
      var first = $('input:checked', steps[cur]) || $('input', steps[cur]);
      if (first && !reduce) first.focus({ preventScroll: true }); else if (first) first.focus();
      var top = quiz.getBoundingClientRect().top + window.scrollY - 140;
      if (window.scrollY > top) window.scrollTo({ top: top, behavior: reduce ? 'auto' : 'smooth' });
    }
    quiz.addEventListener('change', function (e) {
      refresh();
      // Single-answer questions: move on automatically after a short beat (not on the last step).
      var t = e.target;
      if (t.type === 'radio' && cur < steps.length - 1 && steps[cur].contains(t)) setTimeout(function () { if (steps[cur].contains(t)) go(cur + 1, false); }, reduce ? 0 : 420);
    });
    prev.addEventListener('click', function () { go(cur - 1, true); });
    next.addEventListener('click', function () { go(cur + 1, false); });
    // Keyboard: 1-9 picks an option of the current question.
    doc.addEventListener('keydown', function (e) {
      if (!steps[cur].classList.contains('is-active') || e.altKey || e.ctrlKey || e.metaKey) return;
      if (/^[1-9]$/.test(e.key) && (!doc.activeElement || doc.activeElement.tagName !== 'TEXTAREA')) {
        var inp = $$('input', steps[cur])[parseInt(e.key, 10) - 1];
        if (inp) { inp.click(); inp.focus(); }
      }
    });
    quiz.addEventListener('submit', function (e) {
      var missing = steps.map(function (s, i) { return answered(s) ? -1 : i; }).filter(function (i) { return i >= 0; });
      if (missing.length && note && !note.classList.contains('show')) {
        e.preventDefault(); e.stopImmediatePropagation();
        note.classList.add('show');
        var m = $('[data-missing]', note); if (m) m.textContent = missing.map(function (i) { return i + 1; }).join(rtl ? '، ' : ', ');
        go(missing[0]);
      }
    }, true);
    var startQuiz = function () {
      if (intro) intro.hidden = true;
      if (stage) stage.hidden = false;
      steps.forEach(function (s) { s.classList.remove('is-active'); });
      steps[0].classList.add('is-active'); cur = 0; refresh();
      var f = $('input', steps[0]); if (f) f.focus();
    };
    if (intro && stage) {
      stage.hidden = true;
      $('[data-quiz-start]').addEventListener('click', startQuiz);
      // Resuming with saved answers? Skip the intro.
      if (steps.some(answered)) startQuiz();
    } else startQuiz();
  }

  // ---------- Result celebration ----------
  var burst = $('.burst');
  if (burst && !reduce) {
    var cs = getComputedStyle(root), cols = ['--color-primary', '--color-accent', '--color-success', '--color-warning'].map(function (v) { return cs.getPropertyValue(v).trim() || '#999'; });
    // A small, single celebration: fewer particles and a shorter reach (meaningful completion only).
    for (var i = 0; i < 18; i++) {
      var p = doc.createElement('i'), ang = Math.random() * Math.PI * 2, dist = 110 + Math.random() * 170;
      p.style.setProperty('--x', Math.cos(ang) * dist + 'px'); p.style.setProperty('--y', Math.sin(ang) * dist * 0.75 - 40 + 'px');
      p.style.setProperty('--r', (Math.random() * 720 - 360) + 'deg'); p.style.setProperty('--c', cols[i % cols.length]);
      p.style.animationDelay = (0.85 + Math.random() * 0.25) + 's';
      if (i % 3 === 0) p.style.borderRadius = '50%';
      burst.appendChild(p);
    }
  }

  // ---------- Login: show/hide password, busy state on submit ----------
  $$('[data-pw-toggle]').forEach(function (b) {
    var input = doc.getElementById(b.getAttribute('data-pw-toggle'));
    b.addEventListener('click', function () {
      var show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      b.setAttribute('aria-pressed', show);
      b.setAttribute('aria-label', b.getAttribute(show ? 'data-hide' : 'data-show'));
      $('.bi', b).className = 'bi ' + (show ? 'bi-eye-slash' : 'bi-eye');
      input.focus();
    });
  });
  $$('form[data-busy]').forEach(function (f) {
    f.addEventListener('submit', function (e) {
      if (e.defaultPrevented) return;
      var b = f.querySelector('button[type=submit], button:not([type])');
      if (b) { b.classList.add('is-loading'); b.setAttribute('aria-busy', 'true'); }
    });
  });

  // ---------- Embedded video player ----------
  $$('[data-video-player]').forEach(function (pl) {
    var video = $('[data-video]', pl), frame = $('.vp-frame', pl);
    // Keep the video's own proportions (16:9 frame by default) and do not blow small videos up past ~1.5x.
    var fit = function () {
      var w = video.videoWidth, h = video.videoHeight;
      if (!w || !h) return;
      var r = w / h;
      frame.style.setProperty('--ar', r >= 1.2 && r <= 2.4 ? (w + ' / ' + h) : '16 / 9');
      frame.style.maxWidth = w < 640 ? Math.max(Math.round(w * 1.5), 480) + 'px' : '';
    };
    video.addEventListener('loadedmetadata', fit); if (video.readyState >= 1) fit();
    var err = $('[data-video-error]', pl);
    video.addEventListener('error', function () { err.hidden = false; pl.classList.add('has-error'); });
    video.addEventListener('loadstart', function () { err.hidden = true; pl.classList.remove('has-error'); });
    video.addEventListener('play', function () { pl.classList.add('is-playing'); });
    video.addEventListener('pause', function () { pl.classList.remove('is-playing'); });
    // Several videos: one player, a list to switch between them.
    $$('[data-video-src]', pl).forEach(function (b) {
      b.addEventListener('click', function () {
        $$('[data-video-src]', pl).forEach(function (x) { x.classList.toggle('is-active', x === b); x.setAttribute('aria-pressed', x === b); });
        video.pause(); video.src = b.getAttribute('data-video-src'); video.setAttribute('aria-label', b.getAttribute('data-video-name'));
        var title = $('[data-video-title]', pl); title.lastChild.textContent = ' ' + b.getAttribute('data-video-name');
        $('[data-video-download]', pl).href = b.getAttribute('data-video-dl'); $('[data-video-error-dl]', pl).href = b.getAttribute('data-video-dl');
        video.load(); video.play().catch(function () { });
      });
    });
  });

  // ---------- Authoring wizard ----------
  // Sections that only apply to one answer of a yes/no question.
  $$('[data-show-when]').forEach(function (el) {
    var radio = doc.getElementById(el.getAttribute('data-show-when'));
    if (!radio) return;
    var sync = function () { el.hidden = !radio.checked; };
    $$('input[name="' + radio.name + '"]').forEach(function (r) { r.addEventListener('change', sync); });
    sync();
  });
  // Live mirror of a text field (e.g. the acknowledgment statement as the employee will see it).
  $$('[data-mirror]').forEach(function (inp) {
    var key = inp.getAttribute('data-mirror');
    var sync = function () { $$('[data-mirror-target="' + key + '"]').forEach(function (t) { t.textContent = inp.value; }); };
    inp.addEventListener('input', sync); sync();
  });
  $$('[data-fill]').forEach(function (b) {
    b.addEventListener('click', function () {
      var inp = doc.getElementById(b.getAttribute('data-fill')); if (!inp) return;
      inp.value = b.getAttribute('data-value'); inp.dispatchEvent(new Event('input', { bubbles: true })); inp.focus();
    });
  });
  $$('[data-counter]').forEach(function (inp) {
    var out = $('[data-counter-for="' + inp.id + '"]'); if (!out) return;
    var sync = function () { out.textContent = inp.value.length + ' / ' + inp.maxLength; };
    inp.addEventListener('input', sync); sync();
  });
  // Drop zone: show the chosen files before upload.
  $$('[data-dropzone]').forEach(function (z) {
    var inp = $('input[type=file]', z), list = $('[data-dz-list]', z);
    ['dragenter', 'dragover'].forEach(function (ev) { z.addEventListener(ev, function () { z.classList.add('is-over'); }); });
    ['dragleave', 'drop'].forEach(function (ev) { z.addEventListener(ev, function () { z.classList.remove('is-over'); }); });
    inp.addEventListener('change', function () {
      list.innerHTML = '';
      Array.prototype.forEach.call(inp.files, function (f) { var s = doc.createElement('span'); s.textContent = '• ' + f.name; s.dir = 'auto'; list.appendChild(s); });
    });
  });
  // Removing an existing assessment asks first.
  var asmNo = $('[data-confirm-change]');
  if (asmNo && asmNo.getAttribute('data-confirm-change')) {
    var asmForm = asmNo.form;
    var syncAsm = function () { if (asmNo.checked) asmForm.setAttribute('data-confirm', asmNo.getAttribute('data-confirm-change')); else asmForm.removeAttribute('data-confirm'); };
    $$('input[name="' + asmNo.name + '"]').forEach(function (r) { r.addEventListener('change', syncAsm); });
    syncAsm();
  }
  // Question editor: type switch, options, fast keyboard entry.
  var qe = $('[data-qeditor]');
  if (qe) {
    var wrap = qe.closest('[data-q-editor-wrap]');
    var typeVal = function () { var r = $('input[name="Question.Type"]:checked', qe); return r ? r.value : '1'; };
    var applyType = function () {
      var t = typeVal();
      $$('[data-when-type]', qe).forEach(function (sec) { sec.hidden = sec.getAttribute('data-when-type').split(',').indexOf(t) < 0; });
      var multi = t === '3';
      $$('.opt-correct', qe).forEach(function (i) { if (i.type !== (multi ? 'checkbox' : 'radio')) { var was = i.checked; i.type = multi ? 'checkbox' : 'radio'; i.checked = was; } });
      if (!multi) { var first = true; $$('.opt-correct', qe).forEach(function (i) { if (i.checked) { if (!first) i.checked = false; first = false; } }); }
      $('[data-hint-single]', qe).hidden = multi; $('[data-hint-multi]', qe).hidden = !multi;
    };
    $$('input[name="Question.Type"]', qe).forEach(function (r) { r.addEventListener('change', applyType); });
    applyType();
    var addOption = function () {
      var hidden = $$('[data-opt-row][hidden]', qe)[0];
      if (!hidden) return null;
      hidden.hidden = false;
      if (!$$('[data-opt-row][hidden]', qe).length) $('[data-add-option]', qe).hidden = true;
      return $('input.form-control', hidden);
    };
    $('[data-add-option]', qe).addEventListener('click', function () { var i = addOption(); if (i) i.focus(); });
    if (!$$('[data-opt-row][hidden]', qe).length) $('[data-add-option]', qe).hidden = true;
    // Enter in an option moves to the next one (and opens a new row when needed) instead of submitting.
    qe.addEventListener('keydown', function (e) {
      if (e.key !== 'Enter' || !e.target.matches('[data-opt-row] input.form-control')) return;
      e.preventDefault();
      var rows = $$('[data-opt-row]:not([hidden])', qe), idx = rows.indexOf(e.target.closest('[data-opt-row]'));
      var nxt = rows[idx + 1] ? $('input.form-control', rows[idx + 1]) : addOption();
      if (nxt) nxt.focus();
    });
    var editingId = $('input[name="Question.Id"]', qe);
    var openEditor = function (focus) {
      wrap.hidden = false;
      wrap.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
      if (focus) { var ta = $('[data-autofocus]', qe); if (ta) setTimeout(function () { ta.focus({ preventScroll: true }); }, reduce ? 0 : 350); }
    };
    $$('[data-open-editor]').forEach(function (b) {
      b.addEventListener('click', function (e) {
        if (editingId && +editingId.value > 0) return; // editing another question: load a fresh form
        e.preventDefault(); openEditor(true);
      });
    });
    $$('[data-close-editor]', qe).forEach(function (b) {
      b.addEventListener('click', function (e) { if (editingId && +editingId.value > 0) return; e.preventDefault(); wrap.hidden = true; });
    });
    if (!wrap.hidden) openEditor(true);
  }
  // Warn before leaving a wizard step with unsaved changes.
  $$('form[data-dirty-guard]').forEach(function (f) {
    var dirty = false, submitting = false;
    f.addEventListener('input', function () { dirty = true; });
    f.addEventListener('change', function () { dirty = true; });
    // The step bar and navigation buttons save through this form (form="id").
    doc.addEventListener('submit', function () { submitting = true; }, true);
    window.addEventListener('beforeunload', function (e) { if (dirty && !submitting) { e.preventDefault(); e.returnValue = ''; } });
  });
  // (The Branding & Appearance studio has its own script: branding-studio.js.)
})();
