using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Data;

public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider sp, IConfiguration cfg, ILogger log)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            if (cfg.GetValue("Database:AutoMigrate", true))
            {
                // Transient database start-up delays are tolerated: 3 attempts.
                for (var attempt = 1; ; attempt++)
                {
                    try { await db.Database.MigrateAsync(); break; }
                    catch (Exception ex) when (attempt < 3) { log.LogWarning(ex, "Database migration attempt {Attempt} failed; retrying in 5 s.", attempt); await Task.Delay(TimeSpan.FromSeconds(5)); }
                }
            }
            else
            {
                // Production model: schema is applied by the DBA/owner account (see docs/POSTGRESQL.md); verify that it is current.
                var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                if (pending.Count > 0) { log.LogCritical("Database schema is out of date ({Pending}). Apply deploy/sql/migrate.sql as the owner account, then recycle the application pool.", string.Join(", ", pending)); return; }
            }
        }
        catch (Exception ex)
        {
            // Keep the site up: pages show a controlled Arabic error while the database is unavailable; /health reports 503.
            log.LogCritical(ex, "Database is unavailable or the schema could not be verified at start-up. The site will answer with error pages until it is fixed; recycle the application pool afterwards.");
            return;
        }

        foreach (var name in new[] { RoleNames.Admin, RoleNames.User })
            if (!await db.Roles.AnyAsync(r => r.Name == name)) db.Roles.Add(new Role { Name = name });
        await db.SaveChangesAsync();

        // Initial administrator: credentials come from configuration (Seed:AdminUsername / Seed:AdminPassword), never from source.
        var adminName = cfg["Seed:AdminUsername"];
        var adminPass = cfg["Seed:AdminPassword"];
        if (!await db.UserRoles.AnyAsync(ur => ur.Role.Name == RoleNames.Admin))
        {
            if (string.IsNullOrWhiteSpace(adminName) || string.IsNullOrWhiteSpace(adminPass))
            {
                log.LogWarning("No administrator exists and Seed:AdminUsername / Seed:AdminPassword are not configured. Configure them and restart.");
            }
            else if (PasswordService.Validate(adminPass) is { } err)
            {
                throw new InvalidOperationException("Seed:AdminPassword rejected: " + err);
            }
            else
            {
                var hasher = scope.ServiceProvider.GetRequiredService<PasswordService>();
                var u = new User
                {
                    Username = adminName.Trim(), NormalizedUsername = adminName.Trim().ToLowerInvariant(),
                    DisplayName = cfg["Seed:AdminDisplayName"] ?? Res.Ar("Administrator"), Email = cfg["Seed:AdminEmail"],
                    AuthSource = "Local", MustChangePassword = true,
                };
                u.PasswordHash = hasher.Hash(u, adminPass);
                var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.Admin);
                u.UserRoles.Add(new UserRole { Role = role });
                db.Users.Add(u);
                db.AuditLogs.Add(new AuditLog { Username = Res.Ar("System"), Action = "USER_CREATED", EntityType = "User", Details = Res.Ar("Initial administrator '{0}' created", u.Username) });
                await db.SaveChangesAsync();
                log.LogInformation("Initial administrator '{User}' created (password change required at first login).", u.Username);
            }
        }
    }

    /// <summary>
    /// Server-side bootstrap of an administrator for a Windows (AD) identity. Requires console access to the server (and the production
    /// configuration), so it cannot be triggered by any website user. Creates the account if needed (Windows sign-in, no password) and
    /// adds the Admin role. Idempotent; writes an audit entry.
    /// </summary>
    public static async Task<(bool Ok, string Message)> BootstrapAdminAsync(IServiceProvider sp, string identity, ILogger log)
    {
        identity = identity.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(identity, @"^[A-Za-z0-9._-]{1,100}\\[A-Za-z0-9._$-]{1,100}$"))
            return (false, "Invalid identity. Use the form DOMAIN\\username, e.g. --bootstrap-admin \"CORP\\itsecurity.admin\".");
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            foreach (var name in new[] { RoleNames.Admin, RoleNames.User })
                if (!await db.Roles.AnyAsync(r => r.Name == name)) db.Roles.Add(new Role { Name = name });
            await db.SaveChangesAsync();
            var n = identity.ToLowerInvariant();
            var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.NormalizedUsername == n);
            var created = user == null;
            if (user == null)
            {
                var sam = identity[(identity.IndexOf('\\') + 1)..];
                user = new User { Username = identity, NormalizedUsername = n, ExternalId = identity, AuthSource = "Windows", DisplayName = sam };
                db.Users.Add(user);
            }
            else if (user.AuthSource != "Windows") return (false, $"'{identity}' exists as a local account; refusing to change it.");
            user.IsActive = true;
            foreach (var roleName in new[] { RoleNames.User, RoleNames.Admin })
                if (!user.UserRoles.Any(r => r.Role.Name == roleName)) user.UserRoles.Add(new UserRole { Role = await db.Roles.FirstAsync(r => r.Name == roleName) });
            db.AuditLogs.Add(new AuditLog { Username = Res.Ar("System"), Action = "ADMIN_BOOTSTRAPPED", EntityType = "User", Details = Res.Ar("Administrator '{0}' assigned from the server command line", identity) });
            await db.SaveChangesAsync();
            log.LogWarning("Administrator role granted to '{Identity}' from the server command line.", identity);
            return (true, created ? $"Created '{identity}' and granted the Administrator role." : $"Granted the Administrator role to existing user '{identity}'.");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Administrator bootstrap failed.");
            return (false, "Bootstrap failed: " + ex.Message);
        }
    }
}
