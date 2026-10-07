using System.ComponentModel.DataAnnotations;
using CyberLms.Web.Domain;

namespace CyberLms.Web.Models;

public class LoginVm
{
    [Required, Display(Name = "Username")] public string Username { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "Password")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
}

public class ChangePasswordVm
{
    [DataType(DataType.Password), Display(Name = "Current password")] public string? Current { get; set; }
    [Required, DataType(DataType.Password), Display(Name = "New password")] public string New { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(New)), Display(Name = "Confirm new password")] public string Confirm { get; set; } = "";
}

public class UserDashboardVm
{
    public List<Content> RecentContent { get; set; } = new();
    public List<Content> PendingAcks { get; set; } = new();
    public List<Assessment> AvailableAssessments { get; set; } = new();
    /// <summary>Content ids the user must acknowledge before the linked assessments unlock (see AckGate).</summary>
    public HashSet<int> PendingAckContentIds { get; set; } = new();
    public List<AssessmentAttempt> Completed { get; set; } = new();
    // Progress overview (read-only counts for the dashboard)
    public int RequiredTotal { get; set; }
    public int AssessmentsTotal { get; set; }
    public int AssessmentsPassed { get; set; }
    public int RequiredDone => Math.Max(0, RequiredTotal - PendingAcks.Count);
}

public class ContentIndexVm
{
    public List<Content> Items { get; set; } = new();
    public HashSet<int> AcknowledgedIds { get; set; } = new();
    public ContentType? Type { get; set; }
    public string? Q { get; set; }
    public bool PoliciesOnly { get; set; }
}

public class ContentDetailsVm
{
    public Content Content { get; set; } = null!;
    public UserAcknowledgment? Ack { get; set; }
    /// <summary>Assessments linked to this content, with the current user's status.</summary>
    public List<AssessmentListItem> Related { get; set; } = new();
    /// <summary>Administrator viewing unpublished content.</summary>
    public bool Preview { get; set; }
    /// <summary>False inside the authoring wizard's preview: buttons are shown exactly as employees see them but do nothing.</summary>
    public bool Interactive { get; set; } = true;
    public string AckText { get; set; } = "";
    /// <summary>The acknowledgment was recorded by the request that redirected here: show the success state and the unlock.</summary>
    public bool JustAcknowledged { get; set; }
}

public class AssessmentListItem
{
    public Assessment Assessment { get; set; } = null!;
    public int QuestionCount { get; set; }
    public int CompletedAttempts { get; set; }
    public bool HasInProgress { get; set; }
    public AssessmentAttempt? Best { get; set; }
    public bool CanStart { get; set; }
    /// <summary>The linked content requires acknowledgment and this user has not acknowledged it yet (see AckGate).</summary>
    public bool LockedByAck { get; set; }
}

public class TakeVm
{
    public AssessmentAttempt Attempt { get; set; } = null!;
    public Assessment Assessment { get; set; } = null!;
    public List<Question> Questions { get; set; } = new();
}

public class ResultVm
{
    public AssessmentAttempt Attempt { get; set; } = null!;
    public Dictionary<int, AssessmentAnswer> Answers { get; set; } = new();
    public List<Question> Questions { get; set; } = new();
    public bool CanRetry { get; set; }
}

public class Pager
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int Total { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
public class ErrorVm { public int Code { get; set; } public string RequestId { get; set; } = ""; }
