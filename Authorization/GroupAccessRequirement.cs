using Microsoft.AspNetCore.Authorization;

namespace WiseLabels.Authorization
{
    public enum AccessLevel
    {
        Limited,
        Full
    }

    public class GroupAccessRequirement : IAuthorizationRequirement
    {
        public GroupAccessRequirement(AccessLevel level)
        {
            Level = level;
        }

        public AccessLevel Level { get; }
    }
}
