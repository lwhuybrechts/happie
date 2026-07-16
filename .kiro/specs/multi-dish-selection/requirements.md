# Requirements Document

## Introduction

The Multi-Dish Selection feature extends the existing saved-dishes functionality to allow selecting multiple saved dishes for a single day plan. Currently, only one saved dish can be selected per day. This feature changes the SavedDishModal from single-select (tap to close) to multi-select (checkmarks, live preview, confirm button), introduces a separate join table in Azure Table Storage to store the many-to-many relationship between DishRecords and SavedDishes, and updates the DayHandler to resolve and join descriptions from all linked saved dishes with " & ". The existing single `SavedDishId` field on DishRecord is replaced by this join table approach. Since the saved-dishes feature has not been deployed to production Azure (only running locally in Azurite), the schema from the original saved-dishes spec can be freely modified without migration concerns. The data model is designed so that a future dish folders/categories feature can be added without schema changes to existing tables.

Additionally, this spec addresses several UX and behavioral fixes to the existing SavedDishModal and DishPanel interactions: modal overlay positioning, scroll lock when modals are open, scroll-within-modal for long lists, automatic saved-dish matching when saving custom descriptions, and mode-toggle behavior changes.

## Glossary

- **DayPlanDishLink**: An Azure Table Storage entity representing the association between a DishRecord (day plan) and a SavedDish. Stored in a dedicated join table.
- **DayPlanDishLinks_Table**: A separate Azure Table Storage table that stores all DayPlanDishLink entities, mapping DishRecord primary keys to SavedDishIds.
- **Multi_Select_Modal**: The updated SavedDishModal that allows toggling multiple saved dishes with checkmarks rather than immediately closing on a single tap.
- **Sticky_Footer**: A fixed footer at the bottom of the Multi_Select_Modal displaying a "Confirm" button with the count of selected dishes, a live preview of the combined dish text, and a "Custom mode" button to switch back to Custom_Mode.
- **Combined_Description**: The joined text of all linked saved dish descriptions for a day, concatenated with " & " as separator.
- **Selection_Limit**: The maximum number of saved dishes that can be linked to a single day plan, set to 10.
- **Dish_Folder**: A planned future categorization mechanism for saved dishes (e.g., "desserts", "Italian", "favourites") that the data model should be designed to accommodate.
- **DishRecord**: The existing Azure Table Storage entity storing the dish for a specific day in a household. The `SavedDishId` field is removed and replaced by the DayPlanDishLinks_Table.
- **SavedDish**: A reusable dish record belonging to a household, identified by a unique GUID.
- **Saved_Mode**: The dish input state on the DayPlan page where the housemate selects one or more saved dishes from the household collection.
- **Custom_Mode**: The default dish input state on the DayPlan page where the housemate types a free-text description (existing behavior).
- **Promote_Option**: An option displayed in the Multi_Select_Modal when a custom dish description is currently entered, allowing the housemate to save the custom description as a new SavedDish and link it to the current day.
- **Scroll_Lock**: The behavior where background page scrolling is disabled while a modal overlay is open.
- **Auto_Match**: The behavior where saving a custom dish description that matches an existing saved dish (active or soft-deleted, case-insensitive, trimmed) automatically links the saved dish instead of storing a custom description. If the matched dish is soft-deleted, it is reactivated.

## Requirements

### Requirement 1: Join Table Data Model

**User Story:** As a housemate, I want multiple saved dishes linked to a single day plan, so that I can represent composite meals (e.g., main course and dessert) without being limited to one dish.

#### Acceptance Criteria

1. THE DayPlanDishLink SHALL have a PartitionKey of `{HouseholdId}_{YYYY-MM-DD}` (combining household ID and date) and a RowKey of `{SavedDishId}`.
2. THE DayPlanDishLink SHALL be stored in the DayPlanDishLinks_Table in Azure Table Storage.
3. THE DayPlanDishLink SHALL contain a `SortOrder` integer field representing the order in which the saved dish was selected (0-based), so that the Combined_Description is deterministic.
4. THE DishRecordEntity SHALL no longer contain a `SavedDishId` field; all saved dish associations SHALL be stored exclusively in the DayPlanDishLinks_Table.
5. WHEN no DayPlanDishLink entities exist for a given DishRecord, THE system SHALL treat the day as either having a custom description or having no dish set (existing behavior).

### Requirement 2: Multi-Select Modal Interaction

