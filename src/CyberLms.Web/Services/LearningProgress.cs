using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Services;

/// <summary>Where an employee stands on one learning item (derived from acknowledgments, attempts and reading completions).</summary>
public enum LearningState
{
    /// <summary>Reading item (no acknowledgment, no assessment) that the user has not marked as read yet.</summary>
    Available,
    /// <summary>The content requires acknowledgment and the user has not acknowledged it (any linked assessment is locked).</summary>
    AckRequired,
    /// <summary>Acknowledged (or not required); a linked assessment can be started.</summary>
    AssessmentAvailable,
    /// <summary>An attempt is in progress.</summary>
    AssessmentInProgress,
    /// <summary>Completed attempts, none passed yet (see <see cref="LearningItem.CanRetry"/>).</summary>
    Failed,
    /// <summary>Every requirement is met: acknowledged (if required) and every linked assessment passed; for a reading item,
    /// the user marked it as read («تمت القراءة»).</summary>
    Completed,
}

public enum StepState { Done, Current, Upcoming, Locked, Failed }

public record JourneyStep(string Key, StepState State);

/// <summary>
/// One learning item for one user: a published content item with its requirements, or a published assessment that is not linked
/// to published content. Read-only view model; the authoritative data stays in acknowledgments, attempts and reading completions.
/// </summary>
public class LearningItem
{
    public Content? Content { get; init; }
    public UserAcknowledgment? Ack { get; init; }
    /// <summary>The user's «تمت القراءة» record; only meaningful for a reading item.</summary>
    public ContentCompletion? Reading { get; init; }
    public List<AssessmentListItem> Assessments { get; init; } = [];
    public LearningState State { get; init; }
    public List<JourneyStep> Steps { get; init; } = [];
    public bool RequiresAck => Content?.RequiresAcknowledgment == true;
    /// <summary>Content with neither acknowledgment nor assessment: completed by the explicit «تمت القراءة» action.</summary>
    public bool IsReadingItem => Content != null && !RequiresAck && Assessments.Count == 0;
    /// <summary>The item has a completion path (acknowledgment, assessment, or reading completion), so it counts toward progress.</summary>
    public bool HasRequirements => IsReadingItem || RequiresAck || Assessments.Count > 0;
    /// <summary>The user has acknowledged, attempted or marked as read.</summary>
    public bool Started => Ack != null || Reading != null || Assessments.Any(a => a.CompletedAttempts > 0 || a.HasInProgress);
    public bool IsCompleted => State == LearningState.Completed;
    public DateTime? CompletedAt { get; init; }
    /// <summary>The assessment to take next (first one not passed yet).</summary>
    public AssessmentListItem? NextAssessment => Assessments.FirstOrDefault(a => a.Best?.Passed != true);
    public bool CanRetry => State == LearningState.Failed && NextAssessment?.CanStart == true;
    public string Title => Content?.Title ?? Assessments.FirstOrDefault()?.Assessment.Title ?? "";
    public int? ContentId => Content?.Id;
    /// <summary>Best passing score of the item's assessments (for completion summaries).</summary>
    public decimal? BestScore => Assessments.Where(a => a.Best != null).Select(a => (decimal?)a.Best!.Percentage).Max();
}

