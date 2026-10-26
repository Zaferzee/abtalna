/* Entity branding page: live preview of the inherited + overridden branding (same app preview component as the central studio).
   Empty fields show the inherited value. Server-side validation in EntityBrandingController.Save. CSP: script-src 'self'. */
(function () {
  'use strict';
  var form = document.querySelector('[data-eb]');
  if (!form) return;
  var scope = form.querySelector('[data-eb-scope]'), vp = form.querySelector('[data-eb-viewport]');
  var HEX = /^#[0-9a-fA-F]{6}$/;
  var presets = JSON.parse(form.getAttribute('data-presets') || '[]');
  var VARS = { PrimaryColor: '--color-primary', SecondaryColor: '--color-secondary', AccentColor: '--color-accent', BackgroundColor: '--color-bg', SurfaceColor: '--color-surface',
    HeaderColor: '--color-header', HeaderTextColor: '--color-on-header', SidebarColor: '--color-sidebar', SidebarTextColor: '--color-on-sidebar', HeroTextColor: '--color-on-hero' };
  function lum(hex) { var c = [1, 3, 5].map(function (i) { var v = parseInt(hex.substr(i, 2), 16) / 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }); return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; }
  function on(hex) { return lum(hex) > 0.42 ? '#14213d' : '#ffffff'; }
  function color(n) { var el = form.querySelector('[data-eb-color="' + n + '"]'); var v = (el.value || '').trim(); return HEX.test(v) ? v : el.getAttribute('data-inherited'); }
  function render() {
    Object.keys(VARS).forEach(function (n) { var c = color(n); if (HEX.test(c || '')) scope.style.setProperty(VARS[n], c); });
    var p = color('PrimaryColor'), btn = HEX.test(color('ButtonColor') || '') ? color('ButtonColor') : p;
    scope.style.setProperty('--color-on-primary', on(p));
    scope.style.setProperty('--color-on-accent', on(color('AccentColor')));
    scope.style.setProperty('--color-button', btn);
    scope.style.setProperty('--color-on-button', HEX.test(color('ButtonTextColor') || '') ? color('ButtonTextColor') : on(btn));
    scope.style.setProperty('--color-surface-alt', 'color-mix(in srgb, ' + color('BackgroundColor') + ' 96%, ' + p + ')');
    form.querySelectorAll('[data-eb-swatch]').forEach(function (sw) {
      var n = sw.getAttribute('data-eb-swatch'), hex = sw.querySelector('[data-eb-color]'), pick = sw.querySelector('[data-eb-pick]');
      var v = hex.value.trim(); hex.classList.toggle('is-invalid', v !== '' && !HEX.test(v));
      if (HEX.test(color(n) || '') && pick.value.toLowerCase() !== color(n).toLowerCase()) pick.value = color(n);
      sw.classList.toggle('is-auto', v === '');
    });
    form.querySelectorAll('[data-eb-text]').forEach(function (t) {
      var n = t.getAttribute('data-eb-text'), v = t.value.trim() || t.getAttribute('placeholder') || '';
      scope.querySelectorAll('[data-b="' + n + '"]').forEach(function (el) { el.textContent = v; });
    });
    var orgFlag = form.querySelector('[data-eb-flag="ShowOrgInSidebar"]'), plate = form.querySelector('[data-eb-flag="LogoPlate"]');
    function flag(sel) { return (sel.value || sel.getAttribute('data-inherited')) === 'true'; }
    scope.querySelectorAll('.sb-names [data-b="OrgName"]').forEach(function (o) { o.hidden = !flag(orgFlag); });
    scope.querySelectorAll('[data-b-logo="sidebar"]').forEach(function (l) { l.classList.toggle('no-plate', !flag(plate)); });
  }
  function fit() { var s = Math.min(vp.parentElement.clientWidth / 1280, 1); vp.style.transform = 'scale(' + s + ')'; vp.parentElement.style.height = Math.round(800 * s) + 'px'; }
  form.addEventListener('input', render);
  form.addEventListener('change', render);
  form.querySelectorAll('[data-eb-pick]').forEach(function (p) { p.addEventListener('input', function () { form.querySelector('[data-eb-color="' + p.getAttribute('data-eb-pick') + '"]').value = p.value; form.querySelector('[data-eb-preset]').value = ''; render(); }); });
  form.querySelectorAll('[data-eb-inherit]').forEach(function (b) { b.addEventListener('click', function () { form.querySelector('[data-eb-color="' + b.getAttribute('data-eb-inherit') + '"]').value = ''; render(); }); });
  form.querySelectorAll('[data-preset-id]').forEach(function (card) {
    card.addEventListener('click', function () {
      var p = presets.filter(function (x) { return x.Id === card.getAttribute('data-preset-id'); })[0]; if (!p) return;
      Object.keys(p.Colors).forEach(function (k) { var el = form.querySelector('[data-eb-color="' + k + '"]'); if (el) el.value = p.Colors[k]; });
      form.querySelector('[data-eb-preset]').value = p.Id;
      form.querySelectorAll('[data-preset-id]').forEach(function (c) { c.classList.toggle('active', c === card); });
      render();
    });
  });
  // chosen logo files preview immediately (data: URLs; the CSP does not allow blob:)
  form.querySelectorAll('[data-eb-file]').forEach(function (inp) {
    inp.addEventListener('change', function () {
      var f = inp.files && inp.files[0]; if (!f) return;
      var rd = new FileReader();
      rd.onload = function () {
        var kind = inp.getAttribute('data-eb-file'), url = String(rd.result);
        var tile = inp.closest('[data-eb-asset]').querySelector('[data-eb-img]'); tile.src = url; tile.hidden = false;
        var w = scope.querySelector(kind === 'logo' ? '[data-b-logo="sidebar"]' : '[data-b-logo="icon"]');
        if (w) { w.querySelector('img').src = url; w.hidden = false; if (kind === 'logo') { var ic = scope.querySelector('[data-b-logo="icon"]'); if (ic) ic.hidden = true; } var m = scope.querySelector('.sb-brand [data-b-mono]'); if (m) m.hidden = true; }
      };
      rd.readAsDataURL(f);
    });
  });
  if ('ResizeObserver' in window) new ResizeObserver(fit).observe(vp.parentElement); else window.addEventListener('resize', fit);
  render(); fit();
})();
