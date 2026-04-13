namespace WiseLabels.Shared.Middleware;

/// <summary>
/// Middleware that enforces portal gate access.
/// Any request to a page other than /Gate or /NotAuthorized requires the session
/// key set by the Gate page. Unauthorized requests are redirected to /Gate.
/// </summary>
public class PortalGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _sessionKey;

    // Paths that are always accessible without a session (the gate itself and static assets)
    private static readonly HashSet<string> _publicPaths =
    [
        "/gate",
        "/notauthorized",
        "/privacy",
    ];

    public PortalGateMiddleware(RequestDelegate next, string sessionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey, nameof(sessionKey));
        _next = next;
        _sessionKey = sessionKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";

        // Always allow static files, favicon, and gate paths
        if (path.StartsWith("/_framework") ||
            path.StartsWith("/css") ||
            path.StartsWith("/js") ||
            path.StartsWith("/img") ||
            path.StartsWith("/lib") ||
            path == "/favicon.ico" ||
            _publicPaths.Contains(path) ||
            _publicPaths.Any(p => path.StartsWith(p + "/")))
        {
            await _next(context);
            return;
        }

        var access = context.Session.GetString(_sessionKey);
        if (access != "granted")
        {
            context.Response.Redirect("/Gate");
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register PortalGateMiddleware with a specific session key.
/// </summary>
public static class PortalGateMiddlewareExtensions
{
    public static IApplicationBuilder UsePortalGate(this IApplicationBuilder app, string sessionKey)
    {
        return app.UseMiddleware<PortalGateMiddleware>(sessionKey);
    }
}
