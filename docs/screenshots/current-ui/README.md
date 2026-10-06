# لقطات الواجهة الحالية (الـ commit 2a0de40)

لقطات حقيقية من التطبيق وهو يعمل (ASP.NET Core + PostgreSQL، وضع Local Authentication) ببيانات تجريبية عربية، بحجم متصفح 1440×900 ودقة مضاعفة (2880 بكسل عرضاً).
الصفحات الطويلة مصوّرة بنافذة بطول الصفحة كاملة. لم يُعدَّل أي تصميم أو شيفرة قبل التصوير.

| الملف | الشاشة | المسار |
|---|---|---|
| `01-login-local.png` | شاشة تسجيل الدخول (وضع Local Authentication) - الحالة الافتراضية للهوية البصرية | `/Account/Login` |
| `02-login-validation-errors.png` | تسجيل الدخول - رسائل التحقق عند ترك الحقول فارغة | `/Account/Login` |
| `03-login-wrong-credentials.png` | تسجيل الدخول - بيانات دخول غير صحيحة | `/Account/Login` |
| `04-change-password-first-login.png` | تغيير كلمة المرور الإجباري عند أول دخول للأدمن | `/Account/ChangePassword` |
| `05-employee-dashboard.png` | لوحة تحكم الموظف - إقرارات مطلوبة واختبارات متاحة ومحتوى حديث | `/` |
| `06-employee-content-list.png` | صفحة المحتوى - بطاقات المحتوى مع نوع المحتوى وحالة الإقرار | `/Content` |
| `07-employee-policies-list.png` | صفحة السياسات - السياسات فقط مع حالة الإقرار | `/Content/Policies` |
| `08-employee-policy-details-ack-required.png` | عرض سياسة - المحتوى المنسّق والمرفقات والرابط، ومربع الإقرار بالاطلاع (قبل الإقرار) | `/Content/Details/1` |
| `09-employee-policy-acknowledged.png` | عرض سياسة بعد الإقرار - حالة «تم الإقرار» وتاريخه | `/Content/Details/1` |
| `10-employee-awareness-content-details.png` | عرض محتوى توعوي مع اختبار مرتبط | `/Content/Details/4` |
| `11-employee-assessments-list.png` | قائمة الاختبارات - بطاقات الاختبارات (نسبة النجاح، عدد المحاولات) | `/Assessments` |
| `12-employee-assessment-take-empty.png` | شاشة أداء الاختبار - الأسئلة قبل الإجابة (اختيار واحد، صح/خطأ، اختيار متعدد) | `/Assessments/Take/6` |
| `13-employee-assessment-take-answered.png` | شاشة أداء الاختبار - بعد اختيار الإجابات | `/Assessments/Take/6` |
| `14-employee-assessment-submit-confirm.png` | نافذة تأكيد إرسال الإجابات | `/Assessments/Take/6` |
| `15-employee-assessment-result-failed.png` | شاشة نتيجة الاختبار - غير مجتاز (مع زر إعادة المحاولة) | `/Assessments/Result/3` |
| `15b-employee-assessment-result-passed.png` | شاشة نتيجة الاختبار - مجتاز مع تفاصيل كل سؤال (إجابة خاطئة واحدة) | `/Assessments/Result/6` |
| `16-employee-dashboard-completed.png` | لوحة الموظف بعد إكمال الإقرارات والاختبارات (نتائج ناجحة) | `/` |
| `17-employee-my-results.png` | صفحة «نتائجي» - الاختبارات المكتملة والإقرارات | `/MyResults` |
| `18-employee-dashboard-after.png` | لوحة الموظف بعد إكمال اختبار والإقرار على سياسة | `/` |
| `19-employee-user-menu.png` | قائمة المستخدم المنسدلة (تغيير كلمة المرور / تسجيل الخروج) | `/` |
| `20-employee-not-found-page.png` | صفحة خطأ 404 بالعربية | `/Content/Details/99999` |
| `21-employee-access-denied.png` | محاولة موظف الدخول إلى صفحة الإدارة - رفض الوصول | `/Account/Denied?ReturnUrl=%2FAdmin%2FDashboard` |
| `22-admin-dashboard.png` | لوحة تحكم الأدمن - مؤشرات المستخدمين والمحتوى والاختبارات والإقرارات والنشاط الأخير | `/Admin/Dashboard` |
| `23-admin-content-list.png` | إدارة المحتوى - قائمة المحتوى مع النوع والحالة وعدد الإقرارات والإجراءات | `/Admin/Content` |
| `24-admin-content-edit.png` | تعديل محتوى - النموذج مع النص المنسّق والمرفقات الحالية وإعادة تعيين الإقرارات | `/Admin/Content/Edit/1` |
| `25-admin-assessments-list.png` | إدارة الاختبارات - القائمة (نشر/إلغاء نشر، نسخ، إرسال بريد، حذف) | `/Admin/Assessments` |
| `26-admin-users-list.png` | إدارة المستخدمين - القائمة مع الأدوار وطريقة الدخول وآخر دخول | `/Admin/Users` |
| `27-admin-users-import-panel.png` | إدارة المستخدمين - لوحة الاستيراد من CSV | `/Admin/Users` |
| `28-admin-user-edit.png` | تعديل مستخدم - مع إعادة تعيين كلمة المرور | `/Admin/Users/Edit/3` |
| `29-admin-report-assessment-results.png` | التقارير - نتائج الاختبارات: مجتاز / غير مجتاز / لم يختبر، مع التصفية والتصدير | `/Admin/Reports/Assessments` |
| `30-admin-content-create-empty.png` | إنشاء محتوى - النموذج الفارغ (محرر النصوص الغنية) | `/Admin/Content/Create` |
| `31-admin-content-create-validation.png` | إنشاء محتوى - رسالة التحقق عند ترك العنوان فارغاً | `/Admin/Content/Create` |
| `32-admin-content-create-filled.png` | إنشاء محتوى - نموذج معبّأ: عنوان ووصف ونص منسّق (عناوين وقوائم واقتباس ورابط وصورة) ومرفق PDF وخيار الإقرار | `/Admin/Content/Create` |
| `34-admin-confirm-delete-modal.png` | نافذة تأكيد الحذف (Modal عربية) | `/Admin/Content` |
| `40-admin-user-create-form.png` | إدارة المستخدمين - نموذج إضافة مستخدم | `/Admin/Users/Create` |
| `41-admin-user-form-validation.png` | نموذج المستخدم - رسائل التحقق | `/Admin/Users/Create` |
| `42-admin-assessment-questions-list.png` | إدارة أسئلة اختبار - قائمة الأسئلة مع الإجابات الصحيحة | `/Admin/Assessments/Questions/1` |
| `43-admin-assessment-create-form.png` | إنشاء اختبار - النموذج (العنوان، المحتوى المرتبط، نسبة النجاح، عدد المحاولات) | `/Admin/Assessments/Create` |
| `44-admin-question-form-single.png` | إضافة سؤال - اختيار من متعدد (إجابة واحدة) مع خيارات الإجابة والدرجة | `/Admin/Assessments/AddQuestion/1` |
| `45-admin-question-form-truefalse.png` | إضافة سؤال - نوع صح / خطأ | `/Admin/Assessments/AddQuestion/1` |
| `46-admin-question-form-multiple.png` | إضافة سؤال - اختيار متعدد (عدة إجابات صحيحة) | `/Admin/Assessments/AddQuestion/1` |
| `47-admin-report-not-attempted.png` | التقارير - المستخدمون الذين لم يختبروا (تصفية) | `/Admin/Reports/Assessments?status=NotAttempted` |
| `48-admin-report-attempts.png` | التقارير - المحاولات والنتائج | `/Admin/Reports/Attempts` |
| `49-admin-attempt-details.png` | تفاصيل محاولة اختبار - الإجابات الصحيحة والمختارة | `/Admin/Reports/Attempt/6` |
| `50-admin-report-acknowledgments.png` | التقارير - الإقرارات: تم الإقرار / لم يتم الإقرار | `/Admin/Reports/Acknowledgments` |
| `51-admin-audit-log.png` | سجل التدقيق | `/Admin/Audit` |
| `52-admin-settings-branding.png` | الإعدادات - تبويب الهوية البصرية (الشعار، الألوان، صفحة الدخول، التذييل) مع المعاينة | `/Admin/Settings` |
| `53-admin-settings-general.png` | الإعدادات - تبويب عام | `/Admin/Settings` |
| `54-admin-settings-smtp.png` | الإعدادات - البريد الإلكتروني (SMTP) | `/Admin/Settings` |
| `55-admin-assessment-draft-questions.png` | اختبار مسودة - قائمة الأسئلة | `/Admin/Assessments/Questions/3` |
| `56-login-branded.png` | شاشة تسجيل الدخول بعد ضبط الهوية البصرية (شعار واسم المؤسسة وتنبيه وتذييل) | `/Account/Login` |
