using CERM.DataAccess;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using WiseLabels;
using WiseLabels.Authorization;

var builder = WebApplication.CreateBuilder(args);

// DinkToPdf
var architectureFolder = RuntimeInformation.ProcessArchitecture switch
{
    Architecture.X64 => "64bit",
    Architecture.X86 => "32bit",
    _ => throw new PlatformNotSupportedException("wkhtmltopdf is only configured for Windows x64/x86")
};
var wkhtmlPath = Path.Combine(AppContext.BaseDirectory, "Libraries", architectureFolder, "libwkhtmltox.dll");
if (!File.Exists(wkhtmlPath))
{
    throw new FileNotFoundException($"wkhtmltopdf native library not found at {wkhtmlPath}.");
}

var context = new CustomAssemblyLoadContext();
context.LoadUnmanagedLibrary(wkhtmlPath);
builder.Services.AddSingleton<IConverter>(_ => new SynchronizedConverter(new PdfTools()));

// Add services to the container.
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<AccessControlOptions>(builder.Configuration.GetSection("Authorization"));
builder.Services.Configure<WiseLabels.Configuration.UserFilteringOptions>(builder.Configuration.GetSection("UserFiltering"));
builder.Services.Configure<WiseLabels.Configuration.LineItemOptions>(builder.Configuration.GetSection(WiseLabels.Configuration.LineItemOptions.SectionName));
builder.Services.AddSingleton<IAuthorizationHandler, GroupAccessHandler>();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Full access policy - for admin features
    options.AddPolicy("FullAccess", policy =>
        policy.Requirements.Add(new GroupAccessRequirement(AccessLevel.Full)));

    // Limited access policy - for regular users
    options.AddPolicy("LimitedAccess", policy =>
        policy.Requirements.Add(new GroupAccessRequirement(AccessLevel.Limited)));
});

builder.Services.AddRazorPages()
    // Session-backed TempData. The default CookieTempDataProvider Base64s the whole
    // serialized QuoteRequest (including QuickQuoteResponseJson) into response cookies
    // on every GetQuote -> Confirm -> Success hop, which pushes against Kestrel's
    // 32 KB header limit once line items are added.
    .AddSessionStateTempDataProvider()
    .AddMicrosoftIdentityUI();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<WiseLabels.Services.IQuoteService, WiseLabels.Services.QuoteService>();
builder.Services.AddScoped<WiseLabels.Services.IEmailService, WiseLabels.Services.EmailService>();
builder.Services.AddScoped<WiseLabels.Services.ICermAuthService, WiseLabels.Services.CermAuthService>();
builder.Services.AddScoped<WiseLabels.Services.IChatService, WiseLabels.Services.ChatService>();
builder.Services.AddScoped<WiseLabels.Services.ICustomerContactService, WiseLabels.Services.CustomerContactService>();
builder.Services.AddScoped<WiseLabels.Services.IUserImpersonationService, WiseLabels.Services.UserImpersonationService>();
builder.Services.AddScoped<WiseLabels.Services.ILineItemCatalogService, WiseLabels.Services.LineItemCatalogService>();
// NOTE: CermDbContext is registered by AddCermDataAccessEF below (which also applies
// UseCompatibilityLevel(120) and EnableRetryOnFailure). Do not re-register it here -
// AddDbContext uses TryAdd, so a duplicate registration silently discards the other
// one's options.
builder.Services.AddScoped<CERM.DataAccess.Repositories.Job.IJobRepository, CERM.DataAccess.Repositories.Job.JobRepositoryEF>();
builder.Services.AddScoped<CERM.DataAccess.Repositories.Substrate.ISubstrateRepository, CERM.DataAccess.Repositories.Substrate.SubstrateRepositoryEF>();

// Choose ONE of these approaches:

// Option 1: Use Entity Framework Core only
builder.Services.AddCermDataAccessEF(builder.Configuration);
// Option 2: Use Dapper only
//builder.Services.AddCermDataAccessDapper(builder.Configuration);
// Option 3: Use both (EF as default)
//builder.Services.AddCermDataAccessBoth(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapRazorPages();

app.Run();
