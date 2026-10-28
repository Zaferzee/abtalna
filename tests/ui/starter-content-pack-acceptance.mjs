// Usage: BASE=http://localhost:5150 OUT=docs/screenshots/starter-content-pack-v1 ANSWERS=answers.json CHROME=/path/to/chrome node tests/ui/starter-content-pack-acceptance.mjs
// Needs: npm i playwright-core; a FRESH Development database where only the seeded administrator exists (admin / Admin#Pass12345)
// and the starter pack was installed explicitly (dotnet CyberLms.Web.dll --install-starter-pack). ANSWERS is a JSON map
// {questionId: correctOptionId} of the phishing lesson's assessment (see tools/starter-pack/run-acceptance.sh).
// Checks through the UI: content list and references (admin only), default audiences, SPC-001 and policy templates not visible to
// employees, Arabic/RTL rendering, local illustrations, no external network request, the AI acknowledgment gate, an assessment with
// the 80% pass mark, results in the reports, an entity-targeting example, and normal content creation still working.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5150', OUT = process.env.OUT;
const ANSWERS = JSON.parse(fs.readFileSync(process.env.ANSWERS, 'utf8'));
fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const errs = [], shots = [], checks = [], external = [];
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); console.log((cond ? 'PASS ' : 'FAIL ') + what); };
const ADMIN_NEW = 'Admin#NewPass99', PASS = 'Employee#Pass123', NEWPASS = 'Employee#NewPass99';
const host = new URL(base).host;
async function page(opts = {}) {
  const c = await b.newContext({ viewport: { width: 1440, height: 900 }, ...opts }); const p = await c.newPage();
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error' && !/status of 40[34]/.test(m.text())) errs.push(p.url() + ' ' + m.text()); });
  p.on('request', r => { const u = new URL(r.url()); if (!['data:', 'blob:', 'about:'].includes(u.protocol) && u.host !== host) external.push(r.url()); });
  p.on('dialog', d => d.accept());
  return p;
}
async function shot(p, name, desc, { wait = 700, full = false } = {}) {
  await p.waitForTimeout(wait);
  await p.screenshot({ path: `${OUT}/${name}.png`, fullPage: full });
  shots.push({ name, desc }); console.log('shot', name);
}
async function login(p, u, pw, newPw) {
  await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw);
  await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle');
  if (p.url().includes('ChangePassword')) {
    await p.fill('#Current', pw); await p.fill('#New', newPw); await p.fill('#Confirm', newPw); await p.click('main button.btn-primary'); await p.waitForLoadState('networkidle');
  }
}
const go = async (p, path) => { const r = await p.goto(base + path); await p.waitForLoadState('networkidle'); return r.status(); };
const rtlOk = p => p.evaluate(() => document.documentElement.dir === 'rtl' && document.documentElement.lang.startsWith('ar') && document.documentElement.scrollWidth <= innerWidth + 1);
const imagesLoaded = p => p.evaluate(() => [...document.querySelectorAll('.doc-body img')].map(i => ({ src: i.getAttribute('src'), ok: i.complete && i.naturalWidth > 0 })));

// ======================================================= central administrator
const a = await page();
await login(a, 'admin', 'Admin#Pass12345', ADMIN_NEW);
await go(a, '/Admin/Content');
const list = await a.evaluate(() => [...document.querySelectorAll('tbody tr')].map(r => ({
  title: r.querySelector('.t-name')?.textContent.trim(), id: +(r.querySelector('.t-name')?.getAttribute('href') || '').split('/').pop(),
  status: r.querySelector('.badge')?.textContent.trim(), refs: [...r.querySelectorAll('.ref-chip')].map(x => x.textContent.trim()), aud: r.querySelector('.t-meta')?.textContent || '' })));
