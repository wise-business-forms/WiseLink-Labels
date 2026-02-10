using System;
using System.Linq;
using System.Threading.Tasks;
using CERM.DataAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace WiseLabels.Pages.Api
{
    [IgnoreAntiforgeryToken]
    public class EstimatesModel : PageModel
    {
        private readonly ILogger<EstimatesModel> _logger;
        private readonly CermDbContext _context;

        public EstimatesModel(ILogger<EstimatesModel> logger, CermDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(string? customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return BadRequest(new { error = "customerId is required" });
            }

            try
            {
                var estimates = await _context.Estimates
                    .AsNoTracking()
                    .Where(e => e.CustomerId == customerId)
                    .OrderByDescending(e => e.OrderDate ?? DateTime.MinValue)
                    .Take(50)
                    .Select(e => new
                    {
                        e.EstimateId,
                        e.Description,
                        e.OrderDate,
                        e.DeliveryDate,
                        e.StatusCode,
                        e.CustomerId,
                        e.CustomerName,
                        e.ContactName,
                        e.PhoneNumber,
                        e.Email
                    })
                    .ToListAsync();


                return new JsonResult(estimates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load estimates for customer {CustomerId}", customerId);
                return StatusCode(500, new { error = "Unable to load estimates." });
            }
        }
    }
}
