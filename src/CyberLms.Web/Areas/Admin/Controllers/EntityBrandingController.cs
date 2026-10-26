using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CyberLms.Web.Areas.Admin.Controllers;

/// <summary>
/// Entity branding (overrides on top of the inherited branding). Requires ManageBranding in that entity: the central administrator
/// always; an entity administrator only when the central administrator allows branding for the entity and granted the capability.
/// An override only affects the entity and its descendants (resolution walks the entity's own ancestor chain), never other entities.
/// The root entity uses the central branding studio (Settings).
/// </summary>
[Capability(Capability.ManageBranding)]
public class EntityBrandingController(AppDbContext db, EntityAccess access, EntityTree tree, EntityBrandingService branding, StorageService storage, AuditService audit) : AdminController
{
    public static readonly string[] ColorFields = ["PrimaryColor", "SecondaryColor", "AccentColor", "BackgroundColor", "SurfaceColor", "HeaderColor", "HeaderTextColor", "SidebarColor", "SidebarTextColor", "ButtonColor", "ButtonTextColor", "HeroTextColor"];
    public static readonly string[] TextFields = ["OrgName", "SystemName", "WelcomeText", "FooterText"];
    public static readonly string[] FlagFields = ["ShowOrgInSidebar", "LogoPlate"];

    private async Task<EntityNode?> Target(int id)
    {
        var node = await tree.FindAsync(db, id);
        if (node == null || node.IsRoot || !await access.CanInAsync(Capability.ManageBranding, id)) return null;
        return node;
    }

    public async Task<IActionResult> Index(int id)
    {
        var node = await Target(id);
        if (node == null) return NotFound();
        ViewBag.Node = node;
        ViewBag.Overrides = branding.Overrides(id);
        ViewBag.Inherited = branding.For(id, upToParent: true);
        ViewBag.Effective = branding.For(id);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Save(int id, IFormFile? logo, IFormFile? icon)
    {
        var node = await Target(id);
        if (node == null) return NotFound();
        var form = Request.Form;
        string? F(string n) => form.TryGetValue(n, out var v) ? v.ToString() : null;
        var values = new Dictionary<string, string?>();
        foreach (var c in ColorFields)
        {
            var v = F(c)?.Trim();
            if (v == null) continue;
            if (v.Length > 0 && !Branding.ColorRegex.IsMatch(v)) { Failure("Invalid color for: {0} (use #RRGGBB).", L[SettingsController.ColorLabel(c)]); return RedirectToAction(nameof(Index), new { id }); }
            values[Branding.Key(c)] = v.Length == 0 ? null : v.ToLowerInvariant();
        }
        foreach (var t in TextFields)
        {
            var v = F(t); if (v == null) continue;
            var (_, max, multi) = Branding.Texts.First(x => x.Name == t);
            v = v.Replace("\r\n", "\n").Trim();
            if (!multi) v = Branding.Inline(v);
            values[Branding.Key(t)] = v.Length == 0 ? null : v.Length > max ? v[..max] : v;
        }
        foreach (var f in FlagFields) { var v = F(f); if (v != null) values[Branding.Key(f)] = v is "true" or "false" ? v : null; }
        if (F("Preset") is { } p) values[Branding.Keys.Preset] = BrandingPresets.All.Any(x => x.Id == p) ? p : null;

        var errors = new List<string>(); var oldFiles = new List<string?>();
        var current = branding.Overrides(id);
        foreach (var (field, file) in new[] { ("logo", logo), ("icon", icon) })
        {
            var key = Branding.Assets.First(a => a.Field == field).Key;
            if (file is { Length: > 0 })
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(ext)) { errors.Add(L.Format("This file type is not allowed ({0}).", ext)); continue; }
                var (err, stored) = await storage.SaveAsync(file, $"branding/entity-{id}", [AttachmentKind.Image]);
                if (err != null) { errors.Add(L.Format(err.Key, err.Args)); continue; }
                oldFiles.Add(current.GetValueOrDefault(key)); values[key] = stored!.RelativePath;
            }
            else if (F("remove" + char.ToUpperInvariant(field[0]) + field[1..]) == "true") { oldFiles.Add(current.GetValueOrDefault(key)); values[key] = null; }
        }
        values[Branding.Keys.Version] = DateTime.UtcNow.Ticks.ToString();
        var changed = values.Keys.Where(k => k != Branding.Keys.Version && current.GetValueOrDefault(k) != values[k]).Select(k => k.Replace("Branding.", "")).ToList();
        await branding.SaveAsync(id, values);
        oldFiles.Where(f => f != null).ToList().ForEach(f => storage.Delete(f!));
        audit.Add("ENTITY_BRANDING_UPDATED", "Entity", id, Res.Ar("{0}: {1}", node.Code, changed.Count == 0 ? "-" : string.Join(", ", changed)), id);
        await db.SaveChangesAsync();
        TempData[errors.Count > 0 ? "Error" : "Success"] = errors.Count > 0 ? L.Format("Saved, but: {0}", string.Join(" ", errors)) : L["Entity branding saved."];
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Reset(int id)
    {
        var node = await Target(id);
        if (node == null) return NotFound();
        var files = await branding.ResetAsync(id);
        files.ForEach(storage.Delete);
        audit.Add("ENTITY_BRANDING_RESET", "Entity", id, node.Code, id);
        await db.SaveChangesAsync();
        Success("The entity now inherits its branding again.");
        return RedirectToAction(nameof(Index), new { id });
    }
}
