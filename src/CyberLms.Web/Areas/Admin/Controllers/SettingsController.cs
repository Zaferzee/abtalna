using CyberLms.Web.Areas.Admin.Models;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Mvc;

namespace CyberLms.Web.Areas.Admin.Controllers;

public class SettingsController(SettingsService settings, AppDbContext db, StorageService storage, AuditService audit, SmtpConfigProvider smtp, IConfiguration cfg, ILogger<SettingsController> log) : AdminController
{
    private SettingsVm Load()
    {
        var s = smtp.Get();
        return new SettingsVm
        {
            BaseUrl = settings.Get("App.BaseUrl"), DefaultLanguage = settings.Get("General.DefaultLanguage", "en")!,
            Brand = Branding.From(settings),
            SmtpHost = s.Host, SmtpPort = s.Port, SmtpSender = s.Sender, SmtpSenderName = s.SenderName, SmtpUsername = s.Username,
            SmtpSecurity = settings.Get("Smtp.Security", cfg["Smtp:Security"] ?? "Auto")!, SmtpPasswordConfigured = !string.IsNullOrEmpty(s.Password),
        };
    }

    public IActionResult Index() => View(Load());

    [HttpPost]
    public async Task<IActionResult> SaveGeneral(SettingsVm vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.BaseUrl) && !(Uri.TryCreate(vm.BaseUrl, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"))
        { Failure("Base URL must be an http(s) URL."); return RedirectToAction(nameof(Index)); }
        await settings.SaveAsync(db, new Dictionary<string, string?> { ["App.BaseUrl"] = vm.BaseUrl?.Trim(), ["General.DefaultLanguage"] = vm.DefaultLanguage is "ar" ? "ar" : "en" });
        audit.Add("SETTINGS_UPDATED", "Settings", "General"); await db.SaveChangesAsync();
        Success("General settings saved.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Saves the Branding & Appearance studio. Every field is validated against the catalogue in <see cref="Branding"/>:
    /// colors must be #RRGGBB, choices must be one of the allowed values, numbers are clamped, texts are trimmed and limited.
    /// Fields that are not part of the submission keep their saved value. Uploads use the existing image checks.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveBranding(IFormFile? logo, IFormFile? loginLogo, IFormFile? icon, IFormFile? favicon, IFormFile? loginBackground)
    {
        var form = Request.Form;
        string? F(string name) => form.TryGetValue(name, out var v) ? v.ToString() : null;
        var values = new Dictionary<string, string?>();

        var bad = new List<string>();
        foreach (var (name, def) in Branding.Colors)
        {
            var v = F(name)?.Trim(); if (v == null) continue;
            if (v.Length == 0) values[Branding.Key(name)] = null;                       // back to the default (or automatic)
            else if (Branding.ColorRegex.IsMatch(v)) values[Branding.Key(name)] = v.ToLowerInvariant();
            else bad.Add(L[ColorLabel(name)]);
        }
        if (bad.Count > 0) { Failure("Invalid color for: {0} (use #RRGGBB).", string.Join(L.Sep, bad)); return RedirectToAction(nameof(Index)); }
        if (F("OrgName") is { } on && string.IsNullOrWhiteSpace(on) || F("SystemName") is { } sn && string.IsNullOrWhiteSpace(sn))
        { Failure("Organization name and system name are required."); return RedirectToAction(nameof(Index)); }

        foreach (var (name, max, multi) in Branding.Texts)
        {
            var v = F(name); if (v == null) continue;
            v = v.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            v = multi ? System.Text.RegularExpressions.Regex.Replace(v, "\n{3,}", "\n\n") : Branding.Inline(v);
            values[Branding.Key(name)] = v.Length == 0 ? null : v.Length > max ? v[..max] : v;
        }
        foreach (var (name, allowed) in Branding.Choices)
            if (F(name) is { } v) values[Branding.Key(name)] = allowed.Contains(v) ? v : allowed[0];
        foreach (var (name, min, max, def) in Branding.Numbers)
            if (F(name) is { } v) values[Branding.Key(name)] = (int.TryParse(v.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? Math.Clamp(n, min, max) : def).ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var (name, _) in Branding.Flags)
            if (form.TryGetValue(name, out var v)) values[Branding.Key(name)] = v.Contains("true") ? "true" : "false";   // hidden "false" + checkbox "true"
        if (F("Preset") is { } preset) values[Branding.Keys.Preset] = BrandingPresets.All.Any(p => p.Id == preset) ? preset : null;

        // assets: upload replaces, "remove<Field>" clears; old files are deleted after saving
        var oldFiles = new List<string?>();
        var errors = new List<string>();
        var uploads = new Dictionary<string, IFormFile?> { ["logo"] = logo, ["loginLogo"] = loginLogo, ["icon"] = icon, ["favicon"] = favicon, ["loginBackground"] = loginBackground };
        foreach (var (field, key, kind) in Branding.Assets)
        {
            var file = uploads[field];
            var remove = F("remove" + char.ToUpperInvariant(field[0]) + field[1..]) == "true";
            if (file is { Length: > 0 })
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExt = kind == "favicon" ? new[] { ".ico", ".png" } : new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
                if (!allowedExt.Contains(ext)) { errors.Add($"{L[AssetLabel(kind)]}: {L.Format("This file type is not allowed ({0}).", ext)}"); continue; }
                var (err, stored) = await storage.SaveAsync(file, "branding", [AttachmentKind.Image]);
                if (err != null) { errors.Add($"{L[AssetLabel(kind)]}: {L.Format(err.Key, err.Args)}"); continue; }
                oldFiles.Add(settings.Get(key)); values[key] = stored!.RelativePath;
            }
            else if (remove) { oldFiles.Add(settings.Get(key)); values[key] = null; }
        }
        values[Branding.Keys.Version] = DateTime.UtcNow.Ticks.ToString(); // cache-busts logo/theme URLs => visible immediately
        await settings.SaveAsync(db, values);
        oldFiles.ForEach(storage.Delete);
        audit.Add("BRANDING_UPDATED", "Settings", "Branding"); await db.SaveChangesAsync();
        TempData[errors.Count > 0 ? "Error" : "Success"] = errors.Count > 0 ? L.Format("Saved, but: {0}", string.Join(" ", errors)) : L["Branding saved and applied."];
        return RedirectToAction(nameof(Index), null, null, "t-brand");
    }

    public static string ColorLabel(string name) => name switch
    {
        "PrimaryColor" => "Primary color", "SecondaryColor" => "Secondary color", "AccentColor" => "Accent color",
        "BackgroundColor" => "Page background", "SurfaceColor" => "Cards and surfaces",
        "HeaderColor" => "Header background", "HeaderTextColor" => "Header text", "SidebarColor" => "Sidebar background", "SidebarTextColor" => "Sidebar text",
        "ButtonColor" => "Button color", "ButtonTextColor" => "Button text", "HeroTextColor" => "Welcome banner text",
        "LoginBackgroundColor" => "Login page background", "LoginTextColor" => "Login hero text", "LoginOverlayColor" => "Image overlay color",
        _ => name,
    };

    public static string AssetLabel(string kind) => kind switch
    {
        "logo" => "Sidebar logo", "loginlogo" => "Login logo", "icon" => "Compact logo (icon)", "favicon" => "Favicon", _ => "Login background image",
    };

    [HttpPost]
    public async Task<IActionResult> ResetBranding()
    {
        var old = Branding.Assets.Select(a => settings.Get(a.Key)).ToList();
        var keys = Branding.AllKeys.Distinct().ToDictionary(k => k, _ => (string?)null);
        keys[Branding.Keys.Version] = DateTime.UtcNow.Ticks.ToString();
        await settings.SaveAsync(db, keys);
        old.ForEach(storage.Delete);
        audit.Add("BRANDING_RESET", "Settings", "Branding"); await db.SaveChangesAsync();
        Success("Branding reset to defaults.");
        return RedirectToAction(nameof(Index), null, null, "t-brand");
    }

    [HttpPost]
    public async Task<IActionResult> SaveSmtp(SettingsVm vm)
    {
        await settings.SaveAsync(db, new Dictionary<string, string?>
        {
            ["Smtp.Host"] = vm.SmtpHost?.Trim(), ["Smtp.Port"] = vm.SmtpPort.ToString(), ["Smtp.Sender"] = vm.SmtpSender?.Trim(),
            ["Smtp.SenderName"] = vm.SmtpSenderName?.Trim(), ["Smtp.Username"] = vm.SmtpUsername?.Trim(),
            ["Smtp.Security"] = vm.SmtpSecurity is "None" or "StartTls" or "Ssl" ? vm.SmtpSecurity : "Auto",
        });
        audit.Add("SETTINGS_UPDATED", "Settings", "SMTP"); await db.SaveChangesAsync();
        Success("Email settings saved. (The SMTP password is read from server configuration, not from this page.)");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TestEmail(string to)
    {
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(to)) { Failure("Enter a valid e-mail address."); return RedirectToAction(nameof(Index)); }
        var c = smtp.Get();
        if (!c.Configured) { Failure("Configure host and sender first."); return RedirectToAction(nameof(Index)); }
        try
        {
            using var client = new SmtpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await SmtpSender.ConnectAsync(client, c, cts.Token);
            await client.SendAsync(SmtpSender.Build(c, new MailJob(to, Res.Ar("Test email"), $"<div dir=\"rtl\" style=\"text-align:right\"><p>{System.Net.WebUtility.HtmlEncode(Res.Ar("SMTP settings are working."))}</p></div>")), cts.Token);
            await client.DisconnectAsync(true, cts.Token);
            Success("Test email sent to {0}.", to);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "SMTP test e-mail to {Recipient} failed (host {Host}:{Port}, security {Security}).", to, c.Host, c.Port, c.Security);
            Failure("The test e-mail could not be sent. Check the e-mail settings and the server log. Technical reason: {0}", ex.Message);
        }
        return RedirectToAction(nameof(Index));
    }
}
