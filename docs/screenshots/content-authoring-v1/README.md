# اختبار قبول: إنشاء «لائحة المخالفات الداخلية للأمن السيبراني» عبر المعالج

لقطات حقيقية (Chromium، عرض 1440 بكسل) من تشغيل آلي للواجهة: أنشأ المسؤول المادة **من خلال واجهة التطبيق فقط** (بدون إدخال مباشر في قاعدة البيانات)، ثم أكملت موظفة (nora.harbi) القراءة والإقرار والاختبار، ثم راجع المسؤول التقارير.
المحتوى: مقدمة مكتوبة، أربع مواد بعناوين، جدول مخالفات وجزاءات (لُصق من Word ثم أضيف له صف بزر الجدول)، أحكام عامة مرقمة (قائمة Word محوّلة)، اقتباس بارز، مرفق PDF، ورابط داخلي؛ الإقرار مطلوب؛ اختبار من 6 أسئلة (اختيار واحد، اختيار متعدد، صح/خطأ) بدرجة نجاح 80% ومحاولتين.

| # | الملف | الوصف |
|---|---|---|
| 1 | [`01-wizard-basic-information.png`](01-wizard-basic-information.png) | المعالج - الخطوة 1: المعلومات الأساسية (العنوان، نوع "لائحة"، وصف مختصر) |
| 2 | [`02-wizard-content-editor.png`](02-wizard-content-editor.png) | المعالج - الخطوة 2: محرر المحتوى (عناوين، قائمة مرقمة من Word، جدول المخالفات، اقتباس، مرفق PDF ورابط) |
| 3 | [`03-wizard-acknowledgment.png`](03-wizard-acknowledgment.png) | المعالج - الخطوة 3: الإقرار مطلوب + نص الإقرار + معاينة مطابقة لما يراه الموظف |
| 4 | [`04b-wizard-question-editor.png`](04b-wizard-question-editor.png) | منشئ الأسئلة: سؤال اختيار متعدد قبل الحفظ (تحديد أكثر من إجابة صحيحة، الدرجة) |
| 5 | [`04-wizard-assessment-builder.png`](04-wizard-assessment-builder.png) | المعالج - الخطوة 4: إعدادات الاختبار (درجة النجاح 80%، محاولتان) وقائمة الأسئلة الست مرتبة مع الإجابات الصحيحة |
| 6 | [`05-wizard-preview.png`](05-wizard-preview.png) | المعالج - الخطوة 5: معاينة كموظف (نفس مكوّنات صفحة الموظف: المحتوى، المرفقات، الإقرار، الاختبار المرتبط، ومعاينة الأسئلة) |
| 7 | [`06-wizard-publish-summary.png`](06-wizard-publish-summary.png) | المعالج - الخطوة 6: الملخص قبل النشر (الإقرار مطلوب، الاختبار، عدد الأسئلة، درجة النجاح) + حفظ كمسودة / نشر الآن |
| 8 | [`06b-wizard-published.png`](06b-wizard-published.png) | بعد النشر: تأكيد النشر وروابط العرض كموظف والإشعار بالبريد والتقارير |
| 9 | [`06c-content-management-list.png`](06c-content-management-list.png) | إدارة المحتوى: المادة الجديدة منشورة مع الإقرار والاختبار المرتبط وتاريخ النشر |
| 10 | [`07a-employee-policies-list.png`](07a-employee-policies-list.png) | الموظف: صفحة السياسات تعرض اللائحة الجديدة بانتظار الإقرار |
| 11 | [`07-employee-regulation-view.png`](07-employee-regulation-view.png) | الموظف: اللائحة المنشورة (التنسيق، الجدول، القائمة المرقمة، المرفق والرابط، الإقرار، ثم الاختبار المرتبط) |
| 12 | [`07b-employee-acknowledged-next-step.png`](07b-employee-acknowledged-next-step.png) | بعد الإقرار: حالة "تم الإقرار" والخطوة التالية: الاختبار |
| 13 | [`08-employee-assessment.png`](08-employee-assessment.png) | الموظف: الاختبار (سؤال اختيار متعدد، شريط التقدم، مؤشر الأسئلة) |
| 14 | [`09-employee-result.png`](09-employee-result.png) | الموظف: النتيجة (100%، مجتاز) ومراجعة الإجابات |
| 15 | [`09b-employee-dashboard-after.png`](09b-employee-dashboard-after.png) | لوحة الموظف بعد الإقرار والاختبار (تحدّث التقدم وأحدث النتائج) |
| 16 | [`10-admin-report-result.png`](10-admin-report-result.png) | تقرير الإدارة: نتيجة الموظفة في اختبار اللائحة (مجتاز، 100%) وبقية الموظفين "لم يختبر" |
| 17 | [`10b-admin-report-acknowledgments.png`](10b-admin-report-acknowledgments.png) | تقرير الإقرارات للائحة: تم الإقرار مع التاريخ والوقت |

## نتائج التحقق الآلي أثناء التشغيل (18/18 ناجحة)

| التحقق | النتيجة |
|---|---|
| step 1 saved, moved to Write | PASS |
| pasted table kept (1 table, 6 rows) | PASS |
| headings kept (4) | PASS |
| Word numbered list converted to a list (4 items) | PASS |
| Word colours and script removed on paste | PASS |
| step 2 saved | PASS |
| 6 questions listed in order | PASS |
| reorder works | PASS |
| moved to preview | PASS |
| employee sees the table with the added row (7 rows) | PASS |
| numbered rules and callout rendered | PASS |
| attachment and link shown | PASS |
| acknowledgment statement shown: أقرّ بأنني قرأت وفهمت لائحة المخالفات الداخلية للأمن السيبراني. | PASS |
| related assessment can be started from the content page | PASS |
| attachment downloads as a PDF | PASS |
| acknowledged | PASS |
| result: passed (مجتاز) | PASS |
| admin report lists the employee | PASS |

لا أخطاء JavaScript ولا نوافذ متصفح أصلية أثناء التشغيل. السكربت: `tests/ui/content-authoring-acceptance.mjs`.
