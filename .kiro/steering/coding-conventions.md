# Happie — Coding Conventions

## Project Structure Conventions (MUST follow)

### Namespace layout

| Location | Namespace | Contents |
|---|---|---|
| `Happie.Shared/Domain/` | `Happie.Shared.Domain` | Shared enums and constants used by both client and server: `AttendanceStatus`, `ChangeType`, `NudgeMessageKey`, `Locale`, `HousemateColors` |
| `Happie.Shared/Contracts/` | `Happie.Shared.Contracts` | HTTP wire format types shared between client and server: request bodies, response envelopes, DTOs |
| `Happie.Shared/Validation/` | `Happie.Shared.Validation` | Shared DataAnnotations validation attributes: `ValidEnumAttribute` |
| `Happie.Api/Domain/` | `Happie.Api.Domain` | Server-only business objects used by handlers and repositories: `Housemate`, `Household`, `AttendanceRecord`, `DishRecord`, `Comment`, `DayHistoryEntry`, `PushSubscription`, `NudgeRequest` |
| `Happie.Api/Results/` | `Happie.Api.Results` | Internal handler return types: `LoginResult`, `UpdateHousemateResult`, `DeleteHousemateOutcome`, `UpdateHousemateOutcome` |
| `Happie.Api/Infrastructure/Entities/` | `Happie.Api.Infrastructure.Entities` | Table Storage entity classes |
| `Happie.Api/Infrastructure/Mappers/` | `Happie.Api.Infrastructure.Mappers` | Mapper interfaces and implementations |
| `Happie.Api/Infrastructure/Repositories/` | `Happie.Api.Infrastructure.Repositories` | Repository interfaces and implementations |
| `Happie.Api/Handlers/` | `Happie.Api.Handlers` | Business logic handlers |
| `Happie.Api/Http/` | `Happie.Api.Http` | HTTP infrastructure helpers: `ReadResult<T>`, `RequestValidator`, `RouteParser` |
| `Happie.Api/Functions/` | `Happie.Api.Functions` | Thin HTTP controller functions |

### Naming conventions for contract types

Types in `Happie.Shared/Contracts/` follow these naming rules:

- **Top-level response envelopes** use the `Response` suffix: `DayPlanResponse`, `CalendarResponse`, `LoginResponse`
- **Nested pieces of a response** (embedded in a top-level response) use the `Dto` suffix: `AttendanceDto`, `CommentDto`, `DishDto`, `HistoryEntryDto`, `HousemateDto`, `CalendarDayDto`
- **Request bodies** use the `Request` suffix: `LoginRequest`, `AddHousemateRequest`, `UpdateHousemateRequest`, `UpdateAttendanceRequest`, `UpdateDishRequest`, `UpdateCommentRequest`

### Dependency direction

```
Functions → Handlers → Domain ← Infrastructure
    ↓                    ↑
   Http              Contracts (shared with client)
```

- `Domain` does NOT depend on `Infrastructure`
- `Infrastructure` depends on `Domain` (maps entities to/from domain types)
- `Handlers` depend on `Domain` and `Infrastructure` (via repository interfaces)
- `Http` contains HTTP infrastructure helpers used by `Functions` only
- `Functions` depend on `Handlers`, `Contracts`, and `Http`
- `Happie.Shared.Domain` (enums/constants) is a dependency of both `Happie.Api.Domain` and `Happie.Shared.Contracts`

---

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

| Entity | PartitionKey | RowKey |
|---|---|---|
| `HouseholdEntity` | `"households"` | `{HouseholdId}` |
| `HousemateEntity` | `{HouseholdId}` | `{HousemateId}` |
| `AttendanceRecordEntity` | `{HouseholdId}` | `{YYYY-MM-DD}_{HousemateId}` |
| `DishRecordEntity` | `{HouseholdId}` | `{YYYY-MM-DD}` |
| `CommentEntity` | `{HouseholdId}` | `{YYYY-MM-DD}_{HousemateId}` |
| `DayHistoryEntity` | `{HouseholdId}` | `{YYYY-MM-DD}_{InvertedTimestamp}` |
| `PushSubscriptionEntity` | `{HouseholdId}` | `{HousemateId}` |

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

