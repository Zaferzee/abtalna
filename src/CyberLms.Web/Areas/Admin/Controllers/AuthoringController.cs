using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>
/// "Create learning material" wizard: one guided flow over the existing Content, Attachment, Acknowledgment, Assessment and Question
/// records (basics -> write -> acknowledgment -> assessment -> preview -> publish). Every step saves to the database, so the item can be
/// left as a draft at any point. Publishing uses the same rules as the rest of the application.
/// </summary>
public class AuthoringController(AppDbContext db, ContentAuthoring authoring, AuditService audit, StorageService storage) : AdminController
{
    // ---------------------------------------------------------------- helpers

    private Task<Content?> Load(int id, bool track = false)
    {
        var q = db.Contents.Include(c => c.Attachments).Where(c => c.Id == id);
        return track ? q.FirstOrDefaultAsync() : q.AsNoTracking().FirstOrDefaultAsync();
    }

    private async Task<T> Fill<T>(T vm, Content c) where T : AuthorPageVm
    {
        vm.Content = c;
        vm.Assessment = await authoring.PrimaryAssessment(c.Id);
        return vm;
    }

    /// <summary>Where to go after saving a step: next / prev / stay / draft (back to the list) / step:N.</summary>
    private IActionResult Go(Content c, string? go, AuthorStep current)
    {
        var target = go switch
        {
            "next" => current + 1,
            "prev" => current - 1,
            { } s when s.StartsWith("step:") && int.TryParse(s[5..], out var n) && Enum.IsDefined(typeof(AuthorStep), n) => (AuthorStep)n,
            _ => current,
        };
        if (go == "draft")
        {
            Success(c.Status == ContentStatus.Published ? "Saved. The changes are already visible to employees." : "Saved as draft. You can continue later from Content Management.");
            return RedirectToAction("Index", "Content");
        }
        if (go == "stay") Success("Saved.");
        return RedirectToAction(target.ToString(), new { id = c.Id });
    }

    // ---------------------------------------------------------------- step 1: basics

    public IActionResult New() => View("Basics", new BasicsStepVm());

