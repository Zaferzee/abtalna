using System.Security.Cryptography;
using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>
/// User administration, scoped: the central administrator manages everyone; an entity administrator with ManageUsers manages the
/// employees of the entities in scope only. Accounts with administrative rights (central role or any entity assignment) can only be
/// changed by the central administrator, so a delegated administrator can never take over a more privileged account.
/// </summary>
[Capability(Capability.ManageUsers)]
public class UsersController(AppDbContext db, PasswordService passwords, AuditService audit, AuthService auth, IMemoryCache cache, EntityAccess access) : AdminController
{
    private Task<EntityScope> Scope() => access.ScopeAsync(Capability.ManageUsers);

    private async Task Lookups(int? selected)
    {
        ViewBag.Entities = await access.EntitiesAsync(Capability.ManageUsers);
        ViewBag.Central = await access.IsCentralAsync();
        ViewBag.Selected = selected;
    }

    /// <summary>A user the current administrator may manage, or null (reported as 404: no hint that it exists elsewhere).</summary>
    private async Task<User?> ScopedUser(int id, bool track = false)
    {
        var q = db.Users.Include(x => x.UserRoles).ThenInclude(r => r.Role).Include(x => x.Entity).InScope(await Scope()).Where(x => x.Id == id);
        return track ? await q.FirstOrDefaultAsync() : await q.AsNoTracking().FirstOrDefaultAsync();
    }

    private async Task<bool> Privileged(User u) => u.UserRoles.Any(r => r.Role.Name == RoleNames.Admin) || await db.EntityAdminAssignments.AnyAsync(a => a.UserId == u.Id);