| Interface | Class | Table |
|---|---|---|
| `IHouseholdRepository` | `HouseholdRepository` | `Households` |
| `IHousemateRepository` | `HousemateRepository` | `Housemates` |
| `IAttendanceRepository` | `AttendanceRepository` | `AttendanceRecords` |
| `IDishRepository` | `DishRepository` | `DishRecords` |
| `ICommentRepository` | `CommentRepository` | `Comments` |
| `IDayHistoryRepository` | `DayHistoryRepository` | `DayHistory` |
| `IPushSubscriptionRepository` | `PushSubscriptionRepository` | `PushSubscriptions` |

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

| Interface | Class | Domain type |
|---|---|---|
| `IHouseholdMapper` | `HouseholdMapper` | `Household` |
| `IHousemateMapper` | `HousemateMapper` | `Housemate` |
| `IAttendanceRecordMapper` | `AttendanceRecordMapper` | `AttendanceRecord` |
| `IDishRecordMapper` | `DishRecordMapper` | `DishRecord` |
| `ICommentMapper` | `CommentMapper` | `Comment` |
| `IDayHistoryEntryMapper` | `DayHistoryEntryMapper` | `DayHistoryEntry` |
| `IPushSubscriptionMapper` | `PushSubscriptionMapper` | `PushSubscription` |

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

---

## Azure Functions Conventions (MUST follow)

### General

- HTTP-triggered Azure Functions, isolated worker model
- Stateless request processing
- Configuration via `local.settings.json` for local development — **never commit this file**

### Request body reading and validation

All functions that accept a request body MUST use `RequestValidator.ReadAndValidateAsync<T>` from `Happie.Api.Http`. This centralises deserialisation, null-checking, and DataAnnotations validation into a single call.

```csharp
// ✅ GOOD: single call handles deserialisation, null check, and validation.
var readResult = await RequestValidator.ReadAndValidateAsync<MyRequest>(request, cancellationToken);
if (!readResult.IsSuccess)
    return readResult.Error;

// readResult.Body is guaranteed non-null here.
await _handler.HandleAsync(readResult.Body.SomeProperty, cancellationToken);
```

- `ReadResult<T>` uses `[MemberNotNullWhen]` so the compiler knows `Body` is non-null when `IsSuccess` is true and `Error` is non-null when `IsSuccess` is false — no null-forgiving operators needed
- NEVER use `req.ReadFromJsonAsync` directly in a function — always go through `RequestValidator`

### Route parameter parsing

All route and query parameter parsing MUST use `RouteParser` from `Happie.Api.Http`:

```csharp
if (!RouteParser.TryParseDate(date, out var parsedDate, out var error))
    return error;

if (!RouteParser.TryParseGuid(housemateId, out var parsedHousemateId, out var guidError))
    return guidError;
```

When the generic error message from `RouteParser` is not appropriate (e.g. query parameters with custom messages), discard the `out` error with `_`:

```csharp
if (!RouteParser.TryParseDate(fromString, out var from, out _))
    return new BadRequestObjectResult(new ApiErrorResponse("Query parameter 'from' must be in yyyy-MM-dd format.", ApiErrorCodes.BadRequest));
```

### DataAnnotations on request contracts

Request contracts in `Happie.Shared/Contracts/` MUST declare their validation rules using DataAnnotations attributes. These are enforced automatically by `RequestValidator.ReadAndValidateAsync`.

- Use `[Required]` for mandatory string fields
- Use `[MaxLength(n)]` for length-limited fields — validates the raw (pre-trim) value
- Use `[MinLength(n)]` for minimum-length collections
- Use `[ValidEnum]` from `Happie.Shared.Validation` for enum properties

```csharp
public record UpdateDishRequest(
    [property: JsonPropertyName("description")]
    [property: MaxLength(100, ErrorMessage = "Dish description must be at most 100 characters.")]
    string Description);

public record UpdateAttendanceRequest(
    [property: JsonPropertyName("status")]
    [property: ValidEnum(ErrorMessage = "Invalid attendance status.")]
    AttendanceStatus Status);
```

### Functions as Thin Controllers

Function classes act as thin controllers only. Business logic MUST be delegated to handler/service classes.

**Function responsibilities:**
- Parse and validate route/query parameters via `RouteParser`
- Read and validate request bodies via `RequestValidator.ReadAndValidateAsync`
- Delegate to handler/service classes for business logic
- Handle HTTP-specific concerns (status codes, headers, responses)
- Return appropriate HTTP responses

