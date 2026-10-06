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
  document.querySelectorAll('input[type=file]:not([data-native])').forEach(function (input) {
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

  // ---- Rich-text editor (Quill 2) with RTL defaults, Arabic tooltips, tables, safe paste (Word) and image upload ----
  // Only these formats survive typing or pasting; anything else from Word (fonts, colours, sizes, scripts) is dropped.
  // The server sanitizes the HTML again on save (ContentAuthoring.CleanBody).
  var EDITOR_FORMATS = ['header', 'bold', 'italic', 'underline', 'list', 'indent', 'align', 'direction', 'link', 'blockquote', 'image', 'table'];
  var ICONS = { 'table-insert': 'bi-table', 'table-row': 'bi-plus-square', 'table-col': 'bi-layout-three-columns', 'table-row-del': 'bi-dash-square', 'table-col-del': 'bi-x-square', 'table-del': 'bi-trash3' };
  document.querySelectorAll('[data-quill-for]').forEach(function (el) {
    if (typeof Quill === 'undefined') return;
    var input = document.getElementById(el.getAttribute('data-quill-for'));
    var form = input.form;
    var tips = {}; try { tips = JSON.parse(el.getAttribute('data-quill-tips') || '{}'); } catch (e) { }
    var q;
    var table = function () { return q.getModule('table'); };
    var inTable = function (fn) { return function () { q.focus(); var t = table(); if (t && t.getTable()[0]) fn(t); }; };
    // Small grid picker for the table size (no browser prompt).
    var picker = null;
    function tablePicker(btn) {
      if (picker) { picker.remove(); picker = null; return; }
      var range = q.getSelection(true);
      picker = document.createElement('div'); picker.className = 'tbl-picker'; picker.setAttribute('role', 'dialog');
      var label = document.createElement('div'); label.className = 'tbl-picker-label'; label.textContent = '3 × 3';
      var grid = document.createElement('div'); grid.className = 'tbl-picker-grid';
      for (var r = 1; r <= 8; r++) for (var c = 1; c <= 6; c++) {
        var cell = document.createElement('button'); cell.type = 'button'; cell.dataset.r = r; cell.dataset.c = c;
        cell.setAttribute('aria-label', r + ' × ' + c); grid.appendChild(cell);
      }
      grid.addEventListener('mouseover', function (e) {
        var t = e.target.dataset; if (!t.r) return;
        label.textContent = t.r + ' × ' + t.c;
        grid.querySelectorAll('button').forEach(function (b) { b.classList.toggle('on', +b.dataset.r <= +t.r && +b.dataset.c <= +t.c); });
      });
      grid.addEventListener('click', function (e) {
        var t = e.target.dataset; if (!t.r) return;
        q.setSelection(range); table().insertTable(+t.r, +t.c);
        picker.remove(); picker = null;
      });
      picker.appendChild(grid); picker.appendChild(label);
      btn.parentNode.appendChild(picker);
    }
    document.addEventListener('mousedown', function (e) { if (picker && !picker.contains(e.target) && !e.target.closest('.ql-table-insert')) { picker.remove(); picker = null; } });
    q = new Quill(el, {
      theme: 'snow',
      placeholder: el.getAttribute('data-placeholder') || '',
      formats: EDITOR_FORMATS,
      modules: {
        table: true,
        toolbar: {
          container: [[{ header: [2, 3, false] }], ['bold', 'italic', 'underline'], [{ list: 'ordered' }, { list: 'bullet' }], [{ align: [] }, { direction: 'rtl' }],
            ['link', 'blockquote', 'image'], ['table-insert', 'table-row', 'table-col', 'table-row-del', 'table-col-del', 'table-del'], ['clean']],
          handlers: {
            'table-insert': function () { tablePicker(this.container.querySelector('.ql-table-insert')); },
            'table-row': inTable(function (t) { t.insertRowBelow(); }),
            'table-col': inTable(function (t) { t.insertColumnRight(); }),
            'table-row-del': inTable(function (t) { t.deleteRow(); }),
            'table-col-del': inTable(function (t) { t.deleteColumn(); }),
            'table-del': inTable(function (t) { t.deleteTable(); }),
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
    var bar = q.container.previousSibling;
    if (bar) {
      bar.setAttribute('aria-label', t('quill'));
      Object.keys(ICONS).forEach(function (k) {
        var b = bar.querySelector('button.ql-' + k);
        if (b) { b.innerHTML = '<i class="bi ' + ICONS[k] + '" aria-hidden="true"></i>'; b.classList.add('ql-ext'); }
      });
      Object.keys(tips).forEach(function (sel) {
        var cls = sel.split('[')[0], val = sel.indexOf('[') > 0 ? sel.slice(sel.indexOf('[')) : '';
        bar.querySelectorAll('.' + cls + val).forEach(function (node) { node.title = tips[sel]; node.setAttribute('aria-label', tips[sel]); });
      });
    }
    if (input.value) q.setContents(q.clipboard.convert({ html: input.value }), 'silent');
    // Keep the hidden field current (also lets the page warn about unsaved changes).
    var sync = function () {
      var hasImg = q.root.querySelector('img');
      input.value = (q.getText().trim().length || hasImg) ? q.getSemanticHTML().replace(/&nbsp;/g, ' ') : '';
    };
    q.on('text-change', function (d, o, source) { sync(); if (source === 'user') input.dispatchEvent(new Event('input', { bubbles: true })); });
    form.addEventListener('submit', sync);
    el.__quill = q;
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
