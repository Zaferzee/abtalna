# Learning experience v2 (employee portal)

The employee portal presents each requirement as a step in «رحلتك في الوعي السيبراني» (your cybersecurity awareness journey). This is a presentation-layer change; no business rule changed.

## Unchanged

The following are as before:
- Authentication and authorization.
- The database schema: no migration.
- Assessment scoring.
- The acknowledgment rules and the server-side gate (`AckGate`).
- Audit logic and file security.
- Deployment and Active Directory preparation.
- The admin portal and the content-authoring workflow.

Existing tests are unchanged, except one browser script whose dashboard selector was ported to the new markup with the same assertion.

## Learning status model

`Services/LearningProgress.cs` is **read-only**. It derives a state for each learning item from existing records:
- the content and its `RequiresAcknowledgment` flag;
- the user's acknowledgment of the current content version;
- the linked published assessments and the user's attempts (via `AssessmentQueries`).

It stores nothing and duplicates no data.

A learning item is either:
- a published content item with its published assessments, or
- a published assessment that is not linked to published content.

| State | Rule | Card status |
|---|---|---|
| `Available` | Reading item (no acknowledgment, no assessment) not yet marked as read | «جديد» when published in the last 14 days |
| `AckRequired` | Acknowledgment required and not given; any assessment is locked | «مطلوب» |
| `AssessmentAvailable` | Acknowledged (or not required); assessment not attempted | «تم الإقرار» or «مطلوب» |
| `AssessmentInProgress` | An attempt is in progress | «قيد التنفيذ» |
| `Failed` | Completed attempts, none passed (retry if attempts remain) | «لم يُجتز بعد» |
| `Completed` | Acknowledged (if required) and every linked assessment passed; for a reading item, «تمت القراءة» recorded | «مكتمل» |

### Journey steps

The journey is content → acknowledgment (if required) → assessment (if any) → completion. Each step is shown as done ✓, current (highlighted), upcoming, locked 🔒, or failed (no attempts left).
- Reading stays current until the employee acts (acknowledges or attempts).
- The assessment is locked while the acknowledgment is missing. This mirrors `AckGate`, which still enforces it on the server.

### Overall progress

Overall progress is completed items ÷ items that have requirements, shown as «أنجزت X من Y مواد» and «التقدم العام N%».

### Next action

The "next step" is chosen by `LearningProgress.Priority`, in this order:
1. Assessment in progress.
2. Acknowledged, assessment ready.
3. Retry available.
4. Acknowledgment required.
5. Assessment ready (reading first).
6. Oldest published first.

## Screens

| Screen | What changed |
|---|---|
| Dashboard (`Views/Home/Index.cshtml`) | Welcome and the journey title; the progress ring with «أنجزت X من Y مواد»; «ما المطلوب مني الآن؟» with one featured next step (full journey and one clear action) and the remaining tasks (status, compact journey, action); available content cards; «إنجازاتك الأخيرة»; recent results |
| Content cards (`_ContentCard.cshtml`) | Type, title, status chip, what the item requires (acknowledgment / assessment, with ✓ or 🔒), and the completed state |
| Content page (`_ContentReader.cshtml`) | «رحلتك في هذه المادة» under the title (it replaces the old side "steps" box); the acknowledgment → unlock transition; the «تم إكمال هذه المادة بنجاح» card when complete. The admin preview shows the same journey and advances it on the simulated acknowledgment |
| Assessments list | Linked content, «لم يُجتز بعد», and the empty state «لا توجد اختبارات متاحة حالياً.» |
| Assessment | Intro with attempt number and estimated time; answered summary in the confirmation; «جارٍ احتساب نتيجتك» while submitting. Correct answers are never shown during the assessment |
| Result | **Pass:** «أحسنت، اجتزت الاختبار بنجاح», score, passing score, correct answers, points, completion date, attempt, a small single celebration, and the learning item completion card. **Fail:** «لم تحقق درجة الاجتياز هذه المرة», an encouraging tone (warm, not red), «مراجعة المحتوى» and «إعادة المحاولة» when allowed |
| My Results | Empty states, «غير مجتاز» wording |

## Files

- **Views:**
  - `Views/Shared/_Journey.cshtml`: full and compact journey.
  - `Views/Shared/_NextAction.cshtml`: the one action for an item.
  - `Views/Shared/_ContentCard.cshtml`.
- **Styles:** `wwwroot/css/learning.css`, loaded after `app.css`.
- **Script:** `wwwroot/js/learning.js`, loaded after `ui.js`. It handles the journey draw, progress since the last visit, the preview journey, and the submission summary and state. It is progressive enhancement only; CSP is unchanged.
- **Motion:** see [MOTION_DESIGN.md](MOTION_DESIGN.md).

## Evidence

- **Automated tests:**
  - `tests/CyberLms.Tests/LearningProgressTests.cs` (6 unit tests of the state model).
  - `tests/CyberLms.Tests/LearningExperienceTests.cs` (the journey, result and dashboard states over real HTTP).
- **UI acceptance:** `tests/ui/learning-experience-acceptance.mjs`; screenshots and recordings in [screenshots/learning-experience-v2/](screenshots/learning-experience-v2/README.md).

## Closure pass

### Reading completion («تمت القراءة»)
- **Applies to:** content with neither acknowledgment nor a published assessment (a "reading item").
- **How it completes:** the employee completes the item explicitly with «تمت القراءة» at the end of the content page.
  - `POST /Content/Complete/{id}` writes one `ContentCompletions` row for the current content version.
  - It is idempotent (unique index) and refuses content that requires acknowledgment or has a published assessment, so there is no duplicate evidence.
  - Opening the page writes nothing.
- **Journey:** المحتوى → الإنجاز.
- **Progress and tasks:** reading items count toward «أنجزت X من Y مواد». They appear in «ما المطلوب مني الآن؟» after the mandatory items.
- **Unchanged evidence for other items:** acknowledgment and assessment rules are the same.
- **Schema:** the additive migration `20261009014228_AddContentCompletion` (see `DATABASE.md`).

### After passing an assessment
- **Assessments list:** the primary action is «عرض النتيجة» (the best result) with «مكتمل».
- **No retake offered:** the content page, result page and dashboard already offered no retake in the passed state.
- **Still possible (rule unchanged):**
  - The server still accepts a new attempt within `MaxAttempts` if it is requested directly.
  - An attempt that is already open shows «متابعة المحاولة المفتوحة».
  - History is kept, and the passed result remains the best one.
  - Covered by `LearningExperienceTests`.

### My Results
- **Summary:** completed learning items, assessments passed, policies acknowledged.
- **Completed assessments:** a score ring, status chip, date, correct answers, passing score, attempt number, the linked learning item, and «عرض النتيجة».
- **Records:** acknowledgments and completed reading.
- **Empty states:** use the v2 style.

### Responsive verification
`tests/ui/responsive-check.mjs` runs at 1366×768, 820×1180 and 390×844. It covers the dashboard, journey, content with video/PDF, acknowledgment, reading completion, assessment, results, My Results and the assessments list, and checks for horizontal overflow.

Two defects were fixed:
- The assessment intro forced a 648px-wide page on phones: the five facts tiles could not shrink.
- Dashboard task titles were truncated on phones.