**Handler/service responsibilities:**
- All business logic and domain operations
- Interaction with repositories and external services
- Complex validation and processing
- Error handling for business logic

```csharp
// ❌ BAD: manual deserialisation and validation in the function.
public async Task<IActionResult> Run(HttpRequest request, CancellationToken cancellationToken)
{
    MyRequest? body;
    try { body = await request.ReadFromJsonAsync<MyRequest>(cancellationToken); }
    catch { return new BadRequestObjectResult(...); }
    if (body is null) return new BadRequestObjectResult(...);
    if (string.IsNullOrWhiteSpace(body.Name)) return new UnprocessableEntityObjectResult(...);
    ...
}

// ✅ GOOD: use RequestValidator and RouteParser, delegate to handler.
public async Task<IActionResult> Run(
    HttpRequest request, string date, CancellationToken cancellationToken)
{
    if (!RouteParser.TryParseDate(date, out var parsedDate, out var routeError))
        return routeError;

    var readResult = await RequestValidator.ReadAndValidateAsync<MyRequest>(request, cancellationToken);
    if (!readResult.IsSuccess)
        return readResult.Error;

    var result = await _handler.HandleAsync(parsedDate, readResult.Body.Name, cancellationToken);
    return new OkObjectResult(result);
}
```

### Error Responses

All HTTP error responses MUST use `ApiErrorResponse` and `ApiErrorCodes` — never anonymous objects:

```csharp
// ❌ BAD: anonymous object.
return new NotFoundObjectResult(new { error = "Not found.", code = "NOT_FOUND" });

// ✅ GOOD: typed record with constant code.
return new NotFoundObjectResult(new ApiErrorResponse("Housemate not found.", ApiErrorCodes.NotFound));
```

- `ApiErrorResponse` is a record in `Happie.Shared/Contracts/` — namespace `Happie.Shared.Contracts` — with `[JsonPropertyName]` attributes for lowercase wire format
- `ApiErrorCodes` is a static class in `Happie.Api/Constants/` with `const string` values
- Exhaustive enum switches that reach an unhandled arm MUST throw `InvalidOperationException`, not return a 500 response:

```csharp
_ => throw new InvalidOperationException($"Unhandled {nameof(MyOutcome)}: {outcome}"),
```

## Test Conventions (MUST follow)

### Shared test helpers

Helpers shared across multiple test classes MUST live in their own file, not be duplicated. For example, `HttpRequestFactory` in `Happie.Api.Tests/Functions/` provides `HttpRequestFactory.Create<T>(body)` for all function tests that need to build an `HttpRequest` with a JSON body.

### File naming

`{ClassUnderTest}Tests.cs`

### One class/record per file

Every class, record, and interface MUST live in its own file. This applies to production code and test helpers alike. Never define multiple types in a single `.cs` file.

### Test method naming

Use the **Act_Arrange_Assert** pattern. **Act (the method under test) comes first** so tests group alphabetically by the method being tested:

```
MethodUnderTest_Scenario_ExpectedOutcome
```

Examples:
- `HandleAsync_CorrectPassword_ReturnsActiveHousemates`
- `Run_NullBody_ReturnsBadRequest`
- `TryValidateToken_ExpiredToken_Rejects`

❌ BAD: `CorrectPassword_HandleAsync_ReturnsHousemates` — scenario first breaks alphabetical grouping
✅ GOOD: `HandleAsync_CorrectPassword_ReturnsActiveHousemates` — act first, groups with all other `HandleAsync_*` tests

### System under test naming

The instance of the class being tested MUST be named `_sut` (System Under Test). This makes it immediately clear which object is the focus of the test.

```csharp
// ❌ BAD: named after the type.
private readonly LoginHandler _handler;
private readonly LoginFunction _function;

// ✅ GOOD: always _sut.
private readonly LoginHandler _sut;
```

### Mock field initialization

Mock dependencies MUST be initialized inline at the field declaration using `new()`, not inside the constructor. This keeps the constructor focused solely on wiring up `_sut`.

```csharp
// ❌ BAD: initialized in constructor.
private readonly Mock<ILoginHandler> _loginHandlerMock;

public LoginFunctionTests()
{
    _loginHandlerMock = new Mock<ILoginHandler>();
    _sut = new LoginFunction(_loginHandlerMock.Object);
}

// ✅ GOOD: initialized inline, constructor only wires _sut.
private readonly Mock<ILoginHandler> _loginHandlerMock = new();

public LoginFunctionTests()
{
    _sut = new LoginFunction(_loginHandlerMock.Object);
}
```

