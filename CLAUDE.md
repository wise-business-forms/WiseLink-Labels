# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WiseLabels is an ASP.NET Core 8 Razor Pages application that provides an instant-quote UI for custom labels. It acts as a server-side proxy to CERM APIs (materials, printing/color codes, finishing types, customers, estimates) to avoid CORS and credential exposure. The frontend is vanilla JavaScript served from `wwwroot/js/`.

## Build & Run Commands

```bash
dotnet build                    # Build the project
dotnet run                      # Run locally (uses appsettings.Development.json)
dotnet publish -c Release       # Publish for deployment
```

VS Code launch configs are in `.vscode/launch.json` (Chrome debug, no-browser, attach).

## Architecture

### Request Flow

1. **Azure AD authentication** via Microsoft Identity Web (OpenID Connect) — all pages require auth
2. **Authorization** uses group-based policies (`FullAccess`, `LimitedAccess`) configured in `Authorization/`
3. **Razor Pages** under `Pages/` handle UI; `Pages/Api/` contains server-side proxy endpoints
4. **Services** (`Services/`) contain business logic: `QuoteService`, `EmailService`, `CermAuthService`, `ChatService`
5. **Data access** via Entity Framework Core through the `CERM.DataAccess` project reference (`CermDbContext`, repository pattern)

### Quote Flow

GetQuote → user selects specs (printing, material, finishing, dimensions) → Confirm → submits to CERM API → Success → email notification sent.

### Key Architectural Decisions

- **All CERM API calls are server-side** — OAuth tokens are acquired and cached in `CermAuthService` (dual strategy: body-only and basic-auth + body)
- **PDF generation** uses DinkToPdf (wkhtmltopdf wrapper). Native DLLs live in `Libraries/{32bit,64bit}/` and are loaded via `CustomAssemblyLoadContext`
- **Session** stores quote state (30-minute idle timeout). Session key constants are in `SessionKeys.cs`
- **Configuration-driven** — all API URLs, credentials, filtering rules, and authorization groups come from `appsettings.json` sections (`Cerm`, `AzureAd`, `Authorization`, `QuoteOptions`, `Chat`)

### Frontend

- Vanilla JS files in `wwwroot/js/` — no build step or bundler
- Quote form logic is split across `quote/quote.js`, `quote/quote-api.js`, `quote/quote-autocomplete.js`, `quote/quote-helpers.js`, `quote/quote-validation.js`
- Scripts depend on DOM element IDs (`send-quote-btn`, `finish`, `material`, `printing-filter`) — must load after DOM or use `defer`
- Script load order matters: `site.js` → `finish.js` → `quote.js`

### API Proxy Pattern

Each endpoint in `Pages/Api/` follows the same pattern: authenticate to CERM via `CermAuthService`, call the configured URL, normalize the JSON response (attempting multiple wrapper shapes: `Data`, `items`, `results`), filter by `AllowRFQ`, sort by English description, and return JSON. New CERM proxies should follow this same pattern.

## External Dependencies

- **CERM APIs** — OAuth token endpoint + parameter/calculation endpoints (configured in `Cerm:*` settings)
- **SQL Server** — CERM database via EF Core (connection string: `CermDatabase`)
- **Ollama** (optional) — local LLM for chat feature at `localhost:11434`
- **SMTP** (optional) — for email notifications

## Documentation

Detailed business rules, API integration guides, and database schema references are in `Documentation/`:
- `BUSINESS_RULES.md` — filtering logic, shape/die-cutting rules, material constraints
- `USING_CERM_API.md` — API integration patterns
- `CERM_tables_columns.json` — full database schema reference
