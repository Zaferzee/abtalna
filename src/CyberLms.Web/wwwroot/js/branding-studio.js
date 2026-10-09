/* Branding & Appearance studio (استوديو الهوية والمظهر): live preview, presets, image controls, readability checks and the
   save/discard workflow. Progressive enhancement: without this script the form still saves. Nothing here is trusted by the
   server (SettingsController.SaveBranding validates every value again). CSP: script-src 'self'. */
(function () {
  'use strict';
  var form = document.querySelector('[data-studio]');
  if (!form) return;
  var $ = function (s, c) { return (c || form).querySelector(s); };
  var $$ = function (s, c) { return Array.prototype.slice.call((c || form).querySelectorAll(s)); };
  var scope = $('[data-pv-scope]'), auth = $('[data-pv-auth]'), hero = auth && auth.querySelector('[data-b-hero]');
  var HEX = /^#[0-9a-fA-F]{6}$/;
  var presets = JSON.parse(form.getAttribute('data-presets') || '[]');
  var defaults = JSON.parse(form.getAttribute('data-defaults') || '{}');
  var t = function (k) { return form.getAttribute('data-t-' + k) || ''; };
  var field = function (n) { return form.elements[n]; };
  var val = function (n) {
    var el = field(n); if (!el) return '';
    if (el instanceof RadioNodeList) { return el.value || ''; }
    return el.type === 'checkbox' ? (el.checked ? 'true' : 'false') : el.value;
  };
  var flag = function (n) { var els = $$('input[type=checkbox][name="' + n + '"]'); return els.length ? els[0].checked : true; };

  // ---------- color math (same rules as the server: WCAG relative luminance) ----------
  function lum(hex) { var c = [1, 3, 5].map(function (i) { var v = parseInt(hex.substr(i, 2), 16) / 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }); return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; }
  function ratio(a, b) { var x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); }
  function onColor(hex) { return lum(hex) > 0.42 ? '#14213d' : '#ffffff'; }
  function color(n) { var v = (val(n) || '').trim(); return HEX.test(v) ? v : (defaults[n] && HEX.test(defaults[n]) ? defaults[n] : ''); }
  function buttonColor() { return color('ButtonColor') || color('PrimaryColor'); }
  function buttonText() { return color('ButtonTextColor') || onColor(buttonColor()); }

  // ---------- editor sections ----------
  var tabs = $$('[data-studio-tab]');
  function showTab(id) {
    tabs.forEach(function (b) { b.classList.toggle('active', b.getAttribute('data-studio-tab') === id); b.setAttribute('aria-selected', b.getAttribute('data-studio-tab') === id); });
    $$('[data-studio-panel]').forEach(function (p) { p.hidden = p.getAttribute('data-studio-panel') !== id; });
    try { sessionStorage.setItem('studio-tab', id); } catch (e) { /* not essential */ }
    if (id === 'media' || id === 'text' || id === 'logos') pvTab('login'); else if (id === 'colors') { /* keep the current preview */ }
  }
  tabs.forEach(function (b) { b.addEventListener('click', function () { showTab(b.getAttribute('data-studio-tab')); }); });
  try { var last = sessionStorage.getItem('studio-tab'); if (last && $('[data-studio-tab="' + last + '"]')) showTab(last); } catch (e) { /* default tab */ }

  // ---------- preview: tabs, device, scaling, enlarge ----------
  var panel = $('[data-preview-panel]'), device = 'desktop', current = 'login';
  function pvTab(id) {
    current = id;
    $$('[data-pv-tab]').forEach(function (b) { b.classList.toggle('active', b.getAttribute('data-pv-tab') === id); });
    $$('[data-pv]').forEach(function (d) { d.hidden = d.getAttribute('data-pv') !== id; });
    fit();
  }
  $$('[data-pv-tab]').forEach(function (b) { b.addEventListener('click', function () { pvTab(b.getAttribute('data-pv-tab')); }); });
  $$('[data-device]').forEach(function (b) {
    b.addEventListener('click', function () {
      device = b.getAttribute('data-device');
      $$('[data-device]').forEach(function (x) { x.classList.toggle('active', x === b); });
      $$('[data-pv]').forEach(function (d) {
        d.setAttribute('data-device-frame', device);
        var vp = d.querySelector('[data-pv-viewport]');
        vp.style.setProperty('--vw', device === 'mobile' ? '390px' : '1280px');
        vp.style.setProperty('--vh', device === 'mobile' ? '780px' : '800px');
      });
      if (auth) auth.classList.toggle('is-compact', device === 'mobile');
      var shell = $('.pv-shell'); if (shell) shell.classList.toggle('is-compact', device === 'mobile');
      fit();
    });
  });
  function fit() {
    $$('[data-pv]').forEach(function (d) {
      if (d.hidden) return;
      var vp = d.querySelector('[data-pv-viewport]');
      var vw = parseFloat(getComputedStyle(vp).getPropertyValue('--vw')) || 1280, vh = parseFloat(getComputedStyle(vp).getPropertyValue('--vh')) || 800;
      var avail = d.clientWidth || scope.clientWidth;
      var maxH = panel.classList.contains('is-expanded') ? window.innerHeight - 170 : (device === 'mobile' ? 560 : 9999);
      var s = Math.min(avail / vw, maxH / vh, 1);
      vp.style.transform = 'scale(' + s + ')';
      d.style.height = Math.round(vh * s) + 'px';
      d.style.width = device === 'mobile' ? Math.round(vw * s) + 'px' : '';
    });
  }
  if ('ResizeObserver' in window) new ResizeObserver(fit).observe(scope); else window.addEventListener('resize', fit);
  var expand = $('[data-pv-expand]');
  if (expand) expand.addEventListener('click', function () { panel.classList.toggle('is-expanded'); document.body.classList.toggle('studio-expanded', panel.classList.contains('is-expanded')); fit(); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && panel.classList.contains('is-expanded')) expand.click(); });

  // ---------- text, flags and choices ----------
  function setText(name, text) { $$('[data-b="' + name + '"]', scope).forEach(function (el) { el.textContent = text; }); }
  function textOf(name) { var el = field(name); if (!el) return ''; var v = el.value.trim(); return v || (el.getAttribute('placeholder') || ''); }
  function inline(s) { return s.replace(/\s*\n\s*/g, ' ').trim(); }
  function renderText() {
    ['OrgName', 'SystemName', 'LoginBadge', 'LoginHeroDescription', 'LoginFeature1', 'LoginFeature2', 'LoginFeature3', 'LoginTitle', 'LoginCardSubtitle', 'LoginSubtitle', 'SupportText', 'FooterText', 'WelcomeText']
      .forEach(function (n) { setText(n, textOf(n)); });
    var title = field('LoginHeroTitle').value.trim() || field('SystemName').value.trim() || field('LoginHeroTitle').getAttribute('placeholder');
    setText('LoginHeroTitle', title);
    setText('OrgNameInline', inline(textOf('OrgName')));
    var mono = (inline(field('SystemName').value) || '•').charAt(0);
    $$('[data-b-mono]', scope).forEach(function (m) { m.textContent = mono; });
    $$('[data-b-wrap]', scope).forEach(function (w) { w.hidden = !textOf(w.getAttribute('data-b-wrap')); });
  }
  function renderOptions() {
    if (!auth) return;
    var layout = val('LoginLayout') || 'split', align = val('LoginTextAlign') || 'start', place = val('LoginLogoPlacement') || 'hero';
    auth.className = auth.className.replace(/\b(layout|align|logo)-[\w-]+/g, '').trim();
    auth.classList.add('layout-' + layout, 'align-' + align, 'logo-' + place);
    auth.classList.toggle('no-names', !flag('ShowNamesOnLogin'));
    auth.classList.toggle('no-badge', !flag('ShowLoginBadge'));
    auth.classList.toggle('no-features', !flag('ShowLoginFeatures'));
    auth.classList.toggle('no-decor', !flag('LoginDecor'));
    $$('.sb-names [data-b="OrgName"]', scope).forEach(function (o) { o.hidden = !flag('ShowOrgInSidebar'); });
    $$('[data-b-logo="sidebar"]', scope).forEach(function (l) { l.classList.toggle('no-plate', !flag('LogoPlate')); });
    $$('[data-b-logo="login"]', scope).forEach(function (l) { l.classList.toggle('plate', flag('LoginLogoPlate')); });
    $$('.st-seg label, .pos-grid label, .pos-custom').forEach(function (l) { var r = l.querySelector('input'); l.classList.toggle('active', !!(r && r.checked)); });
    $$('[data-out-for]').forEach(function (o) { o.textContent = val(o.getAttribute('data-out-for')) + '%'; });
  }

  // ---------- assets (upload / remove / revert) ----------
  var assets = {};
  $$('[data-asset]').forEach(function (tile) {
    var kind = tile.getAttribute('data-asset'), img = tile.querySelector('[data-asset-img]'), fileIn = tile.querySelector('[data-asset-file]');
    var a = assets[kind] = { tile: tile, img: img, file: fileIn, flag: tile.querySelector('[data-asset-remove-flag]'), saved: img.getAttribute('data-saved') || '', url: null };
    // Preview of a chosen (not yet saved) file. The site CSP allows img-src 'self' data: (not blob:), so a data URL is used.
    fileIn.addEventListener('change', function () {
      var f = fileIn.files && fileIn.files[0];
      a.flag.value = '';
      if (!f) { a.url = null; renderAssets(); changed(); return; }
      var rd = new FileReader(), token = a.token = {};
      rd.onload = function () { if (a.token !== token) return; a.url = String(rd.result); renderAssets(); changed(); };
      a.url = 'pending'; rd.readAsDataURL(f);
    });
    tile.querySelector('[data-asset-remove]').addEventListener('click', function () { fileIn.value = ''; a.token = null; a.url = null; a.flag.value = 'true'; renderAssets(); changed(); });
    tile.querySelector('[data-asset-revert]').addEventListener('click', function () { revertAsset(kind); changed(); });
  });
  function revertAsset(kind) { var a = assets[kind]; if (!a) return; a.file.value = ''; a.token = null; a.url = null; a.flag.value = ''; renderAssets(); }
  /** Effective image of an asset: new file, saved file, or none (removed / never uploaded). */
  function src(kind) { var a = assets[kind]; if (!a) return ''; if (a.url && a.url !== 'pending') return a.url; if (a.flag.value === 'true') return ''; return a.saved; }
  function showImg(sel, url) { $$(sel, scope).forEach(function (w) { var i = w.querySelector('img'); if (url) { i.src = url; w.hidden = false; } else { i.removeAttribute('src'); w.hidden = true; } }); }
  function renderAssets() {
    Object.keys(assets).forEach(function (kind) {
      var a = assets[kind], url = src(kind), state = a.url ? 'new' : a.flag.value === 'true' ? 'remove' : a.saved ? 'saved' : 'none';
      if (url) { a.img.src = url; a.img.hidden = false; } else { a.img.hidden = true; }
      a.tile.querySelector('.at-empty').hidden = !!url;
      var st = a.tile.querySelector('[data-asset-state]'); st.textContent = st.getAttribute('data-t-' + state); st.className = 'at-state is-' + state;
      a.tile.querySelector('[data-asset-remove]').hidden = !(a.saved || a.url) || state === 'remove';
      a.tile.querySelector('[data-asset-revert]').hidden = state === 'saved' || state === 'none';
    });
    var logo = src('logo'), icon = src('icon'), login = src('loginlogo') || logo, place = val('LoginLogoPlacement') || 'hero';
    showImg('[data-b-logo="sidebar"]', logo);
    showImg('[data-b-logo="icon"]', !logo && icon ? icon : '');
    $$('.sb-names', scope).forEach(function (n) { var m = n.parentNode.querySelector('[data-b-mono]'); if (m) m.hidden = !!(logo || icon); });
    showImg('[data-b-logo="login"]', login);
    showImg('[data-b-logo="card"]', login && place !== 'hero' ? login : '');
    if (hero) { var hm = hero.querySelector('[data-b-mono]'); if (hm) hm.hidden = !!login && place !== 'card'; }
    var bg = src('loginbg');
    if (hero) { if (bg) hero.style.setProperty('--img', 'url("' + bg + '")'); else hero.style.removeProperty('--img'); }
    if (auth) auth.classList.toggle('has-image', !!bg);
    var fimg = $('[data-focal-img]'); if (fimg) { if (bg) { fimg.src = bg; fimg.hidden = false; } else fimg.hidden = true; }
    var fe = $('.focal-empty'); if (fe) fe.hidden = !!bg;
    renderMedia();
  }

  // ---------- login image controls ----------
  var POS = { center: '50% 50%', top: '50% 0%', bottom: '50% 100%', left: '0% 50%', right: '100% 50%', 'top-left': '0% 0%', 'top-right': '100% 0%', 'bottom-left': '0% 100%', 'bottom-right': '100% 100%' };
  var FIT = { cover: 'cover', contain: 'contain', fill: '100% 100%', auto: 'auto' };
  function renderMedia() {
    if (!hero) return;
    var pos = val('LoginImagePosition') || 'center';
    var p = pos === 'custom' ? val('LoginFocusX') + '% ' + val('LoginFocusY') + '%' : (POS[pos] || '50% 50%');
    hero.style.setProperty('--img-pos', p);
    hero.style.setProperty('--img-size', FIT[val('LoginImageFit')] || 'cover');
    hero.style.setProperty('--img-zoom', (parseInt(val('LoginImageZoom'), 10) || 100) / 100);
    hero.style.setProperty('--ov', color('LoginOverlayColor'));
    hero.style.setProperty('--ov-op', (parseInt(val('LoginOverlayOpacity'), 10) || 0) / 100);
    hero.style.setProperty('--hero-text', color('LoginTextColor'));
    var dot = $('[data-focal-dot]'), xy = p.split(' ');
    if (dot) { dot.style.left = xy[0]; dot.style.top = xy[1]; }
  }
  var focal = $('[data-focal]');
  if (focal) focal.addEventListener('click', function (e) {
    var img = $('[data-focal-img]'); if (!img || img.hidden) return;
    var r = img.getBoundingClientRect();
    var x = Math.round(Math.min(100, Math.max(0, (e.clientX - r.left) / r.width * 100)));
    var y = Math.round(Math.min(100, Math.max(0, (e.clientY - r.top) / r.height * 100)));
    field('LoginFocusX').value = x; field('LoginFocusY').value = y;
    $$('input[name="LoginImagePosition"]').forEach(function (r2) { r2.checked = r2.value === 'custom'; });
    renderOptions(); renderMedia(); changed();
  });

  // ---------- colors ----------
  var VARS = { PrimaryColor: '--color-primary', SecondaryColor: '--color-secondary', AccentColor: '--color-accent', BackgroundColor: '--color-bg', SurfaceColor: '--color-surface',
    HeaderColor: '--color-header', HeaderTextColor: '--color-on-header', SidebarColor: '--color-sidebar', SidebarTextColor: '--color-on-sidebar', HeroTextColor: '--color-on-hero',
    LoginBackgroundColor: '--color-login-bg', LoginTextColor: '--color-login-text' };
  function renderColors() {
    Object.keys(VARS).forEach(function (n) { var c = color(n); if (c) scope.style.setProperty(VARS[n], c); });
    scope.style.setProperty('--color-on-primary', onColor(color('PrimaryColor')));
    scope.style.setProperty('--color-on-accent', onColor(color('AccentColor')));
    scope.style.setProperty('--color-surface-alt', 'color-mix(in srgb, ' + color('BackgroundColor') + ' 96%, ' + color('PrimaryColor') + ')');
    scope.style.setProperty('--color-button', buttonColor());
    scope.style.setProperty('--color-on-button', buttonText());
    $$('[data-swatch]').forEach(function (sw) {
      var n = sw.getAttribute('data-swatch'), hex = sw.querySelector('[data-b-color]'), pick = sw.querySelector('[data-color-for]');
      var v = hex.value.trim(), ok = v === '' ? sw.classList.contains('is-optional') : HEX.test(v);
      hex.classList.toggle('is-invalid', !ok);
      var shown = HEX.test(v) ? v : (n === 'ButtonColor' ? buttonColor() : n === 'ButtonTextColor' ? buttonText() : pick.value);
      if (pick.value.toLowerCase() !== shown.toLowerCase()) pick.value = shown;
      sw.classList.toggle('is-auto', sw.classList.contains('is-optional') && v === '');
    });
    renderMedia(); contrast();
  }
  $$('[data-swatch]').forEach(function (sw) {
    var hex = sw.querySelector('[data-b-color]'), pick = sw.querySelector('[data-color-for]'), auto = sw.querySelector('[data-color-auto]');
    pick.addEventListener('input', function () { hex.value = pick.value; manualColor(); });
    hex.addEventListener('input', manualColor);
    if (auto) auto.addEventListener('click', function () { hex.value = ''; manualColor(); });
  });
  function manualColor() { setPreset(''); renderColors(); changed(); }
  function setPreset(id) {
    field('Preset').value = id;
    $$('[data-preset-id]').forEach(function (c) { c.classList.toggle('active', c.getAttribute('data-preset-id') === id); });
  }
  $$('[data-preset-id]').forEach(function (card) {
    card.addEventListener('click', function () {
      var p = presets.filter(function (x) { return x.Id === card.getAttribute('data-preset-id'); })[0]; if (!p) return;
      Object.keys(p.Colors).forEach(function (k) { var el = field(k); if (el) el.value = p.Colors[k]; });
      setPreset(p.Id); renderColors(); changed();
      scope.classList.remove('pv-flash'); void scope.offsetWidth; scope.classList.add('pv-flash');
    });
  });

  // ---------- readability checks ----------
  function contrast() {
    var checks = {
      header: [color('HeaderColor'), color('HeaderTextColor')], sidebar: [color('SidebarColor'), color('SidebarTextColor')],
      button: [buttonColor(), buttonText()], hero: [color('PrimaryColor'), color('HeroTextColor')],
      login: [src('loginbg') ? color('LoginOverlayColor') : color('PrimaryColor'), color('LoginTextColor')],
      page: [color('BackgroundColor'), '#14213d'], surface: [color('SurfaceColor'), '#14213d'],
    }, any = false;
    Object.keys(checks).forEach(function (k) {
      var row = $('[data-check="' + k + '"]'); if (!row) return;
      var r = ratio(checks[k][0], checks[k][1]), cls = r >= 4.5 ? 'ok' : r >= 3 ? 'weak' : 'low';
      if (r < 4.5) any = true;
      row.className = 'cl-row is-' + cls;
      row.querySelector('.cl-ratio').textContent = r.toFixed(1) + ':1';
      row.querySelector('.cl-state').innerHTML = '<i class="bi ' + (cls === 'ok' ? 'bi-check-circle-fill' : 'bi-exclamation-triangle-fill') + '"></i> ' + t(cls === 'ok' ? 'ok' : cls === 'weak' ? 'weak' : 'low');
    });
    var imgNote = $('[data-check-note="image"]'); if (imgNote) imgNote.hidden = !(src('loginbg') && (parseInt(val('LoginOverlayOpacity'), 10) || 0) < 35);
    var anyNote = $('[data-check-note="any"]'); if (anyNote) anyNote.hidden = !any;
  }

  // ---------- unsaved changes, discard, revert ----------
  var initial = {};
  $$('input, textarea, select').forEach(function (el) {
    if (!el.name || el.type === 'file') return;
    initial[el.id || (el.name + '|' + el.value)] = { el: el, value: el.value, checked: el.checked };
  });
  function snapshot() {
    var parts = [];
    $$('input, textarea, select').forEach(function (el) {
      if (!el.name || el.type === 'file') return;
      if ((el.type === 'checkbox' || el.type === 'radio') && !el.checked) return;
      parts.push(el.name + '=' + el.value);
    });
    Object.keys(assets).forEach(function (k) { parts.push(k + ':' + (assets[k].url ? 'new' : assets[k].flag.value)); });
    return parts.join('&');
  }
  var clean = snapshot(), dirty = false, submitting = false;
  function changed() {
    dirty = snapshot() !== clean;
    [$('[data-studio-status]'), $('[data-studio-bar-state]')].forEach(function (s) {
      if (!s) return;
      s.classList.toggle('is-dirty', dirty);
      s.querySelector('i').className = 'bi ' + (dirty ? 'bi-pencil-square' : 'bi-check-circle');
      s.querySelector('span').textContent = t(dirty ? 'dirty' : 'clean');
    });
    $('[data-studio-discard]').disabled = !dirty;
  }
  function restore(filter) {
    Object.keys(initial).forEach(function (k) {
      var s = initial[k]; if (filter && !filter(s.el)) return;
      if (s.el.type === 'checkbox' || s.el.type === 'radio') s.el.checked = s.checked; else s.el.value = s.value;
    });
  }
  $('[data-studio-discard]').addEventListener('click', function () {
    restore(); Object.keys(assets).forEach(revertAsset);
    setPreset(field('Preset').getAttribute('data-initial') || '');
    renderAll(); changed();
  });
  var COLOR_FIELDS = Object.keys(defaults);
  var MEDIA_FIELDS = ['LoginImageFit', 'LoginImagePosition', 'LoginImageZoom', 'LoginFocusX', 'LoginFocusY', 'LoginOverlayOpacity', 'LoginOverlayColor', 'LoginTextColor', 'LoginBackgroundColor', 'LoginLayout', 'LoginTextAlign', 'LoginDecor'];
  $$('[data-revert-group]').forEach(function (b) {
    b.addEventListener('click', function () {
      var g = b.getAttribute('data-revert-group');
      if (g === 'colors') { restore(function (el) { return COLOR_FIELDS.indexOf(el.name) >= 0 || el.name === 'Preset'; }); setPreset(field('Preset').value); }
      else { restore(function (el) { return MEDIA_FIELDS.indexOf(el.name) >= 0; }); revertAsset('loginbg'); }
      renderAll(); changed();
    });
  });
  form.addEventListener('submit', function () { submitting = true; });
  window.addEventListener('beforeunload', function (e) { if (dirty && !submitting) { e.preventDefault(); e.returnValue = t('leave'); } });

  // ---------- wiring ----------
  form.addEventListener('input', function (e) {
    var n = e.target.name;
    if (e.target.matches('[data-b-input]')) renderText();
    if (e.target.matches('[data-b-number]')) { renderOptions(); renderMedia(); contrast(); }
    if (n) changed();
  });
  form.addEventListener('change', function (e) {
    if (e.target.matches('[data-b-choice], [data-b-flag]')) { renderOptions(); renderAssets(); renderMedia(); contrast(); }
    if (e.target.name) changed();
  });
  function renderAll() { renderText(); renderOptions(); renderAssets(); renderColors(); }
  renderAll(); pvTab('login'); changed();
})();
