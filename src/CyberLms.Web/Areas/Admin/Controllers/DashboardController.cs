using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>Administration home. Figures are limited to the administrator's ViewReports scope and the recent activity to the
/// ViewAudit scope (entity administrators: their entities only; central administrator: everything).</summary>
public class DashboardController(ReportService reports, AppDbContext db, EntityAccess access) : AdminController
{
    public async Task<IActionResult> Index()
    {
        var reportScope = await access.ScopeAsync(Capability.ViewReports);
        var auditScope = await access.ScopeAsync(Capability.ViewAudit);
        var recent = db.AuditLogs.AsNoTracking();
        if (!auditScope.All) recent = recent.Where(a => a.ScopeEntityId != null && auditScope.List.Contains(a.ScopeEntityId.Value));
        var vm = new DashboardVm
        {
            S = await reports.SummaryAsync(reportScope),
            Recent = auditScope.Any ? await recent.OrderByDescending(a => a.Timestamp).Take(8).ToListAsync() : [],
            Central = await access.IsCentralAsync(),
            ScopeEntities = (await access.AnyScopeAsync()) is { All: false } any ? EntityTree.Ordered((await access.AllEntitiesAsync()).Where(e => any.Contains(e.Id))) : [],
        };
        foreach (var c in Enum.GetValues<Capability>().Where(c => c is not (Capability.None or Capability.All or Capability.DefaultAllowed)))
            if (await access.CanAsync(c)) vm.Capabilities |= c;
        return View(vm);
    }
}