**User Story:** As a housemate, I want to toggle multiple saved dishes with checkmarks in the modal and confirm my selection, so that I can compose a multi-dish day plan in a single interaction.

#### Acceptance Criteria

1. WHEN the Mode_Toggle is activated from Custom_Mode, THE Multi_Select_Modal SHALL display a list of all active saved dishes sorted alphabetically, each with a toggleable checkmark.
2. WHEN a housemate taps a saved dish in the Multi_Select_Modal, THE Multi_Select_Modal SHALL toggle the checkmark on that dish without closing the modal.
3. THE Multi_Select_Modal SHALL display a Sticky_Footer containing a "Confirm" button showing the count of currently selected dishes, a live preview of the Combined_Description (joined with " & "), and a "Custom mode" button to switch back to Custom_Mode.
4. WHEN no dishes are selected, THE Sticky_Footer SHALL display the "Confirm" button in a disabled state.
5. WHEN the housemate activates the "Confirm" button, THE Multi_Select_Modal SHALL close and THE DishPanel SHALL switch to Saved_Mode with all confirmed dishes linked to the current day.
6. WHEN a housemate dismisses the Multi_Select_Modal (via the close button or backdrop tap) without confirming, THE DishPanel SHALL remain in its previous state without changes.
7. WHEN entering the Multi_Select_Modal for a day that already has linked saved dishes, THE Multi_Select_Modal SHALL pre-select (checkmark) the currently linked dishes.
8. WHEN the Multi_Select_Modal is opened AND the current custom description (trimmed, case-insensitive) matches an existing active saved dish, THE Multi_Select_Modal SHALL automatically pre-select that matching dish and scroll the list to bring the first selected dish into view, rather than showing a separate "Use existing" action.
9. WHEN the housemate activates the "Custom mode" button in the Sticky_Footer, THE Multi_Select_Modal SHALL close and THE DishPanel SHALL switch to Custom_Mode with the input field pre-filled with the Combined_Description of any previously selected saved dishes (or the existing custom description if no dishes were selected).
10. THE Multi_Select_Modal SHALL display all user-visible text using localized strings resolved via `IStringLocalizer<AppStrings>`.

### Requirement 3: Selection Limit

**User Story:** As a housemate, I want a reasonable limit on how many saved dishes I can select per day, so that the combined description remains readable and the system stays performant.

#### Acceptance Criteria

1. THE Multi_Select_Modal SHALL enforce a Selection_Limit of 10 saved dishes per day plan.
2. WHEN 10 dishes are already selected, THE Multi_Select_Modal SHALL disable the checkmarks on all unselected dishes, preventing further selections.
3. WHEN a housemate deselects a dish while at the Selection_Limit, THE Multi_Select_Modal SHALL re-enable the checkmarks on unselected dishes.
4. IF the API receives a dish save request with more than 10 SavedDishIds, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.

### Requirement 4: Combined Description Resolution

**User Story:** As a housemate, I want all linked saved dish descriptions joined with " & " when viewing a day plan, so that I can see the full composite meal at a glance.

#### Acceptance Criteria

1. WHEN a DishRecord has one or more associated DayPlanDishLink entities, THE API SHALL resolve each linked SavedDish description and join them with " & " in the order defined by the SortOrder field.
2. WHEN a DishRecord has associated DayPlanDishLink entities AND a non-empty custom description, THE API SHALL use only the linked saved dish descriptions (the custom description field is ignored when links exist).
3. WHEN a linked SavedDish is renamed, THE DayPlan response for any day referencing that SavedDish SHALL reflect the updated name without any write operations to the DayPlanDishLinks_Table or DishRecords.
4. IF a DayPlanDishLink references a SavedDish that has been soft-deleted, THEN THE API SHALL still resolve the description from the soft-deleted SavedDish record.
5. IF a DayPlanDishLink references a SavedDishId that does not exist in the household's SavedDish records, THEN THE API SHALL exclude that dish from the Combined_Description and omit its ID from the response.
6. WHEN returning a DayPlan response, THE API SHALL include the list of linked SavedDishIds (in SortOrder) so the frontend can determine whether the dish is in Saved_Mode and which dishes are selected.
7. THE Combined_Description SHALL have no character limit imposed; the Selection_Limit of 10 dishes serves as the practical constraint.

### Requirement 5: Save Dish from DayPlan in Multi-Select Saved Mode

