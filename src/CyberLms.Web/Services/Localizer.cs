namespace CyberLms.Web.Services;

/// <summary>Minimal UI localisation: English text is the key; Arabic is looked up, falling back to English.</summary>
public class Localizer(IHttpContextAccessor http, SettingsService settings)
{
    public const string Cookie = "lang";
    public string Lang
    {
        get
        {
            var c = http.HttpContext?.Request.Cookies[Cookie];
            return c is "ar" or "en" ? c : settings.Get("General.DefaultLanguage", "en")!;
        }
    }
    public bool IsRtl => Lang == "ar";
    public string Dir => IsRtl ? "rtl" : "ltr";

    public string this[string key] => IsRtl && Ar.TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> Ar = new()
    {
        ["Dashboard"] = "لوحة التحكم", ["Content"] = "المحتوى", ["Policies"] = "السياسات", ["Assessments"] = "الاختبارات",
        ["My Results"] = "نتائجي", ["Sign in"] = "تسجيل الدخول", ["Sign out"] = "تسجيل الخروج", ["Username"] = "اسم المستخدم",
        ["Password"] = "كلمة المرور", ["Sign in with Windows account"] = "الدخول بحساب Windows", ["Admin"] = "الإدارة",
        ["Welcome"] = "مرحباً", ["Available content"] = "المحتوى المتاح", ["Required acknowledgments"] = "إقرارات مطلوبة",
        ["Available assessments"] = "اختبارات متاحة", ["Completed assessments"] = "اختبارات مكتملة", ["Score"] = "الدرجة",
        ["Result"] = "النتيجة", ["Passed"] = "ناجح", ["Failed"] = "راسب", ["Not attempted"] = "لم يُجرَ", ["Completed"] = "اكتمل",
        ["Title"] = "العنوان", ["Type"] = "النوع", ["Status"] = "الحالة", ["Date"] = "التاريخ", ["View"] = "عرض", ["Open"] = "فتح",
        ["Start"] = "ابدأ", ["Start assessment"] = "ابدأ الاختبار", ["Submit"] = "إرسال", ["Submit answers"] = "إرسال الإجابات",
        ["Acknowledge"] = "إقرار", ["Acknowledged"] = "تم الإقرار", ["Not acknowledged"] = "لم يتم الإقرار",
        ["I acknowledge that I have read and understood this policy."] = "أقرّ بأنني قرأت هذه السياسة وفهمتها.",
        ["Attachments"] = "المرفقات", ["Related link"] = "رابط ذو صلة", ["Related assessment"] = "اختبار مرتبط", ["Back"] = "رجوع",
        ["Questions"] = "الأسئلة", ["Passing score"] = "درجة النجاح", ["Attempts"] = "المحاولات", ["Unlimited"] = "غير محدودة",
        ["Percentage"] = "النسبة", ["Correct answers"] = "الإجابات الصحيحة", ["Nothing here yet."] = "لا يوجد شيء بعد.",
        ["Language"] = "اللغة", ["Change password"] = "تغيير كلمة المرور", ["Current password"] = "كلمة المرور الحالية",
        ["New password"] = "كلمة المرور الجديدة", ["Confirm new password"] = "تأكيد كلمة المرور", ["Save"] = "حفظ",
        ["Pending"] = "قيد الانتظار", ["Search"] = "بحث", ["All"] = "الكل", ["Select one answer"] = "اختر إجابة واحدة",
        ["Select all that apply"] = "اختر كل ما ينطبق", ["True"] = "صحيح", ["False"] = "خطأ", ["Retry"] = "إعادة المحاولة",
        ["Your result"] = "نتيجتك", ["Question"] = "السؤال", ["Your answer"] = "إجابتك", ["Correct"] = "صحيح", ["Incorrect"] = "خاطئ",
        ["Acknowledged on"] = "تاريخ الإقرار", ["Maximum attempts reached"] = "تم استنفاد المحاولات", ["Continue"] = "متابعة",
        ["Pending acknowledgments"] = "إقرارات معلّقة", ["Completion"] = "الإنجاز", ["Started"] = "بدأ", ["Policy"] = "سياسة",
        ["Cybersecurity Control"] = "ضابط أمن سيبراني", ["Awareness Content"] = "محتوى توعوي", ["Training Content"] = "محتوى تدريبي",
        ["Procedure / Instruction"] = "إجراء / تعليمات", ["General Content"] = "محتوى عام", ["Published"] = "منشور",
        ["Account"] = "الحساب", ["Page"] = "صفحة", ["Previous"] = "السابق", ["Next"] = "التالي",
        ["Invalid username or password."] = "اسم المستخدم أو كلمة المرور غير صحيحة.",
    };
}
