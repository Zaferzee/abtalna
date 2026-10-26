using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Services;

public record AssessmentRow(int UserId, string Username, string DisplayName, string? Department, string? Email, int AssessmentId, string Assessment, string Status, decimal? BestPercentage, int Attempts, DateTime? LastAttempt);
public record AckRow(int UserId, string Username, string DisplayName, string? Department, string? Email, int ContentId, string Content, string Status, DateTime? AcknowledgedAt);
public record AttemptRow(int AttemptId, string Username, string DisplayName, string Assessment, DateTime? Completed, int Correct, int Total, decimal Percentage, bool? Passed);

public class ReportFilter
{
    public int? AssessmentId { get; set; }
    public int? ContentId { get; set; }
    public string? Status { get; set; }
    public string? Q { get; set; }
    /// <summary>Drill-down: employees of this entity and its descendants.</summary>
    public int? EntityId { get; set; }
    /// <summary>Authorization scope (set by the controller from EntityAccess, never bound from the request).</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public EntityScope Scope { get; set; } = EntityScope.Nothing;
    /// <summary>Path of <see cref="EntityId"/> (resolved by the controller after the scope check).</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public string? EntityPath { get; set; }
}

public record EntityBreakdownRow(EntityNode Entity, int Employees, int AckDone, int AckTotal, int Passed, int Failed, int AssessTotal)
{
    public int NotAttempted => AssessTotal - Passed - Failed;
    public decimal? AckRate => AckTotal == 0 ? null : Math.Round(100m * AckDone / AckTotal, 1);
    public decimal? PassRate => AssessTotal == 0 ? null : Math.Round(100m * Passed / AssessTotal, 1);
    /// <summary>Requirements met: acknowledgments given + assessments passed, over all acknowledgment and assessment requirements.</summary>
    public decimal? CompletionRate => AckTotal + AssessTotal == 0 ? null : Math.Round(100m * (AckDone + Passed) / (AckTotal + AssessTotal), 1);
}

/// <summary>
/// Report queries. "Employees" are active users holding the User role, restricted to the caller's entity scope (and the optional
/// drill-down subtree). A user/item pair only exists when the item is targeted to the user's entity (same rule as the employee
/// portal), so central content distributed to some entities is reported only for those entities. Exports use the same queries.
/// </summary>
public class ReportService(AppDbContext db)
{
    public const string Passed = "Passed", Failed = "Failed", NotAttempted = "NotAttempted";
    public const string Acknowledged = "Acknowledged", NotAcknowledged = "NotAcknowledged";

    private IQueryable<User> Employees(ReportFilter f) =>
        db.Users.AsNoTracking().Where(u => u.IsActive && u.UserRoles.Any(r => r.Role.Name == RoleNames.User)).InScope(f.Scope).InSubtree(f.EntityPath);

    private IQueryable<AssessmentRowQ> AssessmentQuery(ReportFilter f)
    {
        var users = Employees(f);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var q = f.Q.Trim().ToLower();
            users = users.Where(u => u.NormalizedUsername.Contains(q) || u.DisplayName.ToLower().Contains(q) || (u.Email != null && u.Email.ToLower().Contains(q)));
        }
        var assessments = db.Assessments.AsNoTracking().Where(a => a.IsPublished);
        if (f.AssessmentId != null) assessments = assessments.Where(a => a.Id == f.AssessmentId);