### Setup and create helper methods

To keep test methods readable, extract mock setups and object construction into **private helper methods** at the bottom of the test class. This removes noise from the Arrange section and makes helpers reusable across tests.

- **Setup methods** configure mock behavior. Name them `Setup{MethodName}(...)`.
- **Create methods** construct domain objects or DTOs. Name them `Create{TypeName}(...)`. Make them `static` when they don't reference instance fields.
- Only add a value as a parameter to a create method if the test needs to assert or reference that specific value. If a property is irrelevant to the test, define it inside the create method instead.
- Place all helper methods **at the bottom of the class**, after all test methods. All setup methods come first, followed by all create methods — no separating comments.

```csharp
// ❌ BAD: householdId is passed in but never asserted — it's noise in the test.
var loginResult = CreateLoginResult(token, housemateId, householdId);

private static LoginResult CreateLoginResult(string token, Guid housemateId, Guid householdId) =>
    new(token, new List<Housemate>
    {
        new(housemateId, householdId, "Alice", HousemateColors.Palette[0], false),
    });

// ✅ GOOD: householdId is irrelevant to the test, so it lives inside the create method.
var loginResult = CreateLoginResult(token, housemateId);

private static LoginResult CreateLoginResult(string token, Guid housemateId) =>
    new(token, new List<Housemate>
    {
        new(housemateId, Guid.NewGuid(), "Alice", HousemateColors.Palette[0], false),
    });
```

```csharp
// ✅ GOOD: Arrange is concise; all setup methods first, then create methods at the bottom.
[Fact]
public async Task HandleAsync_CorrectPassword_ReturnsActiveHousemates()
{
    // Arrange.
    var housemates = CreateHousemates(householdId, aliceId, bobId);
    SetupGetAllHouseholds(new List<Household> { new(householdId, "Test Household", passwordHash) });
    SetupGetAllHousemates(householdId, housemates);

    // Act.
    var result = await _sut.HandleAsync("correct-password");

    // Assert.
    housemates.ToExpectedObject().ShouldEqual(result!.Housemates);
}

private void SetupGetAllHouseholds(List<Household> returns)
{
    _householdRepositoryMock
        .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(returns);
}

private void SetupGetAllHousemates(Guid householdId, List<Housemate> returns)
{
    _housemateRepositoryMock
        .Setup(r => r.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(returns);
}

private static List<Housemate> CreateHousemates(Guid householdId, Guid aliceId, Guid bobId) =>
    new()
    {
        new(aliceId, householdId, "Alice", HousemateColors.Palette[0], false),
        new(bobId, householdId, "Bob", HousemateColors.Palette[1], false),
    };
```

Every test body MUST contain the three section comments exactly as shown:

```csharp
// Arrange.
// Act.
// Assert.
```

### Assertions — prefer single assert with ExpectedObjects

- Prefer **one assert per test** — when a test has multiple asserts, only the first failure is reported, hiding subsequent failures.
- When asserting multiple properties of an object, use the **`ExpectedObjects`** package instead of multiple `Assert.*` calls.
- Add `ExpectedObjects` to the test project: `dotnet add package ExpectedObjects --version 2.2.0`
- **Prefer actual typed objects over anonymous objects** when comparing with `ExpectedObjects`. This improves discoverability and ensures tests break when properties are renamed.

```csharp
// ❌ BAD: multiple asserts — later failures are hidden when the first one fails.
Assert.Equal("Alice", result.Name);
Assert.Equal(housemateId, result.Id);
Assert.Equal("#FF0000", result.Color);

// ❌ ALSO BAD: anonymous object — property renames silently break the comparison.
new { Name = "Alice", Id = housemateId, Color = "#FF0000" }
    .ToExpectedObject()
    .ShouldEqual(new { Name = result.Name, Id = result.Id, Color = result.Color });

// ✅ GOOD: actual typed object — rename-safe and discoverable.
new Housemate(housemateId, householdId, "Alice", "#FF0000", false)
    .ToExpectedObject()
    .ShouldEqual(result);
```

