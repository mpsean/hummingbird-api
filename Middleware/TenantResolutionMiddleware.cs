using Hummingbird.API.Data;
using Hummingbird.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Hummingbird.API.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, MasterDbContext masterDb, TenantContext tenantContext)
    {
        // Admin routes bypass tenant resolution
        if (context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await _next(context);
            return;
        }

        // Prefer X-Forwarded-Host (set by reverse proxy / Vite dev proxy)
        var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var host = !string.IsNullOrWhiteSpace(forwardedHost)
            ? forwardedHost
            : context.Request.Host.Host;

        var subdomain = ExtractSubdomain(host);

        if (string.IsNullOrWhiteSpace(subdomain))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Cannot determine tenant from request host." });
            return;
        }

        var tenant = await masterDb.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain && t.IsActive);

        if (tenant == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { message = $"Tenant '{subdomain}' not found or inactive." });
            return;
        }

        tenantContext.TenantId = tenant.Id;
        tenantContext.TenantName = tenant.Name;
        tenantContext.Subdomain = tenant.Subdomain;
        tenantContext.ConnectionString = tenant.ConnectionString;
        tenantContext.IsResolved = true;

        await _next(context);
    }

    /// <summary>
    /// Extracts the leftmost subdomain label.
    /// "tenant1.hmmbird.xyz" → "tenant1"
    /// "localhost" → "" (no subdomain)
    /// </summary>
    private static string ExtractSubdomain(string host)
    {
        // Strip port if present
        var hostOnly = host.Split(':')[0];
        var parts = hostOnly.Split('.');

        // Need at least 3 parts: sub.domain.tld
        if (parts.Length < 3) return string.Empty;

        return parts[0].ToLowerInvariant();
    }
}
