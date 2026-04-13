# Plan: Public Hidden Sites for Three Customer Segments

## Overview

This plan describes how to deploy three separate, publicly accessible but "hidden" (unlisted / secret-link) web portals for WiseLink Labels. Each portal targets a distinct customer segment while sharing the core application codebase. Users reach a portal only through a private, unguessable URL and are presented with a lightweight verification step before accessing the form — no formal login account is required.

---

## Customer Segments

| Segment | Internal Code | Description |
|---|---|---|
| **Print Distributors** | `dist` | Traditional label distributor partners |
| **End Users** | `enduser` | Direct buyers of custom labels |
| **Channel Partners** | `partner` | Commission-based partners (e.g. contract packagers) |

---

## Section 1 — Access Verification Options

Because these are hidden sites (access controlled by a private link) and no user accounts exist, a lightweight "gate" is appropriate to:

1. Confirm the visitor arrived via an authorized, private link.
2. Deter accidental or automated access.
3. Satisfy any minimal compliance requirement.

Two options are presented below. Choose one per site (or use the same for all three).

---

### Option A — Token / Passphrase Gate (Recommended)

**How it works**

Each portal's private URL contains a short, human-readable passphrase embedded as a path segment or query parameter:

```
https://portal.wiselabels.com/dist/abc123
https://portal.wiselabels.com/enduser/xyz789
https://portal.wiselabels.com/partner/mnop456
```

The landing page checks whether the token in the URL matches a list of valid tokens stored in Azure App Configuration / environment variables. If valid, a short-lived session cookie is written and the visitor is admitted. If invalid, they see a "Not Authorized" message.

**User experience**
1. Partner receives the private URL in an email or printed material.
2. Visitor clicks the link; the page silently validates the token.
3. On success, a brief "Confirm you are an authorized WiseLink partner" checkbox or button is shown before proceeding (one-click acknowledgement, similar to an age gate).
4. Session cookie grants access for 24 hours without re-verifying.

**Pros**
- Zero friction — no form to fill in.
- Tokens are revocable by removing them from configuration.
- Different tokens can be issued to different distribution batches, enabling tracking.

**Cons**
- If the URL is forwarded widely, the token can be shared. Mitigate by making tokens long and rotating them periodically.

**Implementation sketch (ASP.NET Core Razor Pages)**

```csharp
// Pages/Sites/Dist/Gate.cshtml.cs
public class GateModel : PageModel
{
    private readonly IConfiguration _config;
    public GateModel(IConfiguration config) => _config = config;

    public IActionResult OnGet(string token)
    {
        var validTokens = _config.GetSection("Sites:Dist:ValidTokens").Get<string[]>() ?? [];
        if (!validTokens.Contains(token))
            return RedirectToPage("/NotAuthorized");

        HttpContext.Session.SetString("dist_access", "granted");
        return Page();  // Show one-click acknowledgement
    }
}
```

---

### Option B — Human Verification Challenge (Honeypot + CAPTCHA)

**How it works**

The private URL leads to a minimal "verify you are human" splash page that presents a simple challenge before granting access. Two sub-options:

**B-1: hCaptcha / Cloudflare Turnstile (invisible or visible)**
- A CAPTCHA widget is shown on the landing page.
- On successful CAPTCHA solve, a session cookie is written.
- Works entirely client-side/server-side; no user account needed.
- hCaptcha free tier is generous; Cloudflare Turnstile is free with no limits.

**B-2: Custom Question / Passphrase Entry**
- Landing page shows a short prompt, e.g.: _"Enter the access code found in your welcome email."_
- Visitor types a shared passphrase (e.g., `WISELINK2025`).
- Server validates the passphrase (stored in environment variable); on success writes a session cookie.

**User experience (B-1)**
1. Visitor opens the hidden URL.
2. Brief splash page appears: "Welcome to WiseLink Partner Portal — please verify you are human."
3. Cloudflare Turnstile widget appears (often invisible; may show a checkbox).
4. On success, visitor is forwarded to the actual form.

**Pros**
- Familiar UX pattern.
- Strong bot protection.
- No secret URL structure needed — the URL itself can be simpler.

