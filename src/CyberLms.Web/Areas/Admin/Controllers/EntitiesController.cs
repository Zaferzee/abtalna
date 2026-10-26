using System.Text.RegularExpressions;
using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>
/// Organization / entities. The central administrator creates, edits, moves and disables entities and decides what each entity may
/// be delegated (AllowedCapabilities). Administrators are assigned per entity with explicit capabilities: by the central
/// administrator, or by an entity administrator holding ManageAdmins there - who can never grant more than they hold themselves.
/// Entity administrators see (read-only) only the entities in their scope; anything else is 404.
/// </summary>
public class EntitiesController(AppDbContext db, EntityAccess access, EntityTree tree, AuditService audit, EntityBrandingService branding,
    DirectorySecrets secrets, ReportService reports, IMemoryCache cache) : AdminController
{
    public static readonly Capability[] Delegable = Enum.GetValues<Capability>().Where(c => c is not (Capability.None or Capability.All or Capability.DefaultAllowed)).ToArray();
    private static readonly Regex CodeRx = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$", RegexOptions.Compiled);

    private async Task<EntityNode?> Visible(int id) => (await access.AnyScopeAsync()).Contains(id) ? await tree.FindAsync(db, id) : null;

    private static Capability Combine(IEnumerable<Capability>? caps) => (caps ?? []).Where(c => Delegable.Contains(c)).Aggregate(Capability.None, (a, c) => a | c);

    // ------------------------------------------------------------------------------------------------ list / tree

    public async Task<IActionResult> Index()
    {
        var scope = await access.AnyScopeAsync();
        var all = await access.AllEntitiesAsync();
        var nodes = EntityTree.Ordered(all.Where(e => scope.Contains(e.Id)));
        var ids = nodes.Select(n => n.Id).ToList();
        var users = await db.Users.Where(u => ids.Contains(u.EntityId)).GroupBy(u => u.EntityId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var admins = await db.EntityAdminAssignments.Where(a => ids.Contains(a.EntityId)).GroupBy(a => a.EntityId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var dirs = await db.EntityDirectorySources.AsNoTracking().Where(d => ids.Contains(d.EntityId)).ToDictionaryAsync(d => d.EntityId);
        var vm = new EntityListVm
        {
            Central = await access.IsCentralAsync(),
            Rows = nodes.Select(n => new EntityRowVm
            {
                Node = n, Parent = all.FirstOrDefault(p => p.Id == n.ParentId),
                Users = users.GetValueOrDefault(n.Id), SubtreeUsers = EntityTree.Subtree(all, n).Sum(x => users.GetValueOrDefault(x.Id)),
                Admins = admins.GetValueOrDefault(n.Id), CustomBranding = branding.HasOverrides(n.Id), LogoUrl = branding.OwnLogoUrl(n.Id, n.IsRoot),
                Directory = dirs.TryGetValue(n.Id, out var d) ? (d.Enabled ? "enabled" : "disabled") : "none",
                Children = all.Count(c => c.ParentId == n.Id),
            }).ToList(),
        };
        return View(vm);
    }

    // ------------------------------------------------------------------------------------------------ create / edit (central)

    [CentralOnly]
    public async Task<IActionResult> Create(int? parentId)
    {
        var root = await tree.RootAsync(db);
        return View("Form", await FormLookups(new EntityFormVm { ParentEntityId = parentId ?? root.Id, IsActive = true, Allowed = Delegable.Where(c => Capability.DefaultAllowed.HasFlag(c)).ToList() }));
    }

    private async Task<EntityFormVm> FormLookups(EntityFormVm vm)
    {
        var all = await access.AllEntitiesAsync();
        var self = vm.Id == 0 ? null : all.FirstOrDefault(e => e.Id == vm.Id);
        // a node cannot be moved under itself or its descendants
        vm.Parents = EntityTree.Ordered(all.Where(e => self == null || !e.Path.StartsWith(self.Path, StringComparison.Ordinal)));
        vm.IsRoot = self?.IsRoot == true;
        return vm;
    }

    private async Task Validate(EntityFormVm vm)
    {
        vm.Code = (vm.Code ?? "").Trim().ToUpperInvariant();
        vm.Name = (vm.Name ?? "").Trim();
        if (vm.Name.Length == 0) ModelState.AddModelError(nameof(vm.Name), L["Enter the entity name."]);
        if (!CodeRx.IsMatch(vm.Code)) ModelState.AddModelError(nameof(vm.Code), L["Code: letters, digits, '.', '_' or '-' (up to 50)."]);
        else if (await db.Entities.AnyAsync(e => e.Code == vm.Code && e.Id != vm.Id)) ModelState.AddModelError(nameof(vm.Code), L["This code is already used by another entity."]);
    }

    [HttpPost, CentralOnly]
    public async Task<IActionResult> Create(EntityFormVm vm)
    {
        await Validate(vm);
        var parent = await db.Entities.FirstOrDefaultAsync(e => e.Id == vm.ParentEntityId);
        if (parent == null) ModelState.AddModelError(nameof(vm.ParentEntityId), L["Choose the parent entity."]);
        if (!ModelState.IsValid) return View("Form", await FormLookups(vm));
        var e = await tree.CreateAsync(db, parent!, vm.Name, vm.Code, Combine(vm.Allowed));
        if (!vm.IsActive) { e.IsActive = false; await db.SaveChangesAsync(); tree.Invalidate(); }
        audit.Add("ENTITY_CREATED", "Entity", e.Id, Res.Ar("{0} ({1}) under {2}; allowed: {3}", e.Name, e.Code, parent!.Name, e.AllowedCapabilities), e.Id);
        await db.SaveChangesAsync();
        Success("Entity created.");
        return RedirectToAction(nameof(Details), new { id = e.Id });
    }

    [CentralOnly]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await db.Entities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        return View("Form", await FormLookups(new EntityFormVm
        {
            Id = e.Id, Name = e.Name, Code = e.Code, ParentEntityId = e.ParentEntityId, IsActive = e.IsActive,
            Allowed = Delegable.Where(c => e.AllowedCapabilities.HasFlag(c)).ToList(),
        }));
    }

    [HttpPost, CentralOnly]
    public async Task<IActionResult> Edit(int id, EntityFormVm vm)
    {
        var e = await db.Entities.FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        vm.Id = id;
        await Validate(vm);
        if (e.IsRoot && !vm.IsActive) ModelState.AddModelError(nameof(vm.IsActive), L["The root entity cannot be disabled."]);
        OrgEntity? parent = null;
        if (!e.IsRoot)
        {
            parent = await db.Entities.FirstOrDefaultAsync(x => x.Id == vm.ParentEntityId);
            if (parent == null || parent.Path.StartsWith(e.Path, StringComparison.Ordinal)) ModelState.AddModelError(nameof(vm.ParentEntityId), L["An entity cannot be placed under itself or one of its sub-entities."]);
        }
        if (!ModelState.IsValid) return View("Form", await FormLookups(vm));
        var allowed = e.IsRoot ? Capability.All : Combine(vm.Allowed);
        var changes = new List<string>();
        if (e.Name != vm.Name) changes.Add(Res.Ar("name: {0} → {1}", e.Name, vm.Name));
        if (e.Code != vm.Code) changes.Add(Res.Ar("code: {0} → {1}", e.Code, vm.Code));
        if (e.AllowedCapabilities != allowed) audit.Add("ENTITY_CAPABILITIES_CHANGED", "Entity", e.Id, Res.Ar("{0}: {1} → {2}", e.Code, e.AllowedCapabilities, allowed), e.Id);
        if (e.IsActive != vm.IsActive) audit.Add(vm.IsActive ? "ENTITY_ENABLED" : "ENTITY_DISABLED", "Entity", e.Id, e.Name, e.Id);
        e.Name = vm.Name; e.Code = vm.Code; e.AllowedCapabilities = allowed; e.IsActive = vm.IsActive || e.IsRoot; e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (parent != null && parent.Id != e.ParentEntityId)
        {
            var from = e.ParentEntityId;
            await tree.MoveAsync(db, e, parent);
            audit.Add("ENTITY_MOVED", "Entity", e.Id, Res.Ar("{0}: parent {1} → {2}", e.Code, from, parent.Id), e.Id);
        }
        if (changes.Count > 0) audit.Add("ENTITY_UPDATED", "Entity", e.Id, string.Join("; ", changes), e.Id);
        await db.SaveChangesAsync();
        tree.Invalidate();
        // sessions of users in a disabled subtree are re-validated within a minute (cookie validation)
        Success("Saved.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ------------------------------------------------------------------------------------------------ details (tabs)

    public async Task<IActionResult> Details(int id, string? tab)
    {
        var node = await Visible(id);
        if (node == null) return NotFound();
        var all = await access.AllEntitiesAsync();
        var subtreeIds = EntityTree.Subtree(all, node).Select(e => e.Id).ToList();
        var vm = new EntityDetailsVm
        {
            Node = node, Tab = tab ?? "overview", Central = await access.IsCentralAsync(),
            Ancestors = node.AncestorIds.Select(a => all.First(e => e.Id == a)).ToList(),
            Children = all.Where(e => e.ParentId == id).OrderBy(e => e.Name).ToList(),
            MyCapabilities = await access.CapabilitiesInAsync(id),
            UsersOwn = await db.Users.CountAsync(u => u.EntityId == id),
            UsersSubtree = await db.Users.CountAsync(u => subtreeIds.Contains(u.EntityId)),
            ContentOwned = await db.Contents.CountAsync(c => c.OwnerEntityId == id),
            CustomBranding = branding.HasOverrides(id), LogoUrl = branding.OwnLogoUrl(id, node.IsRoot),
        };
        vm.Admins = await db.EntityAdminAssignments.AsNoTracking().Include(a => a.User).Where(a => a.EntityId == id).OrderBy(a => a.User.DisplayName).ToListAsync();
        vm.Directory = await db.EntityDirectorySources.AsNoTracking().FirstOrDefaultAsync(d => d.EntityId == id);
        vm.DirectorySecretSet = secrets.CanUnprotect(vm.Directory?.BindSecretProtected);
        switch (vm.Tab)
        {
            case "users":
                if (!vm.MyCapabilities.HasFlag(Capability.ManageUsers)) break;
                vm.Users = await db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(r => r.Role).Where(u => u.EntityId == id).OrderBy(u => u.DisplayName).Take(100).ToListAsync();
                break;
            case "content":
                // owned by this entity, plus content owned elsewhere that is targeted to it (central distribution)
                var viewer = new EntityVisibility.Viewer(id, node.AncestorIds);
                vm.ContentOwnedList = await db.Contents.AsNoTracking().Include(c => c.Targets).Where(c => c.OwnerEntityId == id).OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync();
                vm.ContentTargeted = await db.Contents.AsNoTracking().Include(c => c.Targets).Where(c => c.OwnerEntityId != id && c.Status == ContentStatus.Published).Where(EntityVisibility.Content(viewer)).OrderByDescending(c => c.PublishedAt).Take(100).ToListAsync();
                break;
            case "reports":
                if (!await access.CanInAsync(Capability.ViewReports, id)) break;
                var f = new ReportFilter { Scope = await access.ScopeAsync(Capability.ViewReports) };
                vm.Breakdown = await reports.EntityBreakdownAsync(f, [node, .. vm.Children.Where(c => f.Scope.Contains(c.Id))], all);
                break;
            case "audit":
                if (!await access.CanInAsync(Capability.ViewAudit, id)) break;
                vm.Audit = await db.AuditLogs.AsNoTracking().Where(a => a.ScopeEntityId != null && subtreeIds.Contains(a.ScopeEntityId.Value)).OrderByDescending(a => a.Timestamp).Take(50).ToListAsync();
                break;
        }
        ViewBag.EntityNames = all.ToDictionary(e => e.Id, e => e.Name);
        ViewBag.All = all;
        return View(vm);
    }

    // ------------------------------------------------------------------------------------------------ administrators & permissions

    /// <summary>Who may manage administrators here: the central administrator, or an entity administrator with ManageAdmins in this entity.</summary>
    private async Task<bool> CanManageAdmins(int entityId) => await access.IsCentralAsync() || await access.CanInAsync(Capability.ManageAdmins, entityId);

    /// <summary>Capabilities the actor may grant here: never more than they hold in the entity, never more than the entity allows.</summary>
    private async Task<Capability> Grantable(EntityNode node) => await access.CapabilitiesInAsync(node.Id) & node.Allowed;

    [HttpPost]
    public async Task<IActionResult> AssignAdmin(int id, string username, List<Capability>? caps, bool includeDescendants = true)
    {
        var node = await Visible(id);
        if (node == null) return NotFound();
        if (!await CanManageAdmins(id)) return Forbid();
        var n = (username ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).Include(u => u.Entity).FirstOrDefaultAsync(u => u.NormalizedUsername == n && u.IsActive);
        var central = await access.IsCentralAsync();
        // delegated administrators may only appoint people from their own entity subtree; nobody may assign a central administrator
        if (user == null || (!central && !(await access.ScopeAsync(Capability.ManageAdmins)).Contains(user.EntityId)))
        { Failure("No active user with this username in your scope."); return RedirectToAction(nameof(Details), new { id, tab = "admins" }); }
        if (user.UserRoles.Any(r => r.Role.Name == RoleNames.Admin)) { Failure("This user is already a central administrator."); return RedirectToAction(nameof(Details), new { id, tab = "admins" }); }
        if (user.Id == User.UserId()) return Forbid();
        var requested = Combine(caps);
        var granted = requested & await Grantable(node);
        if (granted != requested) return Forbid(); // asked for more than the actor may delegate here
        if (granted == Capability.None) { Failure("Choose at least one capability."); return RedirectToAction(nameof(Details), new { id, tab = "admins" }); }
        var a = await db.EntityAdminAssignments.FirstOrDefaultAsync(x => x.UserId == user.Id && x.EntityId == id);
        var created = a == null;
        a ??= new EntityAdminAssignment { UserId = user.Id, EntityId = id, CreatedById = User.UserId() };
        var before = a.Capabilities;
        a.Capabilities = granted; a.IncludeDescendants = includeDescendants; a.UpdatedAt = created ? null : DateTime.UtcNow;
        if (created) db.EntityAdminAssignments.Add(a);
        audit.Add(created ? "ADMIN_ASSIGNED" : "ADMIN_PERMISSIONS_CHANGED", "User", user.Id,
            created ? Res.Ar("{0} → {1}: {2}; sub-entities: {3}", user.Username, node.Code, granted, includeDescendants) : Res.Ar("{0} @ {1}: {2} → {3}; sub-entities: {4}", user.Username, node.Code, before, granted, includeDescendants), id);
        await db.SaveChangesAsync();
        cache.Remove($"userstate.{user.Id}");
        Success(created ? "Administrator assigned." : "Permissions updated.");
        return RedirectToAction(nameof(Details), new { id, tab = "admins" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateAdmin(int id, int assignmentId, List<Capability>? caps, bool includeDescendants = true)
    {
        var node = await Visible(id);
        if (node == null) return NotFound();
        if (!await CanManageAdmins(id)) return Forbid();
        var a = await db.EntityAdminAssignments.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == assignmentId && x.EntityId == id);
        if (a == null) return NotFound();
        if (a.UserId == User.UserId()) return Forbid(); // nobody edits their own delegation
        var grantable = await Grantable(node);
        // a delegated administrator cannot touch an assignment that holds capabilities beyond their own
        if ((a.Capabilities & ~grantable) != 0 && !await access.IsCentralAsync()) return Forbid();
        var requested = Combine(caps);
        if ((requested & ~grantable) != 0) return Forbid();
        if (requested == Capability.None) return await Revoke(node, a);
        audit.Add("ADMIN_PERMISSIONS_CHANGED", "User", a.UserId, Res.Ar("{0} @ {1}: {2} → {3}; sub-entities: {4}", a.User.Username, node.Code, a.Capabilities, requested, includeDescendants), id);
        a.Capabilities = requested; a.IncludeDescendants = includeDescendants; a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Success("Permissions updated.");
        return RedirectToAction(nameof(Details), new { id, tab = "admins" });
    }

    [HttpPost]
    public async Task<IActionResult> RevokeAdmin(int id, int assignmentId)
    {
        var node = await Visible(id);
        if (node == null) return NotFound();
        if (!await CanManageAdmins(id)) return Forbid();
        var a = await db.EntityAdminAssignments.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == assignmentId && x.EntityId == id);
        if (a == null) return NotFound();
        if (a.UserId == User.UserId()) return Forbid();
        if ((a.Capabilities & ~await Grantable(node)) != 0 && !await access.IsCentralAsync()) return Forbid();
        return await Revoke(node, a);
    }

    private async Task<IActionResult> Revoke(EntityNode node, EntityAdminAssignment a)
    {
        audit.Add("ADMIN_REVOKED", "User", a.UserId, Res.Ar("{0} @ {1}: {2}", a.User.Username, node.Code, a.Capabilities), node.Id);
        db.EntityAdminAssignments.Remove(a);
        await db.SaveChangesAsync();
        cache.Remove($"userstate.{a.UserId}");
        Success("Administrator removed.");
        return RedirectToAction(nameof(Details), new { id = node.Id, tab = "admins" });
    }

    // ------------------------------------------------------------------------------------------------ directory source

    /// <summary>
    /// Directory source of an entity: needs ManageDirectory in the entity (and the entity must allow it). The bind secret is write-only:
    /// encrypted with Data Protection, never shown, never logged or audited; leaving the field empty keeps it, "clear" removes it.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveDirectory(int id, DirectoryFormVm vm)
    {
        var node = await Visible(id);
        if (node == null) return NotFound();
        if (!await access.CanInAsync(Capability.ManageDirectory, id)) return Forbid();
        var domain = (vm.Domain ?? "").Trim().ToUpperInvariant();
        if (vm.Enabled && !Regex.IsMatch(domain, "^[A-Z0-9][A-Z0-9-]{0,14}$")) { Failure("Enter the NetBIOS domain name (e.g. AGENCY)."); return RedirectToAction(nameof(Details), new { id, tab = "directory" }); }
        if (vm.Enabled && await db.EntityDirectorySources.AnyAsync(d => d.Enabled && d.Domain == domain && d.EntityId != id))
        { Failure("This domain is already mapped to another entity."); return RedirectToAction(nameof(Details), new { id, tab = "directory" }); }
        static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, max)];
        var d = await db.EntityDirectorySources.FirstOrDefaultAsync(x => x.EntityId == id);
        if (d == null) { d = new EntityDirectorySource { EntityId = id }; db.EntityDirectorySources.Add(d); }
        d.Enabled = vm.Enabled; d.Domain = domain; d.LdapPath = Clean(vm.LdapPath, 500); d.SearchBase = Clean(vm.SearchBase, 500); d.BindUser = Clean(vm.BindUser, 256);
        var secretNote = "unchanged";
        if (vm.ClearSecret) { d.BindSecretProtected = null; secretNote = "cleared"; }
        else if (!string.IsNullOrEmpty(vm.BindSecret)) { d.BindSecretProtected = secrets.Protect(vm.BindSecret); secretNote = "replaced"; }
        d.UpdatedAt = DateTime.UtcNow;
        d.ValidationNote = "Pending validation on the Windows Server (domain reachability, trust and service account).";
        // the secret itself is never part of the audit details
        audit.Add("DIRECTORY_CONFIG_CHANGED", "Entity", id, Res.Ar("{0}: enabled={1}; domain={2}; ldap={3}; bind user={4}; secret {5}", node.Code, d.Enabled, d.Domain, d.LdapPath ?? "-", d.BindUser ?? "-", secretNote), id);
        await db.SaveChangesAsync();
        Success("Directory settings saved.");
        return RedirectToAction(nameof(Details), new { id, tab = "directory" });
    }
}
