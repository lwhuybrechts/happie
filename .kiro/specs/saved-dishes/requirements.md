# Requirements Document

## Introduction

The Saved Dishes feature adds a household-level collection of reusable dishes that can be referenced from day plans. A new dedicated Saved Dishes page allows housemates to manage this collection, with a reveal-on-click add pattern, inline editing, and an always-visible explanation section describing current and future benefits. The DayPlan dish editor gains a toggle between "custom" mode (free-text, current behavior) and "saved" mode (referencing a saved dish by ID), including the ability to promote a custom dish to a saved dish directly from the toggle list. When a saved dish is used in a day plan, the DishRecord stores a reference to the saved dish so that edits to the saved dish description propagate to all day plans that reference it. The data model accommodates future features (public dishes, saved-dish-history, recipes, statistics) without requiring schema changes.

## Glossary

- **Saved_Dish**: A reusable dish record belonging to a household, identified by a unique ID. Contains a description (max 100 characters) and an `IsDeleted` flag for soft-delete.
- **Saved_Dishes_Page**: A dedicated page listing all active saved dishes for the household, including a suggestions section and an always-visible explanation section.
- **Add_Button**: A "+" button on the Saved_Dishes_Page that reveals the input field for adding a new saved dish. The input field is hidden by default and disappears again after a dish is successfully saved.
- **Highlight_Animation**: A brief visual animation applied to a newly added saved dish after it is inserted at its alphabetical position in the list and scrolled into view, so the housemate can see where the dish ended up.
- **Custom_Mode**: The default dish input state on the DayPlan page where the housemate types a free-text description (existing behavior).
- **Saved_Mode**: An alternative dish input state on the DayPlan page where the housemate selects a saved dish from the household collection. The description field displays the saved dish description but is disabled.
- **Mode_Toggle**: A button adjacent to the dish input field that switches between Custom_Mode and Saved_Mode.
- **SavedDishId**: A nullable GUID reference stored on the DishRecord that links a day plan dish to a Saved_Dish.
- **Retroactive_Conversion**: The process of scanning all existing day plan DishRecords for a household and converting custom dishes with a matching description to reference a newly added Saved_Dish.
- **Soft_Delete**: Marking a Saved_Dish as deleted (hidden from listings) while preserving existing day plan references.
- **Suggestions_Section**: A section on the Saved_Dishes_Page showing the last 5 distinct custom dish descriptions from day plans that do not match any active or soft-deleted saved dish.
- **DishRecord**: The existing Azure Table Storage entity storing the dish for a specific day in a household. Extended with an optional SavedDishId reference.
- **Promote_Option**: An option displayed at the top of the saved dishes list in the Mode_Toggle overlay when a custom dish description is currently entered, allowing the housemate to save the custom description as a new Saved_Dish directly from the DayPlan page.
- **Explanation_Section**: A permanently visible section on the Saved_Dishes_Page that explains the purpose and future benefits of saved dishes (reuse, history, sharing, recipes, statistics), displayed regardless of whether any saved dishes exist.

## Requirements

### Requirement 1: Saved Dish Data Model

**User Story:** As a housemate, I want saved dishes to be stored at the household level with a unique ID, so that they can be referenced from day plans and managed independently.

#### Acceptance Criteria

