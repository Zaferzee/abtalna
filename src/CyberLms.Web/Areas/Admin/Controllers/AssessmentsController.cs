using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

public class AssessmentsController(AppDbContext db, AuditService audit, NotificationService notify) : AdminController
{
    public async Task<IActionResult> Index()
    {
        var rows = await db.Assessments.AsNoTracking().OrderByDescending(a => a.CreatedAt)
            .Select(a => new AssessmentRowVm { A = a, Questions = a.Questions.Count, Attempts = db.AssessmentAttempts.Count(t => t.AssessmentId == a.Id && t.Status == AttemptStatus.Completed), ContentTitle = a.Content == null ? null : a.Content.Title })
            .ToListAsync();
        return View(rows);
    }

    private async Task Lookups(int? selected) =>
        ViewBag.Contents = new SelectList(await db.Contents.AsNoTracking().OrderBy(c => c.Title).Select(c => new { c.Id, c.Title }).ToListAsync(), "Id", "Title", selected);

    public async Task<IActionResult> Create() { await Lookups(null); return View("Form", new AssessmentFormVm()); }

    [HttpPost]
    public async Task<IActionResult> Create(AssessmentFormVm vm)
    {
        await Lookups(vm.ContentId);
        if (!ModelState.IsValid) return View("Form", vm);
        var a = new Assessment { Title = vm.Title.Trim(), Description = vm.Description?.Trim(), ContentId = vm.ContentId, PassingPercentage = vm.PassingPercentage, MaxAttempts = vm.MaxAttempts };
        db.Assessments.Add(a);
        await db.SaveChangesAsync();
        audit.Add("ASSESSMENT_CREATED", "Assessment", a.Id, a.Title);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions), new { id = a.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var a = await db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        await Lookups(a.ContentId);
        return View("Form", new AssessmentFormVm { Id = a.Id, Title = a.Title, Description = a.Description, ContentId = a.ContentId, PassingPercentage = a.PassingPercentage, MaxAttempts = a.MaxAttempts });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AssessmentFormVm vm)
    {
        var a = await db.Assessments.FindAsync(id);
        if (a == null) return NotFound();
        await Lookups(vm.ContentId);
        if (!ModelState.IsValid) return View("Form", vm);
        a.Title = vm.Title.Trim(); a.Description = vm.Description?.Trim(); a.ContentId = vm.ContentId;
        a.PassingPercentage = vm.PassingPercentage; a.MaxAttempts = vm.MaxAttempts; a.UpdatedAt = DateTime.UtcNow;
        audit.Add("ASSESSMENT_UPDATED", "Assessment", a.Id, a.Title);
        await db.SaveChangesAsync();
        Success("Saved. (Completed attempts keep the passing percentage they were taken with.)");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, bool publish)
    {
        var a = await db.Assessments.Include(x => x.Questions).ThenInclude(q => q.Options).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        if (publish)
        {
            if (a.Questions.Count == 0) { Failure("Add at least one question before publishing."); return RedirectToAction(nameof(Index)); }
            a.IsPublished = true;
            audit.Add("ASSESSMENT_PUBLISHED", "Assessment", a.Id, a.Title);
        }
        else { a.IsPublished = false; audit.Add("ASSESSMENT_UNPUBLISHED", "Assessment", a.Id, a.Title); }
        await db.SaveChangesAsync();
        Success(publish ? "Published." : "Unpublished.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await db.Assessments.FindAsync(id);
        if (a == null) return NotFound();
        if (await db.AssessmentAttempts.AnyAsync(t => t.AssessmentId == id)) { Failure("This assessment has attempts and cannot be deleted. Unpublish it instead."); return RedirectToAction(nameof(Index)); }
        audit.Add("ASSESSMENT_DELETED", "Assessment", a.Id, a.Title);
        db.Assessments.Remove(a);
        await db.SaveChangesAsync();
        Success("Deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Duplicate(int id)
    {
        var a = await db.Assessments.AsNoTracking().Include(x => x.Questions).ThenInclude(q => q.Options).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        var copy = new Assessment { Title = a.Title + " (copy)", Description = a.Description, ContentId = a.ContentId, PassingPercentage = a.PassingPercentage, MaxAttempts = a.MaxAttempts };
        foreach (var q in a.Questions.OrderBy(q => q.SortOrder))
            copy.Questions.Add(new Question { Text = q.Text, Type = q.Type, Points = q.Points, SortOrder = q.SortOrder, Options = q.Options.OrderBy(o => o.SortOrder).Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect, SortOrder = o.SortOrder }).ToList() });
        db.Assessments.Add(copy);
        await db.SaveChangesAsync();
        audit.Add("ASSESSMENT_CREATED", "Assessment", copy.Id, Res.Ar("Duplicate of {0}", id));
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions), new { id = copy.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Notify(int id)
    {
        var a = await db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsPublished);
        if (a == null) return NotFound();
        if (!notify.Ready) { Failure("SMTP is not configured (Settings → Email)."); return RedirectToAction(nameof(Index)); }
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive && u.Email != null && u.UserRoles.Any(r => r.Role.Name == RoleNames.User) &&
            !db.AssessmentAttempts.Any(t => t.UserId == u.Id && t.AssessmentId == id && t.Status == AttemptStatus.Completed)).Select(u => new { u.Email, u.DisplayName }).ToListAsync();
        var n = notify.Send(users.Select(u => (u.Email, u.DisplayName)), "Assessment available: {0}", "Please complete the assessment: {0}", [a.Title], "/Assessments");
        audit.Add("NOTIFICATION_QUEUED", "Assessment", id, Res.Ar("{0} emails", n));
        await db.SaveChangesAsync();
        Success("{0} email(s) queued (users who have not completed it yet).", n);
        return RedirectToAction(nameof(Index));
    }

