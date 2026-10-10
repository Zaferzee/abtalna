// Usage: BASE=http://localhost:5150 OUT=docs/screenshots/branding-studio-v1.1 ASSETS=./brand-assets CHROME=/path/to/chrome node tests/ui/branding-studio-refine.mjs
// Needs: npm i playwright-core; the Arabic demo data set (admin / Admin#NewPass99);
// ASSETS must contain logo-light.png (520x140), logo-seal.png (440x140), icon.png (256x256), emblem.png (512x512), bg-navy.jpg.
// Branding studio refinement through the UI only: independent logos (sidebar / login / icon), login image display modes
// (small, medium, full background, hidden) and decorative patterns, checked in the live preview and on the real pages.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5150', OUT = process.env.OUT, A = process.env.ASSETS;
fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const errs = [], shots = [], checks = [];
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); console.log((cond ? 'PASS ' : 'FAIL ') + what); };
const file = (name, mime) => ({ name, mimeType: mime, buffer: fs.readFileSync(`${A}/${name}`) });
async function page(opts = {}) {
  const c = await b.newContext({ viewport: { width: 1440, height: 900 }, ...opts }); const p = await c.newPage();
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push(p.url() + ' ' + m.text()); });
  p.on('dialog', d => d.accept());
  return p;
}
async function shot(p, name, desc, { wait = 700, el = null } = {}) {
  await p.waitForTimeout(wait);
  if (el) await p.locator(el).first().screenshot({ path: `${OUT}/${name}.png` }); else await p.screenshot({ path: `${OUT}/${name}.png` });
  shots.push({ name, desc }); console.log('shot', name);
}
const login = async (p, u, pw) => { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle'); };
const tab = async (p, id) => { await p.click(`[data-studio-tab="${id}"]`); await p.waitForTimeout(250); };
const choose = (p, name, value) => p.click(`label:has(> input[name="${name}"][value="${value}"])`);
const range = (p, name, v) => p.evaluate(([n, x]) => { const el = document.querySelector(`input[name="${n}"]`); el.value = x; el.dispatchEvent(new Event('input', { bubbles: true })); }, [name, v]);
const toStudio = async p => { await p.goto(base + '/Admin/Settings'); await p.waitForLoadState('networkidle'); };
const save = async p => { await p.click('[data-studio-save]'); await p.waitForLoadState('networkidle'); };
const upload = (p, field, name, mime) => p.setInputFiles(`input[name="${field}"]`, file(name, mime));
const waitImg = (p, sel) => p.waitForFunction(s => { const i = document.querySelector(s); return i && i.complete && i.naturalWidth > 0; }, sel, { timeout: 5000 }).catch(() => null);
// natural width of the logo shown on the real login page / in the real sidebar (each test logo has a different width)
async function loginLogoWidth(p) { await p.goto(base + '/Account/Login'); await p.waitForLoadState('networkidle'); return p.evaluate(() => { const i = document.querySelector('.auth-hero .auth-logo img, .auth-card-logo img'); return i ? { w: i.naturalWidth, src: i.getAttribute('src') } : { w: 0, src: '' }; }); }
async function sidebarLogoWidth(p) { await p.goto(base + '/Admin/Dashboard'); await p.waitForLoadState('networkidle'); return p.evaluate(() => { const i = document.querySelector('.sidebar .sb-logo img'); return i ? { w: i.naturalWidth, src: i.getAttribute('src') } : { w: 0, src: '' }; }); }
const pvState = p => p.evaluate(() => {
  const root = document.querySelector('[data-pv-auth]'), vis = s => { const e = root.querySelector(s); return !!e && getComputedStyle(e).display !== 'none' && !e.closest('[hidden]'); };
  const fig = [...root.querySelectorAll('.auth-figure')].find(f => getComputedStyle(f).display !== 'none');
  const inner = root.querySelector('.auth-hero-inner');
  return { cls: root.className, frame: vis('.auth-media-frame'), figure: !!fig, slot: fig ? (fig.classList.contains('slot-above') ? 'above' : 'below') : '',
    figW: fig ? Math.round(fig.getBoundingClientRect().width / inner.getBoundingClientRect().width * 100) : 0,
    decor: [...root.querySelectorAll('.dc')].filter(e => getComputedStyle(e).display !== 'none').map(e => [...e.classList].filter(c => c.startsWith('dc-')).join(' ')) };
});
const realState = p => p.evaluate(() => {
  const root = document.querySelector('.auth'), fig = document.querySelector('.auth-figure'), inner = document.querySelector('.auth-hero-inner');
  const art = [...document.querySelectorAll('.dc')].filter(e => getComputedStyle(e).display !== 'none').map(e => e.tagName.toLowerCase() + '.' + [...e.classList].filter(c => c.startsWith('dc-')).join('.'));
  return { cls: root.className, rtl: document.documentElement.dir, overflow: document.documentElement.scrollWidth > innerWidth + 1,
    frame: getComputedStyle(document.querySelector('.auth-media-frame')).display !== 'none', mediaBg: getComputedStyle(document.querySelector('.auth-media')).backgroundImage,
    figure: !!fig && getComputedStyle(fig).display !== 'none', figW: fig ? Math.round(fig.getBoundingClientRect().width / inner.getBoundingClientRect().width * 100) : 0,
    figBg: fig ? getComputedStyle(fig.querySelector('.auth-figure-img')).backgroundImage : '', art };
});

const a = await page();
await login(a, 'admin', 'Admin#NewPass99');
const g = await page();   // signed out: the real login page

// ================= 1. independent logos =================
await toStudio(a); await tab(a, 'logos');
await upload(a, 'logo', 'logo-light.png', 'image/png');
await waitImg(a, '[data-pv-scope] [data-b-logo="sidebar"] img');
let pv = await a.evaluate(() => ({ side: document.querySelector('[data-pv-scope] [data-b-logo="sidebar"] img').getAttribute('src') || '', loginHidden: document.querySelector('[data-pv-auth] [data-b-logo="login"]').hidden, cardHidden: document.querySelector('[data-pv-auth] [data-b-logo="card"]').hidden }));
ok(pv.side.startsWith('data:image/png') && pv.loginHidden && pv.cardHidden, 'preview: a new sidebar logo does not appear on the login page');
await upload(a, 'loginLogo', 'logo-seal.png', 'image/png');
await waitImg(a, '[data-pv-auth] [data-b-logo="login"] img');
pv = await a.evaluate(() => ({ side: document.querySelector('[data-pv-scope] [data-b-logo="sidebar"] img').naturalWidth, login: document.querySelector('[data-pv-auth] [data-b-logo="login"] img').naturalWidth }));
ok(pv.side === 520 && pv.login === 440, `preview: login logo (${pv.login}px) and sidebar logo (${pv.side}px) are different files`);
await a.evaluate(() => document.querySelector('[data-asset="loginlogo"]').scrollIntoView({ block: 'center', behavior: 'instant' }));
await shot(a, '01-studio-logos-independent', 'الاستوديو: شعار القائمة الجانبية وشعار صفحة الدخول ملفان مستقلان؛ كل بطاقة توضّح أين يُستخدم الشعار فقط', { wait: 600 });
await a.click('[data-pv-tab="app"]'); await a.waitForTimeout(400);
await shot(a, '02-preview-app-sidebar-logo', 'المعاينة (التطبيق): القائمة الجانبية تعرض شعارها الخاص، لا شعار صفحة الدخول', { el: '.studio-preview' });
await save(a);
let L = await loginLogoWidth(g), S = await sidebarLogoWidth(a);
ok(L.w === 440 && L.src.includes('kind=loginlogo') && S.w === 520 && S.src.includes('kind=logo&'), 'saved: login page shows the login logo, the sidebar shows the sidebar logo');
await shot(a, '03-sidebar-own-logo', 'لوحة الإدارة: شعار القائمة الجانبية (مستقل عن شعار صفحة الدخول)');
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
await shot(g, '04-login-own-logo', 'صفحة الدخول: شعار الدخول الخاص (الختم) وليس شعار القائمة الجانبية');

// change the sidebar logo only -> login logo unchanged
await toStudio(a); await tab(a, 'logos'); await upload(a, 'logo', 'icon.png', 'image/png'); await save(a);
L = await loginLogoWidth(g); S = await sidebarLogoWidth(a);
ok(S.w === 256 && L.w === 440, 'changing the sidebar logo does not change the login logo');
// change the login logo only -> sidebar logo unchanged
await toStudio(a); await tab(a, 'logos'); await upload(a, 'loginLogo', 'emblem.png', 'image/png'); await save(a);
L = await loginLogoWidth(g); S = await sidebarLogoWidth(a);
ok(L.w === 512 && S.w === 256, 'changing the login logo does not change the sidebar logo');
// remove the sidebar logo -> login logo stays; the compact icon is used only in the sidebar
await toStudio(a); await tab(a, 'logos'); await a.click('[data-asset="logo"] [data-asset-remove]'); await upload(a, 'icon', 'icon.png', 'image/png'); await save(a);
L = await loginLogoWidth(g);
const side3 = await a.evaluate(async () => { const r = await fetch('/Admin/Dashboard'); const h = await r.text(); return { logo: h.includes('kind=logo&'), icon: h.includes('kind=icon') }; });
ok(L.w === 512 && !side3.logo && side3.icon, 'removing the sidebar logo leaves the login logo; the compact icon appears only in the sidebar');
// restore the two logos used for the remaining screenshots
await toStudio(a); await tab(a, 'logos'); await upload(a, 'logo', 'logo-light.png', 'image/png'); await upload(a, 'loginLogo', 'logo-seal.png', 'image/png'); await save(a);

// ================= 2. login image display modes =================
await toStudio(a); await tab(a, 'media');
await upload(a, 'loginBackground', 'emblem.png', 'image/png');
await a.waitForFunction(() => (document.querySelector('[data-pv-auth] .auth-hero').style.getPropertyValue('--img') || '').includes('data:image/png'), null, { timeout: 5000 });
await choose(a, 'LoginImageMode', 'small');
pv = await pvState(a);
ok(pv.cls.includes('img-small') && pv.figure && !pv.frame && pv.figW >= 18 && pv.figW <= 26, `preview: small image shown as a framed mark (${pv.figW}% of the text column), no full background`);
const sizeShown = await a.evaluate(() => !document.querySelector('[data-show-modes="large medium small"]').hidden && document.querySelector('[data-show-modes="background"]').hidden);
ok(sizeShown, 'size / alignment / placement controls appear for framed modes; overlay only for the full background');
await choose(a, 'LoginImageAlign', 'start'); await choose(a, 'LoginImageSlot', 'above'); await range(a, 'LoginImageSize', 24);
await choose(a, 'LoginDecorStyle', 'grid');
await shot(a, '05-studio-image-small', 'الاستوديو: وضع «صغيرة (عنصر بصري)» مع الحجم والمحاذاة والمكان، والمعاينة تعرض الصورة فوراً');
await save(a);
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
let R = await realState(g);
ok(R.cls.includes('img-small') && R.figure && !R.frame && R.figBg.includes('kind=loginbg') && R.figW >= 20 && R.figW <= 28 && !R.overflow && R.rtl === 'rtl', `login page: small image (${R.figW}%), RTL, no overflow`);
await shot(g, '06-login-image-small', 'صفحة الدخول: صورة صغيرة كعنصر بصري فوق النص، مع نمط «شبكة الضوابط»', { wait: 1200 });

// medium, below the text, centered
await toStudio(a); await tab(a, 'media');
await upload(a, 'loginBackground', 'bg-navy.jpg', 'image/jpeg');
await a.waitForFunction(() => (document.querySelector('[data-pv-auth] .auth-hero').style.getPropertyValue('--img') || '').includes('data:image/jpeg'), null, { timeout: 5000 });
await choose(a, 'LoginImageMode', 'medium'); await choose(a, 'LoginImageSlot', 'below'); await choose(a, 'LoginImageAlign', 'start');
pv = await pvState(a);
ok(pv.cls.includes('img-medium') && pv.figure && pv.slot === 'below' && pv.figW >= 56 && pv.figW <= 64, `preview: medium image below the text (${pv.figW}%)`);
await choose(a, 'LoginDecorStyle', 'orbs');
await save(a);
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
R = await realState(g);
ok(R.cls.includes('img-medium') && R.figure && R.figW >= 56 && R.figW <= 64 && !R.frame && !R.overflow, `login page: medium image (${R.figW}%)`);
await shot(g, '07-login-image-medium', 'صفحة الدخول: صورة متوسطة تحت النص، مع «دوائر ناعمة»', { wait: 1200 });

// full background with an inset frame
await toStudio(a); await tab(a, 'media');
await choose(a, 'LoginImageMode', 'background'); await range(a, 'LoginImageInset', 0); await choose(a, 'LoginDecorStyle', 'shield');
pv = await pvState(a);
ok(pv.cls.includes('img-background') && pv.cls.includes('has-image') && pv.frame && !pv.figure, 'preview: full background, no framed picture');
await shot(a, '08-studio-image-background', 'الاستوديو: وضع «خلفية كاملة» (تظهر التغطية والتكبير ونقطة التركيز)');
await save(a);
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
R = await realState(g);
ok(R.cls.includes('img-background') && R.frame && R.mediaBg.includes('kind=loginbg') && !R.figure && !R.overflow, 'login page: full background image');
await shot(g, '09-login-image-background', 'صفحة الدخول: الصورة كخلفية كاملة مع طبقة التغطية', { wait: 1200 });
// phone
const m = await page({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
await m.goto(base + '/Account/Login'); await m.waitForLoadState('networkidle'); await m.evaluate(() => window.scrollTo(0, 0));
R = await realState(m);
ok(!R.overflow && R.frame && R.art.every(x => !x.startsWith('svg')), 'phone: full background, no overflow, line art hidden');
await m.context().close();

// hidden
await toStudio(a); await tab(a, 'media'); await choose(a, 'LoginImageMode', 'hidden');
pv = await pvState(a);
ok(pv.cls.includes('img-hidden') && !pv.frame && !pv.figure, 'preview: hidden mode shows no image (file kept)');

// ================= 3. decorative patterns (instant in the preview) =================
const styles = ['shield', 'orbs', 'checklist', 'grid', 'document', 'journey', 'none'];
const seen = {};
for (const s of styles) { await choose(a, 'LoginDecorStyle', s); seen[s] = await pvState(a); }
ok(styles.every(s => seen[s].cls.includes('decor-' + s)), 'preview: every decorative pattern applies immediately');
ok(['checklist', 'grid', 'document', 'journey'].every(s => seen[s].decor.some(d => d.includes('dc-' + s))) && seen.none.decor.length === 0 && seen.none.cls.includes('no-decor'),
  'preview: the chosen line art is shown and "No decoration" hides everything');
await choose(a, 'LoginDecorStyle', 'checklist');
await a.evaluate(() => document.querySelector('.decor-picker').scrollIntoView({ block: 'center', behavior: 'instant' }));
await shot(a, '10-studio-decor-picker', 'الاستوديو: اختيار «النمط الزخرفي» (درع، دوائر ناعمة، قائمة تحقق، شبكة الضوابط، وثيقة السياسة، مسار التقدم، بدون) والمعاينة تتغير فوراً');
await save(a);
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
R = await realState(g);
ok(R.cls.includes('decor-checklist') && R.art.includes('svg.dc-checklist'), 'login page: "Checklist" pattern');
await shot(g, '11-login-decor-checklist', 'صفحة الدخول: نمط «قائمة تحقق»', { wait: 1200 });
await toStudio(a); await tab(a, 'media'); await choose(a, 'LoginDecorStyle', 'journey'); await save(a);
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
R = await realState(g);
ok(R.cls.includes('decor-journey') && R.art.includes('svg.dc-journey'), 'login page: "Progress journey" pattern');
await shot(g, '12-login-decor-journey', 'صفحة الدخول: نمط «مسار التقدم»', { wait: 1200 });
await toStudio(a); await tab(a, 'media'); await choose(a, 'LoginDecorStyle', 'document'); await save(a);
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
await shot(g, '13-login-decor-document', 'صفحة الدخول: نمط «وثيقة السياسة»', { wait: 1200 });

// ================= 4. themes, Arabic and RTL unaffected =================
await toStudio(a); await tab(a, 'colors'); await a.click('[data-preset-id="mauve"]'); await tab(a, 'media'); await choose(a, 'LoginImageMode', 'large'); await choose(a, 'LoginDecorStyle', 'grid'); await save(a);
const css = await (await a.request.get(base + '/branding/theme.css')).text();
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
R = await realState(g);
const ar = await g.evaluate(() => ({ lang: document.documentElement.lang, title: document.querySelector('.auth-card h2, .auth-card h1')?.innerText || '' }));
ok(css.includes('--color-primary:#7d4f6e') && R.cls.includes('img-large') && R.figW >= 95 && R.rtl === 'rtl' && ar.lang.startsWith('ar') && /[؀-ۿ]/.test(ar.title) && !R.overflow,
  'theme preset (Soft Mauve Rose) + large image: colors applied, Arabic, RTL, no overflow');
await shot(g, '14-login-mauve-large', 'سمة «الوردي الموفي الهادئ» مع صورة كبيرة فوق النص ونمط «شبكة الضوابط»', { wait: 1200 });

// reset
await toStudio(a); await a.click('[data-studio-reset]'); await a.waitForSelector('#confirmModal.show'); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
R = await realState(g);
ok(R.cls.includes('decor-shield') && R.cls.includes('img-none') && !R.figure && !R.frame, 'reset to default: default decoration, no image');

console.log('CHECKS ' + checks.filter(c => c.pass).length + '/' + checks.length);
console.log('ERRORS ' + (errs.length ? errs.join('\n') : 'none'));
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
await b.close();
