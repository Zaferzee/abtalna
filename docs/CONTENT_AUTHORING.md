# Content authoring: «إنشاء مادة جديدة»

One guided flow that lets an administrator publish learning material together with its acknowledgment and assessment. Example: «لائحة المخالفات الداخلية للأمن السيبراني».

**Entry points:**
- Content Management → **إنشاء مادة جديدة** (also on the admin dashboard).
- **Edit** on an existing item opens the same wizard, with everything that item already has (text, attachments, acknowledgment, assessment, questions).

The wizard composes the existing records: `Content`, `ContentAttachment`, `Assessment` (linked through `ContentId`), `Question`/`QuestionOption` and `UserAcknowledgment`. There is no separate course engine. Every step saves to the database, so you can leave at any time; the item stays a **draft** until you publish it.

## The six steps

| # | Step | What the administrator does |
|---|---|---|
| 1 | المعلومات الأساسية | Title, type (سياسة، لائحة، إجراء، ضابط أمن سيبراني، محتوى توعوي، محتوى تدريبي، تعليمات، محتوى عام), short description. |
| 2 | كتابة المحتوى | Rich-text editor (Arabic RTL): headings, paragraphs, bold, italic, underline, bulleted and numbered lists, **tables** (size picker, add/delete row and column, delete table), quote/callout, links, inline images, alignment and text direction. **Paste from Word** keeps headings, lists (including Word numbering) and tables, and drops fonts, colours, sizes, scripts and styles. Attachments: PDF, Word/Excel/PowerPoint, images, MP4/WebM video, uploaded to the existing secure storage (type/size/signature checks, stored outside `wwwroot`). One link for an intranet page, shared document or video portal. |
| 3 | الإقرار | Yes / No. If Yes, write the statement or use the suggested one («أقرّ بأنني قرأت وفهمت ‹العنوان›.»). A live preview shows the panel exactly as the employee will see it. |
| 4 | الاختبار | Yes / No. If Yes: name, instructions, passing score (%), number of attempts (0 = unlimited). Then add questions **on the same page**: single choice, multiple choice or true/false; text, options (up to 8), correct answer(s), points. After you save a question it appears in the numbered list and the editor is ready for the next one (Enter moves between options). Questions can be edited, deleted and moved up or down. |
| 5 | المعاينة | «معاينة كموظف»: the real employee page (same components), with inactive buttons, plus a preview of every assessment question as the employee sees it. Correct answers are marked for the administrator only. |
| 6 | النشر | A summary (content, acknowledgment, assessment, number of questions, passing score, attempts, total points) with a checklist. **حفظ كمسودة** or **نشر الآن**: the content and its assessment are published together. Once published, the step offers view as employee, email employees (the existing notification) and links to the acknowledgment and assessment reports, plus **إلغاء النشر**. |

## Employee experience after publishing

The content page reads top to bottom:

1. Content
2. Attachments and supporting material
3. Acknowledgment (if required)
4. **Next step: assessment**, with Start / Continue / Retry, or View result

A side card shows the steps for the item and their status. Regulations are listed on the employee's **Policies** page, together with policies. The assessment can still be started before acknowledging; this is the existing behaviour, kept unchanged.

## Data-integrity rules (unchanged production behaviour)

- **Questions lock** as soon as an assessment has any attempt. Add, edit, delete and reorder are refused, and the wizard shows the lock. Name, passing score and attempts can still change; completed attempts keep the passing score they were taken with (snapshot).
- **An assessment with attempts cannot be removed** from the wizard ("No assessment" is refused). It can only be unpublished.
- **Acknowledgments are never changed** by editing. A new statement or text does not reset existing acknowledgments; the existing *Reset* actions in reports remain the only way to do that.
- **Unpublish** withdraws the content and its assessment. Attempts, results and acknowledgments are kept.
- **Publish is blocked** while the linked assessment has no questions. Add questions, or choose "No assessment".
- **Duplicate** creates a new *draft*. It copies the text, settings, attachments (as new files) and an unpublished copy of the assessment and questions. Acknowledgments and attempts are never copied.
- Saving re-sanitizes the body on the server (HtmlSanitizer: no scripts or event handlers; images only from the editor's own uploads). Every action is written to the audit log.

## Database change

**One additive migration**, `20261006173411_AddContentAcknowledgmentText`: the nullable column `Contents.AcknowledgmentText varchar(1000)`. Existing rows keep `NULL` and show the default statement.

The new types *لائحة* (7) and *تعليمات* (8) are values of the existing integer column, so they need no schema change.

`deploy/sql/02-migrate.sql` was regenerated and checked to be idempotent on a database at `InitialCreate`; see [DEPLOYMENT.md §7](DEPLOYMENT.md).

## Evidence

- **Automated:** `tests/CyberLms.Tests/AuthoringTests.cs`, 2 tests, real HTTP against PostgreSQL. It covers the full wizard, sanitizing, table persistence, PDF attachment, statement, questions (add / invalid / reorder / edit / delete), preview, publish, the employee flow (acknowledge, attempt, 100 %), report, locking after attempts, refusal to remove the assessment, acknowledgments kept, duplicate, unpublish with history kept, and audit entries.
- **UI acceptance through the browser:** the regulation was created by the administrator only through the UI; 18/18 checks passed. See [screenshots/content-authoring-v1/README.md](screenshots/content-authoring-v1/README.md).

## Limitations

- Exactly one link per content item: the existing `ExternalUrl` field.
- Tables are simple grids (Quill 2): no merged cells, no header cells. The first row is styled as the header. Cell shading and colours from Word are removed on purpose.
- One assessment per content item is managed in the wizard. Additional linked assessments, which could already be created on the Assessments page, are listed there and managed from that page.
- Paste from Word was verified with Word-format clipboard HTML in Chromium; pasting from desktop Word on Windows still needs to be tried on a real workstation.
