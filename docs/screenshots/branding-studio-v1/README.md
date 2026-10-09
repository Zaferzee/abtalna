# لقطات استوديو الهوية والمظهر (branding-studio v1)

خصّص المسؤول هوية المنصة بالكامل عبر واجهة «الإعدادات ← الهوية والمظهر» فقط (بدون تعديل ملفات): أسماء متعددة الأسطر، نصوص صفحة الدخول، الشعارات، صورة الدخول (الملاءمة والموضع ونقطة التركيز والتكبير والتغطية)، ثم سمة «الكحلي التنفيذي» وبعدها «الوردي الموفي الهادئ»، وأخيراً استعادة الافتراضي.
السكربت: `tests/ui/branding-studio-acceptance.mjs`. الدقة 1440×900 (والجوال 390×844).

## اللقطات

| الملف | الوصف |
|---|---|
| [01-studio-identity.png](01-studio-identity.png) | الاستوديو: الهوية العامة (الأسماء متعددة الأسطر وخيارات الظهور) مع المعاينة المباشرة لصفحة الدخول |
| [02-studio-login-text.png](02-studio-login-text.png) | نصوص صفحة الدخول: الشارة والعنوان والوصف والنقاط والبطاقة والتنبيه، كلها قابلة للتعديل |
| [03-studio-logos.png](03-studio-logos.png) | الشعارات والأيقونات: شعار القائمة الجانبية، شعار الدخول وموضعه، الأيقونة المختصرة، أيقونة المتصفح (رفع/استبدال/إزالة/تراجع) |
| [04-studio-login-media.png](04-studio-login-media.png) | صورة صفحة الدخول: الملاءمة، الموضع (شبكة 3×3) ونقطة التركيز، التكبير، طبقة التغطية، التخطيط وموضع النص |
| [05-studio-colors-presets.png](05-studio-colors-presets.png) | الألوان والسمات: 8 سمات رسمية جاهزة ثم ضبط يدوي لكل لون |
| [06-studio-contrast-warning.png](06-studio-contrast-warning.png) | فحص الوضوح: تحذير عند اختيار تباين منخفض (نص الترويسة) |
| [07-studio-preview-app.png](07-studio-preview-app.png) | معاينة التطبيق: القائمة الجانبية (الأسماء متعددة الأسطر والشعار) والترويسة واللافتة والبطاقات والأزرار وشارات الحالة |
| [08-studio-preview-mobile.png](08-studio-preview-mobile.png) | معاينة صفحة الدخول على الجوال |
| [09-studio-preview-enlarged.png](09-studio-preview-enlarged.png) | المعاينة المكبّرة لصفحة الدخول قبل الحفظ |
| [10-login-navy.png](10-login-navy.png) | صفحة الدخول بسمة «الكحلي التنفيذي»: صورة بنقطة تركيز وتغطية 55%، شعار بدون خلفية، أسماء وعنوان على سطرين ونصوص مخصصة |
| [11-login-navy-mobile.png](11-login-navy-mobile.png) | صفحة الدخول على الجوال (390×844): اللوحة المرئية |
| [11b-login-navy-mobile-card.png](11b-login-navy-mobile-card.png) | صفحة الدخول على الجوال: بطاقة تسجيل الدخول ونص الدعم |
| [12-admin-sidebar-navy.png](12-admin-sidebar-navy.png) | لوحة الإدارة بالهوية الجديدة: اسمان على سطرين في القائمة الجانبية وشعار للخلفيات الداكنة |
| [13-studio-mauve-full.png](13-studio-mauve-full.png) | الاستوديو: سمة «الوردي الموفي الهادئ» مع صورة بملء الشاشة ونص في المنتصف وشعار داخل بطاقة الدخول |
| [14-login-mauve.png](14-login-mauve.png) | صفحة الدخول بسمة «الوردي الموفي الهادئ»: صورة بملء الشاشة، نص في المنتصف، الشعار داخل البطاقة |
| [15-employee-mauve.png](15-employee-mauve.png) | بوابة الموظف بسمة «الوردي الموفي الهادئ» |

## الفحوص (25/25 ناجحة، بدون أخطاء JavaScript)

- ✅ studio opens with "all changes saved"
- ✅ editing marks the studio as having unsaved changes
- ✅ preview shows the system name on two lines
- ✅ discard changes restores the saved values
- ✅ preview follows the login texts live (title on two lines, features, notice)
- ✅ logos: the chosen files render in the preview immediately (decoded under the site CSP) and are marked "applied when you save"
- ✅ image position preset is applied to the preview (100% 0%)
- ✅ focal point picked on the image (62%, 39%)
- ✅ preview applies the new image, zoom and overlay before saving
- ✅ preset "Executive Navy" fills every color and recolors the preview
- ✅ readability check warns about low header contrast
- ✅ revert theme restores the saved colors
- ✅ saved: success message, no unsaved changes
- ✅ saved images are shown as uploaded after saving
- ✅ login page: multi-line hero title and system name
- ✅ login page: custom badge, card title and support text
- ✅ login page: login logo without plate, favicon
- ✅ login page: background image with the chosen focal point (62% 39%)
- ✅ theme.css carries the Executive Navy colors
- ✅ login page on a phone: no horizontal overflow
- ✅ sidebar: multi-line names (no clipping), logo on dark background; page title on one line
- ✅ preview switches layout (full-screen), text placement (centered), decoration and logo placement
- ✅ login page: full-screen layout, centered text, logo on the sign-in card
- ✅ theme.css carries the Soft Mauve Rose colors
- ✅ reset to default: default layout, no image, no logo, default texts
