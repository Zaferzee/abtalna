using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CyberLms.Web.Services;

/// <summary>
/// Entity branding with inheritance: root (central) branding = SystemSettings "Branding.*"; each entity may override a subset of keys
/// (EntitySettings). The effective branding of an entity is the root settings overlaid by the overrides of its ancestors and then its
/// own, nearest first. Nothing is copied, so a central change reaches every entity that has not overridden that key.
/// The login page is central only (no entity is known before sign-in), so login keys are not overridable.
/// </summary>
public class EntityBrandingService(AppDbContext db, SettingsService settings, EntityTree tree, IMemoryCache cache)
{
    private const string CacheKey = "entity.branding.overrides";

    /// <summary>Fields an entity may override: identity, sidebar logo / icon, application colors.</summary>
    public static readonly string[] OverridableFields =
    [
        "OrgName", "SystemName", "WelcomeText", "FooterText", "ShowOrgInSidebar", "LogoPlate",
        "PrimaryColor", "SecondaryColor", "AccentColor", "BackgroundColor", "SurfaceColor", "HeaderColor", "HeaderTextColor",
        "SidebarColor", "SidebarTextColor", "ButtonColor", "ButtonTextColor", "HeroTextColor",
    ];
    public static readonly string[] OverridableAssets = ["logo", "icon"];

    public static bool IsOverridableKey(string key) =>
        key == Branding.Keys.Preset || key == Branding.Keys.Version
        || OverridableFields.Any(f => Branding.Key(f) == key)
        || Branding.Assets.Any(a => OverridableAssets.Contains(a.Field) && a.Key == key);

    private Dictionary<int, Dictionary<string, string?>> All() =>
        cache.GetOrCreate(CacheKey, e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return db.EntitySettings.AsNoTracking().Where(s => s.Key.StartsWith("Branding.")).ToList()
                .GroupBy(s => s.EntityId).ToDictionary(g => g.Key, g => g.ToDictionary(s => s.Key, s => s.Value));
        })!;

    public void Invalidate() => cache.Remove(CacheKey);

    /// <summary>The entity's own overrides.</summary>
    public IReadOnlyDictionary<string, string?> Overrides(int entityId) => All().TryGetValue(entityId, out var d) ? d : new Dictionary<string, string?>();
    public bool HasOverrides(int entityId) => Overrides(entityId).Keys.Any(k => k != Branding.Keys.Version);

    private List<int> Chain(int entityId)
    {
        var node = tree.AllAsync(db).GetAwaiter().GetResult().FirstOrDefault(x => x.Id == entityId);
        return node == null ? [] : node.AncestorIds.Skip(1).ToList(); // the root's branding is the central one
    }

    /// <summary>Effective branding of an entity. <paramref name="upToParent"/>: what the entity would inherit (its own overrides excluded).</summary>
    public Branding For(int entityId, bool upToParent = false)
    {
        var chain = Chain(entityId);
        if (upToParent && chain.Count > 0) chain.RemoveAt(chain.Count - 1);
        var all = All();
        var layers = chain.Select(id => (Id: id, Map: all.TryGetValue(id, out var d) ? d : null)).Where(x => x.Map != null).Reverse().ToList();
        string? Get(string key)
        {
            if (key == Branding.Keys.Version)
                return settings.Get(key, "0") + (layers.Count == 0 ? "" : "." + string.Join(".", layers.Select(l => l.Map!.GetValueOrDefault(key) ?? "0")));
            if (IsOverridableKey(key)) foreach (var (_, map) in layers) if (map!.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            return settings.Get(key);
        }
        int? Source(string key) { foreach (var (id, map) in layers) if (map!.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return id; return null; }
        var b = Branding.From(Get, null, Source);
        b.EntityId = entityId;
        return b;
    }

    /// <summary>Stores the overrides of one entity (null/empty value = inherit). Non-overridable keys are ignored.</summary>
    public async Task SaveAsync(int entityId, IDictionary<string, string?> values)
    {
        var keys = values.Keys.Where(IsOverridableKey).ToList();
        var existing = await db.EntitySettings.Where(s => s.EntityId == entityId && keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key);
        foreach (var k in keys)
        {
            var v = values[k];
            if (string.IsNullOrWhiteSpace(v)) { if (existing.TryGetValue(k, out var row)) db.EntitySettings.Remove(row); }
            else if (existing.TryGetValue(k, out var row)) row.Value = v;
            else db.EntitySettings.Add(new EntitySetting { EntityId = entityId, Key = k, Value = v });
        }
        await db.SaveChangesAsync();
        Invalidate();
    }

    /// <summary>Removes every override of the entity (back to full inheritance). Returns the stored asset paths to delete.</summary>
    public async Task<List<string>> ResetAsync(int entityId)
    {
        var rows = await db.EntitySettings.Where(s => s.EntityId == entityId && s.Key.StartsWith("Branding.")).ToListAsync();
        var files = rows.Where(r => Branding.Assets.Any(a => a.Key == r.Key) && !string.IsNullOrEmpty(r.Value)).Select(r => r.Value!).ToList();
        db.EntitySettings.RemoveRange(rows);
        await db.SaveChangesAsync();
        Invalidate();
        return files;
    }

    /// <summary>Stored relative path of an asset as effective for an entity (root setting when not overridden).</summary>
    public string? AssetPath(int? entityId, string key)
    {
        if (entityId == null) return settings.Get(key);
        foreach (var id in Enumerable.Reverse(Chain(entityId.Value)))
            if (Overrides(id).TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
        return settings.Get(key);
    }
}

/// <summary>Encrypts directory bind secrets at rest (ASP.NET Data Protection; keys are DPAPI-protected on Windows). Never logged.</summary>
public class DirectorySecrets(IDataProtectionProvider dp)
{
    private readonly IDataProtector _p = dp.CreateProtector("CyberLms.DirectorySource.BindSecret.v1");
    public string Protect(string secret) => _p.Protect(secret);
    public bool CanUnprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue)) return false;
        try { _p.Unprotect(protectedValue); return true; } catch { return false; }
    }
    /// <summary>For the directory connector only (Windows Server acceptance phase); never rendered.</summary>
    public string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue)) return null;
        try { return _p.Unprotect(protectedValue); } catch { return null; }
    }
}
