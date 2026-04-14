using WiseLabels.Shared.Models;

namespace WiseLabels.Shared.Services;

/// <summary>
/// Looks up a <see cref="DistributorProfile"/> by the access token embedded in a
/// white-label portal URL, or by the subdomain label in the request host header.
/// </summary>
public interface IDistributorProfileService
{
    /// <summary>
    /// Returns the <see cref="DistributorProfile"/> whose <c>Tokens</c> array contains
    /// <paramref name="token"/>, or <c>null</c> if no matching profile exists.
    /// </summary>
    DistributorProfile? FindByToken(string token);

    /// <summary>
    /// Returns the <see cref="DistributorProfile"/> whose <c>Subdomain</c> matches
    /// <paramref name="subdomain"/> (case-insensitive), or <c>null</c> if no matching
    /// profile exists.
    /// </summary>
    DistributorProfile? FindBySubdomain(string subdomain);
}
