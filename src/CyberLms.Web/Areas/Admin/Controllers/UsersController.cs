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

public class UsersController(AppDbContext db, PasswordService passwords, AuditService audit, AuthService auth, IMemoryCache cache) : AdminController
{
    public async Task<IActionResult> Index(string? q, string? role, int page = 1)
    {
        var query = db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(r => r.Role).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var s = q.Trim().ToLower(); query = query.Where(u => u.NormalizedUsername.Contains(s) || u.DisplayName.ToLower().Contains(s) || (u.Email != null && u.Email.ToLower().Contains(s))); }
        if (!string.IsNullOrEmpty(role)) query = query.Where(u => u.UserRoles.Any(r => r.Role.Name == role));
        var pager = new Pager { Page = Math.Max(1, page), PageSize = 50, Total = await query.CountAsync() };
        var items = await query.OrderBy(u => u.DisplayName).Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize).ToListAsync();
        return View(new UserListVm { Items = items, Q = q, Role = role, Pager = pager });
    }

    public IActionResult Create() { ViewBag.Windows = auth.WindowsEnabled; return View("Form", new UserFormVm { AuthSource = auth.LocalEnabled ? "Local" : "Windows" }); }

    [HttpPost]
    public async Task<IActionResult> Create(UserFormVm vm)
    {
        ViewBag.Windows = auth.WindowsEnabled;
        var n = vm.Username?.Trim().ToLowerInvariant() ?? "";
        if (await db.Users.AnyAsync(u => u.NormalizedUsername == n)) ModelState.AddModelError(nameof(vm.Username), "Username already exists.");
        if (vm.AuthSource == "Local") { if (PasswordService.Validate(vm.Password) is { } e) ModelState.AddModelError(nameof(vm.Password), e); }
        else if (vm.AuthSource != "Windows") ModelState.AddModelError("", "Invalid authentication source.");
        if (!ModelState.IsValid) return View("Form", vm);
        var u = new User { Username = vm.Username!.Trim(), NormalizedUsername = n, DisplayName = vm.DisplayName.Trim(), Email = vm.Email?.Trim(), Department = vm.Department?.Trim(), IsActive = vm.IsActive, AuthSource = vm.AuthSource };
        if (vm.AuthSource == "Local") { u.PasswordHash = passwords.Hash(u, vm.Password!); u.MustChangePassword = true; }
        else u.ExternalId = u.Username;
        await SetRoles(u, vm.IsAdmin);
        db.Users.Add(u);
        await db.SaveChangesAsync();
        audit.Add("USER_CREATED", "User", u.Id, u.Username);
        await db.SaveChangesAsync();
        TempData["Success"] = "User created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(r => r.Role).FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        return View("Form", new UserFormVm { Id = u.Id, Username = u.Username, DisplayName = u.DisplayName, Email = u.Email, Department = u.Department, IsActive = u.IsActive, AuthSource = u.AuthSource, IsAdmin = u.UserRoles.Any(r => r.Role.Name == RoleNames.Admin) });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserFormVm vm)
    {
        var u = await db.Users.Include(x => x.UserRoles).ThenInclude(r => r.Role).FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        ModelState.Remove(nameof(vm.Username)); ModelState.Remove(nameof(vm.Password));
        if (!ModelState.IsValid) { vm.Id = id; vm.Username = u.Username; vm.AuthSource = u.AuthSource; return View("Form", vm); }
        if (u.Id == User.UserId() && (!vm.IsAdmin || !vm.IsActive)) { TempData["Error"] = "You cannot remove your own admin role or deactivate yourself."; return RedirectToAction(nameof(Edit), new { id }); }
        u.DisplayName = vm.DisplayName.Trim(); u.Email = vm.Email?.Trim(); u.Department = vm.Department?.Trim(); u.IsActive = vm.IsActive;
        await SetRoles(u, vm.IsAdmin);
        audit.Add("USER_UPDATED", "User", u.Id, u.Username);
        await db.SaveChangesAsync();
        cache.Remove($"userstate.{u.Id}");
        TempData["Success"] = "Saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, string newPassword)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (u.AuthSource != "Local") { TempData["Error"] = "This account uses Windows authentication."; return RedirectToAction(nameof(Index)); }
        if (PasswordService.Validate(newPassword) is { } e) { TempData["Error"] = e; return RedirectToAction(nameof(Edit), new { id }); }
        u.PasswordHash = passwords.Hash(u, newPassword); u.MustChangePassword = true; u.FailedLoginCount = 0; u.LockoutUntil = null;
        audit.Add("USER_PASSWORD_RESET", "User", u.Id, u.Username);
        await db.SaveChangesAsync();
        TempData["Success"] = "Password reset; the user must change it at next sign-in.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Bulk import. CSV header: username,displayname,email,department. Windows-source users need no password.</summary>
    [HttpPost]
    public async Task<IActionResult> Import(IFormFile file, string authSource, string? initialPassword)
    {
        if (file == null || file.Length == 0 || file.Length > 5_000_000) { TempData["Error"] = "Choose a CSV file (max 5 MB)."; return RedirectToAction(nameof(Index)); }
        if (authSource == "Local" && PasswordService.Validate(initialPassword) is { } pe) { TempData["Error"] = "Initial password: " + pe; return RedirectToAction(nameof(Index)); }
        if (authSource is not ("Local" or "Windows")) return BadRequest();
        var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.User);
        var existing = (await db.Users.Select(u => u.NormalizedUsername).ToListAsync()).ToHashSet();
        int created = 0, skipped = 0;
        using var reader = new StreamReader(file.OpenReadStream());
        var header = (await reader.ReadLineAsync())?.Split(',').Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToList();
        if (header == null || !header.Contains("username")) { TempData["Error"] = "CSV must have a header row with at least 'username'."; return RedirectToAction(nameof(Index)); }
        string? Col(string[] f, string name) { var i = header.IndexOf(name); return i >= 0 && i < f.Length ? f[i].Trim().Trim('"') : null; }
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            var name = Col(f, "username");
            if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || !existing.Add(name.ToLowerInvariant())) { skipped++; continue; }
            var u = new User { Username = name, NormalizedUsername = name.ToLowerInvariant(), DisplayName = Col(f, "displayname") is { Length: > 0 } d ? d : name, Email = Col(f, "email"), Department = Col(f, "department"), AuthSource = authSource };
            if (authSource == "Local") { u.PasswordHash = passwords.Hash(u, initialPassword!); u.MustChangePassword = true; } else u.ExternalId = name;
            u.UserRoles.Add(new UserRole { Role = role });
            db.Users.Add(u); created++;
        }
        audit.Add("USERS_IMPORTED", "User", null, $"{created} created, {skipped} skipped");
        await db.SaveChangesAsync();
        TempData["Success"] = $"Import finished: {created} created, {skipped} skipped (duplicates/invalid).";
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
