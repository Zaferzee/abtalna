using System.Net;
using System.Text.RegularExpressions;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>
/// Optional starter content pack: explicit, idempotent installation through the normal content model (root-owned, sanitized,
/// default audiences), quality rules of the pack itself, employee visibility, the acknowledgment gate, results in the reports,
/// and the development-only demo data.
/// </summary>
public class StarterContentPackTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99";
    private const string UserPass = "Employee#Pass123";
    private static readonly string[] Awareness = Enumerable.Range(1, 11).Select(i => $"AWT-{i:000}").ToArray();
    private static readonly SemaphoreSlim Gate = new(1, 1);

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

    /// <summary>Installs the pack once for this test class (tests may run in any order).</summary>
    private async Task<Dictionary<string, int>> Installed()
    {
        await Gate.WaitAsync();
        try { await StarterContentPack.InstallAsync(app.Services, NullLogger.Instance); }
        finally { Gate.Release(); }
        return await Codes();
    }

    private Task<Dictionary<string, int>> Codes() => app.Db(async d =>
        (await d.SystemSettings.Where(s => s.Key.StartsWith(StarterContentPack.ItemPrefix)).ToListAsync())
        .ToDictionary(s => System.Text.Json.JsonDocument.Parse(s.Value!).RootElement.GetProperty("Code").GetString()!, s => int.Parse(s.Key[StarterContentPack.ItemPrefix.Length..])));

    [Fact]
    public void The_pack_meets_its_content_rules()
    {
        var pack = StarterContentPack.Load();
        Assert.Equal(16, pack.Items.Count);
        Assert.Equal(12, pack.Items.Count(i => i.Assessment != null));
        Assert.Equal(96, pack.Items.Sum(i => i.Assessment?.Questions.Count ?? 0));
        foreach (var it in pack.Items)
        {
            var html = ContentAuthoring.CleanBody(it.Body)!;
            Assert.DoesNotContain("<script", html); Assert.DoesNotContain("http", html);            // no scripts, no external references
            Assert.All(Regex.Matches(html, "<img [^>]*src=\"([^\"]*)\"").Select(m => m.Groups[1].Value), src => Assert.Matches("^/img/starter-pack/[a-z-]+\\.svg$", src));
            Assert.DoesNotMatch(@"\d-\d+-\d", Regex.Replace(it.Body, "<[^>]+>", " "));            // no control numbers in what employees read
            if (it.Code.StartsWith("POL"))
            {
                Assert.StartsWith("<blockquote><strong>نموذج استرشادي تجريبي — يجب استبداله أو مراجعته واعتماده من الجهة قبل الاستخدام الإنتاجي.</strong>", it.Body);
                Assert.True(it.RequiresAcknowledgment); Assert.Equal(ContentStatus.Draft, it.Status); Assert.Equal(ContentType.Policy, it.Type);
                Assert.Null(it.Assessment);
                continue;
            }
            var words = Regex.Replace(it.Body, "<[^>]+>", " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.InRange(words, 450, 750);
            Assert.InRange(Regex.Matches(it.Body, "<h2>").Count - 1, 4, 6);                        // 4-6 sections + the final takeaway
            Assert.EndsWith("</ul>", it.Body); Assert.Contains("<h2>تذكر</h2>", it.Body);
            Assert.Contains("هدف الدرس", it.Body);
            var a = it.Assessment!;
            Assert.Equal(80, a.PassingPercentage);
            Assert.Equal(8, a.Questions.Count);
            Assert.Equal(3, a.Questions.Count(q => q.Kind == "scenario"));
            Assert.Equal(3, a.Questions.Count(q => q.Kind == "knowledge"));
            Assert.Equal(2, a.Questions.Count(q => q.Kind is "truefalse" or "bestaction"));
            foreach (var q in a.Questions)
            {
                Assert.Single(q.Options, o => o.Correct);
                Assert.Equal(q.Type == QuestionType.TrueFalse ? 2 : 4, q.Options.Count);
                Assert.False(string.IsNullOrWhiteSpace(q.Explanation));
            }
        }
        var ai = pack.Items.Single(i => i.Code == "AWT-008");
        Assert.True(ai.RequiresAcknowledgment && ai.AcknowledgmentIsTemplate);
        Assert.StartsWith("أقر بأنني اطلعت على متطلبات الاستخدام الآمن للأدوات الرقمية والذكاء الاصطناعي", ai.AcknowledgmentText);
        Assert.Contains("نموذج إقرار استرشادي", ai.Body);
        var spc = pack.Items.Single(i => i.Code == "SPC-001");
        Assert.Equal(ContentStatus.Draft, spc.Status); Assert.Equal(ContentTargetScope.OwnerOnly, spc.Scope); Assert.False(spc.Assessment!.Published);
        // employees report to the internal channel; the entity handles any regulatory reporting
        Assert.Contains("قناة الإبلاغ الداخلية المعتمدة", pack.Items.Single(i => i.Code == "POL-004").Body);
    }

    [Fact]
    public async Task Install_is_explicit_root_owned_with_default_audiences_and_idempotent()
    {
        var codes = await Installed();
        Assert.Equal(16, codes.Count);
        var root = await app.Db(d => d.Entities.Where(e => e.ParentEntityId == null).Select(e => e.Id).SingleAsync());
        var items = await app.Db(d => d.Contents.Include(c => c.Targets).Where(c => codes.Values.Contains(c.Id)).ToListAsync());
        Assert.All(items, c => Assert.Equal(root, c.OwnerEntityId));
        Assert.All(items, c => Assert.Empty(c.Targets));
        foreach (var code in Awareness)
        {
            var c = items.Single(x => x.Id == codes[code]);
            Assert.Equal(ContentStatus.Published, c.Status); Assert.Equal(ContentTargetScope.OwnerAndDescendants, c.TargetScope);
            Assert.Contains("/img/starter-pack/", c.Body);
        }
        var spc = items.Single(x => x.Id == codes["SPC-001"]);
        Assert.Equal(ContentStatus.Draft, spc.Status); Assert.Equal(ContentTargetScope.OwnerOnly, spc.TargetScope);
        foreach (var code in new[] { "POL-001", "POL-002", "POL-003", "POL-004" })
        {
            var c = items.Single(x => x.Id == codes[code]);
            Assert.Equal(ContentStatus.Draft, c.Status); Assert.True(c.RequiresAcknowledgment); Assert.StartsWith("<blockquote><strong>نموذج استرشادي تجريبي", c.Body);
        }
        var asm = await app.Db(d => d.Assessments.Include(a => a.Questions).ThenInclude(q => q.Options).Where(a => a.ContentId != null && codes.Values.Contains(a.ContentId.Value)).ToListAsync());
        Assert.Equal(12, asm.Count); Assert.Equal(96, asm.Sum(a => a.Questions.Count));
        Assert.All(asm, a => { Assert.Equal(80, a.PassingPercentage); Assert.Equal(root, a.OwnerEntityId); });
        Assert.All(asm.SelectMany(a => a.Questions), q => Assert.Single(q.Options, o => o.IsCorrect));
        Assert.False(asm.Single(a => a.ContentId == codes["SPC-001"]).IsPublished);

        // running it again changes nothing
        var before = await app.Db(d => Task.FromResult((d.Contents.Count(), d.Questions.Count(), d.Assessments.Count())));
        var again = await StarterContentPack.InstallAsync(app.Services, NullLogger.Instance);
        Assert.True(again.Ok); Assert.False(again.Installed); Assert.Contains("already installed", again.Message);
        Assert.Equal(before, await app.Db(d => Task.FromResult((d.Contents.Count(), d.Questions.Count(), d.Assessments.Count()))));
        Assert.Equal(1, await app.Db(d => d.AuditLogs.CountAsync(l => l.Action == "STARTER_PACK_INSTALLED")));

        // a lost marker does not duplicate anything either: recorded items are skipped
        await app.Db(async d => { await d.SystemSettings.Where(s => s.Key == StarterContentPack.MarkerKey).ExecuteDeleteAsync(); return 0; });
        var rerun = await StarterContentPack.InstallAsync(app.Services, NullLogger.Instance);
        Assert.True(rerun.Ok); Assert.Equal(0, rerun.Contents);
        Assert.Equal(before, await app.Db(d => Task.FromResult((d.Contents.Count(), d.Questions.Count(), d.Assessments.Count()))));
    }

    [Fact]
    public async Task Employees_see_the_published_items_and_administrators_see_the_references()
    {
        var codes = await Installed();
        var admin = await AdminAsync();
        var root = await app.Db(d => d.Entities.Where(e => e.ParentEntityId == null).Select(e => e.Id).SingleAsync());
        var code = "SP" + Guid.NewGuid().ToString("N")[..6];
        var created = await admin.PostForm("/Admin/Entities/Create", "/Admin/Entities/Create", [F("Name", "جهة تابعة " + code), F("Code", code), F("ParentEntityId", root.ToString()), F("IsActive", "true"), F("Allowed", "ManageUsers")]);
        var child = int.Parse(Regex.Match(created.Headers.Location!.ToString(), @"/Details/(\d+)").Groups[1].Value);
        var name = "sp." + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", name), F("DisplayName", "موظف " + name), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true"), F("EntityId", child.ToString())])).EnsureRedirect();
        var emp = await LoginAsync(name, UserPass, "Starter#NewPass1");

        var list = await emp.GetStringAsync("/Content");
        foreach (var c in Awareness) Assert.Contains($"/Content/Details/{codes[c]}\"", list);   // all entities / descendants
        Assert.DoesNotContain($"/Content/Details/{codes["SPC-001"]}\"", list);                  // specialized item is not global
        Assert.DoesNotContain($"/Content/Details/{codes["POL-001"]}\"", list);                  // policy templates stay draft
        Assert.Equal(HttpStatusCode.NotFound, (await emp.GetAsync($"/Content/Details/{codes["SPC-001"]}")).StatusCode);

        var lesson = await emp.GetStringAsync($"/Content/Details/{codes["AWT-002"]}");
        Assert.Contains("التصيد والهندسة الاجتماعية", lesson);
        Assert.Contains("<img src=\"/img/starter-pack/phishing.svg\"", lesson);
        Assert.DoesNotContain("مرجع استرشادي", lesson); Assert.DoesNotContain("1-10-3-1", lesson);  // governance metadata stays on the admin side
        Assert.Equal(HttpStatusCode.OK, (await emp.GetAsync("/img/starter-pack/phishing.svg")).StatusCode);

        var adminList = await admin.GetStringAsync("/Admin/Content");
        Assert.Contains("مرجع استرشادي: ECC 2:2024 — 1-10-3-1", adminList);
        Assert.Contains("مرجع استرشادي: DCC — 1-3-1-1، 1-3-1-6", adminList);
        var shell = await admin.GetStringAsync($"/Admin/Authoring/Basics/{codes["AWT-008"]}");
        Assert.Contains("حزمة المحتوى الأولي", shell); Assert.Contains("نموذج استرشادي: يُراجع ويُعتمد قبل الاستخدام الإنتاجي", shell);
        Assert.Contains("موضع وسائط رسمية", shell);
    }

    [Fact]
    public async Task The_AI_lesson_is_gated_by_its_acknowledgment_and_results_reach_the_reports()
    {
        var codes = await Installed();
        var admin = await AdminAsync();
        var name = "ai." + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", name), F("DisplayName", "موظف " + name), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
        var emp = await LoginAsync(name, UserPass, "Starter#NewPass1");
        var cid = codes["AWT-008"];
        var asm = await app.Db(d => d.Assessments.Include(a => a.Questions).ThenInclude(q => q.Options).SingleAsync(a => a.ContentId == cid));

        var page = await emp.GetStringAsync($"/Content/Details/{cid}");
        Assert.Contains("الاختبار مقفل", page);
        Assert.Contains("أقر بأنني اطلعت على متطلبات الاستخدام الآمن للأدوات الرقمية والذكاء الاصطناعي", page);
        Assert.Equal($"/Content/Details/{cid}#sec-ack", (await emp.PostForm($"/Content/Details/{cid}", $"/Assessments/Start/{asm.Id}", [])).Headers.Location!.ToString());

        (await emp.PostForm($"/Content/Details/{cid}", $"/Content/Acknowledge/{cid}", [])).EnsureRedirect();
        var start = await emp.PostForm($"/Content/Details/{cid}", $"/Assessments/Start/{asm.Id}", []);
        var take = start.Headers.Location!.ToString();
        Assert.Contains("/Assessments/Take/", take);
        var attemptId = int.Parse(take.Split('/').Last());
        var answers = asm.Questions.OrderBy(q => q.SortOrder).Select((q, i) => F($"q_{q.Id}", (i == 0 ? q.Options.First(o => !o.IsCorrect) : q.Options.Single(o => o.IsCorrect)).Id.ToString())).ToList();
        (await emp.PostForm(take, $"/Assessments/Submit/{attemptId}", answers)).EnsureRedirect();
        var done = await app.Db(d => d.AssessmentAttempts.SingleAsync(a => a.Id == attemptId));
        Assert.Equal(87.5m, done.Percentage); Assert.True(done.Passed); Assert.Equal(80, done.PassingPercentageSnapshot);   // 7/8 >= 80%

        Assert.Contains(name, await admin.GetStringAsync($"/Admin/Reports/Assessments?assessmentId={asm.Id}&status=Passed"));
        Assert.Contains(name, await admin.GetStringAsync($"/Admin/Reports/Acknowledgments?contentId={cid}&status=Acknowledged"));
    }

    [Fact]
    public async Task Demo_data_is_development_only_and_separate_from_the_content()
    {
        await Installed();
        var prod = new FakeEnv { EnvironmentName = Environments.Production };
        var refused = await StarterContentPack.SeedDemoDataAsync(app.Services, prod, NullLogger.Instance);
        Assert.False(refused.Ok); Assert.Contains("Development environment only", refused.Message);
        Assert.Equal(0, await app.Db(d => d.Users.CountAsync(u => u.Username.StartsWith("demo.employee"))));

        var dev = new FakeEnv { EnvironmentName = Environments.Development };
        var r = await StarterContentPack.SeedDemoDataAsync(app.Services, dev, NullLogger.Instance);
        Assert.True(r.Ok && r.Installed);
        var demo = await app.Db(d => d.Users.Where(u => u.Username.StartsWith("demo.employee")).ToListAsync());
        Assert.Equal(8, demo.Count); Assert.All(demo, u => Assert.Null(u.PasswordHash));                  // cannot sign in
        var attempts = await app.Db(d => d.AssessmentAttempts.Where(a => demo.Select(u => u.Id).Contains(a.UserId)).ToListAsync());
        Assert.NotEmpty(attempts); Assert.All(attempts, a => { Assert.Equal(AttemptStatus.Completed, a.Status); Assert.Equal(a.Percentage >= 80, a.Passed); });
        Assert.False((await StarterContentPack.SeedDemoDataAsync(app.Services, dev, NullLogger.Instance)).Installed);   // once only
    }

    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "CyberLms.Web";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
