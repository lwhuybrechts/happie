---
inclusion: fileMatch
fileMatchPattern: "**/*Tests*/**,**/*Test*/**"
---

# Happie — Testing Conventions

## General

- Unit tests: xUnit
- Property-based tests: FsCheck, minimum 100 iterations per property
- Each property test must be tagged: `// Feature: happie, Property {N}: {property_text}`
- Both client-side and server-side validation must be enforced for all field length rules

## Shared test helpers

Helpers shared across multiple test classes MUST live in their own file, not be duplicated. For example, `HttpRequestFactory` in `Happie.Api.Tests/Functions/` provides `HttpRequestFactory.Create<T>(body)` for all function tests that need to build an `HttpRequest` with a JSON body.

## File naming

`{ClassUnderTest}Tests.cs`

## One class/record per file

Every class, record, and interface MUST live in its own file. This applies to production code and test helpers alike. Never define multiple types in a single `.cs` file.

## Test method naming

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

## System under test naming

The instance of the class being tested MUST be named `_sut` (System Under Test). This makes it immediately clear which object is the focus of the test.

```csharp
// ❌ BAD: named after the type.
private readonly LoginHandler _handler;
private readonly LoginFunction _function;

// ✅ GOOD: always _sut.
private readonly LoginHandler _sut;
```

## Mock field initialization

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

## Setup and create helper methods

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

## Assertions — prefer single assert with ExpectedObjects

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

## Full example

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

## Test isolation

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

## Integration tests — Azurite prerequisite (MUST follow)

Integration tests that hit Azure Table Storage use the local Azurite emulator. **Azurite must be running before executing `dotnet test`**, otherwise all integration tests will fail with a connection error.

**CRITICAL: When you need to run integration tests, you MUST start Azurite as a background process BEFORE running `dotnet test`.** Do NOT skip integration tests because Azurite is not running — start it yourself. Refer to `.kiro/steering/local-dev.md` for the full startup procedure and ports.

The connection string used by integration tests defaults to `"UseDevelopmentStorage=true"` when the `AZURE_STORAGE_CONNECTION_STRING` environment variable is not set — this points to the local Azurite instance on the default ports (Blob: 10000, Queue: 10001, Table: 10002).

## Integration tests — disable parallel execution (MUST follow)

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

## Property-based tests (FsCheck)

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