**Cons**
- Slight friction (CAPTCHA step).
- Third-party dependency (hCaptcha / Cloudflare).

---

### Recommended Combination

Use **Option A (token in URL)** as the primary mechanism AND add a **simple one-click acknowledgement page** (checkbox: "I confirm I have been authorized by WiseLink Labels to access this portal"). This combination:

- Requires zero typed input from the user.
- Still provides an explicit consent action (comparable to the "I am 21+" button on alcohol websites).
- Makes token revocation possible.
- Keeps the experience frictionless.

---

## Section 2 — Azure Hosting Options

Both options below use Azure, which is the preferred cloud platform.

---

### Option 1 — Azure App Service (Recommended)

Deploy all three portals as **slots** or **sub-paths** under a single Azure App Service Plan, alongside the existing WiseLabels internal app.

| Resource | Value |
|---|---|
| Service | Azure App Service (Windows or Linux) |
| Tier | B2 or P1v3 (production) |
| Region | East US 2 (or match existing resource group) |
| Runtime | .NET 8 |

**Benefits**
- Single App Service Plan → lower cost.
- Easy CI/CD via GitHub Actions (already in repo or straightforward to add).
- Built-in staging/swap slots.
- Supports custom domains and managed TLS certificates at no extra charge.
- Scales automatically with autoscale rules.
- Application Insights integration for monitoring.

**Deployment slots approach**

```
App Service: wiselabels-portals
├── Production slot          → main internal app
├── dist-portal slot         → Print Distributor portal
├── enduser-portal slot      → End User portal
└── partner-portal slot      → Channel Partner portal
```

Each slot gets its own environment variables (`Sites:Dist:ValidTokens`, etc.) and can be independently deployed.

---

### Option 2 — Azure Static Web Apps + Azure Functions (Serverless)

If the portals are eventually simplified to static HTML/JS forms that call APIs, use Azure Static Web Apps with an Azure Functions backend.

| Resource | Value |
|---|---|
| Service | Azure Static Web Apps (Standard tier) |
| Functions | Azure Functions v4 (.NET isolated) |
| Storage | Azure Blob Storage (for any assets) |
| Region | East US 2 |

**Benefits**
- Very low cost for low/medium traffic.
- Global CDN built in.
- Free managed TLS.
- GitHub Actions deployment built in to Static Web Apps.

**Drawbacks**
- Requires refactoring Razor Pages to static + API architecture.
- More complex if form logic relies on server-rendered Razor.

> **Recommendation: Use Option 1 (App Service)** because the existing codebase is ASP.NET Core Razor Pages and migrating to a fully static architecture would require significant rework. Option 2 is noted for a potential future phase.

---

## Section 3 — URL Options

Two sets of URL structures are presented. Select one set for all three portals.

### URL Set A — Subdomain per Segment (Cleaner, Professional)

```
https://distributor.wiselabels.com
https://order.wiselabels.com
https://partner.wiselabels.com
```

- Each subdomain is a custom domain mapping in Azure App Service (or a separate slot).
- Requires adding three CNAME records in DNS at your domain registrar.
- Most professional appearance; hides the fact they share infrastructure.
- Easiest to hand out in emails and printed materials.

### URL Set B — Sub-path on Existing Domain (Simpler, Fewer DNS Changes)

```
https://portal.wiselabels.com/dist
https://portal.wiselabels.com/enduser
https://portal.wiselabels.com/partner
```

- A single new `portal` subdomain with path-based routing.
- Only one DNS record and one TLS certificate required.
- Routing handled by Azure Application Gateway or within the ASP.NET Core app itself using Area routing.
- Slightly less visually "clean" but requires less DNS / certificate management.

> **Recommendation:** URL Set A (subdomains) for a polished customer experience. URL Set B is a viable fallback if DNS management is a concern.

---

## Section 4 — Step-by-Step Azure Setup

The steps below assume **Azure App Service + URL Set A (subdomains)** — one App Service per portal for maximum isolation.

### Prerequisites

- Azure subscription (Contributor or Owner role)
- GitHub repository with existing WiseLink-Labels code
- Domain `wiselabels.com` with access to DNS management
- .NET 8 SDK installed locally for testing

---

### Step 1: Create a Resource Group

