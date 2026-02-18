using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace WiseLabels.Authorization
{
    public class GroupAccessHandler : AuthorizationHandler<GroupAccessRequirement>
    {
        private readonly AccessControlOptions _options;
        private readonly ILogger<GroupAccessHandler> _logger;

        public GroupAccessHandler(IOptions<AccessControlOptions> options, ILogger<GroupAccessHandler> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, GroupAccessRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                return Task.CompletedTask;
            }

            var userIdentifier = GetUserIdentifier(context.User);
            var groupValues = GetGroupClaims(context.User).ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool hasFullUserOverride = !string.IsNullOrWhiteSpace(userIdentifier) &&
                _options.FullAccessUsers.Any(u => string.Equals(u, userIdentifier, StringComparison.OrdinalIgnoreCase));

            bool hasLimitedUserOverride = !string.IsNullOrWhiteSpace(userIdentifier) &&
                _options.LimitedAccessUsers.Any(u => string.Equals(u, userIdentifier, StringComparison.OrdinalIgnoreCase));

            bool hasFullGroup = _options.FullAccessGroups.Any(value => groupValues.Contains(value));
            bool hasLimitedGroup = _options.LimitedAccessGroups.Any(value => groupValues.Contains(value));

            var hasFullAccess = hasFullGroup || hasFullUserOverride;
            var hasLimitedAccess = hasFullAccess || hasLimitedGroup || hasLimitedUserOverride;

            if (requirement.Level == AccessLevel.Full && hasFullAccess)
            {
                context.Succeed(requirement);
            }
            else if (requirement.Level == AccessLevel.Limited && hasLimitedAccess)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {User} failed {Level} access check. FullAccess={FullAccess}, LimitedAccess={LimitedAccess}",
                    userIdentifier ?? "(unknown)", requirement.Level, hasFullAccess, hasLimitedAccess);
            }

            return Task.CompletedTask;
        }

        private static string? GetUserIdentifier(ClaimsPrincipal user)
        {
            return user.FindFirst("preferred_username")?.Value
                ?? user.FindFirst(ClaimTypes.Upn)?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? user.Identity?.Name;
        }

        private static IEnumerable<string> GetGroupClaims(ClaimsPrincipal user)
        {
            foreach (var claim in user.Claims)
            {
                if (claim.Type is ClaimTypes.Role or ClaimTypes.GroupSid ||
                    string.Equals(claim.Type, "groups", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase))
                {
                    yield return claim.Value;
                }
            }
        }
    }
}
