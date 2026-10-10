# لقطات تحسين استوديو الهوية (branding-studio v1.1)

ثلاثة تحسينات محدودة عبر الواجهة فقط: فصل الشعارات (القائمة الجانبية / الدخول / المختصر / المتصفح)، أوضاع عرض صورة صفحة الدخول (خلفية كاملة، كبيرة، متوسطة، صغيرة، مخفية)، واختيار النمط الزخرفي.
السكربت: `tests/ui/branding-studio-refine.mjs`. الدقة 1440×900 (والجوال 390×844).

## اللقطات

| الملف | الوصف |
|---|---|
| [01-studio-logos-independent.png](01-studio-logos-independent.png) | الاستوديو: شعار القائمة الجانبية وشعار صفحة الدخول ملفان مستقلان؛ كل بطاقة توضّح أين يُستخدم الشعار فقط |
| [02-preview-app-sidebar-logo.png](02-preview-app-sidebar-logo.png) | المعاينة (التطبيق): القائمة الجانبية تعرض شعارها الخاص، لا شعار صفحة الدخول |
| [03-sidebar-own-logo.png](03-sidebar-own-logo.png) | لوحة الإدارة: شعار القائمة الجانبية (مستقل عن شعار صفحة الدخول) |
| [04-login-own-logo.png](04-login-own-logo.png) | صفحة الدخول: شعار الدخول الخاص (الختم) وليس شعار القائمة الجانبية |
| [05-studio-image-small.png](05-studio-image-small.png) | الاستوديو: وضع «صغيرة (عنصر بصري)» مع الحجم والمحاذاة والمكان، والمعاينة تعرض الصورة فوراً |
| [06-login-image-small.png](06-login-image-small.png) | صفحة الدخول: صورة صغيرة كعنصر بصري فوق النص، مع نمط «شبكة الضوابط» |
| [07-login-image-medium.png](07-login-image-medium.png) | صفحة الدخول: صورة متوسطة تحت النص، مع «دوائر ناعمة» |
| [08-studio-image-background.png](08-studio-image-background.png) | الاستوديو: وضع «خلفية كاملة» (تظهر التغطية والتكبير ونقطة التركيز) |
| [09-login-image-background.png](09-login-image-background.png) | صفحة الدخول: الصورة كخلفية كاملة مع طبقة التغطية |
| [10-studio-decor-picker.png](10-studio-decor-picker.png) | الاستوديو: اختيار «النمط الزخرفي» (درع، دوائر ناعمة، قائمة تحقق، شبكة الضوابط، وثيقة السياسة، مسار التقدم، بدون) والمعاينة تتغير فوراً |
| [11-login-decor-checklist.png](11-login-decor-checklist.png) | صفحة الدخول: نمط «قائمة تحقق» |
| [12-login-decor-journey.png](12-login-decor-journey.png) | صفحة الدخول: نمط «مسار التقدم» |
| [13-login-decor-document.png](13-login-decor-document.png) | صفحة الدخول: نمط «وثيقة السياسة» |
| [14-login-mauve-large.png](14-login-mauve-large.png) | سمة «الوردي الموفي الهادئ» مع صورة كبيرة فوق النص ونمط «شبكة الضوابط» |

## الفحوص (21/21 ناجحة، بدون أخطاء JavaScript)

- ✅ preview: a new sidebar logo does not appear on the login page
- ✅ preview: login logo (440px) and sidebar logo (520px) are different files
- ✅ saved: login page shows the login logo, the sidebar shows the sidebar logo
- ✅ changing the sidebar logo does not change the login logo
- ✅ changing the login logo does not change the sidebar logo
- ✅ removing the sidebar logo leaves the login logo; the compact icon appears only in the sidebar
- ✅ preview: small image shown as a framed mark (22% of the text column), no full background
- ✅ size / alignment / placement controls appear for framed modes; overlay only for the full background
- ✅ login page: small image (24%), RTL, no overflow
- ✅ preview: medium image below the text (60%)
- ✅ login page: medium image (60%)
- ✅ preview: full background, no framed picture
- ✅ login page: full background image
- ✅ phone: full background, no overflow, line art hidden
- ✅ preview: hidden mode shows no image (file kept)
- ✅ preview: every decorative pattern applies immediately
- ✅ preview: the chosen line art is shown and "No decoration" hides everything
- ✅ login page: "Checklist" pattern
- ✅ login page: "Progress journey" pattern
- ✅ theme preset (Soft Mauve Rose) + large image: colors applied, Arabic, RTL, no overflow
- ✅ reset to default: default decoration, no image
