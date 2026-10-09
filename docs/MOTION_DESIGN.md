# Motion design

Motion in CyberLMS is **functional and quiet**:
- It shows where things come from and what just changed (saved, answered, passed).
- It never blocks input and never loops near reading content.
- Every animation is CSS (`wwwroot/css/ds.css`, `app.css`) or a small progressive enhancement in `wwwroot/js/ui.js`.
- Pages work, and look complete, with motion turned off.

Real-browser recordings (Chromium, 1440×900, WebM) are in [`docs/screenshots/redesign-v1/motion/`](screenshots/redesign-v1/motion/).

| Recording | What it shows |
|---|---|
| `01-login-motion.webm` | Staggered entrance of the login page, slow floating background orbs, focus rings, password show/hide, button feedback, and the page transition after sign-in |
| `02-employee-dashboard.webm` | Hero progress ring and count-up, staggered cards, hover lift on to-do items, tiles and result rows, scroll reveal of the content cards |
| `03-assessment-selection-transition.webm` | Assessment intro → start, choice cards (hover, select, check-mark pop), automatic advance after a single-choice answer with a direction-aware slide, multi-select, "previous", the question navigator, number-key selection |
| `04-assessment-result-reveal.webm` | Result ring fill with the passing-score tick, number count-up, the "Passed" pop and the short celebration burst, then the answer review opening |
| `05-admin-content-wizard.webm` | Content wizard: step bar, type cards, editor heading + table-size picker, acknowledgment Yes/No reveal with the live statement preview, assessment reveal, question type switch, saving a question (the list item flashes, and the editor is ready for the next question) |
| `06-reduced-motion-dashboard.webm` | The same dashboard with the operating system's *reduce motion* setting: everything is in place immediately, no movement |

## Learning experience v2 (employee portal)

Recordings (Chromium, 1440×900, WebM) are in [`docs/screenshots/learning-experience-v2/motion/`](screenshots/learning-experience-v2/motion/):

| Recording | What it shows |
|---|---|
| `01-dashboard-progress.webm` | Dashboard entrance, the progress ring filling **from the value seen on the previous visit** to the current one, the "+N%" change chip, journey track drawing, hover on tasks and content cards |
| `02-acknowledgment-unlock.webm` | Content page → acknowledgment: checkbox enables the button, loading state, "تم الإقرار بنجاح", the lock lifting off the assessment card, the card un-greying and the "ابدأ الاختبار" call to action glowing once |
| `03-assessment-selection.webm` | Assessment intro, answer hover and selection, auto-advance and direction-aware step transition, previous/next, submission summary in the confirmation and the calm "جارٍ احتساب نتيجتك" state |
| `04-result-pass.webm` | Passed result: ring fill and count-up, the success icon settling, "مجتاز" status, one small celebration, and the "تم إكمال هذه المادة بنجاح" card rising in |
| `05-result-fail.webm` | Failed result: the same reveal without celebration, warm (not red) tone, "مراجعة المحتوى" and "إعادة المحاولة" |
| `06-full-learning-journey.webm` | A fresh employee: dashboard → content with its journey → acknowledgment and unlock → assessment → passed result and completion → dashboard with the progress increase and the new achievement |

### Additional tokens (`wwwroot/css/learning.css`)

| Token | Value | Use |
|---|---|---|
| `--lx-fast` | 160 ms | Hover and press feedback (task rows, answer options) |
| `--lx` | 260 ms | State changes: journey nodes, card states, borders and shadows |
| `--lx-slow` | 420 ms | Journey track fill, larger reveals |
| `--lx-ease` | `cubic-bezier(.2, .7, .2, 1)` | Ease-out without overshoot, used for all learning-experience motion. The existing spring is kept only for check marks |

### Catalogue (learning experience)

