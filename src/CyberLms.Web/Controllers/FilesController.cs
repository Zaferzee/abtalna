using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Controllers;

public class FilesController(AppDbContext db, StorageService storage, SettingsService settings) : AppController
{
    /// <summary>Authenticated download/stream of a content attachment. Non-admins can only reach attachments of published content.</summary>
    [Authorize]
    public async Task<IActionResult> Attachment(int id, bool download = false)
    {
        var a = await db.ContentAttachments.AsNoTracking().Include(x => x.Content).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        if (a.Content.Status != ContentStatus.Published && !User.IsInRole(RoleNames.Admin)) return NotFound();
        var path = storage.Resolve(a.StoredPath);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        var stream = System.IO.File.OpenRead(path);
        var type = StorageService.ContentTypeFor(a.StoredPath);
        // enableRangeProcessing lets browsers seek inside videos.
        return download || a.Kind == AttachmentKind.Document && type != "application/pdf"
            ? File(stream, type, a.FileName, enableRangeProcessing: true)
            : File(stream, type, enableRangeProcessing: true);
    }

    /// <summary>Images embedded in rich-text content (uploaded through the editor). Any signed-in user may view them.</summary>
    [Authorize, HttpGet("Files/Inline/{name}")]
    public IActionResult Inline(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name ?? "", "^[0-9a-f]{32}\\.(png|jpe?g|gif|webp)$")) return NotFound();
        var path = storage.Resolve("inline/" + name);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "private, max-age=86400";
        return PhysicalFile(path, StorageService.ContentTypeFor(path));
    }

    /// <summary>Branding images are public (needed on the login page).</summary>
    [AllowAnonymous]
    public IActionResult Brand(string kind)
    {
        var key = kind switch { "logo" => Branding.Keys.Logo, "favicon" => Branding.Keys.Favicon, "loginbg" => Branding.Keys.LoginBgImage, _ => null };
        var rel = key == null ? null : settings.Get(key);
        var path = rel == null ? null : storage.Resolve(rel);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400"; // URL carries ?v=<version>, so changes show immediately
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return PhysicalFile(path, StorageService.ContentTypeFor(path));
    }

    [AllowAnonymous, Route("branding/theme.css")]
    public IActionResult ThemeCss()
    {
        var b = Branding.From(settings);
        string C(string v, string fb) => Branding.ColorRegex.IsMatch(v) ? v : fb; // only validated #rrggbb values reach the CSS
        var css = $$"""
            :root{
              --brand-primary:{{C(b.PrimaryColor, "#0d47a1")}};
              --brand-secondary:{{C(b.SecondaryColor, "#546e7a")}};
              --brand-accent:{{C(b.AccentColor, "#00897b")}};
              --brand-header:{{C(b.HeaderColor, "#0b2a5b")}};
              --brand-header-text:{{C(b.HeaderTextColor, "#ffffff")}};
              --brand-sidebar:{{C(b.SidebarColor, "#f1f4f9")}};
              --brand-sidebar-text:{{C(b.SidebarTextColor, "#1f2d3d")}};
              --brand-login-bg:{{C(b.LoginBackgroundColor, "#e8eef7")}};
              --bs-primary:var(--brand-primary);--bs-link-color:var(--brand-primary);--bs-secondary:var(--brand-secondary);--bs-info:var(--brand-accent);
            }
            """;
        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(css, "text/css");
    }
}
