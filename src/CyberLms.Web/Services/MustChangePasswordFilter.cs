using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CyberLms.Web.Services;

/// <summary>Forces users flagged MustChangePassword (e.g. the seeded admin, reset passwords) to change it first.</summary>
public class MustChangePasswordFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.FindFirst("must_change_password")?.Value != "1") return;
        var controller = ctx.RouteData.Values["controller"]?.ToString();
        if (controller == "Account" || controller == "Files") return;
        ctx.Result = new RedirectToActionResult("ChangePassword", "Account", new { area = "" });
    }
    public void OnActionExecuted(ActionExecutedContext ctx) { }
}