    public async Task<IActionResult> Index(string? q, string? role, int? entity, int page = 1)
    {
        var scope = await Scope();
        var query = db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(r => r.Role).Include(u => u.Entity).InScope(scope);
        if (entity is int eid && scope.Contains(eid) && (await access.AllEntitiesAsync()).FirstOrDefault(x => x.Id == eid) is { } node) query = query.InSubtree(node.Path);
        if (!string.IsNullOrWhiteSpace(q)) { var s = q.Trim().ToLower(); query = query.Where(u => u.NormalizedUsername.Contains(s) || u.DisplayName.ToLower().Contains(s) || (u.Email != null && u.Email.ToLower().Contains(s))); }
        if (!string.IsNullOrEmpty(role)) query = query.Where(u => u.UserRoles.Any(r => r.Role.Name == role));
        var pager = new Pager { Page = Math.Max(1, page), PageSize = 50, Total = await query.CountAsync() };
        var items = await query.OrderBy(u => u.DisplayName).Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize).ToListAsync();
        await Lookups(entity);
        ViewBag.AdminIds = (await db.EntityAdminAssignments.Select(a => a.UserId).Distinct().ToListAsync()).ToHashSet();
        return View(new UserListVm { Items = items, Q = q, Role = role, EntityId = entity, Pager = pager });
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Windows = auth.WindowsEnabled;
        await Lookups(null);
        return View("Form", new UserFormVm { AuthSource = auth.LocalEnabled ? "Local" : "Windows", EntityId = await access.DefaultEntityAsync(Capability.ManageUsers) ?? 0 });
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserFormVm vm)
    {
        ViewBag.Windows = auth.WindowsEnabled;
        await Lookups(vm.EntityId);
        var central = await access.IsCentralAsync();
        if (!(await Scope()).Contains(vm.EntityId)) ModelState.AddModelError(nameof(vm.EntityId), L["Choose an entity you administer."]);
        if (vm.IsAdmin && !central) return Forbid();
        var n = vm.Username?.Trim().ToLowerInvariant() ?? "";
        if (await db.Users.AnyAsync(u => u.NormalizedUsername == n)) ModelState.AddModelError(nameof(vm.Username), L["Username already exists."]);
        if (vm.AuthSource == "Local") { if (PasswordService.Validate(vm.Password) is { } e) ModelState.AddModelError(nameof(vm.Password), L[e]); }
        else if (vm.AuthSource != "Windows") ModelState.AddModelError("", L["Invalid authentication source."]);
        if (!ModelState.IsValid) return View("Form", vm);
        var u = new User { EntityId = vm.EntityId, Username = vm.Username!.Trim(), NormalizedUsername = n, DisplayName = vm.DisplayName.Trim(), Email = vm.Email?.Trim(), Department = vm.Department?.Trim(), IsActive = vm.IsActive, AuthSource = vm.AuthSource };
        if (vm.AuthSource == "Local") { u.PasswordHash = passwords.Hash(u, vm.Password!); u.MustChangePassword = true; }
        else u.ExternalId = u.Username;
        await SetRoles(u, vm.IsAdmin);
        db.Users.Add(u);
        await db.SaveChangesAsync();
        audit.Add("USER_CREATED", "User", u.Id, u.Username, u.EntityId);
        await db.SaveChangesAsync();
        Success("User created.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var u = await ScopedUser(id);
        if (u == null) return NotFound();
        if (!await access.IsCentralAsync() && await Privileged(u)) { Failure("This account has administrative rights; only the central administrator can change it."); return RedirectToAction(nameof(Index)); }
        await Lookups(u.EntityId);
        return View("Form", new UserFormVm { Id = u.Id, EntityId = u.EntityId, Username = u.Username, DisplayName = u.DisplayName, Email = u.Email, Department = u.Department, IsActive = u.IsActive, AuthSource = u.AuthSource, IsAdmin = u.UserRoles.Any(r => r.Role.Name == RoleNames.Admin) });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserFormVm vm)
    {
        var u = await ScopedUser(id, track: true);
        if (u == null) return NotFound();
        var central = await access.IsCentralAsync();
        if (!central && (await Privileged(u) || vm.IsAdmin)) return Forbid();
        ModelState.Remove(nameof(vm.Username)); ModelState.Remove(nameof(vm.Password));
        if (!(await Scope()).Contains(vm.EntityId)) ModelState.AddModelError(nameof(vm.EntityId), L["Choose an entity you administer."]);
        if (!ModelState.IsValid) { vm.Id = id; vm.Username = u.Username; vm.AuthSource = u.AuthSource; await Lookups(vm.EntityId); return View("Form", vm); }
        if (u.Id == User.UserId() && (!vm.IsAdmin || !vm.IsActive)) { Failure("You cannot remove your own admin role or deactivate yourself."); return RedirectToAction(nameof(Edit), new { id }); }
        if (u.EntityId != vm.EntityId) audit.Add("USER_ENTITY_CHANGED", "User", u.Id, Res.Ar("{0}: entity {1} → {2}", u.Username, u.EntityId, vm.EntityId), vm.EntityId);
        u.DisplayName = vm.DisplayName.Trim(); u.Email = vm.Email?.Trim(); u.Department = vm.Department?.Trim(); u.IsActive = vm.IsActive; u.EntityId = vm.EntityId;
        if (central) await SetRoles(u, vm.IsAdmin);
        audit.Add("USER_UPDATED", "User", u.Id, u.Username, u.EntityId);
        await db.SaveChangesAsync();
        cache.Remove($"userstate.{u.Id}");
        Success("Saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, string newPassword)
    {
        var u = await ScopedUser(id, track: true);
        if (u == null) return NotFound();
        if (!await access.IsCentralAsync() && await Privileged(u)) return Forbid();
        if (u.AuthSource != "Local") { Failure("This account uses Windows authentication."); return RedirectToAction(nameof(Index)); }
        if (PasswordService.Validate(newPassword) is { } e) { Failure(e); return RedirectToAction(nameof(Edit), new { id }); }
        u.PasswordHash = passwords.Hash(u, newPassword); u.MustChangePassword = true; u.FailedLoginCount = 0; u.LockoutUntil = null;
        audit.Add("USER_PASSWORD_RESET", "User", u.Id, u.Username, u.EntityId);
        await db.SaveChangesAsync();
        Success("Password reset; the user must change it at next sign-in.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Bulk import. CSV header: username,displayname,email,department. Windows-source users need no password.</summary>
    [HttpPost]
    public async Task<IActionResult> Import(IFormFile file, string authSource, string? initialPassword, int entityId)
    {
        if (!(await Scope()).Contains(entityId)) return Forbid();
        if (file == null || file.Length == 0 || file.Length > 5_000_000) { Failure("Choose a CSV file (max 5 MB)."); return RedirectToAction(nameof(Index)); }
        if (authSource == "Local" && PasswordService.Validate(initialPassword) is { } pe) { Failure("Initial password: {0}", L[pe]); return RedirectToAction(nameof(Index)); }
        if (authSource is not ("Local" or "Windows")) return BadRequest();
        var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.User);
        var existing = (await db.Users.Select(u => u.NormalizedUsername).ToListAsync()).ToHashSet();
        int created = 0, skipped = 0;
        using var reader = new StreamReader(file.OpenReadStream());
        var header = (await reader.ReadLineAsync())?.Split(',').Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToList();
        if (header == null || !header.Contains("username")) { Failure("CSV must have a header row with at least 'username'."); return RedirectToAction(nameof(Index)); }
        string? Col(string[] f, string name) { var i = header.IndexOf(name); return i >= 0 && i < f.Length ? f[i].Trim().Trim('"') : null; }
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            var name = Col(f, "username");
            if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || !existing.Add(name.ToLowerInvariant())) { skipped++; continue; }
            var u = new User { EntityId = entityId, Username = name, NormalizedUsername = name.ToLowerInvariant(), DisplayName = Col(f, "displayname") is { Length: > 0 } d ? d : name, Email = Col(f, "email"), Department = Col(f, "department"), AuthSource = authSource };
            if (authSource == "Local") { u.PasswordHash = passwords.Hash(u, initialPassword!); u.MustChangePassword = true; } else u.ExternalId = name;
            u.UserRoles.Add(new UserRole { Role = role });
            db.Users.Add(u); created++;
        }
        audit.Add("USERS_IMPORTED", "User", null, Res.Ar("{0} created, {1} skipped", created, skipped), entityId);
        await db.SaveChangesAsync();
        Success("Import finished: {0} created, {1} skipped (duplicates/invalid).", created, skipped);
        return RedirectToAction(nameof(Index));
    }

    private async Task SetRoles(User u, bool admin)
    {
        var roles = await db.Roles.ToListAsync();
        void Set(string name, bool on)
        {
            var role = roles.First(r => r.Name == name);
            var has = u.UserRoles.FirstOrDefault(r => r.RoleId == role.Id);
            if (on && has == null) u.UserRoles.Add(new UserRole { Role = role });
            if (!on && has != null) u.UserRoles.Remove(has);
        }
        Set(RoleNames.User, true);
        Set(RoleNames.Admin, admin);
    }
}
