using ClosedXML.Excel;
using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>
/// Reports. Every query (pages, exports, reminders) is restricted to the employees of the administrator's scope: ViewReports for the
/// pages, ExportReports for files, ManageNotifications for reminders. The optional entity filter drills into one subtree (only
/// within scope). Central administrators see every entity and the entity comparison.
/// </summary>
[Capability(Capability.ViewReports)]
public class ReportsController(ReportService reports, AppDbContext db, TimeDisplay time, AuditService audit, NotificationService notify, EntityAccess access) : AdminController
{
    private const int PageSize = 50;

    /// <summary>Sets the authorization scope and resolves the drill-down entity (ignored when outside the scope).</summary>
    private async Task Scope(ReportFilter f, Capability cap)
    {
        f.Scope = await access.ScopeAsync(cap);
        var node = f.EntityId is int id ? (await access.AllEntitiesAsync()).FirstOrDefault(e => e.Id == id) : null;
        if (node == null || !f.Scope.Contains(node.Id)) f.EntityId = null;
        f.EntityPath = f.EntityId == null ? null : node!.Path;
    }

    /// <summary>Items an administrator can pick in the filters: those targeted to at least one entity in scope.</summary>
    private async Task<ReportVm> Base(ReportFilter f, int page)
    {
        var all = await access.AllEntitiesAsync();
        bool Relevant(Content? c, int owner)
        {
            if (f.Scope.All) return true;
            var audience = c != null ? ContentAuthoring.AudienceEntityIds(c, all) : EntityTree.Subtree(all, all.First(e => e.Id == owner)).Select(e => e.Id).ToHashSet();
            return audience.Any(f.Scope.Contains);
        }
        var asm = await db.Assessments.AsNoTracking().Include(a => a.Content).ThenInclude(c => c!.Targets).OrderBy(a => a.Title).ToListAsync();
        var cnt = await db.Contents.AsNoTracking().Include(c => c.Targets).Where(c => c.RequiresAcknowledgment).OrderBy(c => c.Title).ToListAsync();
        var vm = new ReportVm
        {
            Filter = f, Pager = new Pager { Page = Math.Max(1, page), PageSize = PageSize },
            AllAssessments = asm.Where(a => Relevant(a.Content, a.OwnerEntityId)).ToList(),
            AllContent = cnt.Where(c => Relevant(c, c.OwnerEntityId)).ToList(),
            Entities = await access.EntitiesAsync(Capability.ViewReports),
            CanExport = await access.CanAsync(Capability.ExportReports),
            CanRemind = await access.CanAsync(Capability.ManageNotifications),
        };
        vm.DrillEntity = f.EntityId == null ? null : all.FirstOrDefault(e => e.Id == f.EntityId);
        return vm;
    }

    /// <summary>Exports need ExportReports; the file is limited to that scope (∩ the drill-down).</summary>
    private async Task<bool> ExportScope(ReportFilter f)
    {
        if (!await access.CanAsync(Capability.ExportReports)) return false;
        var requested = f.EntityId;
        await Scope(f, Capability.ExportReports);
        if (requested != null && f.EntityId == null) return false;
        return true;
    }

    /// <summary>Entity comparison: completion, acknowledgment and pass rates per entity (children of the drill-down entity).</summary>
    public async Task<IActionResult> Entities(ReportFilter f)
    {
        await Scope(f, Capability.ViewReports);
        var vm = await Base(f, 1);
        var all = await access.AllEntitiesAsync();
        List<EntityNode> rows;
        if (vm.DrillEntity is { } d) rows = [d, .. all.Where(e => e.ParentId == d.Id && f.Scope.Contains(e.Id)).OrderBy(e => e.Name)];
        else if (f.Scope.All) { var root = all.First(e => e.IsRoot); rows = [root, .. all.Where(e => e.ParentId == root.Id).OrderBy(e => e.Name)]; }
        else rows = vm.Entities.Where(e => e.ParentId == null || !f.Scope.Contains(e.ParentId.Value)).ToList();
        vm.Breakdown = await reports.EntityBreakdownAsync(f, rows, all);
        return View(vm);
    }

    public IActionResult Index() => RedirectToAction(nameof(Assessments));

    public async Task<IActionResult> Assessments(ReportFilter f, int page = 1, string? export = null)
    {
        if (export != null)
        {
            if (!await ExportScope(f)) return Forbid();
            var all = await reports.AssessmentResultsAllAsync(f);
            audit.Add("REPORT_EXPORTED", "Report", "AssessmentResults", Res.Ar("{0} rows, {1}", all.Count, export), f.EntityId); await db.SaveChangesAsync();
            return Export(export, L["File: assessment results"], H("Username", "Name", "Department", "Email", "Assessment", "Status", "Best score %", "Attempts", "Last attempt date"),
                all.Select(r => (IEnumerable<object?>)[r.Username, r.DisplayName, r.Department, r.Email, r.Assessment, L[Labels.Status(r.Status)], r.BestPercentage, r.Attempts, time.Export(r.LastAttempt)]));
        }
        await Scope(f, Capability.ViewReports);
        var vm = await Base(f, page);
        (vm.Assessments, vm.Pager.Total) = await reports.AssessmentResultsAsync(f, vm.Pager.Page, PageSize);
        return View(vm);
    }

