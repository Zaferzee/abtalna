// Usage: BASE=http://localhost:5096 OUT=docs/screenshots/ack-gate-v1 CHROME=/path/to/chrome node tests/ui/ack-gate-acceptance.mjs
// Needs: npm i playwright-core; the Arabic demo data set (admin / Admin#NewPass99, employees with Pass#Employee1).
// Mandatory acknowledgment gate through the UI: the admin creates content that requires acknowledgment with a linked assessment
// (wizard), the preview shows the lock and the simulated unlock, then an employee: locked everywhere -> tries to start anyway
// (refused by the server) -> acknowledges -> success + unlock -> takes the assessment. A second employee stays locked.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5096', OUT = process.env.OUT;
fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const errs = [], shots = [], checks = [];
const W = 1440, H = 900, TITLE = 'سياسة تجريبية للإقرار الإلزامي';
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); console.log((cond ? 'PASS ' : 'FAIL ') + what); };
async function newPage(ctxOpts = {}) {
  const c = await b.newContext({ viewport: { width: W, height: H }, ...ctxOpts });
  const p = await c.newPage();
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push(p.url() + ' ' + m.text()); });
  p.on('dialog', d => { errs.push('NATIVE DIALOG ' + d.message()); d.accept(); });
  return p;
}
async function shot(p, name, desc, { wait = 1000 } = {}) {
  await p.waitForTimeout(wait); await p.screenshot({ path: `${OUT}/${name}.png` });
  shots.push({ name, desc }); console.log('shot', name);
}
const scrollTo = (p, sel, off = 110) => p.evaluate(([s, o]) => { const el = document.querySelector(s); window.scrollTo({ top: el.getBoundingClientRect().top + window.scrollY - o, behavior: 'instant' }); }, [sel, off]);
const login = async (p, u, pw) => { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle'); };
const nav = async (p, go) => { await p.click(`.wz-nav button[value="${go}"], .wz-nav a.btn-primary`); await p.waitForLoadState('networkidle'); };
const nsState = p => p.evaluate(() => { const ns = document.querySelector('.next-step'); return { locked: ns.classList.contains('is-locked'), text: ns.querySelector('.ns-action').innerText.trim(), start: !!ns.querySelector('form[action*="/Assessments/Start/"]'), msg: ns.querySelector('.lock-msg')?.innerText.trim() || '' }; });

// ================= ADMIN: content that requires acknowledgment + linked assessment (wizard) =================
const a = await newPage();
await login(a, 'admin', 'Admin#NewPass99');
await a.goto(base + '/Admin/Authoring/New');
await a.fill('#Title', TITLE); await a.click('label.type-card:has-text("سياسة")');
await a.fill('#Description', 'سياسة قصيرة يجب الإقرار بالاطلاع عليها قبل الاختبار.');
await nav(a, 'next');
const contentId = +a.url().split('/').pop();
await a.click('.ql-editor');
await a.keyboard.type('يلتزم جميع الموظفين بقفل أجهزتهم عند مغادرة المكتب، وعدم مشاركة كلمات المرور مع أي شخص.');
await nav(a, 'next');
await a.click('label.yn-card:has-text("نعم، يتطلب إقراراً")'); await a.click('button[data-fill="Statement"]');
await nav(a, 'next');
await a.click('label.yn-card:has-text("نعم، أضف اختباراً")');
await a.fill('#Settings_Title', 'اختبار السياسة التجريبية'); await a.fill('#Settings_PassingPercentage', '50'); await a.fill('#Settings_MaxAttempts', '2');
await a.click('button[value=stay]'); await a.waitForLoadState('networkidle');
await a.click('.seg-control label:has-text("صح / خطأ")'); await a.fill('textarea[name="Question.Text"]', 'يجوز مشاركة كلمة المرور مع الزملاء.');
await a.click('.q-tf label.yn-card:has-text("خطأ")'); await a.click('button:has-text("حفظ السؤال")'); await a.waitForLoadState('networkidle');
await nav(a, 'next');

// preview: lock, then simulated acknowledgment unlocks it (nothing recorded)
ok(a.url().includes('/Preview/'), 'preview step');
await scrollTo(a, '.employee-frame #sec-ack');
const pv1 = await a.evaluate(() => { const ns = document.querySelector('.employee-frame .next-step'); return { locked: ns.classList.contains('is-locked'), text: ns.querySelector('.ns-action').innerText }; });
ok(pv1.locked && pv1.text.includes('الاختبار مقفل') && !/ابدأ الاختبار/.test(pv1.text.replace(/\s+/g, ' ').split('الاختبار مقفل')[0]), 'preview: assessment shown as "🔒 الاختبار مقفل" before acknowledgment');
await shot(a, '01-preview-locked', 'المعاينة كموظف: الاختبار مقفل قبل الإقرار');
await a.check('.employee-frame [data-ack-sim-check]'); await a.click('.employee-frame [data-ack-sim]');
await a.waitForTimeout(400);
const pv2 = await a.evaluate(() => { const ns = document.querySelector('.employee-frame .next-step'); const open = ns.querySelector('[data-ns-open]'); return { locked: ns.classList.contains('is-locked'), open: open && !open.hidden && open.innerText.includes('ابدأ الاختبار'), title: document.querySelector('.employee-frame [data-ack-title]').innerText }; });
ok(!pv2.locked && pv2.open && pv2.title.includes('تم الإقرار بنجاح'), 'preview: simulated acknowledgment shows "تم الإقرار بنجاح" and unlocks "ابدأ الاختبار"');
await shot(a, '02-preview-simulated-unlock', 'المعاينة: بعد محاكاة الإقرار يُفتح الاختبار (لا يُسجَّل أي إقرار)', { wait: 900 });
await nav(a, 'next');
await a.click('button[name=go][value="publish"]'); await a.waitForSelector('#confirmModal.show'); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
const assessmentId = +(await a.content()).match(/assessmentId=(\d+)/)[1];
ok(assessmentId > 0, 'published (assessment ' + assessmentId + ')');

// ================= EMPLOYEE =================
const e = await newPage({ recordVideo: { dir: OUT + '/.video', size: { width: W, height: H } } });
await login(e, 'fahad.qahtani', 'Pass#Employee1');
// dashboard
// (learning experience v2: one row per learning item; the assessment step of its journey shows the lock)
const dash = await e.evaluate(t => { const it = [...document.querySelectorAll('.lx-focus, .lx-task')].find(x => x.innerText.includes(t)); return it && { locked: !!it.querySelector('.jm-locked, .j-locked[data-step="assessment"]'), start: !!it.querySelector('form[action*="/Assessments/Start/"]'), href: it.querySelector('a[href*="/Content/Details/"]')?.getAttribute('href'), text: it.innerText }; }, TITLE);
ok(dash && dash.locked && !dash.start && dash.href.endsWith(`/Content/Details/${contentId}#sec-ack`) && dash.text.includes('مطلوب'), 'dashboard: assessment locked and points to the content acknowledgment');
await e.evaluate(t => [...document.querySelectorAll('.lx-focus, .lx-task')].find(x => x.innerText.includes(t)).scrollIntoView({ block: 'center', behavior: 'instant' }), TITLE);
await shot(e, '03-dashboard-locked', 'لوحة الموظف: الاختبار مقفل ويوجّه إلى الإقرار بالاطلاع');
// assessments list
await e.goto(base + '/Assessments');
const card = await e.evaluate(id => { const c = [...document.querySelectorAll('.quiz-card')].find(x => x.innerText.includes('اختبار السياسة التجريبية')); return c && { locked: c.classList.contains('is-locked'), start: !!c.querySelector(`form[action$="/Assessments/Start/${id}"]`), text: c.innerText }; }, assessmentId);
ok(card && card.locked && !card.start && card.text.includes('مقفل'), 'assessments list: card locked, no start button');
await e.evaluate(() => document.querySelector('.quiz-card.is-locked').scrollIntoView({ block: 'center', behavior: 'instant' }));
await shot(e, '04-assessments-list-locked', 'قائمة الاختبارات: بطاقة الاختبار مقفلة مع رابط "اقرأ وأقرّ"');
// content page
await e.goto(base + `/Content/Details/${contentId}`); await e.waitForLoadState('networkidle');
ok((await e.title()) && (await e.locator('h1').innerText()).includes(TITLE), 'content page loads');
let ns = await nsState(e);
ok(ns.locked && !ns.start && ns.text.includes('الاختبار مقفل') && !ns.text.includes('ابدأ الاختبار') && ns.msg.includes('يتطلب إكمال الإقرار بالاطلاع أولاً'), 'content page: assessment locked ("يتطلب إكمال الإقرار بالاطلاع أولاً"), button does not say "ابدأ الاختبار"');
await scrollTo(e, '#sec-ack');
await shot(e, '05-content-locked', 'صفحة المحتوى قبل الإقرار: لوحة الإقرار ثم الاختبار المقفل');
// try to start anyway: a forged POST to the start endpoint (as a user would by replaying the request)
const startResp = e.waitForResponse(r => r.url().includes('/Assessments/Start/'));
await e.evaluate(id => { const f = document.createElement('form'); f.method = 'post'; f.action = '/Assessments/Start/' + id; const t = document.querySelector('input[name="__RequestVerificationToken"]').cloneNode(); f.appendChild(t); document.body.appendChild(f); f.submit(); }, assessmentId);
const denied = await startResp; await e.waitForSelector('.alert-danger'); await e.waitForLoadState('networkidle');
const deniedMsg = await e.locator('.alert, .toast').allInnerTexts();
ok(denied.status() === 302 && e.url().endsWith(`/Content/Details/${contentId}#sec-ack`) && deniedMsg.join(' ').includes('يجب الإقرار بالاطلاع على المحتوى قبل بدء الاختبار'), 'direct start request refused by the server -> back to the content with "يجب الإقرار بالاطلاع على المحتوى قبل بدء الاختبار."');
await e.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
await shot(e, '06-direct-start-denied', 'محاولة بدء الاختبار مباشرة قبل الإقرار: رفض من الخادم مع رسالة عربية', { wait: 600 });
// acknowledge
const ackBtn = e.locator('[data-ack-submit]');
ok(await ackBtn.isDisabled(), 'acknowledge button disabled until the checkbox is checked');
await e.check('[data-ack-check]'); await e.waitForTimeout(300); await ackBtn.click(); await e.waitForLoadState('networkidle');
await e.waitForTimeout(300);
const done = await e.evaluate(() => ({ title: document.querySelector('.ack-panel.is-done h2')?.innerText, url: location.href }));
ns = await nsState(e);
ok(done.title === 'تم الإقرار بنجاح', 'success state "تم الإقرار بنجاح"');
ok(!ns.locked && ns.start && ns.text.includes('ابدأ الاختبار'), 'assessment unlocked: "ابدأ الاختبار" enabled');
ok(await e.locator('.next-step.is-unlocking .unlock-chip').count() === 1, 'unlock transition shown ("تم فتح الاختبار")');
await shot(e, '07-acknowledged-unlocked', 'بعد الإقرار: "تم الإقرار بنجاح" وفتح الاختبار مع زر "ابدأ الاختبار"', { wait: 1100 });
// take the assessment
await e.click('.next-step form button'); await e.waitForLoadState('networkidle');
ok(e.url().includes('/Assessments/Take/'), 'start assessment opens the questions');
await e.click('[data-quiz-start]'); await e.waitForTimeout(400);
await e.locator('.qstep.is-active label.choice', { hasText: 'خطأ' }).first().click(); await e.waitForTimeout(500);
await e.click('[data-q-submit]'); await e.waitForSelector('#confirmModal.show'); await e.click('#confirmModalOk'); await e.waitForURL('**/Result/**');
ok((await e.locator('.result-status').innerText()).trim() === 'مجتاز', 'assessment works (passed)');
await shot(e, '08-assessment-passed', 'الاختبار بعد الإقرار: النتيجة مجتاز', { wait: 1400 });
// back on the content page later: acknowledged state, not asked again
await e.goto(base + `/Content/Details/${contentId}`); await e.waitForLoadState('networkidle');
ok(await e.locator('[data-ack-check]').count() === 0 && (await e.locator('.ack-panel.is-done h2').innerText()) === 'تم الإقرار', 'revisit: already acknowledged, not asked again');
const vpath = await e.video().path(); await e.context().close();
fs.renameSync(vpath, `${OUT}/ack-gate-flow.webm`); fs.rmSync(OUT + '/.video', { recursive: true, force: true });

// ================= SECOND EMPLOYEE: still locked =================
const o = await newPage();
await login(o, 'nora.harbi', 'Pass#Employee1');
await o.goto(base + `/Content/Details/${contentId}`); await o.waitForLoadState('networkidle');
ns = await nsState(o);
ok(ns.locked && !ns.start, "another employee is still locked (one user's acknowledgment does not unlock it for others)");

console.log(`CHECKS ${checks.filter(c => c.pass).length}/${checks.length}`);
console.log('ERRORS', errs.length ? errs : 'none');
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
await b.close();