const byTitle = t => list.find(x => x.title === t);
const T = {
  awt1: 'الأمن السيبراني مسؤولية الجميع', awt2: 'التصيد والهندسة الاجتماعية', awt8: 'الذكاء الاصطناعي والأدوات غير المعتمدة',
  spc: 'الاستخدام الآمن للأنظمة الحساسة', pol1: 'سياسة الاستخدام المقبول للأصول التقنية', pol4: 'مسؤولية الإبلاغ عن الحوادث والأحداث المشبوهة',
};
ok(list.length === 16, `content list: 16 starter items (${list.length})`);
ok(list.filter(x => x.status === 'منشور').length === 11 && list.filter(x => x.status === 'مسودة').length === 5, 'status: 11 published lessons; SPC-001 and the 4 policy templates are drafts');
ok(byTitle(T.awt2).refs.some(r => r.includes('مرجع استرشادي: ECC 2:2024 — 1-10-3-1')), 'admin list shows the guidance reference (ECC 2:2024 — 1-10-3-1)');
ok(byTitle(T.pol1).refs.some(r => r.includes('نموذج استرشادي')) && byTitle(T.awt8).refs.some(r => r.includes('نموذج استرشادي')), 'policy templates and the AI acknowledgment are flagged as templates to review');
ok(byTitle(T.awt1).aud.includes('جميع الجهات') || byTitle(T.awt1).aud.includes('الجهة المالكة وجميع'), 'AWT items target all entities');
await shot(a, '06-admin-content-list', 'قائمة إدارة المحتوى للمدير المركزي: 16 عنصرًا من الحزمة الأولية مع المرجع الاسترشادي وحالة النشر والجمهور');
await go(a, `/Admin/Authoring/Basics/${byTitle(T.awt2).id}`);
ok(await a.evaluate(() => [...document.querySelectorAll('.starter-ref .ref-chip')].some(x => x.textContent.includes('AWT-002'))), 'authoring page shows the pack code, reference, audience and media slot');
// assessments: 12 with 8 questions and 80%
await go(a, '/Admin/Assessments');
const asm = await a.evaluate(() => document.body.innerText);
ok((asm.match(/80%/g) || []).length >= 12, 'assessments list: pass mark 80% on the starter assessments');

// policy template preview (draft: administrators only)
await go(a, `/Admin/Authoring/Preview/${byTitle(T.pol1).id}`);
const pol = await a.evaluate(() => ({ banner: document.querySelector('.doc-body blockquote')?.innerText || '', ack: !!document.querySelector('#sec-ack') }));
ok(pol.banner.includes('نموذج استرشادي تجريبي — يجب استبداله أو مراجعته واعتماده من الجهة قبل الاستخدام الإنتاجي.') && pol.ack, 'policy template: banner at the top and a mandatory acknowledgment');
ok((await go(a, `/Content/Details/${byTitle(T.pol1).id}`)) === 200, 'administrator can open the draft policy page');
await a.evaluate(() => { document.querySelector('.doc-body blockquote').scrollIntoView({ block: 'start' }); window.scrollBy(0, -120); });
await shot(a, '05-policy-template', 'نموذج سياسة (مسودة) في المعاينة: شريط «نموذج استرشادي تجريبي» في البداية وإقرار إلزامي');

// entities: a ministry with a critical-systems unit
async function entity(name, code, parent) {
  await go(a, '/Admin/Entities/Create'); await a.fill('#f-Name', name); await a.fill('#f-Code', code);
  await a.selectOption('#f-ParentEntityId', String(parent));
  await a.click('form button.btn-primary.btn-lg'); await a.waitForLoadState('networkidle');
  return +a.url().match(/Details\/(\d+)/)[1];
}
await go(a, '/Admin/Entities');
const root = await a.evaluate(() => +document.querySelector('.entity-table a[href*="/Admin/Entities/Details/"]').getAttribute('href').split('/').pop());
const agency = await entity('هيئة الخدمات الرقمية', 'DSA', root);
const critical = await entity('مركز تشغيل الأنظمة الحساسة', 'DSA-OPS', agency);
async function user(u, n, e) {
  await go(a, '/Admin/Users/Create'); await a.fill('#Username', u); await a.fill('#DisplayName', n); await a.fill('#Email', u + '@gov.local');
  await a.selectOption('#AuthSource', 'Local'); await a.fill('#Password', PASS); await a.selectOption('#f-EntityId', String(e));
  await a.click('main form button.btn-primary'); await a.waitForLoadState('networkidle');
}
await user('noura.employee', 'نورة السبيعي', agency);
await user('ops.engineer', 'خالد المالكي', critical);

