using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Controllers;

[Authorize]
public class ContentController(AppDbContext db, ContentAuthoring authoring) : AppController
{
    private IQueryable<Content> Published => db.Contents.AsNoTracking().Where(c => c.Status == ContentStatus.Published);

    private async Task<ContentIndexVm> Build(ContentType? type, string? q, bool policies)
    {
        var query = Published;
        if (policies) query = query.Where(c => c.Type == ContentType.Policy || c.Type == ContentType.Regulation);
        else if (type != null) query = query.Where(c => c.Type == type);
        if (!string.IsNullOrWhiteSpace(q)) { var s = q.Trim().ToLower(); query = query.Where(c => c.Title.ToLower().Contains(s) || (c.Description != null && c.Description.ToLower().Contains(s))); }
        var uid = User.UserId();
        var items = await query.OrderByDescending(c => c.PublishedAt).Take(500).ToListAsync();
        var ids = items.Select(i => i.Id).ToList();
        var acked = await db.UserAcknowledgments.Where(a => a.UserId == uid && ids.Contains(a.ContentId)).Select(a => new { a.ContentId, a.ContentVersion }).ToListAsync();
        return new ContentIndexVm
        {
            Items = items, Type = type, Q = q, PoliciesOnly = policies,
            AcknowledgedIds = items.Where(i => acked.Any(a => a.ContentId == i.Id && a.ContentVersion == i.Version)).Select(i => i.Id).ToHashSet(),
        };
    }

    public async Task<IActionResult> Index(ContentType? type, string? q) => View(await Build(type, q, false));
    public async Task<IActionResult> Policies(string? q) => View("Index", await Build(null, q, true));

    public async Task<IActionResult> Details(int id)
    {
        var c = await db.Contents.AsNoTracking().Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
        var preview = false;
        if (c == null) return NotFound();
        if (c.Status != ContentStatus.Published)
        {
            if (!User.IsInRole(RoleNames.Admin)) return NotFound();
            preview = true;
        }
        var uid = User.UserId();
        // Employees see the published assessments of this content; an administrator previewing unpublished content also sees the draft one.
        var related = await db.Assessments.AsNoTracking().Where(a => a.ContentId == id && a.Questions.Any() && (a.IsPublished || preview)).OrderBy(a => a.Id).ToListAsync();
        return View(new ContentDetailsVm
        {
            Content = c, Preview = preview, AckText = authoring.AckText(c),
            // An administrator previewing unpublished content cannot acknowledge it: the page behaves like the wizard's preview.
            Interactive = !preview, JustAcknowledged = TempData["AckDone"] is true,
            Ack = await db.UserAcknowledgments.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == uid && a.ContentId == id && a.ContentVersion == c.Version),
            Related = await AssessmentQueries.ForUser(db, uid, related),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Acknowledge(int id)
    {
        var c = await db.Contents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == ContentStatus.Published && x.RequiresAcknowledgment);
        if (c == null) return NotFound();
        var uid = User.UserId();
        if (!await db.UserAcknowledgments.AnyAsync(a => a.UserId == uid && a.ContentId == id && a.ContentVersion == c.Version))
        {
            db.UserAcknowledgments.Add(new UserAcknowledgment { UserId = uid, ContentId = id, ContentVersion = c.Version });
            try { await db.SaveChangesAsync(); TempData["AckDone"] = true; }
            catch (DbUpdateException) { /* double click: unique index already holds the first acknowledgment */ }
        }
        return RedirectToAction(nameof(Details), null, new { id }, "sec-ack");
    }
}