    // ---- Questions ----
    public async Task<IActionResult> Questions(int id)
    {
        var a = await db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        var qs = await db.Questions.AsNoTracking().Include(q => q.Options).Where(q => q.AssessmentId == id).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToListAsync();
        return View(new QuestionsVm { Assessment = a, Questions = qs, Attempts = await db.AssessmentAttempts.CountAsync(t => t.AssessmentId == id) });
    }

    private async Task<bool> Locked(int assessmentId) => await db.AssessmentAttempts.AnyAsync(t => t.AssessmentId == assessmentId);

    public async Task<IActionResult> AddQuestion(int id)
    {
        if (await Locked(id)) { Failure("This assessment already has attempts; its questions are locked. Duplicate it to change questions."); return RedirectToAction(nameof(Questions), new { id }); }
        var max = await db.Questions.Where(q => q.AssessmentId == id).MaxAsync(q => (int?)q.SortOrder) ?? 0;
        return View("QuestionForm", new QuestionFormVm { AssessmentId = id, SortOrder = max + 1 });
    }

    [HttpPost]
    public async Task<IActionResult> AddQuestion(int id, QuestionFormVm vm)
    {
        vm.AssessmentId = id;
        if (!await db.Assessments.AnyAsync(a => a.Id == id)) return NotFound();
        if (await Locked(id)) return RedirectToAction(nameof(Questions), new { id });
        var q = new Question { AssessmentId = id };
        if (!Apply(q, vm)) return View("QuestionForm", vm);
        db.Questions.Add(q);
        audit.Add("QUESTION_ADDED", "Assessment", id, q.Text.Length > 100 ? q.Text[..100] : q.Text);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions), new { id });
    }

    public async Task<IActionResult> EditQuestion(int id)
    {
        var q = await db.Questions.AsNoTracking().Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound();
        if (await Locked(q.AssessmentId)) { Failure("Questions are locked because attempts exist. Duplicate the assessment instead."); return RedirectToAction(nameof(Questions), new { id = q.AssessmentId }); }
        return View("QuestionForm", ContentAuthoring.FormFor(q));
    }

    [HttpPost]
    public async Task<IActionResult> EditQuestion(int id, QuestionFormVm vm)
    {
        var q = await db.Questions.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound();
        vm.Id = id; vm.AssessmentId = q.AssessmentId;
        if (await Locked(q.AssessmentId)) return RedirectToAction(nameof(Questions), new { id = q.AssessmentId });
        db.QuestionOptions.RemoveRange(q.Options);
        q.Options.Clear();
        if (!Apply(q, vm)) { db.ChangeTracker.Clear(); return View("QuestionForm", vm); }
        audit.Add("QUESTION_UPDATED", "Assessment", q.AssessmentId, Res.Ar("Question {0}", q.Id));
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions), new { id = q.AssessmentId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        var q = await db.Questions.FindAsync(id);
        if (q == null) return NotFound();
        if (await Locked(q.AssessmentId)) { Failure("Questions are locked because attempts exist."); return RedirectToAction(nameof(Questions), new { id = q.AssessmentId }); }
        audit.Add("QUESTION_DELETED", "Assessment", q.AssessmentId, Res.Ar("Question {0}", q.Id));
        db.Questions.Remove(q);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions), new { id = q.AssessmentId });
    }

    private bool Apply(Question q, QuestionFormVm vm) => ContentAuthoring.ApplyQuestion(q, vm, ModelState, L);
}