// ======================================================= employee (sub-entity)
const e = await page();
await login(e, 'noura.employee', PASS, NEWPASS);
await go(e, '/');
ok(await rtlOk(e), 'employee dashboard: Arabic, RTL, no horizontal overflow');
await shot(e, '01-employee-dashboard', 'لوحة الموظف: محتوى الحزمة الأولية موزع على جميع الجهات (موظفة في جهة تابعة)');
await go(e, '/Content');
const empList = await e.evaluate(() => [...document.querySelectorAll('a[href*="/Content/Details/"]')].map(x => +x.getAttribute('href').split('/').pop().split('#')[0]));
ok(new Set(empList).size === 11, `employee sees the 11 published lessons (${new Set(empList).size})`);
ok(!empList.includes(byTitle(T.spc).id) && !empList.includes(byTitle(T.pol1).id), 'SPC-001 and policy templates are not visible to employees');
ok((await go(e, `/Content/Details/${byTitle(T.spc).id}`)) === 404, 'SPC-001 by direct URL: not found for a non-targeted employee');

// phishing lesson
await go(e, `/Content/Details/${byTitle(T.awt2).id}`);
const imgs = await imagesLoaded(e);
const lesson = await e.evaluate(() => ({ h2: [...document.querySelectorAll('.doc-body h2')].map(h => h.textContent), text: document.querySelector('.doc-body').innerText }));
ok(imgs.length === 1 && imgs[0].ok && imgs[0].src === '/img/starter-pack/phishing.svg', 'phishing lesson: local illustration loads');
ok(lesson.h2.includes('تذكر') && lesson.h2.length >= 5 && !/مرجع استرشادي|1-10-3-1/.test(lesson.text), 'lesson: sections + "تذكر", no control numbers for employees');
ok(await rtlOk(e), 'lesson: RTL, no horizontal overflow');
await shot(e, '02-phishing-lesson', 'درس «التصيد والهندسة الاجتماعية»: هدف الدرس ورسم توضيحي محلي وأقسام قصيرة');
await shot(e, '02b-phishing-lesson-full', 'درس التصيد كاملًا', { full: true });

