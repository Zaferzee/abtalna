# لقطات العرض المضمن للفيديو وPDF وسجل التدقيق (multimedia v1)

أُنشئت المادة «لائحة تجريبية متعددة الوسائط» عبر واجهة المستخدم فقط (معالج إنشاء المحتوى): نص عربي منسق، فيديو MP4، ملف PDF من 3 صفحات، إقرار، واختبار مرتبط؛ ثم نُشرت وتحقق منها موظف ومسؤول. السكربت: `tests/ui/multimedia-acceptance.mjs`. الدقة 1440×900.

## اللقطات

| الملف | الوصف |
|---|---|
| [01-employee-inline-video.png](01-employee-inline-video.png) | صفحة الموظف: الفيديو المضمن أثناء التشغيل (عناصر التحكم الأصلية: تشغيل/إيقاف، تقديم، صوت، ملء الشاشة) |
| [02-employee-inline-pdf.png](02-employee-inline-pdf.png) | صفحة الموظف: عارض PDF المضمن (اسم المستند، التنقل بين الصفحات، التكبير، ملء الشاشة، التنزيل) |
| [03-pdf-viewer-normal-zoom.png](03-pdf-viewer-normal-zoom.png) | عارض PDF عند تكبير 100% (صفحة A4) |
| [03b-pdf-viewer-page3-zoom125.png](03b-pdf-viewer-page3-zoom125.png) | عارض PDF: الصفحة 3 بتكبير 125% |
| [04-admin-preview-multimedia.png](04-admin-preview-multimedia.png) | معاينة المسؤول كموظف: الفيديو المضمن وعارض PDF داخل إطار صفحة الموظف قبل النشر |
| [04b-admin-preview-full.png](04b-admin-preview-full.png) | المعاينة كاملة (نص → فيديو → PDF → إقرار → اختبار) |
| [05-audit-log.png](05-audit-log.png) | سجل التدقيق: سجلات فعلية (التاريخ والوقت، المستخدم/المنفذ، الإجراء بالعربية مع الرمز الأصلي، الكيان، التفاصيل) |
| [05b-audit-log-filtered.png](05b-audit-log-filtered.png) | سجل التدقيق مصفّى حسب الإجراء "نشر محتوى" |
| [06-pdf-viewer-error-missing.png](06-pdf-viewer-error-missing.png) | حالة الخطأ بالعربية: المستند غير متاح |

## التسجيل

[multimedia-content-flow.webm](multimedia-content-flow.webm) (≈20 ثانية): فتح صفحة المحتوى → تشغيل الفيديو → التمرير إلى PDF → التنقل بين الصفحات والتكبير في العارض.

## نتائج التحقق (22/22)

| # | التحقق | النتيجة |
|---|---|---|
| 1 | content written and files uploaded through the wizard | ✅ |
| 2 | preview step | ✅ |
| 3 | 9. admin employee-preview shows the inline video and the PDF viewer | ✅ |
| 4 | published | ✅ |
| 5 | 1. content loads | ✅ |
| 6 | page order: text, video, PDF, acknowledgment, assessment | ✅ |
| 7 | 2. video embedded in-page with native controls (authenticated attachment URL, no storage path): /Files/Attachment/3 | ✅ |
| 8 | video frame 960x540 (16:9, max 960px) | ✅ |
| 9 | 3. video plays (currentTime 2.2s of 8.6s) | ✅ |
| 10 | 4. PDF visible in-page (pages: 3, rendered: 1, viewer width 920px) | ✅ |
| 11 | 5. PDF viewer works (next page -> 2, zoom in -> 1.25, go to page -> 3) | ✅ |
| 12 | viewer shows Arabic messages (missing: "المستند غير متاح (ربما حُذف، أو ليست لديك صلاحية الوصول إليه).", invalid: "تعذّر عرض الملف: الملف تالف أو ليس مستند PDF صالحاً.") | ✅ |
| 13 | 6a. signed-in employee: PDF 200 application/pdf, video range request 206 | ✅ |
| 14 | 6b. anonymous: PDF 302, video 302 (redirect to sign-in, no file) | ✅ |
| 15 | 6c. employee cannot open an attachment of unpublished content (/Files/Attachment/2 -> 404) | ✅ |
| 16 | 6d. expired session in the viewer -> "انتهت جلستك. سجّل الدخول مجدداً لعرض المستند." | ✅ |
| 17 | 7. acknowledgment works | ✅ |
| 18 | 8. assessment works (passed) | ✅ |
| 19 | 11. dashboard "عرض الكل" opens the Audit Log (50 entries on page 1) | ✅ |
| 20 | 10. Audit Log shows real entries with Arabic action names: نشر اختبار ASSESSMENT_PUBLISHED | ✅ |
| 21 | audit filter by action works (6 × CONTENT_PUBLISHED) | ✅ |
| 22 | empty state in Arabic when nothing matches | ✅ |

ملاحظة: متصفح Chromium المستخدم في الاختبار الآلي لا يتضمن ترميز H.264، لذا استُخدم ملف MP4 بترميز VP9/Opus. متصفحات Chrome وEdge وFirefox وSafari على أجهزة الموظفين تشغّل ملفات MP4 بترميز H.264 المعتادة.
