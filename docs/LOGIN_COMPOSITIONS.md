# Login visual compositions

The visual panel of the login page is decorated by a **composition**: up to three line-art layers drawn as local SVG (no CDN, no
external images), all in the login text color, so every composition follows the active theme.

## Presets

| Id | Name | Layers |
|---|---|---|
| `executive` (default) | Executive Security | oversized translucent shield · soft geometric rings · small security network |
| `governance` | Governance & Compliance | controls grid · policy document (slightly rotated) · quiet rings |
| `policy` | Policy & Controls | policy document · checklist card · faint controls grid |
| `journey` | Awareness Journey | progress path with milestones · rings |
| `defense` | Cyber Defense | security network · guarding shield · rings |
| `minimal` | Minimal Executive | one soft geometric accent |
| `clean` | Clean | none |

Choosing a preset fills the three layer editors. Changing any layer makes the composition **custom** (`LoginComposition=custom` and the
`Layer{1..3}*` settings). Preset definitions live in code (`Services/LoginCompositions.cs`), so they can be refined in later releases
without touching saved data.

## Layer model and safe ranges

| Setting | Range / values |
|---|---|
| Motif | shield, controls grid, checklist, policy document, progress journey, security network, geometric accent, none |
| Position | 3×3 anchor grid (top/center/bottom × start/center/end, logical: correct in RTL and LTR) |
| Opacity | 4–30 % — **capped at 12 %** when the layer sits in the text column (start or center column) |
| Scale | 50–160 % of a base size relative to the panel width (container query units) |
| Rotation | −12° … +12°, only for motifs that tolerate it (never the shield) |
| Visible | on/off |

`LoginCompositions.Safe` applies these rules on the server; the studio script mirrors them (and shows a note when a layer is capped).
Layers are rendered behind the content (`z-index: 0`, `pointer-events: none`, `aria-hidden`), toned down to 65 % over a background
photograph, and on phones only the soft geometric accent remains.

## One rendering component

The real login page and the studio preview both render `Views/Shared/_LoginHero.cshtml`. The motif library
(`_LoginMotifs.cshtml`, `<symbol>` + `<use>`) is rendered once per page. Sizes use `cqi` (container query) units of the hero, so the
scaled preview and the real page are identical.

## Backward compatibility

The single "decorative pattern" of v1.1 (`Branding.LoginDecorStyle`) maps to the closest composition until a composition is saved:
shield → Executive Security, soft circles → Minimal Executive, checklist/document → Policy & Controls, grid → Governance & Compliance,
journey → Awareness Journey, none (or decorations off) → Clean.
