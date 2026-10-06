using System.Text.RegularExpressions;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CyberLms.Web.Services;

/// <summary>Key/value settings stored in PostgreSQL, cached in memory. Changes apply immediately (cache is invalidated on save).</summary>
public class SettingsService(IServiceScopeFactory scopes, IMemoryCache cache, ILogger<SettingsService>? log = null)
{
    private const string CacheKey = "settings.all";

    public IReadOnlyDictionary<string, string?> All()
    {
        return cache.GetOrCreate(CacheKey, e =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return (IReadOnlyDictionary<string, string?>)db.SystemSettings.AsNoTracking().ToDictionary(s => s.Key, s => s.Value);
            }
            catch (Exception ex)
            {
                // Database unavailable: fall back to defaults for a few seconds so error pages can still render (in Arabic).
                log?.LogError(ex, "Settings could not be loaded from the database; using defaults.");
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                return new Dictionary<string, string?>();
            }
        })!;
    }

    public string? Get(string key, string? fallback = null) => All().TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
    public bool GetBool(string key, bool fallback = false) => bool.TryParse(Get(key), out var b) ? b : fallback;
    public int GetInt(string key, int fallback) => int.TryParse(Get(key), out var i) ? i : fallback;

    public async Task SaveAsync(AppDbContext db, IDictionary<string, string?> values)
    {
        var existing = await db.SystemSettings.Where(s => values.Keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key);
        foreach (var (k, v) in values)
        {
            if (existing.TryGetValue(k, out var row)) row.Value = v;
            else db.SystemSettings.Add(new SystemSetting { Key = k, Value = v });
        }
        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
    }

    public void Invalidate() => cache.Remove(CacheKey);
}

/// <summary>Strongly typed branding values. Defaults are only used until an administrator changes them in Settings.</summary>
public class Branding
{
    public string OrgName { get; init; } = "";
    public string SystemName { get; init; } = "";
    public string PrimaryColor { get; init; } = "";
    public string SecondaryColor { get; init; } = "";
    public string AccentColor { get; init; } = "";
    public string HeaderColor { get; init; } = "";
    public string HeaderTextColor { get; init; } = "";
    public string SidebarColor { get; init; } = "";
    public string SidebarTextColor { get; init; } = "";
    public string LoginTitle { get; init; } = "";
    public string LoginSubtitle { get; init; } = "";
    public string LoginBackgroundColor { get; init; } = "";
    public string WelcomeText { get; init; } = "";
    public string FooterText { get; init; } = "";
    public bool HasLogo { get; init; }
    public bool HasFavicon { get; init; }
    public bool HasLoginBackground { get; init; }
    public string Version { get; init; } = "0";

    public static readonly Regex ColorRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public static class Keys
    {
        public const string OrgName = "Branding.OrgName", SystemName = "Branding.SystemName",
            Primary = "Branding.PrimaryColor", Secondary = "Branding.SecondaryColor", Accent = "Branding.AccentColor",
            Header = "Branding.HeaderColor", HeaderText = "Branding.HeaderTextColor",
            Sidebar = "Branding.SidebarColor", SidebarText = "Branding.SidebarTextColor",
            LoginTitle = "Branding.LoginTitle", LoginSubtitle = "Branding.LoginSubtitle", LoginBg = "Branding.LoginBackgroundColor",
            Welcome = "Branding.WelcomeText", Footer = "Branding.FooterText",
            Logo = "Branding.LogoFile", Favicon = "Branding.FaviconFile", LoginBgImage = "Branding.LoginBackgroundImage",
            Version = "Branding.Version";
    }

    public static Branding From(SettingsService s, System.Globalization.CultureInfo? culture = null) => new()
    {
        OrgName = s.Get(Keys.OrgName, Res.Get("Default organization name", culture))!,
        SystemName = s.Get(Keys.SystemName, Res.Get("Default system name", culture))!,
        PrimaryColor = s.Get(Keys.Primary, "#1f4fd8")!,
        SecondaryColor = s.Get(Keys.Secondary, "#5b6b86")!,
        AccentColor = s.Get(Keys.Accent, "#0f9d8a")!,
        HeaderColor = s.Get(Keys.Header, "#ffffff")!,
        HeaderTextColor = s.Get(Keys.HeaderText, "#14213d")!,
        SidebarColor = s.Get(Keys.Sidebar, "#0c1f4a")!,
        SidebarTextColor = s.Get(Keys.SidebarText, "#e9eefc")!,
        LoginTitle = s.Get(Keys.LoginTitle, Res.Get("Sign in", culture))!,
        LoginSubtitle = s.Get(Keys.LoginSubtitle, "")!,
        LoginBackgroundColor = s.Get(Keys.LoginBg, "#eaf0fb")!,
        WelcomeText = s.Get(Keys.Welcome, "")!,
        FooterText = s.Get(Keys.Footer, "")!,
        HasLogo = !string.IsNullOrEmpty(s.Get(Keys.Logo)),
        HasFavicon = !string.IsNullOrEmpty(s.Get(Keys.Favicon)),
        HasLoginBackground = !string.IsNullOrEmpty(s.Get(Keys.LoginBgImage)),
        Version = s.Get(Keys.Version, "0")!,
    };
}

/// <summary>Per-request access to branding for views.</summary>
public class BrandingAccessor(SettingsService settings)
{
    private Branding? _b;
    public Branding Current => _b ??= Branding.From(settings);
}
