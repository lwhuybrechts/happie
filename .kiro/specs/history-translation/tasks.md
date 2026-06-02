# Implementation Plan: History Translation

## Overview

Replace the pre-rendered English `Description` field in DayHistory with structured `TranslationKey` + `Parameters` fields, enabling locale-aware resolution at read time. Introduces a shared `SharedStringResolver` class and `.resx` resource files in `Happie.Shared`, updates the API to return raw key+params, resolves client-side in `HistorySection`, resolves server-side for push notifications, migrates `NudgeMessageResolver` into the shared pattern, and converts all existing records via a migration script.

## Tasks

- [x] 1. Create SharedStringResolver and shared resource files
  - [x] 1.1 Create `SharedStrings.resx` (Dutch default) and `SharedStrings.en.resx` (English) in `Happie.Shared/Resources/`
    - Add all history keys: `history_attendance_set`, `history_dish_set`, `history_comment_set`, `history_comment_deleted`, `history_chef_status_changed`
    - Add all nudge keys: `nudge_please_add_attendance`, `nudge_what_would_you_like_to_eat`, `nudge_dinner_soon_whats_your_plan`
    - Add AttendanceStatus display name keys: `status_Unknown`, `status_EatingIn`, `status_NotEatingIn`
    - Add enabled/disabled display name keys: `enabled_true`, `enabled_false`
    - Use templates with `{placeholder}` syntax as defined in the design
    - _Requirements: 2.2, 2.3, 5.1, 6.1, 6.2, 6.3, 8.1_

  - [x] 1.2 Create `SharedStringResolver` class in `Happie.Shared/Resources/SharedStringResolver.cs`
    - Implement `Resolve(string translationKey, string? parameters, Locale locale)` overload
    - Implement `Resolve(string translationKey, Dictionary<string, string>? parameters, Locale locale)` overload
    - Use `ResourceManager` to load templates from `SharedStrings` resources
    - Substitute `{placeholder}` tokens with parameter values
    - Special-case `status` parameter: resolve raw enum value to localized display name via `status_{enumValue}` key
    - Special-case `enabled` parameter: resolve `"true"`/`"false"` to localized display name via `enabled_{value}` key (e.g. `enabled_true` → "enabled"/"ingeschakeld")
    - Special-case `date` parameter: format using locale convention (`"d MMMM"` for Dutch, `"MMMM d"` for English)
    - Return raw key as fallback when key not found
    - Return template without substitution when parameters are null/empty
    - Return raw key as fallback when parameters JSON is malformed
    - _Requirements: 2.1, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 7.5, 8.2, 8.3, 8.4_

  - [x] 1.3 Write unit tests for `SharedStringResolver`
    - Test each history key × locale combination produces expected output
    - Test each nudge key × locale combination produces expected output
    - Test AttendanceStatus display name resolution for all enum values × locales
    - Test null parameters returns template without substitution
    - Test empty parameters returns template without substitution
    - Test malformed JSON parameters returns raw key as fallback
    - Test unknown translation key returns the key itself
    - Test unknown status value passes through unchanged
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 2.7, 8.2, 8.3, 8.4_

- [x] 2. Modify domain and entity types
  - [x] 2.1 Update `DayHistoryEntry` domain record in `Happie.Api/Domain/DayHistoryEntry.cs`
    - Replace `Description` parameter with `TranslationKey` (string) and `Parameters` (string)
    - _Requirements: 1.1, 1.2, 1.10_

  - [x] 2.2 Update `DayHistoryEntity` in `Happie.Api/Infrastructure/Entities/DayHistoryEntity.cs`
    - Remove `Description` property
    - Add `TranslationKey` string property (default `string.Empty`)
    - Add `Parameters` string property (default `string.Empty`)
    - _Requirements: 1.1, 1.2, 1.10_

  - [x] 2.3 Update `DayHistoryEntryMapper` in `Happie.Api/Infrastructure/Mappers/`
    - Map `TranslationKey` and `Parameters` between entity and domain type instead of `Description`
    - _Requirements: 1.1, 1.2_

  - [x] 2.4 Update `HistoryEntryDto` in `Happie.Shared/Contracts/HistoryEntryDto.cs`
    - Remove `description` field
    - Add `translationKey` (string) with `[JsonPropertyName("translationKey")]`
    - Add `parameters` (string) with `[JsonPropertyName("parameters")]`
    - _Requirements: 3.1, 3.2_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update DayHandler write and read paths
  - [x] 4.1 Update `DayHandler` write path to store structured data
    - Attendance changes: store `"history_attendance_set"` with `{"name":"...","status":"..."}`
    - Dish changes: store `"history_dish_set"` with `{"description":"..."}`
    - Comment set: store `"history_comment_set"` with `{"name":"...","text":"..."}`
    - Comment deleted: store `"history_comment_deleted"` with `{"name":"..."}`
    - Chef status changes: store `"history_chef_status_changed"` with `{"name":"...","enabled":"true/false"}`
    - _Requirements: 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10_

  - [x] 4.2 Update `DayHandler` read path to return raw key+params in `HistoryEntryDto`
    - Pass `TranslationKey` and `Parameters` directly to `HistoryEntryDto` without resolution
    - _Requirements: 3.3_

  - [x] 4.3 Write unit tests for `DayHandler` write path
    - Verify each handler method stores the correct TranslationKey and Parameters (mock repository captures stored entry)
    - Verify `GetDayPlanAsync` returns raw `translationKey` and `parameters` in the DTO without resolution
    - _Requirements: 1.3, 1.6, 1.7, 1.8, 1.9, 3.3_

