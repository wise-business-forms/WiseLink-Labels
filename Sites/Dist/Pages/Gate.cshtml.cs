using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WiseLabels.Dist.Pages;

/// <summary>
/// Landing gate page for the Print Distributor portal.
/// Validates the token embedded in the URL against the configured list of valid tokens.
/// On success with acknowledgement, writes a short-lived session cookie granting access.
/// </summary>
public class GateModel : PageModel
{
    private const string SessionKey = "dist_access";
    private readonly IConfiguration _config;

    public bool TokenValid { get; private set; }

    public GateModel(IConfiguration config)
    {
        _config = config;
    }

    public IActionResult OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TokenValid = false;
            return Page();
        }

        var validTokens = _config.GetSection("Sites:Dist:ValidTokens").Get<string[]>() ?? [];
        TokenValid = validTokens.Contains(token, StringComparer.Ordinal);
        if (TokenValid)
        {
            // Store a server-side flag in session (not a cookie) so the POST handler
            // can confirm the validation without re-exposing the token to the client.
            HttpContext.Session.SetString("dist_token_validated", "yes");
        }

        return Page();
    }

    public IActionResult OnPostAcknowledge(bool confirmed)
    {
        if (!confirmed)
            return RedirectToPage("/Gate");

        var validated = HttpContext.Session.GetString("dist_token_validated");
        if (validated != "yes")
            return RedirectToPage("/Gate");

        // Upgrade the session from "token validated" to "full access granted"
        HttpContext.Session.Remove("dist_token_validated");
        HttpContext.Session.SetString(SessionKey, "granted");
        return RedirectToPage("/Index");
    }
}