public static class LearningProgress
{
    /// <summary>Builds the item from data that is already loaded (no database access): content, the user's acknowledgment of its
    /// current version, the linked published assessments with the user's status, and (reading items only) the user's
    /// «تمت القراءة» record for the current version.</summary>
    public static LearningItem Build(Content? content, UserAcknowledgment? ack, List<AssessmentListItem> assessments, ContentCompletion? readRecord = null)
    {
        var requiresAck = content?.RequiresAcknowledgment == true;
        if (content != null && !requiresAck && assessments.Count == 0)
        {
            // Reading item: the explicit completion is the only completion evidence (opening the page is not enough).
            var read = readRecord != null;
            return new LearningItem
            {
                Content = content, Reading = readRecord, Assessments = assessments,
                State = read ? LearningState.Completed : LearningState.Available, CompletedAt = readRecord?.CompletedAt,
                Steps = [new("content", read ? StepState.Done : StepState.Current), new("done", read ? StepState.Done : StepState.Upcoming)],
            };
        }
        var acked = !requiresAck || ack != null;
        var passedAll = assessments.All(a => a.Best?.Passed == true);
        var hasReq = requiresAck || assessments.Count > 0;
        var next = assessments.FirstOrDefault(a => a.Best?.Passed != true);

        var state = !hasReq ? LearningState.Available
            : !acked ? LearningState.AckRequired
            : passedAll ? LearningState.Completed
            : next!.HasInProgress ? LearningState.AssessmentInProgress
            : next.CompletedAttempts > 0 ? LearningState.Failed
            : LearningState.AssessmentAvailable;

        DateTime? completedAt = null;
        if (state == LearningState.Completed)
        {
            var dates = assessments.Select(a => a.FirstPassedAt).Append(requiresAck ? ack!.AcknowledgedAt : null).Where(d => d != null).ToList();
            completedAt = dates.Count > 0 ? dates.Max() : null;
        }

        // Journey: content -> acknowledgment (if required) -> assessment (if any) -> completion.
        // Reading is "current" until the user acts (acknowledges or attempts); a step after an unmet acknowledgment is locked.
        var started = ack != null || assessments.Any(a => a.CompletedAttempts > 0 || a.HasInProgress);
        var reading = hasReq && !started && state != LearningState.Completed;
        var steps = new List<JourneyStep>();
        if (content != null) steps.Add(new("content", reading ? StepState.Current : StepState.Done));
        if (requiresAck) steps.Add(new("ack", ack != null ? StepState.Done : reading ? StepState.Upcoming : StepState.Current));
        if (assessments.Count > 0)
            steps.Add(new("assessment", passedAll ? StepState.Done
                : !acked ? StepState.Locked
                : state == LearningState.Failed && next?.CanStart != true ? StepState.Failed
                : reading && content != null ? StepState.Upcoming
                : StepState.Current));
        if (hasReq) steps.Add(new("done", state == LearningState.Completed ? StepState.Done : StepState.Upcoming));

        return new LearningItem { Content = content, Ack = requiresAck ? ack : null, Assessments = assessments, State = state, Steps = steps, CompletedAt = completedAt };
    }

    /// <summary>All learning items of the user: published content (with its published assessments) plus published assessments that
    /// are not linked to published content. Optionally restricted to one content item.</summary>
    public static async Task<List<LearningItem>> ForUser(AppDbContext db, int userId, int? contentId = null)
    {
        var contents = await db.Contents.AsNoTracking()
            .Where(c => c.Status == ContentStatus.Published && (contentId == null || c.Id == contentId))
            .OrderByDescending(c => c.PublishedAt).Take(1000).ToListAsync();
        var cids = contents.Select(c => c.Id).ToList();
        var asmQuery = db.Assessments.AsNoTracking().Where(a => a.IsPublished && a.Questions.Any());
        asmQuery = contentId == null ? asmQuery : asmQuery.Where(a => a.ContentId == contentId);
        var assessments = await asmQuery.OrderBy(a => a.Id).ToListAsync();
        var status = await AssessmentQueries.ForUser(db, userId, assessments);
        var acks = await db.UserAcknowledgments.AsNoTracking().Where(a => a.UserId == userId && cids.Contains(a.ContentId)).ToListAsync();
        var reads = await db.ContentCompletions.AsNoTracking().Where(r => r.UserId == userId && cids.Contains(r.ContentId)).ToListAsync();

        var items = contents.Select(c => Build(c,
            acks.FirstOrDefault(a => a.ContentId == c.Id && a.ContentVersion == c.Version),
            status.Where(s => s.Assessment.ContentId == c.Id).ToList(),
            reads.FirstOrDefault(r => r.ContentId == c.Id && r.ContentVersion == c.Version))).ToList();
        if (contentId == null)
            items.AddRange(status.Where(s => s.Assessment.ContentId == null || !cids.Contains(s.Assessment.ContentId.Value))
                .Select(s => Build(null, null, [s])));
        return items;
    }

    /// <summary>Order for "what should I do next": started items first, then acknowledgment-required, then the rest;
    /// reading items after the mandatory ones; completed last.</summary>
    public static int Priority(LearningItem i) => i.State switch
    {
        LearningState.AssessmentInProgress => 0,
        LearningState.AssessmentAvailable when i.Ack != null => 1,
        LearningState.Failed when i.CanRetry => 2,
        LearningState.AckRequired => 3,
        LearningState.AssessmentAvailable => 4,
        LearningState.Failed => 6,
        LearningState.Available => 7,
        LearningState.Completed => 8,
        _ => 9,
    };
}
