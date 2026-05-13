# Implementation Plan: Login Page Redesign

## Overview

Implement the login page redesign for the Happie Blazor WebAssembly PWA. The work is structured in three incremental steps: add the new layout, rewrite the login page markup and logic, and wire in localization and tests. Each step builds on the previous one and leaves no orphaned code.

Note: `LoginHandler` already filters deleted housemates server-side before building the `LoginResponse`. `HousemateDto` has no `IsDeleted` field and no client-side filtering is needed.

## Tasks

- [ ] 1. Add localization keys to both resx files
  - [ ] 1.1 Add `Login_WelcomeHeading` and `Login_Subtitle` keys; update `Login_PasswordPlaceholder`
    - Open `Happie.Web/Resources/AppStrings.resx` (neutral/Dutch fallback)
    - Add `Login_WelcomeHeading` → `Welkom bij Happie`
    - Add `Login_Subtitle` → `Coördineer de maaltijden van je huishouden`
    - Update `Login_PasswordPlaceholder` → `Huishoudwachtwoord`
    - Open `Happie.Web/Resources/AppStrings.en.resx`
    - Add `Login_WelcomeHeading` → `Welcome to Happie`
    - Add `Login_Subtitle` → `Coordinate your household meals`
    - Update `Login_PasswordPlaceholder` → `Household Password`
    - _Requirements: 7.1, 7.2, 7.3_

- [ ] 2. Create `LoginLayout.razor` and its scoped CSS
  - [ ] 2.1 Create `Happie.Web/Layout/LoginLayout.razor`
    - Inherit `LayoutComponentBase`
    - Render a single `<div class="login-page">@Body</div>` — no sidebar, no top bar
    - _Requirements: 1.1, 1.2, 1.3_

  - [ ] 2.2 Create `Happie.Web/Layout/LoginLayout.razor.css`
    - `.login-page`: `min-height: 100vh; background-color: #E8EEF4`
    - Media query `(min-width: 641px)`: add `display: flex; align-items: center; justify-content: center`
    - _Requirements: 1.4, 1.5, 1.6_

  - [ ]* 2.3 Write bUnit component test for `LoginLayout`
    - Render `LoginLayout` with a stub body fragment
    - Assert no element matching a sidebar selector (`.sidebar`, `nav`) is present
    - Assert no element matching a top-bar selector (`.top-bar`, `.navbar`) is present
    - Assert the root element carries the `login-page` CSS class
    - _Requirements: 1.1, 1.2_

- [ ] 3. Rewrite `LoginPage.razor` markup, code-behind, and scoped CSS
  - [ ] 3.1 Update `LoginPage.razor` — layout directive and card structure
    - Add `@layout LoginLayout` at the top of the file
    - Inject `LocaleService` via `[Inject]`
    - Wrap all content in `<div class="login-card">`
    - Add locale toggle at the top-right of the card: two `<button>` elements (`EN` / `NL`) calling `SwitchLocaleAsync`
    - Add logo `<div class="login-logo">H</div>`
    - Add `<h1>@Localizer["Login_WelcomeHeading"]</h1>` and `<p class="login-subtitle">@Localizer["Login_Subtitle"]</p>`
    - _Requirements: 1.3, 2.1, 2.2, 2.6, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 9.1, 9.2_

  - [ ] 3.2 Update `LoginPage.razor` — password form view
    - Replace the existing bare form with an `<EditForm>` containing a `<div class="password-field">` wrapper
    - Add lock icon SVG inside the wrapper, left-aligned
    - Use `<InputText type="password" placeholder="@Localizer["Login_PasswordPlaceholder"]" />`
    - Render `<p role="alert">` for `_showError`
    - Render `<button type="submit" class="login-btn" disabled="@_isSubmitting">@Localizer["Login_SubmitButton"]</button>`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.7, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [ ] 3.3 Update `LoginPage.razor` — housemate selection view
    - Replace the bare `<ul>` with `<ul class="housemate-list">`
    - Sort on assignment: `_housemates = loginResponse.Housemates.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList()`
    - Render `<h2>@Localizer["Login_SelectHousemate"]</h2>` above the list
    - Render empty-state `<p>` when the housemate list is empty
    - Each `<li>` contains a `<button class="housemate-row" style="--housemate-color: @housemate.Color">` with:
      - `<span class="housemate-avatar" style="background-color: @housemate.Color">@housemate.Name[0]</span>`
      - `<span class="housemate-name">@housemate.Name</span>`
      - Right-pointing chevron SVG
    - Treat null or empty housemate list after a successful HTTP response as an error (`_showError = true`)
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.9, 8.10_

  - [ ] 3.4 Update `LoginPage.razor` — `SwitchLocaleAsync` and housemate selection code-behind
    - Add `private async Task SwitchLocaleAsync(Locale locale)` that calls `LocaleService.SetLocaleAsync(locale)` then `StateHasChanged()` — no `NavigationManager.NavigateTo` with `forceLoad: true`
    - Update `SelectHousemateAsync` to persist `activeHousemateId` and navigate to today's day plan (existing logic is correct; verify it is unchanged)
    - _Requirements: 8.8, 9.3, 9.4_

  - [ ] 3.5 Create `Happie.Web/Pages/LoginPage.razor.css`
    - `.login-card`: white background, `border-radius: 12px`, `padding: 16px`, `width: 100%`, `max-width: 400px` on desktop
    - `.login-logo`: `56×56px`, `background-color: #4CAF50`, `border-radius: 8px`, white `H` centered, `font-size: 2rem; font-weight: bold`
    - `.login-locale-toggle`: flex row, positioned top-right of card
    - `.locale-btn`: small text button; `.locale-btn--active`: `font-weight: bold; color: #4CAF50`
    - `.password-field`: flex row, `border-radius: 8px`; `:focus-within` border `#4CAF50`
    - `.password-field input`: flex-grow 1, no border, no outline
    - `.login-btn`: full width, `background-color: #4CAF50`, white bold text, `border-radius: 4px`; `:disabled`: `background-color: #A5D6A7; opacity: 0.5; cursor: not-allowed`
    - `.housemate-list`: no list-style, no padding
    - `.housemate-row`: full-width button, flex row, white background, rounded corners, subtle border, `gap: 12px`
    - `.housemate-row:hover`: `outline: 2px solid var(--housemate-color); box-shadow: 0 0 0 4px color-mix(in srgb, var(--housemate-color) 20%, transparent)` — color is driven by the `--housemate-color` CSS custom property set inline on each row
    - `.housemate-avatar`: `36×36px`, `border-radius: 8px`, white text centered, `font-weight: bold`
    - `.housemate-name`: `font-weight: bold`, flex-grow 1
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.2, 5.3, 5.5, 5.6, 6.2, 6.3, 6.4, 6.6, 8.4, 8.5, 8.6, 8.7, 9.5, 9.6, 10.1, 10.2, 10.3_

