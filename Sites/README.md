# Sites — Public Portal Projects

This folder contains the three public-facing, hidden-URL portals for WiseLink Labels.
Each portal serves a distinct customer segment but shares the core Razor Pages,
services, and static assets through the **WiseLabels.Shared** Razor Class Library.

## Structure

```
Sites/
├── Shared/          Razor Class Library — common pages, services, models, assets
│   ├── Middleware/  PortalGateMiddleware (used by all four portals)
│   ├── Models/      DistributorProfile (white-label branding model)
│   └── Services/    IDistributorProfileService / DistributorProfileService
├── Dist/            Print Distributor portal  →  distributor.wiselabels.com
├── EndUser/         End User / Order portal   →  order.wiselabels.com
├── Partner/         Channel Partner portal    →  partner.wiselabels.com
└── WhiteLabel/      Distributor white-label portal  →  order.wiselabels.com  (shared URL)
```

## How Access Works

Each portal uses a **token-in-URL gate** pattern:

1. An authorized customer is given a private URL such as:
   `https://distributor.wiselabels.com/gate/abc123def456`
2. The `Gate.cshtml` page validates the token against `Sites:<Segment>:ValidTokens` in configuration.
3. If valid, a one-click acknowledgement checkbox is shown (similar to an age-verification gate).
4. On acknowledgement, a session cookie (`dist_access`, `enduser_access`, or `partner_access`) is written.
5. The `PortalGateMiddleware` in `Sites/Shared/Middleware/` enforces that all other pages require this cookie.

## White-Label Portal

Distributors can share a private URL with their end customers that shows the distributor's
own branding (logo, company name, contact info) instead of WiseLink Labels branding.

**How it works:**
1. Each distributor has a profile in `Sites:WhiteLabel:Distributors` config (Azure App Settings / Key Vault).
2. The distributor hands their customers a URL like `https://order.wiselabels.com/gate/<distributor-token>`.
3. `Gate.cshtml.cs` looks up the token → `DistributorProfile`, stores it in server-side session.
4. `_WhiteLabelLayout.cshtml` reads the profile from session and renders the distributor's logo,
   company name, and contact info in the header on every page.
5. A "Powered by WiseLink Labels" attribution appears in the footer.

**Adding a new distributor** — add a profile to `Sites__WhiteLabel__Distributors__N__*` in Azure App Settings.
See the "Adding a New Distributor — Checklist" in `Documentation/PUBLIC_HIDDEN_SITES_PLAN.md` (Section 8).

## Running Locally

```bash
# Distributor portal
cd Sites/Dist
dotnet run

# End User portal
cd Sites/EndUser
dotnet run

# Partner portal
cd Sites/Partner
dotnet run

# White-label portal
cd Sites/WhiteLabel
dotnet run
```

Add valid tokens to each project's `appsettings.Development.json` (do **not** commit real tokens):

```json
// Sites/Dist/appsettings.Development.json
{
  "Sites": {
    "Dist": {
      "ValidTokens": [ "local-dev-token" ]
    }
  }
}
```

For the white-label portal:

```json
// Sites/WhiteLabel/appsettings.Development.json
{
  "Sites": {
    "WhiteLabel": {
      "Distributors": [
        {
          "Slug": "test-dist",
          "CompanyName": "Test Distributor Co.",
          "LogoUrl": "/img/wise-logo-blue.png",
          "LogoAlt": "Test Distributor",
          "ContactName": "Jane Doe",
          "ContactPhone": "(555) 000-1111",
          "ContactEmail": "jane@testdist.com",
          "PrimaryColor": "#003399",
          "ReferencePrefix": "TEST-",
          "Tokens": [ "local-wl-token" ]
        }
      ]
    }
  }
}
```

Then navigate to: `https://localhost:5001/gate/local-wl-token`

## Deployment

See [Documentation/PUBLIC_HIDDEN_SITES_PLAN.md](../Documentation/PUBLIC_HIDDEN_SITES_PLAN.md)
for the full Azure setup guide (Sections 4 and 8).

GitHub Actions workflows for each portal are in `.github/workflows/`:
- `deploy-dist.yml`
- `deploy-enduser.yml`
- `deploy-partner.yml`
- `deploy-whitelabel.yml`

## Token Management

- Tokens are stored in Azure App Service **Environment Variables** (or Azure Key Vault) only — **never committed to the repository**.
- Generate tokens with a tool like [random.org](https://www.random.org/strings/) or PowerShell:
  ```powershell
  -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 24 | % { [char]$_ })
  ```
- Rotate tokens at least annually or when a distribution batch is retired.
