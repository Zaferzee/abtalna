using System.Security.Claims;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Services;

/// <summary>
/// Authentication abstraction: local password login and Windows (AD) login both end in the same place - an application
/// User record plus a cookie. Application authorisation (roles) always comes from our own tables, never from the login method.
/// </summary>
public class AuthService(AppDbContext db, PasswordService passwords, IConfiguration cfg, AuditService audit, IDirectoryLookup directory, ILogger<AuthService> log)
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    public string Mode => cfg["Authentication:Mode"] ?? "Local";
    public bool LocalEnabled => Mode is "Local" or "Both";
    public bool WindowsEnabled => Mode is "Windows" or "Both";

    public async Task<(User? User, string? Error)> ValidateLocalAsync(string username, string password)
    {
        if (!LocalEnabled) return (null, "Local sign-in is disabled.");
        var n = username.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.NormalizedUsername == n);
        const string generic = "Invalid username or password.";
        if (user == null || user.PasswordHash == null || !user.IsActive) { passwords.Verify(new User { PasswordHash = null }, password); return (null, generic); }
        if (user.LockoutUntil > DateTime.UtcNow) return (null, "Account temporarily locked. Try again later.");
        if (!passwords.Verify(user, password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailures) { user.LockoutUntil = DateTime.UtcNow + Lockout; user.FailedLoginCount = 0; }
            await db.SaveChangesAsync();
            return (null, generic);
        }
        user.FailedLoginCount = 0; user.LockoutUntil = null;
        return (user, null);
    }

    /// <summary>
    /// Maps a Windows identity (DOMAIN\user, as given by IIS) to an application User.
    /// * A domain user NEVER becomes an administrator automatically: new accounts only get the Employee (User) role.
    /// * Accounts are created on first valid sign-in when Authentication:Windows:AutoProvision is true (default).
    /// * Authentication:Windows:AllowedDomains (optional) rejects identities from other domains / local machine accounts.
    /// * No password is ever stored for Windows accounts.
    /// </summary>
    public async Task<(User? User, string? Error)> ResolveWindowsAsync(string windowsName)
    {
        windowsName = windowsName.Trim();
        var slash = windowsName.IndexOf('\\');
        var domain = slash > 0 ? windowsName[..slash] : "";
        var sam = slash > 0 ? windowsName[(slash + 1)..] : windowsName;
        if (sam.Length == 0 || sam.Length > 200) return (null, "Your account has not been enabled in this system.");

        var allowed = cfg.GetSection("Authentication:Windows:AllowedDomains").Get<string[]>()?.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray() ?? [];
        if (allowed.Length > 0 && !allowed.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            log.LogWarning("Windows sign-in rejected: domain '{Domain}' of identity '{Identity}' is not in Authentication:Windows:AllowedDomains.", domain, windowsName);
            return (null, "Your Windows domain is not permitted for this system.");
        }

        var n = windowsName.ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.NormalizedUsername == n);
        if (user == null)
        {
            if (!cfg.GetValue("Authentication:Windows:AutoProvision", true))
            {
                log.LogWarning("Windows sign-in rejected: no application account for '{Identity}' and AutoProvision is off.", windowsName);
                return (null, "Your account has not been enabled in this system.");
            }
            var info = directory.Find(domain, sam);
            var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.User);
            user = new User
            {
                Username = windowsName, NormalizedUsername = n, ExternalId = windowsName, AuthSource = "Windows",
                DisplayName = string.IsNullOrWhiteSpace(info?.DisplayName) ? sam : info!.DisplayName!, Email = info?.Email,
            };
            user.UserRoles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            audit.Add("USER_CREATED", "User", user.Id, Res.Ar("Account created automatically from Windows identity {0}", windowsName));
            log.LogInformation("Account created from Windows identity '{Identity}'.", windowsName);
        }
        else if (user.AuthSource == "Windows" && user.IsActive && (string.IsNullOrWhiteSpace(user.Email) || user.DisplayName == sam))
        {
            // Pre-created / imported accounts: fill in display name and e-mail from AD once, never overwrite edits.
            var info = directory.Find(domain, sam);
            if (info != null)
            {
                if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(info.Email)) user.Email = info.Email;
                if (user.DisplayName == sam && !string.IsNullOrWhiteSpace(info.DisplayName)) user.DisplayName = info.DisplayName!;
            }
        }
        if (!user.IsActive) { log.LogWarning("Windows sign-in rejected: account '{Identity}' is disabled.", windowsName); return (null, "Your account is disabled."); }
        return (user, null);
    }

    public async Task SignInAsync(HttpContext http, User user)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("display_name", user.DisplayName),
            new("must_change_password", user.MustChangePassword ? "1" : "0"),
        };
        claims.AddRange(user.UserRoles.Select(r => new Claim(ClaimTypes.Role, r.Role.Name)));
        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
    }
}
