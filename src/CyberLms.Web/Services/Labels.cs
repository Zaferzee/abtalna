using CyberLms.Web.Domain;

namespace CyberLms.Web.Services;

/// <summary>Maps enum/status codes to resource keys (translated by Localizer in views and exports).</summary>
public static class Labels
{
    public static string Type(ContentType t) => t switch
    {
        ContentType.Policy => "Policy", ContentType.Control => "Cybersecurity Control", ContentType.Awareness => "Awareness Content",
        ContentType.Training => "Training Content", ContentType.Procedure => "Procedure", ContentType.Regulation => "Regulation",
        ContentType.Instructions => "Instructions", _ => "General Content",
    };
    /// <summary>Content types in the order they are offered to authors and in filters.</summary>
    public static readonly ContentType[] TypeOrder =
        [ContentType.Policy, ContentType.Regulation, ContentType.Procedure, ContentType.Control, ContentType.Awareness, ContentType.Training, ContentType.Instructions, ContentType.General];
    /// <summary>Governance documents listed on the employees' "Policies" page.</summary>
    public static bool IsPolicyLike(ContentType t) => t is ContentType.Policy or ContentType.Regulation;
    public static string Status(string s) => s switch
    {
        ReportService.Passed => "Passed", ReportService.Failed => "Failed", ReportService.NotAttempted => "Not attempted",
        ReportService.Acknowledged => "Acknowledged", ReportService.NotAcknowledged => "Not acknowledged", _ => s,
    };
    public static string Badge(string s) => s switch
    {
        ReportService.Passed or ReportService.Acknowledged => "success",
        ReportService.Failed => "danger", _ => "secondary",
    };
    public static string ContentStatus(Domain.ContentStatus s) => s == Domain.ContentStatus.Published ? "Published" : "Draft";
    public static string QuestionType(Domain.QuestionType t) => t switch { Domain.QuestionType.SingleChoice => "Single choice", Domain.QuestionType.TrueFalse => "True / False", _ => "Multiple choice" };
    public static string Role(string r) => r == RoleNames.Admin ? "Role: Central administrator" : "Role: Employee";
    public static string AuthSource(string s) => s == "Windows" ? "Sign-in: Windows / Active Directory" : "Sign-in: Local password";
    /// <summary>True/False options are stored as the English words "True"/"False" and translated for display.</summary>
    public static string Option(Question q, QuestionOption o) => q.Type == Domain.QuestionType.TrueFalse ? o.Text : "";
    public static string Capability(Domain.Capability c) => c switch
    {
        Domain.Capability.ManageUsers => "Capability: Manage users",
        Domain.Capability.ManageContent => "Capability: Create and edit content",
        Domain.Capability.PublishContent => "Capability: Publish content",
        Domain.Capability.ManageAssessments => "Capability: Manage assessments",
        Domain.Capability.ManageAcknowledgments => "Capability: Manage acknowledgments",
        Domain.Capability.ViewReports => "Capability: View reports",
        Domain.Capability.ExportReports => "Capability: Export reports",
        Domain.Capability.ManageBranding => "Capability: Manage entity branding",
        Domain.Capability.ManageDirectory => "Capability: Manage directory integration",
        Domain.Capability.ManageNotifications => "Capability: Send notifications and reminders",
        Domain.Capability.ManageAdmins => "Capability: Manage entity administrators",
        Domain.Capability.ViewAudit => "Capability: View audit log",
        _ => c.ToString(),
    };
    public static string CapabilityIcon(Domain.Capability c) => c switch
    {
        Domain.Capability.ManageUsers => "bi-people", Domain.Capability.ManageContent => "bi-journal-richtext", Domain.Capability.PublishContent => "bi-broadcast",
        Domain.Capability.ManageAssessments => "bi-patch-question", Domain.Capability.ManageAcknowledgments => "bi-pen", Domain.Capability.ViewReports => "bi-bar-chart-line",
        Domain.Capability.ExportReports => "bi-download", Domain.Capability.ManageBranding => "bi-palette2", Domain.Capability.ManageDirectory => "bi-hdd-network",
        Domain.Capability.ManageNotifications => "bi-envelope", Domain.Capability.ManageAdmins => "bi-person-gear", Domain.Capability.ViewAudit => "bi-clock-history", _ => "bi-dot",
    };
    public static string Size(long b) => b >= 1 << 20 ? $"{b / 1048576.0:0.#} MB" : $"{Math.Max(1, b / 1024)} KB";
}
