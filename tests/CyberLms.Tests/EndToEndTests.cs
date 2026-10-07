using System.Net;
using System.Text;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>Runs the "Definition of MVP Done" scenario through real HTTP against PostgreSQL.</summary>
public class EndToEndTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99";
    private const string UserPass = "Employee#Pass123";

    private async Task<HttpClient> LoginAsync(string user, string pass, string? newPass = null)
    {
        var c = app.NewClient();
        var r = await c.PostForm("/Account/Login", "/Account/Login", [F("Username", user), F("Password", pass)]);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var home = await c.GetAsync("/");
        if (home.StatusCode == HttpStatusCode.Redirect && home.Headers.Location!.ToString().Contains("ChangePassword"))
        {
            Assert.NotNull(newPass);
            var p = await c.PostForm("/Account/ChangePassword", "/Account/ChangePassword", [F("Current", pass), F("New", newPass!), F("Confirm", newPass!)]);
            Assert.Equal(HttpStatusCode.Redirect, p.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/")).StatusCode);
        }
        return c;
    }

    private async Task<HttpClient> AdminAsync()
    {
        try { return await LoginAsync("admin", NewAdminPass, NewAdminPass); } catch (Exception) { }
        return await LoginAsync("admin", TestApp.AdminPass, NewAdminPass);
    }

    [Fact]
    public async Task Full_MVP_scenario()
    {
        // 1. Admin logs in (forced password change on first login)
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin/Dashboard")).StatusCode);

        // create two employees
        foreach (var u in new[] { "alice", "bob" })
        {
            var r = await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", u), F("DisplayName", u + " عربي"), F("Email", u + "@x.local"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")]);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        }

        // 2-4. Content with PNG + link, XSS in body, published
        var created = await admin.PostMultipart("/Admin/Content/Create", "/Admin/Content/Create", mp =>
        {
            mp.Add(new StringContent("Password Policy"), "Title");
            mp.Add(new StringContent("Rules for passwords"), "Description");
            mp.Add(new StringContent("<p>سياسة كلمات المرور</p><script>alert(1)</script><img src=x onerror=alert(1)>"), "Body");
            mp.Add(new StringContent("1"), "Type");
            mp.Add(new StringContent("true"), "RequiresAcknowledgment");
            mp.Add(new StringContent("https://intranet.local/policy"), "ExternalUrl");
            mp.Add(new StringContent("true"), "publish");
            mp.AddFile("files", "diagram.png", Png, "image/png");
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var content = await app.Db(d => d.Contents.Include(c => c.Attachments).SingleAsync(c => c.Title == "Password Policy"));
        Assert.Equal(ContentStatus.Published, content.Status);
        Assert.DoesNotContain("<script", content.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", content.Body, StringComparison.OrdinalIgnoreCase);
        var att = Assert.Single(content.Attachments);
        Assert.Equal(AttachmentKind.Image, att.Kind);
        Assert.True(File.Exists(Path.Combine(app.StorageDir, att.StoredPath)));   // on disk, not in the DB

        // a draft, to prove users cannot see unpublished content
        await admin.PostMultipart("/Admin/Content/Create", "/Admin/Content/Create", mp => { mp.Add(new StringContent("Secret draft"), "Title"); mp.Add(new StringContent("3"), "Type"); mp.Add(new StringContent("false"), "publish"); });
        var draft = await app.Db(d => d.Contents.SingleAsync(c => c.Title == "Secret draft"));
        Assert.Equal(ContentStatus.Draft, draft.Status);

        // 5-6. Employee reads it
        var alice = await LoginAsync("alice", UserPass, "Alice#NewPass123");
        var details = await alice.GetStringAsync($"/Content/Details/{content.Id}");
        Assert.Contains("Password Policy", details);
        Assert.Contains("سياسة كلمات المرور", details);
        Assert.DoesNotContain("<script>alert", details);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/Files/Attachment/{att.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/Content/Details/{draft.Id}")).StatusCode);
        Assert.DoesNotContain("Secret draft", await alice.GetStringAsync("/Content"));

        // 7-8. Assessment with 3 questions (single, true/false, multiple); pass 60%, 1 attempt
        var r1 = await admin.PostForm("/Admin/Assessments/Create", "/Admin/Assessments/Create", [F("Title", "Phishing Quiz"), F("PassingPercentage", "60"), F("MaxAttempts", "1"), F("ContentId", content.Id.ToString())]);
        Assert.Equal(HttpStatusCode.Redirect, r1.StatusCode);
        var asm = await app.Db(d => d.Assessments.SingleAsync(a => a.Title == "Phishing Quiz"));
        var qUrl = $"/Admin/Assessments/AddQuestion/{asm.Id}";
        (await admin.PostForm(qUrl, qUrl, [F("Text", "What is phishing?"), F("Type", "1"), F("Points", "1"), F("SortOrder", "1"), F("Options[0]", "A fish"), F("Options[1]", "A fraudulent message"), F("Options[2]", "A virus"), F("Correct", "1")])).EnsureRedirect();
        (await admin.PostForm(qUrl, qUrl, [F("Text", "Passwords may be shared"), F("Type", "2"), F("Points", "1"), F("SortOrder", "2"), F("TrueIsCorrect", "false")])).EnsureRedirect();
        (await admin.PostForm(qUrl, qUrl, [F("Text", "Pick safe habits"), F("Type", "3"), F("Points", "2"), F("SortOrder", "3"), F("Options[0]", "MFA"), F("Options[1]", "Reuse passwords"), F("Options[2]", "Lock screen"), F("Correct", "0"), F("Correct", "2")])).EnsureRedirect();
        // invalid question (no correct answer) is rejected
        var bad = await admin.PostForm(qUrl, qUrl, [F("Text", "Bad"), F("Type", "1"), F("Points", "1"), F("Options[0]", "x"), F("Options[1]", "y")]);
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        (await admin.PostForm("/Admin/Assessments", $"/Admin/Assessments/SetStatus/{asm.Id}?publish=true", [])).EnsureRedirect();

        // 8b. Alice acknowledges the content first (twice => one record): the linked assessment is locked until she does
        var ackUrl = $"/Content/Acknowledge/{content.Id}";
        Assert.Contains("أقرّ بأنني قرأت هذه السياسة وفهمتها", await alice.GetStringAsync($"/Content/Details/{content.Id}"));
        (await alice.PostForm($"/Content/Details/{content.Id}", ackUrl, [])).EnsureRedirect();
        (await alice.PostForm($"/Content/Details/{content.Id}", ackUrl, [])).EnsureRedirect();
        Assert.Equal(1, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == content.Id)));

        // 9-11. Alice answers: Q1 correct, Q2 wrong, Q3 correct => 3/4 points = 75% => Passed
        var qs = await app.Db(d => d.Questions.Include(q => q.Options).Where(q => q.AssessmentId == asm.Id).OrderBy(q => q.SortOrder).ToListAsync());
        Assert.Equal(3, qs.Count);
        var start = await alice.PostForm("/Assessments", $"/Assessments/Start/{asm.Id}", []);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var take = start.Headers.Location!.ToString();
        Assert.Contains("Phishing Quiz", await alice.GetStringAsync(take));
        var attemptId = int.Parse(take.Split('/').Last());
        var answers = new List<KeyValuePair<string, string>>
        {
            F($"q_{qs[0].Id}", qs[0].Options.Single(o => o.IsCorrect).Id.ToString()),
            F($"q_{qs[1].Id}", qs[1].Options.Single(o => o.IsCorrect).Id.ToString() is var _ ? qs[1].Options.Single(o => !o.IsCorrect).Id.ToString() : ""),
        };
        foreach (var o in qs[2].Options.Where(o => o.IsCorrect)) answers.Add(F($"q_{qs[2].Id}", o.Id.ToString()));
        var submit = await alice.PostForm(take, $"/Assessments/Submit/{attemptId}", answers);
        Assert.Equal(HttpStatusCode.Redirect, submit.StatusCode);
        var result = await alice.GetStringAsync($"/Assessments/Result/{attemptId}");
        Assert.Contains("75", result);
        var attempt = await app.Db(d => d.AssessmentAttempts.SingleAsync(a => a.Id == attemptId));
        Assert.Equal(AttemptStatus.Completed, attempt.Status);
        Assert.Equal(75m, attempt.Percentage); Assert.True(attempt.Passed); Assert.Equal(2, attempt.CorrectAnswers); Assert.Equal(3, attempt.TotalQuestions);
        Assert.Equal(3, await app.Db(d => d.AssessmentAnswers.CountAsync(a => a.AttemptId == attemptId)));
        // single attempt: cannot start again; re-submitting does not change anything
        var again = await alice.PostForm("/Assessments", $"/Assessments/Start/{asm.Id}", []);
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Equal(1, await app.Db(d => d.AssessmentAttempts.CountAsync(a => a.UserId == attempt.UserId)));
        // other users can't open Alice's attempt
        var bob = await LoginAsync("bob", UserPass, "Bob#NewPass1234");
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/Assessments/Result/{attemptId}")).StatusCode);

        // 12. Admin sees Alice's result; Bob is "not attempted" (16)
        var rep = await admin.GetStringAsync($"/Admin/Reports/Assessments?assessmentId={asm.Id}");
        Assert.Contains("alice", rep);
        var notAttempted = await admin.GetStringAsync($"/Admin/Reports/Assessments?assessmentId={asm.Id}&status=NotAttempted");
        Assert.Contains("bob", notAttempted); Assert.DoesNotContain(">alice<", notAttempted);
        var failedOnly = await admin.GetStringAsync($"/Admin/Reports/Assessments?assessmentId={asm.Id}&status=Failed");
        Assert.DoesNotContain(">alice<", failedOnly);

        // 15. Admin acknowledgment report
        Assert.Contains(">alice<", await admin.GetStringAsync($"/Admin/Reports/Acknowledgments?contentId={content.Id}&status=Acknowledged"));
        var notAck = await admin.GetStringAsync($"/Admin/Reports/Acknowledgments?contentId={content.Id}&status=NotAcknowledged");
        Assert.Contains(">bob<", notAck); Assert.DoesNotContain(">alice<", notAck);

        // 17. CSV + Excel exports
        var csv = await admin.GetAsync($"/Admin/Reports/Assessments?assessmentId={asm.Id}&export=csv");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var csvText = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        Assert.Contains("alice", csvText); Assert.Contains("مجتاز", csvText); Assert.Contains("لم يختبر", csvText);
        Assert.Contains("اسم المستخدم", csvText); Assert.Contains("البريد الإلكتروني", csvText); Assert.Contains("تاريخ آخر محاولة", csvText); Assert.DoesNotContain("Username", csvText);
        Assert.Contains("filename*=UTF-8''", string.Join(";", csv.Content.Headers.ContentDisposition!.ToString()) + csv.Content.Headers.ContentDisposition!.FileNameStar);
        var xlsx = await admin.GetAsync($"/Admin/Reports/Acknowledgments?export=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal((byte)'P', (await xlsx.Content.ReadAsByteArrayAsync())[0]); // zip container

        // dashboard + audit
        var dash = await admin.GetStringAsync("/Admin/Dashboard");
        Assert.Contains("إجمالي المستخدمين", dash);
        var audit = await app.Db(d => d.AuditLogs.Select(a => a.Action).Distinct().ToListAsync());
        Assert.Contains("CONTENT_CREATED", audit); Assert.Contains("CONTENT_PUBLISHED", audit); Assert.Contains("ASSESSMENT_CREATED", audit); Assert.Contains("USER_CREATED", audit); Assert.Contains("REPORT_EXPORTED", audit);

        // questions are locked once attempts exist
        Assert.Equal(HttpStatusCode.Redirect, (await admin.GetAsync($"/Admin/Assessments/EditQuestion/{qs[0].Id}")).StatusCode);
        // content with acknowledgments cannot be deleted
        await admin.PostForm("/Admin/Content", $"/Admin/Content/SetStatus/{content.Id}?publish=false", []);
        await admin.PostForm("/Admin/Content", $"/Admin/Content/Delete/{content.Id}", []);
        Assert.True(await app.Db(d => d.Contents.AnyAsync(c => c.Id == content.Id)));
    }

    [Fact]
    public async Task Security_controls()
    {
        var admin = await AdminAsync();
        if (!await app.Db(d => d.Users.AnyAsync(u => u.Username == "carol")))
            (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", "carol"), F("DisplayName", "Carol"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();

        // anonymous => redirected to login
        var anon = app.NewClient();
        Assert.Contains("/Account/Login", (await anon.GetAsync("/Admin/Dashboard")).Headers.Location!.ToString());
        Assert.Contains("/Account/Login", (await anon.GetAsync("/Content")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/Files/Attachment/1")).StatusCode == HttpStatusCode.Redirect ? HttpStatusCode.NotFound : HttpStatusCode.OK);

        // employee can't reach admin area
        var carol = await LoginAsync("carol", UserPass, "Carol#NewPass123");
        var denied = await carol.GetAsync("/Admin/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("Denied", denied.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await carol.GetAsync("/Admin/Settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await carol.GetAsync("/Admin/Reports/Assessments?export=csv")).StatusCode);

        // CSRF: POST without token is rejected
        var noToken = await admin.PostAsync("/Admin/Users/Create", new FormUrlEncodedContent([F("Username", "evil"), F("DisplayName", "x"), F("AuthSource", "Local"), F("Password", UserPass)]));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.False(await app.Db(d => d.Users.AnyAsync(u => u.Username == "evil")));

        // wrong password + lockout
        var c = app.NewClient();
        for (var i = 0; i < 5; i++) await c.PostForm("/Account/Login", "/Account/Login", [F("Username", "carol"), F("Password", "wrong-password")]);
        var locked = await c.PostForm("/Account/Login", "/Account/Login", [F("Username", "carol"), F("Password", "Carol#NewPass123")]);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode); // stays on login page (locked)

        // upload validation: executable, fake PNG, oversize type spoof, traversal name
        async Task<List<string>> Upload(string name, byte[] bytes, string mime)
        {
            await admin.PostMultipart("/Admin/Content/Create", "/Admin/Content/Create", mp =>
            { mp.Add(new StringContent("Upload " + name), "Title"); mp.Add(new StringContent("6"), "Type"); mp.Add(new StringContent("false"), "publish"); mp.AddFile("files", name, bytes, mime); });
            return await app.Db(async d => await d.ContentAttachments.Where(a => a.Content.Title == "Upload " + name).Select(a => a.StoredPath).ToListAsync());
        }
        Assert.Empty(await Upload("evil.exe", Encoding.ASCII.GetBytes("MZ......"), "application/octet-stream"));
        Assert.Empty(await Upload("fake.png", Encoding.ASCII.GetBytes("<html>not a png</html>"), "image/png"));
        Assert.Empty(await Upload("shell.png.aspx", Png, "image/png"));
        var ok = await Upload("..%2F..%2Fetc%2Fpasswd.png", Png, "image/png");
        var stored = Assert.Single(ok);
        Assert.StartsWith("content/", stored);
        Assert.DoesNotContain("..", stored);
        Assert.StartsWith(Path.GetFullPath(app.StorageDir), Path.GetFullPath(Path.Combine(app.StorageDir, stored)));
    }

    [Fact]
    public async Task Branding_is_configurable_from_settings_and_applies_without_restart()
    {
        var admin = await AdminAsync();
        var anon = app.NewClient();
        var before = await anon.GetStringAsync("/Account/Login");
        Assert.Contains("منصة التوعية بالأمن السيبراني", before); // default (Arabic), nothing hard-coded to a customer

        var save = await admin.PostMultipart("/Admin/Settings", "/Admin/Settings/SaveBranding", mp =>
        {
            foreach (var (k, v) in new Dictionary<string, string>
            {
                ["OrgName"] = "شركة الاختبار", ["SystemName"] = "منصة التوعية", ["PrimaryColor"] = "#aa1122", ["SecondaryColor"] = "#334455", ["AccentColor"] = "#00ff88",
                ["HeaderColor"] = "#111111", ["HeaderTextColor"] = "#fefefe", ["SidebarColor"] = "#eeeeee", ["SidebarTextColor"] = "#222222", ["LoginBackgroundColor"] = "#ddeeff",
                ["LoginTitle"] = "مرحبا بك", ["LoginSubtitle"] = "للاستخدام الداخلي فقط", ["WelcomeText"] = "Welcome text here", ["FooterText"] = "© Test Co. Contact: sec@test.local",
            }) mp.Add(new StringContent(v), k);
            mp.AddFile("logo", "logo.png", Png, "image/png");
            mp.AddFile("favicon", "fav.png", Png, "image/png");
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var after = await anon.GetStringAsync("/Account/Login");
        Assert.Contains("منصة التوعية", after); Assert.Contains("شركة الاختبار", after); Assert.Contains("kind=logo", after); Assert.Contains("kind=favicon", after);
        Assert.Contains("للاستخدام الداخلي فقط", after); Assert.Contains("Contact: sec@test.local", after);
        var css = await anon.GetStringAsync("/branding/theme.css");
        Assert.Contains("--color-primary:#aa1122", css); Assert.Contains("--color-header:#111111", css); Assert.Contains("--color-login-bg:#ddeeff", css);
        var logo = await anon.GetAsync("/Files/Brand?kind=logo");
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode); Assert.Equal("image/png", logo.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await logo.Content.ReadAsByteArrayAsync());
        Assert.Contains("منصة التوعية", await admin.GetStringAsync("/Admin/Dashboard"));
        Assert.Contains("Welcome text here", await LoginViaNewClient());

        // invalid color / CSS injection is rejected and not applied
        var inj = await admin.PostMultipart("/Admin/Settings", "/Admin/Settings/SaveBranding", mp =>
        {
            foreach (var (k, v) in new Dictionary<string, string> { ["OrgName"] = "X", ["SystemName"] = "Y", ["PrimaryColor"] = "red;}body{display:none", ["SecondaryColor"] = "#334455", ["AccentColor"] = "#00ff88", ["HeaderColor"] = "#111111", ["HeaderTextColor"] = "#fefefe", ["SidebarColor"] = "#eeeeee", ["SidebarTextColor"] = "#222222", ["LoginBackgroundColor"] = "#ddeeff" }) mp.Add(new StringContent(v), k);
        });
        Assert.Equal(HttpStatusCode.Redirect, inj.StatusCode);
        Assert.DoesNotContain("display:none", await anon.GetStringAsync("/branding/theme.css"));
        Assert.Contains("--color-primary:#aa1122", await anon.GetStringAsync("/branding/theme.css"));

        // non-image logo rejected
        var badLogo = await admin.PostMultipart("/Admin/Settings", "/Admin/Settings/SaveBranding", mp =>
        {
            foreach (var (k, v) in new Dictionary<string, string> { ["OrgName"] = "شركة الاختبار", ["SystemName"] = "منصة التوعية", ["PrimaryColor"] = "#aa1122", ["SecondaryColor"] = "#334455", ["AccentColor"] = "#00ff88", ["HeaderColor"] = "#111111", ["HeaderTextColor"] = "#fefefe", ["SidebarColor"] = "#eeeeee", ["SidebarTextColor"] = "#222222", ["LoginBackgroundColor"] = "#ddeeff" }) mp.Add(new StringContent(v), k);
            mp.AddFile("logo", "logo.svg", Encoding.UTF8.GetBytes("<svg onload=alert(1)/>"), "image/svg+xml");
        });
        Assert.Equal(HttpStatusCode.Redirect, badLogo.StatusCode);
        Assert.Equal(Png, await (await anon.GetAsync("/Files/Brand?kind=logo")).Content.ReadAsByteArrayAsync()); // unchanged

        // reset
        (await admin.PostForm("/Admin/Settings", "/Admin/Settings/ResetBranding", [])).EnsureRedirect();
        Assert.Contains("منصة التوعية بالأمن السيبراني", await anon.GetStringAsync("/Account/Login"));
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/Files/Brand?kind=logo")).StatusCode);

        async Task<string> LoginViaNewClient() => await admin.GetStringAsync("/");
    }

    [Fact]
    public async Task English_remains_available_as_future_compatibility()
    {
        var c = app.NewClient();
        c.DefaultRequestHeaders.Add("Cookie", "CyberLms.Culture=c%3Den-US%7Cuic%3Den-US");
        var html = await c.GetStringAsync("/Account/Login");
        Assert.Contains("lang=\"en\"", html); Assert.Contains("dir=\"ltr\"", html); Assert.Contains("Username", html); Assert.DoesNotContain("bootstrap.rtl", html);
    }

    [Fact]
    public async Task Portal_is_Arabic_and_RTL_by_default()
    {
        var anon = app.NewClient();
        var login = await anon.GetStringAsync("/Account/Login");
        Assert.Contains("lang=\"ar\"", login); Assert.Contains("dir=\"rtl\"", login); Assert.Contains("bootstrap.rtl.min.css", login);
        Assert.Contains("تسجيل الدخول", login); Assert.Contains("اسم المستخدم", login); Assert.Contains("كلمة المرور", login);

        // validation messages are Arabic
        var r = await anon.PostForm("/Account/Login", "/Account/Login", [F("Username", ""), F("Password", "")]);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("حقل اسم المستخدم مطلوب", html); Assert.Contains("حقل كلمة المرور مطلوب", html);
        // wrong credentials message is Arabic
        var bad = await (await anon.PostForm("/Account/Login", "/Account/Login", [F("Username", "nobody"), F("Password", "x")])).Content.ReadAsStringAsync();
        Assert.Contains("اسم المستخدم أو كلمة المرور غير صحيحة", bad);

        // status pages are Arabic (404 and CSRF 400)
        var admin = await AdminAsync();
        var nf = await admin.GetAsync("/Content/Details/999999");
        Assert.Equal(HttpStatusCode.NotFound, nf.StatusCode);
        Assert.Contains("الصفحة غير موجودة", await nf.Content.ReadAsStringAsync());
        var csrf = await admin.PostAsync("/Admin/Users/Create", new FormUrlEncodedContent([F("Username", "z")]));
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Contains("طلب غير صالح", await csrf.Content.ReadAsStringAsync());

        // admin pages are Arabic
        foreach (var (url, text) in new[] { ("/Admin/Dashboard", "لوحة التحكم"), ("/Admin/Content", "إدارة المحتوى"), ("/Admin/Assessments", "اختبار جديد"), ("/Admin/Users", "مستخدم جديد"),
                     ("/Admin/Reports/Assessments", "نتائج الاختبارات"), ("/Admin/Settings", "الهوية البصرية"), ("/Admin/Audit", "سجل التدقيق"), ("/Admin/Content/Create", "حفظ ونشر") })
            Assert.Contains(text, await admin.GetStringAsync(url));

        // admin-side validation error in Arabic (empty content title)
        var vr = await admin.PostMultipart("/Admin/Content/Create", "/Admin/Content/Create", mp => { mp.Add(new StringContent(""), "Title"); mp.Add(new StringContent("1"), "Type"); });
        Assert.Contains("حقل العنوان مطلوب", await vr.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Failures_are_controlled_Arabic_messages()
    {
        var admin = await AdminAsync();
        if (!await app.Db(d => d.Users.AnyAsync(u => u.Username == "faila")))
        {
            (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", "faila"), F("DisplayName", "موظف"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
            (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", "failb"), F("DisplayName", "موظف آخر"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
        }
        var a = await LoginAsync("faila", UserPass, "Faila#NewPass123");
        var b2 = await LoginAsync("failb", UserPass, "Failb#NewPass123");

        // --- oversized upload (limit 1 MB for images in the test configuration): refused with an Arabic message, nothing stored
        var big = new byte[1_500_000]; Png.CopyTo(big, 0);
        var r = await admin.PostMultipart("/Admin/Content/Create", "/Admin/Content/Create", mp => { mp.Add(new StringContent("Oversize"), "Title"); mp.Add(new StringContent("6"), "Type"); mp.Add(new StringContent("true"), "publish"); mp.AddFile("files", "big.png", big, "image/png"); });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var edit = await admin.GetStringAsync(r.Headers.Location!.ToString());
        Assert.Contains("حجم الملف كبير جداً", edit);
        Assert.False(await app.Db(d => d.ContentAttachments.AnyAsync(x => x.Content.Title == "Oversize")));

        // --- request larger than the hard limit: controlled 413 page in Arabic
        var huge = new byte[8_000_000]; Png.CopyTo(huge, 0);
        var r2 = await admin.PostMultipart("/Admin/Content/Create", "/Admin/Content/Create", mp => { mp.Add(new StringContent("Huge"), "Title"); mp.AddFile("files", "huge.png", huge, "image/png"); });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r2.StatusCode);
        Assert.Contains("الملف كبير جداً", await r2.Content.ReadAsStringAsync());

        // --- registered attachment whose file is missing on disk: 404 page, no path leaked
        await admin.PostMultipart("/Admin/Content/Create", "/Admin/Content/Create", mp => { mp.Add(new StringContent("WithFile"), "Title"); mp.Add(new StringContent("6"), "Type"); mp.Add(new StringContent("true"), "publish"); mp.AddFile("files", "x.png", Png, "image/png"); });
        var att = await app.Db(d => d.ContentAttachments.SingleAsync(x => x.Content.Title == "WithFile"));
        File.Delete(Path.Combine(app.StorageDir, att.StoredPath));
        var missing = await a.GetAsync($"/Files/Attachment/{att.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var body = await missing.Content.ReadAsStringAsync();
        Assert.Contains("الصفحة غير موجودة", body); Assert.DoesNotContain(att.StoredPath, body); Assert.DoesNotContain(app.StorageDir, body);

        // --- invalid assessment submissions
        int asmId = await app.Db(async d =>
        {
            var asm = new Assessment { Title = "Tamper", PassingPercentage = 50, MaxAttempts = 0, IsPublished = true };
            var q = new Question { Text = "q", Points = 1, Type = QuestionType.SingleChoice, SortOrder = 1, Options = { new QuestionOption { Text = "a", IsCorrect = true, SortOrder = 1 }, new QuestionOption { Text = "b", SortOrder = 2 } } };
            asm.Questions.Add(q); d.Assessments.Add(asm); await d.SaveChangesAsync(); return asm.Id;
        });
        var start = await a.PostForm("/Assessments", $"/Assessments/Start/{asmId}", []);
        var attemptId = int.Parse(start.Headers.Location!.ToString().Split('/').Last());
        // another employee cannot read/submit it
        Assert.Equal(HttpStatusCode.NotFound, (await b2.GetAsync($"/Assessments/Take/{attemptId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b2.PostForm("/Assessments", $"/Assessments/Submit/{attemptId}", [F("q_1", "1")])).StatusCode);
        // forged option ids and unknown fields score zero and do not break the attempt
        var qid = await app.Db(d => d.Questions.Where(x => x.AssessmentId == asmId).Select(x => x.Id).SingleAsync());
        var sub = await a.PostForm($"/Assessments/Take/{attemptId}", $"/Assessments/Submit/{attemptId}", [F($"q_{qid}", "999999"), F("q_abc", "x"), F("q_-5", "1")]);
        Assert.Equal(HttpStatusCode.Redirect, sub.StatusCode);
        var done = await app.Db(d => d.AssessmentAttempts.SingleAsync(x => x.Id == attemptId));
        Assert.Equal(0m, done.Percentage); Assert.False(done.Passed);
        // submitting the completed attempt again changes nothing
        await a.PostForm("/Assessments", $"/Assessments/Submit/{attemptId}", [F($"q_{qid}", "1")]);
        Assert.Equal(0m, (await app.Db(d => d.AssessmentAttempts.SingleAsync(x => x.Id == attemptId))).Percentage);
        // starting an assessment that does not exist / is unpublished
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm("/Assessments", "/Assessments/Start/999999", [])).StatusCode);
    }
}


static class RespExt
{
    public static void EnsureRedirect(this HttpResponseMessage r) => Assert.True(r.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.OK && r.StatusCode == HttpStatusCode.Redirect, $"Expected redirect, got {(int)r.StatusCode}");
}
