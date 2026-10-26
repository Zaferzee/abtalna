// Usage: BASE=http://localhost:5150 OUT=docs/screenshots/enterprise-v1 CHROME=/path/to/chrome node tests/ui/enterprise-entities-acceptance.mjs
// Needs: npm i playwright-core; the Arabic demo data set (admin / Admin#NewPass99) on a database migrated to the entity hierarchy.
// Everything through the UI: the central administrator builds a hierarchy, creates users, delegates two entity administrators with
// different capabilities, distributes central content to selected entities; each entity administrator works in their own scope
// (and is refused elsewhere, also by direct URL); entity branding; entity comparison report; login visual compositions.
import { chromium } from 'playwright-core';
import fs from 'fs';
const base = process.env.BASE || 'http://localhost:5150', OUT = process.env.OUT;
fs.mkdirSync(OUT, { recursive: true });
const b = await chromium.launch({ executablePath: process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--no-sandbox'] });
const errs = [], shots = [], checks = [];
const ok = (cond, what) => { checks.push({ what, pass: !!cond }); console.log((cond ? 'PASS ' : 'FAIL ') + what); };
const PASS = 'Employee#Pass123', NEWPASS = 'Employee#NewPass99';
async function page(opts = {}) {
  const c = await b.newContext({ viewport: { width: 1440, height: 900 }, ...opts }); const p = await c.newPage();
  p.on('pageerror', e => errs.push(p.url() + ' PAGEERR ' + e.message));
  p.on('console', m => { if (m.type() === 'error' && !/status of 40[34]/.test(m.text())) errs.push(p.url() + ' ' + m.text()); });
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
  if (p.url().includes('/Account/Login') && newPw) { // password already changed in an earlier step
    await p.fill('#Username', u); await p.fill('#Password', newPw); await p.click('form[action="/Account/Login"] button.btn-primary'); await p.waitForLoadState('networkidle');
  }
  if (p.url().includes('ChangePassword')) {
    await p.fill('#Current', pw); await p.fill('#New', newPw); await p.fill('#Confirm', newPw); await p.click('main button.btn-primary'); await p.waitForLoadState('networkidle');
  }
}
const go = async (p, path) => { const r = await p.goto(base + path); await p.waitForLoadState('networkidle'); return r.status(); };
const rtlOk = p => p.evaluate(() => document.documentElement.dir === 'rtl' && document.documentElement.scrollWidth <= innerWidth + 1);
const top = p => p.evaluate(() => window.scrollTo(0, 0));

// ======================================================= central administrator: hierarchy
const c = await page();
await login(c, 'admin', 'Admin#NewPass99');
ok(await go(c, '/Admin/Entities') === 200, 'central: organization page');
const rootId = await c.evaluate(() => +new URL(document.querySelector('.org-tree a').href).pathname.split('/').pop());
async function createEntity(name, code, parent, extra = []) {
  await go(c, '/Admin/Entities/Create');
  await c.fill('#f-Name', name); await c.fill('#f-Code', code);
  await c.selectOption('#f-ParentEntityId', String(parent));
  for (const cap of extra) await c.check(`input[name=Allowed][value=${cap}]`);
  await c.click('form button.btn-primary.btn-lg'); await c.waitForLoadState('networkidle');
  return +c.url().match(/Details\/(\d+)/)[1];
}
const dga = await createEntity('هيئة الحكومة الرقمية', 'DGA', rootId, ['ManageBranding', 'ManageAdmins']);
const dgaCs = await createEntity('الإدارة العامة للأمن السيبراني', 'DGA-CS', dga);
const dgaAw = await createEntity('إدارة التوعية والتدريب', 'DGA-AW', dgaCs);
const sda = await createEntity('هيئة البيانات الوطنية', 'SDA', rootId);
const sdaGov = await createEntity('إدارة حوكمة البيانات', 'SDA-GOV', sda);
ok(dga && dgaCs && dgaAw && sda && sdaGov, 'central: five entities created through the form (three levels deep)');

async function createUser(username, name, entity) {
  await go(c, '/Admin/Users/Create');
  await c.fill('#Username', username); await c.fill('#DisplayName', name); await c.fill('#Email', username + '@gov.local');
  await c.selectOption('#AuthSource', 'Local'); await c.fill('#Password', PASS);
  await c.selectOption('#f-EntityId', String(entity));
  await c.click('main form button.btn-primary'); await c.waitForLoadState('networkidle');
}
for (const [u, n, e] of [['dga.admin', 'ريم القحطاني', dga], ['sda.admin', 'فهد العنزي', sda], ['dga.emp', 'هند الشهري', dga], ['cs.emp', 'ماجد الدوسري', dgaCs],
  ['aw.emp', 'لمى الغامدي', dgaAw], ['sda.emp', 'سلمان الحربي', sda], ['gov.emp', 'نوف المطيري', sdaGov]]) await createUser(u, n, e);
await go(c, '/Admin/Users?entity=' + dga);
const dgaUsers = await c.evaluate(() => [...document.querySelectorAll('tbody small[dir=ltr]')].map(x => x.textContent));
ok(dgaUsers.includes('cs.emp') && dgaUsers.includes('aw.emp') && !dgaUsers.includes('sda.emp'), 'central: user filter by entity drills into the subtree');

await go(c, '/Admin/Entities');
await shot(c, '01-entity-hierarchy', 'الهيكل التنظيمي: شجرة الجهات (ثلاثة مستويات) مع جدول الحالة والمستخدمين والمديرين والهوية والدليل');
await go(c, `/Admin/Entities/Details/${dga}`);
await shot(c, '02-central-entity-management', 'إدارة الجهة مركزياً: البطاقة التعريفية والتبويبات (نظرة عامة، المديرون والصلاحيات، المستخدمون، الهوية، الدليل، المحتوى، التقارير، التدقيق)');

// ======================================================= delegation
async function assign(entity, username, caps, desc = true) {
  await go(c, `/Admin/Entities/Details/${entity}?tab=admins`);
  await c.fill('#f-username', username);
  for (const cap of caps) await c.check(`form[action*="AssignAdmin"] input[name=caps][value=${cap}]`);
  if (!desc) await c.uncheck('#f-desc');
  await c.click('form[action*="AssignAdmin"] button.btn-primary'); await c.waitForLoadState('networkidle');
}
await assign(dga, 'dga.admin', ['ManageUsers', 'ManageContent', 'PublishContent', 'ManageAssessments', 'ManageAcknowledgments', 'ViewReports', 'ExportReports', 'ManageBranding', 'ViewAudit']);
await assign(sda, 'sda.admin', ['ManageUsers', 'ViewReports']);
const assigned = await c.evaluate(() => document.querySelectorAll('.admin-card').length);
ok(assigned === 1, 'central: entity administrator assigned with explicit capabilities');
await go(c, `/Admin/Entities/Details/${dga}?tab=admins`);
await shot(c, '03-delegated-permissions', 'الصلاحيات المفوّضة: مدير الجهة وصلاحياته الصريحة، والسقف المسموح للجهة كما حدده المدير المركزي', { full: true });

// ======================================================= central content targeted to selected entities
await go(c, '/Admin/Authoring/New');
await c.fill('#Title', 'سياسة تصنيف البيانات الوطنية');
await c.check('input[name=Type][value="1"]', { force: true });
await c.fill('#Description', 'تحدد مستويات تصنيف البيانات وضوابط التعامل معها.');
await c.check('input[name=Audience][value="2"]', { force: true });
for (const id of [dgaCs, sda]) await c.check(`input[name=TargetIds][value="${id}"]`);
await c.evaluate(() => document.querySelector('#audience').scrollIntoView({ block: 'start', behavior: 'instant' }));
await c.evaluate(() => window.scrollBy(0, -90));
await shot(c, '05-central-content-selected-entities', 'محتوى مركزي موجّه لجهات محددة: الإدارة العامة للأمن السيبراني (وتوابعها) وهيئة البيانات الوطنية، دون نسخ المحتوى');
await c.click('button[form="author-form"][value="next"]');
await c.waitForLoadState('networkidle');
const policyId = +c.url().match(/\/(\d+)/)[1];
await go(c, `/Admin/Authoring/Acknowledgment/${policyId}`);
await c.check('#ack-yes', { force: true });
await c.click('button[form="author-form"][value="next"]');
await c.waitForLoadState('networkidle');
await go(c, `/Admin/Authoring/Publish/${policyId}`);
await c.locator('button[name=go][value=publish]').first().click(); await c.waitForSelector('#confirmModal.show'); await c.click('#confirmModalOk');
await c.waitForLoadState('networkidle');
await go(c, '/Admin/Content');
const chip = await c.evaluate(id => { const row = [...document.querySelectorAll('tbody tr')].find(r => r.querySelector(`a[href$="/Basics/${id}"]`)); return row ? row.querySelector('.chip-scope')?.textContent.trim() : ''; }, policyId);
ok(/2/.test(chip), 'central: content list shows the audience ("2 selected entities")');

// ======================================================= employees: who sees the central content
async function employee(user) { const p = await page(); await login(p, user, PASS, NEWPASS); return p; }
const vis = {};
for (const u of ['cs.emp', 'aw.emp', 'sda.emp', 'gov.emp', 'dga.emp']) {
  const p = await employee(u);
  vis[u] = (await go(p, `/Content/Details/${policyId}`)) === 200;
  if (u === 'cs.emp' || u === 'gov.emp') { await p.check('[data-ack-check]'); await p.click('[data-ack-submit]'); await p.waitForLoadState('networkidle'); }
  await p.context().close();
}
ok(vis['cs.emp'] && vis['aw.emp'] && vis['sda.emp'] && vis['gov.emp'] && !vis['dga.emp'], 'targeting: selected entities and their sub-entities see the policy; the parent agency (not selected) does not');

// ======================================================= entity administrator 1 (DGA): scoped work
const d = await page();
await login(d, 'dga.admin', PASS, NEWPASS);
ok(await go(d, '/Admin/Dashboard') === 200, 'DGA admin: administration area');
const banner = await d.evaluate(() => document.querySelector('[data-scope-banner]')?.innerText || '');
ok(banner.includes('هيئة الحكومة الرقمية') && !banner.includes('هيئة البيانات'), 'DGA admin: dashboard shows the delegated scope');
const nav = await d.evaluate(() => [...document.querySelectorAll('.sb-nav .sb-link')].map(a => a.getAttribute('href')));
ok(!nav.some(h => /Settings/.test(h)) && nav.some(h => /Users/.test(h)), 'DGA admin: menu reflects capabilities (no system settings)');
await shot(d, '04-entity-admin-dashboard', 'لوحة مدير الجهة: الأرقام والنشاط محصورة في نطاقه (هيئة الحكومة الرقمية وتوابعها) مع شريط النطاق والصلاحيات');
await go(d, '/Admin/Users');
const seen = await d.evaluate(() => [...document.querySelectorAll('tbody small[dir=ltr]')].map(x => x.textContent));
ok(seen.includes('cs.emp') && seen.includes('aw.emp') && !seen.includes('sda.emp') && !seen.includes('gov.emp'), 'DGA admin: users list limited to own subtree');
const sdaEmpId = await c.evaluate(async () => { const r = await fetch('/Admin/Users?q=sda.emp'); const h = await r.text(); return +(h.match(/\/Admin\/Users\/Edit\/(\d+)/) || [])[1]; });
ok(await go(d, `/Admin/Users/Edit/${sdaEmpId}`) === 404, 'DGA admin: direct URL to another entity\'s employee -> 404');
ok(await go(d, `/Admin/Entities/Details/${sda}`) === 404, 'DGA admin: direct URL to another entity -> 404');
ok(await go(d, `/Admin/Authoring/Basics/${policyId}`) === 404, 'DGA admin: centrally owned content cannot be edited');
const settings = await d.goto(base + '/Admin/Settings'); ok(settings.url().includes('Denied') || settings.status() === 403, 'DGA admin: system settings refused');
// local content for its own entity only
await go(d, '/Admin/Authoring/New');
await d.fill('#Title', 'دليل الاستخدام الآمن للبريد - الحكومة الرقمية');
await d.check('input[name=Type][value="8"]', { force: true });
await d.check('input[name=Audience][value="1"]', { force: true });
const owners = await d.evaluate(() => [...document.querySelectorAll('#f-OwnerEntityId option')].map(o => o.textContent.trim()));
ok(owners.length === 3 && !owners.some(t => t.includes('البيانات')), 'DGA admin: can own content only in its own entities');
await d.click('button[form="author-form"][value="draft"]');
await d.waitForLoadState('networkidle');
// entity branding (allowed for DGA)
await go(d, `/Admin/EntityBranding/Index/${dga}`);
await d.click('[data-preset-id="emerald"]');
await d.fill('#f-SystemName', 'بوابة التوعية\nالحكومة الرقمية');
await d.waitForTimeout(400);
await shot(d, '07a-entity-branding-studio', 'هوية الجهة: الحقول الفارغة موروثة من الهوية المركزية، مع معاينة حية بنفس مكوّن المعاينة المركزي');
await d.click('form[data-eb] button.btn-primary.btn-lg'); await d.waitForLoadState('networkidle');
ok(await d.evaluate(() => document.querySelector('.alert-success') != null), 'DGA admin: entity branding saved');

// ======================================================= entity administrator 2 (SDA): different, narrower capabilities
const s2 = await page();
await login(s2, 'sda.admin', PASS, NEWPASS);
await go(s2, '/Admin/Dashboard');
const nav2 = await s2.evaluate(() => [...document.querySelectorAll('.sb-nav .sb-link')].map(a => a.getAttribute('href')));
ok(!nav2.some(h => /Content|Assessments|Audit/.test(h)), 'SDA admin: no content, assessment or audit capabilities in the menu');
const contentDirect = await s2.goto(base + '/Admin/Content'); ok(contentDirect.url().includes('Denied') || contentDirect.status() === 403, 'SDA admin: content management refused by URL');
const brandDirect = await s2.goto(base + `/Admin/EntityBranding/Index/${dga}`); ok(brandDirect.url().includes('Denied') || brandDirect.status() === 403 || brandDirect.status() === 404, 'SDA admin: other entity branding refused');
await go(s2, '/Admin/Reports/Acknowledgments');
const sdaReport = await s2.evaluate(() => document.querySelector('main').innerText);
ok(sdaReport.includes('sda.emp') && sdaReport.includes('gov.emp') && !sdaReport.includes('cs.emp'), 'SDA admin: acknowledgment report limited to own entity employees');
const exp = await s2.goto(base + '/Admin/Reports/Acknowledgments?export=csv').catch(e => null);
ok(!exp || exp.url().includes('Denied') || exp.status() === 403, 'SDA admin: export refused (no export capability)');
await shot(s2, '04b-second-entity-admin', 'مدير جهة ثانٍ بصلاحيات أضيق: المستخدمون والتقارير فقط (لا محتوى ولا تصدير ولا تدقيق)');
await go(s2, '/Admin/Dashboard');
await shot(s2, '04c-second-entity-admin-dashboard', 'لوحة مدير هيئة البيانات الوطنية: نطاق مختلف وصلاحيات مختلفة');

// ======================================================= branding differs per entity (employee portal)
const e1 = await employee('cs.emp'); await go(e1, '/');
const e1Primary = await e1.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--color-primary').trim());
await shot(e1, '07b-entity-branding-dga-employee', 'بوابة موظف في جهة تابعة للحكومة الرقمية: الهوية المخصصة موروثة من الجهة الأم');
const e2 = await employee('gov.emp'); await go(e2, '/');
const e2Primary = await e2.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--color-primary').trim());
await shot(e2, '07c-central-branding-sda-employee', 'بوابة موظف في هيئة البيانات: الهوية المركزية (لا يتأثر بهوية جهة أخرى)');
ok(e1Primary === '#0f5c4a' && e2Primary !== e1Primary && !e1.url().includes('Login'), `branding: DGA subtree uses its own colors (${e1Primary}), SDA keeps the central ones (${e2Primary})`);
ok(await rtlOk(e1) && await rtlOk(e2), 'RTL: employee portals right-to-left without horizontal overflow');

// ======================================================= central reports across entities
await go(c, '/Admin/Reports/Entities');
const rows = await c.evaluate(() => [...document.querySelectorAll('.entity-compare tbody tr')].map(r => r.innerText));
ok(rows.some(r => r.includes('هيئة الحكومة الرقمية')) && rows.some(r => r.includes('هيئة البيانات الوطنية')), 'central report: comparison across entities');
await shot(c, '06-central-report-entities', 'تقرير مركزي: مقارنة الجهات (نسبة الإنجاز والإقرار والنجاح) مع التعمق في الهيكل');
await go(c, `/Admin/Reports/Entities?entityId=${dga}`);
await shot(c, '06b-central-report-drilldown', 'التعمق في هيئة الحكومة الرقمية: الجهة كاملة ثم جهاتها التابعة');
await go(c, `/Admin/Audit?entity=${dga}`);
const auditText = await c.evaluate(() => document.querySelector('main').innerText);
ok(/ENTITY_CREATED|إنشاء جهة/.test(auditText) && /ADMIN_ASSIGNED|تعيين مدير جهة/.test(auditText) && /ENTITY_BRANDING_UPDATED|تعديل هوية الجهة/.test(auditText), 'audit: entity, delegation and branding events recorded for the entity');
await shot(c, '09-audit-by-entity', 'سجل التدقيق مفلتراً حسب الجهة: إنشاء الجهات وتعيين المديرين وتعديل الهوية مع الجهة المتأثرة');
ok(await rtlOk(c), 'RTL: central administration pages without horizontal overflow');

// responsive: organization page and entity admin dashboard on a phone
const m = await page({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
await login(m, 'dga.admin', NEWPASS);
await go(m, '/Admin/Dashboard'); ok(await rtlOk(m), 'responsive: entity admin dashboard at 390px');
await go(m, '/Admin/Entities'); ok(await rtlOk(m), 'responsive: entities page at 390px');
await shot(m, '10-mobile-entities', 'الهيكل التنظيمي على الجوال (390×844)');
await m.context().close();

// ======================================================= premium login visual compositions
await go(c, '/Admin/Settings'); await c.click('[data-studio-tab="composition"]'); await c.waitForTimeout(300);
await top(c);
await c.evaluate(() => document.querySelector('[data-studio-panel="composition"]').scrollIntoView({ block: 'start', behavior: 'instant' })); await c.evaluate(() => window.scrollBy(0, -130));
await shot(c, '08a-studio-compositions', 'استوديو الهوية: التكوينات البصرية الجاهزة بمعاينات مصغّرة حقيقية، ثم ضبط الطبقات');
await c.evaluate(() => document.querySelector('.layer-list').scrollIntoView({ block: 'start', behavior: 'instant' })); await c.evaluate(() => window.scrollBy(0, -130));
await shot(c, '08b-studio-layers', 'محرر الطبقات: العنصر والموضع والشفافية والحجم والدوران ضمن حدود آمنة');
const g = await page();
for (const [id, name, desc] of [['governance', '08c-login-governance', 'صفحة الدخول بتكوين «الحوكمة والامتثال»'], ['defense', '08d-login-cyber-defense', 'صفحة الدخول بتكوين «الدفاع السيبراني»'], ['journey', '08e-login-awareness-journey', 'صفحة الدخول بتكوين «رحلة التوعية»'], ['executive', '08f-login-executive', 'صفحة الدخول بتكوين «الأمن التنفيذي»']]) {
  await go(c, '/Admin/Settings'); await c.click('[data-studio-tab="composition"]'); await c.click(`[data-comp-preset="${id}"]`);
  await c.click('[data-studio-save]'); await c.waitForLoadState('networkidle');
  await go(g, '/Account/Login'); await g.waitForTimeout(700);
  const L = await g.evaluate(() => ({ cls: document.querySelector('.auth').className, layers: [...document.querySelectorAll('.auth-layer')].filter(l => getComputedStyle(l).display !== 'none').map(l => l.dataset.motif), pe: [...document.querySelectorAll('.auth-layer')].every(l => getComputedStyle(l).pointerEvents === 'none') }));
  ok(L.cls.includes('comp-' + id) && L.layers.length >= 2 && L.pe, `login: "${id}" composition renders ${L.layers.join(' + ')} (no pointer events)`);
  await shot(g, name, desc);
}
// a custom layer behind the text is capped for readability
await go(c, '/Admin/Settings'); await c.click('[data-studio-tab="composition"]');
await c.click('[data-layer-card="1"] label:has(> input[value="center-start"])');
await c.evaluate(() => { const r = document.querySelector('input[name=Layer1Opacity]'); r.value = 30; r.dispatchEvent(new Event('input', { bubbles: true })); });
const capped = await c.evaluate(() => ({ note: !document.querySelector('[data-layer-card="1"] [data-comp-capped]').hidden, op: document.querySelector('[data-pv-auth] [data-layer="1"]').style.getPropertyValue('--l-op'), id: document.querySelector('[data-comp-id]').value }));
ok(capped.note && capped.op === '0.12' && capped.id === 'custom', 'studio: a layer moved behind the text is capped at 12% and becomes a custom composition (preview)');
await c.click('[data-studio-discard]');
// phone: only the soft accent remains
const gm = await page({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
await go(gm, '/Account/Login'); await gm.evaluate(() => window.scrollTo(0, 0));
const mob = await gm.evaluate(() => [...document.querySelectorAll('.auth-layer')].filter(l => getComputedStyle(l).display !== 'none').map(l => l.dataset.motif));
ok(mob.every(x => x === 'circles') && await rtlOk(gm), 'login on a phone: decorative layers simplified (' + (mob.join(',') || 'none') + '), no overflow');
await shot(gm, '08g-login-mobile', 'صفحة الدخول على الجوال: تبقى اللمسة الهندسية الناعمة فقط');

console.log('CHECKS ' + checks.filter(x => x.pass).length + '/' + checks.length);
console.log('ERRORS ' + (errs.length ? errs.join('\n') : 'none'));
fs.writeFileSync(`${OUT}/.shots.json`, JSON.stringify(shots, null, 1));
fs.writeFileSync(`${OUT}/.checks.json`, JSON.stringify(checks, null, 1));
await b.close();
