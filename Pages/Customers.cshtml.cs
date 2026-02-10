using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CERM.DataAccess.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;

namespace WiseLabels.Pages
{
    public class CustomersModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CustomersModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
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
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{Request.Scheme}://{Request.Host}/Api/Customers");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Customers = JsonSerializer.Deserialize<List<Customer>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Customer>();
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