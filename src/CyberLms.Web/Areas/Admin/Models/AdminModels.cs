using System.ComponentModel.DataAnnotations;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;

namespace CyberLms.Web.Areas.Admin.Models;

public class ContentFormVm
{
    public int Id { get; set; }
    [Required, StringLength(300), Display(Name = "Title")] public string Title { get; set; } = "";
    [StringLength(2000), Display(Name = "Description")] public string? Description { get; set; }
    public string? Body { get; set; }
    public ContentType Type { get; set; } = ContentType.General;
    public bool RequiresAcknowledgment { get; set; }
    [StringLength(1000), Display(Name = "Internal link / video URL")] public string? ExternalUrl { get; set; }
    public ContentStatus Status { get; set; }
    public List<ContentAttachment> Attachments { get; set; } = new();
}

public class ContentListVm
{
    public List<ContentRow> Items { get; set; } = new();
    public string? Q { get; set; }
    public ContentType? Type { get; set; }
    public ContentStatus? Status { get; set; }
    public Pager Pager { get; set; } = new();
}
public record ContentRow(Content Content, int Acked, int? AssessmentId = null, string? AssessmentTitle = null, int Questions = 0, bool AssessmentPublished = false);

public class AssessmentFormVm
{
    public int Id { get; set; }
    [Required, StringLength(300), Display(Name = "Title")] public string Title { get; set; } = "";
    [StringLength(2000), Display(Name = "Description")] public string? Description { get; set; }
    public int? ContentId { get; set; }
    [Range(0, 100), Display(Name = "Passing percentage")] public int PassingPercentage { get; set; } = 70;
    [Range(0, 100), Display(Name = "Max attempts (0 = unlimited)")] public int MaxAttempts { get; set; } = 1;
}

public class AssessmentRowVm { public Assessment A { get; set; } = null!; public int Questions { get; set; } public int Attempts { get; set; } public string? ContentTitle { get; set; } }

public class QuestionFormVm
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    [Required, StringLength(2000), Display(Name = "Question text")] public string Text { get; set; } = "";
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    [Range(1, 1000), Display(Name = "Points")] public int Points { get; set; } = 1;
    public int SortOrder { get; set; }
    public List<string?> Options { get; set; } = Enumerable.Repeat<string?>(null, 8).ToList();
    public List<int> Correct { get; set; } = new();
    public bool TrueIsCorrect { get; set; } = true;
}

public class QuestionsVm { public Assessment Assessment { get; set; } = null!; public List<Question> Questions { get; set; } = new(); public int Attempts { get; set; } }

public class UserFormVm
{
    public int Id { get; set; }
    [Required, StringLength(256), Display(Name = "Username")] public string Username { get; set; } = "";
    [Required, StringLength(256), Display(Name = "Display name")] public string DisplayName { get; set; } = "";
    [EmailAddress, StringLength(256), Display(Name = "Email")] public string? Email { get; set; }
    [StringLength(256), Display(Name = "Department")] public string? Department { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public string AuthSource { get; set; } = "Local";
    [DataType(DataType.Password), Display(Name = "Password")] public string? Password { get; set; }
}

public class UserListVm
{
    public List<User> Items { get; set; } = new();
    public string? Q { get; set; }
    public string? Role { get; set; }
    public Pager Pager { get; set; } = new();
}

public class ReportVm
{
    public ReportFilter Filter { get; set; } = new();
    public Pager Pager { get; set; } = new();
    public List<AssessmentRow> Assessments { get; set; } = new();
    public List<AckRow> Acks { get; set; } = new();
    public List<AttemptRow> Attempts { get; set; } = new();
    public List<Assessment> AllAssessments { get; set; } = new();
    public List<Content> AllContent { get; set; } = new();
}

public class DashboardVm { public ReportService.Summary S { get; set; } = null!; public List<AuditLog> Recent { get; set; } = new(); }

public class AttemptDetailVm
{
    public AssessmentAttempt Attempt { get; set; } = null!;
    public List<Question> Questions { get; set; } = new();
    public Dictionary<int, AssessmentAnswer> Answers { get; set; } = new();
}

public class SettingsVm
{
    // General
    public string? BaseUrl { get; set; }
    public string DefaultLanguage { get; set; } = "en";
    // Branding
    public string OrgName { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string PrimaryColor { get; set; } = "";
    public string SecondaryColor { get; set; } = "";
    public string AccentColor { get; set; } = "";
    public string HeaderColor { get; set; } = "";
    public string HeaderTextColor { get; set; } = "";
    public string SidebarColor { get; set; } = "";
    public string SidebarTextColor { get; set; } = "";
    public string LoginTitle { get; set; } = "";
    public string? LoginSubtitle { get; set; }
    public string LoginBackgroundColor { get; set; } = "";
    public string? WelcomeText { get; set; }
    public string? FooterText { get; set; }
    public bool HasLogo { get; set; }
    public bool HasFavicon { get; set; }
    public bool HasLoginBackground { get; set; }
    // SMTP
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 25;
    public string? SmtpSender { get; set; }
    public string? SmtpSenderName { get; set; }
    public string? SmtpUsername { get; set; }
    public string SmtpSecurity { get; set; } = "Auto";
    public bool SmtpPasswordConfigured { get; set; }
}