- [ ] 4. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Write bUnit component tests for `LoginPage`
  - [ ] 5.1 Write bUnit tests for the password form view
    - Add `Happie.Web.Tests/Pages/LoginPageTests.cs`
    - Add bUnit (`bunit`) package reference to `Happie.Web.Tests.csproj`
    - Add `ProjectReference` to `Happie.Web` in `Happie.Web.Tests.csproj`
    - Test: render `LoginPage` in password form state; assert logo present, `h1` with `Login_WelcomeHeading`, `p` with `Login_Subtitle`, `input[type=password]`, lock icon, submit button with `Login_SubmitButton` label, locale toggle with EN and NL buttons
    - Test: simulate failed login; assert `role="alert"` error message is shown
    - Test: simulate `_isSubmitting = true`; assert submit button has `disabled` attribute
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 5.7, 6.1, 6.5, 6.6, 9.1, 9.2_

  - [ ] 5.2 Write bUnit tests for the housemate selection view and locale toggle
    - Test: simulate successful login; assert housemate selection view is shown with `Login_SelectHousemate` heading
    - Test: simulate housemate row tap; assert `localStorage.setItem` is called with the correct housemate ID and navigation occurs
    - Test: simulate locale toggle click; assert `LocaleService.SetLocaleAsync` is called and no page reload occurs (verify `NavigationManager.NavigateTo` with `forceLoad: true` is NOT called)
    - _Requirements: 8.1, 8.2, 8.8, 9.3, 9.4_

- [ ] 6. Write FsCheck property-based tests for housemate list logic
  - [ ] 6.1 Write property test for avatar color and first character (Property 1)
    - Add `Happie.Web.Tests/Pages/LoginPagePropertyTests.cs`
    - Generate random `HousemateDto` values with non-empty names and valid color strings using `FsCheck.Fluent`
    - Render the housemate selection view via bUnit
    - Assert each `.housemate-avatar` element's `style` attribute contains `housemate.Color` and its text content equals `housemate.Name[0].ToString()`
    - Tag: `// Feature: login-page-redesign, Property 1: Avatar reflects housemate color and first character`
    - _Requirements: 8.5_

  - [ ] 6.2 Write property test for alphabetical sort order (Property 2)
    - Generate random `List<HousemateDto>` with varying names
    - Render the housemate selection view via bUnit
    - Assert the order of `.housemate-name` text contents matches `housemates.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)`
    - Tag: `// Feature: login-page-redesign, Property 2: Housemate rows are sorted alphabetically, case-insensitively`
    - _Requirements: 8.9_

  - [ ] 6.3 Write property test for hover color CSS custom property (Property 3)
    - Generate random `HousemateDto` values with valid color strings
    - Render the housemate selection view via bUnit
    - Assert each `.housemate-row` element's inline `style` attribute contains `--housemate-color: {housemate.Color}`
    - Tag: `// Feature: login-page-redesign, Property 3: Hover state uses the housemate's own color`
    - _Requirements: 10.1, 10.2_

- [ ] 7. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (Properties 1–3 from the design document)
- Unit/component tests validate specific examples and edge cases
- bUnit must be added to `Happie.Web.Tests.csproj` and `Happie.Web` must be added as a `ProjectReference` before component tests can run
- The hover outline and shadow are driven by a `--housemate-color` CSS custom property set inline on each `.housemate-row` button, so the `:hover` rule in the scoped CSS file can reference it without JavaScript

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3", "3.4"] },
    { "id": 4, "tasks": ["3.5"] },
    { "id": 5, "tasks": ["5.1", "5.2"] },
    { "id": 6, "tasks": ["6.1", "6.2", "6.3"] }
  ]
}
```
