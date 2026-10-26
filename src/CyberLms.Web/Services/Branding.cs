using System.Globalization;
using System.Text.RegularExpressions;

namespace CyberLms.Web.Services;

/// <summary>
/// Branding & appearance ("استوديو الهوية والمظهر"). Every value is a key/value system setting named "Branding.&lt;Field&gt;"
/// (no schema). Empty values fall back to defaults, so a fresh installation and "Restore defaults" look the same.
/// All values reach pages only through Razor encoding (text) or after validation (colors, enumerations, numbers).
/// </summary>
public class Branding
{
    public static readonly Regex ColorRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    // ---------- field catalogue (form field name == setting suffix) ----------
    /// <summary>Theme colors and their defaults (the "Classic blue" preset). Empty default = derived automatically.</summary>
    public static readonly (string Name, string Default)[] Colors =
    [
        ("PrimaryColor", "#1f4fd8"), ("SecondaryColor", "#5b6b86"), ("AccentColor", "#0f9d8a"),
        ("BackgroundColor", "#f4f6fb"), ("SurfaceColor", "#ffffff"),
        ("HeaderColor", "#ffffff"), ("HeaderTextColor", "#14213d"),
        ("SidebarColor", "#0c1f4a"), ("SidebarTextColor", "#e9eefc"),
        ("ButtonColor", ""), ("ButtonTextColor", ""), ("HeroTextColor", "#ffffff"),
        ("LoginBackgroundColor", "#eaf0fb"), ("LoginTextColor", "#ffffff"), ("LoginOverlayColor", "#0b1a3f"),
    ];

    /// <summary>Free text (max length). Line breaks are kept where marked multi-line.</summary>
    public static readonly (string Name, int Max, bool MultiLine)[] Texts =
    [
        ("OrgName", 200, true), ("SystemName", 200, true),
        ("LoginBadge", 80, false), ("LoginHeroTitle", 200, true), ("LoginHeroDescription", 600, true),
        ("LoginFeature1", 120, false), ("LoginFeature2", 120, false), ("LoginFeature3", 120, false),
        ("LoginTitle", 150, false), ("LoginCardSubtitle", 300, false), ("LoginSubtitle", 500, true),
        ("SupportText", 300, true), ("FooterText", 500, false), ("WelcomeText", 1000, true),
    ];

    /// <summary>Choices: allowed values, first = default.</summary>
    public static readonly (string Name, string[] Allowed)[] Choices =
    [
        ("LoginImageFit", ["cover", "contain", "fill", "auto"]),
        ("LoginImagePosition", ["center", "top", "bottom", "left", "right", "top-left", "top-right", "bottom-left", "bottom-right", "custom"]),
        ("LoginLayout", ["split", "split-reverse", "full"]),
        ("LoginTextAlign", ["start", "center"]),
        ("LoginLogoPlacement", ["hero", "card", "both"]),
        // how the login image is shown: full background of the visual panel, a framed picture (large / medium / small) or not at all
        ("LoginImageMode", ["background", "large", "medium", "small", "hidden"]),
        ("LoginImageAlign", ["start", "center", "end"]),
        ("LoginImageSlot", ["above", "below"]),
        ("LoginDecorStyle", ["shield", "orbs", "checklist", "grid", "document", "journey", "none"]),   // legacy (v1.1); see LoginCompositionId
        // visual composition (preset or "custom" = the three layers below)
        ("LoginComposition", LoginCompositions.Ids),
        ("Layer1Motif", LoginCompositions.Motifs), ("Layer1Position", LoginCompositions.Positions),
        ("Layer2Motif", LoginCompositions.Motifs), ("Layer2Position", LoginCompositions.Positions),
        ("Layer3Motif", LoginCompositions.Motifs), ("Layer3Position", LoginCompositions.Positions),
    ];