**User Story:** As a housemate, I want to save a day plan dish that references multiple saved dishes, so that the day plan stays linked to all selected saved dishes.

#### Acceptance Criteria

1. WHEN the housemate confirms the selection in the Multi_Select_Modal, THE DishPanel SHALL send the list of selected SavedDishIds (in selection order) in the API request along with a null description.
2. WHEN the API receives a dish save request with a non-empty list of SavedDishIds, THE API SHALL create DayPlanDishLink entities for each SavedDishId (with SortOrder matching the list index) and clear the DishRecord description field.
3. WHEN the API receives a dish save request with a non-empty list of SavedDishIds, THE API SHALL delete any previously existing DayPlanDishLink entities for that DishRecord before creating the new set (full replacement).
4. IF any referenced SavedDishId does not exist or belongs to a different household, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.
5. WHEN the housemate saves the dish while in Custom_Mode, THE DishPanel SHALL send a null SavedDishIds list and the typed description in the API request (existing behavior), and THE API SHALL delete any existing DayPlanDishLink entities for that DishRecord.
6. IF the API receives a dish save request with both a non-empty SavedDishIds list AND a non-null description, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.
7. IF the API receives a dish save request with both an empty SavedDishIds list and a null or empty description, THEN THE API SHALL clear the DishRecord description field and delete any existing DayPlanDishLink entities. IF the DishRecord has no other data (no DinnerTime set), THEN THE API SHALL delete the DishRecord entirely. IF the DishRecord has a DinnerTime set, THEN THE API SHALL preserve the DishRecord with an empty description and no linked dishes.

### Requirement 6: DishPanel Display for Multi-Dish Selection

**User Story:** As a housemate, I want to see the combined description of all linked saved dishes in the DishPanel, so that I know at a glance what is planned for the day.

#### Acceptance Criteria

1. WHEN a DishRecord has one or more linked saved dishes, THE DishPanel SHALL display the Combined_Description (all descriptions joined with " & ") in read mode.
2. WHEN a DishRecord has one or more linked saved dishes, THE DishPanel SHALL display the saved-dish visual indicator (bookmark icon) adjacent to the Combined_Description.
3. WHEN a DishRecord has linked saved dishes and the housemate enters edit mode, THE DishPanel SHALL start in Saved_Mode with the linked dishes pre-selected.
4. WHEN the housemate activates the Mode_Toggle from Saved_Mode to Custom_Mode, THE DishPanel SHALL enable the input field and pre-fill it with the Combined_Description of the previously selected saved dishes.

### Requirement 7: Promote Option in Multi-Select Context

**User Story:** As a housemate, I want to promote a custom dish to a saved dish from within the Multi_Select_Modal, so that I can add it to the household collection without leaving the day plan.

#### Acceptance Criteria

1. WHEN the Multi_Select_Modal is opened AND the dish input field contains a non-empty description (after trimming, max 100 characters), THE Multi_Select_Modal SHALL display a Promote_Option at the top of the list.
2. WHEN the housemate selects the Promote_Option, THE system SHALL create a new SavedDish via `POST /api/saved-dishes` and, on success, automatically check the newly created dish in the Multi_Select_Modal.
3. IF the Promote_Option succeeds, THE Multi_Select_Modal SHALL remain open with the new dish checked, allowing the housemate to select additional dishes before confirming.
4. IF the Promote_Option fails because a SavedDish with the same description already exists (HTTP 409), THEN THE Multi_Select_Modal SHALL display a localized error message and automatically check the existing matching dish.
5. IF the Promote_Option fails due to a network or server error, THEN THE Multi_Select_Modal SHALL display a localized error message and remain open without changes.
6. WHEN the dish input field is empty, whitespace-only after trimming, or exceeds 100 characters, THE Multi_Select_Modal SHALL NOT display the Promote_Option.

### Requirement 8: Offline Support for Multi-Dish Selection

**User Story:** As a housemate, I want multi-dish selection to work offline, so that I can compose meals without connectivity.

#### Acceptance Criteria

1. WHEN saving a multi-dish selection on the DayPlan page, THE DishPanel SHALL send the list of SavedDishIds through `CachedApiClient`, which SHALL queue the mutation for offline replay using the same mechanism as existing dish saves.
2. WHEN a multi-dish mutation is applied optimistically to the DayPlan cache, THE CachedApiClient SHALL store the Combined_Description (resolved from the locally known saved dish descriptions) in the cached DishDto so the dish displays without requiring a network lookup.
3. WHEN the DayPlan page loads a day while offline, THE cached DayPlan response SHALL already contain the Combined_Description for any multi-dish reference, so the dish displays identically to the online state.
4. THE CachedApiClient SHALL include the list of SavedDishIds in the cached DishDto so that the Multi_Select_Modal can pre-select the correct dishes when re-opened offline.

