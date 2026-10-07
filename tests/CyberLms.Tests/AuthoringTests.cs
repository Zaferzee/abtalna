using System.Net;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>
/// The "create learning material" wizard end to end over HTTP: content + table + attachment + acknowledgment + assessment with questions,
/// preview, publish, employee experience, locking after attempts, copy and unpublish (history preserved).
/// </summary>
public class AuthoringTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99";
    private const string UserPass = "Employee#Pass123";
    private static readonly byte[] Pdf = "%PDF-1.4\n%test\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n"u8.ToArray();

    private async Task<HttpClient> LoginAsync(string user, string pass, string? newPass = null)
    {
        var c = app.NewClient();
        (await c.PostForm("/Account/Login", "/Account/Login", [F("Username", user), F("Password", pass)])).EnsureRedirect();
        var home = await c.GetAsync("/");
        if (home.StatusCode == HttpStatusCode.Redirect && home.Headers.Location!.ToString().Contains("ChangePassword"))
            (await c.PostForm("/Account/ChangePassword", "/Account/ChangePassword", [F("Current", pass), F("New", newPass!), F("Confirm", newPass!)])).EnsureRedirect();
        return c;
    }

    private async Task<HttpClient> AdminAsync()
    {
        try { return await LoginAsync("admin", NewAdminPass, NewAdminPass); } catch (Exception) { }
        return await LoginAsync("admin", TestApp.AdminPass, NewAdminPass);
    }

    private static string Loc(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); return r.Headers.Location!.ToString(); }

    [Fact]
    public async Task Wizard_creates_publishes_and_protects_history()
    {
        var admin = await AdminAsync();
        var before = await app.Db(d => d.Contents.CountAsync());
        (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", "emp1"), F("DisplayName", "موظف"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();

        // Step 1: basics (invalid, then valid). Nothing is created by an invalid submission.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", ""), F("Type", "7"), F("go", "next")])).StatusCode);
        Assert.Equal(before, await app.Db(d => d.Contents.CountAsync()));
        var next = Loc(await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics",
            [F("Title", "لائحة المخالفات الداخلية للأمن السيبراني"), F("Type", "7"), F("Description", "مخالفات أمن المعلومات وجزاءاتها"), F("go", "next")]));
        var c = await app.Db(d => d.Contents.SingleAsync(x => x.Title == "لائحة المخالفات الداخلية للأمن السيبراني"));
        Assert.Equal(ContentType.Regulation, c.Type);
        Assert.Equal(ContentStatus.Draft, c.Status);
        Assert.EndsWith($"/Admin/Authoring/Write/{c.Id}", next);
        var id = c.Id;

        // Step 2: rich text with a table (sanitized) + bad link rejected + PDF attachment
        var body = "<h2>المادة الأولى</h2><ol><li>بند</li></ol><table><tbody><tr><td data-row=\"r1\">المخالفة</td><td>الجزاء</td></tr><tr><td>مشاركة كلمة المرور</td><td>إنذار</td></tr></tbody></table>"
                 + "<script>alert(1)</script><p onclick=\"x()\" style=\"color:red\">نص</p><img src=\"https://evil/x.png\">";
        var bad = await admin.PostMultipart($"/Admin/Authoring/Write/{id}", $"/Admin/Authoring/Write/{id}", mp => { mp.Add(new StringContent(body), "Body"); mp.Add(new StringContent("javascript:alert(1)"), "ExternalUrl"); mp.Add(new StringContent("next"), "go"); });
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Null((await app.Db(d => d.Contents.SingleAsync(x => x.Id == id))).Body);
        Assert.EndsWith($"/Admin/Authoring/Acknowledgment/{id}", Loc(await admin.PostMultipart($"/Admin/Authoring/Write/{id}", $"/Admin/Authoring/Write/{id}", mp =>
        {
            mp.Add(new StringContent(body), "Body"); mp.Add(new StringContent("https://intranet.local/regulation"), "ExternalUrl"); mp.Add(new StringContent("next"), "go");
            mp.AddFile("files", "لائحة.pdf", Pdf, "application/pdf");
        })));
        c = await app.Db(d => d.Contents.Include(x => x.Attachments).SingleAsync(x => x.Id == id));
        Assert.Contains("<table>", c.Body); Assert.Contains("<td>مشاركة كلمة المرور</td>", c.Body);
        Assert.DoesNotContain("<script", c.Body); Assert.DoesNotContain("onclick", c.Body); Assert.DoesNotContain("evil", c.Body);
        var att = Assert.Single(c.Attachments);
        Assert.Equal(AttachmentKind.Document, att.Kind);
        Assert.True(File.Exists(Path.Combine(app.StorageDir, att.StoredPath)));

        // Step 3: acknowledgment with a custom statement
        const string statement = "أقر بأنني قرأت وفهمت لائحة المخالفات الداخلية للأمن السيبراني.";
        Loc(await admin.PostForm($"/Admin/Authoring/Acknowledgment/{id}", $"/Admin/Authoring/Acknowledgment/{id}", [F("RequiresAcknowledgment", "true"), F("Statement", statement), F("go", "next")]));
        c = await app.Db(d => d.Contents.SingleAsync(x => x.Id == id));
        Assert.True(c.RequiresAcknowledgment); Assert.Equal(statement, c.AcknowledgmentText);

        // Step 4: assessment + questions, without leaving the wizard
        var aUrl = $"/Admin/Authoring/Assessment/{id}";
        Assert.Contains("add=1", Loc(await admin.PostForm(aUrl, $"/Admin/Authoring/AssessmentSettings/{id}",
            [F("include", "true"), F("Settings.Title", "اختبار لائحة المخالفات"), F("Settings.PassingPercentage", "60"), F("Settings.MaxAttempts", "2"), F("go", "stay")])));
        var asm = await app.Db(d => d.Assessments.SingleAsync(a => a.ContentId == id));
        Assert.Equal(id, asm.ContentId); Assert.False(asm.IsPublished); Assert.Equal(60, asm.PassingPercentage);
        var save = $"/Admin/Authoring/SaveQuestion/{id}";
        Loc(await admin.PostForm(aUrl, save, [F("Question.Text", "ما جزاء مشاركة كلمة المرور؟"), F("Question.Type", "1"), F("Question.Points", "1"), F("Question.Options[0]", "إنذار"), F("Question.Options[1]", "لا شيء"), F("Question.Correct", "0")]));
        Loc(await admin.PostForm(aUrl, save, [F("Question.Text", "يجوز مشاركة كلمة المرور."), F("Question.Type", "2"), F("Question.Points", "1"), F("Question.TrueIsCorrect", "false")]));
        Loc(await admin.PostForm(aUrl, save, [F("Question.Text", "اختر الممارسات الآمنة"), F("Question.Type", "3"), F("Question.Points", "2"), F("Question.Options[0]", "المصادقة متعددة العوامل"), F("Question.Options[1]", "كلمة مرور واحدة لكل شيء"), F("Question.Options[2]", "قفل الشاشة"), F("Question.Correct", "0"), F("Question.Correct", "2")]));
        // invalid (no correct answer) stays on the page with the editor open and saves nothing
        var inv = await admin.PostForm(aUrl, save, [F("Question.Text", "سؤال ناقص"), F("Question.Type", "1"), F("Question.Points", "1"), F("Question.Options[0]", "أ"), F("Question.Options[1]", "ب")]);
        Assert.Equal(HttpStatusCode.OK, inv.StatusCode);
        Assert.Contains("data-qeditor", await inv.Content.ReadAsStringAsync());
        var qs = await app.Db(d => d.Questions.Where(q => q.AssessmentId == asm.Id).OrderBy(q => q.SortOrder).ToListAsync());
        Assert.Equal(3, qs.Count);
        Assert.Equal([1, 2, 3], qs.Select(q => q.SortOrder));
        // reorder: move the third question up
        Loc(await admin.PostForm(aUrl, $"/Admin/Authoring/MoveQuestion/{id}?questionId={qs[2].Id}&dir=-1", []));
        var order = await app.Db(d => d.Questions.Where(q => q.AssessmentId == asm.Id).OrderBy(q => q.SortOrder).Select(q => q.Id).ToListAsync());
        Assert.Equal([qs[0].Id, qs[2].Id, qs[1].Id], order);
        // edit in place (no duplicate is created)
        Loc(await admin.PostForm(aUrl, save, [F("Question.Id", qs[0].Id.ToString()), F("Question.Text", "ما الجزاء المقرر لمشاركة كلمة المرور؟"), F("Question.Type", "1"), F("Question.Points", "1"), F("Question.Options[0]", "إنذار كتابي"), F("Question.Options[1]", "لا شيء"), F("Question.Correct", "0")]));
        Assert.Equal(3, await app.Db(d => d.Questions.CountAsync(q => q.AssessmentId == asm.Id)));
        Assert.Equal("ما الجزاء المقرر لمشاركة كلمة المرور؟", (await app.Db(d => d.Questions.SingleAsync(q => q.Id == qs[0].Id))).Text);
        // delete + add again
        Loc(await admin.PostForm(aUrl, $"/Admin/Authoring/DeleteQuestion/{id}?questionId={qs[1].Id}", []));
        Loc(await admin.PostForm(aUrl, save, [F("Question.Text", "يجوز مشاركة كلمة المرور مع المدير."), F("Question.Type", "2"), F("Question.Points", "1"), F("Question.TrueIsCorrect", "false")]));
        Assert.Equal(3, await app.Db(d => d.Questions.CountAsync(q => q.AssessmentId == asm.Id)));

        // Step 5: preview shows the employee components, statement and assessment, inactive
        var preview = await admin.GetStringAsync($"/Admin/Authoring/Preview/{id}");
        Assert.Contains(statement, preview); Assert.Contains("اختبار لائحة المخالفات", preview); Assert.Contains("<table>", preview);
        Assert.DoesNotContain("/Content/Acknowledge/", preview); Assert.DoesNotContain("/Assessments/Start/", preview);

        // Step 6: publish both
        Assert.Contains("نشر الآن", await admin.GetStringAsync($"/Admin/Authoring/Publish/{id}"));
        Loc(await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")]));
        Assert.Equal(ContentStatus.Published, (await app.Db(d => d.Contents.SingleAsync(x => x.Id == id))).Status);
        Assert.True((await app.Db(d => d.Assessments.SingleAsync(x => x.Id == asm.Id))).IsPublished);

        // Employee: content -> attachment -> acknowledgment -> assessment
        var emp = await LoginAsync("emp1", UserPass, "Emp#NewPass1234");
        Assert.Contains("لائحة المخالفات الداخلية", await emp.GetStringAsync("/Content/Policies"));   // regulations are listed with policies
        var page = await emp.GetStringAsync($"/Content/Details/{id}");
        Assert.Contains(statement, page); Assert.Contains("<table>", page); Assert.Contains($"/Files/Attachment/{att.Id}", page);
        Assert.DoesNotContain($"/Assessments/Start/{asm.Id}", page);                         // locked until acknowledged
        Assert.Contains("الاختبار مقفل", page);
        Assert.Equal(HttpStatusCode.OK, (await emp.GetAsync($"/Files/Attachment/{att.Id}")).StatusCode);
        Loc(await emp.PostForm($"/Content/Details/{id}", $"/Content/Acknowledge/{id}", []));
        Assert.Contains($"/Assessments/Start/{asm.Id}", await emp.GetStringAsync($"/Content/Details/{id}"));
        Assert.Equal(1, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == id)));
        var take = Loc(await emp.PostForm($"/Content/Details/{id}", $"/Assessments/Start/{asm.Id}", []));
        var attemptId = int.Parse(take.Split('/').Last());
        var questions = await app.Db(d => d.Questions.Include(q => q.Options).Where(q => q.AssessmentId == asm.Id).ToListAsync());
        var answers = questions.SelectMany(q => q.Options.Where(o => o.IsCorrect).Select(o => F($"q_{q.Id}", o.Id.ToString()))).ToList();
        Loc(await emp.PostForm(take, $"/Assessments/Submit/{attemptId}", answers));
        var attempt = await app.Db(d => d.AssessmentAttempts.SingleAsync(t => t.Id == attemptId));
        Assert.True(attempt.Passed); Assert.Equal(100m, attempt.Percentage);
        Assert.Contains("emp1", await admin.GetStringAsync($"/Admin/Reports/Assessments?assessmentId={asm.Id}&status=Passed"));

        // History is protected: questions locked, assessment cannot be removed, statement edits keep acknowledgments
        var qCount = await app.Db(d => d.Questions.CountAsync(q => q.AssessmentId == asm.Id));
        Loc(await admin.PostForm(aUrl, save, [F("Question.Text", "سؤال جديد"), F("Question.Type", "2"), F("Question.Points", "1"), F("Question.TrueIsCorrect", "true")]));
        Loc(await admin.PostForm(aUrl, $"/Admin/Authoring/DeleteQuestion/{id}?questionId={questions[0].Id}", []));
        Assert.Equal(qCount, await app.Db(d => d.Questions.CountAsync(q => q.AssessmentId == asm.Id)));
        Assert.Contains("bi-lock-fill", await admin.GetStringAsync(aUrl));
        Loc(await admin.PostForm(aUrl, $"/Admin/Authoring/AssessmentSettings/{id}", [F("include", "false"), F("go", "next")]));
        Assert.True(await app.Db(d => d.Assessments.AnyAsync(a => a.Id == asm.Id)));
        Loc(await admin.PostForm($"/Admin/Authoring/Acknowledgment/{id}", $"/Admin/Authoring/Acknowledgment/{id}", [F("RequiresAcknowledgment", "true"), F("Statement", statement + " (محدّث)"), F("go", "stay")]));
        Assert.Equal(1, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == id)));

        // Copy: a new draft with the text, a separate copy of the file, and an unpublished copy of the assessment; no acknowledgments or attempts
        var copyUrl = Loc(await admin.PostForm("/Admin/Content", $"/Admin/Authoring/Copy/{id}", []));
        var copyId = int.Parse(copyUrl.Split('/').Last());
        var copy = await app.Db(d => d.Contents.Include(x => x.Attachments).SingleAsync(x => x.Id == copyId));
        Assert.Equal(ContentStatus.Draft, copy.Status); Assert.Equal(c.Body, copy.Body); Assert.True(copy.RequiresAcknowledgment);
        var catt = Assert.Single(copy.Attachments);
        Assert.NotEqual(att.StoredPath, catt.StoredPath); Assert.True(File.Exists(Path.Combine(app.StorageDir, catt.StoredPath)));
        var casm = await app.Db(d => d.Assessments.Include(a => a.Questions).SingleAsync(a => a.ContentId == copyId));
        Assert.False(casm.IsPublished); Assert.Equal(qCount, casm.Questions.Count);
        Assert.Equal(0, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == copyId)));

        // Unpublish from the wizard: both withdrawn, results and acknowledgments kept
        Loc(await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "unpublish")]));
        Assert.Equal(ContentStatus.Draft, (await app.Db(d => d.Contents.SingleAsync(x => x.Id == id))).Status);
        Assert.False((await app.Db(d => d.Assessments.SingleAsync(x => x.Id == asm.Id))).IsPublished);
        Assert.Equal(1, await app.Db(d => d.AssessmentAttempts.CountAsync(t => t.AssessmentId == asm.Id)));
        Assert.Equal(1, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == id)));
        Assert.Equal(HttpStatusCode.NotFound, (await emp.GetAsync($"/Content/Details/{id}")).StatusCode);

        // Content Management shows the integrated item; audit trail recorded the steps
        var list = await admin.GetStringAsync("/Admin/Content");
        Assert.Contains("اختبار لائحة المخالفات", list); Assert.Contains($"/Admin/Authoring/Basics/{id}", list);
        var actions = await app.Db(d => d.AuditLogs.Select(a => a.Action).Distinct().ToListAsync());
        foreach (var a in new[] { "CONTENT_CREATED", "CONTENT_UPDATED", "ATTACHMENT_ADDED", "ASSESSMENT_CREATED", "QUESTION_ADDED", "QUESTION_UPDATED", "QUESTION_DELETED", "CONTENT_PUBLISHED", "ASSESSMENT_PUBLISHED", "CONTENT_UNPUBLISHED", "ASSESSMENT_UNPUBLISHED" })
            Assert.Contains(a, actions);
    }

    [Fact]
    public async Task Publish_is_blocked_while_the_assessment_has_no_questions()
    {
        var admin = await AdminAsync();
        var w = Loc(await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", "Empty quiz content"), F("Type", "6"), F("go", "next")]));
        var id = int.Parse(w.Split('/').Last());
        Loc(await admin.PostForm($"/Admin/Authoring/Assessment/{id}", $"/Admin/Authoring/AssessmentSettings/{id}", [F("include", "true"), F("Settings.Title", "Empty"), F("Settings.PassingPercentage", "70"), F("Settings.MaxAttempts", "1"), F("go", "stay")]));
        Loc(await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")]));
        Assert.Equal(ContentStatus.Draft, (await app.Db(d => d.Contents.SingleAsync(x => x.Id == id))).Status);
        // choosing "No assessment" removes the empty one (no attempts), then publishing works
        Loc(await admin.PostForm($"/Admin/Authoring/Assessment/{id}", $"/Admin/Authoring/AssessmentSettings/{id}", [F("include", "false"), F("go", "next")]));
        Assert.False(await app.Db(d => d.Assessments.AnyAsync(a => a.ContentId == id)));
        Loc(await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")]));
        Assert.Equal(ContentStatus.Published, (await app.Db(d => d.Contents.SingleAsync(x => x.Id == id))).Status);
    }
}
