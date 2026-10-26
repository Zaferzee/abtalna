using System.Linq.Expressions;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CyberLms.Web.Services;

/// <summary>Read model of one entity (cached).</summary>
public record EntityNode(int Id, string Name, string Code, int? ParentId, string Path, int Depth, bool IsActive, bool EffectiveActive, Capability Allowed)
{
    public bool IsRoot => ParentId == null;
    /// <summary>Ids from the root down to this entity (inclusive), parsed from the materialized path.</summary>
    public List<int> AncestorIds => EntityTree.IdsOf(Path);
}

/// <summary>
/// The organizational tree: cached read model plus the operations that keep the materialized path consistent (create, move).
/// </summary>
public class EntityTree(IMemoryCache cache)
{
    private const string Key = "entities.tree";

    public static List<int> IdsOf(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

    public async Task<IReadOnlyList<EntityNode>> AllAsync(AppDbContext db)
    {
        if (cache.TryGetValue(Key, out IReadOnlyList<EntityNode>? hit) && hit != null) return hit;
        var rows = await db.Entities.AsNoTracking().OrderBy(e => e.Path).ToListAsync();
        var inactive = rows.Where(r => !r.IsActive).Select(r => r.Path).ToList();
        var list = rows.Select(r => new EntityNode(r.Id, r.Name, r.Code, r.ParentEntityId, r.Path, r.Depth, r.IsActive,
            !inactive.Any(p => r.Path.StartsWith(p, StringComparison.Ordinal)), r.AllowedCapabilities)).ToList();
        cache.Set(Key, (IReadOnlyList<EntityNode>)list, TimeSpan.FromMinutes(5));
        return list;
    }

    public void Invalidate() => cache.Remove(Key);

    public async Task<EntityNode> RootAsync(AppDbContext db) => (await AllAsync(db)).First(e => e.IsRoot);
    public async Task<EntityNode?> FindAsync(AppDbContext db, int id) => (await AllAsync(db)).FirstOrDefault(e => e.Id == id);

    /// <summary>The entity and every descendant.</summary>
    public static IEnumerable<EntityNode> Subtree(IEnumerable<EntityNode> all, EntityNode root) => all.Where(e => e.Path.StartsWith(root.Path, StringComparison.Ordinal));

    /// <summary>Tree order (pre-order by path) - the cached list is already ordered by path; this orders siblings by name.</summary>
    public static List<EntityNode> Ordered(IEnumerable<EntityNode> nodes)
    {
        var list = nodes.ToList();
        var byParent = list.ToLookup(n => n.ParentId);
        var ids = list.Select(n => n.Id).ToHashSet();
        var result = new List<EntityNode>();
        void Walk(EntityNode n) { result.Add(n); foreach (var c in byParent[n.Id].OrderBy(c => c.Name)) Walk(c); }
        foreach (var top in list.Where(n => n.ParentId == null || !ids.Contains(n.ParentId.Value)).OrderBy(n => n.Depth).ThenBy(n => n.Name)) Walk(top);
        return result;
    }

    /// <summary>Creates a child entity (path computed after the id is known).</summary>
    public async Task<OrgEntity> CreateAsync(AppDbContext db, OrgEntity parent, string name, string code, Capability allowed)
    {
        var e = new OrgEntity { Name = name, Code = code, ParentEntityId = parent.Id, Depth = parent.Depth + 1, AllowedCapabilities = allowed, Path = "" };
        db.Entities.Add(e);
        await db.SaveChangesAsync();
        e.Path = parent.Path + e.Id + "/";
        await db.SaveChangesAsync();
        Invalidate();
        return e;
    }

    /// <summary>Moves an entity under a new parent, rewriting the subtree's paths. Rejects cycles and moving the root.</summary>
    public async Task<bool> MoveAsync(AppDbContext db, OrgEntity e, OrgEntity newParent)
    {
        if (e.ParentEntityId == null || newParent.Path.StartsWith(e.Path, StringComparison.Ordinal)) return false;
        if (e.ParentEntityId == newParent.Id) return true;
        var oldPath = e.Path; var newPath = newParent.Path + e.Id + "/"; var delta = newParent.Depth + 1 - e.Depth;
        var subtree = await db.Entities.Where(x => x.Path.StartsWith(oldPath)).ToListAsync();
        foreach (var x in subtree) { x.Path = newPath + x.Path[oldPath.Length..]; x.Depth += delta; }
        e.ParentEntityId = newParent.Id;
        await db.SaveChangesAsync();
        Invalidate();
        return true;
    }
}

/// <summary>Set of entities in which the current user may exercise a capability.</summary>
public sealed class EntityScope
{
    public static readonly EntityScope Everything = new(true, new HashSet<int>());
    public static readonly EntityScope Nothing = new(false, new HashSet<int>());
    public bool All { get; }
    public IReadOnlySet<int> Ids { get; }
    private readonly List<int> _list;
    public EntityScope(bool all, HashSet<int> ids) { All = all; Ids = ids; _list = ids.ToList(); }
    public bool Any => All || Ids.Count > 0;
    public bool Contains(int entityId) => All || Ids.Contains(entityId);
    /// <summary>For EF queries (translated to = ANY(@ids)).</summary>
    public List<int> List => _list;
}

/// <summary>
/// Per-request authorization model for organizational scope. The single place that answers "may the current user do X in entity E"
/// and "which entities may the current user do X in". Controllers, services, queries and exports all use it; the UI only mirrors it.
/// * Central administrator (Admin role): every capability in every entity.
/// * Entity administrator: for each assignment, the granted capabilities ∩ the entity's AllowedCapabilities, in the assigned entity
///   and (when IncludeDescendants) its descendants - each descendant again capped by its own AllowedCapabilities. Disabled entities
///   (or entities under a disabled ancestor) grant nothing.
/// Out-of-scope records are reported as 404 by callers (no confirmation that a record exists in another entity).
/// </summary>
public class EntityAccess(AppDbContext db, EntityTree tree, IHttpContextAccessor http)
{
    private bool _loaded;
    private bool _central;
    private int _userId;
    private int _entityId;
    private IReadOnlyList<EntityNode> _all = [];
    private List<(EntityNode Entity, Capability Caps, bool Desc)> _assign = [];
    private readonly Dictionary<Capability, EntityScope> _scopes = new();

