# Implementation Plan: Login Page Redesign

## Overview

Implement the login page redesign for the Happie Blazor WebAssembly PWA. The work is structured in three incremental steps: add the new layout, rewrite the login page markup and logic, and wire in localization and tests. Each step builds on the previous one and leaves no orphaned code.

Note: `LoginHandler` already filters deleted housemates server-side before building the `LoginResponse`. `HousemateDto` has no `IsDeleted` field and no client-side filtering is needed.

## Tasks

- [x] 1. Add localization keys to both resx files
  - [x] 1.1 Add `Login_WelcomeHeading` and `Login_Subtitle` keys; update `Login_PasswordPlaceholder`
    - Open `Happie.Web/Resources/AppStrings.resx` (neutral/Dutch fallback)
    - Add `Login_WelcomeHeading` → `Welkom bij Happie`
    - Add `Login_Subtitle` → `Coördineer de maaltijden van je huishouden`
    - Update `Login_PasswordPlaceholder` → `Huishoudwachtwoord`
    - Open `Happie.Web/Resources/AppStrings.en.resx`
    - Add `Login_WelcomeHeading` → `Welcome to Happie`
    - Add `Login_Subtitle` → `Coordinate your household meals`
    - Update `Login_PasswordPlaceholder` → `Household Password`
    - _Requirements: 7.1, 7.2, 7.3_

- [x] 2. Create `LoginLayout.razor` and its scoped CSS
  - [x] 2.1 Create `Happie.Web/Layout/LoginLayout.razor`
    - Inherit `LayoutComponentBase`
    - Render a single `<div class="login-page">@Body</div>` — no sidebar, no top bar
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 2.2 Create `Happie.Web/Layout/LoginLayout.razor.css`
    - `.login-page`: `min-height: 100vh; background-color: #E8EEF4`
    - Media query `(min-width: 641px)`: add `display: flex; align-items: center; justify-content: center`
    - _Requirements: 1.4, 1.5, 1.6_

  - [x]* 2.3 Write bUnit component test for `LoginLayout`
    - Render `LoginLayout` with a stub body fragment
    - Assert no element matching a sidebar selector (`.sidebar`, `nav`) is present
    - Assert no element matching a top-bar selector (`.top-bar`, `.navbar`) is present
    - Assert the root element carries the `login-page` CSS class
    - _Requirements: 1.1, 1.2_

- [x] 3. Rewrite `LoginPage.razor` markup, code-behind, and scoped CSS
  - [x] 3.1 Update `LoginPage.razor` — layout directive and card structure
    - Add `@layout LoginLayout` at the top of the file
    - Inject `LocaleService` via `[Inject]`
    - Wrap all content in `<div class="login-card">`
    - Add locale toggle at the top-right of the card: two `<button>` elements (`EN` / `NL`) calling `SwitchLocaleAsync`
    - Add logo `<div class="login-logo">H</div>`
    - Add `<h1>@Localizer["Login_WelcomeHeading"]</h1>` and `<p class="login-subtitle">@Localizer["Login_Subtitle"]</p>`
    - _Requirements: 1.3, 2.1, 2.2, 2.6, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 9.1, 9.2_

  - [x] 3.2 Update `LoginPage.razor` — password form view
    - Replace the existing bare form with an `<EditForm>` containing a `<div class="password-field">` wrapper
    - Add lock icon SVG inside the wrapper, left-aligned
    - Use `<InputText type="password" placeholder="@Localizer["Login_PasswordPlaceholder"]" />`
    - Render `<p role="alert">` for `_showError`
    - Render `<button type="submit" class="login-btn" disabled="@_isSubmitting">@Localizer["Login_SubmitButton"]</button>`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.7, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 3.3 Update `LoginPage.razor` — housemate selection view
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

  - [x] 3.4 Update `LoginPage.razor` — `SwitchLocaleAsync` and housemate selection code-behind
    - `SwitchLocaleAsync` calls `LocaleService.SetLocaleAsync(locale)` then `NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true)` — a full reload is required because Blazor WASM's ResourceManager cannot switch satellite assemblies mid-session
    - Housemates are persisted to `sessionStorage` after login so they survive the locale-switch reload; `OnInitializedAsync` restores them from `sessionStorage` when JWT exists but no `activeHousemateId` is set
    - `SelectHousemateAsync` persists `activeHousemateId`, cleans up `sessionStorage`, and navigates to today's day plan
    - `OnInitializedAsync` only auto-redirects to `/day/{today}` when both `jwt` AND `activeHousemateId` are present in `localStorage`
    - _Requirements: 8.8, 9.3, 9.4_

  - [x] 3.5 Create `Happie.Web/Pages/LoginPage.razor.css`
    - `.login-card`: white background, `border-radius: 12px`, `padding: 16px`, `width: 100%`, `max-width: 400px` on desktop
    - `.login-logo`: `56×56px`, `background-color: #4CAF50`, `border-radius: 8px`, white `H` centered, `font-size: 2rem; font-weight: bold`, `margin: 0 auto` (centered)
    - `.login-locale-toggle`: flex row, positioned top-right of card
    - `.locale-btn`: small text button with hover effect (`color: #4CAF50; background-color: rgba(76, 175, 80, 0.08)`); `.locale-btn--active`: `font-weight: bold; color: #4CAF50`
    - `.password-field`: flex row, pill-shaped (`border-radius: 24px`), `background-color: #f3f4f6`, subtle grey border; `:focus-within` border turns `#4CAF50`
    - `.password-field ::deep input`: flex-grow 1, `border: 0; outline: 0; background: transparent` — uses `::deep` to penetrate Blazor's `<InputText>` component
    - `.login-btn`: full width, `background-color: #4CAF50`, white bold text, `border-radius: 4px`; `:disabled`: `background-color: #A5D6A7; opacity: 0.5; cursor: not-allowed`
    - `.housemate-list`: no list-style, no padding
    - `.housemate-row`: full-width button, flex row, white background, rounded corners, subtle border, `gap: 12px`
    - `.housemate-row:hover`: `outline: 2px solid #4CAF50; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1)` — uses brand green (not housemate color) with a drop shadow
    - `.housemate-avatar`: `36×36px`, `border-radius: 8px`, white text centered, `font-weight: bold`
    - `.housemate-name`: `font-weight: bold`, flex-grow 1
    - `h1`, `.login-subtitle`: `text-align: center`
    - `LoginLayout.razor.css`: added `padding: 16px` and `min-height: 100dvh` so the card has breathing room on mobile without white bleed
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.2, 5.3, 5.5, 5.6, 6.2, 6.3, 6.4, 6.6, 8.4, 8.5, 8.6, 8.7, 9.5, 9.6_

