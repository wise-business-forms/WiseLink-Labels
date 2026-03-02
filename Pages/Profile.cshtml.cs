using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using WiseLabels.Authorization;

namespace WiseLabels.Pages
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly AccessControlOptions _accessOptions;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<ProfileModel> _logger;
        private readonly IConfiguration _configuration;

        public ProfileModel(
            IOptions<AccessControlOptions> accessOptions,
            IAuthorizationService authorizationService,
            ILogger<ProfileModel> logger,
            IConfiguration configuration)
        {
            _accessOptions = accessOptions.Value;
            _authorizationService = authorizationService;
            _logger = logger;
            _configuration = configuration;
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
                    //  if can't seem to get showing the AD group names working so doing this instead
                    var _claim = claim.Value;
                    
                    if (claim.Value == "93c0e017-7f53-4b5f-8f94-54775a6eeb48")
                        _claim = "MFA_Conditional_Access_ALP";


                    Groups.Add(_claim);
                }

                // Store all claims for debugging
                var claimType = claim.Type.Split('/').Last();
                if (!AllClaims.ContainsKey(claimType))
                {
                    AllClaims[claimType] = claim.Value;
                }
            }

            //Groups.Clear();
            //foreach (string s in GetAdGroupNamesForCurrentUser())
            //{
            //    Groups.Add(s);
            //}

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

        public async Task<string> GetAzureGroupNameAsync(string groupObjectId)
        {
            // 1. Setup Authentication (ClientSecretCredential for server-to-server)
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var clientSecretCredential = new ClientSecretCredential(
                "977e68d4-b494-4f1c-a460-575f8ae1abef", "386586ae-c86b-4692-b87a-d40f5f87cdb9", "G4g8Q~rII4Wxl-NLygSoRxzq82AXKKEjjs6yEdi-");

            var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

            try
            {
                // 2. Fetch the group by ID and only select the displayName
                var group = await graphClient.Groups[groupObjectId].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "displayName" };
                });

                return group?.DisplayName;
            }
            catch (ServiceException ex)
            {
                // Handle cases where the group ID doesn't exist
                return $"Error: {ex.Message}";
            }
        }
    }
}
