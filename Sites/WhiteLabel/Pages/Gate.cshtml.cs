using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using WiseLabels.Shared.Middleware;
using WiseLabels.Shared.Models;
using WiseLabels.Shared.Services;

namespace WiseLabels.WhiteLabel.Pages;

/// <summary>
/// Landing gate page for the white-label distributor portal.
///
/// Supports two identification modes, tried in order:
///
/// **Mode 1 — Subdomain routing (recommended):**
/// The distributor is identified by their subdomain (e.g. <c>abc-printing.labels-tags.com</c>).
/// <see cref="SubdomainDistributorMiddleware"/> has already resolved the
/// <see cref="DistributorProfile"/> from the <c>Host</c> header and stored it in
/// <see cref="HttpContext.Items"/>. No token in the URL is required.
/// Customer URL example: <c>https://abc-printing.labels-tags.com/gate</c>
///
/// **Mode 2 — Token-in-URL (fallback):**
/// The distributor is identified by a private token in the URL path.
/// Used when accessing the portal through the base App Service hostname or as a
/// fallback when no subdomain is configured for a distributor.
/// Customer URL example: <c>https://orders.labels-tags.com/gate/&lt;token&gt;</c>
/// </summary>
public class GateModel : PageModel
{
    internal const string SessionKeyAccess = "wl_access";
    internal const string SessionKeyValidated = "wl_token_validated";
    internal const string SessionKeyProfile = "wl_distributor_profile";

    private readonly IDistributorProfileService _profiles;

    public bool TokenValid { get; private set; }
    public DistributorProfile? Profile { get; private set; }

    public GateModel(IDistributorProfileService profiles)
    {
        _profiles = profiles;
    }

    public IActionResult OnGet(string? token)
    {
        // Mode 1: subdomain-identified — profile was resolved by SubdomainDistributorMiddleware
        Profile = HttpContext.Items[SubdomainDistributorMiddleware.DistributorProfileItemKey]
                      as DistributorProfile;

        // Mode 2: token-in-URL fallback
        if (Profile is null && !string.IsNullOrWhiteSpace(token))
        {
            Profile = _profiles.FindByToken(token);
        }

        TokenValid = Profile is not null;

        if (TokenValid)
        {
            // Store a validation flag and the serialized profile server-side in session.
            // The profile is stored so the acknowledgement POST (which does not carry the
            // token or subdomain) can still write the profile into the access-granted session
            // without re-exposing the token to the client.
            HttpContext.Session.SetString(SessionKeyValidated, "yes");
            HttpContext.Session.SetString(SessionKeyProfile,
                JsonSerializer.Serialize(Profile));
        }

        return Page();
    }

    public IActionResult OnPostAcknowledge(bool confirmed)
    {
        if (!confirmed)
            return RedirectToPage("/Gate");

        var validated = HttpContext.Session.GetString(SessionKeyValidated);
        if (validated != "yes")
            return RedirectToPage("/Gate");

        var profileJson = HttpContext.Session.GetString(SessionKeyProfile);
        if (string.IsNullOrEmpty(profileJson))
            return RedirectToPage("/Gate");

        // Upgrade: remove the intermediate validation flag, keep the profile, grant access
        HttpContext.Session.Remove(SessionKeyValidated);
        HttpContext.Session.SetString(SessionKeyAccess, "granted");
        // SessionKeyProfile remains so pages can read the distributor branding

        return RedirectToPage("/Index");
    }
}
