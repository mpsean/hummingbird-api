using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Hummingbird.API.Services;
using Microsoft.IdentityModel.Tokens;

namespace Hummingbird.API.Middleware;

public class JwtTenantAuthMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Admin routes use their own X-Admin-Key auth — skip JWT check
        if (context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await next(context);
            return;
        }

        var auth = context.Request.Headers.Authorization.FirstOrDefault();
        if (auth is null || !auth.StartsWith("Bearer "))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { message = "Missing or invalid Authorization header." });
            return;
        }

        var token = auth["Bearer ".Length..].Trim();
        var claims = ValidateToken(token);

        if (claims is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid or expired token." });
            return;
        }

        var tokenTenant = claims.FindFirst("tenant_slug")?.Value;
        if (tokenTenant != tenantContext.Subdomain)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { message = "Token does not belong to this tenant." });
            return;
        }

        await next(context);
    }

    private System.Security.Claims.ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var secret = config["Jwt:SecretKey"]
                ?? throw new InvalidOperationException("Jwt:SecretKey not configured");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey        = key,
                ValidateIssuer          = true,
                ValidIssuer             = config["Jwt:Issuer"],
                ValidateAudience        = true,
                ValidAudience           = config["Jwt:Audience"],
                ValidateLifetime        = true,
                ClockSkew               = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}