### Requirement 9: Retroactive Conversion Update

**User Story:** As a developer, I want retroactive conversion to work with the new join table model, so that creating a saved dish still links historical DishRecords.

#### Acceptance Criteria

1. WHEN a new SavedDish is created (including reactivation of a soft-deleted dish), THE API SHALL scan all DishRecords for the household where no DayPlanDishLink entities exist and the DishRecord description matches the new SavedDish description (case-insensitive, trimmed).
2. FOR ALL matching DishRecords found during retroactive conversion, THE API SHALL create a DayPlanDishLink entity (with SortOrder 0) linking the DishRecord to the new SavedDish, and set the DishRecord description field to an empty string.
3. IF retroactive conversion fails for one or more DishRecords, THEN THE API SHALL log the failure but SHALL NOT roll back the SavedDish creation or return an error to the client.

### Requirement 10: API Contract Changes

**User Story:** As a developer, I want the API contract to support multiple saved dish IDs per day, so that the frontend can send and receive multi-dish selections.

#### Acceptance Criteria

1. THE UpdateDishRequest SHALL replace the nullable `savedDishId` field with a nullable `savedDishIds` field (JSON array of GUIDs), representing the ordered list of linked saved dishes.
2. THE DishDto in the DayPlan response SHALL replace the nullable `savedDishId` field with a nullable `savedDishIds` field (JSON array of GUIDs in SortOrder), so the frontend can determine which dishes are linked.
3. WHEN `savedDishIds` is null or an empty array in the UpdateDishRequest, THE API SHALL treat the save as a custom-mode save (existing behavior with description field).
4. WHEN `savedDishIds` contains one or more GUIDs, THE API SHALL validate that each GUID references an active or soft-deleted SavedDish in the same household.
5. THE API SHALL validate that the `savedDishIds` array contains no duplicate GUIDs. IF duplicates are present, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.

### Requirement 11: Future Dish Folders Prompt Document

**User Story:** As a developer, I want a prompt document for the planned dish folders/categories feature, so that a separate spec can be created for it later.

#### Acceptance Criteria

1. THE spec SHALL include a prompt document for the "Dish Folders" feature describing folder/category management (create, rename, delete), assigning saved dishes to folders, filtering the saved dishes list by folder, and how folders appear in the Multi_Select_Modal.
2. THE prompt document SHALL describe how a folder/category model relates to the existing SavedDish and DayPlanDishLink entities, and what fields or tables would need to be added.
3. THE prompt document SHALL contain at minimum: a one-paragraph feature summary, a user story, a list of key behaviors to be specified, and a list of affected existing components or data models.
4. THE prompt document SHALL be stored as `prompt-dish-folders.md` in the same spec directory as this requirements document.

### Requirement 12: Sticky Footer Live Preview

**User Story:** As a housemate, I want to see a live preview of the combined dish text while selecting dishes, so that I can judge how the composite meal description will look before confirming.

#### Acceptance Criteria

1. THE Sticky_Footer SHALL display the Combined_Description of all currently checked dishes, joined with " & " in the order they were selected.
2. WHEN the housemate toggles a dish on or off, THE Sticky_Footer SHALL immediately update the live preview text and the selection count.
3. WHEN no dishes are selected, THE Sticky_Footer SHALL display a localized placeholder message (e.g., "Select dishes") instead of an empty preview.
4. THE Sticky_Footer SHALL remain visible at all times while the Multi_Select_Modal is open, regardless of scroll position within the dish list.
5. THE "Confirm" button text SHALL include the count of selected dishes (e.g., "Confirm (3)") resolved via `IStringLocalizer<AppStrings>`.

### Requirement 13: Modal Overlay Positioning Fix

**User Story:** As a housemate, I want the modal overlay to cover the entire screen including the day plan header, so that the blurred background effect is consistent and professional.

#### Acceptance Criteria

1. THE Multi_Select_Modal overlay SHALL cover the entire viewport including the weekday/date header section of the DayPlan page, matching the behavior of the NudgeModal overlay.
2. THE Multi_Select_Modal overlay SHALL use `position: fixed` with `inset: 0` and `z-index: 1100` so that it renders above the mobile header and bottom navigation.
3. THE Multi_Select_Modal dialog SHALL use `z-index: 1101` to render above its own overlay.

