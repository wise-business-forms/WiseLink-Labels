using System.Security.Claims;
using CERM.DataAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WiseLabels.Configuration;

namespace WiseLabels.Services
{
    public interface IUserImpersonationService
    {
        string? GetImpersonatedUserId();
        Task<string?> GetImpersonatedUserNameAsync();
        bool IsImpersonating();
        void SetImpersonation(string userId);
        void ClearImpersonation();
        Task<string?> GetCurrentUserCermIdAsync();
        Task<string?> GetEffectiveUserIdAsync();
        Task<bool> IsCurrentUserAdminAsync();
        Task<bool> ShouldFilterDataAsync();
    }

    public class UserImpersonationService : IUserImpersonationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CermDbContext _context;
        private readonly UserFilteringOptions _filteringOptions;
        private const string ImpersonationSessionKey = "AdminImpersonatedUserId";

        public UserImpersonationService(
            IHttpContextAccessor httpContextAccessor,
            CermDbContext context,
            IOptions<UserFilteringOptions> filteringOptions)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _filteringOptions = filteringOptions.Value;
        }

        public string? GetImpersonatedUserId()
        {
            return _httpContextAccessor.HttpContext?.Session.GetString(ImpersonationSessionKey);
        }

        public async Task<string?> GetImpersonatedUserNameAsync()
        {
            var impersonatedUserId = GetImpersonatedUserId();
            if (string.IsNullOrWhiteSpace(impersonatedUserId))
            {
                return null;
            }

            // Try to find in system users (paswrd__) first
            var systemUser = await _context.CermUsers
                .AsNoTracking()
                .Where(u => u.Username == impersonatedUserId)
                .FirstOrDefaultAsync();

            if (systemUser != null)
            {
                return systemUser.Username;
            }

            // Try to find in sales team (verte___)
            var salesRep = await _context.Representatives
                .AsNoTracking()
                .Where(r => r.Id == impersonatedUserId)
                .FirstOrDefaultAsync();

            return salesRep?.Name;
        }

        public bool IsImpersonating()
        {
            return !string.IsNullOrWhiteSpace(GetImpersonatedUserId());
        }

        public void SetImpersonation(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            _httpContextAccessor.HttpContext?.Session.SetString(ImpersonationSessionKey, userId);
        }

        public void ClearImpersonation()
        {
            _httpContextAccessor.HttpContext?.Session.Remove(ImpersonationSessionKey);
        }

        /// <summary>
        /// Gets the current authenticated user's CERM ID by matching their email
        /// to either the paswrd__ table (system users) or verte___ table (sales team)
        /// </summary>
        public async Task<string?> GetCurrentUserCermIdAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null || !httpContext.User.Identity?.IsAuthenticated == true)
            {
                return null;
            }

            // Get user's email from Azure AD claims
            var userEmail = httpContext.User.FindFirstValue(ClaimTypes.Email) ??
                           httpContext.User.FindFirstValue("preferred_username") ??
                           httpContext.User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(userEmail))
            {
                return null;
            }

            // Try to find in system users (paswrd__) first
            var systemUser = await _context.CermUsers
                .AsNoTracking()
                .Where(u => u.SystemUser == "Y" && u.Email == userEmail)
                .FirstOrDefaultAsync();

            if (systemUser != null)
            {
                return systemUser.Username;
            }

            // Try to find in sales team (verte___)
            var salesRep = await _context.Representatives
                .AsNoTracking()
                .Where(r => r.Email == userEmail)
                .FirstOrDefaultAsync();

            return salesRep?.Id;
        }

        /// <summary>
        /// Gets the effective user ID for data filtering purposes.
        /// Returns impersonated user if impersonating, otherwise returns current user's CERM ID.
        /// </summary>
        public async Task<string?> GetEffectiveUserIdAsync()
        {
            // If impersonating, use impersonated user ID
            var impersonatedUserId = GetImpersonatedUserId();
            if (!string.IsNullOrWhiteSpace(impersonatedUserId))
            {
                return impersonatedUserId;
            }

            // Otherwise, return current user's CERM ID
            return await GetCurrentUserCermIdAsync();
        }

        /// <summary>
        /// Checks if the current authenticated user is defined as an administrator
        /// in the UserFiltering:Administrators configuration
        /// </summary>
        public async Task<bool> IsCurrentUserAdminAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null || !httpContext.User.Identity?.IsAuthenticated == true)
            {
                return false;
            }

            // Get user's email and username
            var userEmail = httpContext.User.FindFirstValue(ClaimTypes.Email) ??
                           httpContext.User.FindFirstValue("preferred_username");

            var userName = httpContext.User.FindFirstValue(ClaimTypes.Name);

            // Get CERM user ID
            var cermUserId = await GetCurrentUserCermIdAsync();

            // Check if any identifier matches the admin list
            var adminList = _filteringOptions.Administrators ?? new List<string>();
            return adminList.Any(admin =>
                (!string.IsNullOrWhiteSpace(userEmail) && admin.Equals(userEmail, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(userName) && admin.Equals(userName, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(cermUserId) && admin.Equals(cermUserId, StringComparison.OrdinalIgnoreCase))
            );
        }

        /// <summary>
        /// Checks if the given user ID belongs to a system user (administrator)
        /// </summary>
        private async Task<bool> IsSystemUserAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            // Check if user is in the admin configuration list
            var adminList = _filteringOptions.Administrators ?? new List<string>();
            if (adminList.Any(admin => admin.Equals(userId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Check if user exists in paswrd__ table with SystemUser = 'Y'
            var systemUser = await _context.CermUsers
                .AsNoTracking()
                .Where(u => u.Username == userId && u.SystemUser == "Y")
                .FirstOrDefaultAsync();

            return systemUser != null;
        }

        /// <summary>
        /// Determines if data should be filtered based on user.
        /// Returns true if:
        /// - User is NOT an admin/system user, OR
        /// - User IS an admin but is impersonating a non-admin user (sales rep)
        /// Returns false if:
        /// - User is an admin and NOT impersonating
        /// - User is an admin impersonating another admin/system user
        /// </summary>
        public async Task<bool> ShouldFilterDataAsync()
        {
            var isAdmin = await IsCurrentUserAdminAsync();
            var isImpersonating = IsImpersonating();

            // If user is not an admin, always filter
            if (!isAdmin)
            {
                return true;
            }

            // User is an admin - check if impersonating
            if (!isImpersonating)
            {
                // Admin not impersonating - no filtering
                return false;
            }

            // Admin is impersonating - check if impersonating another system user
            var impersonatedUserId = GetImpersonatedUserId();
            var impersonatedUserIsSystemUser = await IsSystemUserAsync(impersonatedUserId);

            // Only filter if impersonating a non-system user (sales rep)
            return !impersonatedUserIsSystemUser;
        }
    }
}
