using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

public class DashboardController(ReportService reports, AppDbContext db) : AdminController
{
    public async Task<IActionResult> Index() =>
        View(new DashboardVm { S = await reports.SummaryAsync(), Recent = await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Timestamp).Take(8).ToListAsync() });
}