    private async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        _all = await tree.AllAsync(db);
        var p = http.HttpContext?.User;
        _userId = p?.UserId() ?? 0;
        if (_userId == 0) return;
        var u = await db.Users.AsNoTracking().Where(x => x.Id == _userId && x.IsActive)
            .Select(x => new { x.EntityId, Central = x.UserRoles.Any(r => r.Role.Name == RoleNames.Admin) }).FirstOrDefaultAsync();
        if (u == null) return;
        _entityId = u.EntityId;
        _central = u.Central;
        if (_central) return;
        var rows = await db.EntityAdminAssignments.AsNoTracking().Where(a => a.UserId == _userId).ToListAsync();
        _assign = rows.Select(a => (_all.FirstOrDefault(e => e.Id == a.EntityId), a.Capabilities, a.IncludeDescendants))
            .Where(x => x.Item1 is { EffectiveActive: true }).Select(x => (x.Item1!, x.Capabilities, x.IncludeDescendants)).ToList();
    }

    public async Task<bool> IsCentralAsync() { await LoadAsync(); return _central; }
    /// <summary>Central administrator or holder of at least one active assignment (may enter the administration area).</summary>
    public async Task<bool> IsAdminAsync() { await LoadAsync(); return _central || _assign.Any(a => (a.Caps & a.Entity.Allowed) != 0); }
    public async Task<int> UserEntityIdAsync() { await LoadAsync(); return _entityId; }
    public async Task<IReadOnlyList<EntityNode>> AllEntitiesAsync() { await LoadAsync(); return _all; }

    public async Task<EntityScope> ScopeAsync(Capability cap)
    {
        await LoadAsync();
        if (_scopes.TryGetValue(cap, out var s)) return s;
        if (_central) return _scopes[cap] = EntityScope.Everything;
        var ids = new HashSet<int>();
        foreach (var (entity, caps, desc) in _assign)
        {
            if ((caps & cap) != cap) continue;
            var targets = desc ? EntityTree.Subtree(_all, entity) : [entity];
            foreach (var t in targets) if (t.EffectiveActive && (t.Allowed & cap) == cap) ids.Add(t.Id);
        }
        return _scopes[cap] = ids.Count == 0 ? EntityScope.Nothing : new EntityScope(false, ids);
    }

    public async Task<bool> CanAsync(Capability cap) => (await ScopeAsync(cap)).Any;
    public async Task<bool> CanInAsync(Capability cap, int entityId) => (await ScopeAsync(cap)).Contains(entityId);

    /// <summary>Capabilities the current user holds in a given entity (for delegation: an administrator can never grant more).</summary>
    public async Task<Capability> CapabilitiesInAsync(int entityId)
    {
        await LoadAsync();
        if (_central) return Capability.All;
        var result = Capability.None;
        foreach (Capability c in Enum.GetValues<Capability>().Where(c => c is not (Capability.None or Capability.All or Capability.DefaultAllowed)))
            if (await CanInAsync(c, entityId)) result |= c;
        return result;
    }

    /// <summary>Entities (tree order) in which the user holds the capability.</summary>
    public async Task<List<EntityNode>> EntitiesAsync(Capability cap)
    {
        var s = await ScopeAsync(cap);
        return EntityTree.Ordered(_all.Where(e => s.Contains(e.Id) && (s.All || e.EffectiveActive)));
    }

    /// <summary>Union of entities the user administers in any capability (what the entity pages may show).</summary>
    public async Task<EntityScope> AnyScopeAsync()
    {
        await LoadAsync();
        if (_central) return EntityScope.Everything;
        var ids = new HashSet<int>();
        foreach (Capability c in Enum.GetValues<Capability>().Where(c => c is not (Capability.None or Capability.All or Capability.DefaultAllowed)))
            foreach (var id in (await ScopeAsync(c)).Ids) ids.Add(id);
        return ids.Count == 0 ? EntityScope.Nothing : new EntityScope(false, ids);
    }

