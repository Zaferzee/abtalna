// In-page PDF reader for content pages, built on the locally bundled PDF.js (wwwroot/lib/pdfjs): no CDN, no internet.
// The document is fetched from the same authenticated endpoint as any attachment (/Files/Attachment/{id}), with the
// user's session cookie, and handed to PDF.js as bytes; no public or alternative file URL exists.
// CSP-compatible: module script from 'self', worker from 'self', isEvalSupported = false.
import * as pdfjsLib from '../lib/pdfjs/pdf.min.mjs';

const LIB = new URL('../lib/pdfjs/', import.meta.url).href;
pdfjsLib.GlobalWorkerOptions.workerSrc = LIB + 'pdf.worker.min.mjs';
const CSS_UNITS = 96 / 72;                 // PDF points -> CSS pixels at 100 %
const ZOOMS = [0.5, 0.75, 1, 1.25, 1.5, 2, 3];
const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

class PdfViewer {
  constructor(root) {
    this.root = root;
    this.$ = s => root.querySelector(s);
    this.stage = this.$('[data-pdf-stage]');
    this.pagesEl = this.$('[data-pdf-pages]');
    this.state = this.$('[data-pdf-state]');
    this.t = k => root.getAttribute('data-t-' + k) || '';
    this.zoom = 'fit';
    this.doc = null; this.pages = []; this.current = 1; this.token = 0;
    this.bind();
    this.load(root.getAttribute('data-src'));
  }

