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
