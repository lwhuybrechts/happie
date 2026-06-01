# Design Document: Chef Toggle

## Overview

The Chef Toggle feature adds an independent binary toggle to each housemate row in the Attendance Section, allowing any housemate to mark one or more housemates as "chef" for a given day. The toggle is visually represented by a chef's hat icon and operates independently from the three-state attendance radio buttons.

Key design decisions:
- **Merged into attendance table**: Chef status is stored as an `IsChef` boolean on the existing `AttendanceRecordEntity` rather than in a separate table. The two concepts share the same per-housemate-per-day granularity and the same RowKey pattern (`{YYYY-MM-DD}_{HousemateId}`), making a single record the natural home for both.
- **Frontend-initiated auto-chef**: When a housemate fills in a dish and no one is currently marked as chef, the **frontend** detects this condition and fires a regular `PUT /api/days/{date}/chef/{housemateId}` call. The server has no auto-chef logic — it only handles explicit chef toggles.
- **Optimistic UI without reload**: The chef toggle follows the same optimistic pattern as `AttendanceSection`: apply override locally, call API, on success keep local state, on failure roll back. No `LoadDayPlanAsync()` reload is triggered.
- **Last-write-wins concurrency**: Consistent with the existing attendance and dish patterns, concurrent chef toggles use last-write-wins semantics.

## Architecture

The feature follows the existing vertical slice architecture:

```mermaid
graph TD
    A[AttendanceSection.razor] -->|PUT /api/days/{date}/chef/{housemateId}| B[DaysFunction]
    B --> C[DayHandler]
    C --> D[IAttendanceRepository]
    C --> E[IDayHistoryRepository]
    D --> F[Azure Table Storage - AttendanceRecords]
    E --> G[Azure Table Storage - DayHistory]
    
    H[DishPanel.razor] -->|PUT /api/days/{date}/dish| B
    H -->|Auto-chef: PUT /api/days/{date}/chef/{housemateId}| B
```

### Data Flow — Manual Chef Toggle

1. User clicks chef toggle button in `AttendanceSection.razor`
2. Component applies optimistic UI update (toggles `IsChef` in local override dictionary immediately)
3. Component sends `PUT /api/days/{date}/chef/{housemateId}` with `{ "isChef": true/false }`
4. `DaysFunction.PutChefStatusAsync` validates route params and request body
5. `DayHandler.UpsertChefStatusAsync` verifies housemate exists, upserts `IsChef` on the attendance record, writes `DayHistory` entry
6. On success: local state is already correct (no reload needed); on failure: component rolls back visual state and shows error toast

### Data Flow — Auto-Chef Assignment (Frontend-Initiated)

1. User saves a non-empty dish via `DishPanel.razor`
2. On successful dish save, the `DishPanel` (via `OnDishChanged` callback) or `DayPlanPage` checks the current attendance list
3. If no housemate has `IsChef = true`: the frontend fires `PUT /api/days/{date}/chef/{actingHousemateId}` with `{ "isChef": true }`
4. The server processes this as a regular chef toggle — no special auto-chef logic on the server
5. If at least one housemate has `IsChef = true`: no chef API call is made
6. Race condition (two users save dish simultaneously, both see no chef) is acceptable — last-write-wins means one ends up as chef

### Data Flow — Day Plan Load

1. `DayHandler.GetDayPlanAsync` fetches attendance records (which now include `IsChef`) — no separate chef fetch needed
2. Attendance records are joined with the housemate list to populate `IsChef` on each `AttendanceDto`
3. Housemates without an attendance record default to `IsChef = false`

## Components and Interfaces

### New Types

| Layer | Type | Location |
|---|---|---|
| Shared Domain | `ChangeType.ChefStatusChanged` (enum value) | `Happie.Shared/Domain/ChangeType.cs` |
| Shared Contracts | `UpdateChefStatusRequest` | `Happie.Shared/Contracts/UpdateChefStatusRequest.cs` |
| Handler | `IDayHandler.UpsertChefStatusAsync` (new method) | `Happie.Api/Handlers/IDayHandler.cs` |
| Function | `DaysFunction.PutChefStatusAsync` (new endpoint) | `Happie.Api/Functions/DaysFunction.cs` |

### Modified Types