Use plain `Assert.*` only when asserting a single scalar value (e.g., `Assert.Null`, `Assert.True`, `Assert.IsType`).

### Full example

```csharp
private readonly LoginHandler _sut;

[Fact]
public async Task HandleAsync_CorrectPassword_ReturnsActiveHousemates()
{
    // Arrange.
    var householdId = Guid.NewGuid();
    _repositoryMock.Setup(...).ReturnsAsync(...);

    // Act.
    var result = await _sut.HandleAsync("correct-password");

    // Assert.
    new LoginResult("expected-token", expectedHousemates)
        .ToExpectedObject()
        .ShouldEqual(result);
}
```

### Test isolation

- **DO NOT use `IDisposable`** for Azure Table Storage test cleanup — it doesn't guarantee execution order
- **ALWAYS use `TableHelper.TruncateTable`** in the constructor to clean tables BEFORE each test
- Truncate all tables the test will use to ensure a clean state

```csharp
public class MyRepositoryTests
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly IMyRepository _repository;

    public MyRepositoryTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? "UseDevelopmentStorage=true";

        _tableServiceClient = new TableServiceClient(connectionString);

        // Truncate tables BEFORE test execution.
        TableHelper.TruncateTable(_tableServiceClient, "MyTable");
        TableHelper.TruncateTable(_tableServiceClient, "RelatedTable");

        _repository = new MyRepository(_tableServiceClient, NullLogger<MyRepository>.Instance);
    }

    [Fact]
    public async Task MyTest_ShouldWork()
    {
        // Test implementation.
    }
}
```

### Integration tests — Azurite prerequisite (MUST follow)

Integration tests that hit Azure Table Storage use the local Azurite emulator. **Azurite must be running before executing `dotnet test`**, otherwise all integration tests will fail with a connection error.

Start Azurite before running tests:

```bash
azurite --silent
```

Or use the **Azurite extension** in VS Code / Visual Studio, which starts it automatically when the workspace opens.

The connection string used by integration tests defaults to `"UseDevelopmentStorage=true"` when the `AZURE_STORAGE_CONNECTION_STRING` environment variable is not set — this points to the local Azurite instance on the default ports (Blob: 10000, Queue: 10001, Table: 10002).

### Integration tests — disable parallel execution (MUST follow)

Any integration test project that shares Azure Table Storage tables across test classes MUST disable xUnit's parallel test execution. Without this, multiple test class constructors truncate the same tables simultaneously, causing cross-test contamination.

Add an `AssemblyInfo.cs` file to the integration test project with the following content:

```csharp
using Xunit;

// Disable parallel test execution for integration tests because they share Azure Table Storage tables.
// Running tests in parallel causes cross-test contamination when multiple test class constructors
// truncate and write to the same tables simultaneously.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

This file already exists in `Happie.Api.IntegrationTests/AssemblyInfo.cs`. Any new integration test project that uses shared tables MUST include the same file.

### Property-based tests (FsCheck)

- Use **FsCheck 3.1+** which supports `async` property callbacks natively — **never use `GetAwaiter().GetResult()`**
- Property test methods return `Task<Property>`; the lambda passed to `Prop.ForAll` is `async`; call `.ToProperty()` on the result
- Mid-iteration cleanup (`DeleteAsync` etc.) is sometimes necessary even when tables are truncated at construction time, because FsCheck runs all iterations within a single test invocation — data written in iteration N persists into iteration N+1

```csharp
[Property(MaxTest = 100)]
public Property MyRepository_SomeProperty()
{
    return Prop.ForAll(
        MyArb(),
        async input =>
        {
            await _repository.UpsertAsync(input);
            var result = await _repository.GetAsync(input.Id);

            // Clean up if leftover data could affect subsequent iterations.
            await _repository.DeleteAsync(input.Id);

            return (result != null)
                .Label($"Expected to find {input.Id} after upsert");
        });
}
```

---

## Code Conventions (MUST follow)

### Braces

- **Omit braces for single-statement `if`, `else`, `for`, `foreach`, `while` bodies**
- The statement goes on the next line, indented
- ❌ BAD:
  ```csharp
  if (result is null)
  {
      return null;
  }
  ```
- ✅ GOOD:
  ```csharp
  if (result is null)
      return null;
  ```
- Exception: always use braces when the body spans multiple lines or when an `if`/`else` chain mixes single-line and multi-line bodies

### One type per file

Every class, record, interface, and enum MUST live in its own `.cs` file. Never define multiple types in a single file, even for small DTOs or request/response records.

- ❌ BAD: `LoginFunction.cs` containing `LoginRequest`, `LoginResponse`, `HousemateDto`, and `LoginFunction`
- ✅ GOOD: `LoginRequest.cs`, `LoginResponse.cs`, `HousemateDto.cs`, `LoginFunction.cs` — one type each

### Comments

- All comments MUST end with a period at the end of sentences
- Applies to single-line (`//`), multi-line (`/* */`), and XML documentation (`///`) comments
- **NEVER place a comment at the end of a line of code** — always place comments on the line above the code they describe
- Example: `// This is a correct comment.`
- Example: `/// <summary>This is correct.</summary>`
- ❌ BAD: `public string Name { get; set; } // The housemate's name.`
- ✅ GOOD:
  ```csharp
  // The housemate's name.
  public string Name { get; set; }
  ```