  bind() {
    this.$('[data-pdf-prev]').addEventListener('click', () => this.go(this.current - 1));
    this.$('[data-pdf-next]').addEventListener('click', () => this.go(this.current + 1));
    const pageInput = this.$('[data-pdf-page]');
    pageInput.addEventListener('change', () => this.go(parseInt(pageInput.value, 10) || 1));
    pageInput.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); this.go(parseInt(pageInput.value, 10) || 1); } });
    const sel = this.$('[data-pdf-zoom]');
    sel.addEventListener('change', () => this.setZoom(sel.value));
    this.$('[data-pdf-zoom-in]').addEventListener('click', () => this.step(1));
    this.$('[data-pdf-zoom-out]').addEventListener('click', () => this.step(-1));
    this.$('[data-pdf-full]').addEventListener('click', () => this.toggleFull());
    document.addEventListener('fullscreenchange', () => {
      const on = document.fullscreenElement === this.root;
      this.root.classList.toggle('is-full', on);
      this.$('[data-pdf-full] .bi').className = 'bi ' + (on ? 'bi-fullscreen-exit' : 'bi-arrows-fullscreen');
      this.relayout();
    });
    this.stage.addEventListener('scroll', () => this.trackPage(), { passive: true });
    this.stage.addEventListener('keydown', e => {
      if (e.key === 'PageDown' || (e.key === ' ' && !e.shiftKey)) return; // native scrolling
      if (e.key === 'Home') { e.preventDefault(); this.go(1); } else if (e.key === 'End') { e.preventDefault(); this.go(this.pages.length); }
      else if ((e.ctrlKey || e.metaKey) && (e.key === '+' || e.key === '=')) { e.preventDefault(); this.step(1); }
      else if ((e.ctrlKey || e.metaKey) && e.key === '-') { e.preventDefault(); this.step(-1); }
    });
    let t; window.addEventListener('resize', () => { clearTimeout(t); t = setTimeout(() => { if (this.zoom === 'fit' || this.zoom === 'page') this.relayout(); }, 150); });
    // Several documents: switch the viewer to another PDF of the same content.
    const section = this.root.closest('section');
    section && section.querySelectorAll('[data-pdf-switch]').forEach(b => b.addEventListener('click', () => {
      section.querySelectorAll('[data-pdf-switch]').forEach(x => { x.classList.toggle('is-active', x === b); x.setAttribute('aria-selected', x === b); });
      this.$('[data-pdf-name]').textContent = b.getAttribute('data-name');
      this.$('[data-pdf-size]').textContent = b.getAttribute('data-size');
      this.$('[data-pdf-download]').href = b.getAttribute('data-download');
      this.root.setAttribute('data-src', b.getAttribute('data-src'));
      this.load(b.getAttribute('data-src'));
    }));
  }

  showState(kind, message, withRetry, withLogin) {
    this.root.classList.toggle('is-loading', kind === 'loading');
    this.root.classList.toggle('has-error', kind === 'error');
    this.state.hidden = kind === 'ready';
    if (kind === 'ready') return;
    this.state.innerHTML = '';
    if (kind === 'loading') { const s = document.createElement('span'); s.className = 'pdfv-spinner'; s.setAttribute('aria-hidden', 'true'); this.state.appendChild(s); }
    else { const i = document.createElement('i'); i.className = 'bi bi-file-earmark-x pdfv-state-icon'; i.setAttribute('aria-hidden', 'true'); this.state.appendChild(i); }
    const p = document.createElement('p'); p.textContent = message; p.setAttribute('role', kind === 'error' ? 'alert' : 'status'); this.state.appendChild(p);
    const actions = document.createElement('div'); actions.className = 'pdfv-state-actions';
    if (withRetry) { const b = document.createElement('button'); b.type = 'button'; b.className = 'btn btn-primary btn-sm'; b.textContent = this.t('retry'); b.addEventListener('click', () => this.load(this.root.getAttribute('data-src'))); actions.appendChild(b); }
    if (withLogin) { const a = document.createElement('a'); a.className = 'btn btn-primary btn-sm'; a.href = this.root.getAttribute('data-login-url'); a.textContent = this.t('signin'); actions.appendChild(a); }
    if (kind === 'error' && !withLogin) { const d = this.$('[data-pdf-download]').cloneNode(true); d.removeAttribute('data-pdf-download'); actions.appendChild(d); }
    if (actions.children.length) this.state.appendChild(actions);
  }

  async load(url) {
    const token = ++this.token;
    if (this.doc) { this.doc.destroy(); this.doc = null; }
    this.pages = []; this.pagesEl.innerHTML = ''; this.current = 1; this.setControls(false);
    this.showState('loading', this.t('loading'));
    let res;
    try { res = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/pdf' } }); }
    catch { if (token === this.token) this.showState('error', this.t('error'), true); return; }
    if (token !== this.token) return;
    // An expired session is redirected to the sign-in page; a missing file or content the user may not see is a 404.
    if (res.redirected && /\/Account\/(Login|WindowsLogin)/i.test(res.url)) return this.showState('error', this.t('session'), false, true);
    if (res.status === 401 || res.status === 403) return this.showState('error', this.t('session'), false, true);
    if (res.status === 404) return this.showState('error', this.t('missing'), false);
    if (!res.ok) return this.showState('error', this.t('error'), true);
    if (!/application\/pdf/i.test(res.headers.get('content-type') || '')) return this.showState('error', this.t('invalid'), false);
    const data = new Uint8Array(await res.arrayBuffer());
    try {
      this.doc = await pdfjsLib.getDocument({
        data, isEvalSupported: false, enableXfa: false,
        cMapUrl: LIB + 'cmaps/', cMapPacked: true, standardFontDataUrl: LIB + 'standard_fonts/', wasmUrl: LIB + 'wasm/',
      }).promise;
    } catch (e) {
      if (token !== this.token) return;
      return this.showState('error', this.t(e && e.name === 'PasswordException' ? 'password' : e && e.name === 'InvalidPDFException' ? 'invalid' : 'error'), e && e.name !== 'InvalidPDFException' && e.name !== 'PasswordException');
    }
    if (token !== this.token) return;
    await this.build();
  }

  async build() {
    const n = this.doc.numPages;
    const first = await this.doc.getPage(1);
    this.base = first.getViewport({ scale: 1 });         // placeholder size until each page is measured
    this.pages = [];
    for (let i = 1; i <= n; i++) {
      const el = document.createElement('div'); el.className = 'pdfv-page'; el.dataset.page = i;
      el.setAttribute('aria-label', this.t('page').replace('{0}', i));
      const c = document.createElement('canvas'); c.setAttribute('aria-hidden', 'true'); el.appendChild(c);
      const num = document.createElement('span'); num.className = 'pdfv-num'; num.textContent = i; num.dir = 'ltr'; el.appendChild(num);
      this.pagesEl.appendChild(el);
      this.pages.push({ el, canvas: c, page: i === 1 ? first : null, rendered: 0, task: null });
    }
    this.$('[data-pdf-count]').textContent = n;
    this.$('[data-pdf-page]').max = n;
    this.showState('ready');
    this.setControls(true);
    if (this.io) this.io.disconnect();
    this.io = new IntersectionObserver(es => es.forEach(e => { if (e.isIntersecting) this.render(+e.target.dataset.page); }), { root: this.stage, rootMargin: '400px 0px' });
    this.pages.forEach(p => this.io.observe(p.el));
    this.relayout();
  }

  setControls(on) {
    this.root.querySelectorAll('[data-pdf-prev],[data-pdf-next],[data-pdf-page],[data-pdf-zoom],[data-pdf-zoom-in],[data-pdf-zoom-out]').forEach(x => x.disabled = !on);
    if (on) this.updateNav();
  }

  scaleFor(vp1) {
    const avail = this.stage.clientWidth - 48;
    if (this.zoom === 'fit') return Math.max(0.4, Math.min(avail / vp1.width, 2.2));
    if (this.zoom === 'page') return Math.max(0.3, Math.min(avail / vp1.width, (this.stage.clientHeight - 48) / vp1.height));
    return parseFloat(this.zoom) * CSS_UNITS;
  }

  relayout() {
    if (!this.doc) return;
    const keep = this.current;
    this.scale = this.scaleFor(this.base);
    this.pages.forEach(p => {
      const vp = p.page ? p.page.getViewport({ scale: 1 }) : this.base;
      p.el.style.width = Math.floor(vp.width * this.scale) + 'px';
      p.el.style.height = Math.floor(vp.height * this.scale) + 'px';
      p.rendered = 0;
    });
    this.go(keep, true);
    // render what is visible now (the observer only fires on changes)
    const top = this.stage.scrollTop, bottom = top + this.stage.clientHeight + 400;
    this.pages.forEach((p, i) => { const t = p.el.offsetTop; if (t < bottom && t + p.el.offsetHeight > top - 400) this.render(i + 1); });
  }

  async render(n) {
    const p = this.pages[n - 1];
    if (!p || p.rendered === this.scale) return;
    const scale = this.scale;
    p.rendered = scale;
    if (!p.page) p.page = await this.doc.getPage(n);
    const vp = p.page.getViewport({ scale });
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    p.el.style.width = Math.floor(vp.width) + 'px'; p.el.style.height = Math.floor(vp.height) + 'px';
    if (p.task) { p.task.cancel(); p.task = null; }
    const off = document.createElement('canvas');
    off.width = Math.floor(vp.width * dpr); off.height = Math.floor(vp.height * dpr);
    off.style.width = Math.floor(vp.width) + 'px'; off.style.height = Math.floor(vp.height) + 'px';
    off.setAttribute('aria-hidden', 'true');
    p.task = p.page.render({ canvas: off, canvasContext: off.getContext('2d'), viewport: vp, transform: dpr !== 1 ? [dpr, 0, 0, dpr, 0, 0] : null });
    try { await p.task.promise; } catch { return; } // cancelled by a newer zoom
    if (p.rendered !== scale) return;
    p.el.replaceChild(off, p.canvas); p.canvas = off; p.task = null;
    p.el.classList.add('is-rendered');
  }

  go(n, instant) {
    if (!this.pages.length) return;
    n = Math.max(1, Math.min(this.pages.length, n));
    this.current = n;
    const el = this.pages[n - 1].el;
    this.stage.scrollTo({ top: el.offsetTop - 16, behavior: instant || reduce ? 'auto' : 'smooth' });
    this.updateNav();
  }

  trackPage() {
    if (!this.pages.length) return;
    const mid = this.stage.scrollTop + this.stage.clientHeight * 0.35;
    let cur = 1;
    for (const p of this.pages) { if (p.el.offsetTop <= mid) cur = +p.el.dataset.page; else break; }
    if (cur !== this.current) { this.current = cur; this.updateNav(); }
  }

  updateNav() {
    this.$('[data-pdf-page]').value = this.current;
    this.$('[data-pdf-prev]').disabled = this.current <= 1;
    this.$('[data-pdf-next]').disabled = this.current >= this.pages.length;
  }

  setZoom(z) {
    this.zoom = z;
    const sel = this.$('[data-pdf-zoom]');
    if (![...sel.options].some(o => o.value === z)) { const o = document.createElement('option'); o.value = z; o.textContent = Math.round(parseFloat(z) * 100) + '%'; sel.appendChild(o); }
    sel.value = z;
    this.relayout();
  }

  step(dir) {
    const now = this.scale / CSS_UNITS;
    const next = dir > 0 ? ZOOMS.find(z => z > now + 0.01) : [...ZOOMS].reverse().find(z => z < now - 0.01);
    if (next) this.setZoom(String(next));
  }

  toggleFull() {
    if (document.fullscreenElement === this.root) document.exitFullscreen();
    else if (this.root.requestFullscreen) this.root.requestFullscreen().catch(() => this.root.classList.toggle('is-full'));
    else { this.root.classList.toggle('is-full'); this.relayout(); }
  }
}

document.querySelectorAll('[data-pdf-viewer]').forEach(el => { el.__viewer = new PdfViewer(el); });
