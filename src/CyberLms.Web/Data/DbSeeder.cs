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
        if (cfg.GetValue("Database:AutoMigrate", true)) await db.Database.MigrateAsync();

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
}
