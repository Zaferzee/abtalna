using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Controllers;

[Authorize]
public class AssessmentsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var uid = User.UserId();
        var list = await db.Assessments.AsNoTracking().Where(a => a.IsPublished && a.Questions.Any()).OrderBy(a => a.Title).ToListAsync();
        var attempts = await db.AssessmentAttempts.AsNoTracking().Where(a => a.UserId == uid).ToListAsync();
        var counts = await db.Questions.GroupBy(q => q.AssessmentId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var items = list.Select(a =>
        {
            var mine = attempts.Where(t => t.AssessmentId == a.Id).ToList();
            var done = mine.Where(t => t.Status == AttemptStatus.Completed).ToList();
            var inProg = mine.Any(t => t.Status == AttemptStatus.InProgress);
            return new AssessmentListItem
            {
                Assessment = a, QuestionCount = counts.GetValueOrDefault(a.Id), CompletedAttempts = done.Count, HasInProgress = inProg,
                Best = done.OrderByDescending(t => t.Percentage).FirstOrDefault(),
                CanStart = inProg || a.MaxAttempts == 0 || done.Count < a.MaxAttempts,
            };
        }).ToList();
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Start(int id)
    {
        var uid = User.UserId();
        var a = await db.Assessments.FirstOrDefaultAsync(x => x.Id == id && x.IsPublished && x.Questions.Any());
        if (a == null) return NotFound();
        var existing = await db.AssessmentAttempts.FirstOrDefaultAsync(t => t.UserId == uid && t.AssessmentId == id && t.Status == AttemptStatus.InProgress);
        if (existing != null) return RedirectToAction(nameof(Take), new { id = existing.Id });
        var done = await db.AssessmentAttempts.CountAsync(t => t.UserId == uid && t.AssessmentId == id && t.Status == AttemptStatus.Completed);
        if (a.MaxAttempts != 0 && done >= a.MaxAttempts) { TempData["Error"] = "Maximum attempts reached"; return RedirectToAction(nameof(Index)); }
        var attempt = new AssessmentAttempt { UserId = uid, AssessmentId = id, PassingPercentageSnapshot = a.PassingPercentage };
        db.AssessmentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Take), new { id = attempt.Id });
    }

    private Task<AssessmentAttempt?> OwnAttempt(int id) =>
        db.AssessmentAttempts.Include(a => a.Assessment).FirstOrDefaultAsync(a => a.Id == id && a.UserId == User.UserId());

    public async Task<IActionResult> Take(int id)
    {
        var attempt = await OwnAttempt(id);
        if (attempt == null) return NotFound();
        if (attempt.Status == AttemptStatus.Completed) return RedirectToAction(nameof(Result), new { id });
        var qs = await db.Questions.AsNoTracking().Include(q => q.Options).Where(q => q.AssessmentId == attempt.AssessmentId).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToListAsync();
        return View(new TakeVm { Attempt = attempt, Assessment = attempt.Assessment, Questions = qs });
    }

    [HttpPost]
    public async Task<IActionResult> Submit(int id)
    {
        var attempt = await OwnAttempt(id);
        if (attempt == null) return NotFound();
        if (attempt.Status == AttemptStatus.Completed) return RedirectToAction(nameof(Result), new { id });

        var questions = await db.Questions.Include(q => q.Options).Where(q => q.AssessmentId == attempt.AssessmentId).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToListAsync();
        var selections = new Dictionary<int, int[]>();
        foreach (var q in questions)
            selections[q.Id] = Request.Form[$"q_{q.Id}"].Select(v => int.TryParse(v, out var n) ? n : -1).Where(n => n > 0).ToArray();

        var r = Scoring.Score(questions, selections, attempt.PassingPercentageSnapshot);
        foreach (var qr in r.Questions)
            attempt.Answers.Add(new AssessmentAnswer { QuestionId = qr.QuestionId, SelectedOptionIds = qr.Selected, IsCorrect = qr.Correct, PointsAwarded = qr.Points });
        attempt.TotalQuestions = r.TotalQuestions; attempt.CorrectAnswers = r.CorrectAnswers;
        attempt.TotalPoints = r.TotalPoints; attempt.EarnedPoints = r.EarnedPoints;
        attempt.Percentage = r.Percentage; attempt.Passed = r.Passed;
        attempt.CompletedAt = DateTime.UtcNow; attempt.Status = AttemptStatus.Completed;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return RedirectToAction(nameof(Result), new { id }); } // double submit
        return RedirectToAction(nameof(Result), new { id });
    }

    public async Task<IActionResult> Result(int id)
    {
        var attempt = await OwnAttempt(id);
        if (attempt == null) return NotFound();
        if (attempt.Status != AttemptStatus.Completed) return RedirectToAction(nameof(Take), new { id });
        var answers = await db.AssessmentAnswers.AsNoTracking().Where(a => a.AttemptId == id).ToDictionaryAsync(a => a.QuestionId);
        var qs = await db.Questions.AsNoTracking().Include(q => q.Options).Where(q => answers.Keys.Contains(q.Id)).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToListAsync();
        var done = await db.AssessmentAttempts.CountAsync(t => t.UserId == attempt.UserId && t.AssessmentId == attempt.AssessmentId && t.Status == AttemptStatus.Completed);
        var a = attempt.Assessment;
        return View(new ResultVm { Attempt = attempt, Answers = answers, Questions = qs, CanRetry = a.IsPublished && attempt.Passed != true && (a.MaxAttempts == 0 || done < a.MaxAttempts) });
    }
}
