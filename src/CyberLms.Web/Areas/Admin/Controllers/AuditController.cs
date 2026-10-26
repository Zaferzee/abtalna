using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using CyberLms.Web.Data;
using CyberLms.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>Read-only audit trail (append-only table; nothing here edits or deletes entries). The central administrator sees the
/// complete history (optionally filtered by affected entity subtree); an entity administrator with ViewAudit sees only entries whose
/// affected entity is in scope.</summary>
[Capability(Capability.ViewAudit)]
public class AuditController(AppDbContext db, EntityAccess access) : AdminController
{
    // The action filter is bound from "?op=". It must NOT be called "action": that name is MVC's reserved route value
    // (the action name, "Index"), which model binding prefers over the query string; the list was then always filtered
    // by Action == "Index" and came back empty.
    public async Task<IActionResult> Index(string? q, [FromQuery(Name = "op")] string? op, int? entity, int page = 1)
    {
        var scope = await access.ScopeAsync(Capability.ViewAudit);
        var all = await access.AllEntitiesAsync();
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!scope.All) query = query.Where(a => a.ScopeEntityId != null && scope.List.Contains(a.ScopeEntityId.Value));
        if (entity is int eid && scope.Contains(eid) && all.FirstOrDefault(e => e.Id == eid) is { } node)
        {
            var ids = EntityTree.Subtree(all, node).Select(e => e.Id).ToList();
            query = query.Where(a => a.ScopeEntityId != null && ids.Contains(a.ScopeEntityId.Value));
        }
        var baseQuery = query;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(a => a.Username.ToLower().Contains(s) || (a.Details != null && a.Details.ToLower().Contains(s)) || a.Action.ToLower().Contains(s) || (a.EntityId != null && a.EntityId == q.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(op)) query = query.Where(a => a.Action == op);
        var pager = new Pager { Page = Math.Max(1, page), PageSize = 50, Total = await query.CountAsync() };
        ViewBag.Actions = await baseQuery.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
        ViewBag.Q = q; ViewBag.Op = op; ViewBag.Pager = pager; ViewBag.EntityId = entity;
        ViewBag.All = await baseQuery.CountAsync();
        ViewBag.Entities = await access.EntitiesAsync(Capability.ViewAudit);
        ViewBag.EntityNames = all.ToDictionary(e => e.Id, e => e.Name);
        return View(await query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize).ToListAsync());
    }
}
