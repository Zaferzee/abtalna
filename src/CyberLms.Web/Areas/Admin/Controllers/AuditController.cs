using CyberLms.Web.Data;
using CyberLms.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>Read-only audit trail (append-only table; nothing here edits or deletes entries).</summary>
public class AuditController(AppDbContext db) : AdminController
{
    // The action filter is bound from "?op=". It must NOT be called "action": that name is MVC's reserved route value
    // (the action name, "Index"), which model binding prefers over the query string; the list was then always filtered
    // by Action == "Index" and came back empty.
    public async Task<IActionResult> Index(string? q, [FromQuery(Name = "op")] string? op, int page = 1)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(a => a.Username.ToLower().Contains(s) || (a.Details != null && a.Details.ToLower().Contains(s)) || a.Action.ToLower().Contains(s) || (a.EntityId != null && a.EntityId == q.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(op)) query = query.Where(a => a.Action == op);
        var pager = new Pager { Page = Math.Max(1, page), PageSize = 50, Total = await query.CountAsync() };
        ViewBag.Actions = await db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
        ViewBag.Q = q; ViewBag.Op = op; ViewBag.Pager = pager;
        ViewBag.All = await db.AuditLogs.CountAsync();
        return View(await query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize).ToListAsync());
    }
}
