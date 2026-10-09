// Usage: BASE=http://localhost:5130 OUT=docs/screenshots/learning-experience-v2/responsive MEDIA=./media CHROME=/path/to/chrome node tests/ui/responsive-check.mjs
// Needs: npm i playwright-core; the Arabic demo data set (admin / Admin#NewPass99, employees with Pass#Employee1);
// MEDIA must contain awareness.mp4 and regulation.pdf.
// Employee experience at 1366x768, 820x1180 and 390x844: dashboard, journey, content (video/PDF), acknowledgment, reading
// completion, assessment, results and My Results. Fails a check when a page scrolls horizontally or an element overflows the viewport.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5130', OUT = process.env.OUT, M = process.env.MEDIA;
fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const SIZES = [['desktop', 1366, 768], ['tablet', 820, 1180], ['mobile', 390, 844]];
const READ_TITLE = 'دليل التعامل مع المرفقات المشبوهة';
const checks = [], errs = [], shots = [];
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); console.log((cond ? 'PASS ' : 'FAIL ') + what); };
async function login(p, u, pw) { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle'); }
async function session(u, pw, w = 1366, h = 768) {
  const c = await b.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: 1, hasTouch: w < 900, isMobile: w < 500 });
  const p = await c.newPage();
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push(p.url() + ' ' + m.text()); });
  await login(p, u, pw); return p;
}
// Horizontal overflow: the page itself, or visible elements sticking out of the viewport (ignores intentionally scrollable boxes).
const overflow = p => p.evaluate(() => {
  const W = document.documentElement.clientWidth, out = [];
  if (document.documentElement.scrollWidth > W + 1) out.push('page scrollWidth ' + document.documentElement.scrollWidth + ' > ' + W);
  for (const el of document.querySelectorAll('main *')) {
    const r = el.getBoundingClientRect(); if (!r.width || !r.height) continue;
    let s = el.parentElement, clipped = false;
    while (s && s !== document.body) { const o = getComputedStyle(s).overflowX; if (o === 'auto' || o === 'scroll' || o === 'hidden') { clipped = true; break; } s = s.parentElement; }
    if (!clipped && (r.right > W + 1 || r.left < -1)) out.push((el.className && typeof el.className === 'string' ? '.' + el.className.split(' ')[0] : el.tagName) + ' [' + Math.round(r.left) + ',' + Math.round(r.right) + ']');
    if (out.length > 6) break;
  }
  return out;
});
async function capture(p, size, name, desc, { full = true, before = null } = {}) {
  if (before) await before();
  await p.waitForTimeout(1200);
  const o = await overflow(p);
  ok(o.length === 0, `${size}: ${name} has no horizontal overflow${o.length ? ' -> ' + o.join('; ') : ''}`);
  await p.screenshot({ path: `${OUT}/${size}-${name}.png`, fullPage: full });
  shots.push({ name: `${size}-${name}`, desc: `${size}: ${desc}` });
}

// ---- admin: a reading item (no acknowledgment, no assessment) with a video and a PDF, through the wizard ----
const a = await session('admin', 'Admin#NewPass99');
await a.goto(base + '/Admin/Authoring/New');
await a.fill('#Title', READ_TITLE); await a.click('label.type-card:has-text("محتوى توعوي")');
await a.fill('#Description', 'دليل قصير: كيف تتعامل مع مرفق غير متوقع قبل فتحه.');
await a.click('.wz-nav button[value="next"]'); await a.waitForLoadState('networkidle');
const readId = +a.url().split('/').pop();
await a.click('.ql-editor'); await a.keyboard.type('لا تفتح أي مرفق غير متوقع قبل التحقق من المرسل. شاهد الفيديو ثم اطّلع على الدليل المرفق.');
await a.setInputFiles('input[name=files]', [
  { name: 'التعامل مع المرفقات.mp4', mimeType: 'video/mp4', buffer: fs.readFileSync(`${M}/awareness.mp4`) },
  { name: 'دليل المرفقات.pdf', mimeType: 'application/pdf', buffer: fs.readFileSync(`${M}/regulation.pdf`) },
]);
await a.click('.wz-nav button[value="next"]'); await a.waitForLoadState('networkidle');
await a.goto(base + `/Admin/Authoring/Publish/${readId}`);
await a.click('button[name=go][value="publish"]'); await a.waitForSelector('#confirmModal.show'); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
ok(a.url().includes('/Publish/'), 'admin: reading item with video + PDF published');
await a.context().close();

