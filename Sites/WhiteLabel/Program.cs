using WiseLabels.Shared.Middleware;
using WiseLabels.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Register the distributor profile lookup service (reads Sites:WhiteLabel:Distributors config)
builder.Services.AddSingleton<IDistributorProfileService, DistributorProfileService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// Subdomain distributor middleware — runs before the gate so that the Gate page
// can find the DistributorProfile in HttpContext.Items when routing by subdomain.
// e.g. abc-printing.labels-tags.com → profile for "abc-printing"
var apexDomain = app.Configuration["Sites:WhiteLabel:ApexDomain"] ?? "labels-tags.com";
app.UseSubdomainDistributor(apexDomain);

// Gate middleware — must come after UseSession so the session is available
// "wl_access" is the white-label portal session key
app.UsePortalGate("wl_access");

app.MapRazorPages();
app.Run();
