using System.DirectoryServices;
using System.Runtime.Versioning;

namespace CyberLms.Web.Services;

public record DirectoryInfoResult(string? DisplayName, string? Email);

/// <summary>Optional enrichment of a Windows identity (display name, e-mail). Failure never blocks sign-in.</summary>
public interface IDirectoryLookup
{
    DirectoryInfoResult? Find(string domain, string samAccountName);
}

/// <summary>
/// Reads displayName/mail of the signed-in user from Active Directory using the application pool identity (read-only LDAP query,
/// no passwords involved). Windows only; returns null elsewhere or on any error (logged).
/// </summary>
public class ActiveDirectoryLookup(IConfiguration cfg, ILogger<ActiveDirectoryLookup> log) : IDirectoryLookup
{
    public DirectoryInfoResult? Find(string domain, string samAccountName)
    {
        if (!OperatingSystem.IsWindows() || !cfg.GetValue("Authentication:Windows:LookupDirectory", true)) return null;
        try { return FindWindows(samAccountName); }
        catch (Exception ex) { log.LogWarning(ex, "Active Directory lookup failed for {Domain}\\{User}; continuing without display name/e-mail.", domain, samAccountName); return null; }
    }

    [SupportedOSPlatform("windows")]
    private DirectoryInfoResult? FindWindows(string sam)
    {
        var path = cfg["Authentication:Windows:LdapPath"]; // e.g. LDAP://DC=corp,DC=local ; empty = domain of the server
        using var root = string.IsNullOrWhiteSpace(path) ? new DirectoryEntry() : new DirectoryEntry(path);
        using var searcher = new DirectorySearcher(root, $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeFilter(sam)}))", ["displayName", "mail"])
        {
            SizeLimit = 2, ClientTimeout = TimeSpan.FromSeconds(5), ServerTimeLimit = TimeSpan.FromSeconds(5),
        };
        var results = searcher.FindAll();
        if (results.Count != 1) { log.LogInformation("AD lookup for {User} returned {Count} entries; ignored.", sam, results.Count); return null; }
        var r = results[0];
        string? Get(string p) => r.Properties[p].Count > 0 ? r.Properties[p][0]?.ToString() : null;
        return new DirectoryInfoResult(Get("displayName"), Get("mail"));
    }

    /// <summary>RFC 4515 escaping so a user name can never alter the LDAP filter.</summary>
    public static string EscapeFilter(string s) =>
        s.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
