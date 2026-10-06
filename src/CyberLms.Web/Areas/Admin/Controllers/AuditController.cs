using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

public class AuditController(AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index(string? q, string? action, int page = 1)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var s = q.Trim().ToLower(); query = query.Where(a => a.Username.ToLower().Contains(s) || (a.Details != null && a.Details.ToLower().Contains(s))); }
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        var pager = new Pager { Page = Math.Max(1, page), PageSize = 50, Total = await query.CountAsync() };
        ViewBag.Actions = await db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
        ViewBag.Q = q; ViewBag.Action = action; ViewBag.Pager = pager;
        return View(await query.OrderByDescending(a => a.Timestamp).Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize).ToListAsync());
    }
}
