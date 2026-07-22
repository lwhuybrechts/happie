# Prompt: Dish Folders

## Feature Summary

Dish Folders (also referred to as "categories") allow housemates to organize their saved dishes into named groups — such as "Italian", "Desserts", "Quick meals", or "Favourites" — so they can browse and filter the household's saved dishes collection more easily as it grows. Rather than scrolling through a flat alphabetical list in the Multi_Select_Modal, housemates can narrow the view to a specific folder and quickly find the dish they want.

## User Story

As a housemate, I want to organize my saved dishes into folders or categories, so that I can quickly find and select dishes when the collection becomes large.

## Key Behaviors to Be Specified

- **Folder CRUD** — Create, rename, and delete folders within a household.
- **Assigning saved dishes to folders** — Link one or more saved dishes to a folder. Decide whether a saved dish can belong to multiple folders or only one.
- **Filtering the saved dishes list by folder** — In the Multi_Select_Modal, allow housemates to filter or group the dish list by folder.
- **Multiple folder membership** — Specify whether a saved dish can belong to multiple folders simultaneously, or whether it is restricted to a single folder.
- **Unassigned dishes** — Define how dishes that have not been assigned to any folder appear (e.g., an "All" view that shows everything, or an "Uncategorized" group for unassigned dishes).
- **Folder display in the Multi_Select_Modal** — Determine the UI pattern for folder-based navigation (tabs, collapsible groups, filter chips, or another approach).
- **Maximum number of folders per household** — Set a reasonable upper limit to keep the UI manageable.
- **Folder ordering** — Define how folders are ordered in the UI (alphabetical, manual drag-to-reorder, or creation order).

## Affected Existing Components and Data Models

- **`SavedDish` domain model** — May need a folder reference (either a `FolderIds` collection or a single `FolderId` field).
- **`DayPlanDishLink`** — Unaffected. Links connect day plans to dishes regardless of which folder a dish belongs to.
- **`Multi_Select_Modal`** — Needs folder filtering or grouping UI added to the existing multi-select interaction.
- **`SavedDishesFunction` / `SavedDishHandler`** — New CRUD endpoints for folders, plus an endpoint or parameter to update dish-to-folder assignments.
- **New `DishFolder` domain model / entity / repository** — A new domain type representing a named folder within a household.
- **New `DishFolderAssignment` join table OR a `FolderId` field on `SavedDishEntity`** — The storage mechanism for the dish-to-folder relationship.

## Relationship to Existing Entities

The multi-dish selection data model was designed so that folders can be added without schema changes to existing tables (`DayPlanDishLinks`, `DishRecords`). Folders are an orthogonal layer on top of saved dishes — they affect how dishes are browsed and filtered, not how they are linked to day plans.

A new Azure Table Storage table (e.g., `DishFolders`) would store folder metadata with a partition key of `{HouseholdId}` and a row key of `{FolderId}` (GUID). This follows the same patterns as other household-scoped entities.

For assigning dishes to folders, two approaches are viable:

1. **A `FolderIds` property on `SavedDishEntity`** — A JSON-serialized list of folder GUIDs stored directly on the saved dish entity. Simple to query (all data in one table), but requires updating the saved dish entity when folder assignments change.
2. **A separate `DishFolderAssignments` join table** — A dedicated table with PK = `{HouseholdId}` and RK = `{FolderId}_{SavedDishId}`. More normalized, supports querying all dishes in a folder efficiently, but adds an extra table read when loading the full dish list with folder metadata.

The choice between these approaches should be made during the full spec based on query patterns and the decision about single vs. multiple folder membership.
