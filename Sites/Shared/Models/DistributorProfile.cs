namespace WiseLabels.Shared.Models;

/// <summary>
/// Branding and contact profile for a single distributor white-label portal instance.
/// One profile exists per distributor; each profile is identified by one or more tokens
/// embedded in the private URL the distributor hands out to their end users.
/// </summary>
public class DistributorProfile
{
    /// <summary>
    /// Access tokens for this distributor.  Each token is a unique, random string that the
    /// distributor includes in the URL they share with their customers
    /// (e.g. https://order.wiselabels.com/gate/&lt;token&gt;).
    /// Multiple tokens allow issuing per-campaign or per-batch links that can be
    /// revoked independently.
    /// </summary>
    public string[] Tokens { get; init; } = [];

    /// <summary>Distributor's company name, shown in the page header and footer.</summary>
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>
    /// URL of the distributor's logo image.
    /// Can be an absolute https URL (e.g. hosted in Azure Blob Storage) or a
    /// root-relative path served from this portal's wwwroot (e.g. /img/dist-logos/abc.png).
    /// </summary>
    public string LogoUrl { get; init; } = string.Empty;

    /// <summary>Alt text for the distributor logo image.</summary>
    public string LogoAlt { get; init; } = string.Empty;

    /// <summary>Primary contact name shown on the portal (e.g. sales rep name).</summary>
    public string ContactName { get; init; } = string.Empty;

    /// <summary>Contact phone number shown on the portal.</summary>
    public string ContactPhone { get; init; } = string.Empty;

    /// <summary>Contact email address shown on the portal.</summary>
    public string ContactEmail { get; init; } = string.Empty;

    /// <summary>
    /// Optional CSS hex color (e.g. "#003399") used as the portal's primary brand color.
    /// When provided, it overrides the default WiseLink brand color in the white-label layout.
    /// </summary>
    public string? PrimaryColor { get; init; }

    /// <summary>
    /// Optional prefix prepended to quote reference numbers for quotes submitted through
    /// this distributor's portal (e.g. "ABC-" → quote reference "ABC-20250001").
    /// Falls back to the global Sites:WhiteLabel:DefaultReferencePrefix if not set.
    /// </summary>
    public string? ReferencePrefix { get; init; }

    /// <summary>
    /// Internal slug used as a short identifier in session keys and logs
    /// (e.g. "abc-printing", "xyz-supply").  Must be unique across all distributor profiles.
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// DNS subdomain label for this distributor on the shared apex domain
    /// (e.g. "abc-printing" → <c>abc-printing.labels-tags.com</c>).
    /// Must be a valid DNS label: lowercase letters, digits, and hyphens only; no dots.
    /// Leave empty if you do not want a dedicated subdomain for this distributor and
    /// prefer token-in-URL access instead.
    /// </summary>
    public string Subdomain { get; init; } = string.Empty;
}
