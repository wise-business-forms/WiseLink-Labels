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
https://labels-tags.com/dist/abc123
https://labels-tags.com/enduser/xyz789
https://labels-tags.com/partner/mnop456
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
https://distributor.labels-tags.com
https://order.labels-tags.com
https://partner.labels-tags.com
```

- Each subdomain is a custom domain mapping in Azure App Service.
- Requires adding three CNAME records in the `labels-tags.com` DNS zone (Azure DNS).
- Most professional appearance; hides the fact they share infrastructure.
- Easiest to hand out in emails and printed materials.

### URL Set B — Sub-path on `labels-tags.com` (Simpler, Fewer DNS Changes)

```
https://labels-tags.com/dist
https://labels-tags.com/enduser
https://labels-tags.com/partner
```

- A single apex domain with path-based routing.
- Only one DNS binding and one TLS certificate required.
- Routing handled by Azure Application Gateway or within the ASP.NET Core app itself using Area routing.
- Slightly less visually "clean" but requires less DNS / certificate management.

> **Recommendation:** URL Set A (subdomains) for a polished customer experience. URL Set B is a viable fallback if DNS management is a concern.

---

## Section 4 — Step-by-Step Azure Setup

The steps below assume **Azure App Service + URL Set A (subdomains)** — one App Service per portal for maximum isolation.

### Prerequisites

- Azure subscription (Contributor or Owner role)
- GitHub repository with existing WiseLink-Labels code
- Domain `labels-tags.com` with access to DNS management (Azure DNS recommended)
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
2. Enter the subdomain (e.g., `distributor.labels-tags.com`).
3. Azure provides a TXT record (ownership verification) and a CNAME record to add.
4. In your DNS provider (Azure DNS for `labels-tags.com`):
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
│   │   ├── Middleware/
│   │   │   ├── PortalGateMiddleware.cs  # Enforces session access on all non-public paths
│   │   │   └── SubdomainDistributorMiddleware.cs  # Resolves distributor by Host header subdomain
│   │   ├── Models/
│   │   │   └── DistributorProfile.cs    # Per-distributor branding model (white-label)
│   │   ├── Services/
│   │   │   ├── IDistributorProfileService.cs
│   │   │   └── DistributorProfileService.cs  # Token & subdomain → profile lookup
│   │   ├── Pages/
│   │   │   ├── Quote.cshtml             # Shared quote form (all portals use this)
│   │   │   ├── Confirm.cshtml
│   │   │   ├── Success.cshtml
│   │   │   ├── Api/                     # Shared API proxies (CERM, etc.)
│   │   │   └── Shared/                  # Shared layouts, partials
│   │   └── wwwroot/                     # Shared static assets (CSS, JS, images)
│   │
│   ├── Dist/                            # Print Distributor portal
│   │   ├── WiseLabels.Dist.csproj       # References WiseLabels.Shared
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Pages/
│   │   │   ├── Gate.cshtml
│   │   │   ├── Gate.cshtml.cs
│   │   │   └── NotAuthorized.cshtml
│   │   └── wwwroot/css/dist-theme.css
│   │
│   ├── EndUser/                         # End User portal
│   │   ├── WiseLabels.EndUser.csproj    # References WiseLabels.Shared
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Pages/
│   │   │   ├── Gate.cshtml
│   │   │   ├── Gate.cshtml.cs
│   │   │   └── NotAuthorized.cshtml
│   │   └── wwwroot/css/enduser-theme.css
│   │
│   ├── Partner/                         # Channel Partner portal
│   │   ├── WiseLabels.Partner.csproj    # References WiseLabels.Shared
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Pages/
│   │   │   ├── Gate.cshtml
│   │   │   ├── Gate.cshtml.cs
│   │   │   └── NotAuthorized.cshtml
│   │   └── wwwroot/css/partner-theme.css
│   │
│   └── WhiteLabel/                      # Distributor white-label portal (NEW)
│       ├── WiseLabels.WhiteLabel.csproj # References WiseLabels.Shared
│       ├── Program.cs                   # Registers SubdomainDistributorMiddleware + gate
│       ├── appsettings.json             # ApexDomain + Distributors (populated via Azure config)
│       ├── Pages/
│       │   ├── _ViewStart.cshtml        # Uses _WhiteLabelLayout
│       │   ├── _ViewImports.cshtml
│       │   ├── Gate.cshtml              # Shows distributor logo/contact before acknowledgement
│       │   ├── Gate.cshtml.cs           # Subdomain or token → profile; stores in session
│       │   └── Shared/
│       │       └── _WhiteLabelLayout.cshtml  # Header with dist. branding; footer attribution
│       └── wwwroot/
│           ├── css/whitelabel-base.css  # CSS custom property overrides per distributor
│           └── img/                     # Optional: local copies of distributor logos
│
├── Pages/                               # Existing internal (authenticated) app — unchanged
├── Services/
├── Program.cs
├── WiseLabels.csproj
├── WiseLabels.slnx
└── Documentation/
    └── PUBLIC_HIDDEN_SITES_PLAN.md      # This document
```

