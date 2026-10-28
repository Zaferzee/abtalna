# Starter content pack v1 (حزمة المحتوى الأولي)

An **optional** starter cybersecurity-awareness pack: 12 learning items with assessments and 4 policy templates. It lets the platform be demonstrated and accepted end to end (content, acknowledgment, assessments, entity targeting, reports) before the organization's own material is added.

- It is **off by default** and is installed only on explicit request. Production start-up never installs it silently.
- It uses the existing content, acknowledgment, assessment and entity-targeting model. There is no new subsystem and **no database migration**.
- Everything is local: the text and illustrations ship inside the application. No external URL is fetched during installation or display, so the pack works on an isolated on-premise network.
- All lesson text and questions are **original Abtalna content**, inspired by public NCA awareness topics. No NCA text, logos, campaign artwork, videos or toolkit files are reproduced.

> **Policy templates (POL-001 … POL-004) are starter templates, not official policies.** They are not NCA policies. They must be reviewed, adapted and formally approved by the organization's legal and cybersecurity governance team before production use. Every template opens with the banner «نموذج استرشادي تجريبي — يجب استبداله أو مراجعته واعتماده من الجهة قبل الاستخدام الإنتاجي.» The acknowledgment text of AWT-008 is also a starter template and is flagged as such on the administrator pages.

## 1. Included content

