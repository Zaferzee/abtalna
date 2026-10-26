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
    /// <summary>Primary organizational scope (entity) of the user.</summary>
    public int EntityId { get; set; }
    public OrgEntity Entity { get; set; } = null!;
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
    /// <summary>Owning entity (who may manage it). Central content is owned by the root entity.</summary>
    public int OwnerEntityId { get; set; }
    public OrgEntity OwnerEntity { get; set; } = null!;
    public ContentTargetScope TargetScope { get; set; } = ContentTargetScope.OwnerOnly;
    public List<ContentEntityTarget> Targets { get; set; } = new();
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
    /// <summary>Owning entity. A linked assessment follows its content's owner and audience; a standalone one is visible to the owner subtree.</summary>
    public int OwnerEntityId { get; set; }
    public OrgEntity OwnerEntity { get; set; } = null!;
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
    /// <summary>Entity of the acting user (their primary entity) at the time of the action.</summary>
    public int? ActorEntityId { get; set; }
    /// <summary>Entity the action affected (used to scope the audit trail for entity administrators).</summary>
    public int? ScopeEntityId { get; set; }
}

public class SystemSetting
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

// ===================================================================== organizational hierarchy (enterprise v1)

/// <summary>
/// Capabilities that can be delegated to an entity administrator. The central administrator (Admin role) implicitly holds all of
/// them for the whole hierarchy. An entity administrator holds the intersection of the capabilities granted on the assignment and
/// the capabilities the central administrator allows for that entity (<see cref="OrgEntity.AllowedCapabilities"/>).
/// </summary>
[Flags]
public enum Capability
{
    None = 0,
    ManageUsers = 1 << 0,
    ManageContent = 1 << 1,
    PublishContent = 1 << 2,
    ManageAssessments = 1 << 3,
    ManageAcknowledgments = 1 << 4,
    ViewReports = 1 << 5,
    ExportReports = 1 << 6,
    ManageBranding = 1 << 7,
    ManageDirectory = 1 << 8,
    ManageNotifications = 1 << 9,
    ManageAdmins = 1 << 10,
    ViewAudit = 1 << 11,
    All = (1 << 12) - 1,
    /// <summary>What a new entity may be granted unless the central administrator changes it: no branding, directory or
    /// administrator delegation (safe by default).</summary>
    DefaultAllowed = ManageUsers | ManageContent | PublishContent | ManageAssessments | ManageAcknowledgments | ViewReports | ExportReports | ManageNotifications | ViewAudit,
}

/// <summary>Who sees a content item (no copies are made per entity).</summary>
public enum ContentTargetScope
{
    /// <summary>Employees of the owning entity only (for central content: "root entity only").</summary>
    OwnerOnly = 0,
    /// <summary>The owning entity and all its descendants (for central content: "all entities").</summary>
    OwnerAndDescendants = 1,
    /// <summary>Only the entities listed in <see cref="Content.Targets"/> (optionally with their descendants).</summary>
    Selected = 2,
}

/// <summary>An organizational unit. Exactly one root (ParentEntityId null); any depth below it.</summary>
public class OrgEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Short unique code (e.g. MOH, AGY-01).</summary>
    public string Code { get; set; } = "";
    public int? ParentEntityId { get; set; }
    public OrgEntity? Parent { get; set; }
    /// <summary>Materialized path of ids, e.g. "/1/5/9/" (root "/1/"). Maintained by EntityTree.</summary>
    public string Path { get; set; } = "";
    public int Depth { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Ceiling for anything delegated to administrators of this entity (set by the central administrator).</summary>
    public Capability AllowedCapabilities { get; set; } = Capability.DefaultAllowed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsRoot => ParentEntityId == null;
}

/// <summary>Delegated administration: a user administers an entity (and optionally its descendants) with explicit capabilities.</summary>
public class EntityAdminAssignment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int EntityId { get; set; }
    public OrgEntity Entity { get; set; } = null!;
    public Capability Capabilities { get; set; }
    public bool IncludeDescendants { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedById { get; set; }
}

/// <summary>One audience entry of a content item whose <see cref="Content.TargetScope"/> is Selected.</summary>
public class ContentEntityTarget
{
    public int ContentId { get; set; }
    public Content Content { get; set; } = null!;
    public int EntityId { get; set; }
    public OrgEntity Entity { get; set; } = null!;
    public bool IncludeDescendants { get; set; } = true;
}

/// <summary>Per-entity setting override (branding keys "Branding.*"); absent = inherited from the parent chain / root settings.</summary>
public class EntitySetting
{
    public int EntityId { get; set; }
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

/// <summary>
/// Directory source of an entity (Active Directory domain used for user provisioning / attribute lookup). This is directory
/// synchronization and mapping only; it does not configure authentication (Windows Integrated Authentication stays an IIS /
/// domain-trust concern). The bind secret is stored encrypted (ASP.NET Data Protection) and never displayed or logged.
/// </summary>
public class EntityDirectorySource
{
    public int EntityId { get; set; }
    public OrgEntity Entity { get; set; } = null!;
    public bool Enabled { get; set; }
    /// <summary>NetBIOS domain name as it appears in DOMAIN\user (used to map new Windows users to this entity).</summary>
    public string Domain { get; set; } = "";
    public string? LdapPath { get; set; }
    public string? SearchBase { get; set; }
    public string? BindUser { get; set; }
    public string? BindSecretProtected { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public string? ValidationNote { get; set; }
}
