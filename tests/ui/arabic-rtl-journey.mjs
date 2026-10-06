import { chromium } from 'playwright-core';
import fs from 'fs';
// Usage: OUT=./ui-out BASE=http://localhost:5080 CHROME=/path/to/chrome node tests/ui/arabic-rtl-journey.mjs
// Needs: npm i playwright-core; a FRESH database seeded with Seed__AdminUsername=admin / Seed__AdminPassword='Admin#Pass12345'.
// Walks the whole Arabic UI as admin and employee, saves a screenshot of every screen to $OUT/shots and writes $OUT/scan.json
// listing every Latin-script word that is visible on each page (so untranslated English is easy to spot).
const L = process.env.OUT || './ui-out';
const base = process.env.BASE || 'http://localhost:5080';
fs.mkdirSync(`${L}/shots`, { recursive: true });
const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==', 'base64');
fs.writeFileSync(`${L}/logo.png`, PNG); fs.writeFileSync(`${L}/pic.png`, PNG);
fs.writeFileSync(`${L}/doc.pdf`, '%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF');
const b = await chromium.launch({ executablePath: process.env.CHROME || undefined, args: ['--no-sandbox'] });
const scan = [];
const errors = [];
async function newCtx() {
  const ctx = await b.newContext({ viewport: { width: 1280, height: 850 }, acceptDownloads: true, locale: 'en-US', ignoreHTTPSErrors: true }); // browser locale is English on purpose
  const p = await ctx.newPage();
  p.on('pageerror', e => errors.push('PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error' && !m.text().includes('404')) errors.push('CONSOLE ' + m.text()); });
  p.on('dialog', d => { errors.push('NATIVE DIALOG: ' + d.message()); d.dismiss(); });
  return p;
}
async function shot(p, name, full = true) {
  await p.waitForLoadState('networkidle');
  await p.screenshot({ path: `${L}/shots/${name}.png`, fullPage: full });
  const t = await p.evaluate(() => {
    const out = [document.body.innerText];
    document.querySelectorAll('[placeholder],[title],[aria-label],[alt]').forEach(e => { ['placeholder', 'title', 'aria-label', 'alt'].forEach(a => { const v = e.getAttribute(a); if (v) out.push(v); }); });
    document.querySelectorAll('option').forEach(o => out.push(o.textContent));
    const dir = document.documentElement.dir, lang = document.documentElement.lang;
    return { text: out.join('\n'), dir, lang, title: document.title };
  });
  const words = [...new Set((t.text.match(/[A-Za-z][A-Za-z_\-\.]{2,}/g) || []))];
  scan.push({ name, dir: t.dir, lang: t.lang, title: t.title, latin: words });
}
const login = async (p, u, pw) => { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); };

// ---------- admin ----------
const a = await newCtx();
await a.goto(base + '/Account/Login'); await shot(a, '01-login');
await login(a, 'admin', 'WrongPass1'); await shot(a, '02-login-error');
await login(a, 'admin', 'Admin#Pass12345');
await shot(a, '03-force-change-password');
await a.fill('#Current', 'Admin#Pass12345'); await a.fill('#New', 'short'); await a.fill('#Confirm', 'other'); await a.click('main button.btn-primary'); await shot(a, '04-change-password-errors');
await a.fill('#Current', 'Admin#Pass12345'); await a.fill('#New', 'Admin#NewPass99'); await a.fill('#Confirm', 'Admin#NewPass99'); await a.click('main button.btn-primary');
await a.goto(base + '/Admin/Dashboard'); await shot(a, '05-admin-dashboard');

// branding
await a.goto(base + '/Admin/Settings');
await a.fill('[name=OrgName]', 'شركة الأمان الرقمي'); await a.fill('[name=SystemName]', 'منصة التوعية بالأمن السيبراني');
await a.fill('[name=FooterText]', 'جميع الحقوق محفوظة - إدارة الأمن السيبراني'); await a.fill('[name=WelcomeText]', 'نرحب بكم في منصة التوعية والامتثال بالأمن السيبراني.');
await a.fill('[name=LoginSubtitle]', 'للاستخدام الداخلي المصرّح به فقط.');
await a.setInputFiles('input[name=logo]', `${L}/logo.png`);
await shot(a, '06-settings-branding');
await a.click('button:has-text("حفظ الهوية البصرية")'); await shot(a, '07-settings-saved');
await a.click('button[data-bs-target="#t-mail"]'); await shot(a, '08-settings-smtp', false);
await a.click('button[data-bs-target="#t-general"]'); await shot(a, '09-settings-general', false);

