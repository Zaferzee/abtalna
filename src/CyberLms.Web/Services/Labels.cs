using CyberLms.Web.Domain;

namespace CyberLms.Web.Services;

public static class Labels
{
    public static string Type(ContentType t) => t switch
    {
        ContentType.Policy => "Policy", ContentType.Control => "Cybersecurity Control", ContentType.Awareness => "Awareness Content",
        ContentType.Training => "Training Content", ContentType.Procedure => "Procedure / Instruction", _ => "General Content",
    };
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
    public static string Size(long b) => b >= 1 << 20 ? $"{b / 1048576.0:0.#} MB" : $"{Math.Max(1, b / 1024)} KB";
}
