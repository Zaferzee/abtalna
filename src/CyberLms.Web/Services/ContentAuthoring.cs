using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Ganss.Xss;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Services;

/// <summary>
/// Content/assessment authoring rules shared by the classic content form, the assessment pages and the authoring wizard,
/// so every entry point sanitizes, stores files, publishes and validates questions in exactly the same way.
/// </summary>
public class ContentAuthoring(AppDbContext db, StorageService storage, AuditService audit, Localizer L)
{
    public static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var s = new HtmlSanitizer();
        // Images may only reference files uploaded through the editor (served by FilesController.Inline); no external or data: images.
        s.FilterUrl += (_, e) => { if (string.Equals(e.Tag?.LocalName, "img", StringComparison.OrdinalIgnoreCase) && !(e.OriginalUrl ?? "").StartsWith("/Files/Inline/", StringComparison.Ordinal)) e.SanitizedUrl = null; };
        s.AllowedAttributes.Add("dir");
        // Tables (regulations, violation schedules): structure only. Cell spans are kept; no inline event/style tricks get through the sanitizer.
        foreach (var a in new[] { "colspan", "rowspan" }) s.AllowedAttributes.Add(a);
        return s;
    }

    public static string? CleanBody(string? html) => string.IsNullOrWhiteSpace(html) ? null : Sanitizer.Sanitize(html);

    /// <summary>Default acknowledgment statement shown when the content has none of its own.</summary>
    public const string DefaultAckKey = "I acknowledge that I have read and understood this policy.";
    /// <summary>Suggested statement for new content: "I acknowledge that I have read and understood {title}."</summary>
    public string SuggestedAck(string title) => L.Format("I acknowledge that I have read and understood {0}.", title.Trim());
    public string AckText(Content c) => string.IsNullOrWhiteSpace(c.AcknowledgmentText) ? L[DefaultAckKey] : c.AcknowledgmentText!;

    /// <summary>Returns a localized error when the link is not an http(s) URL or a site-relative path.</summary>
    public string? LinkError(string? url) =>
        string.IsNullOrWhiteSpace(url) || (Uri.TryCreate(url.Trim(), UriKind.RelativeOrAbsolute, out var u) && (!u.IsAbsoluteUri ? url.Trim().StartsWith('/') : u.Scheme is "http" or "https"))
            ? null : L["Enter an http(s) URL or a path starting with '/'."];

    public void Publish(Content c)
    {
        c.Status = ContentStatus.Published;
        c.PublishedAt ??= DateTime.UtcNow;
        audit.Add("CONTENT_PUBLISHED", "Content", c.Id, c.Title);
    }

    public void Unpublish(Content c)
    {
        c.Status = ContentStatus.Draft;
        audit.Add("CONTENT_UNPUBLISHED", "Content", c.Id, c.Title);
    }

    public async Task<List<string>> SaveFiles(Content c, IEnumerable<IFormFile>? files)
    {
        var errors = new List<string>();
        foreach (var f in (files ?? []).Where(f => f.Length > 0))
        {
            var (err, stored) = await storage.SaveAsync(f, $"content/{c.Id}");
            if (err != null) { errors.Add($"{f.FileName}: {L.Format(err.Key, err.Args)}"); continue; }
            db.ContentAttachments.Add(new ContentAttachment
            {
                ContentId = c.Id, FileName = stored!.OriginalName, StoredPath = stored.RelativePath, ContentType = stored.ContentType, SizeBytes = stored.Size, Kind = stored.Kind,
            });
            audit.Add("ATTACHMENT_ADDED", "Content", c.Id, stored.OriginalName);
        }
        return errors;
    }

    /// <summary>The assessment managed together with a content item (the first one linked to it).</summary>
    public Task<Assessment?> PrimaryAssessment(int contentId, bool track = false)
    {
        var q = db.Assessments.Include(a => a.Questions).ThenInclude(x => x.Options).Where(a => a.ContentId == contentId).OrderBy(a => a.Id);
        return track ? q.FirstOrDefaultAsync() : q.AsNoTracking().FirstOrDefaultAsync();
    }

    /// <summary>Questions are frozen once anyone has started the assessment, so historical results stay explainable.</summary>
    public Task<bool> QuestionsLocked(int assessmentId) => db.AssessmentAttempts.AnyAsync(t => t.AssessmentId == assessmentId);

    /// <summary>Validates the question form and fills the question + options. Returns false (with model errors) when invalid.</summary>
    public static bool ApplyQuestion(Question q, QuestionFormVm vm, ModelStateDictionary ms, Localizer L)
    {
        if (!ms.IsValid) return false;
        q.Text = vm.Text.Trim(); q.Type = vm.Type; q.Points = vm.Points; q.SortOrder = vm.SortOrder;
        q.Options = new List<QuestionOption>();
        if (vm.Type == QuestionType.TrueFalse)
        {
            q.Options.Add(new QuestionOption { Text = "True", IsCorrect = vm.TrueIsCorrect, SortOrder = 1 });
            q.Options.Add(new QuestionOption { Text = "False", IsCorrect = !vm.TrueIsCorrect, SortOrder = 2 });
            return true;
        }
        var order = 0;
        for (var i = 0; i < vm.Options.Count; i++)
        {
            var t = vm.Options[i]?.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            q.Options.Add(new QuestionOption { Text = t.Length > 1000 ? t[..1000] : t, IsCorrect = vm.Correct.Contains(i), SortOrder = ++order });
        }
        if (q.Options.Count < 2) ms.AddModelError("", L["Provide at least two answer options."]);
        var correct = q.Options.Count(o => o.IsCorrect);
        if (correct == 0) ms.AddModelError("", L["Mark at least one correct answer."]);
        if (vm.Type == QuestionType.SingleChoice && correct > 1) ms.AddModelError("", L["Single choice questions must have exactly one correct answer."]);
        return ms.IsValid;
    }

    /// <summary>Fills a question form from an existing question (for editing).</summary>
    public static QuestionFormVm FormFor(Question q)
    {
        var opts = q.Options.OrderBy(o => o.SortOrder).ToList();
        var vm = new QuestionFormVm { Id = q.Id, AssessmentId = q.AssessmentId, Text = q.Text, Type = q.Type, Points = q.Points, SortOrder = q.SortOrder };
        if (q.Type == QuestionType.TrueFalse) vm.TrueIsCorrect = opts.FirstOrDefault()?.IsCorrect ?? true;
        else for (var i = 0; i < opts.Count && i < vm.Options.Count; i++) { vm.Options[i] = opts[i].Text; if (opts[i].IsCorrect) vm.Correct.Add(i); }
        return vm;
    }

    /// <summary>
    /// Copies a content item as a new draft: text, settings, attachments (files are duplicated in storage) and its assessment
    /// with all questions (unpublished). Acknowledgments and attempts are never copied.
    /// </summary>
    public async Task<Content> CopyAsync(int id, int userId)
    {
        var src = await db.Contents.AsNoTracking().Include(c => c.Attachments).FirstAsync(c => c.Id == id);
        var copy = new Content
        {
            Title = Cut(L.Format("{0} (copy)", src.Title), 300), Description = src.Description, Body = src.Body, Type = src.Type,
            RequiresAcknowledgment = src.RequiresAcknowledgment, AcknowledgmentText = src.AcknowledgmentText, ExternalUrl = src.ExternalUrl,
            Status = ContentStatus.Draft, CreatedById = userId,
        };
        db.Contents.Add(copy);
        await db.SaveChangesAsync();
        foreach (var a in src.Attachments.OrderBy(a => a.Id))
        {
            var rel = storage.Copy(a.StoredPath, $"content/{copy.Id}");
            if (rel == null) continue; // source file missing on disk: skip rather than create a broken attachment
            db.ContentAttachments.Add(new ContentAttachment { ContentId = copy.Id, FileName = a.FileName, StoredPath = rel, ContentType = a.ContentType, SizeBytes = a.SizeBytes, Kind = a.Kind });
        }
        var asm = await PrimaryAssessment(id);
        if (asm != null)
        {
            var na = new Assessment { Title = Cut(L.Format("{0} (copy)", asm.Title), 300), Description = asm.Description, ContentId = copy.Id, PassingPercentage = asm.PassingPercentage, MaxAttempts = asm.MaxAttempts };
            foreach (var q in asm.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id))
                na.Questions.Add(new Question { Text = q.Text, Type = q.Type, Points = q.Points, SortOrder = q.SortOrder, Options = q.Options.OrderBy(o => o.SortOrder).Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.IsCorrect, SortOrder = o.SortOrder }).ToList() });
            db.Assessments.Add(na);
        }
        audit.Add("CONTENT_CREATED", "Content", copy.Id, Res.Ar("Duplicate of {0}", id));
        await db.SaveChangesAsync();
        return copy;
    }

    private static string Cut(string s, int n) => s.Length > n ? s[..n] : s;
}
