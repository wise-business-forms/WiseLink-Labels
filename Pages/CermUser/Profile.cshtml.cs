using CERM.DataAccess;
using CERM.DataAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WiseLabels.Pages.CermUser
{
    public class ProfileModel : PageModel
    {
        private readonly CermDbContext _context;
        private readonly ILogger<ProfileModel> _logger;

        public ProfileModel(CermDbContext context, ILogger<ProfileModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Function { get; set; }
        public string UserType { get; set; } = string.Empty;
        
        // Representative-specific properties
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? Fax { get; set; }
        public string? Department { get; set; }
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? CountryCode { get; set; }
        public double? Commission { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToPage("/Index");
            }

            UserId = userId;

            // Try to find in system users (paswrd__) first
            var systemUser = await _context.CermUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == userId);

            if (systemUser != null)
            {
                UserName = systemUser.Username;
                Email = systemUser.Email;
                Function = systemUser.Function;
                UserType = "System User";
                
                _logger.LogInformation("Loaded system user profile for {UserId}", userId);
                return Page();
            }

            // Try to find in sales team (verte___)
            var representative = await _context.Representatives
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == userId);

            if (representative != null)
            {
                UserId = representative.Id;
                UserName = representative.Name ?? representative.Id;
                Email = representative.Email;
                Function = representative.Function;
                Phone = representative.Phone;
                Mobile = representative.Mobile;
                Fax = representative.Fax;
                Department = representative.Department;
                Street = representative.Street;
                City = representative.City;
                State = representative.State;
                PostalCode = representative.PostalCode;
                CountryCode = representative.CountryCode;
                Commission = representative.Commission;
                UserType = "Sales Representative";

                _logger.LogInformation("Loaded representative profile for {UserId}", userId);
                return Page();
            }

            _logger.LogWarning("User {UserId} not found in CERM database", userId);
            return RedirectToPage("/Index");
        }
    }
}
