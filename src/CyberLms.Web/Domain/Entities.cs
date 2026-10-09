namespace CyberLms.Web.Domain;

public enum ContentType { Policy = 1, Control = 2, Awareness = 3, Training = 4, Procedure = 5, General = 6, Regulation = 7, Instructions = 8 }
public enum ContentStatus { Draft = 0, Published = 1 }
public enum AttachmentKind { Image = 1, Video = 2, Document = 3 }
public enum QuestionType { SingleChoice = 1, TrueFalse = 2, MultipleChoice = 3 }
public enum AttemptStatus { InProgress = 0, Completed = 1 }

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public class User
{
    public int Id { get; set; }
    /// <summary>Login name. For Windows auth this is DOMAIN\user (stored as given).</summary>
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Department { get; set; }
    /// <summary>Null for accounts that authenticate externally (AD).</summary>
    public string? PasswordHash { get; set; }
    /// <summary>Identity provider key (e.g. AD objectSid / UPN). Null for local accounts.</summary>
    public string? ExternalId { get; set; }
    public string AuthSource { get; set; } = "Local";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public List<UserRole> UserRoles { get; set; } = new();
}

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<UserRole> UserRoles { get; set; } = new();
}

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public class Content
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Sanitized HTML.</summary>
    public string? Body { get; set; }
    public ContentType Type { get; set; } = ContentType.General;
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public bool RequiresAcknowledgment { get; set; }
    /// <summary>Statement the employee confirms when acknowledging. Null = the default statement.</summary>
    public string? AcknowledgmentText { get; set; }
    /// <summary>Reserved for future re-acknowledgment; acknowledgments record the version they were given for.</summary>
    public int Version { get; set; } = 1;
    public string? ExternalUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public List<ContentAttachment> Attachments { get; set; } = new();
}

public class ContentAttachment
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public Content Content { get; set; } = null!;
    public string FileName { get; set; } = "";
    /// <summary>Path relative to the configured storage root.</summary>
    public string StoredPath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public AttachmentKind Kind { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class UserAcknowledgment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ContentId { get; set; }
    public Content Content { get; set; } = null!;
    public int ContentVersion { get; set; } = 1;
    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Acknowledged";
}

/// <summary>
/// "تمت القراءة": the employee's explicit completion of a content item that has neither an acknowledgment nor an assessment
/// (which would otherwise be the completion evidence). Recorded for the content version that was read.
/// </summary>
public class ContentCompletion
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ContentId { get; set; }
    public Content Content { get; set; } = null!;
    public int ContentVersion { get; set; } = 1;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class Assessment
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? ContentId { get; set; }
    public Content? Content { get; set; }
    public int PassingPercentage { get; set; } = 70;
    /// <summary>0 = unlimited attempts.</summary>
    public int MaxAttempts { get; set; } = 1;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<Question> Questions { get; set; } = new();
}

public class Question
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    public string Text { get; set; } = "";
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public int Points { get; set; } = 1;
    public int SortOrder { get; set; }
    public List<QuestionOption> Options { get; set; } = new();
}

public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

public class AssessmentAttempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalPoints { get; set; }
    public int EarnedPoints { get; set; }
    public decimal Percentage { get; set; }
    /// <summary>Passing percentage at the time of the attempt (snapshot).</summary>
    public int PassingPercentageSnapshot { get; set; }
    public bool? Passed { get; set; }
    public List<AssessmentAnswer> Answers { get; set; } = new();
}

public class AssessmentAnswer
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public AssessmentAttempt Attempt { get; set; } = null!;
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int[] SelectedOptionIds { get; set; } = Array.Empty<int>();
    public bool IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    public string Username { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}

public class SystemSetting
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