| Type | Change |
|---|---|
| `ChangeType` | Add `ChefStatusChanged` value |
| `AttendanceRecord` (domain) | Add `bool IsChef` parameter |
| `AttendanceRecordEntity` | Add `bool IsChef` property |
| `IAttendanceRecordMapper` / `AttendanceRecordMapper` | Map `IsChef` field in both directions |
| `AttendanceDto` | Add `bool IsChef` property |
| `IAttendanceRepository` | Add `UpsertChefStatusAsync` method (upserts only the `IsChef` field) |
| `AttendanceRepository` | Implement `UpsertChefStatusAsync` |
| `IDayHandler` | Add `UpsertChefStatusAsync` method |
| `DayHandler` | Implement `UpsertChefStatusAsync`; extend `GetDayPlanAsync` to include `IsChef` in `AttendanceDto` |
| `DaysFunction` | Add `PutChefStatusAsync` function |
| `AttendanceSection.razor` | Add chef toggle button per housemate row with optimistic UI |
| `DishPanel.razor` | After successful dish save, detect no-chef condition and fire auto-chef API call |
| Resource files | Add localization keys for chef toggle aria-labels and history descriptions |

### Interface Definitions

```csharp
// IAttendanceRepository.cs — new method added to existing interface
Task UpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, CancellationToken ct = default);
```

```csharp
// IDayHandler.cs — new method
Task<bool> UpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, Guid actingHousemateId, CancellationToken ct = default);
```

### UpsertChefStatusAsync Implementation Notes

The `AttendanceRepository.UpsertChefStatusAsync` method must handle the case where no attendance record exists yet for the housemate on that day. It should:
1. Attempt to read the existing `AttendanceRecordEntity` for the given `{householdId}`, `{date}_{housemateId}`
2. If it exists: update only the `IsChef` property and upsert
3. If it does not exist: create a new entity with `Status = AttendanceStatus.Unknown` and the given `IsChef` value, then upsert

This ensures that toggling chef status never overwrites an existing attendance status, and that chef status can be set even before attendance is explicitly chosen.

Similarly, the existing `UpsertAttendanceAsync` in `DayHandler` must preserve the current `IsChef` value when changing attendance. The handler should:
1. Read the existing attendance record (if any) to get the current `IsChef` value
2. Create the new `AttendanceRecord` with the existing `IsChef` (defaulting to `false` if no record exists)
3. Upsert the full record

## Data Models

### Updated AttendanceRecord (Domain)

```csharp
// Happie.Api/Domain/AttendanceRecord.cs
public record AttendanceRecord(
    Guid HouseholdId,
    Guid HousemateId,
    DateOnly Date,
    AttendanceStatus Status,
    bool IsChef
);
```

### Updated AttendanceRecordEntity (Table Storage)

| Property | Type | Description |
|---|---|---|
| PartitionKey | string | `{HouseholdId}` |
| RowKey | string | `{YYYY-MM-DD}_{HousemateId}` |
| HousemateId | Guid | The housemate this record belongs to |
| Status | AttendanceStatus | The attendance status |
| IsChef | bool | Whether the housemate is marked as chef |

Table name: `AttendanceRecords` (existing table — no new table needed)

```csharp
// Happie.Api/Infrastructure/Entities/AttendanceRecordEntity.cs
public class AttendanceRecordEntity : MyTableEntity
{
    public AttendanceRecordEntity() { }

    public AttendanceRecordEntity(Guid householdId, DateOnly date, Guid housemateId)
    {
        PartitionKey = householdId.ToString();
        RowKey = $"{date:yyyy-MM-dd}_{housemateId}";
    }

    public Guid HousemateId { get; set; }
    public AttendanceStatus Status { get; set; }
    public bool IsChef { get; set; }
}
```

### Updated AttendanceDto

```csharp
// Happie.Shared/Contracts/AttendanceDto.cs
public record AttendanceDto(
    [property: JsonPropertyName("housemateId")] Guid HousemateId,
    [property: JsonPropertyName("housemateName")] string HousemateName,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("status")] AttendanceStatus Status,
    [property: JsonPropertyName("isChef")] bool IsChef);
```

### UpdateChefStatusRequest

```csharp
// Happie.Shared/Contracts/UpdateChefStatusRequest.cs
public record UpdateChefStatusRequest(
    [property: JsonPropertyName("isChef")] bool IsChef);
```

### Updated ChangeType Enum

```csharp
public enum ChangeType
{
    Attendance,
    Dish,
    Comment,
    ChefStatusChanged,
}
```

### API Endpoint

