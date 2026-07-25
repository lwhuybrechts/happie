---
inclusion: fileMatch
fileMatchPattern: "**/Happie.Api/**,**/Happie.Shared/**"
---

# Happie — API Conventions

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

---

## Configuration — Options Pattern (MUST follow)

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
