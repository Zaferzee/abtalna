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
public class HomeController(AppDbContext db, ILogger<HomeController> log) : AppController
{
    public async Task<IActionResult> Index()
    {
        var uid = User.UserId();
        var items = await LearningProgress.ForUser(db, uid);
        var vm = new UserDashboardVm
        {
            Items = items,
            RecentContent = items.Where(i => i.Content != null).Select(i => i.Content!).OrderByDescending(c => c.PublishedAt).Take(6).ToList(),
            Completed = await db.AssessmentAttempts.AsNoTracking().Include(a => a.Assessment)
                .Where(a => a.UserId == uid && a.Status == AttemptStatus.Completed).OrderByDescending(a => a.CompletedAt).Take(5).ToListAsync(),
        };
        return View(vm);
    }

    [AllowAnonymous, IgnoreAntiforgeryToken]
    public IActionResult Error()
    {
        // Reached through the exception handler: classify the failure so the user gets a precise (Arabic) message; details are in the server log.
        var ex = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var code = 500;
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is Microsoft.AspNetCore.Http.BadHttpRequestException { StatusCode: 413 } || (e is InvalidDataException && e.Message.Contains("length limit", StringComparison.OrdinalIgnoreCase))) { code = 413; break; }
            if (e is System.Data.Common.DbException || e is System.Net.Sockets.SocketException) { code = 503; break; }
        }
        if (code != 500) log.LogWarning(ex, "Request {RequestId} failed with a handled condition ({Code}).", HttpContext.TraceIdentifier, code);
        Response.StatusCode = code;
        return View("Status", new ErrorVm { Code = code, RequestId = HttpContext.TraceIdentifier });
    }

    /// <summary>Friendly Arabic page for 400/403/404/500 (re-executed by the status-code middleware).</summary>
    [AllowAnonymous, IgnoreAntiforgeryToken] // re-executed for failed POSTs (e.g. CSRF 400), so it must not demand a token itself
    public IActionResult Status()
    {
        // Read the code from the query string directly: parameter model binding would also read the (possibly huge) request body of a failed POST.
        var code = int.TryParse(Request.Query["code"], out var c) && c is >= 400 and <= 599 ? c : 500;
        return View(new ErrorVm { Code = code, RequestId = HttpContext.TraceIdentifier });
    }
}

