# Happie — Coding Conventions

## Azure Table Storage Entity Conventions (MUST follow)

All Table Storage entity classes MUST adhere to the following rules:

- Inherit from `MyTableEntity` base class
- File naming: `{Domain}Entity.cs` (e.g., `HousemateEntity.cs`, `AttendanceRecordEntity.cs`)
- Include a parameterless constructor for Azure Table Storage deserialization
- Include a parameterized constructor that sets `PartitionKey` and `RowKey`
- Set partition/row keys in the parameterized constructor for optimal Table Storage queries
- Use nullable reference types (`?`) for optional properties
- Use `= string.Empty` as default for required string properties

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
| `AttendanceRecordEntity` | `{HouseholdId}` | `{YYYY-MM-DD}#{HousemateId}` |
| `DishRecordEntity` | `{HouseholdId}` | `{YYYY-MM-DD}` |
| `CommentEntity` | `{HouseholdId}` | `{YYYY-MM-DD}#{HousemateId}` |
| `DayHistoryEntity` | `{HouseholdId}` | `{YYYY-MM-DD}#{InvertedTimestamp}` |
| `PushSubscriptionEntity` | `{HouseholdId}` | `{HousemateId}` |

## Azure Functions Conventions (MUST follow)

### General

- HTTP-triggered Azure Functions, isolated worker model
- Stateless request processing
- Configuration via `local.settings.json` for local development — **never commit this file**

### Functions as Thin Controllers

Function classes act as thin controllers only. Business logic MUST be delegated to handler/service classes.

**Function responsibilities:**
- Parse and validate HTTP requests
- Deserialize request payloads
- Delegate to handler/service classes for business logic
- Handle HTTP-specific concerns (status codes, headers, responses)
- Log HTTP-level errors
- Return appropriate HTTP responses

**Handler/service responsibilities:**
- All business logic and domain operations
- Interaction with repositories and external services
- Complex validation and processing
- Error handling for business logic

```csharp
// ❌ BAD: Business logic in Function class
public class MyFunction
{
    [Function("MyEndpoint")]
    public async Task<IActionResult> Run(HttpRequest req)
    {
        var data = await req.ReadFromJsonAsync<MyData>();
        // ❌ Don't do complex processing here
        var result = await _repository.GetAsync(data.Id);
        var processed = ProcessComplexLogic(result);
        return new OkObjectResult(processed);
    }
}

// ✅ GOOD: Delegate to handler
public class MyFunction
{
    private readonly IMyHandler _handler;

    [Function("MyEndpoint")]
    public async Task<IActionResult> Run(HttpRequest req)
    {
        var data = await req.ReadFromJsonAsync<MyData>();
        if (data == null) return new BadRequestResult();
        var result = await _handler.HandleAsync(data);
        return new OkObjectResult(result);
    }
}
```

## Test Conventions (MUST follow)

### File naming

`{ClassUnderTest}Tests.cs`

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

---

## Code Conventions (MUST follow)

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

### Implicit Usings

- Common namespaces are auto-imported (`System`, `System.Collections.Generic`, etc.)
- Do not add redundant using statements for implicit namespaces

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