| Method | Route | Request Body | Response |
|---|---|---|---|
| PUT | `/api/days/{date}/chef/{housemateId}` | `UpdateChefStatusRequest` | 204 No Content / 404 Not Found |

### Updated AttendanceRecordMapper

```csharp
// Happie.Api/Infrastructure/Mappers/AttendanceRecordMapper.cs
public class AttendanceRecordMapper : IAttendanceRecordMapper
{
    public AttendanceRecord ToModel(Guid householdId, AttendanceRecordEntity entity)
    {
        var date = DateOnly.Parse(entity.RowKey[..10]);
        return new AttendanceRecord(householdId, entity.HousemateId, date, entity.Status, entity.IsChef);
    }

    public AttendanceRecordEntity ToEntity(AttendanceRecord record)
    {
        var entity = new AttendanceRecordEntity(record.HouseholdId, record.Date, record.HousemateId);
        entity.HousemateId = record.HousemateId;
        entity.Status = record.Status;
        entity.IsChef = record.IsChef;
        return entity;
    }
}
```

### Updated GetDayPlanAsync (relevant change)

```csharp
// In DayHandler.GetDayPlanAsync — the attendance DTO construction now includes IsChef.
var attendance = allHousemates
    .Where(x => !x.IsDeleted)
    .OrderBy(x => x.SortOrder)
    .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
    .Select(x =>
    {
        var status = attendanceByHousemateId.TryGetValue(x.Id, out var record)
            ? record.Status
            : AttendanceStatus.Unknown;
        var isChef = attendanceByHousemateId.TryGetValue(x.Id, out var chefRecord)
            ? chefRecord.IsChef
            : false;
        return new AttendanceDto(x.Id, x.Name, x.Color, status, isChef);
    })
    .ToList();
```

### Frontend Auto-Chef Logic (in DishPanel or DayPlanPage)

```csharp
// After successful dish save in DishPanel.SaveAsync:
if (!string.IsNullOrEmpty(trimmed))
{
    // Check if any housemate is currently chef.
    var anyChef = Attendance.Any(x => x.IsChef);
    if (!anyChef)
    {
        // Auto-assign chef to the acting housemate.
        // Optimistically set IsChef locally, then fire API call.
        await Http.PutAsJsonAsync(
            $"days/{Date}/chef/{ActingHousemateId}",
            new UpdateChefStatusRequest(true));
    }
}
```

### Frontend Optimistic UI Pattern (in AttendanceSection)

