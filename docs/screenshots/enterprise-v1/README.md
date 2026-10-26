# لقطات الإدارة الهرمية للجهات والتكوينات البصرية (enterprise v1)

قبول عبر المتصفح لكل شيء من الواجهة فقط: المدير المركزي يبني هيكلاً من ثلاثة مستويات، وينشئ المستخدمين، ويفوّض مديرَي جهتين بصلاحيات مختلفة، ويوجّه محتوى مركزياً إلى جهات محددة؛ ويعمل كل مدير جهة داخل نطاقه فقط (ويُرفض خارجه حتى بالرابط المباشر)؛ ثم هوية الجهة، وتقرير المقارنة بين الجهات، والتكوينات البصرية لصفحة الدخول.
السكربت: `tests/ui/enterprise-entities-acceptance.mjs`. الدقة 1440×900 (والجوال 390×844).

الهيكل المستخدم:
```
شركة الأمان الرقمي (ROOT)
├─ هيئة الحكومة الرقمية (DGA)            ← مدير: dga.admin (تسع صلاحيات منها الهوية والتدقيق)
│  └─ الإدارة العامة للأمن السيبراني (DGA-CS)
│     └─ إدارة التوعية والتدريب (DGA-AW)
└─ هيئة البيانات الوطنية (SDA)             ← مدير: sda.admin (المستخدمون والتقارير فقط)
   └─ إدارة حوكمة البيانات (SDA-GOV)
```

## اللقطات

| الملف | الوصف |
|---|---|
| [01-entity-hierarchy.png](01-entity-hierarchy.png) | الهيكل التنظيمي: شجرة الجهات (ثلاثة مستويات) مع جدول الحالة والمستخدمين والمديرين والهوية والدليل |
| [02-central-entity-management.png](02-central-entity-management.png) | إدارة الجهة مركزياً: البطاقة التعريفية والتبويبات (نظرة عامة، المديرون والصلاحيات، المستخدمون، الهوية، الدليل، المحتوى، التقارير، التدقيق) |
| [03-delegated-permissions.png](03-delegated-permissions.png) | الصلاحيات المفوّضة: مدير الجهة وصلاحياته الصريحة، والسقف المسموح للجهة كما حدده المدير المركزي |
| [05-central-content-selected-entities.png](05-central-content-selected-entities.png) | محتوى مركزي موجّه لجهات محددة: الإدارة العامة للأمن السيبراني (وتوابعها) وهيئة البيانات الوطنية، دون نسخ المحتوى |
| [04-entity-admin-dashboard.png](04-entity-admin-dashboard.png) | لوحة مدير الجهة: الأرقام والنشاط محصورة في نطاقه (هيئة الحكومة الرقمية وتوابعها) مع شريط النطاق والصلاحيات |
| [07a-entity-branding-studio.png](07a-entity-branding-studio.png) | هوية الجهة: الحقول الفارغة موروثة من الهوية المركزية، مع معاينة حية بنفس مكوّن المعاينة المركزي |
| [04b-second-entity-admin.png](04b-second-entity-admin.png) | مدير جهة ثانٍ بصلاحيات أضيق: المستخدمون والتقارير فقط (لا محتوى ولا تصدير ولا تدقيق) |
| [04c-second-entity-admin-dashboard.png](04c-second-entity-admin-dashboard.png) | لوحة مدير هيئة البيانات الوطنية: نطاق مختلف وصلاحيات مختلفة |
| [07b-entity-branding-dga-employee.png](07b-entity-branding-dga-employee.png) | بوابة موظف في جهة تابعة للحكومة الرقمية: الهوية المخصصة موروثة من الجهة الأم |
| [07c-central-branding-sda-employee.png](07c-central-branding-sda-employee.png) | بوابة موظف في هيئة البيانات: الهوية المركزية (لا يتأثر بهوية جهة أخرى) |
| [06-central-report-entities.png](06-central-report-entities.png) | تقرير مركزي: مقارنة الجهات (نسبة الإنجاز والإقرار والنجاح) مع التعمق في الهيكل |
| [06b-central-report-drilldown.png](06b-central-report-drilldown.png) | التعمق في هيئة الحكومة الرقمية: الجهة كاملة ثم جهاتها التابعة |
| [09-audit-by-entity.png](09-audit-by-entity.png) | سجل التدقيق مفلتراً حسب الجهة: إنشاء الجهات وتعيين المديرين وتعديل الهوية مع الجهة المتأثرة |
| [10-mobile-entities.png](10-mobile-entities.png) | الهيكل التنظيمي على الجوال (390×844) |
| [08a-studio-compositions.png](08a-studio-compositions.png) | استوديو الهوية: التكوينات البصرية الجاهزة بمعاينات مصغّرة حقيقية، ثم ضبط الطبقات |
| [08b-studio-layers.png](08b-studio-layers.png) | محرر الطبقات: العنصر والموضع والشفافية والحجم والدوران ضمن حدود آمنة |
| [08c-login-governance.png](08c-login-governance.png) | صفحة الدخول بتكوين «الحوكمة والامتثال» |
| [08d-login-cyber-defense.png](08d-login-cyber-defense.png) | صفحة الدخول بتكوين «الدفاع السيبراني» |
| [08e-login-awareness-journey.png](08e-login-awareness-journey.png) | صفحة الدخول بتكوين «رحلة التوعية» |
| [08f-login-executive.png](08f-login-executive.png) | صفحة الدخول بتكوين «الأمن التنفيذي» |
| [08g-login-mobile.png](08g-login-mobile.png) | صفحة الدخول على الجوال: تبقى اللمسة الهندسية الناعمة فقط |

