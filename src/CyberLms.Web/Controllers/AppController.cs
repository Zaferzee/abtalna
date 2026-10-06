using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CyberLms.Web.Controllers;

/// <summary>Base controller: gives access to the localizer so user-facing messages come from resources, not code.</summary>
public abstract class AppController : Controller
{
    private Localizer? _l;
    protected Localizer L => _l ??= HttpContext.RequestServices.GetRequiredService<Localizer>();

    protected void Success(string key, params object?[] args) => TempData["Success"] = L.Format(key, args);
    protected void Failure(string key, params object?[] args) => TempData["Error"] = L.Format(key, args);
}
