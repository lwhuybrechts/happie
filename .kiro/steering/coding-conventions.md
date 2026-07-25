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
