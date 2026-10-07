using System.Net;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>
/// Mandatory acknowledgment gate: when content requires acknowledgment, its linked assessment stays locked for each user until that
/// user has acknowledged the content. Enforced on the server for starting an attempt, the question page and submitting answers.
/// </summary>
public class AckGateTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99";
    private const string UserPass = "Employee#Pass123";
    private const string DeniedMsg = "يجب الإقرار بالاطلاع على المحتوى قبل بدء الاختبار.";

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

    private async Task<(HttpClient Client, int UserId)> NewEmployee(HttpClient admin)
    {
        var name = "gate." + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", name), F("DisplayName", "موظف " + name), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
        var c = await LoginAsync(name, UserPass, "Gate#NewPass123");
        return (c, await app.Db(d => d.Users.Where(u => u.Username == name).Select(u => u.Id).SingleAsync()));
    }

    /// <summary>Content + linked assessment (one question) created and published through the authoring wizard.</summary>
    private async Task<(int ContentId, int AssessmentId)> Publish(HttpClient admin, bool requiresAck)
    {
        var w = (await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", "Gate " + Guid.NewGuid().ToString("N")[..6]), F("Type", "1"), F("go", "next")])).Headers.Location!.ToString();
        var id = int.Parse(w.Split('/').Last());
        (await admin.PostMultipart($"/Admin/Authoring/Write/{id}", $"/Admin/Authoring/Write/{id}", mp => { mp.Add(new StringContent("<p>نص السياسة.</p>"), "Body"); mp.Add(new StringContent("next"), "go"); })).EnsureRedirect();
        var ack = new List<KeyValuePair<string, string>> { F("go", "next") };
        if (requiresAck) ack.Add(F("RequiresAcknowledgment", "true"));
        (await admin.PostForm($"/Admin/Authoring/Acknowledgment/{id}", $"/Admin/Authoring/Acknowledgment/{id}", ack)).EnsureRedirect();
        var aUrl = $"/Admin/Authoring/Assessment/{id}";
        (await admin.PostForm(aUrl, $"/Admin/Authoring/AssessmentSettings/{id}", [F("include", "true"), F("Settings.Title", "اختبار البوابة"), F("Settings.PassingPercentage", "50"), F("Settings.MaxAttempts", "0"), F("go", "stay")])).EnsureRedirect();
        (await admin.PostForm(aUrl, $"/Admin/Authoring/SaveQuestion/{id}", [F("Question.Text", "يجوز مشاركة كلمة المرور."), F("Question.Type", "2"), F("Question.Points", "1"), F("Question.TrueIsCorrect", "false")])).EnsureRedirect();
        (await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")])).EnsureRedirect();
        var c = await app.Db(d => d.Contents.SingleAsync(x => x.Id == id));
        Assert.Equal(ContentStatus.Published, c.Status); Assert.Equal(requiresAck, c.RequiresAcknowledgment);
        return (id, await app.Db(d => d.Assessments.Where(a => a.ContentId == id).Select(a => a.Id).SingleAsync()));
    }

    private Task<int> Attempts(int userId, int assessmentId) => app.Db(d => d.AssessmentAttempts.CountAsync(a => a.UserId == userId && a.AssessmentId == assessmentId));

    private static Task<HttpResponseMessage> Start(HttpClient c, int contentId, int assessmentId) => c.PostForm($"/Content/Details/{contentId}", $"/Assessments/Start/{assessmentId}", []);

    /// <summary>The request was refused and sent back to the content's acknowledgment, with the Arabic reason on the next page.</summary>
    private static async Task AssertSentToAcknowledgment(HttpClient c, HttpResponseMessage r, int contentId)
    {
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal($"/Content/Details/{contentId}#sec-ack", r.Headers.Location!.ToString());
        Assert.Contains(DeniedMsg, await c.GetStringAsync($"/Content/Details/{contentId}"));
    }

    [Fact]
    public async Task Without_required_acknowledgment_the_assessment_is_open()
    {
        var admin = await AdminAsync();
        var (cid, aid) = await Publish(admin, requiresAck: false);
        var (emp, uid) = await NewEmployee(admin);

        var page = await emp.GetStringAsync($"/Content/Details/{cid}");
        Assert.Contains($"/Assessments/Start/{aid}", page);
        Assert.DoesNotContain("الاختبار مقفل", page);
        var start = await Start(emp, cid, aid);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.Contains("/Assessments/Take/", start.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await emp.GetAsync(start.Headers.Location!.ToString())).StatusCode);
        Assert.Equal(1, await Attempts(uid, aid));
    }

    [Fact]
    public async Task Required_acknowledgment_locks_the_assessment_on_every_path_until_the_user_acknowledges()
    {
        var admin = await AdminAsync();
        var (cid, aid) = await Publish(admin, requiresAck: true);
        var (emp, uid) = await NewEmployee(admin);
        var (other, otherId) = await NewEmployee(admin);

        // content page: locked state, no "start" action
        var page = await emp.GetStringAsync($"/Content/Details/{cid}");
        Assert.Contains("الاختبار مقفل", page);
        Assert.Contains("يتطلب إكمال الإقرار بالاطلاع أولاً", page);
        Assert.DoesNotContain($"/Assessments/Start/{aid}", page);
        Assert.DoesNotContain("ابدأ الاختبار", page);
        // assessments list and dashboard: locked, pointing to the content instead
        var list = await emp.GetStringAsync("/Assessments");
        Assert.DoesNotContain($"/Assessments/Start/{aid}", list);
        Assert.Contains($"/Content/Details/{cid}#sec-ack", list);
        Assert.Contains($"/Content/Details/{cid}#sec-ack", await emp.GetStringAsync("/"));

        // direct HTTP request to create an attempt: refused, nothing created
        await AssertSentToAcknowledgment(emp, await Start(emp, cid, aid), cid);
        Assert.Equal(0, await Attempts(uid, aid));

        // an attempt that already exists (e.g. created before the rule): question page and submission are refused too
        var legacy = await app.Db(async d =>
        {
            var t = new AssessmentAttempt { UserId = uid, AssessmentId = aid, PassingPercentageSnapshot = 50 };
            d.AssessmentAttempts.Add(t); await d.SaveChangesAsync(); return t.Id;
        });
        await AssertSentToAcknowledgment(emp, await emp.GetAsync($"/Assessments/Take/{legacy}"), cid);
        await AssertSentToAcknowledgment(emp, await emp.PostForm($"/Content/Details/{cid}", $"/Assessments/Submit/{legacy}", []), cid);
        Assert.Equal(AttemptStatus.InProgress, (await app.Db(d => d.AssessmentAttempts.SingleAsync(a => a.Id == legacy))).Status);

        // acknowledge -> success state, assessment unlocked
        var ack = await emp.PostForm($"/Content/Details/{cid}", $"/Content/Acknowledge/{cid}", []);
        Assert.Equal($"/Content/Details/{cid}#sec-ack", ack.Headers.Location!.ToString());
        var after = await emp.GetStringAsync($"/Content/Details/{cid}");
        Assert.Contains("تم الإقرار بنجاح", after);
        Assert.Contains("تم فتح الاختبار", after);
        Assert.Contains($"/Assessments/Start/{aid}", after);
        Assert.DoesNotContain("الاختبار مقفل", after);
        Assert.Equal(HttpStatusCode.OK, (await emp.GetAsync($"/Assessments/Take/{legacy}")).StatusCode);
        var start = await Start(emp, cid, aid);                                   // continues the in-progress attempt
        Assert.Equal($"/Assessments/Take/{legacy}", start.Headers.Location!.ToString());
        Assert.Contains($"/Assessments/Start/{aid}", await emp.GetStringAsync("/Assessments"));

        // already acknowledged: not asked again, and acknowledging again does not duplicate the record
        var later = await emp.GetStringAsync($"/Content/Details/{cid}");
        Assert.DoesNotContain($"/Content/Acknowledge/{cid}", later);
        Assert.DoesNotContain("تم الإقرار بنجاح", later);                         // the success message is shown once, right after acknowledging
        (await emp.PostForm($"/Content/Details/{cid}", $"/Content/Acknowledge/{cid}", [])).EnsureRedirect();
        Assert.Equal(1, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.UserId == uid && a.ContentId == cid)));
        Assert.Equal(HttpStatusCode.Redirect, (await Start(emp, cid, aid)).StatusCode);
        Assert.Equal(1, await Attempts(uid, aid));

        // one user's acknowledgment does not unlock the assessment for another user
        Assert.Contains("الاختبار مقفل", await other.GetStringAsync($"/Content/Details/{cid}"));
        await AssertSentToAcknowledgment(other, await Start(other, cid, aid), cid);
        Assert.Equal(0, await Attempts(otherId, aid));
        Assert.Equal(0, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.UserId == otherId)));
    }

    [Fact]
    public async Task Preview_shows_the_lock_and_the_simulated_unlock_without_recording_an_acknowledgment()
    {
        var admin = await AdminAsync();
        var (cid, aid) = await Publish(admin, requiresAck: true);
        var before = await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == cid));
        var preview = await admin.GetStringAsync($"/Admin/Authoring/Preview/{cid}");
        Assert.Contains("الاختبار مقفل", preview);
        Assert.Contains("data-ack-locked", preview);   // unlocked in the browser by the simulated acknowledgment
        Assert.Contains("data-ack-sim", preview);
        Assert.DoesNotContain("/Content/Acknowledge/", preview);
        Assert.DoesNotContain($"/Assessments/Start/{aid}", preview);
        Assert.Equal(before, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == cid)));
    }
}