| Animation | Where | Duration / easing | Purpose |
|---|---|---|---|
| Journey track draw | Every full journey (dashboard next step, content page, result) | 420 ms after a 250 ms pause, `--lx-ease` | Shows how far along the item is, filling toward the reading direction |
| Journey step change (`is-changing`) | Content page right after the acknowledgment | Node settles 550 ms, check mark 450 ms spring | The acknowledgment step turns done and the assessment becomes current, in the same place the employee is looking |
| Progress since last visit | Dashboard hero ring + "+N%" chip | Ring from the previous value (1.4 s, existing fill); chip fades in and out over 2.6 s, once | Makes completing something visible on return to the dashboard. Stored per browser only; the number shown always comes from the server |
| Lock → unlock | Assessment card after acknowledgment | Lock lifts and fades (1 s), assessment icon fades in at 0.75 s, the card leaves its greyed state (0.9 s), the CTA glows once (1.6 s) | A clear, satisfying change from "🔒 الاختبار مقفل" to "ابدأ الاختبار" |
| Button loading (`is-loading`) | Acknowledge, start/continue/retry assessment | Until navigation | Confirms the click; prevents double submission |
| "Calculating your result" | Assessment after confirming | 300 ms fade-in; spinner and shimmer lines until the result page | Replaces an abrupt blank wait with a calm, layout-stable state |
| Unanswered warning | Assessment | 300 ms rise | Draws attention without shaking or flashing |
| Result icon settle + status | Result page (pass) | Icon 700 ms at 0.5 s, status 450 ms at 1.1 s | The success moment arrives after the score is readable |
| Celebration (`burst`) | Result page, pass only | 18 particles, once, ~1.4 s | Small and single; never on a failed result, never elsewhere |
| Completion card rise (`is-new`) | Result page / content page when the item was just completed | 600 ms rise, seal settles 700 ms | Marks the completion of a professional training requirement |

Everything above is disabled under `prefers-reduced-motion: reduce` (the journey is drawn in its final state immediately, no change chip, no unlock or completion animation, no celebration). Verified by the acceptance script (reduced-motion check).

## Tokens

| Token | Value | Use |
|---|---|---|
| `--dur-fast` | 150 ms | Colour and background changes on hover and focus |
| `--dur` | 280 ms | Default for state changes: borders, shadows, small transforms |
| `--dur-slow` | 600 ms | Larger reveals |
| `--ease` | `cubic-bezier(.22, .8, .24, 1)` | Standard ease-out: fast start, soft landing; used for almost everything |
| `--ease-spring` | `cubic-bezier(.34, 1.56, .64, 1)` | Small overshoot. Only for confirmations (check marks, the selected state, the current step) |

Distances stay small: 2–4 px for hover lifts, 14–16 px for entrances, and 28 px for the assessment step slide.

## Catalogue

