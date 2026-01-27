using CERM.DataAccess;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddScoped<WiseLabels.Services.IQuoteService, WiseLabels.Services.QuoteService>();
builder.Services.AddScoped<WiseLabels.Services.IEmailService, WiseLabels.Services.EmailService>();
builder.Services.AddDbContext<CermDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("CermDatabase")));
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

app.UseAuthorization();

app.MapRazorPages();

app.Run();