// assessment (phishing): 8 questions, all answered correctly
const startForm = await e.$('form[action^="/Assessments/Start/"]');
ok(!!startForm, 'phishing lesson: assessment can be started');
await startForm.evaluate(f => f.submit()); await e.waitForLoadState('networkidle');
await e.click('[data-quiz-start]').catch(() => {});
const qcount = await e.evaluate(() => document.querySelectorAll('fieldset.qstep').length);
ok(qcount === 8, `assessment has 8 questions (${qcount})`);
for (const [q, o] of Object.entries(ANSWERS)) await e.evaluate(id => { const i = document.getElementById('o_' + id); i.checked = true; i.dispatchEvent(new Event('change', { bubbles: true })); }, o);
await shot(e, '03-assessment', 'اختبار درس التصيد: ثمانية أسئلة سلوكية بخيارات عربية');
await Promise.all([e.waitForURL(/\/Assessments\/Result\//), e.evaluate(() => HTMLFormElement.prototype.submit.call(document.querySelector('form[data-quiz]')))]);
await e.waitForLoadState('networkidle'); await e.waitForTimeout(1500);
const res = await e.evaluate(() => document.body.innerText);
ok(res.includes('100%') && res.includes('اجتزت الاختبار بنجاح') && res.includes('80%'), 'result: 100%, passed (pass mark 80%)');
await shot(e, '03b-assessment-result', 'نتيجة الاختبار ومراجعة الإجابات');

// AI lesson: acknowledgment gate
await go(e, `/Content/Details/${byTitle(T.awt8).id}`);
const gate = await e.evaluate(() => ({ locked: document.body.innerText.includes('الاختبار مقفل'), ack: document.querySelector('#sec-ack .ack-check')?.innerText || '', start: !!document.querySelector('form[action^="/Assessments/Start/"]') }));
ok(gate.locked && !gate.start && gate.ack.startsWith('أقر بأنني اطلعت على متطلبات الاستخدام الآمن للأدوات الرقمية والذكاء الاصطناعي'), 'AI lesson: assessment locked until the acknowledgment (exact statement)');
ok((await imagesLoaded(e)).every(i => i.ok), 'AI lesson: illustration loads');
await e.evaluate(() => document.querySelector('#sec-ack').scrollIntoView({ block: 'center' }));
await shot(e, '04-ai-acknowledgment-gate', 'درس الذكاء الاصطناعي: الإقرار الإلزامي قبل فتح الاختبار (نص الإقرار نموذج استرشادي يُراجع قبل الإنتاج)');
await e.check('[data-ack-check]'); await e.click('[data-ack-submit]'); await e.waitForLoadState('networkidle');
ok(await e.evaluate(() => !!document.querySelector('form[action^="/Assessments/Start/"]')), 'after acknowledging: the AI assessment is unlocked');

// ======================================================= entity targeting example: SPC-001 to the critical-systems unit
await go(a, `/Admin/Authoring/Basics/${byTitle(T.spc).id}`);
await a.check(`input[name=Audience][value="2"]`);
await a.check(`input[name=TargetIds][value="${critical}"]`);
await a.evaluate(() => { document.querySelector('input[name=TargetIds]:checked').scrollIntoView({ block: 'center' }); window.scrollBy(0, 160); });
await shot(a, '07-entity-targeting', 'مثال استهداف: المدير المركزي يوجّه SPC-001 إلى «مركز تشغيل الأنظمة الحساسة» فقط');
await a.click('button[name=go][value=draft]'); await a.waitForLoadState('networkidle');
await go(a, `/Admin/Authoring/Publish/${byTitle(T.spc).id}`);
await a.click('button[name=go][value=publish]'); await a.waitForSelector('#confirmModal.show'); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
const ops = await page();
await login(ops, 'ops.engineer', PASS, NEWPASS);
const opsSees = (await go(ops, `/Content/Details/${byTitle(T.spc).id}`)) === 200;
const nouraSees = (await go(e, `/Content/Details/${byTitle(T.spc).id}`)) === 200;
ok(opsSees && !nouraSees, 'after targeting: SPC-001 reaches the critical-systems unit only');

// ======================================================= reports
await go(a, '/Admin/Reports/Assessments?status=Passed');
await shot(a, '08-report', 'تقرير الاختبارات: نتيجة الموظفة في اختبار درس التصيد (نجاح)');
const rep = await a.evaluate(() => document.body.innerText);
ok(rep.includes('نورة السبيعي') || rep.includes('noura.employee'), 'report recognizes the employee result');
await go(a, `/Admin/Reports/Acknowledgments?contentId=${byTitle(T.awt8).id}&status=Acknowledged`);
ok((await a.evaluate(() => document.body.innerText)).includes('نورة السبيعي'), 'acknowledgment report recognizes the AI acknowledgment');

// ======================================================= normal content still works
await go(a, '/Admin/Authoring/New');
await a.fill('#Title', 'تعميم داخلي تجريبي');
await a.check('input[name=Type][value="6"]');
await a.click('button[name=go][value=next]'); await a.waitForLoadState('networkidle');
const newId = +(a.url().match(/\/(\d+)$/) || [])[1];
ok(newId > 0, 'a new content item can still be created manually');
await go(a, `/Admin/Authoring/Basics/${newId}`);
await a.fill('#Title', 'تعميم داخلي تجريبي (معدل)');
await a.click('button[name=go][value=draft]'); await a.waitForLoadState('networkidle');
await go(a, '/Admin/Content');
ok((await a.evaluate(() => document.body.innerText)).includes('تعميم داخلي تجريبي (معدل)'), 'and edited');

ok(external.length === 0, 'no external network request (' + (external.slice(0, 3).join(', ') || 'none') + ')');
console.log('CHECKS ' + checks.filter(x => x.pass).length + '/' + checks.length);
console.log('ERRORS ' + (errs.length ? errs.join('\n') : 'none'));
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
await b.close();
