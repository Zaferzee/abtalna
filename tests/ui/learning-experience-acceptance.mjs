// Usage: BASE=http://localhost:5111 OUT=docs/screenshots/learning-experience-v2 CHROME=/path/to/chrome node tests/ui/learning-experience-acceptance.mjs
// Needs: npm i playwright-core; the Arabic demo data set (admin / Admin#NewPass99, employees with Pass#Employee1).
// Learning experience v2: the admin creates a policy (acknowledgment + assessment, passing score 100%, 2 attempts) in the wizard.
// Employee A: dashboard -> content (locked assessment) -> acknowledgment -> unlock -> fails -> retries -> passes -> completed.
// Employee B: the full journey in one recording. Screenshots go to $OUT, WebM recordings (1440x900) to $OUT/motion.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5111', OUT = process.env.OUT, MOTION = OUT + '/motion';
fs.mkdirSync(MOTION, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const W = 1440, H = 900, TITLE = 'سياسة حماية البيانات التجريبية', QUIZ = 'اختبار سياسة حماية البيانات';
const errs = [], shots = [], checks = [], clips = [];
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); console.log((cond ? 'PASS ' : 'FAIL ') + what); };
const watch = p => {
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push(p.url() + ' ' + m.text()); });
  p.on('dialog', d => { errs.push('NATIVE DIALOG ' + d.message()); d.accept(); });
};
async function page(opts = {}) { const c = await b.newContext({ viewport: { width: W, height: H }, ...opts }); const p = await c.newPage(); watch(p); return p; }
async function shot(p, name, desc, { wait = 900, el = null } = {}) {
  await p.waitForTimeout(wait);
  if (el) await p.locator(el).first().screenshot({ path: `${OUT}/${name}.png` }); else await p.screenshot({ path: `${OUT}/${name}.png` });
  shots.push({ name, desc }); console.log('shot', name);
}
const scrollTo = (p, sel, off = 100) => p.evaluate(([s, o]) => { const el = document.querySelector(s); window.scrollTo({ top: el.getBoundingClientRect().top + window.scrollY - o, behavior: 'instant' }); }, [sel, off]);
const glide = async (p, sel, off = 100, ms = 900) => { // smooth, visible scroll for recordings
  await p.evaluate(([s, o, d]) => new Promise(r => { const el = document.querySelector(s), y0 = window.scrollY, y1 = el.getBoundingClientRect().top + y0 - o, t0 = performance.now();
    const f = t => { const k = Math.min(1, (t - t0) / d), e = k < .5 ? 2 * k * k : 1 - Math.pow(-2 * k + 2, 2) / 2; window.scrollTo({ top: y0 + (y1 - y0) * e, behavior: 'instant' }); k < 1 ? requestAnimationFrame(f) : r(); }; requestAnimationFrame(f); }), [sel, off, ms]);
};
async function login(p, u, pw) { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle'); }
async function session(u, pw) { const p = await page(); await login(p, u, pw); const st = await p.context().storageState(); await p.context().close(); return st; }
async function recorder(state) { const p = await page({ storageState: state, recordVideo: { dir: MOTION + '/.tmp', size: { width: W, height: H } } }); return p; }
async function saveClip(p, name, desc) { const v = p.video(); await p.context().close(); fs.renameSync(await v.path(), `${MOTION}/${name}.webm`); clips.push({ name, desc }); console.log('clip', name); }
const nav = async (p, go) => { await p.click(`.wz-nav button[value="${go}"], .wz-nav a.btn-primary`); await p.waitForLoadState('networkidle'); };
const answer = async (p, picks) => { for (const t of picks) { await p.locator('.qstep.is-active label.choice', { hasText: t }).first().click(); await p.waitForTimeout(650); } };
const submitQuiz = async p => { await p.click('[data-q-submit]'); await p.waitForSelector('#confirmModal.show'); await p.waitForTimeout(700); const txt = await p.locator('#confirmModalText').innerText(); await p.click('#confirmModalOk'); await p.waitForURL('**/Result/**'); await p.waitForLoadState('networkidle'); return txt; };

// ================= ADMIN: the learning item, through the wizard =================
const a = await page();
await login(a, 'admin', 'Admin#NewPass99');
await a.goto(base + '/Admin/Authoring/New');
await a.fill('#Title', TITLE); await a.click('label.type-card:has-text("سياسة")');
await a.fill('#Description', 'كيف نتعامل مع بيانات العملاء والموظفين، ومتى نبلغ عن أي تسريب محتمل.');
await nav(a, 'next');
const contentId = +a.url().split('/').pop();
await a.click('.ql-editor');
await a.click('.ql-header'); await a.click('.ql-picker-item[data-value="2"]'); await a.keyboard.type('مبادئ حماية البيانات'); await a.keyboard.press('Enter');
await a.keyboard.type('تُصنّف البيانات حسب حساسيتها، ولا تُشارك إلا عبر القنوات المعتمدة في المؤسسة.'); await a.keyboard.press('Enter');
await a.click('.ql-list[value="ordered"]'); await a.keyboard.type('لا تُرسل بيانات العملاء إلى بريد شخصي.'); await a.keyboard.press('Enter');
await a.keyboard.type('أبلغ فريق الأمن السيبراني فوراً عند الاشتباه بأي تسريب.');
await nav(a, 'next');
await a.click('label.yn-card:has-text("نعم، يتطلب إقراراً")'); await a.click('button[data-fill="Statement"]');
await nav(a, 'next');
await a.click('label.yn-card:has-text("نعم، أضف اختباراً")');
await a.fill('#Settings_Title', QUIZ); await a.fill('#Settings_PassingPercentage', '100'); await a.fill('#Settings_MaxAttempts', '2');
await a.click('button[value=stay]'); await a.waitForLoadState('networkidle');
await a.click('.seg-control label:has-text("صح / خطأ")'); await a.fill('textarea[name="Question.Text"]', 'يجوز إرسال بيانات العملاء إلى البريد الإلكتروني الشخصي للعمل من المنزل.');
await a.click('.q-tf label.yn-card:has-text("خطأ")'); await a.click('button:has-text("حفظ السؤال")'); await a.waitForLoadState('networkidle');
await a.click('.seg-control label:has-text("اختيار واحد")'); await a.fill('textarea[name="Question.Text"]', 'ما أول إجراء عند الاشتباه بتسريب بيانات؟');
for (const [i, t] of ['إبلاغ فريق الأمن السيبراني فوراً', 'حذف الملفات المتأثرة بهدوء', 'الانتظار حتى التأكد التام'].entries()) await a.fill(`input[name="Question.Options[${i}]"]`, t);
await a.locator('[data-opt-row]').nth(0).locator('.opt-correct-wrap').click();
await a.click('button:has-text("حفظ السؤال")'); await a.waitForLoadState('networkidle');
await nav(a, 'next');
ok(a.url().includes('/Preview/'), 'admin: preview step reached');
await nav(a, 'next');
await a.click('button[name=go][value="publish"]'); await a.waitForSelector('#confirmModal.show'); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
const assessmentId = +(await a.content()).match(/assessmentId=(\d+)/)[1];
ok(assessmentId > 0, 'admin: content + assessment published through the wizard');

const nora = await session('nora.harbi', 'Pass#Employee1');
const sara = await session('sara.otaibi', 'Pass#Employee1');

// ================= EMPLOYEE A (screens) =================
const e = await page({ storageState: nora });
await e.goto(base + '/'); await e.waitForLoadState('networkidle');
const d0 = await e.evaluate(() => ({ q: document.querySelector('.section-title h2')?.innerText, focus: !!document.querySelector('.lx-focus .journey'), pct: +document.querySelector('[data-progress]').getAttribute('data-progress'), text: document.querySelector('.lx-progress-text strong')?.innerText }));
ok(d0.q.includes('ما المطلوب مني الآن؟') && d0.focus, 'dashboard: "ما المطلوب مني الآن؟" with the next step and its journey');
ok(/أنجزت \d+ من \d+ مواد/.test(d0.text), `dashboard: overall progress "${d0.text}" (${d0.pct}%)`);
await shot(e, '01-dashboard', 'لوحة الموظف: الترحيب، التقدم العام، "ما المطلوب مني الآن؟" مع رحلة الخطوة التالية، والإنجازات', { wait: 2200 });
await shot(e, '02-journey-component', 'مكوّن الرحلة: المحتوى ← الإقرار ← الاختبار (مقفل) ← الإنجاز', { el: '.lx-focus', wait: 200 });
const task = await e.evaluate(t => { const r = [...document.querySelectorAll('.lx-task, .lx-focus')].find(x => x.innerText.includes(t)); return r && { mini: !!r.querySelector('.journey-mini, .journey'), chip: r.querySelector('.chip')?.innerText, href: r.querySelector('a[href*="/Content/Details/"]')?.getAttribute('href') }; }, TITLE);
ok(task && task.mini && task.chip.includes('مطلوب') && task.href.endsWith(`/Content/Details/${contentId}#sec-ack`), 'dashboard: the new item is listed as "مطلوب" with its journey and a link to the acknowledgment');

await e.goto(base + '/Content/Policies'); await e.waitForLoadState('networkidle');
const card = await e.evaluate(t => { const c = [...document.querySelectorAll('.lx-card')].find(x => x.innerText.includes(t)); return c && c.innerText; }, TITLE);
ok(card && card.includes('مطلوب') && card.includes('الإقرار') && card.includes('الاختبار'), 'content card: status "مطلوب", requires acknowledgment, linked assessment');
await e.evaluate(t => [...document.querySelectorAll('.lx-card')].find(x => x.innerText.includes(t)).scrollIntoView({ block: 'center', behavior: 'instant' }), TITLE);
await shot(e, '03-content-cards', 'بطاقات المحتوى: النوع، الحالة (جديد / مطلوب / تم الإقرار / مكتمل)، متطلبات الإقرار والاختبار');

await e.goto(base + `/Content/Details/${contentId}`); await e.waitForLoadState('networkidle');
const j0 = await e.evaluate(() => [...document.querySelectorAll('.lx-journey-band .j-step')].map(li => li.dataset.step + ':' + li.className.match(/j-(done|current|upcoming|locked|failed)/)[1]).join(' '));
ok(j0 === 'content:current ack:upcoming assessment:locked done:upcoming', 'content page: journey ' + j0);
await shot(e, '04-content-with-journey', 'صفحة المحتوى: شريط "رحلتك في هذه المادة" أعلى المحتوى', { wait: 1400 });
await scrollTo(e, '#sec-ack', 120);
const lk = await e.evaluate(() => { const ns = document.querySelector('.next-step'); return { locked: ns.classList.contains('is-locked'), start: !!ns.querySelector('form[action*="/Assessments/Start/"]'), txt: ns.innerText }; });
ok(lk.locked && !lk.start && lk.txt.includes('الاختبار مقفل') && !lk.txt.includes('ابدأ الاختبار'), 'content page: assessment locked before acknowledgment (no "ابدأ الاختبار")');
await shot(e, '05-locked-assessment', 'قبل الإقرار: لوحة الإقرار ثم بطاقة الاختبار المقفلة "🔒 الاختبار مقفل"');
await e.check('[data-ack-check]'); await e.click('[data-ack-submit]'); await e.waitForLoadState('networkidle');
const un = await e.evaluate(() => ({ done: document.querySelector('.ack-panel.is-done h2')?.innerText, unlocking: !!document.querySelector('.next-step.is-unlocking'), start: document.querySelector('.next-step form button')?.innerText.trim(),
  j: [...document.querySelectorAll('.lx-journey-band .j-step')].map(li => li.dataset.step + ':' + li.className.match(/j-(done|current|upcoming|locked|failed)/)[1]).join(' ') }));
ok(un.done === 'تم الإقرار بنجاح' && un.unlocking && un.start === 'ابدأ الاختبار', 'after acknowledgment: "تم الإقرار بنجاح", unlock transition, "ابدأ الاختبار"');
ok(un.j === 'content:done ack:done assessment:current done:upcoming', 'journey after acknowledgment: ' + un.j);
await shot(e, '06-unlocked-assessment', 'بعد الإقرار: "تم الإقرار بنجاح" وفتح الاختبار مع زر "ابدأ الاختبار"', { wait: 2200 });

// attempt 1: fail (one wrong answer)
await e.click('.next-step form button'); await e.waitForLoadState('networkidle');
const intro = await e.evaluate(() => document.querySelector('[data-quiz-intro]').innerText);
ok(intro.includes('المحاولة') && intro.includes('الوقت التقديري'), 'assessment intro: questions, passing score, attempt and estimated time');
await shot(e, '07-assessment-intro', 'مقدمة الاختبار: عدد الأسئلة، درجة النجاح، المحاولة، الوقت التقديري والتعليمات');
await e.click('[data-quiz-start]'); await e.waitForTimeout(500);
await answer(e, ['صح']);
await e.locator('.qstep.is-active label.choice', { hasText: 'حذف الملفات' }).first().click(); await e.waitForTimeout(400);
ok(await e.locator('.qstep.is-active .choice:has(input:checked)').count() === 1, 'assessment: selected answer state');
await shot(e, '08-assessment-question', 'سؤال الاختبار: شريط التقدم، الإجابة المحددة، التنقل بين الأسئلة');
const confirm1 = await submitQuiz(e);
ok(confirm1.includes('عدد الأسئلة المجاب عنها: 2 من 2'), 'submission confirmation shows the answered summary');
const fail = await e.evaluate(() => ({ h: document.querySelector('.lx-result-hero h1').innerText, status: document.querySelector('.result-status').innerText, btns: [...document.querySelectorAll('.lx-result-actions a, .lx-result-actions button')].map(x => x.innerText.trim()) }));
ok(fail.h === 'لم تحقق درجة الاجتياز هذه المرة' && fail.status.includes('غير مجتاز') && fail.btns.includes('مراجعة المحتوى') && fail.btns.includes('إعادة المحاولة'), 'failed result: encouraging message, review content and retry');
const failUrl = e.url();
await shot(e, '09-result-failed', 'نتيجة غير مجتازة: "لم تحقق درجة الاجتياز هذه المرة" مع مراجعة المحتوى وإعادة المحاولة', { wait: 2600 });
// attempt 2: pass
await e.click('.lx-result-actions button:has-text("إعادة المحاولة")'); await e.waitForLoadState('networkidle');
await e.click('[data-quiz-start]'); await e.waitForTimeout(400);
await answer(e, ['خطأ']); await e.locator('.qstep.is-active label.choice', { hasText: 'إبلاغ فريق' }).first().click(); await e.waitForTimeout(300);
await submitQuiz(e);
const pass = await e.evaluate(() => ({ h: document.querySelector('.lx-result-hero h1').innerText, status: document.querySelector('.result-status').innerText, complete: document.querySelector('.lx-complete h2')?.innerText, isNew: !!document.querySelector('.lx-complete.is-new'), stats: document.querySelector('.lx-result-stats').innerText }));
ok(pass.h === 'أحسنت، اجتزت الاختبار بنجاح' && pass.status.includes('مجتاز') && !pass.status.includes('غير'), 'passed result: "أحسنت، اجتزت الاختبار بنجاح"');
ok(pass.stats.includes('درجتك') && pass.stats.includes('درجة النجاح') && pass.stats.includes('الإجابات الصحيحة') && pass.stats.includes('تاريخ الإكمال'), 'passed result: score, passing score, correct answers, completion date');
ok(pass.complete === 'تم إكمال هذه المادة بنجاح' && pass.isNew, 'passed result: learning item completed ("تم إكمال هذه المادة بنجاح")');
const passUrl = e.url();
await shot(e, '10-result-passed', 'نتيجة مجتازة: لحظة نجاح هادئة، الدرجة ودرجة النجاح والإجابات الصحيحة والتاريخ', { wait: 2800 });
await scrollTo(e, '.lx-complete', 140);
await shot(e, '11-completed-learning-item', 'إكمال المادة: بطاقة "تم إكمال هذه المادة بنجاح" مع الرحلة مكتملة', { wait: 1400 });
await e.goto(base + `/Content/Details/${contentId}`); await e.waitForLoadState('networkidle');
ok(await e.locator('#sec-complete').count() === 1 && await e.locator('[data-ack-check]').count() === 0, 'content page after completion: completion card, acknowledgment not asked again');
await scrollTo(e, '#sec-ack', 120);
await shot(e, '12-content-completed', 'صفحة المحتوى بعد الإكمال: الإقرار، الاختبار المجتاز، وبطاقة الإنجاز', { wait: 1400 });
await e.goto(base + '/'); await e.waitForLoadState('networkidle');
const d1 = await e.evaluate(t => ({ pct: +document.querySelector('[data-progress]').getAttribute('data-progress'), ach: [...document.querySelectorAll('.lx-achievements li')].some(li => li.innerText.includes(t)), todo: [...document.querySelectorAll('.lx-focus, .lx-task')].some(x => x.innerText.includes(t)) }), TITLE);
ok(d1.pct > d0.pct && d1.ach && !d1.todo, `dashboard after completion: progress ${d0.pct}% -> ${d1.pct}%, listed in achievements, no longer a task`);
await shot(e, '13-dashboard-after-completion', 'لوحة الموظف بعد الإكمال: ارتفاع التقدم (+) وظهور المادة في "إنجازاتك الأخيرة"', { wait: 2400 });
await e.goto(base + '/Assessments'); await e.waitForLoadState('networkidle');
await shot(e, '14-assessments-list', 'قائمة الاختبارات: الحالة، المحتوى المرتبط، والإجراء التالي');
await e.goto(base + '/MyResults'); await e.waitForLoadState('networkidle');
ok((await e.locator('.lx-res').count()) >= 2, 'My Results lists the failed and the passed attempt');
await e.context().close();

// ================= MOTION RECORDINGS =================
// 1. dashboard loading + progress animation (employee A, after completion: animates from the previous value)
let r = await recorder(nora);
await r.evaluate(() => 0).catch(() => {});
await r.goto(base + '/Assessments'); await r.waitForTimeout(400);
await r.evaluate(() => { try { localStorage.setItem(Object.keys(localStorage).find(k => k.startsWith('lx-progress-')) || 'x', '0'); } catch (e) {} });
await r.goto(base + '/'); await r.waitForTimeout(3600);
await r.mouse.move(700, 620); await r.waitForTimeout(600);
await glide(r, '.section-title ~ .grid-auto, .lx-tasks', 160, 1400); await r.waitForTimeout(900);
await r.hover('.lx-card >> nth=0'); await r.waitForTimeout(700);
await glide(r, 'body', 0, 1000); await r.waitForTimeout(800);
await saveClip(r, '01-dashboard-progress', 'لوحة الموظف: دخول البطاقات بالتتابع، امتلاء حلقة التقدم والعدّ، شارة الزيادة، وتفاعل المرور');

// 2-6 with employee B (fresh on this item)
r = await recorder(sara);
await r.goto(base + '/'); await r.waitForTimeout(1500); // remembers the starting progress for clip 6
await r.goto(base + `/Content/Details/${contentId}`); await r.waitForLoadState('networkidle'); await r.waitForTimeout(1600);
await glide(r, '#sec-ack', 140, 1300); await r.waitForTimeout(900);
await r.check('[data-ack-check]'); await r.waitForTimeout(700); await r.click('[data-ack-submit]'); await r.waitForLoadState('networkidle');
await r.waitForTimeout(3200);
await saveClip(r, '02-acknowledgment-unlock', 'المحتوى ← الإقرار ← "تم الإقرار بنجاح" وتحوّل القفل إلى فتح الاختبار مع زر "ابدأ الاختبار"');

r = await recorder(sara);
await r.goto(base + `/Content/Details/${contentId}#sec-ack`); await r.waitForLoadState('networkidle'); await r.waitForTimeout(800);
await r.click('.next-step form button'); await r.waitForLoadState('networkidle'); await r.waitForTimeout(1200);
await r.click('[data-quiz-start]'); await r.waitForTimeout(900);
await r.hover('.qstep.is-active label.choice >> nth=1'); await r.waitForTimeout(500);
await answer(r, ['صح']); await r.waitForTimeout(500);
await r.hover('.qstep.is-active label.choice >> nth=2'); await r.waitForTimeout(400);
await r.locator('.qstep.is-active label.choice', { hasText: 'الانتظار' }).first().click(); await r.waitForTimeout(600);
await r.click('[data-q-prev]'); await r.waitForTimeout(900); await r.click('[data-q-next]'); await r.waitForTimeout(700);
await r.click('[data-q-submit]'); await r.waitForSelector('#confirmModal.show'); await r.waitForTimeout(1300); await r.click('#confirmModalOk');
await r.waitForURL('**/Result/**'); await r.waitForTimeout(1200);
const saraFail = r.url();
await saveClip(r, '03-assessment-selection', 'الاختبار: المقدمة، تحديد الإجابات وحالة المرور والتحديد، الانتقال بين الأسئلة، ملخص الإرسال وحالة "جارٍ احتساب نتيجتك"');

r = await recorder(nora);
await r.goto(passUrl); await r.waitForTimeout(4200); await glide(r, '.lx-complete', 140, 1100); await r.waitForTimeout(1600);
await saveClip(r, '04-result-pass', 'نتيجة مجتازة: ظهور الحلقة والعدّ، "أحسنت، اجتزت الاختبار بنجاح"، احتفال صغير لمرة واحدة، وبطاقة إكمال المادة');
r = await recorder(nora);
await r.goto(failUrl); await r.waitForTimeout(3800); await r.hover('.lx-result-actions a'); await r.waitForTimeout(900);
await saveClip(r, '05-result-fail', 'نتيجة غير مجتازة: عرض هادئ غير عقابي مع "مراجعة المحتوى" و"إعادة المحاولة"');

// 6. full journey with a third employee who has not started this item:
//    dashboard -> content (journey) -> acknowledgment -> unlock -> assessment -> passed result + completion -> dashboard (+progress)
const fahad = await session('fahad.qahtani', 'Pass#Employee1');
r = await recorder(fahad);
await r.goto(base + '/'); await r.waitForTimeout(2600);
await r.locator('.lx-focus, .lx-task', { hasText: TITLE }).locator('a').first().click(); await r.waitForLoadState('networkidle');
await r.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' })); await r.waitForTimeout(1800);
await glide(r, '#sec-ack', 140, 1600); await r.waitForTimeout(800);
await r.check('[data-ack-check]'); await r.waitForTimeout(600); await r.click('[data-ack-submit]'); await r.waitForLoadState('networkidle'); await r.waitForTimeout(2600);
await r.click('.next-step form button'); await r.waitForLoadState('networkidle'); await r.waitForTimeout(900);
await r.click('[data-quiz-start]'); await r.waitForTimeout(700);
await answer(r, ['خطأ']); await r.locator('.qstep.is-active label.choice', { hasText: 'إبلاغ فريق' }).first().click(); await r.waitForTimeout(700);
await r.click('[data-q-submit]'); await r.waitForSelector('#confirmModal.show'); await r.waitForTimeout(1000); await r.click('#confirmModalOk');
await r.waitForURL('**/Result/**'); await r.waitForTimeout(4200);
await glide(r, '.lx-complete', 140, 1000); await r.waitForTimeout(1500);
await r.click('.lx-complete a.btn'); await r.waitForLoadState('networkidle'); await r.waitForTimeout(3800);
const fDash = await r.evaluate(t => ({ ach: [...document.querySelectorAll('.lx-achievements li')].some(li => li.innerText.includes(t)), delta: !document.querySelector('[data-progress-delta]').hidden }), TITLE);
await saveClip(r, '06-full-learning-journey', 'الرحلة كاملة: لوحة الموظف ← المحتوى والرحلة ← الإقرار وفتح الاختبار ← الاختبار ← النتيجة وإكمال المادة ← لوحة الموظف وارتفاع التقدم');
ok(saraFail.includes('/Result/'), 'employee B: assessment selection recorded up to the result');
ok(fDash.ach && fDash.delta, 'full journey: completion shows in achievements and the progress increase is indicated on the dashboard');

// reduced motion: everything in place immediately, no errors
const rm = await page({ storageState: nora, reducedMotion: 'reduce' });
await rm.goto(base + '/'); await rm.waitForTimeout(300);
ok(await rm.evaluate(() => [...document.querySelectorAll('[data-journey]')].every(j => j.classList.contains('is-drawn'))), 'reduced motion: journey shown in its final state immediately');
await rm.context().close();

fs.rmSync(MOTION + '/.tmp', { recursive: true, force: true });
console.log(`CHECKS ${checks.filter(c => c.pass).length}/${checks.length}`);
console.log('ERRORS', errs.length ? errs : 'none');
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
fs.writeFileSync(`${OUT}/.clips.json`, JSON.stringify(clips, null, 1));
await b.close();
