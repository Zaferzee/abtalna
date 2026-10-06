using System.Globalization;
using Microsoft.Extensions.Localization;

namespace CyberLms.Web.Services;

/// <summary>
/// View/controller localisation over ASP.NET Core resources (IStringLocalizer&lt;SharedResource&gt;).
/// The English text is the key; the Arabic translation lives in Resources/SharedResource.ar.resx. Unknown keys fall back to the key.
/// </summary>
public class Localizer(IStringLocalizer<SharedResource> loc)
{
    public string this[string key] => string.IsNullOrEmpty(key) ? "" : loc[key].Value;
    public string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, this[key], args);

    /// <summary>List separator (", " in English, Arabic comma in Arabic).</summary>
    public string Sep => this["List separator"];

    public bool IsRtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
    public string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    public string Dir => IsRtl ? "rtl" : "ltr";
    /// <summary>"Back" arrow glyph pointing the reading direction's way back.</summary>
    public string BackArrow => IsRtl ? "→" : "←";
}
