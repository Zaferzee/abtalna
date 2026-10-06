using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

public class ContentController(AppDbContext db, StorageService storage, AuditService audit, NotificationService notify, ContentAuthoring authoring) : AdminController
{
    public async Task<IActionResult> Index(string? q, ContentType? type, ContentStatus? status, int page = 1)
    {
        var query = db.Contents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var s = q.Trim().ToLower(); query = query.Where(c => c.Title.ToLower().Contains(s)); }
        if (type != null) query = query.Where(c => c.Type == type);
        if (status != null) query = query.Where(c => c.Status == status);
        var pager = new Pager { Page = Math.Max(1, page), PageSize = 25, Total = await query.CountAsync() };
        var items = await query.OrderByDescending(c => c.CreatedAt).Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize)
            .Select(c => new ContentRow(c, db.UserAcknowledgments.Count(a => a.ContentId == c.Id && a.ContentVersion == c.Version),
                db.Assessments.Where(a => a.ContentId == c.Id).OrderBy(a => a.Id).Select(a => (int?)a.Id).FirstOrDefault(),
                db.Assessments.Where(a => a.ContentId == c.Id).OrderBy(a => a.Id).Select(a => a.Title).FirstOrDefault(),
                db.Assessments.Where(a => a.ContentId == c.Id).OrderBy(a => a.Id).Select(a => a.Questions.Count).FirstOrDefault(),
                db.Assessments.Where(a => a.ContentId == c.Id).OrderBy(a => a.Id).Select(a => a.IsPublished).FirstOrDefault())).ToListAsync();
        return View(new ContentListVm { Items = items, Q = q, Type = type, Status = status, Pager = pager });
    }

    public IActionResult Create() => View("Form", new ContentFormVm());

    [HttpPost]
    public async Task<IActionResult> Create(ContentFormVm vm, List<IFormFile> files, bool publish)
    {
        Validate(vm);
        if (!ModelState.IsValid) return View("Form", vm);
        var c = new Content { CreatedById = User.UserId() };
        Apply(c, vm);
        db.Contents.Add(c);
        await db.SaveChangesAsync();
        var errors = await SaveFiles(c, files);
        audit.Add("CONTENT_CREATED", "Content", c.Id, c.Title);
        if (publish && errors.Count == 0) { Publish(c); }
        await db.SaveChangesAsync();
        if (errors.Count > 0) { TempData["Error"] = string.Join(" ", errors); return RedirectToAction(nameof(Edit), new { id = c.Id }); }
        Success(publish ? "Content created and published." : "Content created as draft.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await db.Contents.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return View("Form", new ContentFormVm
        {
            Id = c.Id, Title = c.Title, Description = c.Description, Body = c.Body, Type = c.Type, Status = c.Status,
            RequiresAcknowledgment = c.RequiresAcknowledgment, ExternalUrl = c.ExternalUrl, Attachments = c.Attachments.OrderBy(a => a.Id).ToList(),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ContentFormVm vm, List<IFormFile> files, string? command)
    {
        var c = await db.Contents.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        Validate(vm);
        if (!ModelState.IsValid) { vm.Id = id; vm.Status = c.Status; vm.Attachments = c.Attachments.ToList(); return View("Form", vm); }
        Apply(c, vm);
        c.UpdatedAt = DateTime.UtcNow;
        var errors = await SaveFiles(c, files);
        audit.Add("CONTENT_UPDATED", "Content", c.Id, c.Title);
        if (command == "publish" && errors.Count == 0 && c.Status != ContentStatus.Published) Publish(c);
        await db.SaveChangesAsync();
        TempData[errors.Count > 0 ? "Error" : "Success"] = errors.Count > 0 ? string.Join(" ", errors) : L["Saved."];
        return errors.Count > 0 ? RedirectToAction(nameof(Edit), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, bool publish)
    {
        var c = await db.Contents.FindAsync(id);
        if (c == null) return NotFound();
        if (publish) Publish(c);
        else { c.Status = ContentStatus.Draft; audit.Add("CONTENT_UNPUBLISHED", "Content", c.Id, c.Title); }
        await db.SaveChangesAsync();
        Success(publish ? "Published." : "Unpublished.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Contents.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (c.Status == ContentStatus.Published) { Failure("Unpublish the content before deleting it."); return RedirectToAction(nameof(Index)); }
        if (await db.UserAcknowledgments.AnyAsync(a => a.ContentId == id))
        { Failure("This content has acknowledgment records and cannot be deleted (compliance evidence). Keep it unpublished."); return RedirectToAction(nameof(Index)); }
        var paths = c.Attachments.Select(a => a.StoredPath).ToList();
        audit.Add("CONTENT_DELETED", "Content", c.Id, c.Title);
        db.Contents.Remove(c);
        await db.SaveChangesAsync();
        paths.ForEach(storage.Delete);
        Success("Deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        var a = await db.ContentAttachments.FindAsync(id);
        if (a == null) return NotFound();
        var cid = a.ContentId;
        audit.Add("ATTACHMENT_DELETED", "Content", cid, a.FileName);
        db.ContentAttachments.Remove(a);
        await db.SaveChangesAsync();
        storage.Delete(a.StoredPath);
        return RedirectToAction(nameof(Edit), new { id = cid });
    }

    [HttpPost]
    public async Task<IActionResult> ResetAcknowledgments(int id, int? userId)
    {
        var q = db.UserAcknowledgments.Where(a => a.ContentId == id);
        if (userId != null) q = q.Where(a => a.UserId == userId);
        var n = await q.ExecuteDeleteAsync();
        audit.Add("ACKNOWLEDGMENTS_RESET", "Content", id, userId == null ? Res.Ar("All ({0}) acknowledgments reset", n) : Res.Ar("Reset for user {0}", userId));
        await db.SaveChangesAsync();
        Success("{0} acknowledgment(s) reset.", n);
        return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } r && Url.IsLocalUrl(new Uri(r).PathAndQuery) ? new Uri(r).PathAndQuery : Url.Action(nameof(Index))!);
    }

    [HttpPost]
    public async Task<IActionResult> Notify(int id)
    {
        var c = await db.Contents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == ContentStatus.Published);
        if (c == null) return NotFound();
        if (!notify.Ready) { Failure("SMTP is not configured (Settings → Email)."); return RedirectToAction(nameof(Index)); }
        var users = db.Users.AsNoTracking().Where(u => u.IsActive && u.Email != null && u.UserRoles.Any(r => r.Role.Name == RoleNames.User));
        if (c.RequiresAcknowledgment)
            users = users.Where(u => !db.UserAcknowledgments.Any(a => a.UserId == u.Id && a.ContentId == id && a.ContentVersion == c.Version));
        var list = await users.Select(u => new { u.Email, u.DisplayName }).ToListAsync();
        var n = notify.Send(list.Select(u => (u.Email, u.DisplayName)),
            c.RequiresAcknowledgment ? "Acknowledgment required: {0}" : "New content: {0}",
            c.RequiresAcknowledgment ? "Please read and acknowledge: {0}" : "New content has been published: {0}", [c.Title], $"/Content/Details/{id}");
        audit.Add("NOTIFICATION_QUEUED", "Content", id, Res.Ar("{0} emails", n));
        await db.SaveChangesAsync();
        Success("{0} email(s) queued.", n);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Image upload for the rich-text editor. Returns the URL to embed.</summary>
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null) return BadRequest(new { error = L["The file is empty."] });
        var (err, stored) = await storage.SaveAsync(file, "inline", [AttachmentKind.Image]);
        if (err != null) return BadRequest(new { error = L.Format(err.Key, err.Args) });
        audit.Add("ATTACHMENT_ADDED", "Content", null, stored!.OriginalName);
        await db.SaveChangesAsync();
        return Json(new { url = "/Files/Inline/" + Path.GetFileName(stored.RelativePath) });
    }

    private void Publish(Content c) => authoring.Publish(c);
    private Task<List<string>> SaveFiles(Content c, List<IFormFile> files) => authoring.SaveFiles(c, files);

    private void Validate(ContentFormVm vm)
    {
        if (authoring.LinkError(vm.ExternalUrl) is string err) ModelState.AddModelError(nameof(vm.ExternalUrl), err);
    }

    private static void Apply(Content c, ContentFormVm vm)
    {
        c.Title = vm.Title.Trim(); c.Description = vm.Description?.Trim(); c.Type = vm.Type;
        c.RequiresAcknowledgment = vm.RequiresAcknowledgment;
        c.ExternalUrl = string.IsNullOrWhiteSpace(vm.ExternalUrl) ? null : vm.ExternalUrl.Trim();
        c.Body = ContentAuthoring.CleanBody(vm.Body);
    }
}
