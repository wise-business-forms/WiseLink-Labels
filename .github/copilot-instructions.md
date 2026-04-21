# WiseLabels – GitHub Copilot Instructions

## Project Overview
ASP.NET Core 8 Razor Pages app that provides an instant-quote UI for custom labels and acts as a server-side proxy to CERM APIs (materials, color codes, finishing types, cutting dies). Authentication is via Azure AD (OIDC/MSAL). PDF generation uses DinkToPdf (native `libwkhtmltox`).

---

## Build & Run

```bash
# Build
dotnet build WiseLabels.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary

# Watch (hot-reload dev)
dotnet watch run --project WiseLabels.csproj

# Publish
dotnet publish WiseLabels.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary

# Plain run
dotnet run
```

There is **no test project**. `?testing=1` query param or a `localhost` hostname enables a testing mode flag in `GetQuoteModel`.

---

## Architecture

### Component Boundaries

```
Pages/             ← Razor Pages (UI + page models)
Pages/Api/         ← Internal JSON API layer (Razor Pages returning JsonResult)
Services/          ← Business logic and external integrations
Authorization/     ← Custom ASP.NET Core authorization handlers
Models/            ← Shared request/response DTOs
wwwroot/js/        ← Vanilla JS frontend
Libraries/         ← Native DinkToPdf DLLs (32bit/64bit)
```

### External Dependency
`CERM.DataAccess` is a **sibling project** (not in this repo). It provides `CermDbContext`, EF Core models, and `IJobRepository`/`ISubstrateRepository`. Registered in `Program.cs` via `builder.Services.AddCermDataAccessEF(builder.Configuration)`.

### Key Services (all Scoped)

| Interface | Implementation | Purpose |
|---|---|---|
| `ICermAuthService` | `CermAuthService` | CERM OAuth2 password-grant token acquisition |
| `IQuoteService` | `QuoteService` | Quote payload building and CERM API submission |
| `IEmailService` | `EmailService` | SMTP email dispatch |
| `IChatService` | `ChatService` | Ollama LLM streaming/structured chat |

Singletons: `IConverter` (DinkToPdf `SynchronizedConverter`), `IAuthorizationHandler` (`GroupAccessHandler`).

### API Proxy Pages (`Pages/Api/`)
Each returns `JsonResult` and uses `[IgnoreAntiforgeryToken]`. They proxy CERM REST endpoints or query the legacy SQL DB directly.

- **`/Api/Materials`**, **`/Api/ColorCodes`**, **`/Api/FinishingTypes`**, **`/Api/CuttingDie`** — CERM REST proxies
- **`/Api/Customers`** — raw SQL via `CermDbContext` (`SELECT ... FROM sqlb00.dbo.klabas__`)
- **`/Api/Estimates`** — EF or CERM API
- **`/Api/ChatStream`** — Ollama via `IChatService`

---

## Coding Conventions

### Naming
- Interfaces: `I` prefix (`IQuoteService`, `ICermAuthService`)
- Page models: `{PageName}Model` (`GetQuoteModel`, `MaterialsModel`)
- Namespace for services: `WiseLabels.Services`
- Namespace for auth: `WiseLabels.Authorization`
- Namespace for API proxy models: `WiseLabels.Pages.Api`

### Dependency Injection
- Constructor injection everywhere
- All dependencies: `private readonly` fields with underscore prefix (`_logger`, `_configuration`, `_httpClientFactory`)

### Error Handling
- Services return `null` or `(result, errorMessage)` tuples on failure
- API page models return `JsonResult` with `{ error: "..." }` + non-200 HTTP status
- No global exception filter (default `/Error` page)

### Async
- All I/O is `async Task<T>`; `await` consistently; no `.Result` or `.Wait()`

### Session & TempData
- All session key constants live in [SessionKeys.cs](../SessionKeys.cs)
- `QuoteRequest` serialized to JSON and passed between pages via `TempData` or session

---

## Frontend Conventions

