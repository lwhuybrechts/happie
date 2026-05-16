---
inclusion: fileMatch
fileMatchPattern: "**/Happie.Web.Tests/**"
---

# Happie — bUnit Component Testing Patterns

## Test Project Setup

The bUnit test project is `Happie.Web.Tests`. It already has:
- `bunit` 2.0.33-preview
- `Moq` for mocking
- `RichardSzalay.MockHttp` for HTTP mocking
- `FsCheck.Xunit` for property-based tests
- `ProjectReference` to `Happie.Web`

No additional packages or references are needed for new component tests.

## Base Class

All bUnit tests inherit from `BunitContext` (not `TestContext` — that's the older bUnit 1.x API).

```csharp
public class MyComponentTests : BunitContext
```

## JavaScript Interop (localStorage / sessionStorage)

Blazor components in this project use raw `IJSRuntime` calls for storage (not a wrapper interface). Use bUnit's built-in `JSInterop` to mock them.

### Loose mode (default for most tests)

```csharp
JSInterop.Mode = JSRuntimeMode.Loose;
```

Loose mode returns `default` for any unmocked JS call. This means `localStorage.getItem` returns `null` by default — which puts LoginPage in the password form state.

### Explicit setup (when you need specific return values)

```csharp
JSInterop.Setup<string?>("localStorage.getItem", "jwt").SetResult("test-jwt-token");
JSInterop.Setup<string?>("localStorage.getItem", "activeHousemateId").SetResult(null);
JSInterop.Setup<string?>("sessionStorage.getItem", "pendingHousemates").SetResult(serializedJson);
```

### Extract repeated JS interop setups into private Setup methods

When multiple tests need the same localStorage/sessionStorage state, extract it into a `Setup{StateName}` method. This follows the project's existing convention for setup helpers.

```csharp
// ❌ BAD: repeated inline in every test.
JSInterop.Setup<string?>("localStorage.getItem", "jwt").SetResult("existing-jwt-token");
JSInterop.Setup<string?>("localStorage.getItem", "activeHousemateId").SetResult(null);
JSInterop.Setup<string?>("sessionStorage.getItem", "pendingHousemates").SetResult(serialized);

// ✅ GOOD: extracted into a named setup method.
private void SetupJsInteropForHousemateSelection(List<HousemateDto> housemates)
{
    var serializedHousemates = JsonSerializer.Serialize(housemates);
    JSInterop.Setup<string?>("localStorage.getItem", "jwt").SetResult("existing-jwt-token");
    JSInterop.Setup<string?>("localStorage.getItem", "activeHousemateId").SetResult(null);
    JSInterop.Setup<string?>("sessionStorage.getItem", "pendingHousemates").SetResult(serializedHousemates);
}
```

Common session states for LoginPage tests:
- `SetupJsInteropForNoSession()` — no JWT, no activeHousemateId (password form state)
- `SetupJsInteropForHousemateSelection(housemates)` — JWT present, housemates in sessionStorage (selection view)
- `SetupJsInteropForFullyAuthenticated()` — JWT + activeHousemateId (triggers redirect)

### Asserting JS calls were made

```csharp
var setItemInvocations = JSInterop.Invocations
    .Where(x => x.Identifier == "localStorage.setItem")
    .ToList();
var targetInvocation = setItemInvocations
    .First(x => x.Arguments.Count >= 2 && x.Arguments[0]?.ToString() == "activeHousemateId");
Assert.Equal(expectedValue, targetInvocation.Arguments[1]?.ToString());
```

## Registering Services

### LocaleService

`LocaleService` takes `IJSRuntime` in its constructor. Register it as a factory so it resolves from the container:

```csharp
Services.AddSingleton(serviceProvider =>
    new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
```

Do NOT mock `LocaleService` — it's a concrete class, not an interface. Its JS calls go through bUnit's JSInterop.

### IStringLocalizer

Two approaches depending on what you need:

**Option A — Mock (returns key as value, good for structural tests):**
```csharp
private readonly Mock<IStringLocalizer<AppStrings>> _localizerMock = new();

// In constructor:
_localizerMock
    .Setup(x => x[It.IsAny<string>()])
    .Returns((string key) => new LocalizedString(key, key));
_localizerMock
    .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
    .Returns((string key, object[] _) => new LocalizedString(key, key));
Services.AddSingleton(_localizerMock.Object);
```

**Option B — Real localization (uses actual resx files):**
```csharp
Services.AddLocalization();
```

The test project copies resx files to output via `<Content>` items in the csproj, so real localization works.

### HttpClient

**Preferred: Use the shared extension method (simple status + body):**

The project has a reusable `MockHttpMessageHandler` at `Happie.Web.Tests/Helpers/MockHttpMessageHandler.cs` and a `BunitContextExtensions.RegisterHttpClient` extension method at `Happie.Web.Tests/Helpers/BunitContextExtensions.cs`. Use these instead of writing your own:

```csharp
using Happie.Web.Tests.Helpers;

// In test method (note: extension methods on `this` require explicit `this.`):
this.RegisterHttpClient(HttpStatusCode.OK, myResponseObject);
this.RegisterHttpClient(HttpStatusCode.Unauthorized);
```

This registers an `HttpClient` with `BaseAddress = "http://localhost/api/"` that returns the given status code and optional JSON-serialized body for all requests.

**With RichardSzalay.MockHttp (when you need fine-grained request matching or delayed responses):**
```csharp
private readonly RichardSzalay.MockHttp.MockHttpMessageHandler _mockHttp = new();

// In constructor:
var httpClient = _mockHttp.ToHttpClient();
httpClient.BaseAddress = new Uri("http://localhost/api/");
Services.AddSingleton(httpClient);

// In test:
_mockHttp.When("/api/auth/login").Respond(HttpStatusCode.Unauthorized);
```

Use `RichardSzalay.MockHttp` only when you need per-URL matching, delayed responses (`TaskCompletionSource`), or request inspection. For everything else, prefer the shared `this.RegisterHttpClient(...)` extension.

## Known bUnit 2.0.33-preview Quirks

### EditForm conditional removal bug

When an `EditForm` with `DataAnnotationsValidator` is conditionally removed from the render tree during an event handler (e.g., successful login hides the form and shows housemate list), bUnit's renderer can throw. **Workaround:** test the post-transition state by setting up the component in that state directly (e.g., JWT in localStorage + housemates in sessionStorage) rather than submitting the form.

### NavigationManager assertions

Use `BunitNavigationManager` to assert navigation:
```csharp
var navigationManager = Services.GetRequiredService<NavigationManager>();
var bunitNav = (BunitNavigationManager)navigationManager;
var lastNav = bunitNav.History.Last();
Assert.Contains($"/day/{today}", lastNav.Uri);
Assert.True(lastNav.Options.ForceLoad); // for locale switch
```

## Waiting for Async State Changes

When a form submission triggers an async operation, use `WaitForState`:
```csharp
form.Submit();
cut.WaitForState(() => cut.FindAll("[role=alert]").Count > 0);
```

## Testing Disabled Button During Async Operation

Use `TaskCompletionSource` to keep the HTTP response pending:
```csharp
var tcs = new TaskCompletionSource<HttpResponseMessage>();
_mockHttp.When("/api/auth/login").Respond(_ => tcs.Task);

// Submit the form without awaiting.
_ = cut.InvokeAsync(() => form.Submit());

// Assert button is disabled while request is in flight.
Assert.True(submitButton.HasAttribute("disabled"));

// Clean up.
tcs.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
```

## FsCheck Property Tests with bUnit

For property-based tests that render components:
- Create a fresh `BunitContext` per iteration (or use a test class that inherits `BunitContext`)
- Generate `HousemateDto` values with non-empty names and valid hex color strings
- Render the component in the housemate selection state (JWT + housemates in sessionStorage)
- Assert structural properties (avatar color, sort order, element presence)

Tag format: `// Feature: {feature-name}, Property {N}: {description}`

### Extract repeated BunitContext setup into private helper methods

Property tests that create a fresh `BunitContext` per iteration MUST extract the repeated context configuration (service registration, JS interop setup, HTTP client registration) into private helper methods — the same pattern used in regular bUnit test classes.

```csharp
// ❌ BAD: repeated inline in every property test body.
using var context = new BunitContext();
context.JSInterop.Mode = JSRuntimeMode.Loose;
context.Services.AddSingleton(serviceProvider =>
    new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
context.Services.AddLocalization();
var serializedHousemates = JsonSerializer.Serialize(housemates);
context.JSInterop.Setup<string?>("localStorage.getItem", "jwt").SetResult("existing-jwt-token");
context.JSInterop.Setup<string?>("localStorage.getItem", "activeHousemateId").SetResult(null);
context.JSInterop.Setup<string?>("sessionStorage.getItem", "pendingHousemates").SetResult(serializedHousemates);
context.RegisterHttpClient(HttpStatusCode.Unauthorized, null);

// ✅ GOOD: extracted into reusable private methods at the bottom of the class.
var context = CreateBunitContext();
SetupJsInteropForHousemateSelection(context, housemates);
```

Helper methods for property tests take `BunitContext` as a parameter (since the class doesn't inherit `BunitContext`):

```csharp
private static BunitContext CreateBunitContext()
{
    var context = new BunitContext();
    context.JSInterop.Mode = JSRuntimeMode.Loose;
    context.Services.AddSingleton(serviceProvider =>
        new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
    context.Services.AddLocalization();
    context.RegisterHttpClient(HttpStatusCode.Unauthorized, null);
    return context;
}

private static void SetupJsInteropForHousemateSelection(BunitContext context, List<HousemateDto> housemates)
{
    var serializedHousemates = JsonSerializer.Serialize(housemates);
    context.JSInterop.Setup<string?>("localStorage.getItem", "jwt").SetResult("existing-jwt-token");
    context.JSInterop.Setup<string?>("localStorage.getItem", "activeHousemateId").SetResult(null);
    context.JSInterop.Setup<string?>("sessionStorage.getItem", "pendingHousemates").SetResult(serializedHousemates);
}
```

## Shared Test Helpers (`Happie.Web.Tests/Helpers/`)

Reusable utilities live in `Happie.Web.Tests/Helpers/`. Always check here before writing new infrastructure code.

| File | What it provides |
|---|---|
| `MockHttpMessageHandler.cs` | Simple `HttpMessageHandler` that returns a fixed status code and optional JSON body. |
| `BunitContextExtensions.cs` | `this.RegisterHttpClient(statusCode, content?)` extension method — registers an `HttpClient` using `MockHttpMessageHandler` with `BaseAddress = "http://localhost/api/"`. |

When adding new shared helpers, place them in this folder with the namespace `Happie.Web.Tests.Helpers`.