### How the Four Projects Share Code

1. **WiseLabels.Shared** is a [Razor Class Library](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/ui-class) project.
   - All common Razor Pages (Quote form, Confirm, Success, API proxies) live here.
   - Common services (QuoteService, EmailService, CermAuthService) live here.
   - Common static assets (site.css, quote.js) live in `wwwroot/` here.
   - The `DistributorProfile` model and `IDistributorProfileService` live here so the
     white-label portal and any future portal can reference them.

2. **WiseLabels.Dist / EndUser / Partner** are thin ASP.NET Core web applications.
   - Each references `WiseLabels.Shared`.
   - Each adds only site-specific pages (Gate, NotAuthorized) and branding overrides.
   - Each has its own `Program.cs` to register site-specific middleware (the gate check).

3. **WiseLabels.WhiteLabel** is the distributor-branded end-user portal.
   - References `WiseLabels.Shared` for the ordering form and services.
   - Registers `SubdomainDistributorMiddleware` with `ApexDomain = "labels-tags.com"` so
     all `{slug}.labels-tags.com` requests automatically resolve to the matching profile.
   - Adds `Gate.cshtml` / `Gate.cshtml.cs` that first checks `HttpContext.Items` for a
     profile resolved by the subdomain middleware, then falls back to token-in-URL lookup.
   - Adds `_WhiteLabelLayout.cshtml` which reads the profile from session and renders the
     distributor's logo, company name, and contact info in the page header.

4. **The existing internal app** (`WiseLabels.csproj`) remains unchanged.

### Solution File

Add all five projects to a single solution for easy local development:

```
WiseLabels.slnx
  ├── WiseLabels (existing internal app)
  ├── WiseLabels.Shared
  ├── WiseLabels.Dist
  ├── WiseLabels.EndUser
  ├── WiseLabels.Partner
  └── WiseLabels.WhiteLabel
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
| **White-label branding** | **`DistributorProfile` in `Sites:WhiteLabel:Distributors` config — per-distributor logo, color, contact info** |

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
- [ ] White-label distributor logos are served over HTTPS (Azure Blob Storage CDN). No distributor logo URLs are accepted from untrusted sources.
- [ ] The `DistributorProfile` stored in session contains no secrets — only branding data. Tokens are never stored in the session.

---

## Section 7 — Decision Summary

| Decision Point | Recommendation | Alternative |
|---|---|---|
| Access verification | Option A: Token-in-URL + one-click acknowledgement | Option B: Cloudflare Turnstile CAPTCHA |
| Cloud hosting | Azure App Service (Option 1) | Azure Static Web Apps (Option 2) |
| URL structure | URL Set A: subdomains (`distributor.labels-tags.com`) | URL Set B: sub-paths (`labels-tags.com/dist`) |
| Code sharing | Razor Class Library (`WiseLabels.Shared`) | Single project with Areas |
| CI/CD | GitHub Actions workflow per portal | Azure Deployment Center auto-generated workflow |
| White-label branding | Per-distributor profile stored in Azure App Config / Key Vault | Separate Azure App Service per distributor |

---

## Section 8 — White-Label Sites for Distributors

### Overview

Print distributors in the **Dist** segment can re-share a private link with their own end
customers. When a customer opens that link, they see a portal branded with **the
distributor's logo and contact information** rather than the WiseLink Labels brand.
The underlying ordering form, CERM integration, and email workflow are identical to the
shared core — only the visual branding differs.

This approach is sometimes called a "powered-by" or "co-branded" portal:
the distributor presents it as their own ordering experience while WiseLink Labels handles
all the manufacturing and fulfilment in the background.

---

### How It Works (Token → Profile Lookup)

Each distributor is given **one or more private tokens** to embed in their customer-facing
URL.  When a customer opens the link, the White-Label portal:

1. Reads the token from the URL path (`/gate/<token>`).
2. Looks up the matching `DistributorProfile` (name, logo, contact info, brand color) from
   the configuration stored in Azure App Settings / Key Vault.
3. Serializes the profile into the **server-side session**.
4. Shows the distributor's logo and contact block on the gate page.
5. After the customer clicks the acknowledgement checkbox, every subsequent page is
   rendered by `_WhiteLabelLayout.cshtml`, which reads the profile from session and injects:
   - The distributor's logo in the header.
   - The distributor's company name as the page `<title>`.
   - The distributor's contact name, phone, and email in the header.
   - An optional CSS custom property override for the distributor's primary brand color.
6. A "Powered by WiseLink Labels" attribution appears in the page footer.

---

### Distributor Profile Configuration

Profiles are stored in the `Sites:WhiteLabel:Distributors` configuration array.
**Never commit real tokens to the repository.** Add profiles via Azure App Settings or
Azure Key Vault.

Example Azure App Setting structure (using the `__` separator for nested JSON in Azure):

```
Sites__WhiteLabel__ApexDomain                        labels-tags.com

