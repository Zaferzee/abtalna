# Inline video and PDF (multimedia v1)

Content pages show video and PDF attachments inside the page instead of only offering them as downloads. The same partial view (`Views/Shared/_ContentReader.cshtml`) renders:
- the employee content page (`/Content/Details/{id}`);
- the wizard's **معاينة كموظف** preview (`/Admin/Authoring/Preview/{id}`).

As a result, the preview shows the same video and PDF that employees see.

## Page order

1. Title, description and details.
2. Written content, with the table of contents and step card.
3. **الفيديو**: embedded video player.
4. **المستند**: embedded PDF reader.
5. Other attachments, then the link:
   - images are shown as figures;
   - Word, Excel and PowerPoint files are offered as downloads.
6. Acknowledgment.
7. Next step: the linked assessment.

Each attachment is rendered once. A PDF appears in the reader and is not repeated in the download list; the reader has its own download button.

## Video

- **Player:** a native HTML5 `<video controls preload="metadata" playsinline>`.
  - Its `src` is the existing authenticated endpoint `/Files/Attachment/{id}`. There is no storage path, public URL, streaming service or cloud.
  - The endpoint supports HTTP range requests (`206 Partial Content`), so seeking works without downloading the whole file.
- **Layout:**
  - A 16:9 frame, centered, at most 960px wide, and responsive.
  - Videos whose aspect ratio is clearly different (portrait or very wide) keep their own ratio and are letterboxed on a dark background.
  - Small videos are never stretched: the frame is capped at 1.5× the video's natural width, with a minimum of 480px.
- **Several videos:** one player plus a list of titles (the file name without its extension). Choosing a title switches the player.
- **Errors:** if the browser cannot play the file (for example an unsupported codec), the player shows an Arabic message and a download button.
- **Logic:** `wwwroot/js/ui.js` (embedded video player block) and the `.video-player` / `.vp-*` styles in `wwwroot/css/app.css`.

## PDF

- **Library:** [PDF.js](https://mozilla.github.io/pdf.js/) **5.7.284**, legacy build, bundled under `wwwroot/lib/pdfjs/`. There is no CDN and no internet access at runtime.
  - The folder contains the library, its worker, CMaps (for CJK and Arabic font encodings), standard fonts and the JBIG2, OpenJPEG and QCMS decoders.
  - Licenses: `LICENSE` (Apache-2.0) and `wasm/LICENSE_*`.
  - The legacy build is used because the modern build needs very recent browser features.
- **Viewer:** `wwwroot/js/pdf-viewer.mjs`, an ES module loaded with `<script type="module">`, so there are still no inline scripts.
- **Loading:**
  1. The viewer downloads the file with `fetch(src, { credentials: 'same-origin' })` from the same authenticated endpoint `/Files/Attachment/{id}`.
  2. It passes the bytes to PDF.js.
  - PDF.js runs with `isEvalSupported: false` and with XFA disabled.
- **Features:**
  - Previous and next page, a page-number field, and «الصفحة X من Y».
  - Zoom in and out, plus fit to width, whole page, and 75–200 %.
  - Fullscreen and download (`?download=true`).
  - Pages are drawn lazily as they scroll into view, at the screen's pixel density.
  - The page width is about A4 (at most 920px), and the viewer is responsive.
  - The toolbar is in Arabic and RTL. The pages themselves stay left-to-right so that PDF layout is not mirrored.
- **Several PDFs:** tabs above the viewer switch between documents.
- **Arabic states:**

  | State | Message / behavior |
  |---|---|
  | Loading | جارٍ تحميل المستند |
  | Missing (404) | المستند غير متاح |
  | Invalid or corrupt file | تعذّر عرض الملف: الملف تالف أو ليس مستند PDF صالحاً |
  | Password protected | Arabic notice |
  | Session expired (redirect to sign-in, 401 or 403) | Message plus a sign-in button |
  | Any other error | Message plus a retry button |

- **Static file types:** `Program.cs` maps the content types PDF.js needs (`.mjs`, `.bcmap`, `.pfb`, `.wasm`) for the static file middleware.

## Security

- **Authorization is unchanged.** Both players use `/Files/Attachment/{id}`, which:
  - requires sign-in (anonymous requests are redirected to sign-in and never receive the file);
  - returns 404 to employees for attachments of unpublished content (administrators can still preview them);
  - serves every file with `Content-Security-Policy: default-src 'none'; sandbox`.
- **No storage paths or new routes.** The physical storage path is never written into the page, and no new file route or public URL was added.
- **The site CSP is unchanged:** `script-src 'self'`, `media-src 'self'`, `object-src 'self'`. PDF.js and its worker are same-origin files. The PDF is drawn to `<canvas>`, so no `<iframe>`, `<object>` or browser PDF plug-in is involved.
- **Content checks:** PDF.js is configured without `eval`, without XFA forms and without scripting (the QuickJS sandbox was removed from the bundle). Links inside a PDF are not activated.
- **Uploads are unchanged:** the existing type, size and signature checks still apply.

## Audit Log fix

- **Symptom:** **سجل التدقيق** and the dashboard's **عرض الكل** showed an empty table even though entries existed.
- **Root cause:**
  - `AuditController.Index` declared its filter parameter as `string? action`.
  - In ASP.NET Core MVC, `action` is a reserved route value. Model binding takes it from the route («Index») before the query string.
  - Every request was therefore filtered by `Action == "Index"`, which matches no entry.
- **Fix:**
  - The filter is now the query parameter `op` (`[FromQuery(Name = "op")]`).
  - The page shows:
    - date and time, user, the action (Arabic name plus the original code), entity, details and IP;
    - the total count, an Arabic empty state, pagination of 50 per page, and search across user, details, action and entity.
- **Data is unchanged:** audit records are not rewritten, deleted or editable; the page is read-only.
- **Tests:** a regression test fails on the old code (expected 8 rows, actual 0).

## Browser and codec note

- **MP4 codecs:** browsers play MP4 only with codecs they support.
  - Chrome, Edge, Firefox and Safari on Windows and macOS play H.264/AAC MP4, the usual format from cameras and screen recorders.
  - Chromium builds without proprietary codecs (such as the one used for automated tests here) play VP9/AV1 MP4 and WebM, but not H.264.
  - The UI acceptance run therefore used a VP9/Opus MP4. When a codec is unsupported, the player shows its Arabic fallback with a download button.
- **PDF.js decoders:** PDF.js uses WebAssembly decoders only for JBIG2 and JPEG 2000 images.
  - Under the site CSP (no `'wasm-unsafe-eval'`), it uses the bundled JavaScript fallbacks.
  - These are slower for those rare image types but behave the same.

## Evidence

- **Automated:** `tests/CyberLms.Tests/MediaAndAuditTests.cs`, 3 tests against PostgreSQL over real HTTP. They cover:
  - the inline video and PDF markup, with no storage path in the page;
  - an authorized PDF served inline and sandboxed, plus the download variant;
  - video range requests;
  - anonymous access refused;
  - unpublished media hidden from employees but visible in the administrator's preview;
  - the Audit Log rows, Arabic names, filter, search, empty state, pagination, the dashboard «عرض الكل» destination, and employee access denied.
- **UI acceptance:** `tests/ui/multimedia-acceptance.mjs`, 22/22 checks. See [screenshots/multimedia-v1/README.md](screenshots/multimedia-v1/README.md).
