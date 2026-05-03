using WiseLabels.Shared.Models;
using WiseLabels.Shared.Services;

namespace WiseLabels.Shared.Middleware;

/// <summary>
/// Middleware that runs early in the pipeline to resolve a <see cref="DistributorProfile"/>
/// from the incoming request's host header (subdomain routing).
///
/// When a customer opens <c>abc-printing.labels-tags.com</c>, this middleware:
/// 1. Extracts the subdomain label (<c>abc-printing</c>) by stripping the configured
///    apex domain (<c>labels-tags.com</c>) from the <c>Host</c> header.
/// 2. Looks up the matching <see cref="DistributorProfile"/> via
///    <see cref="IDistributorProfileService.FindBySubdomain"/>.
/// 3. Stores the profile in <see cref="HttpContext.Items"/> under the key
///    <see cref="DistributorProfileItemKey"/> so downstream pages and middleware can
///    use it without re-querying the service.
///
/// If the apex domain does not match (e.g. the request arrived on the base App Service
/// hostname like <c>wiselabels-whitelabel.azurewebsites.net</c>) this middleware is a
/// no-op and falls through; token-in-URL gate flow remains available as a fallback.
/// </summary>
public class SubdomainDistributorMiddleware
{
    /// <summary>
    /// Key used to store the resolved <see cref="DistributorProfile"/> in
    /// <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string DistributorProfileItemKey = "DistributorProfile";

    private readonly RequestDelegate _next;
    private readonly string _apexDomain; // e.g. "labels-tags.com"

    public SubdomainDistributorMiddleware(RequestDelegate next, string apexDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apexDomain, nameof(apexDomain));
        _next = next;
        _apexDomain = apexDomain.TrimStart('.').ToLowerInvariant();
    }

    public async Task InvokeAsync(HttpContext context, IDistributorProfileService profileService)
    {
        var host = context.Request.Host.Host.ToLowerInvariant(); // e.g. "abc-printing.labels-tags.com"
        var suffix = "." + _apexDomain;

        if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            var subdomain = host[..^suffix.Length]; // "abc-printing"

            if (!string.IsNullOrEmpty(subdomain))
            {
                var profile = profileService.FindBySubdomain(subdomain);
                if (profile is not null)
                {
                    context.Items[DistributorProfileItemKey] = profile;
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register <see cref="SubdomainDistributorMiddleware"/> with the
/// configured apex domain.
/// </summary>
public static class SubdomainDistributorMiddlewareExtensions
{
    public static IApplicationBuilder UseSubdomainDistributor(
        this IApplicationBuilder app, string apexDomain)
    {
        return app.UseMiddleware<SubdomainDistributorMiddleware>(apexDomain);
    }
}
