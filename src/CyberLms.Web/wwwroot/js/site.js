(function () {
  // Confirm dialogs for destructive forms: <form data-confirm="Are you sure?">
  document.addEventListener('submit', function (e) {
    var msg = e.target.getAttribute && e.target.getAttribute('data-confirm');
    if (msg && !window.confirm(msg)) e.preventDefault();
  });

  // Rich text editor (Quill): <div data-quill-for="InputId">
  document.querySelectorAll('[data-quill-for]').forEach(function (el) {
    if (typeof Quill === 'undefined') return;
    var input = document.getElementById(el.getAttribute('data-quill-for'));
    var q = new Quill(el, {
      theme: 'snow',
      modules: { toolbar: [[{ header: [2, 3, false] }], ['bold', 'italic', 'underline'], [{ list: 'ordered' }, { list: 'bullet' }], [{ align: [] }, { direction: 'rtl' }], ['link', 'blockquote'], ['clean']] }
    });
    q.root.innerHTML = input.value || '';
    input.form.addEventListener('submit', function () { input.value = q.getText().trim().length ? q.root.innerHTML : ''; });
  });

  // Question form: show the section matching the selected type
  var type = document.getElementById('qtype');
  if (type) {
    var apply = function () {
      var tf = type.value === '2';
      document.getElementById('tf-section').classList.toggle('d-none', !tf);
      document.getElementById('opt-section').classList.toggle('d-none', tf);
      var multi = type.value === '3';
      document.querySelectorAll('.opt-correct').forEach(function (i) { i.type = multi ? 'checkbox' : 'radio'; });
    };
    type.addEventListener('change', apply); apply();
  }

  // Live color preview helpers: <input type=color data-hex-for="TextId">
  document.querySelectorAll('input[type=color]').forEach(function (c) {
    var t = document.getElementById(c.getAttribute('data-hex-for'));
    if (!t) return;
    c.addEventListener('input', function () { t.value = c.value; });
    t.addEventListener('input', function () { if (/^#[0-9a-fA-F]{6}$/.test(t.value)) c.value = t.value; });
  });
})();
// Buttons with data-confirm-click ask before submitting (CSP forbids inline onclick handlers)
document.addEventListener('click', function (e) {
  var t = e.target.closest && e.target.closest('[data-confirm-click]');
  if (t && !window.confirm(t.getAttribute('data-confirm-click'))) { e.preventDefault(); e.stopPropagation(); }
}, true);
