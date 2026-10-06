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
}

/// <summary>Report queries. "Employees" are active users holding the User role.</summary>
public class ReportService(AppDbContext db)
{
    public const string Passed = "Passed", Failed = "Failed", NotAttempted = "NotAttempted";
    public const string Acknowledged = "Acknowledged", NotAcknowledged = "NotAcknowledged";

    private IQueryable<User> Employees() => db.Users.AsNoTracking().Where(u => u.IsActive && u.UserRoles.Any(r => r.Role.Name == RoleNames.User));

    private IQueryable<AssessmentRowQ> AssessmentQuery(ReportFilter f)
    {
        var users = Employees();
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
                 select new AssessmentRowQ
                 {
                     UserId = u.Id, Username = u.Username, DisplayName = u.DisplayName, Department = u.Department, Email = u.Email,
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
        public int UserId { get; set; } public string Username { get; set; } = ""; public string DisplayName { get; set; } = "";
        public string? Department { get; set; } public string? Email { get; set; }
        public int ContentId { get; set; } public string Content { get; set; } = ""; public DateTime? At { get; set; }
    }

    private IQueryable<AckRowQ> AckQuery(ReportFilter f)
    {
        var users = Employees();
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
                 select new AckRowQ { UserId = u.Id, Username = u.Username, DisplayName = u.DisplayName, Department = u.Department, Email = u.Email, ContentId = c.Id, Content = c.Title,
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
        var q = db.AssessmentAttempts.AsNoTracking().Where(a => a.Status == AttemptStatus.Completed);
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

    public async Task<Summary> SummaryAsync()
    {
        var none = new ReportFilter();
        var users = await Employees().CountAsync();
        var content = await db.Contents.CountAsync(c => c.Status == ContentStatus.Published);
        var assess = await db.Assessments.CountAsync(a => a.IsPublished);
        var aq = AssessmentQuery(none);
        var passed = await aq.CountAsync(x => x.StatusCode == 2);
        var failed = await aq.CountAsync(x => x.StatusCode == 1);
        var notAtt = await aq.CountAsync(x => x.StatusCode == 0);
        var kq = AckQuery(none);
        var ack = await kq.CountAsync(x => x.At != null);
        var notAck = await kq.CountAsync(x => x.At == null);
        return new Summary(users, content, assess, passed, failed, notAtt, ack, notAck);
    }
}
