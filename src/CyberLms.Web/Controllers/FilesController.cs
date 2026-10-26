using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Controllers;

public class FilesController(AppDbContext db, StorageService storage, SettingsService settings, BrandingAccessor brand, EntityBrandingService entityBranding, EntityAccess access, ILogger<FilesController> log) : AppController
{
    /// <summary>Authenticated download/stream of a content attachment: employees only for published content targeted to their
    /// entity; administrators for content they may manage (any status).</summary>
    [Authorize]
    public async Task<IActionResult> Attachment(int id, bool download = false)
    {
        var a = await db.ContentAttachments.AsNoTracking().Include(x => x.Content).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        var manage = await access.CanInAsync(Capability.ManageContent, a.Content.OwnerEntityId);
        if (!manage)
        {
            var viewer = await EntityVisibility.ForUserAsync(db, User.UserId());
            var visible = a.Content.Status == ContentStatus.Published && await db.Contents.Where(c => c.Id == a.ContentId).Where(EntityVisibility.Content(viewer)).AnyAsync();
            if (!visible) return NotFound();
        }
        var path = storage.Resolve(a.StoredPath);
        if (path == null || !System.IO.File.Exists(path))
        {
            log.LogWarning("Attachment {AttachmentId} (content {ContentId}) is registered but its file is missing or its path is invalid: {StoredPath}", a.Id, a.ContentId, a.StoredPath);
            return NotFound();
        }
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

    /// <summary>Central branding images are public (needed on the login page). An entity's own images (<paramref name="e"/>) are
    /// served to signed-in users of that entity subtree and to administrators with scope over it.</summary>
    [AllowAnonymous]
    public async Task<IActionResult> Brand(string kind, int? e = null)
    {
        var key = Branding.Assets.FirstOrDefault(a => a.Kind == kind).Key;
        if (e != null)
        {
            if (User.Identity?.IsAuthenticated != true) return NotFound();
            var mine = (await EntityVisibility.ForUserAsync(db, User.UserId())).Ancestors.Contains(e.Value);
            if (!mine && !(await access.AnyScopeAsync()).Contains(e.Value)) return NotFound();
        }
        var rel = key == null ? null : e == null ? settings.Get(key) : entityBranding.AssetPath(e, key);
        var path = rel == null ? null : storage.Resolve(rel);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400"; // URL carries ?v=<version>, so changes show immediately
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return PhysicalFile(path, StorageService.ContentTypeFor(path));
    }

    [AllowAnonymous, Route("branding/theme.css")]
    public IActionResult ThemeCss()
    {
        // Only validated #rrggbb values reach the CSS (Branding falls back to defaults for anything else). Signed-in users get their
        // entity's effective branding (inherited / overridden); anonymous pages get the central branding.
        var b = brand.Current;
        var primary = b.PrimaryColor; var accent = b.AccentColor; var bg = b["BackgroundColor"];
        var css = $$"""
            :root{
              --color-primary:{{primary}};
              --color-secondary:{{b.SecondaryColor}};
              --color-accent:{{accent}};
              --color-on-primary:{{OnColor(primary)}};
              --color-on-accent:{{OnColor(accent)}};
              --color-bg:{{bg}};
              --color-surface:{{b["SurfaceColor"]}};
              --color-surface-alt:color-mix(in srgb, {{bg}} 96%, {{primary}});
              --color-header:{{b.HeaderColor}};
              --color-on-header:{{b.HeaderTextColor}};
              --color-sidebar:{{b.SidebarColor}};
              --color-on-sidebar:{{b.SidebarTextColor}};
              --color-button:{{b.ButtonColor}};
              --color-on-button:{{b.ButtonTextColor}};
              --color-on-hero:{{b["HeroTextColor"]}};
              --color-login-bg:{{b.LoginBackgroundColor}};
              --color-login-text:{{b["LoginTextColor"]}};
            }
            """;
        Response.Headers.CacheControl = User.Identity?.IsAuthenticated == true ? "private, max-age=3600" : "public, max-age=3600";
        return Content(css, "text/css");
    }

    /// <summary>Readable text colour (dark or white) for a given #rrggbb background (WCAG relative luminance).</summary>
    public static string OnColor(string hex) => Branding.OnColor(hex);
}
