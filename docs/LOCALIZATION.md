# Localization (Arabic first)

* **Default culture:** `ar-SA` (UI text, right-to-left). English (`en-US`) is kept as future compatibility.
* **Mechanism:** standard ASP.NET Core localization over `.resx` files: `Resources/SharedResource.ar.resx` holds every Arabic text. The **English source text is the key** (`@L["Save"]`, `L.Format("{0} email(s) queued.", n)`), so adding English later only needs an optional `SharedResource.en.resx` (without one, the English key is shown).
* **Where Arabic lives:** only in `.resx`. Controllers, services and views never contain Arabic literals. E-mail templates, report/export headers, validation messages, upload errors and audit details all come from the same resource file. Services return resource keys (e.g. upload errors); the caller translates them.
* **Validation messages:** DataAnnotations + model binding use `IStringLocalizer` (`The {0} field is required.` -> «حقل {0} مطلوب.»); display names come from `[Display(Name = "...")]` keys.
* **Dates & numbers:** stored as UTC; displayed Gregorian `dd/MM/yyyy HH:mm` with Western digits (`CultureSetup.FormattingCulture`). Hijri is not used.
* **E-mail:** always rendered in Arabic, RTL (`Res.Ar(...)`), independent of who triggers it.
* **Editor / widgets:** Quill's built-in English strings are replaced from resources (CSS variables + tooltips); confirmation dialogs and the file-picker use Arabic text passed from the layout via `data-t-*` attributes.
* **Stored data:** True/False answer options are stored as the keys `True`/`False` and translated on display; question/answer/content text is whatever the administrator types (Arabic or English).

## Adding or changing a text
1. Use the English text as the key in code/views: `@L["My new text"]`.
2. Add `<data name="My new text"><value>النص بالعربية</value></data>` to `Resources/SharedResource.ar.resx` (keep `{0}` placeholders).
3. `dotnet test` - `LocalizationTests` fails if a key used anywhere in code/views has no Arabic translation.

## Switching language
Users can switch from the header (cookie `CyberLms.Culture`); administrators set the default for users without a cookie in *Settings -> General*.
