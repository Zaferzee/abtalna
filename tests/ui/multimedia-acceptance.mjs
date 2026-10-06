// Usage: BASE=http://localhost:5095 OUT=docs/screenshots/multimedia-v1 MEDIA=./media CHROME=/path/to/chrome node tests/ui/multimedia-acceptance.mjs
// Needs: npm i playwright-core; the Arabic demo data set (admin / Admin#NewPass99, employees with Pass#Employee1);
// MEDIA must contain awareness.mp4 (MP4 the test browser can play) and regulation.pdf (multi-page Arabic A4 PDF).
// Everything is done through the UI: the admin creates and publishes the item in the wizard, an employee uses it.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5095', OUT = process.env.OUT, M = process.env.MEDIA;
fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const errs = [], shots = [], checks = [];
const W = 1440, H = 900, TITLE = 'لائحة تجريبية متعددة الوسائط';
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); console.log((cond ? 'PASS ' : 'FAIL ') + what); };
async function newPage(ctxOpts = {}) {
  const c = await b.newContext({ viewport: { width: W, height: H }, ...ctxOpts });
  const p = await c.newPage();
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error' && !/status of 404|status of 401/.test(m.text())) errs.push(p.url() + ' ' + m.text()); });
  p.on('dialog', d => { errs.push('NATIVE DIALOG ' + d.message()); d.accept(); });
  return p;
}
async function shot(p, name, desc, { full = false, wait = 1200 } = {}) {
  await p.waitForTimeout(wait);
  if (full) {
    const vp = p.viewportSize(); const h = await p.evaluate(() => document.documentElement.scrollHeight);
    await p.evaluate(() => window.scrollTo(0, 0)); await p.setViewportSize({ width: vp.width, height: Math.min(h, 5200) }); await p.waitForTimeout(900);
    await p.screenshot({ path: `${OUT}/${name}.png` }); await p.setViewportSize(vp);
  } else await p.screenshot({ path: `${OUT}/${name}.png` });
  shots.push({ name, desc }); console.log('shot', name);
}
const login = async (p, u, pw) => { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle'); };
const nav = async (p, go) => { await p.click(`.wz-nav button[value="${go}"], .wz-nav a.btn-primary`); await p.waitForLoadState('networkidle'); };
const pdfReady = p => p.waitForFunction(() => document.querySelectorAll('.pdfv-page.is-rendered').length >= 1 && document.querySelector('[data-pdf-state]').hidden, null, { timeout: 30000 });

// ================= ADMIN: create through the wizard =================
const a = await newPage();
await login(a, 'admin', 'Admin#NewPass99');
await a.goto(base + '/Admin/Authoring/New');
await a.fill('#Title', TITLE); await a.click('label.type-card:has-text("لائحة")');
await a.fill('#Description', 'لائحة تجريبية تجمع النص المنسق والفيديو التوعوي ومستند PDF الرسمي في صفحة واحدة.');
await nav(a, 'next');
const contentId = +a.url().split('/').pop();
await a.click('.ql-editor');
await a.click('.ql-header'); await a.click('.ql-picker-item[data-value="2"]'); await a.keyboard.type('مقدمة اللائحة');
await a.keyboard.press('Enter');
await a.keyboard.type('تهدف هذه اللائحة إلى رفع الوعي بالمخالفات السيبرانية. شاهد الفيديو التوعوي ثم اقرأ المستند الرسمي المعتمد أدناه.');
await a.keyboard.press('Enter');
await a.click('.ql-list[value="ordered"]'); await a.keyboard.type('شاهد الفيديو حتى النهاية.'); await a.keyboard.press('Enter');
await a.keyboard.type('اقرأ المستند الرسمي.'); await a.keyboard.press('Enter'); await a.keyboard.type('أقرّ بالاطلاع ثم أكمل الاختبار.');
await a.setInputFiles('input[name=files]', [
  { name: 'التوعية بالأمن السيبراني.mp4', mimeType: 'video/mp4', buffer: fs.readFileSync(`${M}/awareness.mp4`) },
  { name: 'اللائحة التجريبية المعتمدة.pdf', mimeType: 'application/pdf', buffer: fs.readFileSync(`${M}/regulation.pdf`) },
]);
await nav(a, 'next');
ok(a.url().includes('/Acknowledgment/'), 'content written and files uploaded through the wizard');
await a.click('label.yn-card:has-text("نعم، يتطلب إقراراً")'); await a.click('button[data-fill="Statement"]');
await nav(a, 'next');
await a.click('label.yn-card:has-text("نعم، أضف اختباراً")');
await a.fill('#Settings_Title', 'اختبار اللائحة التجريبية'); await a.fill('#Settings_PassingPercentage', '70'); await a.fill('#Settings_MaxAttempts', '2');
await a.click('button[value=stay]'); await a.waitForLoadState('networkidle');
const Q = [
  { type: 'اختيار واحد', text: 'ما الجزاء الأول لمشاركة كلمة المرور؟', opts: ['إنذار كتابي', 'لا شيء', 'فصل'], correct: [0] },
  { type: 'صح / خطأ', text: 'يجوز فتح المرفقات المجهولة إذا كان المرسل يبدو موثوقاً.', tf: 'خطأ' },
  { type: 'اختيار متعدد', text: 'أي مما يلي يُعد مخالفة؟', opts: ['تعطيل برامج الحماية', 'ترك الجهاز دون قفل', 'الإبلاغ عن حادثة'], correct: [0, 1] },
];
for (const q of Q) {
  await a.click(`.seg-control label:has-text("${q.type}")`); await a.fill('textarea[name="Question.Text"]', q.text);
  if (q.tf) await a.click(`.q-tf label.yn-card:has-text("${q.tf}")`);
  else { for (let i = 0; i < q.opts.length; i++) await a.fill(`input[name="Question.Options[${i}]"]`, q.opts[i]); for (const c of q.correct) await a.locator('[data-opt-row]').nth(c).locator('.opt-correct-wrap').click(); }
  await a.click('button:has-text("حفظ السؤال")'); await a.waitForLoadState('networkidle');
}
await nav(a, 'next');
// 9. preview shows the video and the PDF exactly as employees will see them
ok(a.url().includes('/Preview/'), 'preview step');
const pv = await a.evaluate(() => ({ video: !!document.querySelector('.employee-frame video[src*="/Files/Attachment/"]'), viewer: !!document.querySelector('.employee-frame [data-pdf-viewer]') }));
await pdfReady(a);
ok(pv.video && pv.viewer, '9. admin employee-preview shows the inline video and the PDF viewer');
await a.evaluate(() => document.querySelector('.employee-frame #sec-video').scrollIntoView({ block: 'start', behavior: 'instant' })); await a.evaluate(() => window.scrollBy({ top: -90, behavior: 'instant' }));
await shot(a, '04-admin-preview-multimedia', 'معاينة المسؤول كموظف: الفيديو المضمن وعارض PDF داخل إطار صفحة الموظف قبل النشر', { wait: 1500 });
await shot(a, '04b-admin-preview-full', 'المعاينة كاملة (نص → فيديو → PDF → إقرار → اختبار)', { full: true, wait: 600 });
await nav(a, 'next');
await a.click('button[value=publish]'); await a.waitForSelector('#confirmModal.show'); await a.waitForTimeout(300); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
ok((await a.locator('.publish-done').count()) === 1, 'published');

// ================= EMPLOYEE =================
const e = await newPage();
await login(e, 'fahad.qahtani', 'Pass#Employee1');
await e.goto(base + '/Content/Policies');
await e.click(`a.c-card:has-text("${TITLE}")`); await e.waitForLoadState('networkidle');
ok(e.url().includes(`/Content/Details/${contentId}`), '1. content loads');
// order: text -> video -> pdf -> acknowledgment -> assessment
const order = await e.evaluate(() => ['.doc-card', '#sec-video', '#sec-pdf', '.ack-panel', '.next-step'].map(s => { const el = document.querySelector(s); return el ? el.getBoundingClientRect().top + window.scrollY : -1; }));
ok(order.every(x => x >= 0) && order.every((x, i) => i === 0 || x > order[i - 1]), 'page order: text, video, PDF, acknowledgment, assessment');
const vinfo = await e.evaluate(() => { const v = document.querySelector('[data-video]'); const f = v.closest('.vp-frame').getBoundingClientRect(); return { src: v.getAttribute('src'), controls: v.controls, w: Math.round(f.width), h: Math.round(f.height) }; });
ok(/^\/Files\/Attachment\/\d+$/.test(vinfo.src) && vinfo.controls, '2. video embedded in-page with native controls (authenticated attachment URL, no storage path): ' + vinfo.src);
ok(vinfo.w >= 800 && vinfo.w <= 960 && Math.abs(vinfo.w / vinfo.h - 16 / 9) < 0.03, `video frame ${vinfo.w}x${vinfo.h} (16:9, max 960px)`);
await e.evaluate(() => document.querySelector('#sec-video').scrollIntoView({ block: 'start', behavior: 'instant' })); await e.evaluate(() => window.scrollBy({ top: -90, behavior: 'instant' }));
await e.click('[data-video]'); // native controls: click toggles playback
await e.waitForTimeout(2600);
const played = await e.evaluate(() => { const v = document.querySelector('[data-video]'); return { t: v.currentTime, paused: v.paused, err: v.error && v.error.code, dur: v.duration }; });
ok(played.t > 1 && !played.paused && !played.err, `3. video plays (currentTime ${played.t.toFixed(1)}s of ${played.dur.toFixed(1)}s)`);
await shot(e, '01-employee-inline-video', 'صفحة الموظف: الفيديو المضمن أثناء التشغيل (عناصر التحكم الأصلية: تشغيل/إيقاف، تقديم، صوت، ملء الشاشة)', { wait: 300 });
await e.evaluate(() => document.querySelector('[data-video]').pause());
// PDF
await e.evaluate(() => document.querySelector('#sec-pdf').scrollIntoView({ block: 'start', behavior: 'instant' })); await e.evaluate(() => window.scrollBy({ top: -90, behavior: 'instant' }));
await pdfReady(e);
const pinfo = await e.evaluate(() => ({ count: document.querySelector('[data-pdf-count]').textContent, rendered: document.querySelectorAll('.pdfv-page.is-rendered').length, w: Math.round(document.querySelector('.pdfv').getBoundingClientRect().width) }));
ok(pinfo.count === '3' && pinfo.rendered >= 1, `4. PDF visible in-page (pages: ${pinfo.count}, rendered: ${pinfo.rendered}, viewer width ${pinfo.w}px)`);
await shot(e, '02-employee-inline-pdf', 'صفحة الموظف: عارض PDF المضمن (اسم المستند، التنقل بين الصفحات، التكبير، ملء الشاشة، التنزيل)', { wait: 600 });
await e.selectOption('[data-pdf-zoom]', '1'); await e.waitForTimeout(900);
await shot(e, '03-pdf-viewer-normal-zoom', 'عارض PDF عند تكبير 100% (صفحة A4)', { wait: 600 });
await e.click('[data-pdf-next]'); await e.waitForTimeout(1200);
const p2 = await e.inputValue('[data-pdf-page]');
await e.click('[data-pdf-zoom-in]'); await e.waitForTimeout(800);
const z = await e.inputValue('[data-pdf-zoom]');
await e.fill('[data-pdf-page]', '3'); await e.press('[data-pdf-page]', 'Enter'); await e.waitForTimeout(1200);
const p3 = await e.inputValue('[data-pdf-page]');
ok(p2 === '2' && z === '1.25' && p3 === '3', `5. PDF viewer works (next page -> ${p2}, zoom in -> ${z}, go to page -> ${p3})`);
await shot(e, '03b-pdf-viewer-page3-zoom125', 'عارض PDF: الصفحة 3 بتكبير 125%', { wait: 400 });
// Arabic error states of the viewer (missing file / not a PDF / expired session)
const viewer = '[data-pdf-viewer]';
await e.evaluate(() => document.querySelector('[data-pdf-viewer]').__viewer.load('/Files/Attachment/999999')); await e.waitForTimeout(800);
const missing = await e.locator(`${viewer} [data-pdf-state] p`).innerText();
await shot(e, '06-pdf-viewer-error-missing', 'حالة الخطأ بالعربية: المستند غير متاح', { wait: 200 });
await e.evaluate(() => document.querySelector('[data-pdf-viewer]').__viewer.load('/')); await e.waitForTimeout(800);
const invalid = await e.locator(`${viewer} [data-pdf-state] p`).innerText();
ok(missing.includes('غير متاح') && invalid.includes('تالف'), `viewer shows Arabic messages (missing: "${missing}", invalid: "${invalid}")`);
// 6. authorization of attachments
const pdfUrl = await e.getAttribute('[data-pdf-viewer]', 'data-src'), vidUrl = vinfo.src;
const r1 = await e.request.get(base + pdfUrl), r2 = await e.request.get(base + vidUrl, { headers: { Range: 'bytes=0-99' } });
ok(r1.status() === 200 && r1.headers()['content-type'] === 'application/pdf' && r2.status() === 206, `6a. signed-in employee: PDF 200 application/pdf, video range request ${r2.status()}`);
const anon = await newPage();
const r3 = await anon.request.get(base + pdfUrl, { maxRedirects: 0 }), r4 = await anon.request.get(base + vidUrl, { maxRedirects: 0 });
ok([302, 401].includes(r3.status()) && [302, 401].includes(r4.status()) && !(r3.headers()['content-type'] || '').includes('pdf'), `6b. anonymous: PDF ${r3.status()}, video ${r4.status()} (redirect to sign-in, no file)`);
const draftAtt = await a.evaluate(async () => { const h = await (await fetch('/Admin/Authoring/Write/6')).text(); const m = h.match(/\/Files\/Attachment\/(\d+)/); return m && m[0]; });
const r5 = await e.request.get(base + draftAtt);
ok(r5.status() === 404, `6c. employee cannot open an attachment of unpublished content (${draftAtt} -> ${r5.status()})`);
await e.context().clearCookies();
await e.evaluate(() => document.querySelector('[data-pdf-viewer]').__viewer.load(document.querySelector('[data-pdf-viewer]').getAttribute('data-src'))); await e.waitForTimeout(1000);
const expired = await e.locator(`${viewer} [data-pdf-state] p`).innerText();
ok(expired.includes('انتهت جلستك'), `6d. expired session in the viewer -> "${expired}"`);
// 7-8. acknowledgment + assessment
await login(e, 'fahad.qahtani', 'Pass#Employee1'); await e.goto(base + `/Content/Details/${contentId}`); await e.waitForLoadState('networkidle');
await e.check('[data-ack-check]'); await e.click('[data-ack-submit]'); await e.waitForLoadState('networkidle');
ok((await e.locator('.ack-panel.is-done').count()) === 1, '7. acknowledgment works');
await e.click('.next-step form button'); await e.waitForLoadState('networkidle'); await e.click('[data-quiz-start]'); await e.waitForTimeout(400);
const pick = async t => { await e.locator('.qstep.is-active label.choice', { hasText: t }).first().click(); };
await pick('إنذار كتابي'); await e.waitForTimeout(700); await pick('خطأ'); await e.waitForTimeout(700);
await pick('تعطيل برامج'); await pick('ترك الجهاز');
await e.click('[data-q-submit]'); await e.waitForSelector('#confirmModal.show'); await e.click('#confirmModalOk'); await e.waitForURL('**/Result/**');
ok((await e.locator('.result-status').innerText()).trim() === 'مجتاز', '8. assessment works (passed)');

// ================= ADMIN: audit log =================
await a.goto(base + '/Admin/Dashboard'); await a.waitForLoadState('networkidle');
await a.click('section.panel:has-text("آخر الأنشطة") a:has-text("عرض الكل")'); await a.waitForLoadState('networkidle');
const rows = await a.locator('table.audit-table tbody tr:has(.audit-code)').count();
ok(a.url().endsWith('/Admin/Audit') && rows > 0, `11. dashboard "عرض الكل" opens the Audit Log (${rows} entries on page 1)`);
const firstAction = await a.locator('table.audit-table tbody tr .audit-action').first().innerText();
ok(/[؀-ۿ]/.test(firstAction), '10. Audit Log shows real entries with Arabic action names: ' + firstAction.replace(/\s+/g, ' '));
await shot(a, '05-audit-log', 'سجل التدقيق: سجلات فعلية (التاريخ والوقت، المستخدم/المنفذ، الإجراء بالعربية مع الرمز الأصلي، الكيان، التفاصيل)', { full: true, wait: 600 });
await a.selectOption('select[name=op]', 'CONTENT_PUBLISHED'); await a.click('form.toolbar button.btn-outline-primary'); await a.waitForLoadState('networkidle');
const codes = await a.locator('table.audit-table tbody .audit-code').allInnerTexts();
ok(codes.length > 0 && codes.every(c => c === 'CONTENT_PUBLISHED'), `audit filter by action works (${codes.length} × CONTENT_PUBLISHED)`);
await shot(a, '05b-audit-log-filtered', 'سجل التدقيق مصفّى حسب الإجراء "نشر محتوى"', { wait: 400 });
await a.goto(base + '/Admin/Audit?op=NO_SUCH_ACTION');
ok((await a.locator('table.audit-table .empty h3').innerText()).includes('لا توجد سجلات مطابقة'), 'empty state in Arabic when nothing matches');

// ================= WebM: content page -> play video -> scroll to PDF -> use the viewer =================
{
  const c = await b.newContext({ viewport: { width: W, height: H }, recordVideo: { dir: OUT + '/.rec', size: { width: W, height: H } } });
  const s = await c.newPage(); await s.goto(base + '/Account/Login'); await s.fill('#Username', 'fahad.qahtani'); await s.fill('#Password', 'Pass#Employee1'); await s.click('form[action="/Account/Login"] button.btn-primary'); await s.waitForLoadState('networkidle');
  const sv = s.video(); await s.close(); await sv.delete();
  const r = await c.newPage(); r.on('pageerror', x => errs.push('REC ' + x.message));
  await r.goto(base + `/Content/Details/${contentId}`); await r.waitForTimeout(1800);
  await r.mouse.wheel(0, 380); await r.waitForTimeout(900);
  await r.evaluate(() => document.querySelector('#sec-video').scrollIntoView({ behavior: 'smooth', block: 'start' })); await r.waitForTimeout(1100);
  await r.click('[data-video]'); await r.waitForTimeout(5200);
  await r.evaluate(() => document.querySelector('[data-video]').pause()); await r.waitForTimeout(400);
  await r.evaluate(() => document.querySelector('#sec-pdf').scrollIntoView({ behavior: 'smooth', block: 'start' })); await r.waitForTimeout(1600);
  await r.hover('[data-pdf-next]'); await r.waitForTimeout(300); await r.click('[data-pdf-next]'); await r.waitForTimeout(1400);
  await r.click('[data-pdf-zoom-in]'); await r.waitForTimeout(1200);
  await r.mouse.move(700, 600); await r.mouse.wheel(0, 500); await r.waitForTimeout(1200);
  await r.selectOption('[data-pdf-zoom]', 'fit'); await r.waitForTimeout(1000);
  await r.fill('[data-pdf-page]', '3'); await r.press('[data-pdf-page]', 'Enter'); await r.waitForTimeout(1500);
  await r.click('[data-pdf-prev]'); await r.waitForTimeout(1500);
  const v = r.video(); await r.close(); await v.saveAs(`${OUT}/multimedia-content-flow.webm`); await v.delete(); await c.close();
  fs.rmSync(OUT + '/.rec', { recursive: true, force: true });
  console.log('video multimedia-content-flow.webm');
}
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1)); fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
console.log('CHECKS', checks.filter(c => c.pass).length + '/' + checks.length);
console.log('ERRORS:\n' + errs.join('\n'));
await b.close();
