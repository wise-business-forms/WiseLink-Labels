using WiseLabels.Shared.Models;

namespace WiseLabels.Shared.Services;

/// <summary>
/// Looks up a <see cref="DistributorProfile"/> by the access token embedded in a
/// white-label portal URL.
/// </summary>
public interface IDistributorProfileService
{
    /// <summary>
    /// Returns the <see cref="DistributorProfile"/> whose <c>Tokens</c> array contains
    /// <paramref name="token"/>, or <c>null</c> if no matching profile exists.
    /// </summary>
    DistributorProfile? FindByToken(string token);
}