    public async Task<IActionResult> Basics(int id)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        return View(await Fill(new BasicsStepVm { Title = c.Title, Type = c.Type, Description = c.Description }, c));
    }

    [HttpPost]
    public async Task<IActionResult> Basics(int? id, BasicsStepVm vm, string? go)
    {
        Content? c = null;
        if (id is > 0) { c = await Load(id.Value, track: true); if (c == null) return NotFound(); }
        if (!Enum.IsDefined(vm.Type)) ModelState.AddModelError(nameof(vm.Type), L["Choose a content type."]);
        if (!ModelState.IsValid) return c == null ? View(vm) : View(await Fill(vm, c));
        if (c == null)
        {
            c = new Content { CreatedById = User.UserId(), Status = ContentStatus.Draft };
            db.Contents.Add(c);
        }
        else c.UpdatedAt = DateTime.UtcNow;
        c.Title = vm.Title.Trim(); c.Type = vm.Type; c.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
        await db.SaveChangesAsync();
        audit.Add(id is > 0 ? "CONTENT_UPDATED" : "CONTENT_CREATED", "Content", c.Id, c.Title);
        await db.SaveChangesAsync();
        return Go(c, go ?? "next", AuthorStep.Basics);
    }

    // ---------------------------------------------------------------- step 2: write + attachments

    public async Task<IActionResult> Write(int id)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        return View(await Fill(new WriteStepVm { Body = c.Body, ExternalUrl = c.ExternalUrl }, c));
    }

    [HttpPost]
    public async Task<IActionResult> Write(int id, WriteStepVm vm, List<IFormFile> files, string? go)
    {
        var c = await Load(id, track: true);
        if (c == null) return NotFound();
        if (authoring.LinkError(vm.ExternalUrl) is string err) ModelState.AddModelError(nameof(vm.ExternalUrl), err);
        if (!ModelState.IsValid) return View(await Fill(vm, c));
        c.Body = ContentAuthoring.CleanBody(vm.Body);
        c.ExternalUrl = string.IsNullOrWhiteSpace(vm.ExternalUrl) ? null : vm.ExternalUrl.Trim();
        c.UpdatedAt = DateTime.UtcNow;
        var errors = await authoring.SaveFiles(c, files);
        audit.Add("CONTENT_UPDATED", "Content", c.Id, c.Title);
        await db.SaveChangesAsync();
        if (errors.Count > 0) { TempData["Error"] = string.Join(" ", errors); return RedirectToAction(nameof(Write), new { id }); }
        if (files.Any(f => f.Length > 0) && go is null or "stay") { Success("Files uploaded."); return RedirectToAction(nameof(Write), new { id }); }
        return Go(c, go ?? "next", AuthorStep.Write);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveAttachment(int id, int attachmentId)
    {
        var a = await db.ContentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.ContentId == id);
        if (a == null) return NotFound();
        audit.Add("ATTACHMENT_DELETED", "Content", id, a.FileName);
        db.ContentAttachments.Remove(a);
        await db.SaveChangesAsync();
        storage.Delete(a.StoredPath);
        Success("File removed.");
        return RedirectToAction(nameof(Write), new { id });
    }

    // ---------------------------------------------------------------- step 3: acknowledgment

    private Task<int> AckCount(Content c) => db.UserAcknowledgments.CountAsync(a => a.ContentId == c.Id && a.ContentVersion == c.Version);

    public async Task<IActionResult> Acknowledgment(int id)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        var suggested = authoring.SuggestedAck(c.Title);
        return View(await Fill(new AckStepVm
        {
            RequiresAcknowledgment = c.RequiresAcknowledgment, Suggested = suggested, Acknowledged = await AckCount(c),
            // A new item starts with the suggested statement; an existing one shows what employees currently see.
            Statement = c.AcknowledgmentText ?? (c.RequiresAcknowledgment ? authoring.AckText(c) : suggested),
        }, c));
    }

    [HttpPost]
    public async Task<IActionResult> Acknowledgment(int id, AckStepVm vm, string? go)
    {
        var c = await Load(id, track: true);
        if (c == null) return NotFound();
        if (vm.RequiresAcknowledgment && string.IsNullOrWhiteSpace(vm.Statement)) vm.Statement = authoring.SuggestedAck(c.Title);
        if (!ModelState.IsValid)
        {
            vm.Suggested = authoring.SuggestedAck(c.Title); vm.Acknowledged = await AckCount(c);
            return View(await Fill(vm, c));
        }
        c.RequiresAcknowledgment = vm.RequiresAcknowledgment;
        if (vm.RequiresAcknowledgment)
        {
            var text = vm.Statement!.Trim();
            // Store nothing when the statement equals the built-in default, so the default keeps following the interface language.
            c.AcknowledgmentText = text == L[ContentAuthoring.DefaultAckKey] ? null : text;
        }
        c.UpdatedAt = DateTime.UtcNow;
        audit.Add("CONTENT_UPDATED", "Content", c.Id, c.Title);
        await db.SaveChangesAsync();
        return Go(c, go ?? "next", AuthorStep.Acknowledgment);
    }

    // ---------------------------------------------------------------- step 4: assessment + questions

    private async Task<AssessmentStepVm> BuildAssessment(Content c, AssessmentStepVm? vm = null, int? edit = null)
    {
        vm ??= new AssessmentStepVm();
        await Fill(vm, c);
        var a = vm.Assessment;
        vm.Others = await db.Assessments.AsNoTracking().Where(x => x.ContentId == c.Id && (a == null || x.Id != a.Id)).OrderBy(x => x.Id).ToListAsync();
        if (a == null)
        {
            if (vm.Settings.Id == 0 && string.IsNullOrEmpty(vm.Settings.Title))
                vm.Settings = new AssessmentFormVm { Title = L.Format("Assessment: {0}", c.Title), PassingPercentage = 80, MaxAttempts = 2, ContentId = c.Id };
            return vm;
        }
        vm.Include = true;
        if (vm.Settings.Id == 0 && string.IsNullOrEmpty(vm.Settings.Title))
            vm.Settings = new AssessmentFormVm { Id = a.Id, Title = a.Title, Description = a.Description, PassingPercentage = a.PassingPercentage, MaxAttempts = a.MaxAttempts, ContentId = c.Id };
        vm.Questions = a.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();
        vm.Attempts = await db.AssessmentAttempts.CountAsync(t => t.AssessmentId == a.Id);
        vm.Locked = vm.Attempts > 0;
        if (edit is int qid && vm.Questions.FirstOrDefault(q => q.Id == qid) is { } eq && !vm.Locked) { vm.Question = ContentAuthoring.FormFor(eq); vm.EditorOpen = true; }
        else if (vm.Question.Id == 0 && string.IsNullOrEmpty(vm.Question.Text)) vm.Question = new QuestionFormVm { AssessmentId = a.Id, SortOrder = (vm.Questions.Max(q => (int?)q.SortOrder) ?? 0) + 1 };
        return vm;
    }

    public async Task<IActionResult> Assessment(int id, int? edit)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        var vm = await BuildAssessment(c, null, edit);
        if (Request.Query.ContainsKey("add") && !vm.Locked) vm.EditorOpen = true;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> AssessmentSettings(int id, bool include, [Bind(Prefix = "Settings")] AssessmentFormVm settings, string? go)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        var a = await db.Assessments.Where(x => x.ContentId == id).OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (!include)
        {
            if (a != null)
            {
                if (await authoring.QuestionsLocked(a.Id))
                {
                    Failure("This assessment already has attempts, so it cannot be removed. Unpublish it from the Assessments page instead.");
                    return RedirectToAction(nameof(Assessment), new { id });
                }
                audit.Add("ASSESSMENT_DELETED", "Assessment", a.Id, a.Title);
                db.Assessments.Remove(a);
                await db.SaveChangesAsync();
            }
            return Go(c, go ?? "next", AuthorStep.Assessment);
        }
        if (!ModelState.IsValid)
            return View(nameof(Assessment), await BuildAssessment(c, new AssessmentStepVm { Include = true, Settings = settings }));
        var created = a == null;
        a ??= new Assessment { ContentId = id };
        a.Title = settings.Title.Trim(); a.Description = string.IsNullOrWhiteSpace(settings.Description) ? null : settings.Description.Trim();
        a.PassingPercentage = settings.PassingPercentage; a.MaxAttempts = settings.MaxAttempts;
        if (created) db.Assessments.Add(a); else a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        audit.Add(created ? "ASSESSMENT_CREATED" : "ASSESSMENT_UPDATED", "Assessment", a.Id, a.Title);
        await db.SaveChangesAsync();
        if (created && go is null or "stay")
        {
            Success("Assessment created. Now add its questions.");
            return Redirect(Url.Action(nameof(Assessment), new { id, add = 1 }) + "#questions");
        }
        return Go(c, go ?? "next", AuthorStep.Assessment);
    }

    [HttpPost]
    public async Task<IActionResult> SaveQuestion(int id, [Bind(Prefix = "Question")] QuestionFormVm q)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        var a = await db.Assessments.Where(x => x.ContentId == id).OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (a == null) return RedirectToAction(nameof(Assessment), new { id });
        if (await authoring.QuestionsLocked(a.Id)) { Failure("This assessment already has attempts; its questions are locked. Duplicate it to change questions."); return RedirectToAction(nameof(Assessment), new { id }); }

        Question? question;
        if (q.Id > 0)
        {
            question = await db.Questions.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == q.Id && x.AssessmentId == a.Id);
            if (question == null) return NotFound();
            db.QuestionOptions.RemoveRange(question.Options);
            question.Options.Clear();
            q.SortOrder = question.SortOrder;
        }
        else
        {
            question = new Question { AssessmentId = a.Id };
            q.SortOrder = (await db.Questions.Where(x => x.AssessmentId == a.Id).MaxAsync(x => (int?)x.SortOrder) ?? 0) + 1;
        }
        q.AssessmentId = a.Id;
        if (!ContentAuthoring.ApplyQuestion(question, q, ModelState, L))
        {
            db.ChangeTracker.Clear();
            return View(nameof(Assessment), await BuildAssessment(c, new AssessmentStepVm { Question = q, EditorOpen = true }));
        }
        if (q.Id == 0) db.Questions.Add(question);
        audit.Add(q.Id > 0 ? "QUESTION_UPDATED" : "QUESTION_ADDED", "Assessment", a.Id, question.Text.Length > 100 ? question.Text[..100] : question.Text);
        await db.SaveChangesAsync();
        Success(q.Id > 0 ? "Question updated." : "Question added.");
        // Fast entry: after adding, the editor opens again, empty, for the next question.
        return Redirect(Url.Action(nameof(Assessment), q.Id > 0 ? new { id } : new { id, add = 1 }) + $"#q-{question.Id}");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteQuestion(int id, int questionId)
    {
        var q = await db.Questions.Include(x => x.Assessment).FirstOrDefaultAsync(x => x.Id == questionId && x.Assessment.ContentId == id);
        if (q == null) return NotFound();
        if (await authoring.QuestionsLocked(q.AssessmentId)) { Failure("Questions are locked because attempts exist."); return RedirectToAction(nameof(Assessment), new { id }); }
        audit.Add("QUESTION_DELETED", "Assessment", q.AssessmentId, Res.Ar("Question {0}", q.Id));
        db.Questions.Remove(q);
        await db.SaveChangesAsync();
        Success("Question deleted.");
        return Redirect(Url.Action(nameof(Assessment), new { id }) + "#questions");
    }

    [HttpPost]
    public async Task<IActionResult> MoveQuestion(int id, int questionId, int dir)
    {
        var q = await db.Questions.Include(x => x.Assessment).FirstOrDefaultAsync(x => x.Id == questionId && x.Assessment.ContentId == id);
        if (q == null) return NotFound();
        if (await authoring.QuestionsLocked(q.AssessmentId)) { Failure("Questions are locked because attempts exist."); return RedirectToAction(nameof(Assessment), new { id }); }
        var all = await db.Questions.Where(x => x.AssessmentId == q.AssessmentId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
        var i = all.FindIndex(x => x.Id == questionId);
        var j = i + Math.Sign(dir);
        if (j >= 0 && j < all.Count) (all[i], all[j]) = (all[j], all[i]);
        for (var k = 0; k < all.Count; k++) all[k].SortOrder = k + 1; // also repairs gaps/duplicates
        audit.Add("QUESTION_UPDATED", "Assessment", q.AssessmentId, Res.Ar("Question {0}", q.Id));
        await db.SaveChangesAsync();
        return Redirect(Url.Action(nameof(Assessment), new { id }) + $"#q-{questionId}");
    }

    // ---------------------------------------------------------------- step 5: preview

    public async Task<IActionResult> Preview(int id)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        var vm = await Fill(new PreviewStepVm(), c);
        var related = new List<AssessmentListItem>();
        if (vm.Assessment is { } a)
            related.Add(new AssessmentListItem { Assessment = a, QuestionCount = a.Questions.Count, CanStart = true });
        vm.Reader = new ContentDetailsVm { Content = c, Interactive = false, Preview = c.Status != ContentStatus.Published, AckText = authoring.AckText(c), Related = related };
        return View(vm);
    }

    // ---------------------------------------------------------------- step 6: summary + publish

    public async Task<IActionResult> Publish(int id)
    {
        var c = await Load(id);
        if (c == null) return NotFound();
        var vm = await Fill(new PublishStepVm(), c);
        vm.AckText = authoring.AckText(c);
        vm.HasBody = !string.IsNullOrWhiteSpace(c.Body);
        vm.Attachments = c.Attachments.Count + (string.IsNullOrEmpty(c.ExternalUrl) ? 0 : 1);
        vm.Acknowledged = await AckCount(c);
        if (vm.Assessment != null) vm.Attempts = await db.AssessmentAttempts.CountAsync(t => t.AssessmentId == vm.Assessment.Id && t.Status == AttemptStatus.Completed);
        Check(vm);
        return View(vm);
    }

    private void Check(PublishStepVm vm)
    {
        if (vm.Assessment != null && vm.QuestionCount == 0) vm.Blockers.Add(L["The assessment has no questions yet. Add questions, or choose \"No assessment\"."]);
        if (!vm.HasBody && vm.Attachments == 0) vm.Warnings.Add(L["The content has no text and no attachments."]);
        if (string.IsNullOrWhiteSpace(vm.Content!.Description)) vm.Warnings.Add(L["There is no short description; it is shown in lists and on the dashboard."]);
    }

    [HttpPost]
    public async Task<IActionResult> Publish(int id, string go, string? back)
    {
        var c = await Load(id, track: true);
        if (c == null) return NotFound();
        var a = await authoring.PrimaryAssessment(id, track: true);
        IActionResult Done() => back == "list" ? RedirectToAction("Index", "Content") : RedirectToAction(nameof(Publish), new { id });
        switch (go)
        {
            case "publish":
                if (a != null && a.Questions.Count == 0) { Failure("The assessment has no questions yet. Add questions, or choose \"No assessment\"."); return RedirectToAction(nameof(Publish), new { id }); }
                if (c.Status != ContentStatus.Published) authoring.Publish(c);
                if (a != null && !a.IsPublished) { a.IsPublished = true; audit.Add("ASSESSMENT_PUBLISHED", "Assessment", a.Id, a.Title); }
                await db.SaveChangesAsync();
                Success("Published. Employees can see it now.");
                return Done();
            case "unpublish":
                if (c.Status == ContentStatus.Published) authoring.Unpublish(c);
                // The item's assessment is withdrawn with it (attempts and results are kept).
                if (a != null && a.IsPublished) { a.IsPublished = false; audit.Add("ASSESSMENT_UNPUBLISHED", "Assessment", a.Id, a.Title); }
                await db.SaveChangesAsync();
                Success("Unpublished. Employees no longer see it; acknowledgments and results are kept.");
                return Done();
            default:
                return Go(c, "draft", AuthorStep.Publish);
        }
    }

    // ---------------------------------------------------------------- copy

    [HttpPost]
    public async Task<IActionResult> Copy(int id)
    {
        if (!await db.Contents.AnyAsync(c => c.Id == id)) return NotFound();
        var copy = await authoring.CopyAsync(id, User.UserId());
        Success("A copy was created as a draft. Review it before publishing.");
        return RedirectToAction(nameof(Basics), new { id = copy.Id });
    }
}
