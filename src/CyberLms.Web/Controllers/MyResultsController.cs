using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Controllers;

[Authorize]
public class MyResultsController(AppDbContext db) : AppController
{
    public async Task<IActionResult> Index()
    {
        var uid = User.UserId();
        ViewBag.Acks = await db.UserAcknowledgments.AsNoTracking().Include(a => a.Content).Where(a => a.UserId == uid).OrderByDescending(a => a.AcknowledgedAt).ToListAsync();
        ViewBag.Reads = await db.ContentCompletions.AsNoTracking().Include(r => r.Content).Where(r => r.UserId == uid && r.Content.Status == ContentStatus.Published).OrderByDescending(r => r.CompletedAt).ToListAsync();
        var items = (await LearningProgress.ForUser(db, uid)).Where(i => i.HasRequirements).ToList();
        ViewBag.ItemsDone = items.Count(i => i.IsCompleted); ViewBag.ItemsTotal = items.Count;
        var attempts = await db.AssessmentAttempts.AsNoTracking().Include(a => a.Assessment).ThenInclude(a => a.Content).Where(a => a.UserId == uid && a.Status == AttemptStatus.Completed).OrderByDescending(a => a.CompletedAt).ToListAsync();
        return View(attempts);
    }
}