- [x] 5. Update PushHandler for server-side resolution
  - [x] 5.1 Update `IPushHandler` and `PushHandler.SendAutoNotificationsAsync` signature
    - Accept `translationKey` and `parameters` instead of pre-rendered `changeDescription` string
    - Inject `SharedStringResolver` into `PushHandler`
    - Resolve per-recipient using their stored locale via `SharedStringResolver`
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 5.2 Migrate `PushHandler.NudgeAsync` to use `SharedStringResolver`
    - Map `NudgeMessageKey` enum values to `nudge_` prefixed translation keys
    - Build parameters dict with locale-formatted date where needed
    - Resolve using `SharedStringResolver` with recipient's stored locale
    - _Requirements: 5.2, 5.3_

  - [x] 5.3 Delete `NudgeMessageResolver` static class from `Happie.Api/Services/`
    - Remove `NudgeMessageResolver.cs`
    - _Requirements: 5.4_

  - [x] 5.4 Write unit tests for `PushHandler` changes
    - Verify auto-notifications resolve per-recipient locale using `SharedStringResolver`
    - Verify nudge messages resolve using `SharedStringResolver` instead of old `NudgeMessageResolver`
    - _Requirements: 4.1, 4.2, 5.2, 5.3_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Update frontend components
  - [x] 7.1 Update `HistorySection.razor` to resolve client-side using `SharedStringResolver`
    - Inject `SharedStringResolver`
    - Resolve each entry's `translationKey` + `parameters` using user's active locale
    - Replace `@entry.Description` with resolved string
    - Derive locale from `CultureInfo.CurrentUICulture`
    - _Requirements: 3.4, 3.5_

  - [x] 7.2 Update `DaysFunction` — remove Accept-Language extraction if present
    - The function no longer needs to extract or pass locale information for history resolution
    - _Requirements: 3.3_

  - [x] 7.3 Register `SharedStringResolver` in DI for both `Happie.Web` (`Program.cs`) and `Happie.Api` (`Program.cs`)
    - Register as singleton in both projects
    - _Requirements: 2.1, 2.8_

- [x] 8. Create migration script and integration tests
  - [x] 8.1 Create `migrate-history.csx` in `Happie.Api.IntegrationTests/Scripts/`
    - Read all existing DayHistory records from Azure Table Storage
    - Parse English description text using regex patterns to determine TranslationKey and extract parameters
    - Include chef status patterns: `"{name}'s chef status enabled."` → `history_chef_status_changed` with `enabled="true"`, `"{name}'s chef status disabled."` → `history_chef_status_changed` with `enabled="false"`
    - Write `TranslationKey` and `Parameters` fields back to each entity, clear old `Description` field
    - Skip already-migrated records (idempotency)
    - Log each converted record to console
    - Log warning and skip unparseable records
    - Handle all 117 existing records across all households
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

  - [x] 8.2 Write integration tests for migration script
    - Seed DayHistory records covering all four description patterns in Azurite
    - Run migration and verify correct TranslationKey + Parameters extraction
    - Verify idempotency by running twice and asserting identical results
    - _Requirements: 9.2, 9.3, 9.5_

