// Usage: BASE=http://localhost:5093 OUT=docs/screenshots/redesign-v1/motion CHROME=/path/to/chrome node tests/ui/motion-recordings.mjs
// Run after content-authoring-acceptance.mjs on the same database (it uses the published regulation and the demo employees).
// Records short real-browser videos (WebM, 1440x900) of the motion design. Each flow records on its own page;
// set-up steps (sign-in, answering) run on a separate page whose video is discarded.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5093', OUT = process.env.OUT, TMP = OUT + '/.tmp';
fs.mkdirSync(TMP, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const size = { width: 1440, height: 900 };
const errs = [];
async function ctx(opts = {}) { return b.newContext({ viewport: size, recordVideo: { dir: TMP, size }, ...opts }); }
async function login(c, u, pw) {
  const p = await c.newPage();
  await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw);
  await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle');
  return p;
}
async function finish(p, name) {
  const v = p.video(); await p.close(); await v.saveAs(`${OUT}/${name}.webm`); await v.delete();
  console.log('video', name);
}
async function discard(p) { const v = p.video(); await p.close(); if (v) await v.delete(); }
const watch = p => { p.on('pageerror', e => errs.push(e.message)); p.on('dialog', d => d.accept()); return p; };
const slow = (p, sel, text) => p.type(sel, text, { delay: 55 });

// 1. Login page: staggered entrance, floating orbs, focus states, password reveal, button feedback
{
  const c = await ctx(); const p = watch(await c.newPage());
  await p.goto(base + '/Account/Login'); await p.waitForTimeout(2200);
  await p.click('#Username'); await slow(p, '#Username', 'nora.harbi'); await p.waitForTimeout(300);
  await p.click('#Password'); await slow(p, '#Password', 'Pass#Employee1'); await p.waitForTimeout(300);
  await p.click('[data-pw-toggle]'); await p.waitForTimeout(900); await p.click('[data-pw-toggle]'); await p.waitForTimeout(500);
  await p.hover('form[action="/Account/Login"] button.btn-primary'); await p.waitForTimeout(700);
  await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForTimeout(2600);
  await finish(p, '01-login-motion'); await c.close();
}

// 2. Employee dashboard: hero ring + count-up, staggered cards, hover lift, scroll reveal
{
  const c = await ctx(); await discard(await login(c, 'omar.shehri', 'Pass#Employee1'));
  const p = watch(await c.newPage());
  await p.goto(base + '/'); await p.waitForTimeout(2600);
  for (const sel of ['.todo-item >> nth=0', '.todo-item >> nth=1', '.p-tile >> nth=0', '.result-row >> nth=0']) { await p.hover(sel).catch(() => {}); await p.waitForTimeout(650); }
  await p.mouse.wheel(0, 700); await p.waitForTimeout(1400);
  await p.hover('.c-card >> nth=0').catch(() => {}); await p.waitForTimeout(700); await p.hover('.c-card >> nth=1').catch(() => {}); await p.waitForTimeout(900);
  await p.mouse.wheel(0, -700); await p.waitForTimeout(900);
  await finish(p, '02-employee-dashboard'); await c.close();
}

// 3. Assessment: intro -> start, choice cards, auto-advance step transition, multi-select, question navigator
{
  const c = await ctx(); const s = await login(c, 'reem.mutairi', 'Pass#Employee1');
  await s.goto(base + '/Assessments'); await s.locator('article.quiz-card', { hasText: 'لائحة المخالفات' }).locator('button.btn-primary').click(); await s.waitForLoadState('networkidle');
  const take = s.url(); await discard(s);
  const p = watch(await c.newPage());
  await p.goto(take); await p.waitForTimeout(1600);
  await p.hover('[data-quiz-start]'); await p.waitForTimeout(400); await p.click('[data-quiz-start]'); await p.waitForTimeout(900);
  const pick = async t => { const l = p.locator('.qstep.is-active label.choice', { hasText: t }).first(); await l.hover(); await p.waitForTimeout(350); await l.click(); };
  await pick('إنذار كتابي'); await p.waitForTimeout(1300);
  await pick('خطأ'); await p.waitForTimeout(1300);
  await pick('ترك الجهاز'); await p.waitForTimeout(500); await pick('نقل بيانات'); await p.waitForTimeout(700);
  await p.click('[data-q-prev]'); await p.waitForTimeout(1100);
  await p.click('[data-qnav] button:nth-child(3)'); await p.waitForTimeout(900);
  await p.keyboard.press('4'); await p.waitForTimeout(500); await p.keyboard.press('4'); await p.waitForTimeout(600);
  await p.click('[data-q-next]'); await p.waitForTimeout(1300);
  await finish(p, '03-assessment-selection-transition'); await c.close();
}

