using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using WiseLabels.Shared.Models;
using WiseLabels.Shared.Services;

namespace WiseLabels.WhiteLabel.Pages;

/// <summary>
/// Landing gate page for the white-label distributor portal.
///
/// Flow:
/// 1. Distributor sends their customers a private URL:
///    https://order.wiselabels.com/gate/&lt;distributor-token&gt;
/// 2. This page looks up the token → DistributorProfile in configuration.
/// 3. If valid, the profile (company name, logo, contact info) is serialized into
///    the server-side session so every downstream page can render the distributor's branding.
/// 4. The visitor sees a one-click acknowledgement (checkbox) before being admitted.
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
        if (string.IsNullOrWhiteSpace(token))
        {
            TokenValid = false;
            return Page();
        }

        Profile = _profiles.FindByToken(token);
        TokenValid = Profile is not null;

        if (TokenValid)
        {
            // Store a validation flag and the serialized profile server-side in session.
            // The profile is stored so the acknowledgement POST (which does not carry the
            // token) can still write the profile into the access-granted session without
            // re-exposing the token to the client.
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