Plain **vanilla JS**, no bundler, no TypeScript, no framework.

```
wwwroot/js/
  site.js                  # global UI utilities
  finish.js                # finish/material dropdown loading
  submit.js                # quote form submit handler
  quote/
    quote.js               # main quote form logic (shape/size/die toggle UI)
    quote-api.js           # QuoteApi static class (API fetch wrappers)
    quote-helpers.js       # utility helpers
    quote-validation.js    # inline field validation
    quote-autocomplete.js  # customer/die autocomplete
```

Key patterns:
- `document.addEventListener('DOMContentLoaded', ...)` wraps all initialization
- `fetch()` with relative `/Api/{Resource}` URLs
- Dual inputs for CERM IDs: a display text input (e.g. `#printing-input`) and hidden value input (e.g. `#printing-value`)
- Printing method selected via button group; active state tracked with `.active` CSS class
- Loading/error states toggled via `style.display` on dedicated DOM elements

---

## Authorization

Three-layer system:

1. **Global fallback:** `RequireAuthenticatedUser()` — every page requires Azure AD login (OIDC)
2. **Named policies:** `"FullAccess"` (admin) and `"LimitedAccess"` (regular users)
3. **`GroupAccessHandler`** resolves claims from `ClaimTypes.Role`, `ClaimTypes.GroupSid`, `"groups"`, or `"roles"` claims against `Authorization:FullAccessGroups` / `LimitedAccessGroups` config arrays (name or GUID). User overrides in `FullAccessUsers` / `LimitedAccessUsers`. Full access implies limited access (hierarchical).

Use `[Authorize(Policy = "FullAccess")]` or `[Authorize(Policy = "LimitedAccess")]` on page models as needed.

---

## Configuration

| Section | Purpose |
|---|---|
| `AzureAd` | OIDC TenantId, ClientId, callback paths |
| `Authorization` | Group/user access lists |
| `Cerm` | API base URL, OAuth URL, credentials, per-resource endpoint URLs, `CustomerID`, `ContactID` |
| `ConnectionStrings:CermDatabase` | SQL Server connection to CERM legacy DB |
| `Email` | SMTP settings |
| `QuoteOptions:PrintingFinishFilters` | Dict mapping printing key → allowed finish names |
| `Chat` | Ollama endpoint, model name, system prompt parts |

Pattern: `_configuration["Section:Key"]` for simple reads; `IOptions<T>` / `.GetSection().Get<T>()` for structured options (`AccessControlOptions`, quote filter options).

**No production secrets in source.** Use environment variables, Azure Key Vault, or `dotnet user-secrets` for local dev. `appsettings.Development.json` contains dev credentials only.

---

## Known Pitfalls

- **DinkToPdf startup crash:** `Libraries/32bit/` or `Libraries/64bit/libwkhtmltox.dll` must exist at runtime. The app throws on startup if missing. Windows-only.

- **`CermApi.cs` is legacy:** `Pages/Api/CermApi.cs` duplicates CERM OAuth logic. **New API proxy pages must use injected `ICermAuthService`**, not this file.

- **`StoreQuoteAsync` is a stub:** Returns a new GUID without persisting — has a `TODO` comment in `QuoteService.cs`.

- **Raw SQL in `/Api/Customers`:** `FromSqlRaw(...)` hardcodes `sqlb00.dbo.klabas__`. Schema changes in the legacy DB will break this silently.

- **`IQuoteService` interface lives in `QuoteService.cs`** (not a separate `IQuoteService.cs` file).

- **Connection string name mismatch:** `appsettings.json` key is `CermDatabase` but `Program.cs` calls `GetConnectionString("CermDbConnection")` — the working name comes from environment-specific config.

- **CERM OAuth two-strategy pattern:** Both `CermAuthService` and `CermApi.cs` deliberately try two OAuth request formats (body-only, then Basic Auth + body) to handle CERM server variability — this is intentional, not a bug.

- **Session middleware order:** `UseSession()` is called after `UseAuthorization()` — unconventional but intentional.
