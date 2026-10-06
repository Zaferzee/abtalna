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
        var b = Branding.From(settings);
        var s = smtp.Get();
        return new SettingsVm
        {
            BaseUrl = settings.Get("App.BaseUrl"), DefaultLanguage = settings.Get("General.DefaultLanguage", "en")!,
            OrgName = b.OrgName, SystemName = b.SystemName, PrimaryColor = b.PrimaryColor, SecondaryColor = b.SecondaryColor, AccentColor = b.AccentColor,
            HeaderColor = b.HeaderColor, HeaderTextColor = b.HeaderTextColor, SidebarColor = b.SidebarColor, SidebarTextColor = b.SidebarTextColor,
            LoginTitle = b.LoginTitle, LoginSubtitle = b.LoginSubtitle, LoginBackgroundColor = b.LoginBackgroundColor, WelcomeText = b.WelcomeText, FooterText = b.FooterText,
            HasLogo = b.HasLogo, HasFavicon = b.HasFavicon, HasLoginBackground = b.HasLoginBackground,
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

    [HttpPost]
    public async Task<IActionResult> SaveBranding(SettingsVm vm, IFormFile? logo, IFormFile? favicon, IFormFile? loginBackground, bool removeLogo, bool removeFavicon, bool removeLoginBackground)
    {
        var colors = new (string Label, string Value)[]
        {
            (L["Primary color"], vm.PrimaryColor), (L["Secondary color"], vm.SecondaryColor), (L["Accent color"], vm.AccentColor), (L["Header background"], vm.HeaderColor),
            (L["Header text"], vm.HeaderTextColor), (L["Sidebar background"], vm.SidebarColor), (L["Sidebar text"], vm.SidebarTextColor), (L["Login page background"], vm.LoginBackgroundColor),
        };
        var bad = colors.Where(c => !Branding.ColorRegex.IsMatch(c.Value ?? "")).Select(c => c.Label).ToList();
        if (bad.Count > 0) { Failure("Invalid color for: {0} (use #RRGGBB).", string.Join(L.Sep, bad)); return RedirectToAction(nameof(Index)); }
        if (string.IsNullOrWhiteSpace(vm.OrgName) || string.IsNullOrWhiteSpace(vm.SystemName)) { Failure("Organization name and system name are required."); return RedirectToAction(nameof(Index)); }

        static string Cut(string? s, int n) { s = s?.Trim() ?? ""; return s.Length > n ? s[..n] : s; }
        var values = new Dictionary<string, string?>
        {
            [Branding.Keys.OrgName] = Cut(vm.OrgName, 150), [Branding.Keys.SystemName] = Cut(vm.SystemName, 150),
            [Branding.Keys.Primary] = vm.PrimaryColor, [Branding.Keys.Secondary] = vm.SecondaryColor, [Branding.Keys.Accent] = vm.AccentColor,
            [Branding.Keys.Header] = vm.HeaderColor, [Branding.Keys.HeaderText] = vm.HeaderTextColor,
            [Branding.Keys.Sidebar] = vm.SidebarColor, [Branding.Keys.SidebarText] = vm.SidebarTextColor, [Branding.Keys.LoginBg] = vm.LoginBackgroundColor,
            [Branding.Keys.LoginTitle] = Cut(vm.LoginTitle, 150), [Branding.Keys.LoginSubtitle] = Cut(vm.LoginSubtitle, 500),
            [Branding.Keys.Welcome] = Cut(vm.WelcomeText, 1000), [Branding.Keys.Footer] = Cut(vm.FooterText, 500),
        };
        var oldFiles = new List<string?>();
        var errors = new List<string>();
        async Task Handle(IFormFile? file, bool remove, string key, string label)
        {
            if (file is { Length: > 0 })
            {
                var (err, stored) = await storage.SaveAsync(file, "branding", [AttachmentKind.Image]);
                if (err != null) { errors.Add($"{label}: {L.Format(err.Key, err.Args)}"); return; }
                oldFiles.Add(settings.Get(key)); values[key] = stored!.RelativePath;
            }
            else if (remove) { oldFiles.Add(settings.Get(key)); values[key] = null; }
        }
        await Handle(logo, removeLogo, Branding.Keys.Logo, L["Logo"]);
        await Handle(favicon, removeFavicon, Branding.Keys.Favicon, L["Favicon"]);
        await Handle(loginBackground, removeLoginBackground, Branding.Keys.LoginBgImage, L["Login background image"]);
        values[Branding.Keys.Version] = DateTime.UtcNow.Ticks.ToString(); // cache-busts logo/theme URLs => visible immediately
        await settings.SaveAsync(db, values);
        oldFiles.ForEach(storage.Delete);
        audit.Add("BRANDING_UPDATED", "Settings", "Branding"); await db.SaveChangesAsync();
        TempData[errors.Count > 0 ? "Error" : "Success"] = errors.Count > 0 ? L.Format("Saved, but: {0}", string.Join(" ", errors)) : L["Branding saved and applied."];
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetBranding()
    {
        var old = new[] { Branding.Keys.Logo, Branding.Keys.Favicon, Branding.Keys.LoginBgImage }.Select(k => settings.Get(k)).ToList();
        var keys = typeof(Branding.Keys).GetFields().Select(f => (string)f.GetRawConstantValue()!).ToDictionary(k => k, _ => (string?)null);
        keys[Branding.Keys.Version] = DateTime.UtcNow.Ticks.ToString();
        await settings.SaveAsync(db, keys);
        old.ForEach(storage.Delete);
        audit.Add("BRANDING_RESET", "Settings", "Branding"); await db.SaveChangesAsync();
        Success("Branding reset to defaults.");
        return RedirectToAction(nameof(Index));
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
            Failure("Test failed: {0}", ex.Message);
        }
        return RedirectToAction(nameof(Index));
    }
}
