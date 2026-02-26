using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using WiseLabels.Authorization;

namespace WiseLabels.Pages
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly AccessControlOptions _accessOptions;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<ProfileModel> _logger;

        public ProfileModel(
            IOptions<AccessControlOptions> accessOptions,
            IAuthorizationService authorizationService,
            ILogger<ProfileModel> logger)
        {
            _accessOptions = accessOptions.Value;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserPrincipalName { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public List<string> Roles { get; set; } = new();
        public AccessLevel AccessLevel { get; set; }
        public string AccessLevelDescription { get; set; } = string.Empty;
        public bool HasFullAccess { get; set; }
        public bool HasLimitedAccess { get; set; }
        public List<string> MatchedFullAccessGroups { get; set; } = new();
        public List<string> MatchedLimitedAccessGroups { get; set; } = new();
        public bool IsFullAccessUser { get; set; }
        public bool IsLimitedAccessUser { get; set; }
        public Dictionary<string, string> AllClaims { get; set; } = new();

        public async Task OnGetAsync()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            // Basic user information
            DisplayName = User.FindFirst("name")?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? User.Identity?.Name
                ?? "Unknown";

            Email = User.FindFirst("email")?.Value
                ?? User.FindFirst("upn")?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? string.Empty;

            UserPrincipalName = User.FindFirst(ClaimTypes.Upn)?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? string.Empty;

            ObjectId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? User.FindFirst("oid")?.Value
                ?? string.Empty;

            TenantId = User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
                ?? User.FindFirst("tid")?.Value
                ?? string.Empty;

            // Extract groups and roles from claims
            foreach (var claim in User.Claims)
            {
                if (claim.Type == ClaimTypes.Role || 
                    claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase))
                {
                    Roles.Add(claim.Value);
                }
                else if (claim.Type == ClaimTypes.GroupSid || 
                         claim.Type.Equals("groups", StringComparison.OrdinalIgnoreCase))
                {
                    Groups.Add(claim.Value);
                }

                // Store all claims for debugging
                var claimType = claim.Type.Split('/').Last();
                if (!AllClaims.ContainsKey(claimType))
                {
                    AllClaims[claimType] = claim.Value;
                }
            }

            // Determine access level
            var userIdentifier = Email ?? UserPrincipalName;
            var groupValues = Groups.Concat(Roles).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Check user-level overrides
            IsFullAccessUser = !string.IsNullOrWhiteSpace(userIdentifier) &&
                _accessOptions.FullAccessUsers.Any(u => string.Equals(u, userIdentifier, StringComparison.OrdinalIgnoreCase));

            IsLimitedAccessUser = !string.IsNullOrWhiteSpace(userIdentifier) &&
                _accessOptions.LimitedAccessUsers.Any(u => string.Equals(u, userIdentifier, StringComparison.OrdinalIgnoreCase));

            // Check group memberships
            MatchedFullAccessGroups = _accessOptions.FullAccessGroups
                .Where(configGroup => groupValues.Contains(configGroup))
                .ToList();

            MatchedLimitedAccessGroups = _accessOptions.LimitedAccessGroups
                .Where(configGroup => groupValues.Contains(configGroup))
                .ToList();

            // Verify access level via authorization service
            var fullAccessResult = await _authorizationService.AuthorizeAsync(User, "FullAccess");
            var limitedAccessResult = await _authorizationService.AuthorizeAsync(User, "LimitedAccess");

            HasFullAccess = fullAccessResult.Succeeded;
            HasLimitedAccess = limitedAccessResult.Succeeded;

            if (HasFullAccess)
            {
                AccessLevel = Authorization.AccessLevel.Full;
                AccessLevelDescription = "Full Access - You have complete access to all features.";
            }
            else if (HasLimitedAccess)
            {
                AccessLevel = Authorization.AccessLevel.Limited;
                AccessLevelDescription = "Limited Access - You have restricted access to certain features.";
            }
            else
            {
                AccessLevelDescription = "No Special Access - You have basic authenticated user access.";
            }

            _logger.LogInformation("Profile viewed by user {Email} with access level {AccessLevel}", 
                Email, HasFullAccess ? "Full" : (HasLimitedAccess ? "Limited" : "None"));
        }
    }
}
