# UI design system (redesign v1)

Presentation layer only. No controller, service, scoring, auth, localization or report logic changed for it.

## Files

| File | Purpose |
|---|---|
| `wwwroot/css/fonts.css` | `@font-face` for the bundled **IBM Plex Sans Arabic** (Arabic and Latin subsets, weights 400–700; licensed under OFL, see `wwwroot/lib/fonts/LICENSE-IBM-Plex-OFL.txt`). Served locally, no CDN. |
| `wwwroot/css/ds.css` | Design tokens and the re-skin of Bootstrap components: buttons, cards, forms, tables, badges, chips, alerts, tabs, pagination, dropdowns, modal, meters, empty states, page header, motion utilities. |
| `wwwroot/css/app.css` | Application shell (sidebar, topbar), the 9 reference screens, and a small compatibility block for screens that have not been redesigned yet. |
| `/branding/theme.css` | Generated per request from the Branding settings. It overrides only the brand tokens below. |
| `wwwroot/js/ui.js` | Progressive enhancement: sidebar toggle, count-up, rings and meters, scroll reveal, reading progress, generated table of contents, acknowledgment checkbox, assessment wizard, result celebration, password toggle, and the Branding **live preview**. There are no inline scripts, so CSP `script-src 'self'` is unchanged. |
| `Services/Ui.cs` | View helpers: `Ui.Pct` (isolated LTR percentage), `Ui.Num`, `Ui.Ring` (SVG progress ring), and type, attachment and audit icons. |

## Tokens

### Brand inputs

These are overridden by `/branding/theme.css`, so nothing organization-specific is hard-coded:

- `--color-primary`, `--color-secondary`, `--color-accent`
- `--color-on-primary`, `--color-on-accent`: computed on the server from WCAG luminance (`FilesController.OnColor`), so button text stays readable for any brand color.
- `--color-header`, `--color-on-header`
- `--color-sidebar`, `--color-on-sidebar`
- `--color-login-bg`

### Derived scales

These use `color-mix()`, so they follow whatever brand is saved:

- `--primary-50 … --primary-900`
- `--accent-50/100/600`
- `--gradient-brand`, `--gradient-soft`

### Neutrals and surfaces

- `--color-bg`, `--color-surface`, `--color-surface-alt`
- `--color-border`, `--color-border-strong`
- `--color-text`, `--color-muted`, `--color-faint`

### Status colors

Reserved for state; never reused as brand colors.

- `--color-success`, `--color-warning`, `--color-danger`, `--color-neutral`, each with `-ink` and `-bg` variants.

### Shape, spacing and type

- **Shape:** `--radius-xs/sm/md/lg/xl/pill`, `--shadow-sm/md/lg/brand`, `--ring` (focus).
- **Rhythm:** `--space-1 … --space-8`, `--text-xs … --text-3xl`, body line-height 1.8.
- **Motion:** `--ease`, `--ease-spring`, `--dur-fast/dur/dur-slow`.

### Live-preview scope

Tokens are declared on `:root, .theme-scope`. The live preview sets brand inputs on its own `.theme-scope` container, and every derived shade recomputes inside it without touching the rest of the page.

## Coverage

All screens use the design system:
- The 9 reference screens.
- The content authoring wizard: see [CONTENT_AUTHORING.md](CONTENT_AUTHORING.md).
- The remaining employee and admin screens: content library, My Results, change password, access denied and error pages, assessments and questions, users, audit log, attempt details, and the classic content form.

Motion is documented in [MOTION_DESIGN.md](MOTION_DESIGN.md).

## Arabic, RTL and numbers

- Percentages and numbers are rendered as isolated LTR runs (`<bdi class="pct" dir="ltr">60%</bdi>`), so Arabic text reads **"درجة النجاح: 60%"** and never "%60". Use `@Ui.Pct(value)` in views.
- Mixed Arabic/English user content uses `.bidi` (`unicode-bidi: plaintext`).
- Directional icons use `.flip-rtl` / `.flip-ltr`.
- Rings are mirrored in RTL, and progress fills start from the reading side.
- Fractions are written as words ("1 من 2") rather than "1 / 2".

## Motion and accessibility

- **Motion:**
  - Staggered `fade-up` on load (`.reveal` with `--i`) and `.reveal-scroll` on scroll.
  - Animated rings, meters and donut; count-up numbers; step transitions in the assessment.
  - A short burst on a passed result; same-origin page view transitions.
  - All of it is disabled under `prefers-reduced-motion: reduce`.
- **Focus:** a visible focus ring on every control (`:focus-visible`), plus a skip link.
- **Keyboard:** choice cards keep real radio and checkbox inputs, so the keyboard works natively. Keys 1–9 pick an answer, and Esc closes the mobile sidebar.
- **Contrast:**
  - The Branding page warns when header or sidebar text contrast is below 4.5:1.
  - Text on primary and accent buttons is chosen automatically.
- **Without JavaScript:** every page still works; the assessment shows all questions on one page.

## Responsive behavior

- **Below 992px:** the sidebar becomes an off-canvas drawer. KPI grid columns drop from 6 to 3, then to 2.
- **Below 1200px:** the reading layout and the branding layout collapse to one column.
- **Verified at:** 1920×1080, 1440×900, 1366×768 and 820×1180 (tablet).