| Animation | Where | Duration / easing | Purpose |
|---|---|---|---|
| `fade-up` entrance (`.reveal`, staggered with `--i` × 70 ms) | Page headers, panels, cards, table rows | 650 ms, `--ease` | Shows the page structure in reading order; the stagger guides the eye from the title to the content |
| Scroll reveal (`.reveal-scroll`, IntersectionObserver) | Content cards, attachments, acknowledgment panel, next step | 700 ms, `--ease`, 60 ms stagger | Content below the fold arrives as it is reached, without loading delays |
| Page transition (View Transitions API) | Every same-origin navigation | Old page 160 ms out, new page 280 ms in | Removes the white flash between pages; the sidebar and top bar stay still (they have their own `view-transition-name`) |
| Floating orbs (`float-y`) | Login hero only | 9 s and 11 s, ease-in-out, infinite | Ambient brand depth on the one page without reading content |
| Progress ring fill | Dashboard hero, results, reports, acknowledgment ratio | 1.4 s, `--ease`, 200 ms delay | Makes "how far am I" tangible; fills toward the reading direction (mirrored in RTL) |
| Count-up (`[data-count]`) | KPI values, ring labels | 600–1400 ms (scales with the value), cubic ease-out | Draws attention to the number while the ring fills |
| Meter / stacked bar / donut segments | Progress tiles, reports, admin dashboard | 1.1–1.2 s, `--ease` | Same as the rings, for linear and categorical data |
| Hover lift | Cards, KPI tiles, to-do items, attachments, type cards | 280 ms, `--ease`, −2/−3 px plus a stronger shadow | Signals that the whole card is clickable |
| Icon spring | Sidebar links, drop zone | 280 ms, `--ease-spring`, scale 1.08–1.12 | Small tactile feedback |
| Choice card selection | Assessment answers, wizard Yes/No and type cards, correct-answer markers | 280 ms colour/border; check mark 350 ms `--ease-spring` from scale 0 | The answer the employee picked is unmistakable |
| Choice hover nudge | Assessment answers | 280 ms, 3 px toward the reading direction | Invites the click without moving the layout |
| Question step slide (`step-in`) | Assessment wizard | 450 ms, `--ease`, 28 px; direction-aware (forward vs back, mirrored for RTL) | Keeps orientation when moving between questions |
| Auto-advance | Single-choice and true/false questions | 420 ms pause, then the step slide | Confirms the choice before moving on; never on the last question |
| Result status pop + burst | Passed result | Status `pop-in` 600 ms spring at 0.9 s; 34 particles over 1.4 s, starting at 0.85–1.1 s (pass only) | Celebrates success once, after the score is readable; nothing for a failed result |
| Acknowledgment confirmation (`check-pop`) | Acknowledged panel, publish success | 600–700 ms, `--ease-spring` | Positive confirmation of a compliance action |
| Modal | Confirmation dialogs | Spring transform plus a blurred backdrop | Focuses attention on the decision |
| Current step (`pop-in`) | Content wizard step bar | 450 ms, `--ease-spring` | Shows which step is active after each save |
| Wizard progress line | Under the step bar | 800 ms, `--ease` | Overall completion of the wizard |
| Yes/No section reveal (`fade-up`) | Acknowledgment statement, assessment settings | 400 ms, `--ease` | Fields appear only when they apply |
| Saved question flash (`flash-in`) | Question list after saving (`:target`) | 1.2 s background fade | Shows where the saved question landed in the list |
| Editor / option rows (`fade-up`) | Question editor, new option row | 300–400 ms | New inputs appear near the cursor |
| Table-size picker (`pop-in`) | Rich-text editor | 180 ms | Small, quick popover |
| Reading progress bar | Content page | Scroll-linked (no timing) | Shows how much of a long policy is left |
| Loading spinner (`.is-loading`) | Sign-in and other submit buttons with `data-busy` | `spin`, continuous until navigation | Shows that the request is in progress |
| `pv-flash` | Branding studio preview after applying a preset or reverting | 0.6 s, once | Confirms that the preview changed; the unsaved state is shown by a static "Unsaved changes" chip (no looping animation) |

## Rules

1. **Motion follows meaning.** Entrances run once. Confirmations use the spring easing. Neutral state changes use the standard ease. Nothing animates only for decoration, except the login orbs, which sit away from any content.
2. **Never delay the task.** Entrances are short (most finish within 0.7 s) and use only opacity and transform, so the layout never shifts. Nothing loops near text an employee has to read.
3. **Direction-aware.** Slides, rings, progress bars and hover nudges follow the reading direction (RTL in Arabic, LTR in English).
4. **Brand-neutral.** All moving colours come from the design tokens, so motion looks right with any saved palette.
5. **Reduced motion.** Under `prefers-reduced-motion: reduce`:
   - Every animation and transition is reduced to 0.001 ms.
   - Entrance states are forced visible.
   - Page transitions are disabled.
   - `ui.js` sets final values immediately: rings, meters and numbers jump to the result, the celebration burst is skipped, and scrolling is instant.

   Verified by recording 06.
6. **Without JavaScript.** Rings and meters show their values in text. The assessment shows all questions on one page. Nothing is hidden behind an animation.
