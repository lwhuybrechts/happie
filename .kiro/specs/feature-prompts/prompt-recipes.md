# Feature Prompt: Recipes

## Summary

The Recipes feature extends the Saved_Dish model with an optional ingredients list and free-text cooking instructions, turning saved dishes into lightweight recipe cards. Housemates can add structured recipe data to any saved dish, which is then displayed on the DayPlan page when that dish is referenced via `SavedDishId`. This helps housemates know what ingredients are needed and how to prepare the meal without leaving the app or consulting external recipe sources.

## User Story

As a housemate, I want to add an ingredients list and cooking instructions to a saved dish, so that whoever is cooking can see what they need and how to prepare the meal directly on the day plan page.

## Key Behaviors to Specify

1. **Ingredients list** — A Saved_Dish gains an optional ordered list of ingredient strings. Define maximum number of ingredients (e.g., 30) and maximum length per ingredient (e.g., 100 characters). Ingredients can be added, removed, and reordered.
2. **Cooking instructions** — A Saved_Dish gains an optional free-text cooking instructions field. Define a maximum length (e.g., 2000 characters). This is a single multi-line text block, not structured steps.
3. **Field lengths and validation** — Define and enforce maximum lengths for ingredients (per item and total count) and instructions on both client and server. Empty ingredients or whitespace-only entries should be rejected.
4. **DayPlan display** — When a DayPlan references a saved dish that has recipe data, the DishPanel should offer an expandable section or link to view ingredients and instructions. Define the UI pattern (inline expand, modal, or separate page). Consider mobile screen constraints.
5. **Editing** — Recipe data is edited on the SavedDishesPage (not from the DayPlan page). Define the edit UI: inline on the dish detail, a separate modal, or a dedicated recipe edit page.
6. **Empty state** — When a saved dish has no recipe data, no recipe section is shown on the DayPlan page. The SavedDishesPage should indicate that recipe data can be added (e.g., "Add recipe" button).
7. **Backward compatibility** — Existing saved dishes without recipe data continue to work identically. Recipe fields are optional and default to empty/null.

## Affected Components and Data Models

| Component | Impact |
|---|---|
| `SavedDish` domain record | Add optional `Ingredients` (list of strings) and `Instructions` (string) fields |
| `SavedDishEntity` | Add `Ingredients` (serialized JSON string) and `Instructions` (string) properties. Consider Azure Table Storage's 64KB property size limit for instructions. |
| `SavedDishMapper` | Map new fields, deserialize ingredients JSON |
| `ISavedDishRepository` / `SavedDishRepository` | No interface change needed (fields are part of the entity) |
| `SavedDishHandler` | Validate ingredients count/length and instructions length on create/update |
| `SavedDishesFunction` | Extend POST/PUT request bodies to accept recipe data |
| `SavedDishDto` | Add optional `Ingredients` and `Instructions` fields |
| `CreateSavedDishRequest` / `UpdateSavedDishRequest` | Add optional recipe fields with validation attributes |
| `DishPanel` | Add expandable recipe section when saved dish has recipe data |
| `SavedDishesPage` | Add recipe editing UI (ingredients list editor, instructions textarea) |
| `DayHandler.GetDayPlanAsync` | Include recipe data when resolving saved dish reference (or expose via separate endpoint) |
| `DishDto` or new `RecipeDto` | Include recipe data in DayPlan response (or separate lazy-load endpoint) |
| `AppStrings.resx` / `AppStrings.en.resx` | Localization keys for recipe UI labels, placeholders, validation messages |
