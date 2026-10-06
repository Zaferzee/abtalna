# لقطات الواجهة الجديدة (Redesign v1)

لقطات حقيقية من التطبيق قيد التشغيل (فرع `claude/ui-redesign-v1`)، بالعربية واتجاه RTL، ببيانات تجريبية عربية.
الدقة الأساسية 1440×900 (اللقطات الكاملة مأخوذة بنافذة طويلة بنفس العرض)، مع فحوصات إضافية بدقة 1920×1080 و1366×768 وجهاز لوحي 820×1180.
لقطات الواجهة السابقة محفوظة كما هي في `docs/screenshots/current-ui/` للمقارنة.

| # | الملف | الوصف | المقاس |
|---|---|---|---|
| 1 | [`01-login.png`](01-login.png) | تسجيل الدخول: لوحة هوية بصرية متحركة + بطاقة دخول (Local Authentication) | 1440×900 |
| 2 | [`02-login-validation.png`](02-login-validation.png) | تسجيل الدخول: رسائل التحقق بالعربية | 1440×900 |
| 3 | [`03-employee-dashboard.png`](03-employee-dashboard.png) | لوحة الموظف: ترحيب، حلقة التقدم العام، قائمة المهام، التقدّم، أحدث النتائج والمحتوى | 1440×900 (صفحة كاملة) |
| 4 | [`04-employee-dashboard-all-done.png`](04-employee-dashboard-all-done.png) | لوحة الموظف بعد إكمال كل المطلوب (حالة الإنجاز) | 1440×900 (صفحة كاملة) |
| 5 | [`05-policy-view.png`](05-policy-view.png) | عرض السياسة: رأس المستند، شرائح البيانات، نص القراءة، فهرس الصفحة، المرفقات، لوحة الإقرار | 1440×900 (صفحة كاملة) |
| 6 | [`06-policy-ack-ready.png`](06-policy-ack-ready.png) | لوحة الإقرار بعد تحديد مربع التأكيد (يتفعّل الزر) | 1440×900 |
| 7 | [`07-policy-acknowledged.png`](07-policy-acknowledged.png) | بعد الإقرار: رسالة النجاح وحالة "تم الإقرار" | 1440×900 |
| 8 | [`08-assessments-list.png`](08-assessments-list.png) | قائمة الاختبارات: بطاقات بحالة كل اختبار، درجة النجاح، المحاولات، أفضل نتيجة | 1440×900 (صفحة كاملة) |
| 9 | [`09-assessment-intro.png`](09-assessment-intro.png) | تجربة الاختبار: شاشة البداية (عدد الأسئلة، درجة النجاح، التعليمات) | 1440×900 |
| 10 | [`10-assessment-single-choice.png`](10-assessment-single-choice.png) | سؤال اختيار واحد: بطاقات اختيار تفاعلية + شريط التقدّم + مؤشر الأسئلة | 1440×900 |
| 11 | [`11-assessment-true-false.png`](11-assessment-true-false.png) | سؤال صح/خطأ: بطاقتان كبيرتان | 1440×900 |
| 12 | [`12-assessment-multiple-choice.png`](12-assessment-multiple-choice.png) | سؤال اختيار متعدد: تحديد أكثر من إجابة | 1440×900 |
| 13 | [`13-assessment-unanswered-warning.png`](13-assessment-unanswered-warning.png) | تنبيه الأسئلة غير المجاب عنها قبل الإرسال (ينقل إلى أول سؤال ناقص) | 1440×900 |
| 14 | [`14-assessment-submit-confirm.png`](14-assessment-submit-confirm.png) | تأكيد الإرسال (نافذة عربية بدل نافذة المتصفح) | 1440×900 |
| 15 | [`15-result-passed.png`](15-result-passed.png) | النتيجة (ناجح): حلقة النسبة مع علامة درجة النجاح، احتفال خفيف، الإحصاءات، مراجعة الإجابات | 1440×900 (صفحة كاملة) |
| 16 | [`16-result-failed.png`](16-result-failed.png) | النتيجة (غير مجتاز): إعادة المحاولة ومراجعة المادة | 1440×900 (صفحة كاملة) |
| 17 | [`17-admin-dashboard.png`](17-admin-dashboard.png) | لوحة الإدارة: مؤشرات بقيم وتسميات، مخطط حالة الاختبارات، الإقرارات، إجراءات سريعة، النشاط الأخير | 1440×900 (صفحة كاملة) |
| 18 | [`18-content-management.png`](18-content-management.png) | إدارة المحتوى: شريط بحث وتصفية، جدول بأيقونات الأنواع وحالات النشر وإجراءات الصف | 1440×900 (صفحة كاملة) |
| 19 | [`19-reports-assessments.png`](19-reports-assessments.png) | التقارير - نتائج الاختبارات: تصفية، شرائح الحالة، تصدير، شريط النتيجة | 1440×900 (صفحة كاملة) |
| 20 | [`20-reports-acknowledgments.png`](20-reports-acknowledgments.png) | التقارير - الإقرارات (نفس المكوّنات المشتركة) | 1440×900 (صفحة كاملة) |
| 21 | [`21-branding-settings.png`](21-branding-settings.png) | الهوية البصرية: الحقول + لوحات ألوان جاهزة + فحص التباين + معاينة مباشرة | 1440×900 (صفحة كاملة) |
| 22 | [`22-branding-live-preview.png`](22-branding-live-preview.png) | المعاينة المباشرة تتغير فوراً (لوحة "زمردي" + اسم جديد) قبل الحفظ | 1440×900 |
| 23 | [`23-branding-login-preview.png`](23-branding-login-preview.png) | معاينة صفحة الدخول داخل صفحة الهوية البصرية | 1440×900 |
| 24 | [`24-branding-contrast-warning.png`](24-branding-contrast-warning.png) | تحذير تباين منخفض عند اختيار لون نص غير مقروء | 1440×900 |
| 25 | [`25-admin-dashboard-1920.png`](25-admin-dashboard-1920.png) | لوحة الإدارة بدقة 1920×1080 | 1920×1080 |
| 26 | [`26-employee-dashboard-1366.png`](26-employee-dashboard-1366.png) | لوحة الموظف بدقة 1366×768 | 1366×768 |
| 27 | [`27-employee-dashboard-tablet.png`](27-employee-dashboard-tablet.png) | لوحة الموظف على جهاز لوحي 820×1180 (قائمة جانبية منزلقة) | 820×1180 |
| 28 | [`28-tablet-sidebar-open.png`](28-tablet-sidebar-open.png) | الجهاز اللوحي: القائمة الجانبية مفتوحة | 820×1180 |
| 29 | [`29-policy-view-tablet.png`](29-policy-view-tablet.png) | عرض السياسة على جهاز لوحي | 820×1180 |
| 30 | [`30-login-tablet.png`](30-login-tablet.png) | تسجيل الدخول على جهاز لوحي | 820×1180 |
| 31 | [`31-brand-alt-employee-dashboard.png`](31-brand-alt-employee-dashboard.png) | نفس الواجهة بعد حفظ لوحة ألوان مختلفة (لا ألوان ثابتة في الكود) | 1440×900 |
| 32 | [`32-brand-alt-admin-dashboard.png`](32-brand-alt-admin-dashboard.png) | لوحة الإدارة بلوحة الألوان البديلة | 1440×900 |

## الحركة (فيديو حقيقي من المتصفح)

تسجيلات WebM بدقة 1440×900 في [`motion/`](motion/): شاشة الدخول، لوحة الموظف، الانتقال بين أسئلة الاختبار، ظهور النتيجة الناجحة، معالج إنشاء المحتوى، ونفس لوحة الموظف مع تفعيل «تقليل الحركة». الشرح الكامل للمدد ومنحنيات التسارع والغرض في [`docs/MOTION_DESIGN.md`](../../MOTION_DESIGN.md).
