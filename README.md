# WiseLabels — Instant Quote Razor Pages App

A Razor Pages web application that provides an instant-quote UI for custom labels and acts as a server-side proxy to CERM APIs (materials, printing/color codes, finishing types). The project centralizes OAuth handling and API mapping server-side to avoid CORS and credential exposure to clients.

## Quick summary
- Framework: ASP.NET Core Razor Pages
- .NET Target: .NET 8
- Frontend: Vanilla JS served from `wwwroot/js` (e.g. `site.js`, `finish.js`, `quote.js`, `submit.js`)
- Server-side API proxies: Razor Pages under `Pages/Api/*` (e.g. `FinishingTypes`)
- HTTP clients: `IHttpClientFactory` + token management for CERM OAuth

## Repository layout (important files)
- `Pages/Index.cshtml` — main quote page and form UI
- `Pages/Api/FinishingTypes.cshtml.cs` — proxy endpoint that reads `Cerm:FinishingTypesUrl` and returns normalized JSON
- `wwwroot/js/finish.js` — populates the `#finish` dropdown from `/Api/FinishingTypes`
- `wwwroot/js/quote.js`, `wwwroot/js/submit.js` — form behavior and submit logic
- `appsettings.Development.json` (example credentials kept here for local dev)

## Prerequisites
- Visual Studio 2026 (or dotnet CLI)
- .NET 8 SDK
- Access to CERM endpoints (OAuth + parameter APIs) and corresponding client credentials
- Optional: SMTP accessible by the app for email notifications

## Configuration
Store environment-specific configuration in `appsettings.Development.json` (local dev) and production configuration in environment variables or `appsettings.Production.json`. Required configuration keys (CERM section):

- `Cerm:OAuthUrl` — OAuth token endpoint (default: `https://brandmark-api.cerm.be/oauth/token`)
- `Cerm:FinishingTypesUrl` — finishing types endpoint (used by `FinishingTypes` proxy)
- `Cerm:MaterialsUrl` — materials endpoint (if used elsewhere)
- `Cerm:ColorCodesUrl` — color codes endpoint
- `Cerm:Username` — API username
- `Cerm:Password` — API password
- `Cerm:ClientId` — OAuth client id
- `Cerm:ClientSecret` — OAuth client secret

Email configuration (optional):
- `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpUsername`, `Email:SmtpPassword`, `Email:FromEmail`, `Email:FromName`

Security notes:
- Do NOT commit production secrets. Use environment variables, Azure Key Vault, AWS Secrets Manager, or user secrets for local development.
- The server-side proxy endpoints require the above credentials and will return 500/401 errors if not present.

Example snippet for local dev (already in repo):
```
{
  "Cerm": {
    "CustomerID": "108620",
    "ContactID": "001",
    "OAuthUrl": "https://brandmark-api.cerm.be/oauth/token",
    "MaterialsUrl": "https://brandmark-api.cerm.be/parameter-api/v1/calculation/substrates",
    "FinishingTypesUrl": "https://brandmark-api.cerm.be/api/v1/finishingtypes",
    "ColorCodesUrl": "https://brandmark-api.cerm.be/parameter-api/v1/calculation/front-adhesive-backing/colour-codes?Filter=AllowRFQ%20eq%20true%20and%20Blocked%20ne%20true",
    "Username": "CermAPI",
    "Password": "Testerke.96145",
    "ClientId": "A8C706636C584336B2CDCF399FAA9605",
    "ClientSecret": "secret"
  }
}
```

## How it works
- Client requests finishing types -> frontend `finish.js` calls `/Api/FinishingTypes`.
- `FinishingTypesModel` reads config, authenticates to CERM (multiple strategies attempted), calls the configured `FinishingTypesUrl`, normalizes the JSON into `ParameterResponse` objects, filters `AllowRFQ`, sorts by `Descriptions` where `ISOLanguageCode == "en-US"`, then returns JSON to the client.
- Frontend uses the `Descriptions` array (en-US entry) to populate the `#finish` dropdown.

## Running locally
1. Ensure .NET 8 SDK is installed.
2. Configure secrets (recommended): `dotnet user-secrets` or environment variables for `Cerm:*`.
3. Open solution in Visual Studio 2026 or run:
   - `dotnet build`
   - `dotnet run --project <project-folder>`
4. Navigate to `https://localhost:5001` (or port shown).

## Program / DI requirements
- `IHttpClientFactory` must be registered (default in `WebApplication.CreateBuilder`).
- Logging and `IConfiguration` are used by `Pages/Api/FinishingTypes.cshtml.cs` — ensure they are available (normal in Razor Pages template).

## Deployment
- Publish using `dotnet publish -c Release`.
- Host on IIS, Kestrel behind a reverse proxy, or Docker.
- For Docker: copy configuration via environment variables or secret mounts.
- Ensure network access to CERM endpoints from the deployment environment.

## Frontend notes & common issues
- The app relies on DOM elements (IDs such as `send-quote-btn`, `finish`, `material`, `printing-filter`). Scripts must be loaded after DOM parsing or use `defer`/`DOMContentLoaded` wrappers.
- Symptom: `TypeError: Cannot read properties of null (reading 'addEventListener')` typically means a script executed before the DOM element existed (or the element id changed). Fix by:
  - Including script tags at the end of the page, or
  - Adding `defer` to `<script>` tags, or
  - Wrapping initialization in `document.addEventListener('DOMContentLoaded', ...)`.
- Script order: include `site.js`, then `finish.js`, then `quote.js` so dropdowns are ready before other logic that depends on them.

## Logs & troubleshooting
- The server logs OAuth and material mapping details. Look at application logs for:
  - OAuth failures (401/400 responses)
  - JSON parsing issues (unexpected shape)
- On JSON parsing issues, `FinishingTypesModel` attempts several wrapper shapes (`Data`, `items`, `results`) and has a `ManualMapMaterials` fallback.
- If the dropdown shows placeholder text or empty, confirm:
  - `/Api/FinishingTypes` returns 200 and contains `materials`
  - App has valid CERM credentials and network access
  - Browser console shows no JS errors and `finish.js` executed

## Contributing & maintenance
- Keep API URLs and credentials configurable — do not hard-code production secrets.
- Add unit/integration tests around `FinishingTypesModel` helpers (token acquisition and JSON mapping).
- Keep frontend initialization defensive (null-check DOM queries before calling `addEventListener`).

## Contact / More info
- For integration with additional CERM endpoints, add new Razor Page proxies under `Pages/Api/` following the same authentication pattern.


