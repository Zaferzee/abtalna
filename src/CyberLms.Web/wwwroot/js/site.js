(function () {
  'use strict';
  var body = document.body;
  var t = function (k) { return body.getAttribute('data-t-' + k) || ''; };
  var rtl = document.documentElement.dir === 'rtl';

  // All validation is done on the server so messages are shown in the application language (not the browser's).
  document.querySelectorAll('form').forEach(function (f) { f.noValidate = true; });

  // ---- Arabic confirmation dialog (replaces the browser's confirm()) ----
  var modalEl = document.getElementById('confirmModal');
  var modal = modalEl && window.bootstrap ? new bootstrap.Modal(modalEl) : null;
  var pending = null;
  function ask(message, onOk) {
    if (!modal) { if (window.confirm(message)) onOk(); return; }
    document.getElementById('confirmModalText').textContent = message;
    pending = onOk; modal.show();
  }
  if (modalEl) document.getElementById('confirmModalOk').addEventListener('click', function () {
    var fn = pending; pending = null; modal.hide(); if (fn) fn();
  });
  document.addEventListener('submit', function (e) {
    var form = e.target, msg = form.getAttribute && form.getAttribute('data-confirm');
    if (!msg || form.__confirmed) return;
    e.preventDefault();
    var submitter = e.submitter;
    ask(msg, function () { form.__confirmed = true; if (submitter && form.requestSubmit) form.requestSubmit(submitter); else form.submit(); });
  });
  document.addEventListener('click', function (e) {
    var btn = e.target.closest && e.target.closest('[data-confirm-click]');
    if (!btn || btn.__confirmed) return;
    e.preventDefault(); e.stopPropagation();
    ask(btn.getAttribute('data-confirm-click'), function () { btn.__confirmed = true; btn.click(); });
  }, true);

  // ---- File inputs with Arabic labels (the native control uses the browser's language) ----
  document.querySelectorAll('input[type=file]').forEach(function (input) {
    var wrap = document.createElement('div'); wrap.className = 'input-group file-picker';
    var btn = document.createElement('button'); btn.type = 'button'; btn.className = 'btn btn-outline-secondary';
    btn.textContent = input.multiple ? t('choose-files') : t('choose-file');
    var label = document.createElement('span'); label.className = 'form-control text-muted bidi'; label.textContent = t('no-file');
    wrap.appendChild(btn); wrap.appendChild(label);
    input.classList.add('d-none'); input.parentNode.insertBefore(wrap, input);
    btn.addEventListener('click', function () { input.click(); });
    input.addEventListener('change', function () {
      var n = input.files ? input.files.length : 0;
      label.textContent = n === 0 ? t('no-file') : n === 1 ? input.files[0].name : t('files-count').replace('{0}', n);
    });
  });

  // ---- Rich-text editor (Quill) with RTL defaults, Arabic tooltips and image upload ----
  document.querySelectorAll('[data-quill-for]').forEach(function (el) {
    if (typeof Quill === 'undefined') return;
    var input = document.getElementById(el.getAttribute('data-quill-for'));
    var form = input.form;
    var tips = {}; try { tips = JSON.parse(el.getAttribute('data-quill-tips') || '{}'); } catch (e) { }
    var q = new Quill(el, {
      theme: 'snow',
      modules: {
        toolbar: {
          container: [[{ header: [2, 3, false] }], ['bold', 'italic', 'underline'], [{ list: 'ordered' }, { list: 'bullet' }], [{ align: [] }, { direction: 'rtl' }], ['link', 'blockquote', 'image'], ['clean']],
          handlers: {
            image: function () {
              var f = document.createElement('input'); f.type = 'file'; f.accept = 'image/png,image/jpeg,image/gif,image/webp';
              f.onchange = function () {
                if (!f.files.length) return;
                var data = new FormData(); data.append('file', f.files[0]);
                var token = form.querySelector('input[name=__RequestVerificationToken]');
                fetch(form.getAttribute('data-upload-url'), { method: 'POST', body: data, headers: { 'RequestVerificationToken': token ? token.value : '' }, credentials: 'same-origin' })
                  .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
                  .then(function (r) {
                    if (!r.ok) { alert(r.j.error || t('upload-failed')); return; }
                    var range = q.getSelection(true); q.insertEmbed(range.index, 'image', r.j.url, 'user'); q.setSelection(range.index + 1);
                  }).catch(function () { alert(t('upload-failed')); });
              };
              f.click();
            }
          }
        }
      }
    });
    if (rtl) q.root.setAttribute('dir', 'rtl');
    {
      Object.keys(tips).forEach(function (sel) {
        var cls = sel.split('[')[0], val = sel.indexOf('[') > 0 ? sel.slice(sel.indexOf('[')) : '';
        var node = q.container.previousSibling && q.container.previousSibling.querySelector('button.' + cls + val);
        if (node) { node.title = tips[sel]; node.setAttribute('aria-label', tips[sel]); }
      });
    }
    if (input.value) q.setContents(q.clipboard.convert({ html: input.value }), 'silent');
    form.addEventListener('submit', function () {
      var hasImg = q.root.querySelector('img');
      input.value = (q.getText().trim().length || hasImg) ? q.getSemanticHTML().replace(/&nbsp;/g, ' ') : '';
    });
  });

  // ---- Question form: show the section matching the selected type ----
  var type = document.getElementById('qtype');
  if (type) {
    var apply = function () {
      var tf = type.value === '2', multi = type.value === '3';
      document.getElementById('tf-section').classList.toggle('d-none', !tf);
      document.getElementById('opt-section').classList.toggle('d-none', tf);
      document.querySelectorAll('.opt-correct').forEach(function (i) { i.type = multi ? 'checkbox' : 'radio'; });
    };
    type.addEventListener('change', apply); apply();
  }

  // ---- Color pickers keep their hex text box in sync ----
  document.querySelectorAll('input[type=color]').forEach(function (c) {
    var tb = document.getElementById(c.getAttribute('data-hex-for'));
    if (!tb) return;
    c.addEventListener('input', function () { tb.value = c.value; });
    tb.addEventListener('input', function () { if (/^#[0-9a-fA-F]{6}$/.test(tb.value)) c.value = tb.value; });
  });
})();