// users
await a.goto(base + '/Admin/Users/Create'); await shot(a, '10-user-form');
for (const [u, n, e, d] of [['sara', 'سارة العتيبي', 'sara@company.local', 'الموارد البشرية'], ['omar', 'عمر الشهري', 'omar@company.local', 'تقنية المعلومات']]) {
  await a.goto(base + '/Admin/Users/Create');
  await a.fill('#Username', u); await a.fill('#DisplayName', n); await a.fill('#Email', e); await a.fill('#Department', d); await a.fill('#Password', 'Employee#Pass123');
  await a.click('main button.btn-primary');
}
await a.goto(base + '/Admin/Users/Create'); await a.click('main button.btn-primary'); await shot(a, '11-user-form-errors');
await a.goto(base + '/Admin/Users'); await shot(a, '12-users-list');

// content with rich text
await a.goto(base + '/Admin/Content/Create'); await a.waitForSelector('.ql-editor');
await a.click('main button[name=publish][value=true]'); await shot(a, '13-content-validation-error');
await a.fill('#Title', 'سياسة كلمات المرور'); await a.fill('#Description', 'ضوابط إنشاء كلمات المرور وحمايتها في المؤسسة.');
await a.selectOption('#Type', '1'); await a.check('#RequiresAcknowledgment');
await a.click('.ql-editor');
await a.selectOption('.ql-header select', '2').catch(() => {});
await a.keyboard.type('الغرض من السياسة'); await a.keyboard.press('Enter');
await a.click('button.ql-list[value=bullet]');
await a.keyboard.type('يجب ألا تقل كلمة المرور عن 12 خانة'); await a.keyboard.press('Enter'); await a.keyboard.type('يُمنع مشاركة كلمة المرور مع الآخرين'); await a.keyboard.press('Enter'); await a.keyboard.press('Enter');
await a.keyboard.type('For more details see the ISO 27001 guidance مع نص عربي.'); await a.keyboard.press('Enter');
const [fc] = await Promise.all([a.waitForEvent('filechooser'), a.click('button.ql-image')]); await fc.setFiles(`${L}/pic.png`);
await a.waitForSelector('.ql-editor img');
await a.setInputFiles('input[name=files]', [`${L}/doc.pdf`, `${L}/pic.png`]);
await a.fill('#ExternalUrl', 'https://intranet.company.local/security');
await shot(a, '14-content-form-filled');
await a.click('main button[name=publish][value=true]'); await shot(a, '15-content-list');
// draft + delete via Arabic confirmation modal
await a.goto(base + '/Admin/Content/Create'); await a.fill('#Title', 'مسودة للحذف'); await a.click('main button[name=publish][value=false]');
await a.click('tr:has-text("مسودة للحذف") button[title="حذف"]'); await a.waitForSelector('#confirmModal.show'); await shot(a, '16-confirm-modal', false);
await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
await a.goto(base + '/Admin/Content/Create'); await a.fill('#Title', 'ضوابط الأمن السيبراني للأجهزة'); await a.selectOption('#Type', '2'); await a.click('main button[name=publish][value=true]');

// assessment
await a.goto(base + '/Admin/Assessments/Create'); await a.fill('#Title', 'اختبار التوعية بالتصيّد الإلكتروني'); await a.fill('#Description', 'اختبار قصير للتعرّف على رسائل التصيّد.');
await a.selectOption('#ContentId', { label: 'سياسة كلمات المرور' }); await a.fill('#PassingPercentage', '60'); await a.fill('#MaxAttempts', '0');
await shot(a, '17-assessment-form'); await a.click('main button.btn-primary');
const addQ = async (text, type, opts, correct, tf) => {
  await a.click('a:has-text("إضافة سؤال")'); await a.fill('#Text', text); await a.selectOption('#qtype', String(type));
  if (type === 2) { await a.check(tf ? '#tft' : '#tff'); } else { for (let i = 0; i < opts.length; i++) await a.fill(`[name="Options[${i}]"]`, opts[i]); for (const c of correct) await a.locator('.opt-correct').nth(c).check(); }
  return a;
};
await addQ('ما الإجراء الصحيح عند وصول رسالة بريد مشبوهة تطلب كلمة مرورك؟', 1, ['الرد عليها بكلمة المرور', 'الإبلاغ عنها وحذفها', 'إعادة توجيهها للزملاء'], [1]);
await shot(a, '18-question-form'); await a.click('main button.btn-primary');
await addQ('يمكن مشاركة كلمة المرور مع الزملاء الموثوقين.', 2, [], [], false); await a.click('main button.btn-primary');
await addQ('اختر السلوكيات الآمنة:', 3, ['تفعيل المصادقة متعددة العوامل', 'استخدام كلمة المرور نفسها في كل مكان', 'قفل الشاشة عند مغادرة المكتب'], [0, 2]); await a.fill('#Points', '2'); await a.click('main button.btn-primary');
await shot(a, '19-questions-list');
await a.goto(base + '/Admin/Assessments'); await a.click('tr:has-text("التصيّد") form[action*="SetStatus"] button'); await shot(a, '20-assessments-list');
await a.goto(base + '/Admin/Content'); await shot(a, '21-content-list-2');

