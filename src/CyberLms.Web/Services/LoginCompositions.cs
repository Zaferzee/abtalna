namespace CyberLms.Web.Services;

/// <summary>
/// Login visual composition system: curated presets made of up to three decorative layers (line-art motifs drawn as local SVG,
/// see Views/Shared/_LoginMotifs.cshtml). A layer has a motif, a position on a 3×3 anchor grid, opacity, scale and an optional
/// small rotation. Safe ranges are enforced on the server (and mirrored by the studio script) so a composition can never cover the
/// text: layers stay behind the content, opacity is capped, and anything placed in the text column is capped further.
/// </summary>
public static class LoginCompositions
{
    public record Layer(string Motif, string Position, int Opacity, int Scale, int Rotate, bool Visible = true);
    public record Composition(string Id, string Name, string Description, Layer[] Layers);

    public static readonly string[] Motifs = ["none", "shield", "grid", "checklist", "document", "journey", "network", "circles"];
    public static readonly string[] Positions = ["top-start", "top-center", "top-end", "center-start", "center", "center-end", "bottom-start", "bottom-center", "bottom-end"];
    /// <summary>Motifs that may be rotated slightly (a rotated shield or emblem looks careless, so it stays upright).</summary>
    public static readonly string[] Rotatable = ["grid", "checklist", "document", "journey", "network", "circles"];

    public const int MinOpacity = 4, MaxOpacity = 30, TextColumnMaxOpacity = 12;
    public const int MinScale = 50, MaxScale = 160, MaxRotate = 12;

    private static readonly Layer Off = new("none", "center-end", 12, 100, 0, false);

    public static readonly Composition[] All =
    [
        new("executive", "Executive Security", "Oversized translucent shield with soft geometric rings",
            [new("shield", "center-end", 16, 118, 0), new("circles", "top-end", 12, 100, 0), new("network", "bottom-end", 9, 70, 0)]),
        new("governance", "Governance & Compliance", "Controls grid with a policy document and quiet rings",
            [new("grid", "center-end", 15, 130, 0), new("document", "bottom-end", 15, 72, -6), new("circles", "top-end", 10, 90, 0)]),
        new("policy", "Policy & Controls", "Policy document with a checklist card",
            [new("document", "center-end", 17, 112, 0), new("checklist", "bottom-end", 15, 70, 6), new("grid", "top-end", 8, 90, 0)]),
        new("journey", "Awareness Journey", "Progress path with milestones",
            [new("journey", "center-end", 19, 130, 0), new("circles", "top-end", 10, 96, 0), Off]),
        new("defense", "Cyber Defense", "Security network with a guarding shield",
            [new("network", "center-end", 17, 140, 0), new("shield", "bottom-end", 15, 66, 0), new("circles", "top-end", 9, 84, 0)]),
        new("minimal", "Minimal Executive", "A single quiet geometric accent",
            [new("circles", "top-end", 12, 120, 0), Off, Off]),
        new("clean", "Clean", "No decoration",
            [Off, Off, Off]),
    ];

    public const string Custom = "custom";
    public static readonly string[] Ids = [.. All.Select(c => c.Id), Custom];

    /// <summary>Legacy single-pattern setting (branding studio v1.1) mapped to the closest composition.</summary>
    public static string FromLegacy(string decorStyle) => decorStyle switch
    {
        "none" => "clean", "orbs" => "minimal", "checklist" or "document" => "policy", "grid" => "governance", "journey" => "journey", _ => "executive",
    };

    /// <summary>Resource key of a position label.</summary>
    public static string PositionLabel(string position) => "Position: " + position;

    /// <summary>Column of a position in reading order: start (text side), center, end (free side).</summary>
    public static string Column(string position) => position.EndsWith("-start") || position == "center-start" ? "start" : position.EndsWith("-end") ? "end" : "center";

    /// <summary>Applies the safety rules: ranges, upright non-rotatable motifs, and a lower opacity ceiling inside the text column
    /// (start and center columns, which hold the text in both alignments).</summary>
    public static Layer Safe(Layer l)
    {
        var motif = Motifs.Contains(l.Motif) ? l.Motif : "none";
        var pos = Positions.Contains(l.Position) ? l.Position : "center-end";
        var op = Math.Clamp(l.Opacity, MinOpacity, MaxOpacity);
        if (Column(pos) != "end") op = Math.Min(op, TextColumnMaxOpacity);
        var rot = Rotatable.Contains(motif) ? Math.Clamp(l.Rotate, -MaxRotate, MaxRotate) : 0;
        return new Layer(motif, pos, op, Math.Clamp(l.Scale, MinScale, MaxScale), rot, l.Visible && motif != "none");
    }
}