    /// <summary>Integers: range and default.</summary>
    public static readonly (string Name, int Min, int Max, int Default)[] Numbers =
    [
        ("LoginImageZoom", 100, 200, 100), ("LoginFocusX", 0, 100, 50), ("LoginFocusY", 0, 100, 50), ("LoginOverlayOpacity", 0, 95, 70),
        ("LoginImageSize", 10, 100, 0),   // width of a framed image in % of the text column; 0 = the mode's default
        ("LoginImageInset", 0, 64, 0),    // spacing (px) from the panel edges (background) or around the framed image
        ("Layer1Opacity", LoginCompositions.MinOpacity, LoginCompositions.MaxOpacity, 14), ("Layer1Scale", LoginCompositions.MinScale, LoginCompositions.MaxScale, 100), ("Layer1Rotate", -LoginCompositions.MaxRotate, LoginCompositions.MaxRotate, 0),
        ("Layer2Opacity", LoginCompositions.MinOpacity, LoginCompositions.MaxOpacity, 14), ("Layer2Scale", LoginCompositions.MinScale, LoginCompositions.MaxScale, 100), ("Layer2Rotate", -LoginCompositions.MaxRotate, LoginCompositions.MaxRotate, 0),
        ("Layer3Opacity", LoginCompositions.MinOpacity, LoginCompositions.MaxOpacity, 14), ("Layer3Scale", LoginCompositions.MinScale, LoginCompositions.MaxScale, 100), ("Layer3Rotate", -LoginCompositions.MaxRotate, LoginCompositions.MaxRotate, 0),
    ];

    /// <summary>On/off options and defaults.</summary>
    public static readonly (string Name, bool Default)[] Flags =
    [
        ("ShowOrgInSidebar", true), ("ShowNamesOnLogin", true), ("LogoPlate", true), ("LoginLogoPlate", true), ("LoginDecor", true),
        ("ShowLoginBadge", true), ("ShowLoginFeatures", true),
        ("Layer1Visible", true), ("Layer2Visible", true), ("Layer3Visible", true),
    ];

    /// <summary>Uploaded assets: form field, setting key, public "kind" for /Files/Brand.</summary>
    public static readonly (string Field, string Key, string Kind)[] Assets =
    [
        ("logo", "Branding.LogoFile", "logo"),               // sidebar / navigation logo
        ("loginLogo", "Branding.LoginLogoFile", "loginlogo"), // login page logo only (independent of the sidebar logo)
        ("icon", "Branding.IconFile", "icon"),               // compact square mark (shown in the sidebar only when there is no sidebar logo)
        ("favicon", "Branding.FaviconFile", "favicon"),
        ("loginBackground", "Branding.LoginBackgroundImage", "loginbg"),
    ];

    public static string Key(string field) => "Branding." + field;

    /// <summary>Every branding setting key (used by "Restore defaults").</summary>
    public static IEnumerable<string> AllKeys =>
        Colors.Select(c => Key(c.Name)).Concat(Texts.Select(t => Key(t.Name))).Concat(Choices.Select(c => Key(c.Name)))
            .Concat(Numbers.Select(n => Key(n.Name))).Concat(Flags.Select(f => Key(f.Name))).Concat(Assets.Select(a => a.Key))
            .Append(Keys.Preset).Append(Keys.Version);

    public static class Keys
    {
        public const string OrgName = "Branding.OrgName", SystemName = "Branding.SystemName",
            Primary = "Branding.PrimaryColor", Secondary = "Branding.SecondaryColor", Accent = "Branding.AccentColor",
            Header = "Branding.HeaderColor", HeaderText = "Branding.HeaderTextColor",
            Sidebar = "Branding.SidebarColor", SidebarText = "Branding.SidebarTextColor",
            LoginTitle = "Branding.LoginTitle", LoginSubtitle = "Branding.LoginSubtitle", LoginBg = "Branding.LoginBackgroundColor",
            Welcome = "Branding.WelcomeText", Footer = "Branding.FooterText",
            Logo = "Branding.LogoFile", LoginLogo = "Branding.LoginLogoFile", Icon = "Branding.IconFile",
            Favicon = "Branding.FaviconFile", LoginBgImage = "Branding.LoginBackgroundImage",
            Preset = "Branding.Preset", Version = "Branding.Version";
    }