Sites__WhiteLabel__Distributors__0__Slug             abc-printing
Sites__WhiteLabel__Distributors__0__Subdomain        abc-printing
Sites__WhiteLabel__Distributors__0__CompanyName      ABC Printing Co.
Sites__WhiteLabel__Distributors__0__LogoUrl          https://yourcdn.blob.core.windows.net/logos/abc.png
Sites__WhiteLabel__Distributors__0__LogoAlt          ABC Printing Co. logo
Sites__WhiteLabel__Distributors__0__ContactName      Jane Doe
Sites__WhiteLabel__Distributors__0__ContactPhone     (555) 100-2000
Sites__WhiteLabel__Distributors__0__ContactEmail     jane@abcprinting.com
Sites__WhiteLabel__Distributors__0__PrimaryColor     #003399
Sites__WhiteLabel__Distributors__0__ReferencePrefix  ABC-
Sites__WhiteLabel__Distributors__0__Tokens__0        <random-token-1>

Sites__WhiteLabel__Distributors__1__Slug             xyz-supply
Sites__WhiteLabel__Distributors__1__Subdomain        xyz-supply
Sites__WhiteLabel__Distributors__1__CompanyName      XYZ Supply Inc.
...
```

- `Subdomain` is the DNS label that will be used as the subdomain on `labels-tags.com`
  (e.g. `"abc-printing"` → `abc-printing.labels-tags.com`).
- `Tokens` remain useful as a fallback (for sharing via token URL when no subdomain has
  been set up yet, or as an extra-security option).

Add additional distributors by incrementing the array index (`__1__`, `__2__`, ...).

> **Security note:** Store token values in **Azure Key Vault** and reference them from App
> Settings with `@Microsoft.KeyVault(...)`.  Each distributor only ever sees their own
> private URL; they have no knowledge of other distributors' tokens.

---

### White-Label URL Options

Three options for structuring white-label URLs. All examples use the `labels-tags.com` domain.

#### Option W-A — Per-Distributor Subdomain on `labels-tags.com` (Recommended)

```
https://abc-printing.labels-tags.com
https://xyz-supply.labels-tags.com
```

- Each distributor gets their own subdomain on the WiseLink-owned `labels-tags.com` domain.
- A single **wildcard DNS record** points all `*.labels-tags.com` subdomains to the same
  App Service: `*.labels-tags.com CNAME wiselabels-whitelabel.azurewebsites.net`
- A single **wildcard TLS certificate** covers all distributor subdomains
  (Azure App Service Managed Certificate supports wildcard certs via Azure DNS zone).
- The ASP.NET Core app reads the subdomain from the `Host` header at runtime and looks
  up the matching `DistributorProfile` — no DNS change needed per distributor, just a
  config update.
- Distributor sees a clean URL with no WiseLink identity visible in the path.
- Gate page URL: `https://abc-printing.labels-tags.com/gate` (no token in URL — the
  subdomain itself is the access gate, plus the one-click acknowledgement).

**Pros**
- Clean, professional URL for each distributor.
- Adding a new distributor requires only a config update — zero DNS changes.
- Wildcard cert means no new TLS certificate per distributor.
- The subdomain is the only differentiator; no token in the URL means the URL is shareable
  (acceptable when the one-click gate is the access mechanism).

**Cons**
- Requires Azure DNS zone for `labels-tags.com` to issue the wildcard managed certificate.
- If a distributor wants their _own_ domain (e.g. `labels.abcprinting.com`), that requires
  a separate custom domain binding and cert (see Option W-C below).

#### Option W-B — Per-Distributor Subfolder on `labels-tags.com`

```
https://labels-tags.com/abc-printing
https://labels-tags.com/xyz-supply
```

- A single apex domain with path-prefix routing.
- The distributor `Slug` value appears as the first path segment.
- Single TLS certificate; one custom domain binding.
- Adding a new distributor requires only a config update.