// ---------- employee ----------
const e = await newCtx();
await login(e, 'sara', 'Employee#Pass123');
await e.fill('#Current', 'Employee#Pass123'); await e.fill('#New', 'Sara#NewPass123'); await e.fill('#Confirm', 'Sara#NewPass123'); await e.click('main button.btn-primary');
await e.goto(base + '/'); await shot(e, '30-employee-dashboard');
await e.goto(base + '/Content'); await shot(e, '31-content-list');
await e.goto(base + '/Content/Policies'); await shot(e, '32-policies');
await e.click('a:has-text("سياسة كلمات المرور")'); await shot(e, '33-content-details');
await e.click('button:has-text("إقرار")'); await shot(e, '34-acknowledged');
await e.goto(base + '/Assessments'); await shot(e, '35-assessments');
await e.click('button:has-text("بدء الاختبار")'); await shot(e, '36-take');
await e.check('label:has-text("الإبلاغ عنها وحذفها")'); await e.check('label:has-text("خطأ")');
await e.check('label:has-text("تفعيل المصادقة")'); await e.check('label:has-text("قفل الشاشة")');
await e.click('main button.btn-lg'); await e.waitForSelector('#confirmModal.show'); await shot(e, '37-submit-confirm', false); await e.click('#confirmModalOk');
await e.waitForURL('**/Result/**'); await shot(e, '38-result');
await e.goto(base + '/MyResults'); await shot(e, '39-my-results');
await e.goto(base + '/Content/Details/99999'); await shot(e, '40-not-found');
await e.goto(base + '/Admin/Dashboard'); await shot(e, '41-denied');

// ---------- admin reports ----------
await a.goto(base + '/Admin/Dashboard'); await shot(a, '50-admin-dashboard-data');
await a.goto(base + '/Admin/Reports/Assessments'); await shot(a, '51-report-assessments');
await a.goto(base + '/Admin/Reports/Attempts'); await shot(a, '52-report-attempts');
await a.click('a:has-text("عرض التفاصيل")'); await shot(a, '53-attempt-details');
await a.goto(base + '/Admin/Reports/Acknowledgments'); await shot(a, '54-report-acks');
const [dl] = await Promise.all([a.waitForEvent('download'), a.click('a:has-text("تصدير Excel")')]);
console.log('XLSX download:', dl.suggestedFilename());
await a.goto(base + '/Admin/Reports/Acknowledgments');
const [dl2] = await Promise.all([a.waitForEvent('download'), a.click('a:has-text("تصدير CSV")')]);
console.log('CSV download:', dl2.suggestedFilename()); await dl2.saveAs(`${L}/ack.csv`);
await a.goto(base + '/Admin/Audit'); await shot(a, '55-audit');
await a.goto(base + '/Admin/Content'); await a.click('tr:has-text("سياسة كلمات المرور") button[title="إرسال بريد إلى الموظفين"]'); await a.waitForSelector('#confirmModal.show'); await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle'); await shot(a, '55b-mail-queued');
await a.goto(base + '/Admin/Content'); await a.click('tr:has-text("سياسة كلمات المرور") a[title="تعديل"]'); await shot(a, '56-content-edit');
await a.goto(base + '/Admin/Assessments'); await shot(a, '57-assessments-list-final');
// login page with branding + English toggle check
const g = await newCtx(); await g.goto(base + '/Account/Login'); await shot(g, '60-login-branded');
fs.writeFileSync(`${L}/scan.json`, JSON.stringify(scan, null, 1));
console.log('errors', JSON.stringify(errors, null, 1));
await b.close();
