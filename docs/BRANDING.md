# Branding & Appearance studio (استوديو الهوية والمظهر)

**Admin → Settings → الهوية والمظهر.** Everything here is applied to all users as soon as it is saved. No rebuild or restart is needed, and there is no schema change: values are stored as `Branding.*` rows in `SystemSettings`.

Screenshots and the UI acceptance run are in [screenshots/branding-studio-v1](screenshots/branding-studio-v1/README.md).

## Sections

| Section | Fields |
|---|---|
| General identity | Organization name and system name (multi-line); show the organization name in the sidebar; show the names next to the logo on the login page; welcome text (employee dashboard); footer text; support text |
| Logos & icons | Sidebar logo (with or without a light plate), login-page logo (with or without a plate; placement: panel / sign-in card / both), compact icon (used in the sidebar when there is no sidebar logo), browser icon (favicon, `.ico`/`.png`) |
| Login page text | Badge (with a show/hide toggle), hero title (multi-line; empty = system name), description, features 1–3 (with a show/hide toggle), card title, card subtitle, login notice |
| Login page media | Background image; fit (cover / contain / fill / original size); position (3×3 grid or a custom focal point picked on the image); zoom 100–200%; overlay color and opacity 0–95%; layout (split, split reversed, full-screen image); text placement (start / center); decorative shapes on or off; revert the media settings |
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
- Tests: `tests/CyberLms.Tests/BrandingTests.cs`; UI: `tests/ui/branding-studio-acceptance.mjs`.

## Limitations

- **No separate dark-mode logo variants.** The application has no dark theme to switch them. Instead, each logo has a light-plate option for dark backgrounds, and the sidebar and login logos are separate uploads.
- **No image cropping editor.** Framing uses fit, position or focal point, and zoom.
- **The preview is a faithful scaled render, not a screenshot.** Fonts and sizes match the real page. The phone view uses the same mobile rules, applied through a class instead of a media query.
- **One theme applies to all users.** There are no per-department themes.
- **Changing colors does not recolor uploaded images.**
