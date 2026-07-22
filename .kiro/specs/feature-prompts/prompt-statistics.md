# Feature Prompt: Statistics

## Summary

The Statistics feature provides analytics on saved dish usage within a household, showing per-dish frequency counts (how often each dish has been used in day plans) and per-housemate cooking attribution (which housemate last set each dish, based on `DishRecord.LastChangedByHousemateId`). Statistics are computed over a configurable time range, giving housemates insight into meal variety, cooking distribution, and dish popularity. This helps households balance cooking responsibilities and discover which dishes they rely on most.

## User Story

As a housemate, I want to see how often each saved dish has been used and who has been cooking which dishes, so that our household can ensure fair cooking distribution and identify favorite or neglected meals.

## Key Behaviors to Specify

1. **Per-dish usage frequency** — For each saved dish (active and optionally soft-deleted), compute how many DishRecords reference it via `SavedDishId` within the selected time range. Display as a count or frequency (e.g., "Used 12 times in the last 3 months"). Consider whether to include custom dishes that match the saved dish description (pre-retroactive-conversion data).
2. **Per-housemate cooking attribution** — For each DishRecord within the time range, attribute the dish to the housemate identified by `DishRecord.LastChangedByHousemateId`. Display a breakdown per housemate showing how many times they set the dish. This answers "who cooks what and how often."
3. **Time range** — Define the selectable time ranges (e.g., last 7 days, last 30 days, last 3 months, last year, all time). Determine the default range. Statistics are recomputed when the range changes.
4. **Computation approach** — Statistics can be computed on-the-fly by scanning DishRecords (acceptable for small households) or pre-aggregated. Define the approach given Azure Table Storage's query capabilities and the expected data volume.
5. **Display** — Define where statistics are shown: a dedicated Statistics page, a section on the SavedDishesPage, or per-dish detail view. Consider charts, ranked lists, or simple tables. Define the mobile-friendly presentation.
6. **Soft-deleted dishes** — Determine whether soft-deleted dishes appear in statistics (they may still have historical usage data). If shown, mark them as deleted in the UI.
7. **No-data state** — When no DishRecords exist in the selected time range, display an appropriate empty state message.

## Affected Components and Data Models

| Component | Impact |
|---|---|
| `DishRecord` domain type | Already has `SavedDishId` and `LastChangedByHousemateId` — used as data source |
| `IDishRepository` / `DishRepository` | May need a date-range query method (e.g., `GetByDateRangeAsync(householdId, from, to)`) |
| `ISavedDishHandler` / `SavedDishHandler` or new `IStatisticsHandler` | Computation logic for frequency counts and attribution |
| `SavedDishesFunction` or new `StatisticsFunction` | New endpoint(s): `GET /api/statistics/dishes?from=...&to=...` |
| New: `DishStatisticsDto` | Response contract with per-dish frequency and per-housemate breakdown |
| New: `StatisticsPage` or section on `SavedDishesPage` | UI for displaying statistics with time range selector |
| `SavedDishDto` | May need to include usage count for inline display on SavedDishesPage |
| `Housemate` / `HousemateDto` | Referenced for attribution display (name, color) |
| `AppStrings.resx` / `AppStrings.en.resx` | Localization keys for statistics labels, time range options, empty states |
| `NavMenu` | If a separate Statistics page is added, a new navigation entry is needed |
