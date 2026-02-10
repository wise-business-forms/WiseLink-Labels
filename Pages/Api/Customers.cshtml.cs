using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using CERM.DataAccess.Models;
using CERM.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace WiseLabels.Pages.Api
{
    [IgnoreAntiforgeryToken]
    public class CustomersModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CustomersModel> _logger;
        private readonly CermDbContext _context;

        public CustomersModel(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<CustomersModel> logger, CermDbContext context)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(string? name = null)
        {
            _logger.LogInformation("Customers OnGetAsync called with name filter: {NameFilter}", name ?? "none");
            try
            {
                // Fetch customers with optional name filter
                var customers = await FetchCustomersAsync(name);

                return new JsonResult(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customers from CERM API");
                return new JsonResult(new { error = $"Error loading customers: {ex.Message}" })
                {
                    StatusCode = 500
                };
            }
        }
        private async Task<List<Customer>> FetchCustomersAsync(string? nameFilter = null)
        {
            // TODO: Replace with proper database query using CERM.DataAccess
            _logger.LogInformation("Fetching customers from database. Name filter: {NameFilter}", nameFilter ?? "none");

            // Stub: SELECT from sqlb00.dbo.klabas__ table
            var customers = await _context.Customers.FromSqlRaw(
                "SELECT kla__ref, CAST(uuid AS nvarchar(50)) AS uuid, gln_____, ape___nr, naam____ FROM sqlb00.dbo.klabas__"
            ).ToListAsync();

            return customers;
        }
    }
}
