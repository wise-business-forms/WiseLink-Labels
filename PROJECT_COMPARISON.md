# Project Comparison: WiseLabels (Original) vs Wise4Labels (Redux)

**Date:** January 23, 2026  
**Projects Compared:**
- Original: `C:\Users\pmenefee\source\WiseLink-Labels\WiseLabels.csproj`
- Redux: `C:\Users\pmenefee\source\Wise4Labels\Wise4Labels.csproj`

---

## Differences Summary

### **1. Project File (.csproj) Differences**

#### **PropertyGroup:**
- **Original:** No `UserSecretsId`
- **Redux:** Has `<UserSecretsId>c1edfca7-191f-47b7-824e-1796a44d3225</UserSecretsId>`

#### **Package References:**
- **Original:** No explicit NuGet packages (relies on transitive dependencies)
- **Redux:** Has 2 explicit packages:
  - `Microsoft.EntityFrameworkCore.SqlServer` (8.0.20)
  - `Microsoft.EntityFrameworkCore.Tools` (8.0.20)

#### **Build Targets:**
- **Original:** Has `UpdateWebConfigForSelfContained` target (modifies web.config for self-contained deployments)
- **Redux:** No custom build targets

#### **Additional Files:**
- **Original:** No additional file configurations
- **Redux:** Has `logs\DO_NOT_DELETE.txt` configured to always copy to output

---

### **2. Program.cs Differences**

#### **Using Statements:**
- **Original:** Has `using CERM.DataAccess;` and `using Microsoft.EntityFrameworkCore;`
- **Redux:** No additional using statements

#### **Service Registrations:**
- **Original:** Registers:
  - `AddHttpClient()`
  - `IQuoteService` / `QuoteService`
  - `IEmailService` / `EmailService`
  - `CermDbContext` with SQL Server
  - `IJobRepository` / `JobRepositoryEF`
  - `ISubstrateRepository` / `SubstrateRepositoryEF`
  - `AddCermDataAccessEF()`
- **Redux:** Only has `AddRazorPages()`

#### **App.Run():**
- **Original:** Has `//app.Run();` (commented out)
- **Redux:** Has `app.Run();` (active)

---

## Summary Table

| Aspect | Original | Redux |
|--------|----------|-------|
| **UserSecretsId** | ❌ Missing | ✅ Configured |
| **EF Core Packages** | ❌ None (transitive) | ✅ 2 explicit packages |
| **Custom Build Targets** | ✅ 1 target | ❌ None |
| **Additional Files** | ❌ None | ✅ logs file |
| **Service Registrations** | ✅ 7 services | ❌ 1 service (RazorPages) |
| **Database Context** | ✅ Configured | ❌ Not configured |
| **app.Run()** | ❌ Commented out | ✅ Active |

---

## Key Functional Differences

1. **Original** is a full application with database access, email services, and business logic
2. **Redux** is a minimal Razor Pages app with no database or custom services
3. **Original** has deployment customization (self-contained deployment support)
4. **Redux** has user secrets configured for development

---

## IIS Deployment Issues & Resolutions

### **Issue 1: 500.30 Error**
**Root Cause:** The `UpdateRuntimeConfigForFlexibility` build target was removing version information from `runtimeconfig.json`, and when `RollForward>Major</RollForward>` was removed, the runtime couldn't find a matching framework version.

**Resolution:** Removed the `UpdateRuntimeConfigForFlexibility` build target. The `runtimeconfig.json` now keeps the default version specification (8.0.0) like Redux project.

### **Issue 2: "The specified version of Microsoft.NetCore.App or Microsoft.AspNetCore.App was not found"**
**Root Cause:** After removing `RollForward`, the runtime became stricter about version matching. The build target that removed version info conflicted with this.

**Resolution:** Removed the `UpdateRuntimeConfigForFlexibility` build target so the version is preserved in `runtimeconfig.json`.

---

## Notes

- Both projects target `net8.0`
- Both have `Nullable` and `ImplicitUsings` enabled
- Both reference the same `CERM.DataAccess` project
- Original has more complex service registration and database dependencies
- Redux is a simpler, minimal implementation

---

## Recommendations

1. **For Original:** Consider adding `UserSecretsId` if development secrets are needed
2. **For Original:** The commented `app.Run()` should probably be uncommented for IIS deployment
3. **For Redux:** If database functionality is needed, add the EF Core packages and service registrations from Original
4. **For Both:** Ensure IIS server has compatible .NET 8.0.x runtime installed
