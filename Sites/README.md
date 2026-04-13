# Sites — Public Portal Projects

This folder contains the three public-facing, hidden-URL portals for WiseLink Labels.
Each portal serves a distinct customer segment but shares the core Razor Pages,
services, and static assets through the **WiseLabels.Shared** Razor Class Library.

## Structure

```
Sites/
├── Shared/          Razor Class Library — common pages, services, models, assets
├── Dist/            Print Distributor portal  →  distributor.wiselabels.com
├── EndUser/         End User / Order portal   →  order.wiselabels.com
└── Partner/         Channel Partner portal    →  partner.wiselabels.com
```

## How Access Works

Each portal uses a **token-in-URL gate** pattern:

1. An authorized customer is given a private URL such as:
   `https://distributor.wiselabels.com/gate/abc123def456`
2. The `Gate.cshtml` page validates the token against `Sites:<Segment>:ValidTokens` in configuration.
3. If valid, a one-click acknowledgement checkbox is shown (similar to an age-verification gate).
4. On acknowledgement, a session cookie (`dist_access`, `enduser_access`, or `partner_access`) is written.
5. The `PortalGateMiddleware` in `Sites/Shared/Middleware/` enforces that all other pages require this cookie.

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
```

Add valid tokens to each project's `appsettings.Development.json` (do **not** commit real tokens):

```json
{
  "Sites": {
    "Dist": {
      "ValidTokens": [ "local-dev-token" ]
    }
  }
}
```

Then navigate to: `https://localhost:5001/gate/local-dev-token`

## Deployment

See [Documentation/PUBLIC_HIDDEN_SITES_PLAN.md](../Documentation/PUBLIC_HIDDEN_SITES_PLAN.md)
for the full Azure setup guide.

GitHub Actions workflows for each portal are in `.github/workflows/`:
- `deploy-dist.yml`
- `deploy-enduser.yml`
- `deploy-partner.yml`

## Token Management

- Tokens are stored in Azure App Service **Environment Variables** (or Azure Key Vault) only — **never committed to the repository**.
- Replace `REPLACE_WITH_RANDOM_TOKEN_1` placeholders in `appsettings.json` with actual tokens at deployment time via Azure Portal environment variables.
- Generate tokens with a tool like [random.org](https://www.random.org/strings/) or PowerShell:
  ```powershell
  [System.Web.Security.Membership]::GeneratePassword(24, 4)
  # or
  -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 24 | % { [char]$_ })
  ```
- Rotate tokens at least annually or when a distribution batch is retired.
