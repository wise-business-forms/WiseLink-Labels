using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CERM.DataAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WiseLabels.Services;

namespace WiseLabels.Pages.Admin
{
    public class IndexModel : PageModel
    {
        private readonly CermDbContext _context;
        private readonly ILogger<IndexModel> _logger;
        private readonly IUserImpersonationService _impersonationService;
        private const string ImpersonationSessionKey = "AdminImpersonatedUserId";

        public IndexModel(
            CermDbContext context, 
            ILogger<IndexModel> logger,
            IUserImpersonationService impersonationService)
        {
            _context = context;
            _logger = logger;
            _impersonationService = impersonationService;
        }

        public List<UserInfo> Users { get; set; } = new();
        public string? CurrentImpersonatedUserId { get; set; }
        public string? CurrentUserEmail { get; set; }
        public string? CurrentCermUsername { get; set; }
        public bool IsAdmin { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? UserTypeFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if current user is an admin first
            IsAdmin = await _impersonationService.IsCurrentUserAdminAsync();

            // If not admin, deny access
            if (!IsAdmin)
            {
                _logger.LogWarning("Non-admin user {Email} attempted to access Admin page", 
                    User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name);
                return Forbid();
            }

            CurrentImpersonatedUserId = HttpContext.Session.GetString(ImpersonationSessionKey);

            // Get current authenticated user's email from Azure AD claims
            CurrentUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? 
                              User.FindFirstValue("preferred_username") ?? 
                              User.FindFirstValue(ClaimTypes.Name);

            // Load users from CERM database (both system users and sales team)
            var allUsers = await LoadUsersFromDatabase();

            // Apply user type filter if specified
            if (!string.IsNullOrWhiteSpace(UserTypeFilter) && UserTypeFilter != "all")
            {
                Users = allUsers.Where(u => u.UserType.Equals(UserTypeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                _logger.LogInformation("Filtered to {Count} users of type {Type}", Users.Count, UserTypeFilter);
            }
            else
            {
                Users = allUsers;
            }

            // Find current user's CERM profile by email
            if (!string.IsNullOrWhiteSpace(CurrentUserEmail))
            {
                var currentCermUser = Users.FirstOrDefault(u => 
                    !string.IsNullOrWhiteSpace(u.Email) && 
                    u.Email.Equals(CurrentUserEmail, StringComparison.OrdinalIgnoreCase));

                if (currentCermUser != null)
                {
                    CurrentCermUsername = currentCermUser.UserId;
                    _logger.LogInformation("Current user {Email} matched to CERM user {Username} ({Type}). IsAdmin: {IsAdmin}", 
                        CurrentUserEmail, CurrentCermUsername, currentCermUser.UserType, IsAdmin);
                }
                else
                {
                    _logger.LogWarning("Current user {Email} not found in CERM users or sales team. IsAdmin: {IsAdmin}", 
                        CurrentUserEmail, IsAdmin);
                }
            }

            _logger.LogInformation("Admin page loaded. Currently impersonating: {UserId}", CurrentImpersonatedUserId ?? "none");

            return Page();
        }

        public async Task<IActionResult> OnPostSetImpersonationAsync(string userId)
        {
            // Check if current user is an admin
            var isAdmin = await _impersonationService.IsCurrentUserAdminAsync();
            if (!isAdmin)
            {
                _logger.LogWarning("Non-admin user attempted to set impersonation");
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                // Clear impersonation
                HttpContext.Session.Remove(ImpersonationSessionKey);
                _logger.LogInformation("Impersonation cleared");
            }
            else
            {
                // Set impersonation
                HttpContext.Session.SetString(ImpersonationSessionKey, userId);
                _logger.LogInformation("Now impersonating user: {UserId}", userId);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClearImpersonation()
        {
            // Check if current user is an admin
            var isAdmin = await _impersonationService.IsCurrentUserAdminAsync();
            if (!isAdmin)
            {
                _logger.LogWarning("Non-admin user attempted to clear impersonation");
                return Forbid();
            }

            HttpContext.Session.Remove(ImpersonationSessionKey);
            _logger.LogInformation("Impersonation cleared via button");

            // Redirect to the referring page if available, otherwise go to home
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                // Get the path from the referer URL (excluding query string to avoid re-triggering the same handler)
                var refererPath = refererUri.LocalPath;
                if (!refererPath.Contains("/Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(refererPath);
                }
            }

            return RedirectToPage("/Index");
        }

        private async Task<List<UserInfo>> LoadUsersFromDatabase()
        {
            var allUsers = new List<UserInfo>();

            try
            {
                // Load system users from paswrd__ table where sysbehr_ = 'Y'
                var cermUsers = await _context.CermUsers
                    .AsNoTracking()
                    .Where(u => u.SystemUser == "Y")
                    .ToListAsync();

                allUsers.AddRange(cermUsers.Select(u => new UserInfo
                {
                    UserId = u.Username,
                    UserName = u.Username, // Use username as display name
                    Email = u.Email,
                    Function = u.Function,
                    UserType = "System User",
                    HasActiveFilters = false
                }));

                _logger.LogInformation("Loaded {Count} system users from paswrd__ table", cermUsers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load system users from CERM database");
            }

            try
            {
                // Load sales team from verte___ table
                var salesTeam = await _context.Representatives
                    .AsNoTracking()
                    .Where(r => !string.IsNullOrWhiteSpace(r.Email)) // Only include reps with email addresses
                    .ToListAsync();

                allUsers.AddRange(salesTeam.Select(r => new UserInfo
                {
                    UserId = r.Id,
                    UserName = r.Name ?? r.Id,
                    Email = r.Email,
                    Function = r.Function ?? r.Responsibility,
                    UserType = "Sales Team",
                    HasActiveFilters = false
                }));

                _logger.LogInformation("Loaded {Count} sales team members from verte___ table", salesTeam.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load sales team from CERM database");
            }

            // Sort by name, then by user type
            return allUsers
                .OrderBy(u => u.UserName)
                .ThenBy(u => u.UserType)
                .ToList();
        }

        public class UserInfo
        {
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? Function { get; set; }
            public string UserType { get; set; } = string.Empty; // "System User" or "Sales Team"
            public bool HasActiveFilters { get; set; }
        }
    }
}