// 4. Successful result: ring fill with pass mark, status pop, celebration burst, count-up
{
  const c = await ctx(); const s = await login(c, 'khaled.zahrani', 'Pass#Employee1');
  await s.goto(base + '/Assessments'); await s.locator('article.quiz-card', { hasText: 'لائحة المخالفات' }).locator('button.btn-primary').click(); await s.waitForLoadState('networkidle');
  await s.click('[data-quiz-start]');
  const answers = [['إنذار كتابي'], ['خطأ'], ['ترك الجهاز', 'نقل بيانات'], ['الإبلاغ عنها لإدارة'], ['خطأ'], ['قفل الشاشة', 'المصادقة متعددة']];
  for (let i = 0; i < answers.length; i++) {
    for (const t of answers[i]) await s.locator('.qstep.is-active label.choice', { hasText: t }).first().click();
    await s.waitForTimeout(800);
    if (answers[i].length > 1 && i < answers.length - 1) { await s.click('[data-q-next]'); await s.waitForTimeout(500); }
  }
  await s.click('[data-q-submit]'); await s.waitForSelector('#confirmModal.show'); await s.click('#confirmModalOk'); await s.waitForURL('**/Result/**');
  const url = s.url(); await discard(s);
  const p = watch(await c.newPage());
  await p.goto(url); await p.waitForTimeout(4200);
  await p.mouse.wheel(0, 600); await p.waitForTimeout(1200);
  await p.click('.review details:first-child summary'); await p.waitForTimeout(1000);
  await finish(p, '04-assessment-result-reveal'); await c.close();
}

// 5. Admin content wizard: step bar, type cards, editor + table picker, acknowledgment live preview, assessment yes/no reveal
{
  const c = await ctx(); await discard(await login(c, 'admin', 'Admin#NewPass99'));
  const p = watch(await c.newPage());
  await p.goto(base + '/Admin/Content'); await p.waitForTimeout(1200);
  await p.hover('a.btn-primary:has-text("إنشاء مادة جديدة")'); await p.waitForTimeout(400);
  await p.click('a.btn-primary:has-text("إنشاء مادة جديدة")'); await p.waitForLoadState('networkidle'); await p.waitForTimeout(900);
  await slow(p, '#Title', 'دليل الإبلاغ عن الحوادث السيبرانية');
  for (const t of ['سياسة', 'إجراء', 'تعليمات']) { await p.hover(`label.type-card:has-text("${t}")`); await p.waitForTimeout(250); }
  await p.click('label.type-card:has-text("إجراء")'); await p.waitForTimeout(600);
  await p.click('.wz-nav button[value=next]'); await p.waitForLoadState('networkidle'); await p.waitForTimeout(900);
  await p.click('.ql-editor'); await p.click('.ql-header'); await p.waitForTimeout(300); await p.click('.ql-picker-item[data-value="2"]');
  await p.keyboard.type('خطوات الإبلاغ', { delay: 40 }); await p.keyboard.press('Enter');
  await p.keyboard.type('أبلغ مركز العمليات الأمنية فور الاشتباه بأي حادثة.', { delay: 25 }); await p.keyboard.press('Enter');
  await p.click('button.ql-table-insert'); await p.waitForTimeout(400);
  await p.hover('.tbl-picker-grid button[data-r="2"][data-c="2"]'); await p.waitForTimeout(250);
  await p.hover('.tbl-picker-grid button[data-r="3"][data-c="3"]'); await p.waitForTimeout(500);
  await p.click('.tbl-picker-grid button[data-r="3"][data-c="3"]'); await p.waitForTimeout(400);
  await p.keyboard.type('نوع الحادثة', { delay: 30 }); await p.waitForTimeout(900);
  await p.click('.wz-nav button[value=next]'); await p.waitForLoadState('networkidle'); await p.waitForTimeout(800);
  await p.click('label.yn-card:has-text("نعم")'); await p.waitForTimeout(700);
  await p.fill('#Statement', ''); await slow(p, '#Statement', 'أقرّ بأنني اطلعت على دليل الإبلاغ عن الحوادث.'); await p.waitForTimeout(900);
  await p.click('.wz-nav button[value=next]'); await p.waitForLoadState('networkidle'); await p.waitForTimeout(800);
  await p.click('label.yn-card:has-text("نعم")'); await p.waitForTimeout(900);
  await p.click('button[value=stay]'); await p.waitForLoadState('networkidle'); await p.waitForTimeout(1200);
  await p.click('.seg-control label:has-text("صح")'); await p.waitForTimeout(400);
  await slow(p, 'textarea[name="Question.Text"]', 'يجب الإبلاغ عن الحادثة فور اكتشافها.');
  await p.click('.q-tf label.yn-card:has-text("صح")'); await p.waitForTimeout(500);
  await p.click('button:has-text("حفظ السؤال")'); await p.waitForLoadState('networkidle'); await p.waitForTimeout(1800);
  await finish(p, '05-admin-content-wizard'); await c.close();
}

// 6. Same dashboard with "reduce motion" enabled in the OS: content appears without animation
{
  const c = await ctx({ reducedMotion: 'reduce' }); await discard(await login(c, 'omar.shehri', 'Pass#Employee1'));
  const p = watch(await c.newPage());
  await p.goto(base + '/'); await p.waitForTimeout(1500); await p.mouse.wheel(0, 700); await p.waitForTimeout(1000);
  await finish(p, '06-reduced-motion-dashboard'); await c.close();
}
fs.rmSync(TMP, { recursive: true, force: true });
console.log('ERRORS:', errs.join('\n'));
await b.close();
