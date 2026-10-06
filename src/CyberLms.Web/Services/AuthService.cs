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
public class AuthService(AppDbContext db, PasswordService passwords, IConfiguration cfg, AuditService audit)
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

    /// <summary>Maps a Windows identity (DOMAIN\user) to a User, optionally auto-provisioning an employee account.</summary>
    public async Task<(User? User, string? Error)> ResolveWindowsAsync(string windowsName)
    {
        var n = windowsName.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.NormalizedUsername == n);
        if (user == null)
        {
            if (!cfg.GetValue("Authentication:Windows:AutoProvision", true)) return (null, "Your account has not been enabled in this system.");
            var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.User);
            var display = windowsName.Contains('\\') ? windowsName[(windowsName.IndexOf('\\') + 1)..] : windowsName;
            user = new User { Username = windowsName.Trim(), NormalizedUsername = n, DisplayName = display, AuthSource = "Windows", ExternalId = windowsName.Trim() };
            user.UserRoles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            audit.Add("USER_CREATED", "User", user.Id, Res.Ar("Account created automatically from Windows identity {0}", windowsName));
        }
        if (!user.IsActive) return (null, "Your account is disabled.");
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
