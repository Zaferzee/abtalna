using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Services;

/// <summary>Per-user assessment status (attempts, best result, whether a new attempt may start, and the acknowledgment lock).</summary>
public static class AssessmentQueries
{
    public static async Task<List<AssessmentListItem>> ForUser(AppDbContext db, int userId, List<Assessment> list)
    {
        var ids = list.Select(a => a.Id).ToList();
        var attempts = await db.AssessmentAttempts.AsNoTracking().Where(a => a.UserId == userId && ids.Contains(a.AssessmentId)).ToListAsync();
        var pendingAck = await AckGate.PendingContentIds(db, userId, list.Select(a => a.ContentId));
        var counts = await db.Questions.Where(q => ids.Contains(q.AssessmentId)).GroupBy(q => q.AssessmentId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return list.Select(a =>
        {
            var mine = attempts.Where(t => t.AssessmentId == a.Id).ToList();
            var done = mine.Where(t => t.Status == AttemptStatus.Completed).ToList();
            var inProg = mine.Any(t => t.Status == AttemptStatus.InProgress);
            return new AssessmentListItem
            {
                Assessment = a, QuestionCount = counts.GetValueOrDefault(a.Id), CompletedAttempts = done.Count, HasInProgress = inProg,
                Best = done.OrderByDescending(t => t.Percentage).FirstOrDefault(),
                CanStart = inProg || a.MaxAttempts == 0 || done.Count < a.MaxAttempts,
                FirstPassedAt = done.Where(t => t.Passed == true).Min(t => t.CompletedAt),
                LockedByAck = a.ContentId != null && pendingAck.Contains(a.ContentId.Value),
            };
        }).ToList();
    }
}
