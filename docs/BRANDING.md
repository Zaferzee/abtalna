# Branding & Appearance studio (استوديو الهوية والمظهر)

**Admin → Settings → الهوية والمظهر.** Everything here is applied to all users as soon as it is saved. No rebuild or restart is needed, and there is no schema change: values are stored as `Branding.*` rows in `SystemSettings`.

Entity branding (overrides per organizational entity, inherited from the central branding) is described in [ENTERPRISE_ENTITIES.md §7](ENTERPRISE_ENTITIES.md). Screenshots and the UI acceptance runs are in [screenshots/branding-studio-v1](screenshots/branding-studio-v1/README.md) and, for independent logos, image display modes and decorative patterns, [screenshots/branding-studio-v1.1](screenshots/branding-studio-v1.1/README.md).

## Sections

| Section | Fields |
|---|---|
| General identity | Organization name and system name (multi-line); show the organization name in the sidebar; show the names next to the logo on the login page; welcome text (employee dashboard); footer text; support text |
| Logos & icons | Four assets: sidebar logo (with or without a light plate), shown in the sidebar and at the top of the login page's visual panel; login-page logo, shown only inside the sign-in card (if empty, the card shows the initial of the system name; it never appears in the visual panel); compact icon (shown in the sidebar only while there is no sidebar logo); browser icon (favicon, `.ico`/`.png`) |
| Login page text | Badge (with a show/hide toggle), hero title (multi-line; empty = system name), description, features 1–3 (with a show/hide toggle), card title, card subtitle, login notice |
| Login page media | Login image; **display mode**: full background / large image / medium image / small (visual mark) / hidden (the file is kept); for the framed modes: size (10–100% of the text column), alignment (start / center / end) and place (above / below the text); spacing from the edges (0–64 px: a frame around the full background, or the space around a framed image); fit (cover / contain / fill / original size); position (3×3 grid or a custom focal point picked on the image); zoom 100–200%; overlay color and opacity 0–95% (full background only); layout (split, split reversed, full-screen image); text placement (start / center); revert the media settings |
| Visual composition | Curated compositions (Executive Security, Governance & Compliance, Policy & Controls, Awareness Journey, Cyber Defense, Minimal Executive, Clean) and up to three layers (motif, position, opacity, scale, rotation, visibility) with safe ranges. See [LOGIN_COMPOSITIONS.md](LOGIN_COMPOSITIONS.md). Replaces the single decorative pattern of v1.1 |
| Colors & theme | 8 formal presets, then fine-tuning of 15 colors: primary, secondary, accent, page background, card surface, header and header text, sidebar and sidebar text, button and button text (empty = automatic), banner text, login background, login text, login overlay |

### Multi-line names

The organization name, the system name, the hero title, the description and the notice keep line breaks (press Enter). They are rendered with `white-space: pre-line` in the login hero, the sidebar and the previews. Page titles, e-mails and the login footer use a single-line form (`OrgNameInline` / `SystemNameInline`), so `<title>` never contains a line break. Single-line fields (badge, features, card title) are flattened on save. The sidebar wraps long names instead of clipping them.

### Presets

| Id | Name | Character |
|---|---|---|
| `classic` | أزرق كلاسيكي (Classic blue) | the original default |
| `navy` | الكحلي التنفيذي (Executive Navy) | official and authoritative |
| `indigo` | النيلي الملكي (Royal Indigo) | prestigious, gold accent |
| `graphite` | الجرافيت الذهبي (Graphite Gold) | restrained, luxurious |
| `emerald` | الزمردي العميق (Deep Emerald) | calm and trustworthy |
| `plum` | البرقوقي التنفيذي (Plum Executive) | elegant |
| `mauve` | الوردي الموفي الهادئ (Soft Mauve Rose) | refined soft rose, still formal |
| `lavender` | الخزامى الرمادي (Lavender Slate) | quiet lavender with slate |

Every preset sets all theme colors. `BrandingTests` checks that the header, sidebar, button, banner, login and page text pairs reach 4.5:1 (WCAG AA). Choosing a preset only fills the color fields; nothing is saved until **Save changes**.

## Workflow

- **Live preview:** the real login markup (the same `_LoginHero` partial) and an application shell (sidebar, header, banner, cards, buttons, status badges). It shows desktop or phone and can be enlarged. Selected images show immediately, read as `data:` URLs because the CSP does not allow `blob:`.
- **Readability check:** the contrast ratio of each text/background pair. Clear ≥ 4.5:1, weak ≥ 3:1, otherwise low.
- **Unsaved changes:** a chip at the top and in the sticky action bar. The browser also asks before leaving the page with unsaved changes.
- **Discard changes:** restores the saved values, including chosen files.
- **Revert theme / revert media:** restores the saved colors, or the saved login image settings.
- **Revert image:** on each asset tile, undoes a newly chosen file or a pending removal.
- **Reset to default:** asks for confirmation, clears every `Branding.*` value and deletes the uploaded branding files.

## Server-side validation (`SettingsController.SaveBranding`)

- Colors must be `#RRGGBB`; otherwise nothing is saved and the invalid fields are named. An empty color means default or automatic.
- Choices must be one of the allowed values; anything else falls back to the default.
- Numbers are parsed culture-independently and clamped.
- Texts are trimmed and cut to their maximum length; three or more blank lines collapse to one.
- Fields absent from a submission keep their saved value.
- An unknown preset id is not stored.
- Images go through the existing upload checks (extension, signature, size). The favicon accepts only `.ico`/`.png`.
- Old files are deleted after a replacement or removal.
- Assets are served by `/Files/Brand?kind=logo|loginlogo|icon|favicon|loginbg`; unknown kinds return 404.
- Audit entries: `BRANDING_UPDATED`, `BRANDING_RESET`.

The login page only writes validated values into its inline CSS variables (`Branding.LoginHeroStyle`), so a stored value cannot inject CSS.

## Files

- `Services/Branding.cs`: field catalogue, defaults, validation on read, CSS helpers, contrast.
- `Services/BrandingPresets.cs`: the presets.
- `Areas/Admin/Views/Settings/_BrandingStudio.cshtml`, `wwwroot/js/branding-studio.js`, `wwwroot/css/brand.css`: the studio.
- `Views/Shared/_LoginHero.cshtml`, `Views/Shared/_SidebarBrand.cshtml`: shared by the real pages and the preview.
- `Controllers/FilesController.cs`: `theme.css` and the brand assets.
- Tests: `tests/CyberLms.Tests/BrandingTests.cs`; UI: `tests/ui/branding-studio-acceptance.mjs`, `tests/ui/branding-studio-refine.mjs`.

## Limitations

- **Login logos:** the visual panel always uses the sidebar logo (the initial of the system name when there is none); the login-page logo is shown only in the sign-in card. The former placement and login-plate options were removed.
- **Fixed frame shapes.** Framed login images use a fixed aspect ratio per mode: large 16:9, medium 4:3, small square. Fit, position or focal point, and zoom decide what is visible inside the frame.

- **No separate dark-mode logo variants.** The application has no dark theme to switch them. Instead, each logo has a light-plate option for dark backgrounds, and the sidebar and login logos are separate uploads.
- **No image cropping editor.** Framing uses fit, position or focal point, and zoom.
- **The preview is a faithful scaled render, not a screenshot.** Fonts and sizes match the real page. The phone view uses the same mobile rules, applied through a class instead of a media query.
- **One theme applies to all users.** There are no per-department themes.
- **Changing colors does not recolor uploaded images.**
