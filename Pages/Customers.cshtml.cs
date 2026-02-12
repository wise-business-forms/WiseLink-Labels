using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CERM.DataAccess;
using CERM.DataAccess.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WiseLabels.Models;

namespace WiseLabels.Pages
{
    public class CustomersModel : PageModel
    {
        private readonly CermDbContext _context;
        private readonly ILogger<CustomersModel> _logger;

        public CustomersModel(CermDbContext context, ILogger<CustomersModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public List<Customer> Customers { get; set; } = new();

        [BindProperty]
        public string? SelectedEstimateId { get; set; }

        [BindProperty]
        public string? SelectedCustomerId { get; set; }

        [BindProperty]
        public string? SelectedCustomerName { get; set; }

        [BindProperty]
        public string? SelectedDescription { get; set; }

        [BindProperty]
        public string? SelectedMaterial { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                Customers = await _context.Customers
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                var representativeIds = Customers
                    .Select(c => c.RepresentativeId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                if (representativeIds.Count > 0)
                {
                    var representatives = await _context.Representatives
                        .Where(r => representativeIds.Contains(r.Id))
                        .AsNoTracking()
                        .ToDictionaryAsync(r => r.Id);

                    foreach (var customer in Customers)
                    {
                        if (!string.IsNullOrWhiteSpace(customer.RepresentativeId) && representatives.TryGetValue(customer.RepresentativeId, out var rep))
                        {
                            customer.Representative = rep;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to load customers list");
                Customers = new List<Customer>();
            }
        }

        public IActionResult OnPostUseQuote()
        {
            if (string.IsNullOrWhiteSpace(SelectedEstimateId))
            {
                TempData["QuoteError"] = "Select a quote before continuing.";
                return RedirectToPage();
            }

            var selection = new QuoteRequest
            {
                EstimateId = SelectedEstimateId,
                CustomerId = SelectedCustomerId,
                CustomerDisplayName = SelectedCustomerName,
                Description = SelectedDescription,
                Material = SelectedMaterial,
                ReferenceType = "Past Quote",
                ReferenceValue = SelectedEstimateId
            };

            HttpContext.Session.SetString(SessionKeys.SelectedQuote, JsonSerializer.Serialize(selection));

            return RedirectToPage("/GetQuote");
        }
    }
}