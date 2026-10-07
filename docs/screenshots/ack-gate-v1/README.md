# لقطات بوابة الإقرار الإلزامي (ack-gate v1)

أنشأ المسؤول عبر المعالج «سياسة تجريبية للإقرار الإلزامي» تتطلب الإقرار ومرتبطاً بها اختبار، ثم استخدمها موظف عبر الواجهة. السكربت: `tests/ui/ack-gate-acceptance.mjs`. الدقة 1440×900.

المسار: قراءة المحتوى ← محاولة بدء الاختبار (مقفل / مرفوض من الخادم) ← الإقرار ← «تم الإقرار بنجاح» وفتح الاختبار ← «ابدأ الاختبار» ← النتيجة.

## اللقطات

| الملف | الوصف |
|---|---|
| [01-preview-locked.png](01-preview-locked.png) | المعاينة كموظف: الاختبار مقفل قبل الإقرار |
| [02-preview-simulated-unlock.png](02-preview-simulated-unlock.png) | المعاينة: بعد محاكاة الإقرار يُفتح الاختبار (لا يُسجَّل أي إقرار) |
| [03-dashboard-locked.png](03-dashboard-locked.png) | لوحة الموظف: الاختبار مقفل ويوجّه إلى الإقرار بالاطلاع |
| [04-assessments-list-locked.png](04-assessments-list-locked.png) | قائمة الاختبارات: بطاقة الاختبار مقفلة مع رابط "اقرأ وأقرّ" |
| [05-content-locked.png](05-content-locked.png) | صفحة المحتوى قبل الإقرار: لوحة الإقرار ثم الاختبار المقفل |
| [06-direct-start-denied.png](06-direct-start-denied.png) | محاولة بدء الاختبار مباشرة قبل الإقرار: رفض من الخادم مع رسالة عربية |
| [07-acknowledged-unlocked.png](07-acknowledged-unlocked.png) | بعد الإقرار: "تم الإقرار بنجاح" وفتح الاختبار مع زر "ابدأ الاختبار" |
| [08-assessment-passed.png](08-assessment-passed.png) | الاختبار بعد الإقرار: النتيجة مجتاز |

## التسجيل

[ack-gate-flow.webm](ack-gate-flow.webm): رحلة الموظف كاملة من لوحة التحكم حتى نتيجة الاختبار.

## نتائج التحقق (17/17)

| # | التحقق | النتيجة |
|---|---|---|
| 1 | preview step | ✅ |
| 2 | preview: assessment shown as "🔒 الاختبار مقفل" before acknowledgment | ✅ |
| 3 | preview: simulated acknowledgment shows "تم الإقرار بنجاح" and unlocks "ابدأ الاختبار" | ✅ |
| 4 | published (assessment 4) | ✅ |
| 5 | dashboard: assessment locked and points to the content acknowledgment | ✅ |
| 6 | assessments list: card locked, no start button | ✅ |
| 7 | content page loads | ✅ |
| 8 | content page: assessment locked ("يتطلب إكمال الإقرار بالاطلاع أولاً"), button does not say "ابدأ الاختبار" | ✅ |
| 9 | direct start request refused by the server -> back to the content with "يجب الإقرار بالاطلاع على المحتوى قبل بدء الاختبار." | ✅ |
| 10 | acknowledge button disabled until the checkbox is checked | ✅ |
| 11 | success state "تم الإقرار بنجاح" | ✅ |
| 12 | assessment unlocked: "ابدأ الاختبار" enabled | ✅ |
| 13 | unlock transition shown ("تم فتح الاختبار") | ✅ |
| 14 | start assessment opens the questions | ✅ |
| 15 | assessment works (passed) | ✅ |
| 16 | revisit: already acknowledged, not asked again | ✅ |
| 17 | another employee is still locked (one user's acknowledgment does not unlock it for others) | ✅ |
