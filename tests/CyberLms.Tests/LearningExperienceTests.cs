using System.Net;
using System.Text.RegularExpressions;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>
/// Learning experience v2 over real HTTP: the journey and status shown on the dashboard, content page and result pages follow
/// the existing records (acknowledgment, attempts). The business rules themselves are covered by AckGateTests and the others.
/// </summary>
public class LearningExperienceTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99";
    private const string UserPass = "Employee#Pass123";

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

    /// <summary>The journey on a page as "step:state" pairs (first full journey only).</summary>
    private static string Journey(string html)
    {
        var ol = Regex.Match(html, "<ol class=\"journey[^\"]*\"[\\s\\S]*?</ol>").Value;
        return string.Join(" ", Regex.Matches(ol, "class=\"j-step j-(\\w+)[^\"]*\" data-step=\"(\\w+)\"").Select(m => $"{m.Groups[2].Value}:{m.Groups[1].Value}"));
    }

    [Fact]
    public async Task Journey_and_result_states_follow_acknowledgment_and_attempts()
    {
        var admin = await AdminAsync();
        var title = "Journey " + Guid.NewGuid().ToString("N")[..6];
        var w = (await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", title), F("Type", "1"), F("go", "next")])).Headers.Location!.ToString();
        var id = int.Parse(w.Split('/').Last());
        (await admin.PostMultipart($"/Admin/Authoring/Write/{id}", $"/Admin/Authoring/Write/{id}", mp => { mp.Add(new StringContent("<p>نص.</p>"), "Body"); mp.Add(new StringContent("next"), "go"); })).EnsureRedirect();
        (await admin.PostForm($"/Admin/Authoring/Acknowledgment/{id}", $"/Admin/Authoring/Acknowledgment/{id}", [F("RequiresAcknowledgment", "true"), F("go", "next")])).EnsureRedirect();
        var aUrl = $"/Admin/Authoring/Assessment/{id}";
        (await admin.PostForm(aUrl, $"/Admin/Authoring/AssessmentSettings/{id}", [F("include", "true"), F("Settings.Title", "اختبار الرحلة"), F("Settings.PassingPercentage", "100"), F("Settings.MaxAttempts", "2"), F("go", "stay")])).EnsureRedirect();
        (await admin.PostForm(aUrl, $"/Admin/Authoring/SaveQuestion/{id}", [F("Question.Text", "يجوز مشاركة كلمة المرور."), F("Question.Type", "2"), F("Question.Points", "1"), F("Question.TrueIsCorrect", "false")])).EnsureRedirect();
        (await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")])).EnsureRedirect();
        var aid = await app.Db(d => d.Assessments.Where(a => a.ContentId == id).Select(a => a.Id).SingleAsync());
        var q = await app.Db(d => d.Questions.Include(x => x.Options).SingleAsync(x => x.AssessmentId == aid));

        var name = "lx." + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", name), F("DisplayName", "موظف"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
        var emp = await LoginAsync(name, UserPass, "Lx#NewPass1234");

        // dashboard: "what should I do now?", the item is required and links to the acknowledgment
        var dash = await emp.GetStringAsync("/");
        Assert.Contains("ما المطلوب مني الآن؟", dash);
        Assert.Contains("رحلتك في الوعي السيبراني", dash);
        Assert.Contains($"/Content/Details/{id}#sec-ack", dash);
        // content page before acknowledgment
        var page = await emp.GetStringAsync($"/Content/Details/{id}");
        Assert.Equal("content:current ack:upcoming assessment:locked done:upcoming", Journey(page));
        Assert.Contains("رحلتك في هذه المادة", page);
        // acknowledge -> journey moves on, assessment is current
        (await emp.PostForm($"/Content/Details/{id}", $"/Content/Acknowledge/{id}", [])).EnsureRedirect();
        page = await emp.GetStringAsync($"/Content/Details/{id}");
        Assert.Equal("content:done ack:done assessment:current done:upcoming", Journey(page));
        Assert.Contains("is-changing", page);                                       // the step change is animated once

        async Task<string> Attempt(bool correct)
        {
            var take = (await emp.PostForm($"/Content/Details/{id}", $"/Assessments/Start/{aid}", [])).Headers.Location!.ToString();
            var attempt = take.Split('/').Last();
            var opt = q.Options.Single(o => o.IsCorrect == correct).Id;
            (await emp.PostForm(take, $"/Assessments/Submit/{attempt}", [F($"q_{q.Id}", opt.ToString())])).EnsureRedirect();
            return await emp.GetStringAsync($"/Assessments/Result/{attempt}");
        }
        // fail: encouraging wording, review + retry, journey still on the assessment
        var fail = await Attempt(false);
        Assert.Contains("لم تحقق درجة الاجتياز هذه المرة", fail);
        Assert.Contains("مراجعة المحتوى", fail);
        Assert.Contains($"/Assessments/Start/{aid}", fail);
        Assert.DoesNotContain("تم إكمال هذه المادة بنجاح", fail);
        Assert.Contains("لم يُجتز بعد", await emp.GetStringAsync("/"));
        // pass: success + the learning item is completed by this attempt
        var pass = await Attempt(true);
        Assert.Contains("أحسنت، اجتزت الاختبار بنجاح", pass);
        Assert.Contains("تم إكمال هذه المادة بنجاح", pass);
        Assert.Contains("lx-complete is-new", pass);
        Assert.Equal("content:done ack:done assessment:done done:done", Journey(pass));
        // content page and dashboard after completion
        page = await emp.GetStringAsync($"/Content/Details/{id}");
        Assert.Contains("id=\"sec-complete\"", page);
        dash = await emp.GetStringAsync("/");
        var ach = dash[dash.IndexOf("lx-achievements", StringComparison.Ordinal)..];
        Assert.Contains(title, ach[..ach.IndexOf("</ul>", StringComparison.Ordinal)]);
        // no data was written by the UI beyond the existing records
        Assert.Equal(1, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.ContentId == id)));
        Assert.Equal(2, await app.Db(d => d.AssessmentAttempts.CountAsync(a => a.AssessmentId == aid && a.Status == AttemptStatus.Completed)));
    }

    private async Task<int> NewContent(HttpClient admin, string title, bool ack, bool publish = true)
    {
        var w = (await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", title), F("Type", "2"), F("go", "next")])).Headers.Location!.ToString();
        var id = int.Parse(w.Split('/').Last());
        (await admin.PostMultipart($"/Admin/Authoring/Write/{id}", $"/Admin/Authoring/Write/{id}", mp => { mp.Add(new StringContent("<p>محتوى توعوي.</p>"), "Body"); mp.Add(new StringContent("next"), "go"); })).EnsureRedirect();
        var a = new List<KeyValuePair<string, string>> { F("go", "next") };
        if (ack) a.Add(F("RequiresAcknowledgment", "true"));
        (await admin.PostForm($"/Admin/Authoring/Acknowledgment/{id}", $"/Admin/Authoring/Acknowledgment/{id}", a)).EnsureRedirect();
        if (publish) (await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")])).EnsureRedirect();
        return id;
    }

    [Fact]
    public async Task Reading_item_is_completed_only_by_the_explicit_server_side_action()
    {
        var admin = await AdminAsync();
        var title = "Reading " + Guid.NewGuid().ToString("N")[..6];
        var id = await NewContent(admin, title, ack: false);
        var ackId = await NewContent(admin, "Ack " + Guid.NewGuid().ToString("N")[..6], ack: true);
        var draftId = await NewContent(admin, "Draft " + Guid.NewGuid().ToString("N")[..6], ack: false, publish: false);
        var name = "rd." + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", name), F("DisplayName", "موظف"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
        var emp = await LoginAsync(name, UserPass, "Rd#NewPass1234");
        var uid = await app.Db(d => d.Users.Where(u => u.Username == name).Select(u => u.Id).SingleAsync());
        Task<int> Reads(int cid) => app.Db(d => d.ContentCompletions.CountAsync(r => r.UserId == uid && r.ContentId == cid));

        // opening the page does not complete it; the explicit action is offered
        var page = await emp.GetStringAsync($"/Content/Details/{id}");
        Assert.Contains($"/Content/Complete/{id}", page);
        Assert.Contains("تمت القراءة", page);
        Assert.Equal("content:current done:upcoming", Journey(page));
        Assert.Equal(0, await Reads(id));
        Assert.Contains(title, (await emp.GetStringAsync("/"))[..]);                   // listed as a task on the dashboard

        // explicit completion: persisted once, success state, journey and completion card
        var r = await emp.PostForm($"/Content/Details/{id}", $"/Content/Complete/{id}", []);
        Assert.Equal($"/Content/Details/{id}#sec-read", r.Headers.Location!.ToString());
        page = await emp.GetStringAsync($"/Content/Details/{id}");
        Assert.Contains("تم تسجيل إكمال القراءة", page);
        Assert.Contains("id=\"sec-complete\"", page);
        Assert.Equal("content:done done:done", Journey(page));
        Assert.DoesNotContain($"/Content/Complete/{id}", page);
        (await emp.PostForm($"/Content/Details/{id}", $"/Content/Complete/{id}", [])).EnsureRedirect();
        Assert.Equal(1, await Reads(id));                                                  // no duplicate
        var dash = await emp.GetStringAsync("/");
        var ach = dash[dash.IndexOf("lx-achievements", StringComparison.Ordinal)..];
        Assert.Contains(title, ach[..ach.IndexOf("</ul>", StringComparison.Ordinal)]);

        // not for content whose completion is its acknowledgment (or assessment), and not for unpublished content
        Assert.DoesNotContain($"/Content/Complete/{ackId}", await emp.GetStringAsync($"/Content/Details/{ackId}"));
        (await emp.PostForm($"/Content/Details/{ackId}", $"/Content/Complete/{ackId}", [])).EnsureRedirect();
        Assert.Equal(0, await Reads(ackId));
        Assert.Equal(0, await app.Db(d => d.UserAcknowledgments.CountAsync(a => a.UserId == uid && a.ContentId == ackId)));
        Assert.Equal(HttpStatusCode.NotFound, (await emp.PostForm($"/Content/Details/{id}", $"/Content/Complete/{draftId}", [])).StatusCode);
        // another user is not affected
        Assert.Equal(1, await app.Db(d => d.ContentCompletions.CountAsync(x => x.ContentId == id)));
    }

    [Fact]
    public async Task After_passing_the_assessment_list_offers_the_result_not_another_attempt()
    {
        var admin = await AdminAsync();
        var id = await NewContent(admin, "Passed " + Guid.NewGuid().ToString("N")[..6], ack: false, publish: false);
        var aUrl = $"/Admin/Authoring/Assessment/{id}";
        (await admin.PostForm(aUrl, $"/Admin/Authoring/AssessmentSettings/{id}", [F("include", "true"), F("Settings.Title", "اختبار بعد النجاح"), F("Settings.PassingPercentage", "50"), F("Settings.MaxAttempts", "0"), F("go", "stay")])).EnsureRedirect();
        (await admin.PostForm(aUrl, $"/Admin/Authoring/SaveQuestion/{id}", [F("Question.Text", "يجوز مشاركة كلمة المرور."), F("Question.Type", "2"), F("Question.Points", "1"), F("Question.TrueIsCorrect", "false")])).EnsureRedirect();
        (await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")])).EnsureRedirect();
        var aid = await app.Db(d => d.Assessments.Where(a => a.ContentId == id).Select(a => a.Id).SingleAsync());
        var q = await app.Db(d => d.Questions.Include(x => x.Options).SingleAsync(x => x.AssessmentId == aid));
        var name = "ps." + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", name), F("DisplayName", "موظف"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
        var emp = await LoginAsync(name, UserPass, "Ps#NewPass1234");

        var take = (await emp.PostForm("/Assessments", $"/Assessments/Start/{aid}", [])).Headers.Location!.ToString();
        var attempt = take.Split('/').Last();
        (await emp.PostForm(take, $"/Assessments/Submit/{attempt}", [F($"q_{q.Id}", q.Options.Single(o => o.IsCorrect).Id.ToString())])).EnsureRedirect();

        // passed, unlimited attempts: the list's primary action is the result; no "retry" is offered anywhere in the passed state
        var list = await emp.GetStringAsync("/Assessments");
        Assert.Contains($"/Assessments/Result/{attempt}", list);
        Assert.DoesNotContain($"action=\"/Assessments/Start/{aid}\"", list);
        Assert.DoesNotContain($"/Assessments/Start/{aid}", await emp.GetStringAsync($"/Assessments/Result/{attempt}"));
        Assert.DoesNotContain($"/Assessments/Start/{aid}", await emp.GetStringAsync($"/Content/Details/{id}"));
        // backend compatibility (unchanged rule): a new attempt can still be started by a direct request within MaxAttempts;
        // history is kept and the passed result stays the best one
        var again = await emp.PostForm("/Assessments", $"/Assessments/Start/{aid}", []);
        Assert.Contains("/Assessments/Take/", again.Headers.Location!.ToString());
        list = await emp.GetStringAsync("/Assessments");
        Assert.Contains($"/Assessments/Result/{attempt}", list);
        Assert.Contains("متابعة المحاولة المفتوحة", list);
        Assert.Equal(2, await app.Db(d => d.AssessmentAttempts.CountAsync(a => a.AssessmentId == aid)));
    }
}
