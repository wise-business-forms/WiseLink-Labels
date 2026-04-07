using CERM.DataAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseLabels.Pages.Api
{
    public class CustomerContactsModel : PageModel
    {
        private readonly CermDbContext _context;
        private readonly ILogger<CustomerContactsModel> _logger;

        public CustomerContactsModel(CermDbContext context, ILogger<CustomerContactsModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return BadRequest(new { error = "Customer ID is required" });
            }

            // Deny access to special accounts (starting with #, *, or -)
            if (customerId.StartsWith("#") || customerId.StartsWith("*") || customerId.StartsWith("-"))
            {
                _logger.LogWarning("Access to contacts for special account {CustomerId} denied", customerId);
                return Forbid();
            }

            try
            {
                var contacts = await _context.Contacts
                    .AsNoTracking()
                    .Where(c => c.CustomerId == customerId && c.IsActive == "Y")
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new
                    {
                        contactId = c.ContactId,
                        customerId = c.CustomerId,
                        firstName = c.FirstName,
                        lastName = c.LastName,
                        fullName = c.FullName,
                        email = c.Email,
                        phone = c.Phone,
                        jobTitle = c.JobTitle
                    })
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} contacts for customer {CustomerId}", contacts.Count, customerId);

                return new JsonResult(contacts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contacts for customer {CustomerId}", customerId);
                return StatusCode(500, new { error = "Failed to load contacts" });
            }
        }
    }
}