    public async Task<IActionResult> Acknowledgments(ReportFilter f, int page = 1, string? export = null)
    {
        if (export != null)
        {
            if (!await ExportScope(f)) return Forbid();
            var all = await reports.AcknowledgmentsAllAsync(f);
            audit.Add("REPORT_EXPORTED", "Report", "Acknowledgments", Res.Ar("{0} rows, {1}", all.Count, export), f.EntityId); await db.SaveChangesAsync();
            return Export(export, L["File: acknowledgments"], H("Username", "Name", "Department", "Email", "Content", "Status", "Acknowledgment date"),
                all.Select(r => (IEnumerable<object?>)[r.Username, r.DisplayName, r.Department, r.Email, r.Content, L[Labels.Status(r.Status)], time.Export(r.AcknowledgedAt)]));
        }
        await Scope(f, Capability.ViewReports);
        var vm = await Base(f, page);
        (vm.Acks, vm.Pager.Total) = await reports.AcknowledgmentsAsync(f, vm.Pager.Page, PageSize);
        return View(vm);
    }

    public async Task<IActionResult> Attempts(ReportFilter f, int page = 1, string? export = null)
    {
        if (export != null)
        {
            if (!await ExportScope(f)) return Forbid();
            var all = await reports.AttemptsAllAsync(f);
            audit.Add("REPORT_EXPORTED", "Report", "Attempts", Res.Ar("{0} rows, {1}", all.Count, export), f.EntityId); await db.SaveChangesAsync();
            return Export(export, L["File: attempts"], H("Username", "Name", "Assessment", "Assessment date", "Correct answers", "Total questions", "Score %", "Result"),
                all.Select(r => (IEnumerable<object?>)[r.Username, r.DisplayName, r.Assessment, time.Export(r.Completed), r.Correct, r.Total, r.Percentage, L[r.Passed == true ? "Passed" : "Failed"]]));
        }
        await Scope(f, Capability.ViewReports);
        var vm = await Base(f, page);
        (vm.Attempts, vm.Pager.Total) = await reports.AttemptsAsync(f, vm.Pager.Page, PageSize);
        return View(vm);
    }

    public async Task<IActionResult> Attempt(int id)
    {
        var users = db.Users.InScope(await access.ScopeAsync(Capability.ViewReports)).Select(u => u.Id);
        var a = await db.AssessmentAttempts.AsNoTracking().Include(x => x.User).Include(x => x.Assessment).FirstOrDefaultAsync(x => x.Id == id && users.Contains(x.UserId));
        if (a == null) return NotFound();
        var answers = await db.AssessmentAnswers.AsNoTracking().Where(x => x.AttemptId == id).ToDictionaryAsync(x => x.QuestionId);
        var qs = await db.Questions.AsNoTracking().Include(q => q.Options).Where(q => answers.Keys.Contains(q.Id)).OrderBy(q => q.SortOrder).ToListAsync();
        return View(new AttemptDetailVm { Attempt = a, Questions = qs, Answers = answers });
    }

    /// <summary>Sends a reminder to everyone currently shown as "not attempted" / "not acknowledged" for the filter.</summary>
    [HttpPost, Capability(Capability.ManageNotifications)]
    public async Task<IActionResult> Remind(string kind, ReportFilter f)
    {
        await Scope(f, Capability.ManageNotifications);
        if (!notify.Ready) { Failure("SMTP is not configured (Settings → Email)."); return RedirectToAction(kind == "ack" ? nameof(Acknowledgments) : nameof(Assessments), f); }
        int n;
        if (kind == "ack")
        {
            f.Status = ReportService.NotAcknowledged;
            var rows = await reports.AcknowledgmentsAllAsync(f);
            n = 0;
            foreach (var g in rows.GroupBy(r => (r.Email, r.DisplayName)))
                n += notify.Send([(g.Key.Email, g.Key.DisplayName)], "Reminder: policy acknowledgment required", "You still need to acknowledge: {0}", [string.Join(Res.Ar("List separator"), g.Select(r => r.Content))], "/Content/Policies");
        }
        else
        {
            f.Status = ReportService.NotAttempted;
            var rows = await reports.AssessmentResultsAllAsync(f);
            n = 0;
            foreach (var g in rows.GroupBy(r => (r.Email, r.DisplayName)))
                n += notify.Send([(g.Key.Email, g.Key.DisplayName)], "Reminder: assessment pending", "You have not yet attempted: {0}", [string.Join(Res.Ar("List separator"), g.Select(r => r.Assessment))], "/Assessments");
        }
        audit.Add("REMINDER_QUEUED", "Report", kind, Res.Ar("{0} emails", n));
        await db.SaveChangesAsync();
        Success("{0} reminder email(s) queued (users without an email address are skipped).", n);
        return RedirectToAction(kind == "ack" ? nameof(Acknowledgments) : nameof(Assessments), new { f.AssessmentId, f.ContentId, f.EntityId });
    }

    private string[] H(params string[] keys) => keys.Select(k => L[k]).ToArray();

    private IActionResult Export(string format, string name, string[] header, IEnumerable<IEnumerable<object?>> rows)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        if (format == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet(name);
            for (var c = 0; c < header.Length; c++) { ws.Cell(1, c + 1).Value = header[c]; ws.Cell(1, c + 1).Style.Font.Bold = true; }
            var r = 2;
            foreach (var row in rows)
            {
                var c = 1;
                // Everything is written as text via Csv.Safe so values can never be interpreted as formulas.
                foreach (var v in row) ws.Cell(r, c++).SetValue(Csv.Safe(v));
                r++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name}-{stamp}.xlsx");
        }
        return File(Csv.Build(header, rows), "text/csv; charset=utf-8", $"{name}-{stamp}.csv");
    }
}