### Namespaces

- Namespace MUST match folder structure exactly
- Example: file at `Happie.Api/Handlers/AttendanceHandler.cs` → namespace `Happie.Api.Handlers`

### Nullable Reference Types

- Enabled project-wide
- Use `?` for all nullable references
- Initialize non-nullable properties with default values or in constructor

### LINQ Style

- **Always use method syntax** (`.Where(...)`, `.Select(...)`, `.All(...)`, etc.)
- **Never use query syntax** (`from x in ...`, `where`, `select` keywords)
- **Single non-nested lambda variable MUST be named `x`**. Use descriptive names only when lambdas are nested and need to be distinguished.
- ❌ BAD: `from a in gen from b in gen where a != b select (a, b)`
- ✅ GOOD: `gen.SelectMany(a => gen.Where(b => b != a).Select(b => (a, b)))` — nested, so `a`/`b` are acceptable
- ❌ BAD: `entities.Select(e => _mapper.ToModel(e))`
- ✅ GOOD: `entities.Select(x => _mapper.ToModel(x))`
- ❌ BAD: `households.FirstOrDefault(h => BCrypt.Verify(password, h.PasswordHash))`
- ✅ GOOD: `households.FirstOrDefault(x => BCrypt.Verify(password, x.PasswordHash))`

### Implicit Usings

- Common namespaces are auto-imported (`System`, `System.Collections.Generic`, etc.)
- Do not add redundant using statements for implicit namespaces

### Variable naming

- **Never use abbreviations in variable names** — use full, descriptive names
- ❌ BAD: `ct`, `req`, `fromStr`, `toStr`, `read`
- ✅ GOOD: `cancellationToken`, `request`, `fromString`, `toString`, `readResult`
- Exception: loop variables and LINQ lambda parameters follow the existing LINQ style rule (`x` for single non-nested lambdas)

### Configuration — Options Pattern (MUST follow)

- Sensitive settings go in `local.settings.json` (gitignored) — never hardcode secrets or connection strings
- Use environment variables / Key Vault for production configuration
- **ALWAYS use the Options Pattern** — NEVER inject `IConfiguration` directly into services
- Create strongly-typed options classes in `Happie.Api/Options/`
- Options class naming: `{Feature}Options.cs` (e.g., `JwtOptions`, `VapidOptions`)
- Include a `const string SectionName` property for the configuration section name
- Add `DataAnnotations` validation attributes to options properties (`[Required]`, `[Range]`, etc.)
- Register options using `.AddOptionsWithValidateOnStart<TOptions>()` to validate at startup, not at first use
- Inject options into services using `IOptions<TOptions>`

```csharp
using System.ComponentModel.DataAnnotations;

public class VapidOptions
{
    public const string SectionName = "Vapid";

    [Required(ErrorMessage = "Vapid:PublicKey is required.")]
    public string PublicKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vapid:PrivateKey is required.")]
    public string PrivateKey { get; set; } = string.Empty;
}
```

```csharp
// Registration with startup validation.
services.Configure<VapidOptions>(configuration.GetSection(VapidOptions.SectionName))
        .AddOptionsWithValidateOnStart<VapidOptions>();
```

```csharp
// Injection into a service.
public class PushNotificationService
{
    private readonly VapidOptions _options;

    public PushNotificationService(IOptions<VapidOptions> options)
    {
        _options = options.Value;
    }
}
```