- [x] 9. Property-based tests
  - [x] 9.1 Write property test: Write path produces valid structured entries (Property 1)
    - **Property 1: Write path produces valid structured entries**
    - Generate random non-empty names, AttendanceStatus values, dish descriptions (1–100 chars), comment texts (1–200 chars), and chef status booleans
    - Call handler write methods with mocked repositories
    - Verify stored TranslationKey ∈ known history keys and Parameters JSON contains exactly the expected placeholder keys
    - **Validates: Requirements 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10**

  - [x] 9.2 Write property test: Resolution produces fully-substituted strings (Property 2)
    - **Property 2: Resolution produces fully-substituted strings**
    - Generate random combinations of (known key, matching parameters dict with non-empty values, locale)
    - Call `SharedStringResolver.Resolve`
    - Assert no `{...}` tokens remain in output and result is non-empty
    - **Validates: Requirements 2.3, 2.4**

  - [x] 9.3 Write property test: AttendanceStatus values resolve to localized display names (Property 3)
    - **Property 3: AttendanceStatus values resolve to localized display names**
    - Generate random AttendanceStatus values × Locale
    - Build parameters dict with `name` = random string and `status` = enum value name
    - Resolve `history_attendance_set`
    - Assert output contains the expected localized display name, not the raw enum name
    - **Validates: Requirements 2.5, 8.2, 8.3**

  - [x] 9.4 Write property test: Unknown translation keys fall back gracefully (Property 4)
    - **Property 4: Unknown translation keys fall back gracefully**
    - Generate random non-empty strings filtered to exclude all known keys
    - Call `SharedStringResolver.Resolve`
    - Assert result equals the input key
    - **Validates: Requirements 2.6, 7.5**

  - [x] 9.5 Write property test: Unknown status values pass through unchanged (Property 5)
    - **Property 5: Unknown status values pass through unchanged**
    - Generate random non-empty strings filtered to exclude `"Unknown"`, `"EatingIn"`, `"NotEatingIn"`
    - Build parameters for `history_attendance_set` with that string as `status`
    - Resolve and assert the raw string appears verbatim in output
    - **Validates: Requirements 8.4**

  - [x] 9.6 Write property test: Nudge messages resolve with locale-formatted dates (Property 6)
    - **Property 6: Nudge messages resolve with locale-formatted dates**
    - Generate random DateOnly values (within reasonable range) × Locale
    - Map `PleaseAddAttendance` to `nudge_please_add_attendance`
    - Build parameters with formatted date, resolve
    - Assert output contains the locale-formatted date string
    - **Validates: Requirements 5.2, 5.3**

  - [x] 9.7 Write property test: Migration parsing round-trip (Property 7)
    - **Property 7: Migration parsing round-trip**
    - Generate random names (letters only, no apostrophe), AttendanceStatus values, dish descriptions (no quotes), comment texts (no quotes), and chef status booleans
    - Render old-format English string, parse with migration regex
    - Assert extracted key and parameters match originals
    - **Validates: Requirements 9.2, 9.3**

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Update steering documents with resource file conventions
  - [x] 11.1 Update `.kiro/steering/project-context.md` and/or `.kiro/steering/coding-conventions.md`
    - Document the new resource file convention:
      - History/nudge/status translations → `Happie.Shared/Resources/SharedStrings.resx` + `SharedStrings.en.resx`
      - UI-only strings (labels, headings, button text) → `Happie.Web/Resources/AppStrings.resx` + `AppStrings.en.resx`
      - Never add `history_*`, `nudge_*`, or `status_*` keys to `AppStrings.resx`
    - Document the `SharedStringResolver` usage pattern for both frontend and backend
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# throughout — all implementation uses .NET 10 / C#
- `SharedStringResolver` is registered as a singleton in both `Happie.Web` and `Happie.Api`
- The migration script follows the same `dotnet-script` pattern as `seed-local.csx`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "2.2", "2.4"] },
    { "id": 2, "tasks": ["1.3", "2.3"] },
    { "id": 3, "tasks": ["4.1", "4.2"] },
    { "id": 4, "tasks": ["4.3", "5.1", "7.2"] },
    { "id": 5, "tasks": ["5.2", "7.1", "7.3"] },
    { "id": 6, "tasks": ["5.3", "5.4"] },
    { "id": 7, "tasks": ["8.1"] },
    { "id": 8, "tasks": ["8.2", "9.1", "9.2", "9.3", "9.4", "9.5", "9.6", "9.7"] },
    { "id": 9, "tasks": ["11.1"] }
  ]
}
```