```csharp
// Chef toggle follows the same pattern as attendance toggle:
private readonly Dictionary<Guid, bool> _chefOverrides = new();
private readonly HashSet<Guid> _chefSavingIds = new();

private bool GetIsChef(Guid housemateId)
{
    if (_chefOverrides.TryGetValue(housemateId, out var overrideValue))
        return overrideValue;
    var item = Attendance.FirstOrDefault(x => x.HousemateId == housemateId);
    return item?.IsChef ?? false;
}

private async Task ToggleChefAsync(Guid housemateId)
{
    if (_chefSavingIds.Contains(housemateId))
        return;

    var currentIsChef = GetIsChef(housemateId);
    var newIsChef = !currentIsChef;

    // Optimistic update.
    _chefOverrides[housemateId] = newIsChef;
    _chefSavingIds.Add(housemateId);

    var response = await Http.PutAsJsonAsync(
        $"days/{Date}/chef/{housemateId}",
        new UpdateChefStatusRequest(newIsChef));

    _chefSavingIds.Remove(housemateId);

    if (!response.IsSuccessStatusCode)
    {
        // Rollback.
        _chefOverrides.Remove(housemateId);
        _chefErrorHousemateId = housemateId;
    }
    else
    {
        // Success: clear override, state is correct.
        _chefOverrides.Remove(housemateId);
    }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Chef toggle round-trip (last-write-wins)

*For any* housemate, any date, and any sequence of boolean values written via `UpsertChefStatusAsync`, reading back the chef status should return the last written value.

**Validates: Requirements 2.1, 4.4**

### Property 2: Chef status is independent of attendance status

*For any* housemate with any chef status (enabled or disabled) and any attendance status change (to Unknown, EatingIn, or NotEatingIn), the chef status after the attendance change must equal the chef status before the attendance change.

**Validates: Requirements 2.2, 2.3, 6.3**

### Property 3: Per-housemate chef independence

*For any* two distinct housemates on the same day, toggling the chef status of one housemate (by any acting housemate) must not change the chef status of the other housemate.

**Validates: Requirements 3.2, 7.3**

### Property 4: Multiple chefs and cross-housemate toggling

*For any* non-empty subset of active housemates in a household, enabling chef status for each housemate in the subset (with any acting housemate) should result in all housemates in the subset having `IsChef = true` in the day plan response.

**Validates: Requirements 2.5, 2.6, 3.1, 3.3, 4.1**

### Property 5: Chef toggle creates correctly attributed history entry

*For any* chef status toggle by any acting housemate for any target housemate on any date, the system should create a `DayHistory` entry with `ChangeType.ChefStatusChanged`, `ChangedByHousemateId` equal to the acting housemate's ID, and the entry associated with the target date.

**Validates: Requirements 4.2, 8.1, 8.2, 8.3**

## Error Handling

| Scenario | Behavior |
|---|---|
| Target housemate does not exist | `UpsertChefStatusAsync` returns `false`; function returns 404 with `ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound)`. No history entry created. |
| Target housemate is soft-deleted | Same as above — treated as not found for chef operations. |
| Invalid date format in route | `RouteParser.TryParseDate` returns error; function returns 400. |
| Invalid GUID in route | `RouteParser.TryParseGuid` returns error; function returns 400. |
| Missing/malformed request body | `RequestValidator.ReadAndValidateAsync` returns error; function returns 400/422. |
| Table Storage transient failure | Exception propagates; Azure Functions runtime returns 500. Frontend rolls back optimistic UI and shows error toast. |
| Concurrent chef toggles | Last-write-wins via Table Storage upsert semantics. No conflict detection needed. |
| Auto-chef race condition | Two users save dish simultaneously, both see no chef, both fire chef endpoint. Last-write-wins means one ends up as chef — acceptable behavior. |
| No attendance record exists when toggling chef | `UpsertChefStatusAsync` creates a new attendance record with `Status = Unknown` and the given `IsChef` value. |

## Testing Strategy

### Property-Based Tests (FsCheck)

The feature's core business logic (chef toggle round-trip, independence guarantees, history attribution) is well-suited for property-based testing. The `DayHandler` methods are pure business logic operating on repository interfaces that can be mocked.

- **Library**: FsCheck 3.1+ (already in use in the project)
- **Minimum iterations**: 100 per property
- **Tag format**: `// Feature: chef-toggle, Property {N}: {property_text}`
- **Test location**: `Happie.Api.Tests/Handlers/DayHandlerChefTests.cs`

Each of the 5 correctness properties maps to a single property-based test. Generators will produce:
- Random `Guid` values for household, housemate, and acting housemate IDs
- Random `DateOnly` values
- Random `AttendanceStatus` values
- Random boolean values (for initial and toggled chef states)
- Random subsets of housemate lists
- Random non-empty sequences of boolean values (for last-write-wins)

### Unit Tests (xUnit)

Unit tests cover specific examples, edge cases, and UI behavior:

- **Handler edge cases**: Non-existent housemate returns false, soft-deleted housemate returns false, no attendance record exists yet (creates one with Unknown status)
- **Function layer**: Route parsing errors, request validation errors, correct delegation to handler
- **Mapper**: Round-trip entity ↔ domain conversion including `IsChef` field
- **Frontend component**: Optimistic UI update, rollback on failure, disabled state during save, aria-pressed attribute values, CSS class toggling, auto-chef detection logic

### Integration Tests

- **Repository**: Verify `AttendanceRepository.UpsertChefStatusAsync` correctly reads/writes to Azure Table Storage (Azurite), preserving existing `Status` when only `IsChef` changes
- **End-to-end**: Verify the full PUT endpoint creates/updates the record and history entry

### Test File Structure

| File | Contents |
|---|---|
| `Happie.Api.Tests/Handlers/DayHandlerChefTests.cs` | Property-based tests for Properties 1–5 |
| `Happie.Api.Tests/Handlers/DayHandlerChefUnitTests.cs` | Unit tests for edge cases (not-found, soft-deleted, no existing record) |
| `Happie.Api.Tests/Functions/DaysFunctionChefTests.cs` | Unit tests for the PutChefStatusAsync function |
| `Happie.Api.Tests/Infrastructure/Mappers/AttendanceRecordMapperChefTests.cs` | Mapper round-trip tests verifying IsChef is mapped correctly |
| `Happie.Api.IntegrationTests/Repositories/AttendanceRepositoryChefTests.cs` | Integration tests for UpsertChefStatusAsync against Azurite |
