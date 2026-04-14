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

Distributors can share a URL with their end customers that shows the distributor's
own branding (logo, company name, contact info) instead of WiseLink Labels branding.
Each distributor gets their own subdomain on `labels-tags.com` (e.g. `abc-printing.labels-tags.com`).

**How it works:**
1. Each distributor has a profile in `Sites:WhiteLabel:Distributors` config (Azure App Settings / Key Vault).
2. The `Subdomain` field in the profile maps to a subdomain on the configured `ApexDomain` (`labels-tags.com`).
3. `SubdomainDistributorMiddleware` reads the `Host` header, extracts the subdomain label,
   and resolves the matching `DistributorProfile` into `HttpContext.Items`.
4. `Gate.cshtml.cs` picks up the profile from `HttpContext.Items` (no token in URL required).
5. `_WhiteLabelLayout.cshtml` reads the profile from session and renders the distributor's logo,
   company name, and contact info in the header on every page.
6. A "Powered by WiseLink Labels" attribution appears in the footer.

**Adding a new distributor** — add a profile to `Sites__WhiteLabel__Distributors__N__*` in Azure App Settings
(include `Subdomain: "abc-printing"` for `abc-printing.labels-tags.com`).
See the "Adding a New Distributor — Checklist" in `Documentation/PUBLIC_HIDDEN_SITES_PLAN.md` (Section 8).

**Azure DNS setup** — a single wildcard CNAME (`*.labels-tags.com → wiselabels-whitelabel.azurewebsites.net`)
and wildcard managed TLS certificate cover all distributor subdomains.
See "Azure Setup Additions for White-Label" in Section 8 of the plan.

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
      "ApexDomain": "localhost",
      "Distributors": [
        {
          "Slug": "test-dist",
          "Subdomain": "test-dist",
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

**Subdomain routing locally:** Add `test-dist.localhost` to your `hosts` file:
```
127.0.0.1   test-dist.localhost
```
Then navigate to: `https://test-dist.localhost:5001/gate`

**Token fallback locally:** navigate to `https://localhost:5001/gate/local-wl-token`

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
