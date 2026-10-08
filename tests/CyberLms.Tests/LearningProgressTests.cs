using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;

namespace CyberLms.Tests;

/// <summary>The read-only learning state derived for the employee UI (journey steps, card status). No database involved.</summary>
public class LearningProgressTests
{
    private static Content C(bool ack) => new() { Id = 1, Title = "Policy", Status = ContentStatus.Published, RequiresAcknowledgment = ack, Version = 1 };
    private static UserAcknowledgment Ack(DateTime? at = null) => new() { ContentId = 1, UserId = 7, AcknowledgedAt = at ?? new DateTime(2026, 1, 1) };

    private static AssessmentListItem A(int completed = 0, bool inProgress = false, bool? passed = null, bool canStart = true, bool locked = false, DateTime? passedAt = null) => new()
    {
        Assessment = new Assessment { Id = 10, Title = "Quiz", ContentId = 1, IsPublished = true },
        CompletedAttempts = completed, HasInProgress = inProgress, CanStart = canStart, LockedByAck = locked,
        Best = passed == null ? null : new AssessmentAttempt { Passed = passed, Percentage = passed == true ? 90 : 40 },
        FirstPassedAt = passed == true ? passedAt ?? new DateTime(2026, 2, 1) : null,
    };

    private static string Steps(LearningItem i) => string.Join(" ", i.Steps.Select(s => $"{s.Key}:{s.State}"));

    [Fact]
    public void Informational_content_has_nothing_to_complete()
    {
        var i = LearningProgress.Build(C(false), null, []);
        Assert.Equal(LearningState.Available, i.State);
        Assert.False(i.HasRequirements);
    }

    [Fact]
    public void Acknowledgment_required_locks_the_assessment_and_reading_is_current()
    {
        var i = LearningProgress.Build(C(true), null, [A(locked: true)]);
        Assert.Equal(LearningState.AckRequired, i.State);
        Assert.Equal("content:Current ack:Upcoming assessment:Locked done:Upcoming", Steps(i));
        Assert.False(i.Started);
    }

    [Fact]
    public void After_acknowledgment_the_assessment_is_the_current_step()
    {
        var i = LearningProgress.Build(C(true), Ack(), [A()]);
        Assert.Equal(LearningState.AssessmentAvailable, i.State);
        Assert.Equal("content:Done ack:Done assessment:Current done:Upcoming", Steps(i));
    }

    [Fact]
    public void In_progress_failed_and_retry()
    {
        Assert.Equal(LearningState.AssessmentInProgress, LearningProgress.Build(C(true), Ack(), [A(inProgress: true)]).State);
        var retry = LearningProgress.Build(C(true), Ack(), [A(completed: 1, passed: false)]);
        Assert.Equal(LearningState.Failed, retry.State);
        Assert.True(retry.CanRetry);
        Assert.Equal("content:Done ack:Done assessment:Current done:Upcoming", Steps(retry));
        var noMore = LearningProgress.Build(C(true), Ack(), [A(completed: 2, passed: false, canStart: false)]);
        Assert.False(noMore.CanRetry);
        Assert.Contains("assessment:Failed", Steps(noMore));
    }

    [Fact]
    public void Completed_needs_acknowledgment_and_every_assessment_passed()
    {
        var done = LearningProgress.Build(C(true), Ack(new DateTime(2026, 1, 5)), [A(completed: 1, passed: true, passedAt: new DateTime(2026, 1, 9))]);
        Assert.Equal(LearningState.Completed, done.State);
        Assert.Equal(new DateTime(2026, 1, 9), done.CompletedAt);
        Assert.All(done.Steps, s => Assert.Equal(StepState.Done, s.State));
        // acknowledgment only
        Assert.Equal(LearningState.Completed, LearningProgress.Build(C(true), Ack(), []).State);
        // a pass recorded before the acknowledgment (attempt that existed before the gate) does not complete the item
        Assert.Equal(LearningState.AckRequired, LearningProgress.Build(C(true), null, [A(completed: 1, passed: true)]).State);
    }

    [Fact]
    public void Without_acknowledgment_the_assessment_follows_reading()
    {
        var i = LearningProgress.Build(C(false), null, [A()]);
        Assert.Equal(LearningState.AssessmentAvailable, i.State);
        Assert.Equal("content:Current assessment:Upcoming done:Upcoming", Steps(i));
        var standalone = LearningProgress.Build(null, null, [A()]);
        Assert.Equal("assessment:Current done:Upcoming", Steps(standalone));
        Assert.Equal("Quiz", standalone.Title);
    }
}
