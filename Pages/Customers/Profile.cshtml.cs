using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CERM.DataAccess;
using CERM.DataAccess.Models;
using CERM.DataAccess.Repositories.OrderConfirmation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WiseLabels.Services;

namespace WiseLabels.Pages.Customers
{
    public class ProfileModel : PageModel
    {
        private readonly CermDbContext _context;
        private readonly ILogger<ProfileModel> _logger;
        private readonly IUserImpersonationService _impersonationService;
        private readonly IConfiguration _configuration;
        private readonly IOrderConfirmationRepository _orderConfirmationRepository;

        public ProfileModel(
            CermDbContext context,
            ILogger<ProfileModel> logger,
            IUserImpersonationService impersonationService,
            IConfiguration configuration,
            IOrderConfirmationRepository orderConfirmationRepository)
        {
            _context = context;
            _logger = logger;
            _impersonationService = impersonationService;
            _configuration = configuration;
            _orderConfirmationRepository = orderConfirmationRepository;
        }

        public Customer? Customer { get; set; }
        public Representative? Representative { get; set; }
        public List<OrderConfirmation> OrderHistory { get; set; } = new();
        public Estimate? SelectedEstimate { get; set; }
        public string GoogleMapsApiKey => _configuration["GoogleMaps:ApiKey"] ?? "";

        public async Task<IActionResult> OnGetAsync(string customerId, string? estimateId = null)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return RedirectToPage("/Customers");
            }

            // Check if this is a special account (starts with #, *, or -)
            if (customerId.StartsWith("#") || customerId.StartsWith("*") || customerId.StartsWith("-"))
            {
                _logger.LogWarning("Access to special account {CustomerId} denied", customerId);
                return RedirectToPage("/Customers");
            }

            // Check if user should see filtered data
            var shouldFilter = await _impersonationService.ShouldFilterDataAsync();
            var effectiveUserId = await _impersonationService.GetEffectiveUserIdAsync();

            // Load customer
            var customerQuery = _context.Customers
                .AsNoTracking()
                .Where(c => !c.Name.StartsWith("#") && !c.Name.StartsWith("*") && !c.Name.StartsWith("-") &&
                           !c.Id.StartsWith("#") && !c.Id.StartsWith("*") && !c.Id.StartsWith("-"));

            // Apply filtering if needed
            if (shouldFilter && !string.IsNullOrWhiteSpace(effectiveUserId))
            {
                customerQuery = customerQuery.Where(c => c.RepresentativeId == effectiveUserId);
            }

            Customer = await customerQuery.FirstOrDefaultAsync(c => c.Id == customerId);

            if (Customer == null)
            {
                _logger.LogWarning("Customer {CustomerId} not found or user doesn't have access", customerId);
                return RedirectToPage("/Customers");
            }

            // Load representative if exists
            if (!string.IsNullOrWhiteSpace(Customer.RepresentativeId))
            {
                Representative = Customer.Representative ?? await _context.Representatives
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == Customer.RepresentativeId);
            }

            // Load order history for this customer from v1bon___
            var orders = await _orderConfirmationRepository.GetByCustomerRefAsync(customerId);
            OrderHistory = orders.Take(20).ToList();

            // Load selected estimate if provided
            if (!string.IsNullOrWhiteSpace(estimateId))
            {
                SelectedEstimate = await _context.Estimates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EstimateId == estimateId && e.CustomerId == customerId);

                if (SelectedEstimate != null)
                {
                    _logger.LogInformation("Loaded estimate {EstimateId} for customer {CustomerId}", estimateId, customerId);
                }
                else
                {
                    _logger.LogWarning("Estimate {EstimateId} not found for customer {CustomerId}", estimateId, customerId);
                }
            }

            _logger.LogInformation("Loaded customer profile for {CustomerId}: {CustomerName} with {OrderCount} recent orders",
                customerId, Customer.Name, OrderHistory.Count);

            return Page();
        }

        public string GetFullAddress()
        {
            if (Customer == null)
                return string.Empty;

            var addressParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Customer.Street))
                addressParts.Add(Customer.Street);

            if (!string.IsNullOrWhiteSpace(Customer.City))
                addressParts.Add(Customer.City);

            if (!string.IsNullOrWhiteSpace(Customer.State))
                addressParts.Add(Customer.State);

            if (!string.IsNullOrWhiteSpace(Customer.PostalCode))
                addressParts.Add(Customer.PostalCode);

            if (!string.IsNullOrWhiteSpace(Customer.Country))
                addressParts.Add(Customer.Country);

            return string.Join(", ", addressParts);
        }
    }
}