for (const [size, w, h] of SIZES) {
  // employee who has not acknowledged content 1 yet
  const n = await session('nora.harbi', 'Pass#Employee1', w, h);
  await n.goto(base + '/'); await n.waitForLoadState('networkidle');
  await capture(n, size, '01-dashboard', 'لوحة الموظف والرحلة');
  await n.goto(base + '/Content/Details/1'); await n.waitForLoadState('networkidle');
  await capture(n, size, '02-content-journey-pdf-ack', 'صفحة المحتوى: الرحلة، عارض PDF، الإقرار والاختبار المقفل');
  await n.goto(base + `/Content/Details/${readId}`); await n.waitForLoadState('networkidle');
  await n.waitForFunction(() => document.querySelectorAll('.pdfv-page.is-rendered').length > 0, null, { timeout: 30000 });
  const media = await n.evaluate(() => { const v = document.querySelector('.vp-frame').getBoundingClientRect(), pv = document.querySelector('.pdfv').getBoundingClientRect(), W = document.documentElement.clientWidth; return { v: Math.round(v.width), pdf: Math.round(pv.width), W, ratio: +(v.width / v.height).toFixed(2) }; });
  ok(media.v <= media.W && media.pdf <= media.W && Math.abs(media.ratio - 16 / 9) < 0.05, `${size}: video ${media.v}px (16:9) and PDF viewer ${media.pdf}px fit the ${media.W}px viewport`);
  await capture(n, size, '03-reading-item-video-pdf', 'مادة قراءة: الفيديو وعارض PDF ولوحة "تمت القراءة"');
  await n.context().close();

  // employee with acknowledgment done: assessment + passed result + My Results
  const f = await session('fahad.qahtani', 'Pass#Employee1', w, h);
  await f.goto(base + '/Content/Details/1#sec-ack'); await f.waitForLoadState('networkidle');
  await f.click('.next-step form button'); await f.waitForLoadState('networkidle');
  if (await f.locator('[data-quiz-start]').isVisible()) { await f.click('[data-quiz-start]'); await f.waitForTimeout(400); }
  await f.locator('.qstep.is-active label.choice').first().click(); await f.waitForTimeout(300);
  await capture(f, size, '04-assessment', 'الاختبار: التقدم والسؤال والإجابة المحددة', { full: false });
  await f.goto(base + '/Assessments/Result/4'); await f.waitForLoadState('networkidle');
  await capture(f, size, '05-result-passed', 'نتيجة مجتازة');
  await f.goto(base + '/MyResults'); await f.waitForLoadState('networkidle');
  await capture(f, size, '07-my-results', 'نتائجي');
  await f.goto(base + '/Assessments'); await f.waitForLoadState('networkidle');
  await capture(f, size, '08-assessments', 'قائمة الاختبارات (حالة "مجتاز": عرض النتيجة)');
  await f.context().close();

  const k = await session('khaled.zahrani', 'Pass#Employee1', w, h);
  await k.goto(base + '/Assessments/Result/5'); await k.waitForLoadState('networkidle');
  await capture(k, size, '06-result-failed', 'نتيجة غير مجتازة');
  await k.context().close();
}

// reading completion through the UI (once): button -> recorded -> completion card
const s = await session('sara.otaibi', 'Pass#Employee1', 390, 844);
await s.goto(base + `/Content/Details/${readId}`); await s.waitForLoadState('networkidle');
await s.click('#sec-read button'); await s.waitForLoadState('networkidle');
const rd = await s.evaluate(() => ({ h: document.querySelector('#sec-read h2')?.innerText, complete: !!document.querySelector('#sec-complete') }));
ok(rd.h === 'تم تسجيل إكمال القراءة' && rd.complete, 'reading completion: "تمت القراءة" recorded, completion card shown');
await capture(s, 'mobile', '09-reading-completed', 'بعد "تمت القراءة": تسجيل الإكمال وبطاقة الإنجاز', { before: () => s.evaluate(() => document.querySelector('#sec-read').scrollIntoView({ block: 'start', behavior: 'instant' })), full: false });
await s.goto(base + '/MyResults'); await s.waitForLoadState('networkidle');
ok((await s.content()).includes(READ_TITLE), 'My Results lists the completed reading');
await capture(s, 'mobile', '10-my-results-reading', 'نتائجي: الإقرارات والقراءة المكتملة');
await s.context().close();

console.log(`CHECKS ${checks.filter(c => c.pass).length}/${checks.length}`);
console.log('ERRORS', errs.length ? errs : 'none');
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
await b.close();