1. Log in to the [Azure Portal](https://portal.azure.com).
2. In the left menu, click **Resource groups** → **+ Create**.
3. Fill in:
   - **Subscription**: your subscription
   - **Resource group name**: `rg-wiselabels-portals`
   - **Region**: East US 2
4. Click **Review + create** → **Create**.

---

### Step 2: Create an App Service Plan

1. In the Azure Portal, search for **App Service plans**.
2. Click **+ Create**.
3. Fill in:
   - **Resource group**: `rg-wiselabels-portals`
   - **Name**: `asp-wiselabels-portals`
   - **Operating System**: Windows or Linux (.NET 8 supports both; Linux is slightly cheaper)
   - **Region**: East US 2
   - **Pricing tier**: `B2` (or `P1v3` for production)
4. Click **Review + create** → **Create**.

---

### Step 3: Create Three App Services (one per portal)

Repeat the following for each portal: **dist**, **enduser**, **partner**.

1. Search for **App Services** → **+ Create**.
2. Fill in:
   - **Resource group**: `rg-wiselabels-portals`
   - **Name**: `wiselabels-dist` (repeat with `wiselabels-enduser`, `wiselabels-partner`)
   - **Runtime stack**: `.NET 8`
   - **OS**: Windows
   - **Region**: East US 2
   - **App Service Plan**: `asp-wiselabels-portals`
3. Click **Review + create** → **Create**.

> **Azure default URLs** (before custom domains):
> - `https://wiselabels-dist.azurewebsites.net`
> - `https://wiselabels-enduser.azurewebsites.net`
> - `https://wiselabels-partner.azurewebsites.net`

---

### Step 4: Configure Application Settings

For each App Service:

1. Go to the App Service → **Environment variables** → **App settings** tab.
2. Add the following settings (adjust values per site):

   | Name | Value (example) |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `Sites__Segment` | `dist` (or `enduser` / `partner`) |
   | `Sites__Dist__ValidTokens__0` | `<random-token-1>` |
   | `Sites__Dist__ValidTokens__1` | `<random-token-2>` |
   | `Cerm__OAuthUrl` | *(shared CERM API URL)* |
   | `Cerm__Username` | *(CERM credentials)* |
   | `Cerm__Password` | *(CERM credentials)* |
   | `Cerm__ClientId` | *(CERM credentials)* |
   | `Cerm__ClientSecret` | *(CERM credentials)* |
   | `Email__SmtpHost` | *(SMTP server)* |

3. Click **Apply** → **Confirm**.

> **Security note:** For production, store secrets in **Azure Key Vault**. Add a Key Vault reference in App Settings: `@Microsoft.KeyVault(VaultName=kv-wiselabels;SecretName=CermPassword)`.

---

### Step 5: Set Up Custom Domains

For each App Service:

1. Go to App Service → **Custom domains** → **+ Add custom domain**.
2. Enter the subdomain (e.g., `distributor.wiselabels.com`).
3. Azure provides a TXT record (ownership verification) and a CNAME record to add.
4. In your DNS provider (e.g., GoDaddy, Cloudflare, Azure DNS):
   - Add the TXT record shown by Azure.
   - Add a CNAME: `distributor` → `wiselabels-dist.azurewebsites.net`
5. Click **Validate** in Azure Portal. Once DNS propagates (up to 48 hours), click **Add**.

---

### Step 6: Enable Free Managed TLS Certificates

1. In the App Service, go to **Certificates** → **Managed certificates** tab.
2. Click **+ Add certificate**.
3. Select the custom domain just added.
4. Click **Add**. Azure provisions a free TLS certificate (auto-renews).

---

### Step 7: Set Up GitHub Actions CI/CD

For each portal, add a GitHub Actions workflow. You can generate it directly from Azure:

1. In the App Service, go to **Deployment Center**.
2. Select **GitHub** as the source.
3. Authenticate and select:
   - **Organization**: `wise-business-forms`
   - **Repository**: `WiseLink-Labels`
   - **Branch**: `main` (or a portal-specific branch, see Section 5)
4. Azure generates a workflow file under `.github/workflows/`. Download it or let Azure commit it.

Example workflow file (`.github/workflows/deploy-dist.yml`):

```yaml
name: Deploy Distributor Portal

on:
  push:
    branches: [ main ]
    paths:
      - 'Sites/Dist/**'
      - 'Sites/Shared/**'
      - '.github/workflows/deploy-dist.yml'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'

      - name: Build
        run: dotnet publish Sites/Dist/WiseLabels.Dist.csproj -c Release -o dist-publish

      - name: Deploy to Azure
        uses: azure/webapps-deploy@v3
        with:
          app-name: wiselabels-dist
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE_DIST }}
          package: dist-publish
```

5. Add the publish profile for each App Service as a GitHub secret:
   - Azure Portal → App Service → **Download publish profile**.
   - GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**.
   - Name: `AZURE_PUBLISH_PROFILE_DIST` (repeat for `_ENDUSER`, `_PARTNER`).

---

### Step 8: Configure Azure Application Insights (Optional but Recommended)

1. In the Azure Portal, search for **Application Insights** → **+ Create**.
2. Fill in:
   - **Resource group**: `rg-wiselabels-portals`
   - **Name**: `ai-wiselabels-portals`
   - **Region**: East US 2
3. After creation, copy the **Connection String**.
4. In each App Service → **Environment variables**, add:
   - `ApplicationInsights__ConnectionString` → `<connection string>`
5. In each project's `Program.cs`, add:
   ```csharp
   builder.Services.AddApplicationInsightsTelemetry();
   ```

---

### Step 9: Verify Deployments

1. Push a test commit to trigger the GitHub Actions workflow.
2. Monitor the workflow run in GitHub → **Actions**.
3. Once deployed, navigate to each custom domain and verify:
   - The gate page loads.
   - An invalid token redirects to "Not Authorized".
   - A valid token sets the session and loads the form.
4. Check Application Insights for any errors.

---

## Section 5 — Repository Organization

The three portals share the majority of the codebase. The recommended approach is a **single repository with a shared library project** and three thin "host" projects that reference the shared project.

### Proposed Folder Structure

```
WiseLink-Labels/
│
├── Sites/
│   ├── Shared/                          # Shared Razor Pages library (class library project)
│   │   ├── WiseLabels.Shared.csproj
│   │   ├── Pages/
│   │   │   ├── Quote.cshtml             # Shared quote form (all three portals use this)
│   │   │   ├── Confirm.cshtml
│   │   │   ├── Success.cshtml
│   │   │   ├── Api/                     # Shared API proxies (CERM, etc.)
│   │   │   └── Shared/                  # Shared layouts, partials
│   │   ├── Services/                    # Shared services (QuoteService, EmailService, etc.)
│   │   ├── Models/
│   │   └── wwwroot/                     # Shared static assets (CSS, JS, images)
│   │
│   ├── Dist/                            # Print Distributor portal
│   │   ├── WiseLabels.Dist.csproj       # References WiseLabels.Shared
│   │   ├── Program.cs                   # Dist-specific DI / middleware (gate middleware)
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   ├── Pages/
│   │   │   ├── Gate.cshtml              # Token verification + one-click acknowledgement
│   │   │   ├── Gate.cshtml.cs
│   │   │   └── NotAuthorized.cshtml
│   │   └── wwwroot/
│   │       └── css/
│   │           └── dist-theme.css       # Distributor-specific branding overrides
│   │
│   ├── EndUser/                         # End User portal
│   │   ├── WiseLabels.EndUser.csproj    # References WiseLabels.Shared
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   ├── Pages/
│   │   │   ├── Gate.cshtml
│   │   │   ├── Gate.cshtml.cs
│   │   │   └── NotAuthorized.cshtml
│   │   └── wwwroot/
│   │       └── css/
│   │           └── enduser-theme.css
│   │
│   └── Partner/                         # Channel Partner portal
│       ├── WiseLabels.Partner.csproj    # References WiseLabels.Shared
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Production.json
│       ├── Pages/
│       │   ├── Gate.cshtml
│       │   ├── Gate.cshtml.cs
│       │   └── NotAuthorized.cshtml
│       └── wwwroot/
│           └── css/
│               └── partner-theme.css
│
├── Pages/                               # Existing internal (authenticated) app — unchanged
├── Services/
├── Program.cs
├── WiseLabels.csproj
├── WiseLabels.slnx
└── Documentation/
    └── PUBLIC_HIDDEN_SITES_PLAN.md      # This document
```

### How the Three Projects Share Code

1. **WiseLabels.Shared** is a [Razor Class Library](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/ui-class) project.
   - All common Razor Pages (Quote form, Confirm, Success, API proxies) live here.
   - Common services (QuoteService, EmailService, CermAuthService) live here.
   - Common static assets (site.css, quote.js) live in `wwwroot/` here.

2. **WiseLabels.Dist / EndUser / Partner** are thin ASP.NET Core web applications.
   - Each references `WiseLabels.Shared`.
   - Each adds only site-specific pages (Gate, NotAuthorized) and branding overrides.
   - Each has its own `Program.cs` to register site-specific middleware (the gate check).

3. **The existing internal app** (`WiseLabels.csproj`) remains unchanged. It can also reference `WiseLabels.Shared` if desired, or keep its own copies.

### Solution File

Add all four projects to a single solution for easy local development:

```
WiseLabels.slnx
  ├── WiseLabels (existing internal app)
  ├── WiseLabels.Shared
  ├── WiseLabels.Dist
  ├── WiseLabels.EndUser
  └── WiseLabels.Partner
```

### Segment-Specific Customizations

Each portal can customize the shared base through:

| Customization | Mechanism |
|---|---|
| Branding / colors | Override CSS file in each portal's `wwwroot/css/` |
| Form fields shown | Feature flags in `appsettings.json` consumed by shared Razor Pages |
| Pricing / product catalog | Separate CERM customer IDs per portal (env variable `Cerm__CustomerID`) |
| Email notifications | Separate "from" address and template per portal |
| Quote reference prefix | `Sites__ReferencePrefix = "DIST-"` / `"EU-"` / `"CP-"` |

---

## Section 6 — Security Checklist

Before going live, verify each portal:

- [ ] Token / passphrase values are long (16+ characters), random, and stored only in Azure App Settings or Key Vault — never committed to the repository.
- [ ] Session cookie is `HttpOnly`, `Secure`, and `SameSite=Strict`.
- [ ] Portal pages (except Gate and NotAuthorized) require the session cookie; unauthenticated requests redirect to the Gate page.
- [ ] Rate limiting is applied to the Gate endpoint to prevent brute-force token guessing (ASP.NET Core 8 `RateLimiter` middleware).
- [ ] HTTPS is enforced at the App Service level (HTTPS-only toggle in Azure Portal).
- [ ] HSTS header is enabled in `Program.cs`.
- [ ] Application Insights alerts are configured for unusual traffic spikes on the Gate endpoint.
- [ ] Access tokens are rotated at least annually (or when a distribution batch is retired).

---

## Section 7 — Decision Summary

| Decision Point | Recommendation | Alternative |
|---|---|---|
| Access verification | Option A: Token-in-URL + one-click acknowledgement | Option B: Cloudflare Turnstile CAPTCHA |
| Cloud hosting | Azure App Service (Option 1) | Azure Static Web Apps (Option 2) |
| URL structure | URL Set A: subdomains (`distributor.wiselabels.com`) | URL Set B: sub-paths (`portal.wiselabels.com/dist`) |
| Code sharing | Razor Class Library (`WiseLabels.Shared`) | Single project with Areas |
| CI/CD | GitHub Actions workflow per portal | Azure Deployment Center auto-generated workflow |

---

## Next Steps

1. **Choose** the access verification option (A or B) and URL structure (Set A or B).
2. **Create** the `WiseLabels.Shared` Razor Class Library and refactor common pages into it.
3. **Create** the three thin portal projects (`WiseLabels.Dist`, `WiseLabels.EndUser`, `WiseLabels.Partner`).
4. **Provision** Azure resources (Steps 1–8 above).
5. **Configure** DNS records for chosen subdomains.
6. **Generate** and distribute the first set of access tokens to each customer segment.
7. **Test** each portal end-to-end before sharing links externally.
8. **Set up monitoring** alerts in Application Insights.
