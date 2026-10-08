// Usage: BASE=http://localhost:5093 OUT=./out ASSETS=./assets CHROME=/path/to/chrome node tests/ui/content-authoring-acceptance.mjs
// Needs: npm i playwright-core; the Arabic demo data set used for the screenshots (admin / Admin#NewPass99, employee nora.harbi / Pass#Employee1).
// UI acceptance: an administrator creates "لائحة المخالفات الداخلية للأمن السيبراني" through the wizard (no database inserts),
// then an employee reads, acknowledges, takes the assessment; the admin checks the report.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5093', OUT = process.env.OUT, A = process.env.ASSETS;
fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const errs = [], shots = [], checks = [];
const W = 1440, H = 900;
async function page() {
  const c = await b.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });
  const p = await c.newPage();
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push(p.url() + ' ' + m.text()); });
  p.on('dialog', d => { errs.push('NATIVE DIALOG ' + d.type() + ' ' + d.message()); d.accept(); });
  return p;
}
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); if (!cond) console.log('CHECK FAILED:', what); };
async function shot(p, name, desc, { full = true, wait = 1500 } = {}) {
  await p.waitForLoadState('networkidle'); await p.evaluate(() => document.fonts.ready);
  const vp = p.viewportSize();
  if (full) {
    let h = await p.evaluate(() => Math.max(document.documentElement.scrollHeight, document.body.scrollHeight));
    await p.evaluate(() => window.scrollTo(0, 0));
    await p.setViewportSize({ width: vp.width, height: Math.min(Math.max(h, vp.height), 5200) });
  }
  await p.waitForTimeout(wait);
  await p.screenshot({ path: `${OUT}/${name}.png` });
  if (full) await p.setViewportSize(vp);
  shots.push({ name, desc, full }); console.log('shot', name);
}
const login = async (p, u, pw) => { await p.goto(base + '/Account/Login'); await p.fill('#Username', u); await p.fill('#Password', pw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle'); };
const nav = async (p, go) => { await p.click(`.wz-nav button[value="${go}"], .wz-nav a.btn-primary`); await p.waitForLoadState('networkidle'); };

// ---------- the PDF attachment (rendered by the browser: a real PDF file) ----------
{
  const p = await page();
  await p.setContent(`<html dir="rtl"><body style="font-family:'Noto Naskh Arabic','Noto Sans Arabic',Tahoma,sans-serif;padding:60px;line-height:2">
    <h1 style="color:#1f4fd8">لائحة المخالفات الداخلية للأمن السيبراني</h1><p>النسخة المعتمدة - الإصدار 1.0</p>
    <h2>جدول المخالفات والجزاءات</h2><table border="1" cellpadding="8" style="border-collapse:collapse;width:100%"><tr><th>المخالفة</th><th>الجزاء</th></tr>
    <tr><td>مشاركة كلمة المرور</td><td>إنذار كتابي</td></tr><tr><td>تعطيل برامج الحماية</td><td>خصم يوم</td></tr></table></body></html>`);
  await p.pdf({ path: `${A}/لائحة-المخالفات.pdf`, format: 'A4' });
  await p.context().close();
}

// Representative regulation as Word puts it on the clipboard (Word HTML: mso classes, inline fonts/colours, a Word numbered list and a table).
const WORD_HTML = `<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word" xmlns="http://www.w3.org/TR/REC-html40">
<head><style><!-- @list l0:level1 { mso-level-number-format:arabic-abjad; } @list l1:level1 { mso-level-text:"%1."; } --></style></head><body lang=AR-SA dir=RTL>
<h2 dir=RTL><span lang=AR-SA style='font-family:"Sakkal Majalla";color:#2F5496'>المادة الأولى: الغرض</span></h2>
<p class=MsoNormal dir=RTL style='text-align:justify'><span lang=AR-SA style='font-size:14.0pt;font-family:"Sakkal Majalla"'>تهدف هذه اللائحة إلى تحديد المخالفات المتعلقة بالأمن السيبراني والجزاءات المترتبة عليها، بما يضمن حماية الأصول المعلوماتية للمؤسسة ورفع مستوى الالتزام بالسياسات المعتمدة.<o:p></o:p></span></p>
<h2 dir=RTL><span lang=AR-SA>المادة الثانية: النطاق</span></h2>
<p class=MsoNormal dir=RTL><span lang=AR-SA style='font-family:"Sakkal Majalla";color:red'>تسري أحكام هذه اللائحة على <b>جميع الموظفين والمتعاقدين</b> ومن في حكمهم ممن يستخدمون أنظمة المؤسسة أو شبكاتها أو بياناتها.<o:p></o:p></span></p>
<h2 dir=RTL><span lang=AR-SA>المادة الثالثة: المخالفات والجزاءات</span></h2>
<p class=MsoNormal dir=RTL><span lang=AR-SA>يبين الجدول التالي المخالفات والجزاءات المتدرجة بحسب تكرار المخالفة خلال سنة:<o:p></o:p></span></p>
<table class=MsoTableGrid border=1 cellspacing=0 cellpadding=0 dir=rtl style='border-collapse:collapse;mso-yfti-tbllook:1184'>
<tr><td style='background:#D9E2F3'><p class=MsoNormal dir=RTL><b>المخالفة</b></p></td><td style='background:#D9E2F3'><p class=MsoNormal dir=RTL><b>المرة الأولى</b></p></td><td style='background:#D9E2F3'><p class=MsoNormal dir=RTL><b>المرة الثانية</b></p></td><td style='background:#D9E2F3'><p class=MsoNormal dir=RTL><b>المرة الثالثة</b></p></td></tr>
<tr><td><p class=MsoNormal dir=RTL>مشاركة كلمة المرور مع الغير</p></td><td><p class=MsoNormal dir=RTL>إنذار كتابي</p></td><td><p class=MsoNormal dir=RTL>خصم يوم من الراتب</p></td><td><p class=MsoNormal dir=RTL>خصم ثلاثة أيام</p></td></tr>
<tr><td><p class=MsoNormal dir=RTL>تعطيل برامج الحماية أو تجاوزها</p></td><td><p class=MsoNormal dir=RTL>خصم يوم من الراتب</p></td><td><p class=MsoNormal dir=RTL>خصم ثلاثة أيام</p></td><td><p class=MsoNormal dir=RTL>الإحالة للتحقيق</p></td></tr>
<tr><td><p class=MsoNormal dir=RTL>فتح مرفقات مشبوهة دون الإبلاغ عنها</p></td><td><p class=MsoNormal dir=RTL>تنبيه شفهي</p></td><td><p class=MsoNormal dir=RTL>إنذار كتابي</p></td><td><p class=MsoNormal dir=RTL>خصم يوم من الراتب</p></td></tr>
<tr><td><p class=MsoNormal dir=RTL>نقل بيانات المؤسسة إلى وسائط أو حسابات شخصية</p></td><td><p class=MsoNormal dir=RTL>خصم ثلاثة أيام</p></td><td><p class=MsoNormal dir=RTL>الإحالة للتحقيق</p></td><td><p class=MsoNormal dir=RTL>الإحالة للتحقيق</p></td></tr>
<tr><td><p class=MsoNormal dir=RTL>ترك الجهاز مفتوحاً دون قفل الشاشة</p></td><td><p class=MsoNormal dir=RTL>تنبيه شفهي</p></td><td><p class=MsoNormal dir=RTL>إنذار كتابي</p></td><td><p class=MsoNormal dir=RTL>خصم يوم من الراتب</p></td></tr>
</table>
<h2 dir=RTL><span lang=AR-SA>المادة الرابعة: أحكام عامة</span></h2>
<p class=MsoListParagraphCxSpFirst dir=RTL style='margin-right:.5in;mso-list:l1 level1 lfo1'><span style='mso-list:Ignore'>1.<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp;</span></span><span lang=AR-SA>تُطبَّق الجزاءات وفق نظام العمل والأنظمة الداخلية المعتمدة.</span></p>
<p class=MsoListParagraphCxSpMiddle dir=RTL style='margin-right:.5in;mso-list:l1 level1 lfo1'><span style='mso-list:Ignore'>2.<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp;</span></span><span lang=AR-SA>تُحتسب المخالفة متكررة إذا وقعت خلال اثني عشر شهراً من المخالفة السابقة.</span></p>
<p class=MsoListParagraphCxSpMiddle dir=RTL style='margin-right:.5in;mso-list:l1 level1 lfo1'><span style='mso-list:Ignore'>3.<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp;</span></span><span lang=AR-SA>يحق للموظف التظلم من الجزاء خلال خمسة عشر يوماً من إبلاغه.</span></p>
<p class=MsoListParagraphCxSpLast dir=RTL style='margin-right:.5in;mso-list:l1 level1 lfo1'><span style='mso-list:Ignore'>4.<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp;</span></span><span lang=AR-SA>تُراجع هذه اللائحة سنوياً من قِبل إدارة الأمن السيبراني.</span></p>
<p class=MsoNormal dir=RTL><span lang=AR-SA>للاستفسار يرجى التواصل مع إدارة الأمن السيبراني.</span><script>alert('x')</script></p>
</body></html>`;

// ================= ADMIN: wizard =================
const a = await page();
await login(a, 'admin', 'Admin#NewPass99');
await a.goto(base + '/Admin/Content');
await a.click('a.btn-primary:has-text("إنشاء مادة جديدة")'); await a.waitForLoadState('networkidle');
// Step 1
await a.fill('#Title', 'لائحة المخالفات الداخلية للأمن السيبراني');
await a.click('label.type-card:has-text("لائحة")');
await a.fill('#Description', 'تحدد المخالفات المتعلقة بالأمن السيبراني والجزاءات المتدرجة المترتبة عليها، وتسري على جميع الموظفين والمتعاقدين.');
await shot(a, '01-wizard-basic-information', 'المعالج - الخطوة 1: المعلومات الأساسية (العنوان، نوع "لائحة"، وصف مختصر)', { full: false });
await nav(a, 'next');
ok(a.url().includes('/Admin/Authoring/Write/'), 'step 1 saved, moved to Write');
const contentId = +a.url().split('/').pop();

// Step 2: type an introduction, paste the regulation from "Word", add a table row with the toolbar, attach the PDF + link
await a.click('.ql-editor');
await a.keyboard.type('مقدمة: اعتمدت المؤسسة هذه اللائحة لتعزيز الالتزام بالأمن السيبراني.');
await a.keyboard.press('Enter');
await a.evaluate((html) => {
  const dt = new DataTransfer(); dt.setData('text/html', html); dt.setData('text/plain', 'لائحة');
  document.querySelector('.ql-editor').dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
}, WORD_HTML);
await a.waitForTimeout(400);
const ed = await a.evaluate(() => { const e = document.querySelector('.ql-editor'); return { tables: e.querySelectorAll('table').length, rows: e.querySelectorAll('table tr').length, h2: e.querySelectorAll('h2').length, ol: e.querySelectorAll('ol li').length, red: !!e.querySelector('[style*="red"]'), script: e.innerHTML.includes('<script') }; });
ok(ed.tables === 1 && ed.rows === 6, `pasted table kept (${ed.tables} table, ${ed.rows} rows)`);
ok(ed.h2 >= 4, `headings kept (${ed.h2})`);
ok(ed.ol >= 4, `Word numbered list converted to a list (${ed.ol} items)`);
ok(!ed.red && !ed.script && !(await a.evaluate(() => document.querySelector('.ql-editor').textContent.includes("alert("))), 'Word colours and script removed on paste');
// add a row to the violations table with the toolbar, then fill it
await a.click('.ql-editor table tr:last-child td:first-child');
await a.click('button.ql-table-row'); await a.waitForTimeout(200);
const cells = ['استخدام برمجيات غير مرخصة', 'إنذار كتابي', 'خصم يوم من الراتب', 'خصم ثلاثة أيام'];
for (let i = 0; i < cells.length; i++) { await a.click(`.ql-editor table tr:last-child td:nth-child(${i + 1})`); await a.keyboard.type(cells[i]); }
// a callout (quote) at the end
await a.click('.ql-editor > p:last-child'); await a.keyboard.press('End'); await a.keyboard.press('Enter');
await a.keyboard.type('يُعد الجهل بأحكام هذه اللائحة غير مبرر لارتكاب المخالفة.');
await a.click('button.ql-blockquote');
await a.setInputFiles('input[name=files]', { name: 'لائحة-المخالفات.pdf', mimeType: 'application/pdf', buffer: fs.readFileSync(`${A}/لائحة-المخالفات.pdf`) });
await a.fill('#ExternalUrl', 'https://intranet.company.local/policies/cyber-violations');
await a.evaluate(() => document.querySelector('.ql-editor table').scrollIntoView({ block: 'center' }));
await shot(a, '02-wizard-content-editor', 'المعالج - الخطوة 2: محرر المحتوى (عناوين، قائمة مرقمة من Word، جدول المخالفات، اقتباس، مرفق PDF ورابط)');
await nav(a, 'next');
ok(a.url().includes('/Acknowledgment/'), 'step 2 saved');

// Step 3: acknowledgment required (suggested statement)
await a.click('label.yn-card:has-text("نعم، يتطلب إقراراً")');
await a.click('button[data-fill="Statement"]');
await a.waitForTimeout(300);
await shot(a, '03-wizard-acknowledgment', 'المعالج - الخطوة 3: الإقرار مطلوب + نص الإقرار + معاينة مطابقة لما يراه الموظف');
await nav(a, 'next');

// Step 4: assessment + questions
await a.click('label.yn-card:has-text("نعم، أضف اختباراً")');
await a.fill('#Settings_Title', 'اختبار لائحة المخالفات الداخلية');
await a.fill('#Settings_PassingPercentage', '80');
await a.fill('#Settings_MaxAttempts', '2');
await a.fill('#Settings_Description', 'أجب عن جميع الأسئلة بعد قراءة اللائحة كاملة. درجة النجاح 80%.');
await a.click('button[value=stay]'); await a.waitForLoadState('networkidle');
const Q = [
  { type: 'اختيار واحد', text: 'ما الجزاء المقرر عند مشاركة كلمة المرور مع الغير لأول مرة؟', opts: ['إنذار كتابي', 'خصم ثلاثة أيام', 'لا يوجد جزاء', 'الإحالة للتحقيق'], correct: [0] },
  { type: 'صح / خطأ', text: 'يجوز تعطيل برنامج مكافحة الفيروسات مؤقتاً لتسريع الجهاز.', tf: 'خطأ' },
  { type: 'اختيار متعدد', text: 'أي مما يلي يُعد مخالفة وفق اللائحة؟', opts: ['ترك الجهاز مفتوحاً دون قفل الشاشة', 'نقل بيانات المؤسسة إلى حساب شخصي', 'الإبلاغ عن رسالة تصيّد', 'تحديث نظام التشغيل'], correct: [0, 1] },
  { type: 'اختيار واحد', text: 'ما التصرف الصحيح عند استلام رسالة مشبوهة تحتوي مرفقاً؟', opts: ['فتح المرفق للتحقق منه', 'إعادة توجيهها للزملاء', 'الإبلاغ عنها لإدارة الأمن السيبراني دون فتح المرفق', 'حذفها فقط'], correct: [2] },
  { type: 'صح / خطأ', text: 'يُعد الجهل بأحكام اللائحة مبرراً لارتكاب المخالفة.', tf: 'خطأ' },
  { type: 'اختيار متعدد', text: 'ما الممارسات التي تحميك من الوقوع في المخالفات؟', opts: ['قفل الشاشة عند مغادرة المكتب', 'تفعيل المصادقة متعددة العوامل', 'مشاركة الحساب مع زميل موثوق', 'حفظ كلمات المرور في ملف نصي'], correct: [0, 1], points: 2 },
];
for (const q of Q) {
  await a.click(`.seg-control label:has-text("${q.type}")`);
  await a.fill('textarea[name="Question.Text"]', q.text);
  if (q.tf) await a.click(`.q-tf label.yn-card:has-text("${q.tf}")`);
  else {
    for (let i = 0; i < q.opts.length; i++) await a.fill(`input[name="Question.Options[${i}]"]`, q.opts[i]);
    for (const c of q.correct) await a.locator("[data-opt-row]").nth(c).locator(".opt-correct-wrap").click();
  }
  if (q.points) await a.fill('input[name="Question.Points"]', String(q.points));
  if (q === Q[5]) await shot(a, '04b-wizard-question-editor', 'منشئ الأسئلة: سؤال اختيار متعدد قبل الحفظ (تحديد أكثر من إجابة صحيحة، الدرجة)', { full: false, wait: 500 });
  await a.click('button:has-text("حفظ السؤال")'); await a.waitForLoadState('networkidle');
}
ok(await a.locator('.q-item').count() === 6, '6 questions listed in order');
// reorder: move question 2 down, then back up
await a.click('.q-item:nth-child(2) button[title="نقل للأسفل"]'); await a.waitForLoadState('networkidle');
ok((await a.locator('.q-item:nth-child(3) .q-text').innerText()).includes('تعطيل'), 'reorder works');
await a.click('.q-item:nth-child(3) button[title="نقل للأعلى"]'); await a.waitForLoadState('networkidle');
await a.goto(base + `/Admin/Authoring/Assessment/${contentId}#questions`); await a.waitForLoadState('networkidle');
await shot(a, '04-wizard-assessment-builder', 'المعالج - الخطوة 4: إعدادات الاختبار (درجة النجاح 80%، محاولتان) وقائمة الأسئلة الست مرتبة مع الإجابات الصحيحة');
await nav(a, 'next');

// Step 5: preview
ok(a.url().includes('/Preview/'), 'moved to preview');
await a.click('details.q-preview > summary');
await shot(a, '05-wizard-preview', 'المعالج - الخطوة 5: معاينة كموظف (نفس مكوّنات صفحة الموظف: المحتوى، المرفقات، الإقرار، الاختبار المرتبط، ومعاينة الأسئلة)');
await nav(a, 'next');

// Step 6: summary + publish
await shot(a, '06-wizard-publish-summary', 'المعالج - الخطوة 6: الملخص قبل النشر (الإقرار مطلوب، الاختبار، عدد الأسئلة، درجة النجاح) + حفظ كمسودة / نشر الآن');
await a.click('button[value=publish]'); await a.waitForSelector('#confirmModal.show'); await a.waitForTimeout(400);
await a.click('#confirmModalOk'); await a.waitForLoadState('networkidle');
await shot(a, '06b-wizard-published', 'بعد النشر: تأكيد النشر وروابط العرض كموظف والإشعار بالبريد والتقارير');
await a.goto(base + '/Admin/Content'); await shot(a, '06c-content-management-list', 'إدارة المحتوى: المادة الجديدة منشورة مع الإقرار والاختبار المرتبط وتاريخ النشر', { full: false });

// ================= EMPLOYEE =================
const e = await page();
await login(e, 'nora.harbi', 'Pass#Employee1');
await e.goto(base + '/Content/Policies');
await shot(e, '07a-employee-policies-list', 'الموظف: صفحة السياسات تعرض اللائحة الجديدة بانتظار الإقرار', { full: false });
await e.click('a.c-card:has-text("لائحة المخالفات الداخلية")'); await e.waitForLoadState('networkidle');
const emp = await e.evaluate(() => ({ table: document.querySelectorAll('.doc-body table tr').length, ol: document.querySelectorAll('.doc-body ol li').length, quote: !!document.querySelector('.doc-body blockquote'), pdf: !!document.querySelector('[data-pdf-viewer][data-src*="/Files/Attachment/"]'), link: !!document.querySelector('a.attach[href*="intranet.company.local"]'), ack: document.querySelector('.ack-check span')?.textContent, start: !!document.querySelector('form[action*="/Assessments/Start/"]'), locked: !!document.querySelector('.next-step.is-locked') }));
ok(emp.table === 7, `employee sees the table with the added row (${emp.table} rows)`);
ok(emp.ol >= 4 && emp.quote, 'numbered rules and callout rendered');
ok(emp.pdf && emp.link, 'attachment and link shown');
ok(emp.ack && emp.ack.includes('لائحة المخالفات الداخلية للأمن السيبراني'), 'acknowledgment statement shown: ' + emp.ack);
ok(!emp.start && emp.locked, 'related assessment is locked until the acknowledgment (mandatory acknowledgment gate)');
await shot(e, '07-employee-regulation-view', 'الموظف: اللائحة المنشورة (التنسيق، الجدول، القائمة المرقمة، المرفق والرابط، الإقرار، ثم الاختبار المرتبط)');
const pdfHref = await e.getAttribute('[data-pdf-viewer]', 'data-src'); // PDFs are shown inline by the embedded viewer
const pdfResp = await e.request.get(base + pdfHref);
ok(pdfResp.status() === 200 && (await pdfResp.body()).slice(0, 4).toString() === '%PDF', 'attachment downloads as a PDF');
// acknowledge
await e.check('[data-ack-check]'); await e.click('[data-ack-submit]'); await e.waitForLoadState('networkidle');
ok(await e.locator('.ack-panel.is-done').count() === 1, 'acknowledged');
ok(await e.locator('.next-step form[action*="/Assessments/Start/"]').count() === 1, 'related assessment can be started from the content page after acknowledging');
await e.evaluate(() => document.querySelector('.ack-panel').scrollIntoView({ block: 'start' }));
await shot(e, '07b-employee-acknowledged-next-step', 'بعد الإقرار: حالة "تم الإقرار" والخطوة التالية: الاختبار', { full: false, wait: 900 });
// assessment
await e.click('form[action*="/Assessments/Start/"] button'); await e.waitForLoadState('networkidle');
await e.click('[data-quiz-start]'); await e.waitForTimeout(500);
const pick = async (texts) => { for (const t of texts) { await e.locator('.qstep.is-active label.choice', { hasText: t }).first().click(); await e.waitForTimeout(150); } };
await pick(['إنذار كتابي']); await e.waitForTimeout(700);
await pick(['خطأ']); await e.waitForTimeout(700);
await pick(['ترك الجهاز مفتوحاً', 'نقل بيانات المؤسسة']);
await shot(e, '08-employee-assessment', 'الموظف: الاختبار (سؤال اختيار متعدد، شريط التقدم، مؤشر الأسئلة)', { full: false, wait: 600 });
await e.click('[data-q-next]'); await e.waitForTimeout(400);
await pick(['الإبلاغ عنها لإدارة']); await e.waitForTimeout(700);
await pick(['خطأ']); await e.waitForTimeout(700);
await pick(['قفل الشاشة', 'المصادقة متعددة']);
await e.click('[data-q-submit]'); await e.waitForSelector('#confirmModal.show'); await e.waitForTimeout(400);
await e.click('#confirmModalOk'); await e.waitForURL('**/Result/**'); await e.waitForLoadState('networkidle');
const resTxt = await e.locator('.result-status').innerText();
ok(resTxt.includes('مجتاز') && !resTxt.includes('غير'), 'result: passed (' + resTxt + ')');
await shot(e, '09-employee-result', 'الموظف: النتيجة (100%، مجتاز) ومراجعة الإجابات', { wait: 2600 });
await e.goto(base + '/'); await shot(e, '09b-employee-dashboard-after', 'لوحة الموظف بعد الإقرار والاختبار (تحدّث التقدم وأحدث النتائج)', { full: false });

// ================= ADMIN: reporting =================
await a.goto(base + '/Admin/Reports/Assessments');
await a.selectOption('#f-a', { label: 'اختبار لائحة المخالفات الداخلية' }); await a.click('form.filters button.btn-primary'); await a.waitForLoadState('networkidle');
ok((await a.content()).includes('nora.harbi'), 'admin report lists the employee');
await shot(a, '10-admin-report-result', 'تقرير الإدارة: نتيجة الموظفة في اختبار اللائحة (مجتاز، 100%) وبقية الموظفين "لم يختبر"');
await a.goto(base + '/Admin/Reports/Acknowledgments');
await a.selectOption('#f-c', { label: 'لائحة المخالفات الداخلية للأمن السيبراني' }); await a.click('form.filters button.btn-primary'); await a.waitForLoadState('networkidle');
await shot(a, '10b-admin-report-acknowledgments', 'تقرير الإقرارات للائحة: تم الإقرار مع التاريخ والوقت');

fs.writeFileSync(`${OUT}/shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/checks.json`, JSON.stringify(checks, null, 1));
console.log('CHECKS', checks.filter(c => c.pass).length + '/' + checks.length);
console.log('ERRORS:\n' + errs.join('\n'));
await b.close();