## الفحوص (34/34 ناجحة، بدون أخطاء JavaScript)

- ✅ central: organization page
- ✅ central: five entities created through the form (three levels deep)
- ✅ central: user filter by entity drills into the subtree
- ✅ central: entity administrator assigned with explicit capabilities
- ✅ central: content list shows the audience ("2 selected entities")
- ✅ targeting: selected entities and their sub-entities see the policy; the parent agency (not selected) does not
- ✅ DGA admin: administration area
- ✅ DGA admin: dashboard shows the delegated scope
- ✅ DGA admin: menu reflects capabilities (no system settings)
- ✅ DGA admin: users list limited to own subtree
- ✅ DGA admin: direct URL to another entity's employee -> 404
- ✅ DGA admin: direct URL to another entity -> 404
- ✅ DGA admin: centrally owned content cannot be edited
- ✅ DGA admin: system settings refused
- ✅ DGA admin: can own content only in its own entities
- ✅ DGA admin: entity branding saved
- ✅ SDA admin: no content, assessment or audit capabilities in the menu
- ✅ SDA admin: content management refused by URL
- ✅ SDA admin: other entity branding refused
- ✅ SDA admin: acknowledgment report limited to own entity employees
- ✅ SDA admin: export refused (no export capability)
- ✅ branding: DGA subtree uses its own colors (#0f5c4a), SDA keeps the central ones (#1f4fd8)
- ✅ RTL: employee portals right-to-left without horizontal overflow
- ✅ central report: comparison across entities
- ✅ audit: entity, delegation and branding events recorded for the entity
- ✅ RTL: central administration pages without horizontal overflow
- ✅ responsive: entity admin dashboard at 390px
- ✅ responsive: entities page at 390px
- ✅ login: "governance" composition renders grid + document + circles (no pointer events)
- ✅ login: "defense" composition renders network + shield + circles (no pointer events)
- ✅ login: "journey" composition renders journey + circles (no pointer events)
- ✅ login: "executive" composition renders shield + circles + network (no pointer events)
- ✅ studio: a layer moved behind the text is capped at 12% and becomes a custom composition (preview)
- ✅ login on a phone: decorative layers simplified (circles), no overflow