| Code | Title | Type | Audience | Default state | Assessment | Acknowledgment |
|---|---|---|---|---|---|---|
| AWT-001 | الأمن السيبراني مسؤولية الجميع | Awareness lesson (5 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-002 | التصيد والهندسة الاجتماعية | Awareness + scenario (7 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-003 | حماية الحسابات وكلمات المرور والتحقق متعدد العوامل | Awareness (6 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-004 | الأجهزة المحمولة وUSB ووسائط التخزين | Awareness (6 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-005 | التصفح الآمن والروابط والتحميلات | Awareness (6 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-006 | الاستخدام الآمن لوسائل التواصل الاجتماعي | Awareness (6 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-007 | حماية البيانات ومنع التسرب | Awareness (7 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-008 | الذكاء الاصطناعي والأدوات غير المعتمدة | Awareness + mandatory acknowledgment (7 min) | All users | Published, all entities | 8 questions, 80% (locked until acknowledged) | Yes (starter template text) |
| AWT-009 | حماية البيانات أثناء السفر والعمل خارج المكتب والاجتماعات | Awareness (6 min) | Users handling organizational/classified data | Published, all entities | 8 questions, 80% | – |
| AWT-010 | الطباعة والمسح والنسخ والإتلاف الآمن | Awareness (5 min) | All users | Published, all entities | 8 questions, 80% | – |
| AWT-011 | التحديثات الأمنية والنسخ الاحتياطي | Awareness (5 min) | All users | Published, all entities | 8 questions, 80% | – |
| SPC-001 | الاستخدام الآمن للأنظمة الحساسة | Specialized awareness (7 min) | Critical-system users only | **Draft**, root entity only | 8 questions, 80% (unpublished) | – |
| POL-001 | سياسة الاستخدام المقبول للأصول التقنية | Policy template | All users | **Draft** | – | Yes |
| POL-002 | تعهد الاستخدام الآمن للبيانات والذكاء الاصطناعي | Policy template | All users | **Draft** | – | Yes |
| POL-003 | سياسة التعامل مع البيانات والمعلومات المصنفة | Policy template | All users | **Draft** | – | Yes |
| POL-004 | مسؤولية الإبلاغ عن الحوادث والأحداث المشبوهة | Policy template | All users | **Draft** | – | Yes |

**Totals:** 16 content items, 12 assessments and 96 questions. Each lesson has 3 scenario, 3 knowledge/application, and 2 true/false or best-action questions. Multiple-choice questions have 4 options and exactly one correct answer, and option order is shuffled so the correct position varies. Assessments allow 3 attempts.

**Lesson format:** each lesson is 450–750 words with the following structure:
- a learning objective («هدف الدرس»),
- one local illustration,
- 4–6 short sections,
- a final «تذكر» takeaway.

Lessons use plain Modern Standard Arabic and contain no control numbers.

**Ownership and audience:** everything is owned by the **root (central) entity**.
- AWT-001 … AWT-011 target the root and all its sub-entities.
- SPC-001 is a draft visible to the root entity only. The central administrator targets it to the critical-system user population, for example a specific entity, from **Learning material → Basic information → Who sees this content? → Selected entities**, then publishes it.
- Policy templates stay drafts until the organization approves them. Their audience is the root and all sub-entities once published.

**Answer explanations:** the existing question model has no explanation field, and adding one would require a migration. The explanation for every question is therefore kept in [starter-content-pack/ANSWER_KEY.md](starter-content-pack/ANSWER_KEY.md) for trainers and reviewers. Employees still see the standard answer review (their answer and whether it was correct) after each attempt.

## 2. Reference mapping (governance metadata)

The references are guidance metadata for administration and governance only («مرجع استرشادي»). They are shown on the administrator pages:
- the Content Management list,
- the header of the authoring wizard, together with the pack code, audience, template flag and media-slot note.

They are **never** shown on the employee lesson. They do not claim that a control requires anything beyond what it states. For example, AWT-003 says MFA is applied according to the organization's policies and risk assessment; it does not say that ECC requires it for every ordinary user.

| Code | Framework | References |
|---|---|---|
| AWT-001 | ECC 2:2024 | 1-10 |
| AWT-002 | ECC 2:2024 | 1-10-3-1 |
| AWT-003 | ECC 2:2024 | 2-2, 2-2-3 |
| AWT-004 | ECC 2:2024 + DCC | ECC 1-10-3-2, ECC 2-3-3-2, DCC 1-3-1-7 |
| AWT-005 | ECC 2:2024 | 1-10-3-3 |
| AWT-006 | ECC 2:2024 | 1-10-3-4 |
| AWT-007 | DCC | 1-3-1-1, 1-3-1-6 |
| AWT-008 | DCC | 1-2-1-2 (implementation guidance), 1-3-1-6 |
| AWT-009 | DCC | 1-3-1-2, 1-3-1-3 |
| AWT-010 | DCC | 1-3-1-4, 1-3-1-5 |
| AWT-011 | ECC 2:2024 + public awareness topics | 2-3-3-3, 2-9 |
| SPC-001 | CSCC | 2-2-1-1, 2-2-1-2, 2-2-1-3, 2-2-1-4 |
| POL-001 | ECC 2:2024 | 2-1-3, 2-1-4 |
| POL-002 | DCC | 1-2-1-2 (guidance) |
| POL-003 | DCC | 1-3-1-1 to 1-3-1-7 |
| POL-004 | ECC 2:2024 | 2-13 |

POL-004 states that employees report to the organization's **approved internal channel**. The organization then handles any regulatory or NCA reporting according to its incident procedures. Employees do not report directly to NCA.

## 3. Installing

Back up the database first, as for any data change.

**Option A: console command (recommended).** Run it once from the application folder, with the same configuration as the site. On the server it uses the same `appsettings.Production.json` and environment as IIS.

```powershell
cd C:\inetpub\cyberlms
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet CyberLms.Web.dll --install-starter-pack
# -> Starter content pack v1 installed: 16 content item(s), 96 question(s).
```

Development:

```bash
dotnet src/CyberLms.Web/bin/Release/net10.0/CyberLms.Web.dll --install-starter-pack
```

**Option B: configuration flag.** Set `Seed:StarterContentPack` to `true`, for example with the environment variable `Seed__StarterContentPack=true`. The pack is then installed at the next start-up, after migrations. Remove the setting again afterwards; leaving it on is harmless, but it keeps the intent explicit.

**Behavior:**
- **Idempotent.** The setting `StarterContentPack.v1.Installed` (in the existing `SystemSettings` table) records the installation. A second run reports *"already installed; nothing was changed"*. Items recorded by an earlier run are never created twice, even if the marker is removed.
- **Non-destructive.** The installer only adds. It never changes or deletes user-created content, existing assessments, results or acknowledgments.
- **Transactional.** Either the whole pack is installed or nothing is.
- **Normal pipeline.** Lesson HTML goes through the same sanitizer as the editor. There is no script, external link or external image. Illustrations are static files under `wwwroot/img/starter-pack/`, and the sanitizer accepts only that exact path pattern besides uploaded inline images.
- **Audited.** The installation writes the audit entry `STARTER_PACK_INSTALLED` (تثبيت حزمة المحتوى الأولي).
- **Database identity.** It runs with the application's normal database identity; it needs no owner rights and makes no schema change.
- **Embedded pack.** The pack file (`Data/StarterPack/starter-content-pack.v1.json`) is compiled into the application. No file path is read at run time.

After installation, every item is ordinary content. Administrators can edit, unpublish, retarget, copy or delete it like anything else. The administrator pages show «حزمة المحتوى الأولي · AWT-00x» and the guidance reference.

## 4. Disabling / not installing

Do nothing: the pack is off by default. Do not pass `--install-starter-pack` and do not set `Seed:StarterContentPack`.

**Removing an installed pack:** unpublish or delete the items from **Content Management** like any other content. Deleting keeps the audit trail. The installer never removes anything by itself.

## 5. Demonstration data (Development only)

The starter content contains **no** completions, results or acknowledgments. Demonstration reporting data is a separate, explicit command:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet CyberLms.Web.dll --seed-starter-demo
```

It creates:
- 8 demonstration employees (`demo.employee01` … `08`, department «بيانات تجريبية»), with **no password**, so they cannot sign in;
- their acknowledgments and scored attempts on the published starter assessments, with a mix of passes and failures.

It is refused outside the Development environment and before the pack is installed. It runs once only (`StarterContentPack.v1.DemoData`). It never runs automatically.

## 6. Replacing the placeholder media with official Haseen/NCA assets

Each lesson carries an administrator-only note on the authoring page: *"Official media slot: an NCA/Haseen awareness asset can be attached here later"*. Its stored metadata reads `OfficialMediaSlot: NCA/Haseen awareness asset can be attached here later`.

When the organization obtains official toolkit material (video, PDF, poster):

1. Copy the files to the server through the normal upload. **Never hotlink media from nca.gov.sa**; the platform serves only uploaded files.
2. Open the item in **Content Management → Learning material → Content** and attach the files:
   - Video (MP4/WebM) plays in the built-in player.
   - PDF opens in the built-in viewer.
   - Images can be inserted into the text.
3. Optionally remove the starter illustration paragraph from the text in the editor.
4. Save. If the material changes the substance of an acknowledged item, use **Reset all** acknowledgments on the content's edit page so employees confirm again.

Respect the license terms of the toolkit material, and keep the original attribution where it is required.

## 7. Maintaining the pack

The pack is generated from `tools/starter-pack/` (`part1.py` … `part4.py` hold the text and questions; `lib.py` holds the helpers). To regenerate:

```bash
cd tools/starter-pack && python3 build.py ../..
```

The build validates:
- word counts and sections;
- the «تذكر» takeaway;
- the question mix, exactly one correct answer, and an explanation for every question;
- the policy banner;
- that there are no scripts or external links.

It writes `src/CyberLms.Web/Data/StarterPack/starter-content-pack.v1.json` and `docs/starter-content-pack/ANSWER_KEY.md`.

A changed pack for already-installed systems must be a **new version** (new marker key). v1 is never re-applied over existing installations.

## 8. Verification

| Area | Detail |
|---|---|
| Integration tests (`StarterContentPackTests`) | Pack quality rules; explicit, root-owned, default-audience and idempotent install, also after a lost marker; employee visibility, with SPC-001 and policies hidden; no references on the employee page; administrator reference display; the AI acknowledgment gate; results and acknowledgments in the reports; demo data refused outside Development |
| Browser acceptance on a fresh database | `tools/starter-pack/run-acceptance.sh` with `tests/ui/starter-content-pack-acceptance.mjs`, 28 checks: Arabic/RTL, local illustrations, no external network request, the gate, 80% pass mark, targeting example, reports, manual content creation; then a second installer run that changes nothing |
| Screenshots | [screenshots/starter-content-pack-v1](screenshots/starter-content-pack-v1/README.md) |

## 9. Known limitations

| Limitation | Detail |
|---|---|
| No per-question explanation | Shown to employees after an attempt only as the standard answer review. Explanations are in `ANSWER_KEY.md`, because a new column would require a migration. |
| Governance metadata in `SystemSettings` | Framework, reference and audience are stored as settings rows (`StarterContentPack.v1.Item.<contentId>`), not as content columns. They are shown on the administrator pages only and are not part of exports or filters. |
| Duration | Shown in the item description (for example "(7 دقائق)"). The reader also shows its own reading-time estimate. |
| Illustrations | Generic original SVGs. They are meant to be replaced or complemented by official toolkit media (section 6). |
| Policy templates and the AI acknowledgment text | Starter templates that need the organization's formal review and approval. |
