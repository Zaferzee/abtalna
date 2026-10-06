using System.Globalization;
using System.Resources;

[assembly: NeutralResourcesLanguage("en")]

namespace CyberLms.Web;

/// <summary>Marker type for the shared resource files (Resources/SharedResource.*.resx). Keys are the English source texts.</summary>
public class SharedResource { }

/// <summary>
/// Resource lookup outside of a request (e-mail templates, default branding, audit details).
/// Arabic (ar-SA) is the system default culture; every text lives in Resources/SharedResource.ar.resx.
/// </summary>
public static class Res
{
    public static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-SA");
    private static readonly ResourceManager Manager = new("CyberLms.Web.Resources.SharedResource", typeof(SharedResource).Assembly);

    /// <summary>Translated text for <paramref name="key"/> in the given culture (default: the current UI culture). Falls back to the key itself.</summary>
    public static string Get(string key, CultureInfo? culture = null)
    {
        try { return Manager.GetString(key, culture ?? CultureInfo.CurrentUICulture) ?? key; }
        catch (MissingManifestResourceException) { return key; }
    }

    public static string Format(string key, CultureInfo? culture, params object?[] args) =>
        string.Format(culture ?? CultureInfo.CurrentUICulture, Get(key, culture), args);

    /// <summary>Arabic text with arguments.</summary>
    public static string Ar(string key, params object?[] args) => string.Format(Arabic, Get(key, Arabic), args);
}
