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
    for (var i = 0; i < 34; i++) {
      var p = doc.createElement('i'), ang = Math.random() * Math.PI * 2, dist = 140 + Math.random() * 260;
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

  // ---------- Branding: live preview of unsaved changes ----------
  var form = $('[data-brand-form]'), pv = $('[data-preview]');
  if (form && pv) {
    var hexOk = function (v) { return /^#[0-9a-fA-F]{6}$/.test(v); };
    var lum = function (hex) {
      var c = [1, 3, 5].map(function (i) { var v = parseInt(hex.substr(i, 2), 16) / 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); });
      return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
    };
    var contrast = function (a, b) { var x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
    var onColor = function (hex) { return lum(hex) > 0.42 ? '#14213d' : '#ffffff'; };
    var val = function (name) { var el = form.elements[name]; return el ? el.value.trim() : ''; };
    var applyColors = function () {
      $$('[data-var]', form).forEach(function (inp) {
        var v = inp.value.trim(); if (!hexOk(v)) return;
        pv.style.setProperty(inp.getAttribute('data-var'), v);
        var sw = inp.closest('.swatch'); if (sw) { var c = $('input[type=color]', sw); if (c && c.value.toLowerCase() !== v.toLowerCase()) c.value = v; }
      });
      if (hexOk(val('PrimaryColor'))) pv.style.setProperty('--color-on-primary', onColor(val('PrimaryColor')));
      if (hexOk(val('AccentColor'))) pv.style.setProperty('--color-on-accent', onColor(val('AccentColor')));
      $$('[data-contrast]', form).forEach(function (n) {
        var pair = n.getAttribute('data-contrast').split(','), a = val(pair[0]), b = val(pair[1]);
        if (!hexOk(a) || !hexOk(b)) return;
        var r = contrast(a, b), ok = r >= 4.5;
        n.classList.toggle('bad', !ok);
        n.innerHTML = '';
        var i = doc.createElement('i'); i.className = 'bi ' + (ok ? 'bi-check-circle-fill text-success' : 'bi-exclamation-triangle-fill');
        var s = doc.createElement('span'); s.textContent = ' ' + n.getAttribute(ok ? 'data-ok' : 'data-bad') + ' ';
        var num = doc.createElement('bdi'); num.dir = 'ltr'; num.textContent = r.toFixed(1) + ':1';
        n.appendChild(i); n.appendChild(s); n.appendChild(num);
      });
    };
    var applyText = function () {
      $$('[data-bind]', form).forEach(function (inp) {
        var name = inp.getAttribute('data-bind'), v = inp.value.trim();
        $$('[data-bind-target="' + name + '"]', pv).forEach(function (t) { t.textContent = v || t.getAttribute('data-fallback') || ''; });
        if (name === 'SystemName') $$('[data-bind-mono]', pv).forEach(function (t) { t.textContent = (v || '•').charAt(0); });
      });
    };
    form.addEventListener('input', function () { applyColors(); applyText(); });
    form.addEventListener('change', function () { applyColors(); applyText(); });
    // Image pickers: show the chosen file in the preview before it is saved.
    $$('[data-preview-image]', form).forEach(function (inp) {
      inp.addEventListener('change', function () {
        var f = inp.files && inp.files[0], kind = inp.getAttribute('data-preview-image');
        if (!f || !/^image\//.test(f.type)) return;
        var r = new FileReader();
        r.onload = function () {
          if (kind === 'logo') {
            $$('[data-pv-logo]', pv).forEach(function (img) { img.src = r.result; });
            $$('.pv-brand', pv).forEach(function (b) { b.classList.add('has-logo'); });
          } else if (kind === 'loginbg') {
            $$('.pv-login-hero', pv).forEach(function (h) { h.style.backgroundImage = 'linear-gradient(160deg, rgba(10,20,50,.75), rgba(10,20,50,.35)), url("' + r.result + '")'; });
          }
          var thumb = $('[data-thumb-for="' + inp.name + '"]', form);
          if (thumb) { thumb.innerHTML = ''; var im = doc.createElement('img'); im.src = r.result; im.alt = ''; thumb.appendChild(im); }
        };
        r.readAsDataURL(f);
      });
    });
    $$('[data-remove-image]', form).forEach(function (cb) {
      cb.addEventListener('change', function () {
        if (cb.getAttribute('data-remove-image') === 'logo') $$('.pv-brand', pv).forEach(function (b) { b.classList.toggle('has-logo', !cb.checked); });
      });
    });
    // Presets fill every colour field at once (still needs "Save" to publish).
    $$('[data-preset]', form).forEach(function (b) {
      b.addEventListener('click', function () {
        var p; try { p = JSON.parse(b.getAttribute('data-preset')); } catch (e) { return; }
        Object.keys(p).forEach(function (k) { var el = form.elements[k]; if (el) el.value = p[k]; });
        applyColors();
        pv.classList.remove('pv-flash'); void pv.offsetWidth; pv.classList.add('pv-flash');
      });
    });
    applyColors(); applyText();
  }
})();
