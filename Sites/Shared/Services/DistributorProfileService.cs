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
    private readonly Dictionary<string, DistributorProfile> _tokenIndex;
    // subdomain label (case-insensitive, e.g. "abc-printing") → profile
    private readonly Dictionary<string, DistributorProfile> _subdomainIndex;

    public DistributorProfileService(IConfiguration configuration, ILogger<DistributorProfileService> logger)
    {
        var profiles = configuration
            .GetSection("Sites:WhiteLabel:Distributors")
            .Get<DistributorProfile[]>() ?? [];

        _tokenIndex = new Dictionary<string, DistributorProfile>(StringComparer.Ordinal);
        _subdomainIndex = new Dictionary<string, DistributorProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            foreach (var token in profile.Tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (!_tokenIndex.TryAdd(token, profile))
                {
                    // Two distributor profiles share the same token — this is a misconfiguration.
                    var existing = _tokenIndex[token];
                    logger.LogWarning(
                        "Duplicate distributor token detected. Token belongs to both '{ExistingSlug}' and '{NewSlug}'. " +
                        "The first profile ('{ExistingSlug}') will be used. Fix the configuration to avoid unexpected behavior.",
                        existing.Slug, profile.Slug, existing.Slug);
                }
            }

            if (!string.IsNullOrWhiteSpace(profile.Subdomain))
            {
                if (!_subdomainIndex.TryAdd(profile.Subdomain, profile))
                {
                    var existing = _subdomainIndex[profile.Subdomain];
                    logger.LogWarning(
                        "Duplicate distributor subdomain '{Subdomain}' detected. " +
                        "Belongs to both '{ExistingSlug}' and '{NewSlug}'. " +
                        "The first profile will be used.",
                        profile.Subdomain, existing.Slug, profile.Slug);
                }
            }
        }
    }

    /// <inheritdoc />
    public DistributorProfile? FindByToken(string token)
        => string.IsNullOrWhiteSpace(token) ? null
           : _tokenIndex.TryGetValue(token, out var profile) ? profile : null;

    /// <inheritdoc />
    public DistributorProfile? FindBySubdomain(string subdomain)
        => string.IsNullOrWhiteSpace(subdomain) ? null
           : _subdomainIndex.TryGetValue(subdomain, out var profile) ? profile : null;
}