**Pros**
- Simplest DNS and certificate setup — only the apex domain is needed.
- One App Service, one TLS cert.

**Cons**
- Less visually distinct — all distributors share the same domain.
- Path-prefix routing requires a routing middleware or `IPageRouteModelConvention` that
  extracts the first path segment and resolves the distributor profile from it.
- The WiseLink-owned domain is fully visible in the URL.

> **Recommendation: Option W-A (per-distributor subdomain)** — provides the cleanest
> per-distributor experience while remaining easy to operate at scale.  Option W-B is a
> valid fallback if a wildcard TLS certificate cannot be obtained.

#### Option W-C — Per-Distributor Custom Domain (Distributor's Own Domain)

```
https://labels.abcprinting.com    (CNAME → wiselabels-whitelabel.azurewebsites.net)
https://labels.xyzupply.com       (CNAME → wiselabels-whitelabel.azurewebsites.net)
```

- Each distributor adds a CNAME at their own DNS provider.
- Azure App Service **custom domain** binding is required per distributor.
- Best for distributors who want zero WiseLink/labels-tags.com branding in the URL.
- More operational effort (requires per-distributor DNS change and Azure portal binding).

> **Recommendation:** Reserve Option W-C for distributors that specifically request their
> own domain. Start with Option W-A for all new distributors.

---

### Repository Structure Additions

The white-label portal adds a fourth thin project:

```
Sites/
├── Shared/
│   ├── WiseLabels.Shared.csproj
│   ├── Middleware/
│   │   ├── PortalGateMiddleware.cs              # Shared — used by all four portals
│   │   └── SubdomainDistributorMiddleware.cs    # NEW — resolves distributor by Host header
│   ├── Models/
│   │   └── DistributorProfile.cs               # NEW — per-distributor branding model
│   └── Services/
│       ├── IDistributorProfileService.cs        # NEW — token & subdomain → profile lookup
│       └── DistributorProfileService.cs         # NEW — configuration-backed implementation
│
├── Dist/         Print Distributor portal  →  distributor.labels-tags.com
├── EndUser/      End User portal           →  order.labels-tags.com
├── Partner/      Channel Partner portal    →  partner.labels-tags.com
│
└── WhiteLabel/   NEW — Distributor white-label portal  →  *.labels-tags.com
    ├── WiseLabels.WhiteLabel.csproj             # References WiseLabels.Shared
    ├── Program.cs                               # Registers SubdomainDistributorMiddleware + gate
    ├── appsettings.json                         # ApexDomain + Distributors (empty — Azure config)
    ├── Pages/
    │   ├── _ViewStart.cshtml                    # Uses _WhiteLabelLayout
    │   ├── _ViewImports.cshtml
    │   ├── Gate.cshtml                          # Shows distributor logo + contact; acknowledgement
    │   ├── Gate.cshtml.cs                       # Subdomain or token → profile; stores in session
    │   └── Shared/
    │       └── _WhiteLabelLayout.cshtml         # Header with dist. branding; footer attribution
    └── wwwroot/
        ├── css/
        │   └── whitelabel-base.css              # CSS custom property overrides per distributor
        └── img/
            └── (distributor logos: Azure Blob Storage CDN or local fallbacks)
```

---

### What Pages the Distributor's Customers See

Once past the gate, the distributor's customers see the **same order form, confirmation
page, and success page** as direct end users — the only difference is:

