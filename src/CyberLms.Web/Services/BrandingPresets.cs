namespace CyberLms.Web.Services;

/// <summary>
/// Formal theme presets for the Branding & Appearance studio. Each sets every theme color; the administrator can fine-tune
/// afterwards. Palettes are deliberately muted (no saturated "playful" hues); text/background pairs meet WCAG AA (4.5:1),
/// which <c>BrandingTests</c> checks.
/// </summary>
public static class BrandingPresets
{
    public record Preset(string Id, string Name, string Description, IReadOnlyDictionary<string, string> Colors);

    private static Preset P(string id, string name, string desc, string primary, string secondary, string accent, string bg, string surface,
        string header, string headerText, string sidebar, string sidebarText, string loginBg, string overlay) =>
        new(id, name, desc, new Dictionary<string, string>
        {
            ["PrimaryColor"] = primary, ["SecondaryColor"] = secondary, ["AccentColor"] = accent,
            ["BackgroundColor"] = bg, ["SurfaceColor"] = surface, ["HeaderColor"] = header, ["HeaderTextColor"] = headerText,
            ["SidebarColor"] = sidebar, ["SidebarTextColor"] = sidebarText, ["ButtonColor"] = "", ["ButtonTextColor"] = "",
            ["HeroTextColor"] = "#ffffff", ["LoginBackgroundColor"] = loginBg, ["LoginTextColor"] = "#ffffff", ["LoginOverlayColor"] = overlay,
        });

    public static readonly Preset[] All =
    [
        P("classic", "Classic blue", "Default: clear and familiar", "#1f4fd8", "#5b6b86", "#0f9d8a", "#f4f6fb", "#ffffff", "#ffffff", "#14213d", "#0c1f4a", "#e9eefc", "#eaf0fb", "#0b1a3f"),
        P("navy", "Executive Navy", "Official and authoritative", "#1b3a6b", "#5a6a82", "#a8864f", "#f3f5f9", "#ffffff", "#ffffff", "#13233f", "#0e1d38", "#e7ecf5", "#edf1f7", "#0b1830"),
        P("indigo", "Royal Indigo", "Prestigious, with a gold accent", "#3f3d9e", "#625f86", "#b08d47", "#f5f5fb", "#ffffff", "#ffffff", "#1c1b4a", "#1c1b4d", "#ebeafa", "#efeefa", "#17164a"),
        P("graphite", "Graphite Gold", "Luxurious and restrained", "#2f3640", "#6b7280", "#a9853a", "#f5f4f1", "#ffffff", "#ffffff", "#1f2329", "#1b1f24", "#ece8de", "#f2f1ed", "#15181c"),
        P("emerald", "Deep Emerald", "Calm and trustworthy", "#0f5c4a", "#4e6a62", "#a8813d", "#f3f7f5", "#ffffff", "#ffffff", "#10302a", "#0b2f27", "#e4f1ec", "#ebf3f0", "#082820"),
        P("plum", "Plum Executive", "Elegant and distinctive", "#5c2a5e", "#6e5e6f", "#a9845a", "#f7f4f7", "#ffffff", "#ffffff", "#2a1530", "#2a1230", "#f1e8f2", "#f4eef4", "#241028"),
        P("mauve", "Soft Mauve Rose", "Refined soft rose, still formal", "#7d4f6e", "#7a6a74", "#a07d72", "#faf6f8", "#ffffff", "#ffffff", "#2e1c28", "#3a2434", "#f6ecf2", "#f7eff3", "#3a2434"),
        P("lavender", "Lavender Slate", "Quiet lavender with slate", "#5f5b8f", "#6b6f80", "#4f8a8b", "#f6f6fa", "#ffffff", "#ffffff", "#24263a", "#2c2e44", "#ecebf6", "#f1f0f8", "#26283c"),
    ];
}
