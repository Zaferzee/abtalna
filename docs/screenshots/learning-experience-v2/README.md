# تجربة التعلّم v2: لقطات وتسجيلات الحركة

رحلة الموظف في الوعي السيبراني: المحتوى ← الإقرار ← الاختبار ← الإنجاز. أنشأ المسؤول عبر المعالج «سياسة حماية البيانات التجريبية» (إقرار + اختبار، درجة النجاح 100%، محاولتان)، ثم استخدمها موظفون عبر الواجهة فقط. السكربت: `tests/ui/learning-experience-acceptance.mjs`. الدقة 1440×900.

## اللقطات

| الملف | الوصف |
|---|---|
| [01-dashboard.png](01-dashboard.png) | لوحة الموظف: الترحيب، التقدم العام، "ما المطلوب مني الآن؟" مع رحلة الخطوة التالية، والإنجازات |
| [02-journey-component.png](02-journey-component.png) | مكوّن الرحلة: المحتوى ← الإقرار ← الاختبار (مقفل) ← الإنجاز |
| [03-content-cards.png](03-content-cards.png) | بطاقات المحتوى: النوع، الحالة (جديد / مطلوب / تم الإقرار / مكتمل)، متطلبات الإقرار والاختبار |
| [04-content-with-journey.png](04-content-with-journey.png) | صفحة المحتوى: شريط "رحلتك في هذه المادة" أعلى المحتوى |
| [05-locked-assessment.png](05-locked-assessment.png) | قبل الإقرار: لوحة الإقرار ثم بطاقة الاختبار المقفلة "🔒 الاختبار مقفل" |
| [06-unlocked-assessment.png](06-unlocked-assessment.png) | بعد الإقرار: "تم الإقرار بنجاح" وفتح الاختبار مع زر "ابدأ الاختبار" |
| [07-assessment-intro.png](07-assessment-intro.png) | مقدمة الاختبار: عدد الأسئلة، درجة النجاح، المحاولة، الوقت التقديري والتعليمات |
| [08-assessment-question.png](08-assessment-question.png) | سؤال الاختبار: شريط التقدم، الإجابة المحددة، التنقل بين الأسئلة |
| [09-result-failed.png](09-result-failed.png) | نتيجة غير مجتازة: "لم تحقق درجة الاجتياز هذه المرة" مع مراجعة المحتوى وإعادة المحاولة |
| [10-result-passed.png](10-result-passed.png) | نتيجة مجتازة: لحظة نجاح هادئة، الدرجة ودرجة النجاح والإجابات الصحيحة والتاريخ |
| [11-completed-learning-item.png](11-completed-learning-item.png) | إكمال المادة: بطاقة "تم إكمال هذه المادة بنجاح" مع الرحلة مكتملة |
| [12-content-completed.png](12-content-completed.png) | صفحة المحتوى بعد الإكمال: الإقرار، الاختبار المجتاز، وبطاقة الإنجاز |
| [13-dashboard-after-completion.png](13-dashboard-after-completion.png) | لوحة الموظف بعد الإكمال: ارتفاع التقدم (+) وظهور المادة في "إنجازاتك الأخيرة" |
| [14-assessments-list.png](14-assessments-list.png) | قائمة الاختبارات: الحالة، المحتوى المرتبط، والإجراء التالي |

## تسجيلات الحركة (WebM)

| الملف | ما يعرضه |
|---|---|
| [motion/01-dashboard-progress.webm](motion/01-dashboard-progress.webm) | لوحة الموظف: دخول البطاقات بالتتابع، امتلاء حلقة التقدم والعدّ، شارة الزيادة، وتفاعل المرور |
| [motion/02-acknowledgment-unlock.webm](motion/02-acknowledgment-unlock.webm) | المحتوى ← الإقرار ← "تم الإقرار بنجاح" وتحوّل القفل إلى فتح الاختبار مع زر "ابدأ الاختبار" |
| [motion/03-assessment-selection.webm](motion/03-assessment-selection.webm) | الاختبار: المقدمة، تحديد الإجابات وحالة المرور والتحديد، الانتقال بين الأسئلة، ملخص الإرسال وحالة "جارٍ احتساب نتيجتك" |
| [motion/04-result-pass.webm](motion/04-result-pass.webm) | نتيجة مجتازة: ظهور الحلقة والعدّ، "أحسنت، اجتزت الاختبار بنجاح"، احتفال صغير لمرة واحدة، وبطاقة إكمال المادة |
| [motion/05-result-fail.webm](motion/05-result-fail.webm) | نتيجة غير مجتازة: عرض هادئ غير عقابي مع "مراجعة المحتوى" و"إعادة المحاولة" |
| [motion/06-full-learning-journey.webm](motion/06-full-learning-journey.webm) | الرحلة كاملة: لوحة الموظف ← المحتوى والرحلة ← الإقرار وفتح الاختبار ← الاختبار ← النتيجة وإكمال المادة ← لوحة الموظف وارتفاع التقدم |

## نتائج التحقق (23/23)

| # | التحقق | النتيجة |
|---|---|---|
| 1 | admin: preview step reached | ✅ |
| 2 | admin: content + assessment published through the wizard | ✅ |
| 3 | dashboard: "ما المطلوب مني الآن؟" with the next step and its journey | ✅ |
| 4 | dashboard: overall progress "أنجزت 0 من 4 مواد" (0%) | ✅ |
| 5 | dashboard: the new item is listed as "مطلوب" with its journey and a link to the acknowledgment | ✅ |
| 6 | content card: status "مطلوب", requires acknowledgment, linked assessment | ✅ |
| 7 | content page: journey content:current ack:upcoming assessment:locked done:upcoming | ✅ |
| 8 | content page: assessment locked before acknowledgment (no "ابدأ الاختبار") | ✅ |
| 9 | after acknowledgment: "تم الإقرار بنجاح", unlock transition, "ابدأ الاختبار" | ✅ |
| 10 | journey after acknowledgment: content:done ack:done assessment:current done:upcoming | ✅ |
| 11 | assessment intro: questions, passing score, attempt and estimated time | ✅ |
| 12 | assessment: selected answer state | ✅ |
| 13 | submission confirmation shows the answered summary | ✅ |
| 14 | failed result: encouraging message, review content and retry | ✅ |
| 15 | passed result: "أحسنت، اجتزت الاختبار بنجاح" | ✅ |
| 16 | passed result: score, passing score, correct answers, completion date | ✅ |
| 17 | passed result: learning item completed ("تم إكمال هذه المادة بنجاح") | ✅ |
| 18 | content page after completion: completion card, acknowledgment not asked again | ✅ |
| 19 | dashboard after completion: progress 0% -> 25%, listed in achievements, no longer a task | ✅ |
| 20 | My Results lists the failed and the passed attempt | ✅ |
| 21 | employee B: assessment selection recorded up to the result | ✅ |
| 22 | full journey: completion shows in achievements and the progress increase is indicated on the dashboard | ✅ |
| 23 | reduced motion: journey shown in its final state immediately | ✅ |

نظام الحركة موثّق في [docs/MOTION_DESIGN.md](../../MOTION_DESIGN.md)، ونموذج الحالات في [docs/LEARNING_EXPERIENCE.md](../../LEARNING_EXPERIENCE.md).
