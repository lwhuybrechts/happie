---
inclusion: fileMatch
fileMatchPattern: "**/*.resx,**/*.razor,**/Happie.Web/**,**/Happie.Api/**,**/Happie.Shared/**"
---

# Happie — i18n & Resource File Conventions

## Localization Rules

- Supported locales: `"en"` (English) and `"nl"` (Dutch)
- Default locale when none is set: `"nl"`
- Locale is persisted across sessions
- Language switches immediately without a page reload
- All source code, identifiers, and comments remain in English regardless of active locale
- Push subscription records store the housemate's locale so predefined nudge messages are resolved server-side in the recipient's language
- **All user-visible strings MUST use `IStringLocalizer<AppStrings>`** — NEVER hardcode English text directly in `.razor` components or service classes. This includes labels like "Today"/"Yesterday", relative time strings like "min ago", section headers, button text, placeholders, and error messages. Add keys to both `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English). For static utility classes that cannot inject `IStringLocalizer`, accept localized strings as method parameters.

---

## Resource File Conventions (MUST follow)

The app uses two separate sets of `.resx` resource files for different purposes. Mixing them up breaks server-side resolution.

### SharedStrings (`Happie.Shared/Resources/`)

| File | Purpose |
|---|---|
| `SharedStrings.resx` | Dutch (default) translations |
| `SharedStrings.en.resx` | English translations |

**Contains:** strings resolved at runtime by `SharedStringResolver` — used by both frontend and backend.

- History keys (`history_*`): `history_attendance_set`, `history_dish_set`, `history_comment_set`, `history_comment_deleted`, `history_chef_status_changed`
- Nudge keys (`nudge_*`): `nudge_please_add_attendance`, `nudge_what_would_you_like_to_eat`, `nudge_dinner_soon_whats_your_plan`
- AttendanceStatus display name keys (`status_*`): `status_Unknown`, `status_EatingIn`, `status_NotEatingIn`
- Enabled/disabled display name keys (`enabled_*`): `enabled_true`, `enabled_false`

**NEVER add UI-only strings (labels, headings, button text) here.**

### AppStrings (`Happie.Web/Resources/`)

| File | Purpose |
|---|---|
| `AppStrings.resx` | Dutch (default) UI strings |
| `AppStrings.en.resx` | English UI strings |

**Contains:** UI-only strings resolved via `IStringLocalizer<AppStrings>` — labels, headings, button text, placeholders, error messages displayed in the UI.

**NEVER add `history_*`, `nudge_*`, `status_*`, or `enabled_*` keys here.** These keys must live in SharedStrings so the backend can resolve them for push notifications.

### SharedStringResolver usage

`SharedStringResolver` is registered as a **singleton** in both `Happie.Web/Program.cs` and `Happie.Api/Program.cs`.

**Method signatures:**
```csharp
string Resolve(string translationKey, string? parameters, Locale locale)
string Resolve(string translationKey, Dictionary<string, string>? parameters, Locale locale)
```

**Frontend (Blazor components):**
- Inject `SharedStringResolver` into the component
- Resolve using the user's active locale from `CultureInfo.CurrentUICulture`

```csharp
@inject SharedStringResolver SharedStringResolver

var locale = CultureInfo.CurrentUICulture.Name == "en" ? Locale.En : Locale.Nl;
var resolved = SharedStringResolver.Resolve(entry.TranslationKey, entry.Parameters, locale);
```

**Backend (handlers/services):**
- Inject `SharedStringResolver` into the handler
- Resolve per-recipient using their stored locale from the push subscription record

```csharp
var resolved = _sharedStringResolver.Resolve(translationKey, parameters, recipientLocale);
```

**Special parameter handling:**
- `status` parameter: raw enum value (e.g. `"EatingIn"`) is resolved to a localized display name via the `status_{enumValue}` key
- `enabled` parameter: `"true"`/`"false"` is resolved to a localized display name via the `enabled_{value}` key
- `date` parameter: formatted using locale convention (`"d MMMM"` for Dutch, `"MMMM d"` for English)
- Unknown keys: returns the raw key as fallback
- Null/empty parameters: returns the template without substitution
