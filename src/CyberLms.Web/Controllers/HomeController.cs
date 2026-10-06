using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Controllers;

[Authorize]
public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var uid = User.UserId();
        var published = db.Contents.AsNoTracking().Where(c => c.Status == ContentStatus.Published);
        var ackedIds = db.UserAcknowledgments.Where(a => a.UserId == uid).Select(a => new { a.ContentId, a.ContentVersion });
        var vm = new UserDashboardVm
        {
            RecentContent = await published.OrderByDescending(c => c.PublishedAt).Take(6).ToListAsync(),
            PendingAcks = await published.Where(c => c.RequiresAcknowledgment &&
                !db.UserAcknowledgments.Any(a => a.UserId == uid && a.ContentId == c.Id && a.ContentVersion == c.Version)).OrderBy(c => c.Title).ToListAsync(),
            Completed = await db.AssessmentAttempts.AsNoTracking().Include(a => a.Assessment)
                .Where(a => a.UserId == uid && a.Status == AttemptStatus.Completed).OrderByDescending(a => a.CompletedAt).Take(10).ToListAsync(),
        };
        var passedIds = await db.AssessmentAttempts.Where(a => a.UserId == uid && a.Passed == true).Select(a => a.AssessmentId).Distinct().ToListAsync();
        vm.AvailableAssessments = await db.Assessments.AsNoTracking()
            .Where(a => a.IsPublished && a.Questions.Any() && !passedIds.Contains(a.Id)).OrderBy(a => a.Title).ToListAsync();
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => View(new ErrorVm { RequestId = HttpContext.TraceIdentifier });
}

