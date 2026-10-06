using CyberLms.Web.Data;
using CyberLms.Web.Models;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Controllers;

public class AccountController(AuthService auth, AppDbContext db, PasswordService passwords, AuditService audit, IConfiguration cfg) : AppController
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect("/");
        ViewBag.Auth = auth;
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        ViewBag.Auth = auth;
        if (!ModelState.IsValid) return View(vm);
        var (user, error) = await auth.ValidateLocalAsync(vm.Username, vm.Password);
        if (user == null)
        {
            audit.Add("LOGIN_FAILED", "User", null, Res.Ar("Username: {0}", vm.Username));
            await db.SaveChangesAsync();
            vm.Error = L[error!]; vm.Password = "";
            return View(vm);
        }
        await auth.SignInAsync(HttpContext, user);
        return LocalRedirect(Url.IsLocalUrl(vm.ReturnUrl) ? vm.ReturnUrl! : "/");
    }

    /// <summary>Windows/AD sign-in. Under IIS the IIS handler negotiates; under Kestrel the Negotiate handler does.</summary>
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> WindowsLogin(string? returnUrl)
    {
        if (!auth.WindowsEnabled) return NotFound();
        var scheme = !string.IsNullOrEmpty(cfg["ASPNETCORE_IIS_HTTPAUTH"]) ? "Windows" : NegotiateDefaults.AuthenticationScheme;
        var result = await HttpContext.AuthenticateAsync(scheme);
        if (!result.Succeeded || result.Principal?.Identity?.Name is not { } name) return Challenge(scheme);
        var (user, error) = await auth.ResolveWindowsAsync(name);
        if (user == null) { TempData["Error"] = L[error!]; return RedirectToAction(nameof(Login)); }
        await auth.SignInAsync(HttpContext, user);
        await db.SaveChangesAsync();
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Denied() => View();

    [Authorize, HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == User.UserId());
        ViewBag.Local = u.PasswordHash != null;
        return View(new ChangePasswordVm());
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        var u = await db.Users.Include(x => x.UserRoles).ThenInclude(r => r.Role).FirstAsync(x => x.Id == User.UserId());
        ViewBag.Local = u.PasswordHash != null;
        if (u.PasswordHash == null) { ModelState.AddModelError("", L["This account signs in with Windows; no password to change."]); return View(vm); }
        if (!passwords.Verify(u, vm.Current ?? "")) ModelState.AddModelError(nameof(vm.Current), L["Current password is incorrect."]);
        if (PasswordService.Validate(vm.New) is { } e) ModelState.AddModelError(nameof(vm.New), L[e]);
        if (!ModelState.IsValid) return View(vm);
        u.PasswordHash = passwords.Hash(u, vm.New);
        u.MustChangePassword = false;
        audit.Add("PASSWORD_CHANGED", "User", u.Id);
        await db.SaveChangesAsync();
        await auth.SignInAsync(HttpContext, u); // refresh claims
        Success("Password changed.");
        return LocalRedirect("/");
    }

    [AllowAnonymous, HttpPost]
    public IActionResult SetLanguage(string lang, string? returnUrl)
    {
        if (lang is "ar" or "en")
            Response.Cookies.Append(CultureSetup.CookieName,
                Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(lang == "ar" ? "ar-SA" : "en-US")),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
