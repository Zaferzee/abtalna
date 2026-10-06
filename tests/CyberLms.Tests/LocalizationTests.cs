using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CyberLms.Web;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>Guards the Arabic-first requirement: every translatable key used in code/views has an Arabic translation.</summary>
public class LocalizationTests
{
    private static readonly string Web = FindWeb();
    private static string FindWeb()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "CyberLms.slnx"))) d = d.Parent;
        return Path.Combine(d!.FullName, "src", "CyberLms.Web");
    }

    private static Dictionary<string, string> Arabic() =>
        XDocument.Load(Path.Combine(Web, "Resources", "SharedResource.ar.resx")).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string)e.Element("value")!);

    private static string Unescape(string s) => s.Replace("\\\\", "\0").Replace("\\\"", "\"").Replace("\\'", "'").Replace("\0", "\\");
    private static readonly Regex Lit = new("\"((?:[^\"\\\\]|\\\\.)*)\"");

    private static IEnumerable<string> BracketLiterals(string text, string opener)
    {
        var i = 0;
        while ((i = text.IndexOf(opener, i, StringComparison.Ordinal)) >= 0)
        {
            var j = i + opener.Length; var depth = 1; var start = j; var inStr = false;
            while (j < text.Length && depth > 0)
            {
                var c = text[j];
                if (inStr) { if (c == '\\') j++; else if (c == '"') inStr = false; }
                else if (c == '"') inStr = true; else if (c == '[') depth++; else if (c == ']') depth--;
                j++;
            }
            var expr = text[start..(j - 1)];
            if (!expr.TrimStart().StartsWith("\"Entity: \"")) foreach (Match m in Lit.Matches(expr)) yield return m.Groups[1].Value;
            i = j;
        }
    }

    private static HashSet<string> UsedKeys()
    {
        var keys = new HashSet<string>();
        var files = Directory.EnumerateFiles(Web, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".cshtml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        foreach (var f in files)
        {
            var t = File.ReadAllText(f);
            foreach (var k in BracketLiterals(t, "L[")) keys.Add(Unescape(k));
            foreach (var k in BracketLiterals(t, "loc[")) keys.Add(Unescape(k));
            foreach (var pat in new[] { @"L\.Format\(\s*""((?:[^""\\]|\\.)*)""", @"(?:Success|Failure)\(\s*(?:[a-z]+\s*\?\s*)?""((?:[^""\\]|\\.)*)""", @"Res\.(?:Ar|Get)\(\s*""((?:[^""\\]|\\.)*)""",
                                        @"Display\(Name = ""((?:[^""\\]|\\.)*)""", @"new UploadError\(\s*""((?:[^""\\]|\\.)*)""", @"\bQ\(""((?:[^""\\]|\\.)*)""\)", @"\bH\(([^)]*)\)" })
                foreach (Match m in Regex.Matches(t, pat))
                    if (pat.StartsWith(@"\bH")) { foreach (Match l in Lit.Matches(m.Groups[1].Value)) keys.Add(Unescape(l.Groups[1].Value)); }
                    else keys.Add(Unescape(m.Groups[1].Value));
            // Literal arguments of Success/Failure/L.Format (e.g. ternaries). In Razor views the call never spans lines, so stay on one line there
            // (otherwise the scan would run on through the following HTML markup).
            foreach (Match m in Regex.Matches(t, f.EndsWith(".cshtml") ? @"(?:Success|Failure|L\.Format)\(([^;\n]*?)\)" : @"(?:Success|Failure|L\.Format)\(([^;]*?)\);"))
                foreach (Match l in Lit.Matches(m.Groups[1].Value)) if (l.Groups[1].Value.Contains(' ') && l.Groups[1].Value.Length > 3) keys.Add(Unescape(l.Groups[1].Value));
            foreach (Match m in Regex.Matches(t, @"notify\.Send\((.*)"))
                foreach (Match l in Lit.Matches(m.Groups[1].Value)) if (!l.Groups[1].Value.StartsWith('/') && l.Groups[1].Value.Contains(' ')) keys.Add(Unescape(l.Groups[1].Value));
            foreach (Match m in Regex.Matches(t, @"audit\.Add\(""([A-Z_]+)""")) keys.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(t, @"Action = ""([A-Z_]+)""")) keys.Add(m.Groups[1].Value);
            if (f.EndsWith("CultureSetup.cs")) foreach (Match m in Regex.Matches(t, @"=> ""([^""]+)""")) keys.Add(m.Groups[1].Value);
            if (f.EndsWith("Labels.cs")) foreach (Match m in Regex.Matches(t, @"""([A-Z][^""]*)""")) if (m.Groups[1].Value is not ("KB" or "MB" or "Windows")) keys.Add(m.Groups[1].Value);
        }
        foreach (var e in new[] { "Content", "Assessment", "User", "Settings", "Report", "Question" }) keys.Add("Entity: " + e);
        // enums rendered through Labels
        keys.RemoveWhere(k => !Regex.IsMatch(k, "[A-Za-z]") || k == "_");
        return keys;
    }

    [Fact]
    public void Every_user_facing_key_has_an_Arabic_translation()
    {
        var ar = Arabic();
        var missing = UsedKeys().Where(k => !ar.ContainsKey(k)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, "Missing Arabic translations:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Arabic_values_are_really_Arabic_and_keep_their_placeholders()
    {
        var bad = new List<string>();
        foreach (var (k, v) in Arabic())
        {
            if (k == "English") continue;
            if (!Regex.IsMatch(v, "[؀-ۿ]") && !Regex.IsMatch(k, "^(SMTP|CSV)")) bad.Add($"not Arabic: {k}");
            var kp = Regex.Matches(k, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToList();
            var vp = Regex.Matches(v, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToList();
            if (!kp.SequenceEqual(vp)) bad.Add($"placeholders differ: {k}");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void Label_helpers_cover_all_enum_values_in_Arabic()
    {
        var ar = Arabic();
        foreach (var t in Enum.GetValues<ContentType>()) Assert.True(ar.ContainsKey(Labels.Type(t)), Labels.Type(t));
        foreach (var s in new[] { ReportService.Passed, ReportService.Failed, ReportService.NotAttempted, ReportService.Acknowledged, ReportService.NotAcknowledged }) Assert.True(ar.ContainsKey(Labels.Status(s)), s);
        foreach (var t in Enum.GetValues<QuestionType>()) Assert.True(ar.ContainsKey(Labels.QuestionType(t)));
        Assert.Equal("مجتاز", ar["Passed"]); Assert.Equal("غير مجتاز", ar["Failed"]); Assert.Equal("لم يختبر", ar["Not attempted"]);
        Assert.Equal("تم الإقرار", ar["Acknowledged"]); Assert.Equal("لم يتم الإقرار", ar["Not acknowledged"]);
    }

    [Fact]
    public void Formatting_culture_uses_gregorian_dates_and_western_digits()
    {
        var c = CultureSetup.FormattingCulture(CultureInfo.GetCultureInfo("ar-SA"));
        Assert.Equal("06/10/2026", new DateTime(2026, 10, 6).ToString("dd/MM/yyyy", c));   // not Hijri
        Assert.Equal("75.5%", (75.5m).ToString("0.#", c) + "%");
        Assert.Equal("1,234", 1234.ToString("N0", c));
    }

    [Fact]
    public void Email_templates_are_Arabic_and_right_to_left()
    {
        var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:BaseUrl"] = "https://lms.local" }).Build();
        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        cache.Set("settings.all", (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>());
        var settings = new SettingsService(null!, cache); // pre-seeded cache: no database needed
        var notify = new NotificationService(new EmailQueue(), new SmtpConfigProvider(settings, cfg), settings, cfg);
        var job = notify.Compose([("a@x.local", "علي")], "Acknowledgment required: {0}", "Please read and acknowledge: {0}", ["سياسة كلمات المرور"], "/Content/Details/5").Single();
        Assert.Equal("مطلوب الإقرار: سياسة كلمات المرور", job.Subject);
        Assert.Contains("dir=\"rtl\"", job.HtmlBody);
        Assert.Contains("يرجى قراءة المحتوى التالي والإقرار عليه", job.HtmlBody);
        Assert.Contains("https://lms.local/Content/Details/5", job.HtmlBody);
        Assert.Contains("الأخ/الأخت علي", job.HtmlBody);
        Assert.DoesNotMatch("[A-Za-z]{4,}", System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(job.HtmlBody, "<[^>]+>", " "), "https?://\\S+", " ")));
    }
}