    /// <summary>The entity new records are attached to by default: the root for the central administrator, otherwise the
    /// administrator's own entity when in scope, else the highest entity in scope.</summary>
    public async Task<int?> DefaultEntityAsync(Capability cap)
    {
        await LoadAsync();
        if (_central) return _all.First(e => e.IsRoot).Id;
        var s = await ScopeAsync(cap);
        if (s.Contains(_entityId)) return _entityId;
        return _all.Where(e => s.Contains(e.Id)).OrderBy(e => e.Depth).Select(e => (int?)e.Id).FirstOrDefault();
    }
}

/// <summary>Who may see which content / assessment (audience targeting). Pure expressions so every query applies the same rule.</summary>
public static class EntityVisibility
{
    /// <summary>The viewer's entity and its ancestor chain (root first).</summary>
    public record Viewer(int EntityId, List<int> Ancestors);

    public static async Task<Viewer> ForUserAsync(AppDbContext db, int userId)
    {
        var path = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Entity.Path).FirstOrDefaultAsync();
        var ids = path == null ? [] : EntityTree.IdsOf(path);
        return new Viewer(ids.Count > 0 ? ids[^1] : 0, ids);
    }

    public static Expression<Func<Content, bool>> Content(Viewer v)
    {
        int e = v.EntityId; var anc = v.Ancestors;
        return c => (c.TargetScope == ContentTargetScope.OwnerOnly && c.OwnerEntityId == e)
                 || (c.TargetScope == ContentTargetScope.OwnerAndDescendants && anc.Contains(c.OwnerEntityId))
                 || (c.TargetScope == ContentTargetScope.Selected && c.Targets.Any(t => t.EntityId == e || (t.IncludeDescendants && anc.Contains(t.EntityId))));
    }

    /// <summary>A linked assessment follows its content's audience; a standalone one is visible to its owner's subtree.</summary>
    public static Expression<Func<Assessment, bool>> Assessment(Viewer v)
    {
        int e = v.EntityId; var anc = v.Ancestors;
        return a => (a.ContentId == null && anc.Contains(a.OwnerEntityId))
                 || (a.ContentId != null && (
                        (a.Content!.TargetScope == ContentTargetScope.OwnerOnly && a.Content.OwnerEntityId == e)
                     || (a.Content.TargetScope == ContentTargetScope.OwnerAndDescendants && anc.Contains(a.Content.OwnerEntityId))
                     || (a.Content.TargetScope == ContentTargetScope.Selected && a.Content.Targets.Any(t => t.EntityId == e || (t.IncludeDescendants && anc.Contains(t.EntityId))))));
    }
}

// ------------------------------------------------------------------------------------------------ authorization plumbing

/// <summary>Admin area entry: central administrator or an active entity-administration assignment (checked against the database).</summary>
public class AdminAreaRequirement : IAuthorizationRequirement { }

public class AdminAreaHandler(IHttpContextAccessor http) : AuthorizationHandler<AdminAreaRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminAreaRequirement requirement)
    {
        var access = http.HttpContext?.RequestServices.GetService<EntityAccess>();
        if (access != null && context.User.Identity?.IsAuthenticated == true && await access.IsAdminAsync()) context.Succeed(requirement);
    }
}

/// <summary>Requires the capability in at least one entity (record-level scope is then checked by the action through EntityAccess).
/// Several attributes on a controller/action all apply. Answers 403.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CapabilityAttribute(Capability capability) : Attribute, IAsyncAuthorizationFilter
{
    public Capability Capability { get; } = capability;
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return; // the policy challenges first
        var access = context.HttpContext.RequestServices.GetRequiredService<EntityAccess>();
        if (!await access.CanAsync(Capability)) context.Result = new ForbidResult();
    }
}

/// <summary>Central administrator only (system-wide settings, entity structure).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CentralOnlyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;
        var access = context.HttpContext.RequestServices.GetRequiredService<EntityAccess>();
        if (!await access.IsCentralAsync()) context.Result = new ForbidResult();
    }
}

/// <summary>Scope filters for queries (one place, so lists, details, reports and exports filter identically).</summary>
public static class ScopeQueries
{
    public static IQueryable<User> InScope(this IQueryable<User> q, EntityScope s) => s.All ? q : q.Where(u => s.List.Contains(u.EntityId));
    public static IQueryable<Content> InScope(this IQueryable<Content> q, EntityScope s) => s.All ? q : q.Where(c => s.List.Contains(c.OwnerEntityId));
    public static IQueryable<Assessment> InScope(this IQueryable<Assessment> q, EntityScope s) => s.All ? q : q.Where(a => s.List.Contains(a.OwnerEntityId));
    /// <summary>Users of an entity subtree (drill-down filter).</summary>
    public static IQueryable<User> InSubtree(this IQueryable<User> q, string? path) => path == null ? q : q.Where(u => u.Entity.Path.StartsWith(path));
}

/// <summary>Model of the shared entity picker (Views/Shared/_EntitySelect.cshtml).</summary>
public record EntitySelectVm(string Name, IReadOnlyList<EntityNode> Entities, int? Selected, string? AllLabel = null, string? Id = null, bool Required = false);