### Requirement 14: Scroll Lock When Modal is Open

**User Story:** As a housemate, I want background scrolling to be disabled while a modal is open, so that I do not accidentally scroll the page content behind the blurred overlay.

#### Acceptance Criteria

1. WHILE any modal overlay is open (Multi_Select_Modal, NudgeModal, or HousemateColorPicker), THE system SHALL prevent background page scrolling by applying `overflow: hidden` to the document body.
2. WHEN the modal is closed (via confirm, dismiss, or backdrop tap), THE system SHALL restore the previous scroll behavior on the document body.
3. THE Scroll_Lock SHALL be applied universally to all existing and new modals in the application, not only the Multi_Select_Modal.

### Requirement 15: Scrollable Modal Content

**User Story:** As a housemate, I want to scroll within the modal when the list of saved dishes is too long for the screen, so that I can see and select all available dishes.

#### Acceptance Criteria

1. WHEN the saved dishes list exceeds the available vertical space within the Multi_Select_Modal, THE Multi_Select_Modal body SHALL be independently scrollable.
2. THE Multi_Select_Modal header and Sticky_Footer SHALL remain fixed in place (not scroll with the list content).
3. THE page itself SHALL NOT scroll when the user scrolls within the Multi_Select_Modal (the scroll is contained within the modal).

### Requirement 16: Auto-Match Custom Description to Saved Dish on Save

**User Story:** As a housemate, I want the system to automatically link a saved dish when I save a custom description that matches one, so that I do not accidentally create duplicate custom entries.

#### Acceptance Criteria

1. WHEN the housemate saves a dish in Custom_Mode AND the typed description (trimmed, case-insensitive) matches the description of an active or soft-deleted SavedDish in the household, THE API SHALL automatically link that SavedDish instead of storing the custom description.
2. WHEN Auto_Match matches a soft-deleted SavedDish, THE API SHALL reactivate the soft-deleted SavedDish (set `IsDeleted` to false) before creating the link.
3. WHEN Auto_Match occurs, THE API SHALL create a DayPlanDishLink entity (with SortOrder 0) for the matched SavedDish and clear the DishRecord description field, identical to a Saved_Mode save with that single dish.
4. WHEN Auto_Match occurs, THE DayPlan response SHALL return the matched SavedDishId in the `savedDishIds` array, so the frontend displays the dish in Saved_Mode on next load.

### Requirement 17: Mode Toggle Behavior Change

**User Story:** As a housemate, I want the bookmark button to always open the Multi_Select_Modal regardless of current mode, so that I can change my dish selection without switching to custom mode first.

#### Acceptance Criteria

1. WHEN the housemate activates the Mode_Toggle while in Saved_Mode, THE DishPanel SHALL open the Multi_Select_Modal with the currently linked dishes pre-selected, rather than switching directly to Custom_Mode.
2. WHEN the housemate activates the Mode_Toggle while in Custom_Mode, THE DishPanel SHALL open the Multi_Select_Modal (existing behavior from Requirement 2).
3. THE Sticky_Footer in the Multi_Select_Modal SHALL include a "Custom mode" button that, when activated, closes the modal and switches the DishPanel to Custom_Mode with the input field pre-filled with the Combined_Description of any currently selected saved dishes.
4. IF no dishes are selected when the "Custom mode" button is activated, THE DishPanel SHALL switch to Custom_Mode with the input field pre-filled with the current custom description (or empty if none exists).

### Requirement 18: Steering Document Update for Modal Conventions

**User Story:** As a developer, I want the modal overlay and scroll-lock conventions documented in the project steering files, so that future modals are implemented correctly without repeating these bugs.

#### Acceptance Criteria

1. THE project steering documents SHALL document that all modal overlays MUST use `position: fixed` with `inset: 0` and `z-index: 1100` to cover the entire viewport including fixed headers.
2. THE project steering documents SHALL document that all modals MUST apply Scroll_Lock (`overflow: hidden` on the document body) while open and restore it on close.
3. THE project steering documents SHALL document that modal content areas MUST be independently scrollable when content exceeds available space, with the header and footer remaining fixed.
4. THE steering document updates SHALL be applied to the existing `coding-conventions.md` file in the `.kiro/steering/` directory.