    // ---------- resolved values ----------
    private readonly Dictionary<string, string> _v = new();
    private readonly Dictionary<string, string> _raw = new();
    private readonly HashSet<string> _assets = new();
    private readonly Dictionary<string, int> _assetEntity = new();
    public string Version { get; private init; } = "0";
    public string Preset { get; private init; } = "";

    /// <summary>Resolved value (saved or default) of a color/text/choice/number field.</summary>
    public string this[string field] => _v.TryGetValue(field, out var x) ? x : "";
    /// <summary>The saved value as entered (empty when the default is used) - for the studio form.</summary>
    public string Raw(string field) => _raw.TryGetValue(field, out var x) ? x : "";
    public bool Flag(string field) => _v.TryGetValue(field, out var x) && x == "true";
    public int Number(string field) => int.TryParse(this[field], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    public bool Has(string kind) => _assets.Contains(kind);
    public string AssetUrl(string kind) => _assetEntity.TryGetValue(kind, out var e) ? $"/Files/Brand?kind={kind}&e={e}&v={Version}" : $"/Files/Brand?kind={kind}&v={Version}";
    /// <summary>Entity whose branding this is (null = central branding).</summary>
    public int? EntityId { get; internal set; }

    // Typed shortcuts used across the application.
    public string OrgName => this["OrgName"];
    public string SystemName => this["SystemName"];
    /// <summary>Single-line forms for page titles, e-mails and other places that cannot wrap.</summary>
    public string OrgNameInline => Inline(OrgName);
    public string SystemNameInline => Inline(SystemName);
    public string PrimaryColor => this["PrimaryColor"];
    public string SecondaryColor => this["SecondaryColor"];
    public string AccentColor => this["AccentColor"];
    public string HeaderColor => this["HeaderColor"];
    public string HeaderTextColor => this["HeaderTextColor"];
    public string SidebarColor => this["SidebarColor"];
    public string SidebarTextColor => this["SidebarTextColor"];
    public string LoginBackgroundColor => this["LoginBackgroundColor"];
    public string LoginTitle => this["LoginTitle"];
    public string LoginSubtitle => this["LoginSubtitle"];
    public string WelcomeText => this["WelcomeText"];
    public string FooterText => this["FooterText"];
    public bool HasLogo => Has("logo");
    // Each asset is independent: the login page never borrows the sidebar logo (and vice versa).
    public bool HasLoginLogo => Has("loginlogo");
    public string LoginLogoUrl => AssetUrl("loginlogo");
    public bool HasIcon => Has("icon");
    public bool HasFavicon => Has("favicon");
    public bool HasLoginBackground => Has("loginbg");
    public string Monogram => SystemNameInline.Trim().Length > 0 ? SystemNameInline.Trim()[0].ToString() : "•";

    public static string Inline(string s) => Regex.Replace(s ?? "", @"\s*\r?\n\s*", " ").Trim();

    /// <summary>Default text of a field in the given culture (null culture = current UI culture).</summary>
    public static string DefaultText(string field, CultureInfo? culture = null, string? systemName = null) => field switch
    {
        "OrgName" => Res.Get("Default organization name", culture),
        "SystemName" => Res.Get("Default system name", culture),
        "LoginBadge" => Res.Get("Cybersecurity awareness", culture),
        "LoginHeroTitle" => systemName ?? Res.Get("Default system name", culture),
        "LoginHeroDescription" => Res.Get("Learn the policies, controls and safe practices that protect our organization - in short, clear lessons.", culture),
        "LoginFeature1" => Res.Get("Policies and controls in one place", culture),
        "LoginFeature2" => Res.Get("Short interactive assessments", culture),
        "LoginFeature3" => Res.Get("Track your progress and acknowledgments", culture),
        "LoginTitle" => Res.Get("Sign in", culture),
        "LoginCardSubtitle" => Res.Get("Welcome back. Sign in to continue your learning.", culture),
        _ => "",
    };

    public static Branding From(SettingsService s, CultureInfo? culture = null) => From(k => s.Get(k), culture);

    /// <summary>Resolves branding from any value source (root settings, or root settings overlaid with an entity's overrides).
    /// <paramref name="assetSource"/> names the entity an uploaded asset comes from (null = the central branding).</summary>
    public static Branding From(Func<string, string?> get, CultureInfo? culture = null, Func<string, int?>? assetSource = null)
    {
        Func<string, string?> s0 = get;
        get = k => s0(k) is { } v && !string.IsNullOrWhiteSpace(v) ? v : null;
        var b = new Branding { Version = get(Keys.Version) ?? "0", Preset = get(Keys.Preset) ?? "" };
        foreach (var (name, def) in Colors)
        {
            var v = get(Key(name)); var ok = v != null && ColorRegex.IsMatch(v);
            b._raw[name] = ok ? v! : ""; b._v[name] = ok ? v! : def;
        }
        // texts: the system name is needed first (hero title default)
        foreach (var (name, _, _) in Texts.OrderBy(t => t.Name == "SystemName" ? 0 : 1))
        {
            var v = get(Key(name)); b._raw[name] = v ?? "";
            b._v[name] = !string.IsNullOrWhiteSpace(v) ? v! : DefaultText(name, culture, name == "LoginHeroTitle" ? b._v.GetValueOrDefault("SystemName") : null);
        }
        foreach (var (name, allowed) in Choices) { var v = get(Key(name)); b._raw[name] = v ?? ""; b._v[name] = v != null && allowed.Contains(v) ? v : allowed[0]; }
        foreach (var (name, min, max, def) in Numbers) { var v = get(Key(name)); b._raw[name] = v ?? ""; b._v[name] = (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Clamp(n, min, max) : def).ToString(CultureInfo.InvariantCulture); }
        foreach (var (name, def) in Flags) { var v = get(Key(name)); b._v[name] = (bool.TryParse(v, out var f) ? f : def) ? "true" : "false"; b._raw[name] = b._v[name]; }
        foreach (var (_, key, kind) in Assets)
            if (!string.IsNullOrEmpty(get(key))) { b._assets.Add(kind); if (assetSource?.Invoke(key) is int src) b._assetEntity[kind] = src; }
        return b;
    }

    // ---------- login image helpers ----------
    /// <summary>CSS background-position for the login image (validated values only).</summary>
    public string LoginImagePositionCss => this["LoginImagePosition"] switch
    {
        "top" => "50% 0%", "bottom" => "50% 100%", "left" => "0% 50%", "right" => "100% 50%",
        "top-left" => "0% 0%", "top-right" => "100% 0%", "bottom-left" => "0% 100%", "bottom-right" => "100% 100%",
        "custom" => $"{Number("LoginFocusX")}% {Number("LoginFocusY")}%",
        _ => "50% 50%",
    };
    /// <summary>Effective image mode: "none" when no image is uploaded.</summary>
    public string LoginImageMode => HasLoginBackground ? this["LoginImageMode"] : "none";
    public bool IsLoginImageFramed => LoginImageMode is "large" or "medium" or "small";
    /// <summary>Default width (% of the text column) of a framed image per mode.</summary>
    public static int DefaultImageWidth(string mode) => mode switch { "large" => 100, "small" => 22, _ => 60 };
    public int LoginImageWidth => Number("LoginImageSize") is > 0 and var n ? n : DefaultImageWidth(this["LoginImageMode"]);
    /// <summary>Decoration actually shown ("none" when decorations are switched off). Legacy (v1.1).</summary>
    public string LoginDecorStyle => Flag("LoginDecor") ? this["LoginDecorStyle"] : "none";

    /// <summary>Composition in use: the saved one; before compositions existed, the closest one to the saved decoration
    /// pattern (so an upgraded installation keeps its intent); "executive" on a fresh installation.</summary>
    public string LoginCompositionId => Raw("LoginComposition") is { Length: > 0 } ? this["LoginComposition"]
        : !Flag("LoginDecor") ? "clean" : Raw("LoginDecorStyle") is { Length: > 0 } ? LoginCompositions.FromLegacy(this["LoginDecorStyle"]) : "executive";

    /// <summary>The three layers actually rendered (preset definition, or the saved custom layers), after the safety rules.</summary>
    public IReadOnlyList<LoginCompositions.Layer> LoginLayers
    {
        get
        {
            var preset = LoginCompositions.All.FirstOrDefault(c => c.Id == LoginCompositionId);
            var raw = preset != null ? preset.Layers
                : Enumerable.Range(1, 3).Select(n => new LoginCompositions.Layer(this[$"Layer{n}Motif"], this[$"Layer{n}Position"], Number($"Layer{n}Opacity"), Number($"Layer{n}Scale"), Number($"Layer{n}Rotate"), Flag($"Layer{n}Visible"))).ToArray();
            return raw.Select(LoginCompositions.Safe).ToList();
        }
    }
    public string LoginImageSizeCss => this["LoginImageFit"] switch { "contain" => "contain", "fill" => "100% 100%", "auto" => "auto", _ => "cover" };

    /// <summary>Inline CSS variables for the login hero (media layer, overlay, text color). Only validated values.</summary>
    public string LoginHeroStyle
    {
        get
        {
            var inv = CultureInfo.InvariantCulture;
            var css = $"--hero-text:{this["LoginTextColor"]};--ov:{this["LoginOverlayColor"]};--ov-op:{(Number("LoginOverlayOpacity") / 100.0).ToString("0.##", inv)};" +
                      $"--img-size:{LoginImageSizeCss};--img-pos:{LoginImagePositionCss};--img-zoom:{(Number("LoginImageZoom") / 100.0).ToString("0.##", inv)};" +
                      $"--img-w:{LoginImageWidth}%;--img-inset:{Number("LoginImageInset")}px;";
            if (HasLoginBackground) css += $"--img:url('{AssetUrl("loginbg")}');";
            return css;
        }
    }

    /// <summary>Classes on the login root for layout, text alignment, image mode and decoration.</summary>
    public string LoginClasses => $"layout-{this["LoginLayout"]} align-{this["LoginTextAlign"]} logo-{this["LoginLogoPlacement"]}" + (LoginLayers.All(l => !l.Visible) ? " no-decor" : "") + (LoginImageMode == "background" ? " has-image" : "") +
        (Flag("ShowLoginBadge") ? "" : " no-badge") + (Flag("ShowLoginFeatures") ? "" : " no-features") + (Flag("ShowNamesOnLogin") ? "" : " no-names") +
        $" comp-{LoginCompositionId} img-{LoginImageMode} img-align-{this["LoginImageAlign"]} img-slot-{this["LoginImageSlot"]}";

    // ---------- contrast ----------
    public static double Luminance(string hex)
    {
        static double Lin(int v) { var c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        return 0.2126 * Lin(Convert.ToInt32(hex.Substring(1, 2), 16)) + 0.7152 * Lin(Convert.ToInt32(hex.Substring(3, 2), 16)) + 0.0722 * Lin(Convert.ToInt32(hex.Substring(5, 2), 16));
    }
    public static double Contrast(string a, string b) { var x = Luminance(a); var y = Luminance(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
    /// <summary>Readable text colour (dark or white) for a background.</summary>
    public static string OnColor(string hex) => Luminance(hex) > 0.42 ? "#14213d" : "#ffffff";
    public string ButtonColor => ColorRegex.IsMatch(this["ButtonColor"]) ? this["ButtonColor"] : PrimaryColor;
    public string ButtonTextColor => ColorRegex.IsMatch(this["ButtonTextColor"]) ? this["ButtonTextColor"] : OnColor(ButtonColor);
}

/// <summary>Per-request access to branding for views.</summary>
public class BrandingAccessor(SettingsService settings, EntityBrandingService entityBranding, IHttpContextAccessor http)
{
    private Branding? _b;
    /// <summary>Branding of the signed-in user's entity (inherited from the root and the entity's ancestors unless overridden);
    /// the central branding for anonymous pages (the login page: no entity is known before sign-in).</summary>
    public Branding Current => _b ??= int.TryParse(http.HttpContext?.User.FindFirst("entity_id")?.Value, out var e) && http.HttpContext!.User.Identity?.IsAuthenticated == true
        ? entityBranding.For(e) : Branding.From(settings);
}
