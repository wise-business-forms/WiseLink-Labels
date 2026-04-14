using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WiseLabels.Shared.Models;

namespace WiseLabels.Shared.Services;

/// <summary>
/// Loads all distributor profiles from the "Sites:WhiteLabel:Distributors" configuration
/// section at startup and provides O(1) token → profile lookups at request time.
///
/// Configuration shape (appsettings.json / Azure App Settings):
/// <code>
/// "Sites": {
///   "WhiteLabel": {
///     "Distributors": [
///       {
///         "Slug": "abc-printing",
///         "CompanyName": "ABC Printing Co.",
///         "LogoUrl": "https://yourcdn.com/logos/abc.png",
///         "LogoAlt": "ABC Printing Co.",
///         "ContactName": "Jane Doe",
///         "ContactPhone": "(555) 100-2000",
///         "ContactEmail": "jane@abcprinting.com",
///         "PrimaryColor": "#003399",
///         "ReferencePrefix": "ABC-",
///         "Tokens": [ "token1abc", "token2abc" ]
///       }
///     ]
///   }
/// }
/// </code>
/// </summary>
public sealed class DistributorProfileService : IDistributorProfileService
{
    // token (case-sensitive) → profile
    private readonly Dictionary<string, DistributorProfile> _index;

    public DistributorProfileService(IConfiguration configuration, ILogger<DistributorProfileService> logger)
    {
        var profiles = configuration
            .GetSection("Sites:WhiteLabel:Distributors")
            .Get<DistributorProfile[]>() ?? [];

        _index = new Dictionary<string, DistributorProfile>(StringComparer.Ordinal);

        foreach (var profile in profiles)
        {
            foreach (var token in profile.Tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (!_index.TryAdd(token, profile))
                {
                    // Two distributor profiles share the same token — this is a misconfiguration.
                    var existing = _index[token];
                    logger.LogWarning(
                        "Duplicate distributor token detected. Token belongs to both '{ExistingSlug}' and '{NewSlug}'. " +
                        "The first profile ('{ExistingSlug}') will be used. Fix the configuration to avoid unexpected behavior.",
                        existing.Slug, profile.Slug, existing.Slug);
                }
            }
        }
    }

    /// <inheritdoc />
    public DistributorProfile? FindByToken(string token)
        => string.IsNullOrWhiteSpace(token) ? null
           : _index.TryGetValue(token, out var profile) ? profile : null;
}