        var attempts = db.AssessmentAttempts.AsNoTracking().Where(t => t.Status == AttemptStatus.Completed);
        var q2 = from u in users
                 from a in assessments
                 // audience: standalone -> owner subtree; linked -> the content's targeting (EntityVisibility, path form)
                 where (a.ContentId == null && u.Entity.Path.StartsWith(a.OwnerEntity.Path))
                    || (a.ContentId != null && (
                           (a.Content!.TargetScope == ContentTargetScope.OwnerOnly && a.Content.OwnerEntityId == u.EntityId)
                        || (a.Content.TargetScope == ContentTargetScope.OwnerAndDescendants && u.Entity.Path.StartsWith(a.Content.OwnerEntity.Path))
                        || (a.Content.TargetScope == ContentTargetScope.Selected && a.Content.Targets.Any(t => t.EntityId == u.EntityId || (t.IncludeDescendants && u.Entity.Path.StartsWith(t.Entity.Path))))))
                 select new AssessmentRowQ
                 {
                     EntityId = u.EntityId, UserId = u.Id, Username = u.Username, DisplayName = u.DisplayName, Department = u.Department, Email = u.Email,
                     AssessmentId = a.Id, Assessment = a.Title,
                     StatusCode = attempts.Any(t => t.UserId == u.Id && t.AssessmentId == a.Id && t.Passed == true) ? 2
                                : attempts.Any(t => t.UserId == u.Id && t.AssessmentId == a.Id) ? 1 : 0,
                     Best = attempts.Where(t => t.UserId == u.Id && t.AssessmentId == a.Id).Max(t => (decimal?)t.Percentage),
                     Attempts = attempts.Count(t => t.UserId == u.Id && t.AssessmentId == a.Id),
                     Last = attempts.Where(t => t.UserId == u.Id && t.AssessmentId == a.Id).Max(t => t.CompletedAt),
                 };
        if (f.Status == Passed) q2 = q2.Where(x => x.StatusCode == 2);
        else if (f.Status == Failed) q2 = q2.Where(x => x.StatusCode == 1);
        else if (f.Status == NotAttempted) q2 = q2.Where(x => x.StatusCode == 0);
        return q2.OrderBy(x => x.Assessment).ThenBy(x => x.DisplayName);
    }

    private class AssessmentRowQ
    {
        public int EntityId { get; set; }
        public int UserId { get; set; } public string Username { get; set; } = ""; public string DisplayName { get; set; } = "";
        public string? Department { get; set; } public string? Email { get; set; }
        public int AssessmentId { get; set; } public string Assessment { get; set; } = "";
        public int StatusCode { get; set; } public decimal? Best { get; set; } public int Attempts { get; set; } public DateTime? Last { get; set; }
    }

    private static AssessmentRow ToRow(AssessmentRowQ x) => new(x.UserId, x.Username, x.DisplayName, x.Department, x.Email, x.AssessmentId, x.Assessment,
        x.StatusCode == 2 ? Passed : x.StatusCode == 1 ? Failed : NotAttempted, x.Best, x.Attempts, x.Last);

    public async Task<(List<AssessmentRow> Rows, int Total)> AssessmentResultsAsync(ReportFilter f, int page, int pageSize)
    {
        var q = AssessmentQuery(f);
        var total = await q.CountAsync();
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows.Select(ToRow).ToList(), total);
    }

    public async Task<List<AssessmentRow>> AssessmentResultsAllAsync(ReportFilter f) => (await AssessmentQuery(f).ToListAsync()).Select(ToRow).ToList();

    private class AckRowQ
    {
        public int EntityId { get; set; }
        public int UserId { get; set; } public string Username { get; set; } = ""; public string DisplayName { get; set; } = "";
        public string? Department { get; set; } public string? Email { get; set; }
        public int ContentId { get; set; } public string Content { get; set; } = ""; public DateTime? At { get; set; }
    }

    private IQueryable<AckRowQ> AckQuery(ReportFilter f)
    {
        var users = Employees(f);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var q = f.Q.Trim().ToLower();
            users = users.Where(u => u.NormalizedUsername.Contains(q) || u.DisplayName.ToLower().Contains(q) || (u.Email != null && u.Email.ToLower().Contains(q)));
        }
        var contents = db.Contents.AsNoTracking().Where(c => c.Status == ContentStatus.Published && c.RequiresAcknowledgment);
        if (f.ContentId != null) contents = contents.Where(c => c.Id == f.ContentId);
        var acks = db.UserAcknowledgments.AsNoTracking();
        var q2 = from u in users
                 from c in contents
                 where (c.TargetScope == ContentTargetScope.OwnerOnly && c.OwnerEntityId == u.EntityId)
                    || (c.TargetScope == ContentTargetScope.OwnerAndDescendants && u.Entity.Path.StartsWith(c.OwnerEntity.Path))
                    || (c.TargetScope == ContentTargetScope.Selected && c.Targets.Any(t => t.EntityId == u.EntityId || (t.IncludeDescendants && u.Entity.Path.StartsWith(t.Entity.Path))))
                 select new AckRowQ { EntityId = u.EntityId, UserId = u.Id, Username = u.Username, DisplayName = u.DisplayName, Department = u.Department, Email = u.Email, ContentId = c.Id, Content = c.Title,
                     At = acks.Where(x => x.UserId == u.Id && x.ContentId == c.Id && x.ContentVersion == c.Version).Min(x => (DateTime?)x.AcknowledgedAt) };
        if (f.Status == Acknowledged) q2 = q2.Where(x => x.At != null);
        else if (f.Status == NotAcknowledged) q2 = q2.Where(x => x.At == null);
        return q2.OrderBy(x => x.Content).ThenBy(x => x.DisplayName);
    }

    private static AckRow ToRow(AckRowQ x) => new(x.UserId, x.Username, x.DisplayName, x.Department, x.Email, x.ContentId, x.Content, x.At != null ? Acknowledged : NotAcknowledged, x.At);

    public async Task<(List<AckRow> Rows, int Total)> AcknowledgmentsAsync(ReportFilter f, int page, int pageSize)
    {
        var q = AckQuery(f);
        var total = await q.CountAsync();
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (rows.Select(ToRow).ToList(), total);
    }

    public async Task<List<AckRow>> AcknowledgmentsAllAsync(ReportFilter f) => (await AckQuery(f).ToListAsync()).Select(ToRow).ToList();

    private IQueryable<AttemptRow> AttemptQuery(ReportFilter f)
    {
        var users = Employees(f).Select(u => u.Id);
        var q = db.AssessmentAttempts.AsNoTracking().Where(a => a.Status == AttemptStatus.Completed && users.Contains(a.UserId));
        if (f.AssessmentId != null) q = q.Where(a => a.AssessmentId == f.AssessmentId);
        if (f.Status == Passed) q = q.Where(a => a.Passed == true);
        else if (f.Status == Failed) q = q.Where(a => a.Passed == false);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var s = f.Q.Trim().ToLower();
            q = q.Where(a => a.User.NormalizedUsername.Contains(s) || a.User.DisplayName.ToLower().Contains(s));
        }
        return q.OrderByDescending(a => a.CompletedAt)
            .Select(a => new AttemptRow(a.Id, a.User.Username, a.User.DisplayName, a.Assessment.Title, a.CompletedAt, a.CorrectAnswers, a.TotalQuestions, a.Percentage, a.Passed));
    }

    public async Task<(List<AttemptRow> Rows, int Total)> AttemptsAsync(ReportFilter f, int page, int pageSize)
    {
        var q = AttemptQuery(f);
        return (await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), await q.CountAsync());
    }

    public Task<List<AttemptRow>> AttemptsAllAsync(ReportFilter f) => AttemptQuery(f).ToListAsync();

    public record Summary(int Users, int Content, int Assessments, int Passed, int Failed, int NotAttempted, int Acknowledged, int NotAcknowledged);

    public async Task<Summary> SummaryAsync(EntityScope scope)
    {
        var none = new ReportFilter { Scope = scope };
        var users = await Employees(none).CountAsync();
        var content = await db.Contents.InScope(scope).CountAsync(c => c.Status == ContentStatus.Published);
        var assess = await db.Assessments.InScope(scope).CountAsync(a => a.IsPublished);
        var aq = AssessmentQuery(none);
        var passed = await aq.CountAsync(x => x.StatusCode == 2);
        var failed = await aq.CountAsync(x => x.StatusCode == 1);
        var notAtt = await aq.CountAsync(x => x.StatusCode == 0);
        var kq = AckQuery(none);
        var ack = await kq.CountAsync(x => x.At != null);
        var notAck = await kq.CountAsync(x => x.At == null);
        return new Summary(users, content, assess, passed, failed, notAtt, ack, notAck);
    }

    /// <summary>
    /// Entity comparison: one row per entity (each aggregating its whole subtree): employees, acknowledgment, pass and completion
    /// rates. Counts are computed per user entity in SQL and rolled up along the materialized paths in memory.
    /// </summary>
    public async Task<List<EntityBreakdownRow>> EntityBreakdownAsync(ReportFilter f, IReadOnlyList<EntityNode> rows, IReadOnlyList<EntityNode> all)
    {
        var emp = await Employees(f).GroupBy(u => u.EntityId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var asm = await AssessmentQuery(f).GroupBy(x => x.EntityId)
            .Select(g => new { g.Key, Passed = g.Count(x => x.StatusCode == 2), Failed = g.Count(x => x.StatusCode == 1), Total = g.Count() }).ToListAsync();
        var ack = await AckQuery(f).GroupBy(x => x.EntityId).Select(g => new { g.Key, Done = g.Count(x => x.At != null), Total = g.Count() }).ToListAsync();
        string PathOf(int id) => all.FirstOrDefault(e => e.Id == id)?.Path ?? "";
        return rows.Select(r =>
        {
            bool In(int id) => PathOf(id).StartsWith(r.Path, StringComparison.Ordinal);
            return new EntityBreakdownRow(r, emp.Where(x => In(x.Key)).Sum(x => x.N),
                ack.Where(x => In(x.Key)).Sum(x => x.Done), ack.Where(x => In(x.Key)).Sum(x => x.Total),
                asm.Where(x => In(x.Key)).Sum(x => x.Passed), asm.Where(x => In(x.Key)).Sum(x => x.Failed), asm.Where(x => In(x.Key)).Sum(x => x.Total));
        }).ToList();
    }
}
