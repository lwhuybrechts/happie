---
inclusion: fileMatch
fileMatchPattern: "**/*.razor,**/*.razor.css,**/Happie.Web/**"
---

# Happie — Loading Strategy

This document defines when each loading indicator type applies. Every data-fetching scenario maps to exactly one category.

## Header_Spinner

**When:** Background refresh behind already-displayed cached data (stale-while-revalidate).

The header spinner is rendered by the `LoadingIndicator` component in the layout. Drive it via the existing `LoadingIndicatorState` service:

```csharp
await LoadingIndicatorState.IncrementAsync();
try
{
    // ... fetch fresh data behind cached content ...
}
finally
{
    await LoadingIndicatorState.DecrementAsync();
}
```

No additional UI in the page — the header spinner appears automatically.

**Used by:** DayPlanPage, SavedDishesPage, CalendarPage (background refreshes).

## Inline_Loading_Dots

**When:** Cold fetch of primary page content (no cached data available).

```razor
@if (_isLoading)
{
    <div class="loading-delayed">
        <LoadingDots />
    </div>
}
```

The `.loading-delayed` class applies a 200ms fade-in delay so cache-fast responses never flash the dots.

**Used by:** DayPlanPage (cold fetch), SavedDishesPage (cold fetch), HousematesPage.

## Panel_Loading_Indicator

**When:** A sub-panel within an already-rendered parent page independently fetches its own data for the first time (cold fetch).

Panel_Loading_Indicator takes precedence over Inline_Loading_Dots when a sub-panel performs a cold fetch.

```razor
<div class="summary-panel__body">
    @if (_isLoading)
    {
        <div class="loading-delayed summary-panel__loading">
            <LoadingDots />
        </div>
    }
    else if (IsAllEmpty)
    {
        <p class="summary-panel__empty">@Localizer["DishDetails_SummaryEmpty"]</p>
    }
    else
    {
        @* ... content ... *@
    }
</div>
```

Each sub-panel tracks its own `_isLoading` boolean:

```csharp
private bool _isLoading = true;

private async Task LoadDataAsync()
{
    _isLoading = true;
    try
    {
        // ... fetch ...
    }
    finally
    {
        _isLoading = false;
    }
}
```

Hide the edit button while loading:

```razor
@if (!_isLoading)
{
    <button class="summary-panel__icon-btn" @onclick="StartEdit">...</button>
}
```

**Used by:** SummaryPanel, IngredientsPanel, InstructionsPanel.

## Stale_Overlay

**When:** Existing content is visible and needs to remain visible while a background operation runs. This applies to:
- **Filter/time range changes** — stale data from the previous selection is shown dimmed
- **Save operations** — the user's edited or displayed content is dimmed while writing to the backend

Existing content is shown at reduced opacity with interaction disabled:

```css
.container--saving {
    opacity: 0.4;
    pointer-events: none;
}
```

Remove the overlay when the operation completes or fails.

### Filter/time range changes

Used on stats pages with a centered spinner overlay on top of dimmed content:

```razor
<div class="stats-container @(_isRefreshing ? "stats-container--loading" : "")">
    @* existing content shown at reduced opacity *@
</div>
@if (_isRefreshing)
{
    <div class="stats-container__overlay">
        <LoadingIndicator />
    </div>
}
```

**Used by:** HousemateStatsPage, DishStatsPage.

### Save operations

When saving data, dim the content area and disable interaction. On failure, remove the overlay and show an error:

```csharp
private bool _isSaving;

private async Task ConfirmAsync()
{
    _isSaving = true;

    try
    {
        var response = await Http.PutAsJsonAsync(...);
        if (response.IsSuccessStatusCode)
        {
            _isEditing = false;
            _isSaving = false;
            // Reload fresh data.
            await LoadDataAsync();
        }
        else
        {
            _isSaving = false;
            _showError = true;
        }
    }
    catch
    {
        _isSaving = false;
        _showError = true;
    }
}
```

```razor
<div class="panel__body @(_isSaving ? "panel__body--saving" : "")">
    @* edit form content shown dimmed while saving *@
</div>
```

**Note:** This applies to components that write directly to the backend without caching. When a page uses caching (e.g., DayPlanPage), the cache is updated optimistically on save so no overlay is needed — use Header_Spinner instead to indicate the background sync.

**Used by:** SummaryPanel, IngredientsPanel, InstructionsPanel, HousematesPage (rename/color), DishDetailsPage (rename).

## Empty State vs Loading State

Empty state messages are reserved exclusively for scenarios where data has loaded successfully and is genuinely empty.

✅ **CORRECT** — Show empty state only AFTER fetch completes with empty data:

```razor
@if (_isLoading)
{
    <div class="loading-delayed">
        <LoadingDots />
    </div>
}
else if (!_items.Any())
{
    <p class="panel__empty">@Localizer["NoItemsYet"]</p>
}
else
{
    @* render items *@
}
```

❌ **INCORRECT** — Show empty state while data is still being fetched:

```razor
@if (!_items.Any())
{
    @* This shows "no data" immediately on load, before the fetch even completes! *@
    <p class="panel__empty">@Localizer["NoItemsYet"]</p>
}
```

This confuses users into thinking there is no data when it simply has not loaded yet.

## Component Reference

| Component / Class | Category | Role |
|---|---|---|
| `LoadingIndicator` | Header_Spinner | Spinning circle in the mobile header/sidebar; driven by `LoadingIndicatorState` |
| `LoadingIndicatorState` | Header_Spinner | Injectable service with `IncrementAsync()`/`DecrementAsync()` and 500ms minimum visibility |
| `LoadingDots` | Inline_Loading_Dots, Panel_Loading_Indicator | Three bouncing dots; parameterless; positioning handled by parent container |
| `.loading-delayed` | Inline_Loading_Dots, Panel_Loading_Indicator | CSS class with 200ms delayed opacity fade-in; wrap around `<LoadingDots />` |