| Element | Direct End User portal | White-Label portal |
|---|---|---|
| Page `<title>` | "WiseLink Labels — Order Portal" | "ABC Printing Co. — Order Portal" |
| Header logo | WiseLink Labels logo | ABC Printing Co. logo |
| Header contact | WiseLink contact info | ABC Printing rep's name, phone, email |
| Brand color | WiseLink navy (#1a3a5c) | Distributor's color (e.g. #003399) |
| Footer | WiseLink contact | "Powered by WiseLink Labels" |
| Quote reference prefix | `EU-` | `ABC-` (or distributor-configured prefix) |
| Confirmation email | "From: WiseLink Labels" | "From: WiseLink Labels" (internal) + copy to dist. rep |

---

### Adding a New Distributor — Checklist

When a new distributor wants their own white-label portal:

1. **Obtain branding assets** from the distributor:
   - Company logo (PNG/SVG, ideally on a transparent or white background, min 200 px wide)
   - Brand hex color (optional)
   - Contact name, phone, and email for the portal header
   - Desired subdomain label (e.g. `abc-printing` for `abc-printing.labels-tags.com`);
     must be lowercase letters, digits, and hyphens only (valid DNS label)

2. **Upload the logo** to Azure Blob Storage:
   - In the Azure Portal, go to the Storage Account → **Containers** → `logos`.
   - Upload the logo file.
   - Copy the public HTTPS URL.

3. **Add a distributor profile** in Azure App Settings for `wiselabels-whitelabel`:
   - Set `Sites__WhiteLabel__Distributors__N__Slug` (e.g. `abc-printing`)
   - Set `Sites__WhiteLabel__Distributors__N__Subdomain` (e.g. `abc-printing`)
   - Set all other `Sites__WhiteLabel__Distributors__N__*` keys (logo URL, contact info, etc.)
   - Optionally set `Sites__WhiteLabel__Distributors__N__Tokens__0` as a token-URL fallback.

4. **Restart the App Service** to pick up the new configuration
   (or use a **Deployment Slot** swap to avoid downtime).

5. **Test** by opening `https://abc-printing.labels-tags.com/gate` — the distributor's logo
   and contact info should appear on the gate page.

6. **Send the distributor** their URL: `https://abc-printing.labels-tags.com`
   (they can share this directly with their customers).

---

### Azure Setup Additions for White-Label

Follow the same steps as Section 4, with these additions:

1. **Create** a fourth App Service: `wiselabels-whitelabel` on the existing
   `asp-wiselabels-portals` App Service Plan.

2. **Move `labels-tags.com` DNS to Azure DNS (required for wildcard managed certificate):**
   - In the Azure Portal, search for **DNS zones** → **+ Create**.
   - Enter `labels-tags.com` as the zone name.
   - Azure provides four name servers (e.g. `ns1-xx.azure-dns.com`).
   - At your domain registrar, change the name server records for `labels-tags.com` to
     the four Azure DNS name servers.
   - Wait for NS propagation (typically 10–60 minutes).

3. **Add a wildcard CNAME in Azure DNS:**
   - In the `labels-tags.com` DNS zone → **+ Record set**.
   - Name: `*`
   - Type: `CNAME`
   - Value: `wiselabels-whitelabel.azurewebsites.net`
   - This routes all `*.labels-tags.com` requests to the white-label App Service.

4. **Bind the wildcard custom domain to the App Service:**
   - In the `wiselabels-whitelabel` App Service → **Custom domains** → **+ Add custom domain**.
   - Enter `*.labels-tags.com`.
   - Azure validates via the DNS TXT record it provides; add the TXT record in Azure DNS.
   - Click **Validate** → **Add**.

5. **Issue a wildcard managed TLS certificate:**
   - In the App Service → **Certificates** → **Managed certificates** → **+ Add certificate**.
   - Select `*.labels-tags.com`.
   - Azure provisions a wildcard certificate (auto-renews). This requires the domain to be
     in Azure DNS (Step 2 above).

6. **Set `AllowedHosts` in App Settings:**
   - Azure App Setting: `AllowedHosts` → `*.labels-tags.com;labels-tags.com`
   - This prevents host header injection attacks while allowing all distributor subdomains.

7. **Azure Blob Storage for logos:**
   - Create a Storage Account: `salabelstags` (or similar).
   - Create a public blob container: `logos`.
   - Set container access level to **Blob** (anonymous read for blobs only).
   - Upload distributor logos; note each blob's HTTPS URL.

8. **GitHub Actions secret:** Add `AZURE_PUBLISH_PROFILE_WHITELABEL` to GitHub Secrets
   (download publish profile from `wiselabels-whitelabel` App Service).

9. **CI/CD workflow:** `.github/workflows/deploy-whitelabel.yml` is already included in
   the repository and will trigger on changes to `Sites/WhiteLabel/**` or `Sites/Shared/**`.

---

## Next Steps

1. **Choose** the access verification option (A or B) and URL structure (Set A or B).
2. **Create** the `WiseLabels.Shared` Razor Class Library and refactor common pages into it.
3. **Create** the three core portal projects (`WiseLabels.Dist`, `WiseLabels.EndUser`, `WiseLabels.Partner`).
4. **Create** the white-label portal project (`WiseLabels.WhiteLabel`) using the scaffold in `Sites/WhiteLabel/`.
5. **Provision** Azure resources (Steps 1–8 above) plus the white-label App Service and Blob Storage account.
6. **Configure** DNS records for chosen subdomains.
7. **Generate** and distribute the first set of access tokens to each customer segment and each initial distributor white-label profile.
8. **Onboard** the first white-label distributor using the "Adding a New Distributor" checklist in Section 8.
9. **Test** each portal end-to-end before sharing links externally.
10. **Set up monitoring** alerts in Application Insights.