1. THE Saved_Dish SHALL have a unique identifier (GUID), a household ID, a description (1–100 characters after trimming, must not be empty or whitespace-only), and an `IsDeleted` boolean flag defaulting to false.
2. THE Saved_Dish SHALL be stored in Azure Table Storage with PartitionKey `{HouseholdId}` and RowKey `{SavedDishId}`.
3. THE API SHALL enforce that Saved_Dish descriptions are unique within a household across both active and soft-deleted records (case-insensitive comparison after trimming).
4. IF a housemate attempts to add a Saved_Dish with a description that matches an existing soft-deleted Saved_Dish in the same household, THEN THE API SHALL reactivate the soft-deleted record by setting `IsDeleted` to false and updating the description to the submitted value (preserving the caller's casing), rather than creating a new record.
5. IF a housemate attempts to add a Saved_Dish with a description that matches an existing active Saved_Dish in the same household, THEN THE API SHALL return HTTP 409 with error code `DISH_ALREADY_EXISTS`.
6. IF a housemate attempts to add a Saved_Dish with a description that is empty, whitespace-only after trimming, or exceeds 100 characters after trimming, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.

### Requirement 2: DishRecord Reference to Saved Dish

**User Story:** As a housemate, I want day plan dishes to reference saved dishes by ID, so that edits to the saved dish description are reflected everywhere it is used.

#### Acceptance Criteria

1. THE DishRecord SHALL include an optional `SavedDishId` field (nullable GUID) that references a Saved_Dish.
2. WHEN a DishRecord has a non-null `SavedDishId`, THE API SHALL resolve the dish description from the referenced Saved_Dish record when returning the DayPlan response, rather than using the DishRecord's own description field.
3. WHEN a DishRecord has a null `SavedDishId`, THE API SHALL use the DishRecord's own description field (existing behavior).
4. WHEN a Saved_Dish description is updated, THE DayPlan response for any DishRecord referencing that Saved_Dish SHALL reflect the updated description without any write operations to DishRecords.
5. IF a DishRecord references a Saved_Dish that has been soft-deleted, THEN THE API SHALL still resolve the description from the soft-deleted Saved_Dish record (the reference remains valid).
6. IF a DishRecord references a `SavedDishId` that does not exist in the household's Saved_Dish records (neither active nor soft-deleted), THEN THE API SHALL fall back to the DishRecord's own description field and return a null `SavedDishId` in the DayPlan response.
7. WHEN returning a DishRecord in the DayPlan response, THE API SHALL include the `SavedDishId` value (or null) so the frontend can determine whether the dish is in Custom_Mode or Saved_Mode.

### Requirement 3: Saved Dishes Page

**User Story:** As a housemate, I want a dedicated page to view and manage saved dishes, so that I can maintain the household's reusable dish collection.

#### Acceptance Criteria

1. THE Saved_Dishes_Page SHALL be accessible via the navigation menu, positioned after Calendar and before Housemates.
2. THE Saved_Dishes_Page SHALL display all active (non-deleted) saved dishes for the household, sorted alphabetically by description in ascending order (A–Z).
3. THE Saved_Dishes_Page SHALL display an Add_Button that, when activated, reveals an input field for adding a new saved dish with a description of at least 1 and at most 100 characters after trimming.
4. WHILE the add input field is hidden, THE Saved_Dishes_Page SHALL only display the Add_Button (the input field is not visible by default).
5. WHEN the housemate activates the Add_Button, THE Saved_Dishes_Page SHALL reveal the input field with focus so the housemate can immediately type a description.
6. WHEN a saved dish is successfully added, THE Saved_Dishes_Page SHALL hide the input field, insert the new dish in the list at its alphabetical position, scroll the list to the newly added dish, and apply a Highlight_Animation to the new dish so the housemate can see where it ended up.
7. THE Saved_Dishes_Page SHALL provide a delete action on each saved dish that triggers a soft-delete and removes the dish from the displayed list without requiring a page reload.
8. WHEN a housemate activates the edit action on a saved dish, THE Saved_Dishes_Page SHALL replace the dish description text with an inline input field pre-filled with the current description (in-place editing), allowing the housemate to modify the text directly.
9. WHILE the inline edit input field is visible, THE Saved_Dishes_Page SHALL display a confirm button (✓) and a cancel button (✗) adjacent to the input field (matching the pattern used on the Housemates page).
10. WHEN the housemate activates the confirm button (✓), THE Saved_Dishes_Page SHALL persist the updated description and replace the input field with the updated description text.
11. WHEN the housemate activates the cancel button (✗), THE Saved_Dishes_Page SHALL discard the changes and restore the original description text without persisting.
12. WHEN a housemate submits a new or edited Saved_Dish description that conflicts with another existing Saved_Dish in the household (active or soft-deleted, case-insensitive after trimming), THE Saved_Dishes_Page SHALL display a localized error message indicating the description is already in use, and SHALL NOT persist the change.
13. IF the add, edit, or delete operation fails due to a network or server error, THEN THE Saved_Dishes_Page SHALL display a localized error message indicating the failure and SHALL preserve the user's input so they can retry.
14. THE Saved_Dishes_Page SHALL display all user-visible text using localized strings resolved via `IStringLocalizer<AppStrings>`.

### Requirement 4: Saved Dishes Page — Explanation Section

**User Story:** As a housemate, I want to always see an explanation of what saved dishes are for, so that I understand the current and future benefits of maintaining a saved dish collection.

#### Acceptance Criteria

1. THE Saved_Dishes_Page SHALL always display an Explanation_Section regardless of whether the household has any active saved dishes.
2. THE Explanation_Section SHALL explain the current benefit of saved dishes: easy reuse on the DayPlan page.
3. THE Explanation_Section SHALL explain planned future benefits: tracking history of edits, sharing dishes with other households, adding recipes, and viewing statistics.
4. THE Explanation_Section SHALL be positioned below the saved dishes list and the suggestions section (at the bottom of the page content).
5. THE Explanation_Section SHALL use localized strings resolved via `IStringLocalizer<AppStrings>`.
6. WHEN the household has no active saved dishes, THE Saved_Dishes_Page SHALL still display the Add_Button and the Explanation_Section (the page is never completely empty).

### Requirement 5: Saved Dishes Page — Suggestions Section

**User Story:** As a housemate, I want to see recent custom dishes as suggestions, so that I can quickly save dishes I have used before.

#### Acceptance Criteria

1. THE Saved_Dishes_Page SHALL display a suggestions section below the saved dishes list when suggestions are available.
2. WHEN the page loads, THE API SHALL compute up to 5 distinct custom dish descriptions from DishRecords in the household that do not have a `SavedDishId` set AND whose description is not empty AND whose description does not match (case-insensitive, trimmed) any existing Saved_Dish (active or soft-deleted).
3. THE suggestions SHALL be ordered by most recent usage (the most recently used custom dish first).
4. WHEN a housemate taps a suggestion, THE Saved_Dishes_Page SHALL add that description as a new Saved_Dish (triggering Retroactive_Conversion per Requirement 7) and remove the suggestion from the displayed suggestions list.
5. WHEN no suggestions are available (all recent dishes are already saved or no custom dishes exist), THE Saved_Dishes_Page SHALL hide the suggestions section entirely.
6. IF adding a suggestion as a Saved_Dish fails, THEN THE Saved_Dishes_Page SHALL display a localized error message and keep the suggestion visible in the list.

### Requirement 6: Soft-Delete Saved Dish

**User Story:** As a housemate, I want to remove a saved dish from the list without breaking existing day plans, so that old references remain intact.

#### Acceptance Criteria

1. WHEN a housemate deletes a Saved_Dish, THE API SHALL set the `IsDeleted` flag to true on the Saved_Dish record rather than physically deleting it.
2. WHILE a Saved_Dish is soft-deleted, THE Saved_Dishes_Page SHALL hide the dish from the saved dishes list.
3. WHILE a Saved_Dish is soft-deleted, THE Mode_Toggle on the DayPlan page SHALL not include the soft-deleted dish in the selectable list.
4. WHILE a Saved_Dish is soft-deleted, THE API SHALL resolve the description from the soft-deleted Saved_Dish record for any DishRecord that references it via `SavedDishId`, returning the description identically to how it resolves an active Saved_Dish reference.
5. IF a housemate adds a new Saved_Dish with a description matching a soft-deleted record (case-insensitive comparison after trimming), THEN THE API SHALL reactivate the soft-deleted record (set `IsDeleted` to false) rather than creating a new record.
6. WHEN a soft-delete succeeds, THE API SHALL return HTTP 204 with no response body.

### Requirement 7: Retroactive Conversion

**User Story:** As a housemate, I want existing day plan dishes to be linked to a saved dish when one is created with the same description, so that historical data benefits from the reference.

#### Acceptance Criteria

1. WHEN a new Saved_Dish is added (including reactivation of a soft-deleted dish), THE API SHALL scan all DishRecords for the household where `SavedDishId` is null and the description matches the new Saved_Dish description (case-insensitive, trimmed).
2. FOR ALL matching DishRecords found during Retroactive_Conversion, THE API SHALL set the `SavedDishId` to the new Saved_Dish's ID and set the DishRecord description field to an empty string.
3. IF the number of matching DishRecords exceeds a threshold that would cause processing to exceed 5 seconds, THEN THE API SHALL return success immediately after creating the Saved_Dish and complete the remaining conversions in the background.
4. IF Retroactive_Conversion is performed synchronously, THEN THE API SHALL complete all conversions before returning the response to the client.
5. IF Retroactive_Conversion fails for one or more DishRecords, THEN THE API SHALL log the failure but SHALL NOT roll back the Saved_Dish creation or return an error to the client.

### Requirement 8: DayPlan Dish Editor — Mode Toggle

**User Story:** As a housemate, I want to switch between typing a custom dish and picking a saved dish, so that I can choose the most convenient input method.

#### Acceptance Criteria

1. THE DishPanel SHALL display a Mode_Toggle button adjacent to the dish input field on the right side.
2. WHILE the Mode_Toggle is in Custom_Mode, THE DishPanel SHALL show the existing free-text dish input (current behavior, max 100 characters).
3. WHEN the housemate activates the Mode_Toggle from Custom_Mode, THE DishPanel SHALL display a list of all active saved dishes for the household, sorted alphabetically.
4. IF no active saved dishes exist when the Mode_Toggle is activated from Custom_Mode, THEN THE DishPanel SHALL display a localized empty-state message indicating no saved dishes are available.
5. WHEN the housemate selects a saved dish from the list, THE DishPanel SHALL switch to Saved_Mode: the input field displays the saved dish description and is disabled (read-only).
6. WHILE in Saved_Mode, THE DishPanel SHALL visually distinguish the dish field from Custom_Mode (indicating the dish is a saved dish reference rather than free text).
7. WHEN the housemate activates the Mode_Toggle from Saved_Mode to Custom_Mode, THE DishPanel SHALL enable the input field and pre-fill it with the description of the previously selected saved dish.
8. WHEN entering edit mode for a day that has no dish set, THE DishPanel SHALL start in Custom_Mode.
9. WHEN a DishRecord already references a Saved_Dish (non-null `SavedDishId`), THE DishPanel SHALL start in Saved_Mode when entering edit mode, displaying the resolved saved dish description.
10. WHEN a DishRecord has a custom description (null `SavedDishId`), THE DishPanel SHALL start in Custom_Mode when entering edit mode.

### Requirement 9: Save Dish from DayPlan in Saved Mode

**User Story:** As a housemate, I want to save a day plan dish that references a saved dish, so that the day plan stays linked to the saved dish.

#### Acceptance Criteria

1. WHEN the housemate saves the dish while in Saved_Mode, THE DishPanel SHALL send the selected `SavedDishId` in the API request along with a null description.
2. WHEN the API receives a dish save request with a non-null `SavedDishId`, THE API SHALL store the `SavedDishId` on the DishRecord and clear the description field.
3. IF the referenced `SavedDishId` does not exist or belongs to a different household, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.
4. WHEN the housemate saves the dish while in Custom_Mode, THE DishPanel SHALL send a null `SavedDishId` and the typed description in the API request (existing behavior).
5. WHEN the API receives a dish save request with a null `SavedDishId` and a non-null description, THE API SHALL store the description on the DishRecord and set `SavedDishId` to null (existing behavior).
6. IF the API receives a dish save request with both a non-null `SavedDishId` and a non-null description, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.
7. IF the API receives a dish save request with both a null `SavedDishId` and a null or empty description, THEN THE API SHALL delete the DishRecord (clearing the dish for that day).

### Requirement 10: DayPlan Dish Display — Saved vs Custom Visual Distinction

**User Story:** As a housemate, I want to see at a glance whether a day's dish is a saved dish or a custom one, so that I understand how the dish is managed.

#### Acceptance Criteria

1. WHEN a DishRecord references a Saved_Dish (non-null `SavedDishId`), THE DishPanel SHALL display a visual indicator (icon or badge) adjacent to the dish description text in read mode to denote it as a saved dish.
2. WHEN a DishRecord has a custom description (null `SavedDishId`), THE DishPanel SHALL display the dish description without the saved-dish indicator (existing behavior).
3. THE visual indicator SHALL maintain a minimum contrast ratio of 3:1 against both the light-background and dark-background contexts used by the DishPanel.
4. THE visual indicator SHALL include an accessible label (via `aria-label` or equivalent) resolved through `IStringLocalizer<AppStrings>` so that screen readers convey the saved-dish status to non-visual users.
5. THE DishDto SHALL include a boolean or nullable GUID field indicating whether the dish references a Saved_Dish, so that the DishPanel can determine which display state to render without additional API calls.

### Requirement 11: Update Saved Dish Description

**User Story:** As a housemate, I want to rename a saved dish, so that the updated description is reflected in all day plans that reference it.

#### Acceptance Criteria

1. WHEN a housemate updates a Saved_Dish description, THE API SHALL validate that the new description does not conflict with any other existing Saved_Dish in the household (excluding the dish being updated; active or soft-deleted, case-insensitive after trimming).
2. IF the new description conflicts with another Saved_Dish, THEN THE API SHALL return HTTP 409 with error code `DISH_ALREADY_EXISTS`.
3. WHEN a Saved_Dish description is updated successfully, THE API SHALL return the updated Saved_Dish record.
4. THE API SHALL enforce a description length between 1 and 100 characters (after trimming) for the updated description. IF the trimmed description is empty or exceeds 100 characters, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.
5. WHEN a Saved_Dish description is updated, all DayPlan responses that reference the Saved_Dish SHALL reflect the new description without any additional write operations to DishRecords.
6. IF the target Saved_Dish does not exist, belongs to a different household, or is soft-deleted, THEN THE API SHALL return HTTP 404 with error code `NOT_FOUND`.

### Requirement 12: Saved Dishes API Endpoints

**User Story:** As a developer, I want dedicated API endpoints for saved dish CRUD operations, so that the frontend can manage the saved dishes collection.

#### Acceptance Criteria

1. THE API SHALL expose a `GET /api/saved-dishes` endpoint that returns HTTP 200 with a JSON array of all active (non-deleted) saved dishes for the authenticated household, sorted alphabetically by description.
2. THE API SHALL expose a `POST /api/saved-dishes` endpoint that accepts a JSON body with a `description` field, creates a new Saved_Dish (or reactivates a soft-deleted one), triggers Retroactive_Conversion, and returns HTTP 201 with the created/reactivated record.
3. THE API SHALL expose a `PUT /api/saved-dishes/{id}` endpoint that accepts a JSON body with a `description` field, updates the description of an existing active Saved_Dish, and returns HTTP 200 with the updated record.
4. THE API SHALL expose a `DELETE /api/saved-dishes/{id}` endpoint that soft-deletes a Saved_Dish and returns HTTP 204 with no response body.
5. THE API SHALL expose a `GET /api/saved-dishes/suggestions` endpoint that returns HTTP 200 with a JSON array of up to 5 distinct custom dish descriptions from DishRecords that are not already saved, ordered by most recent usage.
6. IF a `PUT` or `DELETE` request references a Saved_Dish that does not exist or belongs to a different household, THEN THE API SHALL return HTTP 404 with error code `NOT_FOUND`.
7. IF a `PUT` or `DELETE` request references a Saved_Dish that is already soft-deleted, THEN THE API SHALL return HTTP 404 with error code `NOT_FOUND`.
8. THE API SHALL validate the `description` field on `POST` and `PUT` requests: required, max 100 characters, trimmed, not empty after trimming. Violations SHALL return HTTP 422 with error code `VALIDATION_ERROR`.
9. IF the `{id}` route parameter on `PUT` or `DELETE` is not a valid GUID, THEN THE API SHALL return HTTP 400 with error code `BAD_REQUEST`.
10. IF a `POST` or `PUT` request specifies a description that matches an existing active Saved_Dish in the same household (case-insensitive after trimming), THEN THE API SHALL return HTTP 409 with error code `DISH_ALREADY_EXISTS`.

### Requirement 13: Offline Support for Saved Dishes

**User Story:** As a housemate, I want saved dish operations to work offline where possible, so that I can still manage dishes without connectivity.

#### Acceptance Criteria

1. WHEN the Saved_Dishes_Page attempts any operation (add, edit, delete, load list, load suggestions) while `ConnectivityService.IsOnline` is false, THE Saved_Dishes_Page SHALL prevent the operation from executing and SHALL display a localized error message using the `Error_RequiresInternet` resource key.
2. WHEN saving a dish in Saved_Mode on the DayPlan page, THE DishPanel SHALL send the selected `SavedDishId` (with a null description) through `CachedApiClient`, which SHALL queue the mutation for offline replay using the same mechanism as existing dish saves.
3. WHEN a saved-mode dish mutation is applied optimistically to the DayPlan cache (either during offline queueing or online success), THE CachedApiClient SHALL store the resolved saved dish description in the cached `DishDto` so the dish displays without requiring a network lookup of the Saved_Dish record.
4. WHEN the DayPlan page loads a day while offline, THE cached DayPlan response SHALL already contain the resolved description for any saved dish reference (as written by the API at the time of the original GET or by the optimistic update), so the dish displays identically to the online state.

### Requirement 14: Navigation — Saved Dishes Page Placement

**User Story:** As a housemate, I want to find the Saved Dishes page in a logical position in the navigation, so that I can access it easily.

#### Acceptance Criteria

1. THE navigation menu SHALL include a "Saved Dishes" entry positioned as the third item, after Calendar and before Housemates.
2. WHEN the user taps the "Saved Dishes" navigation entry, THE system SHALL navigate to `/saved-dishes`.
3. WHILE the current route starts with `saved-dishes`, THE navigation menu SHALL display the "Saved Dishes" entry in its active state.
4. THE navigation entry `aria-label` SHALL be resolved via `IStringLocalizer<AppStrings>` using a `Nav_SavedDishes` resource key with translations in both Dutch and English.

### Requirement 15: Future Feature Prompts

**User Story:** As a developer, I want prompt documents for planned future features, so that I can create separate specs for them later.

#### Acceptance Criteria

1. THE spec SHALL include a prompt document for the "Public Dishes" feature describing the toggle-to-public behavior on a Saved_Dish, how public dishes appear as suggestions in other households, and what "copy" means (creating an independent household-local Saved_Dish from a public one).
2. THE spec SHALL include a prompt document for the "Saved-Dish-History" feature describing an audit trail for Saved_Dish changes following the DayHistoryEntry pattern (ChangeType, ChangedByHousemateId, ChangedAt), including which operations are tracked (add, rename, soft-delete, reactivate).
3. THE spec SHALL include a prompt document for the "Recipes" feature describing the addition of an ingredients list and free-text cooking instructions to a Saved_Dish, including maximum field lengths and how recipe data is displayed on the DayPlan page when a saved dish is referenced.
4. THE spec SHALL include a prompt document for the "Statistics" feature describing per-dish usage frequency counts, per-housemate cooking attribution (based on DishRecord.LastChangedByHousemateId), and the time range over which analytics are computed.
5. EACH prompt document SHALL contain at minimum: a one-paragraph feature summary, a user story, a list of key behaviors to be specified, and a list of affected existing components or data models.
6. THE prompt documents SHALL be stored as separate markdown files in the same spec directory as this requirements document, named `prompt-{feature-slug}.md` (e.g., `prompt-public-dishes.md`).

### Requirement 16: Promote Custom Dish to Saved from DayPlan

**User Story:** As a housemate, I want to promote the custom dish I have typed on the DayPlan page to a saved dish directly from the Mode_Toggle list, so that I can save a dish without navigating to the Saved Dishes page.

#### Acceptance Criteria

1. WHEN the Mode_Toggle is activated from Custom_Mode AND the dish input field contains a non-empty description (after trimming), THE DishPanel SHALL display a Promote_Option at the top of the saved dishes list with the text "Add {current custom dish description} to saved dishes" (localized via `IStringLocalizer<AppStrings>`).
2. WHEN the dish input field is empty or whitespace-only after trimming, THE DishPanel SHALL NOT display the Promote_Option in the saved dishes list.
3. WHEN the housemate selects the Promote_Option, THE DishPanel SHALL create a new Saved_Dish via the `POST /api/saved-dishes` endpoint using the current custom dish description (triggering Retroactive_Conversion per Requirement 7).
4. WHEN the Promote_Option succeeds, THE DishPanel SHALL switch to Saved_Mode with the newly created Saved_Dish selected, and the DishRecord for the current day SHALL reference the new Saved_Dish via `SavedDishId`.
5. IF the Promote_Option fails because a Saved_Dish with the same description already exists (HTTP 409), THEN THE DishPanel SHALL display a localized error message indicating the dish is already saved, and SHALL switch to Saved_Mode with the existing matching Saved_Dish selected.
6. IF the Promote_Option fails due to a network or server error, THEN THE DishPanel SHALL display a localized error message and remain in Custom_Mode with the typed description preserved.
7. WHEN the Promote_Option description exceeds 100 characters after trimming, THE DishPanel SHALL NOT display the Promote_Option (the description would fail validation).
