---
inclusion: fileMatch
fileMatchPattern: "**/Infrastructure/**"
---

# Happie — Entity, Repository & Mapper Conventions

## Azure Table Storage Entity Conventions (MUST follow)

All Table Storage entity classes MUST adhere to the following rules:

- Inherit from `MyTableEntity` base class
- Live in `Happie.Api/Infrastructure/Entities/` — namespace `Happie.Api.Infrastructure.Entities`
- File naming: `{Domain}Entity.cs` (e.g., `HousemateEntity.cs`, `AttendanceRecordEntity.cs`)
- Include a parameterless constructor for Azure Table Storage deserialization
- Include a parameterized constructor that sets `PartitionKey` and `RowKey`
- Set partition/row keys in the parameterized constructor for optimal Table Storage queries
- Use nullable reference types (`?`) for optional properties
- Use `= string.Empty` as default for required string properties
- **NEVER use `DateTimeOffset?` (nullable) for entity properties** — the Azure.Data.Tables SDK does not serialize nullable `DateTimeOffset?` on strongly-typed `ITableEntity` classes. Use non-nullable `DateTimeOffset` instead, with `default` (`DateTimeOffset.MinValue`) as the sentinel for "not set". The mapper converts `default` back to `null` in the domain type.
- **Enum properties are stored as their integer value** — Use the enum type directly on entity properties (e.g., `public AttendanceStatus Status { get; set; }`). Azure Table Storage serializes them as the underlying `int` value. NEVER store enums as strings. When defining new enums, only append new members at the end — never reorder or remove existing members — to preserve compatibility with stored integer values.
- Entity classes are internal to the repository layer — NEVER reference them outside `Happie.Api/Infrastructure/`

```csharp
public class ExampleEntity : MyTableEntity
{
    public ExampleEntity() { } // Required for deserialization

    public ExampleEntity(string partitionKey, string rowKey)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;
    }

    public string? OptionalProperty { get; set; }
    public string RequiredProperty { get; set; } = string.Empty;
}
```

### PartitionKey / RowKey patterns for Happie entities

<!-- MAINTENANCE: when adding a new entity, add a row to this table. -->

| Entity | PartitionKey | RowKey |
|---|---|---|
| `HouseholdEntity` | `"households"` | `{HouseholdId}` |
| `HousemateEntity` | `{HouseholdId}` | `{HousemateId}` |
| `AttendanceRecordEntity` | `{HouseholdId}` | `{YYYY-MM-DD}_{HousemateId}` |
| `DishRecordEntity` | `{HouseholdId}` | `{YYYY-MM-DD}` |
| `CommentEntity` | `{HouseholdId}` | `{YYYY-MM-DD}_{HousemateId}` |
| `DayHistoryEntity` | `{HouseholdId}` | `{YYYY-MM-DD}_{InvertedTimestamp}` |
| `PushSubscriptionEntity` | `{HouseholdId}` | `{HousemateId}` |
| `SavedDishEntity` | `{HouseholdId}` | `{SavedDishId}` |
| `DayPlanDishLinkEntity` | `{HouseholdId}` | `{YYYY-MM-DD}_{SavedDishId}` |

`DayHistory` uses an inverted timestamp (`DateTimeOffset.MaxValue.Ticks - entry.ChangedAt.Ticks`) so entries are returned in reverse-chronological order by default.

---

## Repository Pattern Conventions (MUST follow)

All data access MUST go through repository classes. Handlers and services MUST NOT use `ITableStorageClient` directly.

### Structure

- Abstract base class: `BaseRepository<TEntity>` in `Happie.Api/Infrastructure/Repositories/` where `TEntity : MyTableEntity`
- Each concrete repository lives in `Happie.Api/Infrastructure/Repositories/` and has a matching interface
- File naming: `{Domain}Repository.cs` and `I{Domain}Repository.cs` (e.g., `HousemateRepository.cs`, `IHousemateRepository.cs`)
- Table name is defined as `private const string TableName` in each concrete repository
- Repository interfaces and implementations work exclusively with **domain types** from `Happie.Api.Domain` — entity types MUST NOT appear in any interface or public method signature
- Each repository injects a mapper interface to handle entity ↔ domain type conversion

### Concrete repositories

<!-- MAINTENANCE: when adding a new repository, add a row to this table. -->

| Interface | Class | Table |
|---|---|---|
| `IHouseholdRepository` | `HouseholdRepository` | `Households` |
| `IHousemateRepository` | `HousemateRepository` | `Housemates` |
| `IAttendanceRepository` | `AttendanceRepository` | `AttendanceRecords` |
| `IDishRepository` | `DishRepository` | `DishRecords` |
| `ICommentRepository` | `CommentRepository` | `Comments` |
| `IDayHistoryRepository` | `DayHistoryRepository` | `DayHistory` |
| `IPushSubscriptionRepository` | `PushSubscriptionRepository` | `PushSubscriptions` |
| `ISavedDishRepository` | `SavedDishRepository` | `SavedDishes` |
| `IDayPlanDishLinkRepository` | `DayPlanDishLinkRepository` | `DayPlanDishLinks` |

### Registration

All mappers and repositories are registered as singletons in `Program.cs`. Mappers must be registered before repositories:

```csharp
// Register all mappers as singletons.
builder.Services.AddSingleton<IHousemateMapper, HousemateMapper>();
// ... repeat for each mapper

// Register all repositories as singletons.
builder.Services.AddSingleton<IHousemateRepository, HousemateRepository>();
// ... repeat for each repository
```

### Example

```csharp
// BaseRepository.cs
public abstract class BaseRepository<TEntity> where TEntity : MyTableEntity
{
    private readonly ITableStorageClient _client;
    private readonly string _tableName;

    protected BaseRepository(ITableStorageClient client, string tableName)
    {
        _client = client;
        _tableName = tableName;
    }

    protected Task UpsertAsync(TEntity entity, CancellationToken ct = default)
        => _client.UpsertAsync(_tableName, entity, ct);

    protected Task<TEntity?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default)
        => _client.GetAsync<TEntity>(_tableName, partitionKey, rowKey, ct);

    protected Task DeleteAsync(string partitionKey, string rowKey, CancellationToken ct = default)
        => _client.DeleteAsync(_tableName, partitionKey, rowKey, ct);

    protected Task<IReadOnlyList<TEntity>> QueryByPartitionAsync(string partitionKey, CancellationToken ct = default)
        => _client.QueryByPartitionAsync<TEntity>(_tableName, partitionKey, ct);

    protected Task<IReadOnlyList<TEntity>> QueryByRowKeyPrefixAsync(string partitionKey, string prefix, CancellationToken ct = default)
        => _client.QueryByRowKeyPrefixAsync<TEntity>(_tableName, partitionKey, prefix, ct);
}

// HousemateRepository.cs — injects mapper, returns domain types
public class HousemateRepository : BaseRepository<HousemateEntity>, IHousemateRepository
{
    private const string TableName = "Housemates";
    private readonly IHousemateMapper _mapper;

    public HousemateRepository(ITableStorageClient client, IHousemateMapper mapper) : base(client, TableName)
    {
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<Housemate>> GetAllAsync(Guid householdId, CancellationToken ct = default)
    {
        var entities = await QueryByPartitionAsync(householdId.ToString(), ct);
        return entities.Select(e => _mapper.ToModel(householdId, e)).ToList();
    }

    public Task UpsertAsync(Housemate housemate, CancellationToken ct = default)
        => UpsertAsync(_mapper.ToEntity(housemate), ct);
}
```

### Dependency injection in handlers

```csharp
// ✅ GOOD: inject the repository interface, not ITableStorageClient
public class HousemateHandler : IHousemateHandler
{
    private readonly IHousemateRepository _housemateRepository;

    public HousemateHandler(IHousemateRepository housemateRepository)
    {
        _housemateRepository = housemateRepository;
    }
}
```

---

## Mapper Conventions (MUST follow)

Each repository has a dedicated mapper class responsible for converting between the Table Storage entity and the domain type. This keeps all key-encoding knowledge in one place and out of both the repository and the handler.

### Structure

- Mapper interfaces and implementations live in `Happie.Api/Infrastructure/Mappers/` — namespace `Happie.Api.Infrastructure.Mappers`
- File naming: `{Domain}Mapper.cs` and `I{Domain}Mapper.cs` (e.g., `HousemateMapper.cs`, `IHousemateMapper.cs`)
- Each mapper exposes exactly two methods: `ToModel(...)` and `ToEntity(...)`
- Mappers are stateless and registered as singletons

### Mapper table

<!-- MAINTENANCE: when adding a new mapper, add a row to this table. -->

| Interface | Class | Domain type |
|---|---|---|
| `IHouseholdMapper` | `HouseholdMapper` | `Household` |
| `IHousemateMapper` | `HousemateMapper` | `Housemate` |
| `IAttendanceRecordMapper` | `AttendanceRecordMapper` | `AttendanceRecord` |
| `IDishRecordMapper` | `DishRecordMapper` | `DishRecord` |
| `ICommentMapper` | `CommentMapper` | `Comment` |
| `IDayHistoryEntryMapper` | `DayHistoryEntryMapper` | `DayHistoryEntry` |
| `IPushSubscriptionMapper` | `PushSubscriptionMapper` | `PushSubscription` |
| `ISavedDishMapper` | `SavedDishMapper` | `SavedDish` |
| `IDayPlanDishLinkMapper` | `DayPlanDishLinkMapper` | `DayPlanDishLink` |

### Example

```csharp
// IHousemateMapper.cs
public interface IHousemateMapper
{
    Housemate ToModel(Guid householdId, HousemateEntity entity);
    HousemateEntity ToEntity(Housemate housemate);
}

// HousemateMapper.cs
public class HousemateMapper : IHousemateMapper
{
    public Housemate ToModel(Guid householdId, HousemateEntity entity) =>
        new(Guid.Parse(entity.RowKey), householdId, entity.Name, entity.Color, entity.IsDeleted);

    public HousemateEntity ToEntity(Housemate housemate)
    {
        var entity = new HousemateEntity(housemate.HouseholdId, housemate.Id);
        entity.Name = housemate.Name;
        entity.Color = housemate.Color;
        entity.IsDeleted = housemate.IsDeleted;
        return entity;
    }
}
```
