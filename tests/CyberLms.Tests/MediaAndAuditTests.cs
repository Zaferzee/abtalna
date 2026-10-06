using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>
/// Inline video / PDF on content pages (served only through the authenticated attachment endpoint) and the Audit Log page
/// (regression: the page used to be empty because the filter parameter was bound to the reserved "action" route value).
/// </summary>
public class MediaAndAuditTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99";
    private const string UserPass = "Employee#Pass123";
    private static readonly byte[] Pdf = "%PDF-1.4\n%test\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n"u8.ToArray();
    // Minimal ISO-BMFF header ("ftyp" box) followed by padding: enough for the upload signature check and for range requests.
    private static readonly byte[] Mp4 = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 2, 0, (byte)'i', (byte)'s', (byte)'o', (byte)'m', (byte)'m', (byte)'p', (byte)'4', (byte)'1', .. new byte[4000]];

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

    private async Task<HttpClient> EmployeeAsync()
    {
        if (!await app.Db(d => d.Users.AnyAsync(u => u.Username == "media.emp")))
        {
            var admin = await AdminAsync();
            (await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", "media.emp"), F("DisplayName", "موظف الوسائط"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true")])).EnsureRedirect();
            return await LoginAsync("media.emp", UserPass, "Media#NewPass123");
        }
        return await LoginAsync("media.emp", "Media#NewPass123");
    }

    /// <summary>Creates (through the wizard endpoints) a content item with one MP4 and one PDF; optionally publishes it.</summary>
    private async Task<(int ContentId, int PdfId, int VideoId)> CreateMediaContent(HttpClient admin, string title, bool publish)
    {
        var w = (await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", title), F("Type", "7"), F("go", "next")])).Headers.Location!.ToString();
        var id = int.Parse(w.Split('/').Last());
        (await admin.PostMultipart($"/Admin/Authoring/Write/{id}", $"/Admin/Authoring/Write/{id}", mp =>
        {
            mp.Add(new StringContent("<h2>مقدمة</h2><p>نص تجريبي.</p>"), "Body"); mp.Add(new StringContent("next"), "go");
            mp.AddFile("files", "فيديو التوعية.mp4", Mp4, "video/mp4"); mp.AddFile("files", "اللائحة.pdf", Pdf, "application/pdf");
        })).EnsureRedirect();
        if (publish) (await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")])).EnsureRedirect();
        var atts = await app.Db(d => d.ContentAttachments.Where(a => a.ContentId == id).ToListAsync());
        return (id, atts.Single(a => a.Kind == AttachmentKind.Document).Id, atts.Single(a => a.Kind == AttachmentKind.Video).Id);
    }

    [Fact]
    public async Task Content_page_embeds_video_and_pdf_through_the_authenticated_endpoint_only()
    {
        var admin = await AdminAsync();
        var (id, pdfId, videoId) = await CreateMediaContent(admin, "Media page " + Guid.NewGuid().ToString("N")[..6], publish: true);
        var emp = await EmployeeAsync();

        var html = await emp.GetStringAsync($"/Content/Details/{id}");
        Assert.Matches($"<video[^>]*src=\"/Files/Attachment/{videoId}\"", html);                 // inline player
        Assert.Contains("data-pdf-viewer", html);
        Assert.Contains($"data-src=\"/Files/Attachment/{pdfId}\"", html);                        // PDF reader uses the same endpoint
        Assert.Contains("/lib/pdfjs/", await emp.GetStringAsync("/js/pdf-viewer.mjs"));          // bundled locally, no CDN
        foreach (var stored in await app.Db(d => d.ContentAttachments.Where(a => a.ContentId == id).Select(a => a.StoredPath).ToListAsync()))
            Assert.DoesNotContain(stored, html);                                                  // no storage path leaks into the page
        Assert.True(html.IndexOf("id=\"sec-video\"", StringComparison.Ordinal) < html.IndexOf("id=\"sec-pdf\"", StringComparison.Ordinal));

        // authorized PDF: served inline as application/pdf (not as a forced download), sandboxed
        var pdf = await emp.GetAsync($"/Files/Attachment/{pdfId}");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Null(pdf.Content.Headers.ContentDisposition);
        Assert.Contains("sandbox", pdf.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(Pdf, await pdf.Content.ReadAsByteArrayAsync());
        // explicit download keeps working
        Assert.Equal("attachment", (await emp.GetAsync($"/Files/Attachment/{pdfId}?download=true")).Content.Headers.ContentDisposition!.DispositionType);

        // authorized video: inline, seekable (HTTP range requests)
        var req = new HttpRequestMessage(HttpMethod.Get, $"/Files/Attachment/{videoId}");
        req.Headers.Range = new RangeHeaderValue(0, 99);
        var video = await emp.SendAsync(req);
        Assert.Equal(HttpStatusCode.PartialContent, video.StatusCode);
        Assert.Equal("video/mp4", video.Content.Headers.ContentType!.MediaType);
        Assert.Equal(100, (await video.Content.ReadAsByteArrayAsync()).Length);

        // unauthorized: anonymous users are sent to sign-in and never receive the file
        var anon = app.NewClient();
        foreach (var fid in new[] { pdfId, videoId })
        {
            var r = await anon.GetAsync($"/Files/Attachment/{fid}");
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Contains("/Account/Login", r.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task Media_of_unpublished_content_is_hidden_from_employees_but_previewable_by_admins()
    {
        var admin = await AdminAsync();
        var (id, pdfId, videoId) = await CreateMediaContent(admin, "Draft media " + Guid.NewGuid().ToString("N")[..6], publish: false);
        var emp = await EmployeeAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await emp.GetAsync($"/Files/Attachment/{pdfId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await emp.GetAsync($"/Files/Attachment/{videoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await emp.GetAsync($"/Content/Details/{id}")).StatusCode);
        // the wizard's "preview as employee" shows the same inline video and PDF reader to the administrator
        var preview = await admin.GetStringAsync($"/Admin/Authoring/Preview/{id}");
        Assert.Matches($"<video[^>]*src=\"/Files/Attachment/{videoId}\"", preview);
        Assert.Contains($"data-src=\"/Files/Attachment/{pdfId}\"", preview);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/Files/Attachment/{pdfId}")).StatusCode);
        // a missing attachment is a plain 404 (the viewer shows its Arabic "not available" message)
        Assert.Equal(HttpStatusCode.NotFound, (await emp.GetAsync("/Files/Attachment/999999")).StatusCode);
    }

    private static int AuditRows(string html) => Regex.Matches(html, "class=\"audit-code\"").Count;

    [Fact]
    public async Task Audit_log_lists_entries_filters_and_is_reachable_from_the_dashboard()
    {
        var admin = await AdminAsync();
        await CreateMediaContent(admin, "Audit sample " + Guid.NewGuid().ToString("N")[..6], publish: true); // guarantees entries
        var total = await app.Db(d => d.AuditLogs.CountAsync());
        Assert.True(total > 0);

        // regression: the page must list rows (it used to filter by the route value "Index" and render nothing)
        var page = await admin.GetAsync("/Admin/Audit");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(Math.Min(total, 50), AuditRows(html));
        Assert.Contains("نشر محتوى", html);                            // Arabic action name
        Assert.Contains(">CONTENT_PUBLISHED<", html);                   // original code kept visible (data unchanged)
        Assert.DoesNotContain("لا توجد أنشطة مسجّلة بعد", html);

        // filter by action (query parameter "op")
        var published = await app.Db(d => d.AuditLogs.CountAsync(a => a.Action == "CONTENT_PUBLISHED"));
        var filtered = await admin.GetStringAsync("/Admin/Audit?op=CONTENT_PUBLISHED");
        Assert.Equal(Math.Min(published, 50), AuditRows(filtered));
        Assert.Equal(AuditRows(filtered), Regex.Matches(filtered, ">CONTENT_PUBLISHED<").Count);
        // search + empty state
        Assert.True(AuditRows(await admin.GetStringAsync("/Admin/Audit?q=admin")) > 0);
        var none = await admin.GetStringAsync("/Admin/Audit?op=NO_SUCH_ACTION");
        Assert.Equal(0, AuditRows(none));
        Assert.Contains("لا توجد سجلات مطابقة للتصفية", none);
        // pagination when there are more than 50 entries
        if (total > 50) Assert.Equal(Math.Min(total - 50, 50), AuditRows(await admin.GetStringAsync("/Admin/Audit?page=2")));

        // dashboard "View all" (recent activity) points to the Audit Log and that destination renders entries
        var dash = await admin.GetStringAsync("/Admin/Dashboard");
        var recent = dash[dash.IndexOf("آخر الأنشطة", StringComparison.Ordinal)..];
        var href = Regex.Match(recent, "href=\"([^\"]+)\"[^>]*>عرض الكل").Groups[1].Value;
        Assert.Equal("/Admin/Audit", href);
        Assert.True(AuditRows(await admin.GetStringAsync(href)) > 0);

        // read-only and admin-only
        var emp = await EmployeeAsync();
        var denied = await emp.GetAsync("/Admin/Audit");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("Denied", denied.Headers.Location!.ToString());
        Assert.True(await app.Db(d => d.AuditLogs.CountAsync()) >= total); // viewing and filtering never remove entries
    }
}