- [x] 4. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Write bUnit component tests for `LoginPage`
  - [x] 5.1 Write bUnit tests for the password form view
    - Add `Happie.Web.Tests/Pages/LoginPageTests.cs`
    - Add bUnit (`bunit`) package reference to `Happie.Web.Tests.csproj`
    - Add `ProjectReference` to `Happie.Web` in `Happie.Web.Tests.csproj`
    - Test: render `LoginPage` in password form state; assert logo present, `h1` with `Login_WelcomeHeading`, `p` with `Login_Subtitle`, `input[type=password]`, lock icon, submit button with `Login_SubmitButton` label, locale toggle with EN and NL buttons
    - Test: simulate failed login; assert `role="alert"` error message is shown
    - Test: simulate `_isSubmitting = true`; assert submit button has `disabled` attribute
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 5.7, 6.1, 6.5, 6.6, 9.1, 9.2_

  - [x] 5.2 Write bUnit tests for the housemate selection view, locale toggle, and session logic
    - Test: simulate successful login; assert housemate selection view is shown with `Login_SelectHousemate` heading
    - Test: simulate successful login with empty housemate list; assert error message is shown (not the selection view)
    - Test: simulate housemate row tap; assert `localStorage.setItem` is called with the correct housemate ID, `sessionStorage.removeItem` is called for `pendingHousemates`, and navigation to `/day/{today}` occurs
    - Test: simulate locale toggle click; assert `LocaleService.SetLocaleAsync` is called and `NavigationManager.NavigateTo` is called with `forceLoad: true`
    - Test: render with JWT in localStorage but no `activeHousemateId`, with housemates in `sessionStorage`; assert housemate selection view is shown (not password form, not redirect)
    - Test: render with both JWT and `activeHousemateId` in localStorage; assert redirect to `/day/{today}` occurs
    - Test: render with no JWT in localStorage; assert password form is shown (no redirect)
    - _Requirements: 8.1, 8.2, 8.8, 8.10, 9.3, 9.4_

- [x] 6. Write FsCheck property-based tests for housemate list logic
  - [x] 6.1 Write property test for avatar color and first character (Property 1)
    - Add `Happie.Web.Tests/Pages/LoginPagePropertyTests.cs`
    - Generate random `HousemateDto` values with non-empty names and valid color strings using `FsCheck.Fluent`
    - Render the housemate selection view via bUnit
    - Assert each `.housemate-avatar` element's `style` attribute contains `housemate.Color` and its text content equals `housemate.Name[0].ToString()`
    - Tag: `// Feature: login-page-redesign, Property 1: Avatar reflects housemate color and first character`
    - _Requirements: 8.5_

  - [x] 6.2 Write property test for alphabetical sort order (Property 2)
    - Generate random `List<HousemateDto>` with varying names
    - Render the housemate selection view via bUnit
    - Assert the order of `.housemate-name` text contents matches `housemates.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)`
    - Tag: `// Feature: login-page-redesign, Property 2: Housemate rows are sorted alphabetically, case-insensitively`
    - _Requirements: 8.9_

  - [x] 6.3 Write property test for hover outline uses brand green (Property 3)
    - Generate random `HousemateDto` values with valid color strings
    - Render the housemate selection view via bUnit
    - Assert each `.housemate-row` element exists and the scoped CSS rule `.housemate-row:hover` uses `#4CAF50` (brand green) — since this is a CSS-only rule, the bUnit test verifies the row renders correctly; visual hover behavior is verified manually
    - Tag: `// Feature: login-page-redesign, Property 3: Hover state uses brand green for all housemates`
    - _Note: Original requirement 10.1/10.2 specified housemate color; changed to brand green per user feedback_

- [x] 7. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (Properties 1–3 from the design document)
- Unit/component tests validate specific examples and edge cases
- bUnit must be added to `Happie.Web.Tests.csproj` and `Happie.Web` must be added as a `ProjectReference` before component tests can run
- The hover outline uses brand green (`#4CAF50`) for all housemate rows with a drop shadow — changed from the original `--housemate-color` approach per user feedback

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
