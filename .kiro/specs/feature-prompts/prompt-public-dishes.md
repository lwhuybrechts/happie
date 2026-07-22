# Feature Prompt: Public Dishes

## Summary

The Public Dishes feature allows a housemate to toggle a Saved_Dish from private (household-only) to public, making it visible as a suggestion in other households. When a housemate in another household copies a public dish, the system creates an independent household-local Saved_Dish that is no longer linked to the original. This enables cross-household dish discovery without creating dependencies between households — each copy is fully owned by the receiving household and can be renamed, deleted, or referenced in day plans independently.

## User Story

As a housemate, I want to share my saved dishes publicly so that other households can discover and copy them, and I want to copy public dishes from other households into my own collection so I can reuse good meal ideas without typing them manually.

## Key Behaviors to Specify

1. **Toggle-to-public** — A Saved_Dish gains a boolean `IsPublic` flag (default false). A housemate can toggle this flag on the SavedDishesPage. Only active (non-deleted) dishes can be made public. Soft-deleting a public dish should either automatically unpublish it or keep it visible until explicitly unpublished.
2. **Public dish visibility** — Public dishes appear in a cross-household suggestions section (separate from the existing household-local suggestions). The suggestions should show the dish description and optionally the originating household name. Public dishes from the housemate's own household are excluded from cross-household suggestions.
3. **Copy semantics** — Copying a public dish creates a new Saved_Dish in the receiving household with the same description. The copy has its own unique ID and no reference back to the original. Changes to the original do not propagate to copies. If the receiving household already has a Saved_Dish with the same description (case-insensitive), the copy should be rejected or the existing dish highlighted.
4. **Discovery and pagination** — Define how public dishes are queried across all households (separate table? cross-partition query?). Consider pagination or search if the number of public dishes grows large.
5. **Privacy and moderation** — Determine whether household names are visible to other households. Consider abuse scenarios (inappropriate dish names) and whether any moderation is needed.
6. **Unpublishing** — When a public dish is unpublished (toggled back to private), it should no longer appear in other households' suggestions. Existing copies are unaffected.

## Affected Components and Data Models

| Component | Impact |
|---|---|
| `SavedDish` domain record | Add `IsPublic` boolean field |
| `SavedDishEntity` | Add `IsPublic` property |
| `SavedDishMapper` | Map new field |
| `ISavedDishRepository` / `SavedDishRepository` | Add query for public dishes (possibly cross-partition) |
| `ISavedDishHandler` / `SavedDishHandler` | Add toggle-public logic, copy logic, public suggestions query |
| `SavedDishesFunction` | New endpoint(s): `PATCH /api/saved-dishes/{id}/publish`, `GET /api/saved-dishes/public`, `POST /api/saved-dishes/copy` |
| `SavedDishesPage` | Add publish/unpublish toggle per dish, public dishes discovery section |
| `SavedDishDto` | Add `IsPublic` field |
| Azure Table Storage | Consider a separate `PublicDishes` table or index for cross-household queries |
| `Happie.Shared/Contracts/` | New request/response types for public dish operations |
| `AppStrings.resx` / `AppStrings.en.resx` | Localization keys for public dish UI elements |
