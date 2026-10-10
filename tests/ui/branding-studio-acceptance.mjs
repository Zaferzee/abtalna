// Usage: BASE=http://localhost:5150 OUT=docs/screenshots/branding-studio-v1 ASSETS=./brand-assets CHROME=/path/to/chrome node tests/ui/branding-studio-acceptance.mjs
// Needs: npm i playwright-core; the Arabic demo data set (admin / Admin#NewPass99, employees with Pass#Employee1);
// ASSETS must contain bg-navy.jpg, bg-mauve.jpg, logo-light.png, logo-dark.png, icon.png.
// Branding & Appearance studio through the UI only: names, texts, logos, login image controls, presets, readability checks,
// unsaved-changes / discard / revert, save; then the real login page (navy, mauve) and the application; finally reset.
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
async function shot(p, name, desc, { wait = 700, full = false, el = null } = {}) {
  await p.waitForTimeout(wait);
  if (el) await p.locator(el).first().screenshot({ path: `${OUT}/${name}.png` }); else await p.screenshot({ path: `${OUT}/${name}.png`, fullPage: full });
  shots.push({ name, desc }); console.log('shot', name);
}
const login = async (p, u, pw) => { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle'); };
const tab = async (p, id) => { await p.click(`[data-studio-tab="${id}"]`); await p.waitForTimeout(250); };
const choose = (p, name, value) => p.click(`label:has(> input[name="${name}"][value="${value}"])`);
const range = (p, name, v) => p.evaluate(([n, x]) => { const el = document.querySelector(`input[name="${n}"]`); el.value = x; el.dispatchEvent(new Event('input', { bubbles: true })); }, [name, v]);
const dirty = p => p.evaluate(() => document.querySelector('[data-studio-status]').classList.contains('is-dirty'));
const scopeVar = (p, v) => p.evaluate(x => document.querySelector('[data-pv-scope]').style.getPropertyValue(x).trim(), v);
const toStudio = async p => { await p.goto(base + '/Admin/Settings'); await p.waitForLoadState('networkidle'); await p.evaluate(() => { try { sessionStorage.removeItem('studio-tab'); } catch (e) { } }); await p.reload(); await p.waitForLoadState('networkidle'); };
const top = p => p.evaluate(() => { const g = document.querySelector('.studio-grid'); window.scrollTo({ top: g.getBoundingClientRect().top + window.scrollY - 150, behavior: 'instant' }); });

const ORG = 'الهيئة الوطنية للخدمات الرقمية\nإدارة الأمن السيبراني';
const SYS = 'منصة التوعية\nوالامتثال السيبراني';

// ================= studio =================
const a = await page();
await login(a, 'admin', 'Admin#NewPass99');
await toStudio(a); await top(a);
await shot(a, '01-studio-identity', 'الاستوديو: الهوية العامة (الأسماء متعددة الأسطر وخيارات الظهور) مع المعاينة المباشرة لصفحة الدخول');
ok(!(await dirty(a)), 'studio opens with "all changes saved"');

// names (multi-line) + unsaved-changes indicator + discard
await a.fill('#f-OrgName', ORG); await a.fill('#f-SystemName', SYS);
ok(await dirty(a), 'editing marks the studio as having unsaved changes');
const pvNames = await a.evaluate(() => document.querySelector('[data-pv-auth] [data-b="SystemName"]').innerText);
ok(pvNames.includes('\n'), 'preview shows the system name on two lines');
await a.click('[data-studio-discard]'); await a.waitForTimeout(200);
ok(!(await dirty(a)) && (await a.inputValue('#f-OrgName')) !== ORG, 'discard changes restores the saved values');
await a.fill('#f-OrgName', ORG); await a.fill('#f-SystemName', SYS);
await a.fill('#f-SupportText', 'الدعم الفني: تحويلة 4455\nsecurity-desk@agency.gov.sa');
await a.fill('#f-FooterText', '© 2026 الهيئة الوطنية للخدمات الرقمية - جميع الحقوق محفوظة');

// login texts
await tab(a, 'text');
await a.fill('#f-LoginBadge', 'برنامج الوعي السيبراني المؤسسي');
await a.fill('#f-LoginHeroTitle', 'رحلتك نحو\nبيئة رقمية آمنة');
await a.fill('#f-LoginHeroDescription', 'تعرّف على السياسات والضوابط والممارسات الآمنة التي تحمي مؤسستنا وبياناتنا، عبر محتوى موجز واختبارات قصيرة.');
await a.fill('#f-LoginFeature1', 'سياسات وضوابط معتمدة في مكان واحد');
await a.fill('#f-LoginFeature2', 'اختبارات قصيرة تقيس فهمك');
await a.fill('#f-LoginFeature3', 'متابعة الإقرارات والإنجاز');
await a.fill('#f-LoginTitle', 'تسجيل الدخول إلى المنصة');
await a.fill('#f-LoginCardSubtitle', 'استخدم حساب المؤسسة لمتابعة رحلتك التعليمية.');
await a.fill('#f-LoginSubtitle', 'هذه المنصة مخصصة لمنسوبي الهيئة فقط، وجميع العمليات مسجّلة.');
const pvText = await a.evaluate(() => ({ title: document.querySelector('[data-pv-auth] [data-b="LoginHeroTitle"]').innerText, f2: document.querySelector('[data-pv-auth] [data-b="LoginFeature2"]').innerText, notice: !document.querySelector('[data-pv-auth] [data-b-wrap="LoginSubtitle"]').hidden }));
ok(pvText.title.includes('\n') && pvText.f2 === 'اختبارات قصيرة تقيس فهمك' && pvText.notice, 'preview follows the login texts live (title on two lines, features, notice)');
await top(a); await shot(a, '02-studio-login-text', 'نصوص صفحة الدخول: الشارة والعنوان والوصف والنقاط والبطاقة والتنبيه، كلها قابلة للتعديل');

// logos & icons
await tab(a, 'logos');
await a.setInputFiles('input[name=logo]', file('logo-dark.png', 'image/png'));
await a.setChecked('#f-LogoPlate', false);
await a.setInputFiles('input[name=loginLogo]', file('logo-dark.png', 'image/png'));
await a.setChecked('#f-LoginLogoPlate', false);
await a.setInputFiles('input[name=icon]', file('icon.png', 'image/png'));
await a.setInputFiles('input[name=favicon]', file('icon.png', 'image/png'));
await a.waitForFunction(() => { const i = document.querySelector('[data-pv-auth] [data-b-logo="login"] img'); return i && i.complete && i.naturalWidth > 0; }, null, { timeout: 10000 });
const pvLogo = await a.evaluate(() => { const l = document.querySelector('[data-pv-auth] [data-b-logo="login"]'), sb = document.querySelector('.pv-shell [data-b-logo="sidebar"] img'); return { shown: !l.hidden && l.querySelector('img').naturalWidth > 0, sidebar: sb.naturalWidth > 0, state: document.querySelector('[data-asset="logo"] [data-asset-state]').className }; });
ok(pvLogo.shown && pvLogo.sidebar && pvLogo.state.includes('is-new'), 'logos: the chosen files render in the preview immediately (decoded under the site CSP) and are marked "applied when you save"');
await top(a); await shot(a, '03-studio-logos', 'الشعارات والأيقونات: شعار القائمة الجانبية، شعار الدخول وموضعه، الأيقونة المختصرة، أيقونة المتصفح (رفع/استبدال/إزالة/تراجع)');

// login media
await tab(a, 'media');
await a.setInputFiles('input[name=loginBackground]', file('bg-navy.jpg', 'image/jpeg'));
await choose(a, 'LoginImageFit', 'cover');
await choose(a, 'LoginImagePosition', 'top-right');
const posTR = await a.evaluate(() => getComputedStyle(document.querySelector('[data-pv-auth] .auth-hero')).getPropertyValue('--img-pos').trim());
ok(posTR === '100% 0%', 'image position preset is applied to the preview (' + posTR + ')');
await a.waitForFunction(() => { const i = document.querySelector('[data-focal-img]'); return i && !i.hidden && i.naturalWidth > 0; }, null, { timeout: 10000 });
await a.evaluate(() => document.querySelector('[data-focal]').scrollIntoView({ block: 'center', behavior: 'instant' }));
const fb = await a.locator('[data-focal]').boundingBox(); await a.mouse.click(fb.x + fb.width * 0.62, fb.y + fb.height * 0.4);
const focal = await a.evaluate(() => ({ x: +document.querySelector('[name=LoginFocusX]').value, y: +document.querySelector('[name=LoginFocusY]').value, custom: document.querySelector('[name=LoginImagePosition][value=custom]').checked }));
ok(focal.custom && Math.abs(focal.x - 62) <= 3 && Math.abs(focal.y - 40) <= 4, `focal point picked on the image (${focal.x}%, ${focal.y}%)`);
await range(a, 'LoginImageZoom', 110); await range(a, 'LoginOverlayOpacity', 55);
await choose(a, 'LoginLayout', 'split');
const media = await a.evaluate(() => { const h = document.querySelector('[data-pv-auth] .auth-hero'), cs = getComputedStyle(h); return { img: cs.getPropertyValue('--img'), zoom: cs.getPropertyValue('--img-zoom').trim(), op: cs.getPropertyValue('--ov-op').trim(), hasImage: document.querySelector('[data-pv-auth]').classList.contains('has-image') }; });
ok(media.img.includes('data:image/jpeg') && media.zoom === '1.1' && media.op === '0.55' && media.hasImage, 'preview applies the new image, zoom and overlay before saving');
await top(a); await shot(a, '04-studio-login-media', 'صورة صفحة الدخول: الملاءمة، الموضع (شبكة 3×3) ونقطة التركيز، التكبير، طبقة التغطية، التخطيط وموضع النص');

// colors: presets, readability warning, revert
await tab(a, 'colors');
await a.click('[data-preset-id="navy"]'); await a.waitForTimeout(300);
ok((await scopeVar(a, '--color-primary')) === '#1b3a6b' && (await a.inputValue('#f-SidebarColor')) === '#0e1d38', 'preset "Executive Navy" fills every color and recolors the preview');
await top(a); await shot(a, '05-studio-colors-presets', 'الألوان والسمات: 8 سمات رسمية جاهزة ثم ضبط يدوي لكل لون');
await a.fill('#f-HeaderTextColor', '#f4f4f4'); await a.waitForTimeout(250);
const warn = await a.evaluate(() => ({ row: document.querySelector('[data-check="header"]').className, any: !document.querySelector('[data-check-note="any"]').hidden }));
ok(warn.row.includes('is-low') && warn.any, 'readability check warns about low header contrast');
await a.evaluate(() => document.querySelector('[data-contrast-list]').scrollIntoView({ block: 'center', behavior: 'instant' }));
await shot(a, '06-studio-contrast-warning', 'فحص الوضوح: تحذير عند اختيار تباين منخفض (نص الترويسة)');
await a.click('[data-revert-group="colors"]'); await a.waitForTimeout(200);
ok((await a.inputValue('#f-HeaderTextColor')) === '#14213d', 'revert theme restores the saved colors');
await a.click('[data-preset-id="navy"]'); await a.waitForTimeout(200);

// previews: application, mobile, enlarged
await a.click('[data-pv-tab="app"]'); await a.waitForTimeout(400);
await top(a); await shot(a, '07-studio-preview-app', 'معاينة التطبيق: القائمة الجانبية (الأسماء متعددة الأسطر والشعار) والترويسة واللافتة والبطاقات والأزرار وشارات الحالة');
await a.click('[data-pv-tab="login"]'); await a.click('[data-device="mobile"]'); await a.waitForTimeout(500);
await top(a); await shot(a, '08-studio-preview-mobile', 'معاينة صفحة الدخول على الجوال');
await a.click('[data-device="desktop"]'); await a.click('[data-pv-expand]'); await a.waitForTimeout(600);
await shot(a, '09-studio-preview-enlarged', 'المعاينة المكبّرة لصفحة الدخول قبل الحفظ');
await a.click('[data-pv-expand]');
// save
await a.click('[data-studio-save]'); await a.waitForLoadState('networkidle');
ok((await a.locator('.alert-success').count()) > 0 && !(await dirty(a)), 'saved: success message, no unsaved changes');
const savedState = await a.evaluate(() => document.querySelector('[data-asset="loginbg"] [data-asset-state]').className);
ok(savedState.includes('is-saved'), 'saved images are shown as uploaded after saving');

// ================= real login page: navy =================
const g = await page();
await g.goto(base + '/Account/Login'); await g.waitForLoadState('networkidle');
const L1 = await g.evaluate(() => ({
  title: document.querySelector('.auth-title').innerText, names: document.querySelector('.auth-brand-names strong').innerText,
  badge: document.querySelector('.auth-kicker').innerText, card: document.querySelector('.auth-card h2').innerText, support: document.querySelector('.auth-support')?.innerText || '',
  logo: document.querySelector('.auth-logo img')?.getAttribute('src') || '', plate: document.querySelector('.auth-logo').classList.contains('plate'),
  img: getComputedStyle(document.querySelector('.auth-hero')).getPropertyValue('--img'), pos: getComputedStyle(document.querySelector('.auth-hero')).getPropertyValue('--img-pos').trim(),
  favicon: document.querySelector('link[rel=icon]')?.getAttribute('href') || '', mediaBg: getComputedStyle(document.querySelector('.auth-media')).backgroundImage,
}));
ok(L1.title === 'رحلتك نحو\nبيئة رقمية آمنة' && L1.names.includes('\n'), 'login page: multi-line hero title and system name');
ok(L1.badge.includes('برنامج الوعي السيبراني المؤسسي') && L1.card === 'تسجيل الدخول إلى المنصة' && L1.support.includes('4455'), 'login page: custom badge, card title and support text');
ok(L1.logo.includes('kind=loginlogo') && !L1.plate && L1.favicon.includes('kind=favicon'), 'login page: login logo without plate, favicon');
ok(L1.mediaBg.includes('kind=loginbg') && /^6\d% [34]\d%$/.test(L1.pos), 'login page: background image with the chosen focal point (' + L1.pos + ')');
const css1 = await (await g.request.get(base + '/branding/theme.css')).text();
ok(css1.includes('--color-primary:#1b3a6b') && css1.includes('--color-sidebar:#0e1d38'), 'theme.css carries the Executive Navy colors');
await shot(g, '10-login-navy', 'صفحة الدخول بسمة «الكحلي التنفيذي»: صورة بنقطة تركيز وتغطية 55%، شعار بدون خلفية، أسماء وعنوان على سطرين ونصوص مخصصة', { wait: 1400 });
await g.context().close();
const gm = await page({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
await gm.goto(base + '/Account/Login'); await gm.waitForLoadState('networkidle');
ok(await gm.evaluate(() => document.documentElement.scrollWidth <= 391), 'login page on a phone: no horizontal overflow');
// the autofocused username field may sit below a tall hero, so the browser scrolls to the form; show the hero first
await gm.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
await shot(gm, '11-login-navy-mobile', 'صفحة الدخول على الجوال (390×844): اللوحة المرئية', { wait: 1200 });
await gm.evaluate(() => document.querySelector('.auth-card').scrollIntoView({ block: 'start', behavior: 'instant' }));
await shot(gm, '11b-login-navy-mobile-card', 'صفحة الدخول على الجوال: بطاقة تسجيل الدخول ونص الدعم', { wait: 500 });
await gm.context().close();

// application with the saved branding
await a.goto(base + '/Admin/Dashboard'); await a.waitForLoadState('networkidle');
const side = await a.evaluate(() => ({ sys: document.querySelector('.sb-names strong').innerText, org: document.querySelector('.sb-names small').innerText, logo: document.querySelector('.sb-logo img')?.getAttribute('src') || '', plate: !document.querySelector('.sb-logo').classList.contains('no-plate'), title: document.title }));
ok(side.sys.includes('\n') && side.org.includes('\n') && side.logo.includes('kind=logo') && !side.plate && !side.title.includes('\n'), 'sidebar: multi-line names (no clipping), logo on dark background; page title on one line');
await shot(a, '12-admin-sidebar-navy', 'لوحة الإدارة بالهوية الجديدة: اسمان على سطرين في القائمة الجانبية وشعار للخلفيات الداكنة', { wait: 1200 });

// ================= second theme: Soft Mauve Rose, full-screen image, centered text, logo on the card =================
await toStudio(a);
await tab(a, 'colors'); await a.click('[data-preset-id="mauve"]');
await tab(a, 'logos');
await a.setInputFiles('input[name=loginLogo]', file('logo-light.png', 'image/png'));
await choose(a, 'LoginLogoPlacement', 'card'); await a.setChecked('#f-LoginLogoPlate', true);
await tab(a, 'media');
await a.setInputFiles('input[name=loginBackground]', file('bg-mauve.jpg', 'image/jpeg'));
await choose(a, 'LoginImageFit', 'cover'); await choose(a, 'LoginImagePosition', 'bottom');
await range(a, 'LoginImageZoom', 100); await range(a, 'LoginOverlayOpacity', 40);
await a.fill('#f-LoginOverlayColor', '#3a2434');
await choose(a, 'LoginLayout', 'full'); await choose(a, 'LoginTextAlign', 'center');
await choose(a, 'LoginDecorStyle', 'none');
const pvFull = await a.evaluate(() => { const r = document.querySelector('[data-pv-auth]'); return r.classList.contains('layout-full') && r.classList.contains('align-center') && r.classList.contains('no-decor') && r.classList.contains('logo-card'); });
ok(pvFull, 'preview switches layout (full-screen), text placement (centered), decoration and logo placement');
await top(a); await shot(a, '13-studio-mauve-full', 'الاستوديو: سمة «الوردي الموفي الهادئ» مع صورة بملء الشاشة ونص في المنتصف وشعار داخل بطاقة الدخول');
await a.click('[data-studio-save]'); await a.waitForLoadState('networkidle');
const m = await page();
await m.goto(base + '/Account/Login'); await m.waitForLoadState('networkidle');
const L2 = await m.evaluate(() => ({ cls: document.querySelector('.auth').className, cardLogo: document.querySelector('.auth-card-logo img')?.getAttribute('src') || '', heroLogo: getComputedStyle(document.querySelector('.auth-hero .auth-logo')).display }));
ok(L2.cls.includes('layout-full') && L2.cls.includes('align-center') && L2.cls.includes('no-decor') && L2.cardLogo.includes('kind=loginlogo') && L2.heroLogo === 'none', 'login page: full-screen layout, centered text, logo on the sign-in card');
const css2 = await (await m.request.get(base + '/branding/theme.css')).text();
ok(css2.includes('--color-primary:#7d4f6e'), 'theme.css carries the Soft Mauve Rose colors');
await shot(m, '14-login-mauve', 'صفحة الدخول بسمة «الوردي الموفي الهادئ»: صورة بملء الشاشة، نص في المنتصف، الشعار داخل البطاقة', { wait: 1400 });
await m.context().close();
const e = await page();
await login(e, 'nora.harbi', 'Pass#Employee1');
await shot(e, '15-employee-mauve', 'بوابة الموظف بسمة «الوردي الموفي الهادئ»', { wait: 1800 });
await e.context().close();

// ================= reset to default =================
await toStudio(a);
await a.click('[data-studio-reset]'); await a.waitForSelector('#confirmModal.show'); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
const r = await page();
await r.goto(base + '/Account/Login'); await r.waitForLoadState('networkidle');
const L3 = await r.evaluate(() => ({ cls: document.querySelector('.auth').className, img: getComputedStyle(document.querySelector('.auth-media')).backgroundImage, logo: !!document.querySelector('.auth-logo img'), title: document.querySelector('.auth-title').innerText }));
ok(!L3.cls.includes('layout-full') && L3.img === 'none' && !L3.logo && !L3.title.includes('رحلتك'), 'reset to default: default layout, no image, no logo, default texts');
await r.context().close();

console.log(`CHECKS ${checks.filter(c => c.pass).length}/${checks.length}`);
console.log('ERRORS', errs.length ? errs : 'none');
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
await b.close();
