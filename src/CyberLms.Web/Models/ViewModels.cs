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
    public List<AssessmentAttempt> Completed { get; set; } = new();
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
    public List<Assessment> RelatedAssessments { get; set; } = new();
    public bool Preview { get; set; }
}

public class AssessmentListItem
{
    public Assessment Assessment { get; set; } = null!;
    public int QuestionCount { get; set; }
    public int CompletedAttempts { get; set; }
    public bool HasInProgress { get; set; }
    public AssessmentAttempt? Best { get; set; }
    public bool CanStart { get; set; }
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
