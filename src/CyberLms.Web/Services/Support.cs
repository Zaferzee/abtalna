using System.Security.Claims;
using System.Text;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.AspNetCore.Identity;

namespace CyberLms.Web.Services;

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal p) => int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : 0;
}

public class AuditService(AppDbContext db, IHttpContextAccessor http)
{
    /// <summary>Adds an audit row to the context. Caller saves (so the entry commits with the change it describes).</summary>
    public void Add(string action, string entityType, object? entityId = null, string? details = null)
    {
        var ctx = http.HttpContext;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = ctx?.User.UserId() is > 0 and var id ? id : null,
            Username = ctx?.User.Identity?.Name ?? "system",
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details is { Length: > 1000 } ? details[..1000] : details,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
        });
    }
}

public class PasswordService
{
    private readonly PasswordHasher<User> _h = new();
    public string Hash(User u, string password) => _h.HashPassword(u, password);
    public bool Verify(User u, string password) =>
        u.PasswordHash != null && _h.VerifyHashedPassword(u, u.PasswordHash, password) != PasswordVerificationResult.Failed;

    /// <summary>Basic policy: 10+ chars, upper, lower, digit.</summary>
    public static string? Validate(string? p)
    {
        if (string.IsNullOrEmpty(p) || p.Length < 10) return "Password must be at least 10 characters.";
        if (!p.Any(char.IsUpper) || !p.Any(char.IsLower) || !p.Any(char.IsDigit)) return "Password must contain upper-case, lower-case letters and a digit.";
        return null;
    }
}

public static class Csv
{
    public static byte[] Build(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header.Select(Esc)));
        foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(Esc)));
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(); // BOM so Excel reads Arabic
    }

    public static string Safe(object? v)
    {
        var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        // Neutralise spreadsheet formula injection.
        return s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + s : s;
    }

    private static string Esc(object? v)
    {
        var s = Safe(v);
        return s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}

public class TimeDisplay(IConfiguration cfg)
{
    private readonly TimeZoneInfo _tz = Resolve(cfg["App:DisplayTimeZone"]);
    private static TimeZoneInfo Resolve(string? id)
    {
        try { return string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Local; }
    }
    public string Format(DateTime? utc) => utc == null ? "-" : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), _tz).ToString("yyyy-MM-dd HH:mm");
}
