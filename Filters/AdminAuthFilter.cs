using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hummingbird.API.Filters;

/// <summary>
/// Requires X-Admin-Key header matching the configured AdminKey.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminAuthAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedKey = config["AdminKey"] ?? "";

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            context.Result = new ObjectResult(new { message = "Admin key not configured on server." })
            { StatusCode = 500 };
            return;
        }

        var providedKey = context.HttpContext.Request.Headers["X-Admin-Key"].FirstOrDefault() ?? "";

        if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid or missing X-Admin-Key header." });
            return;
        }

        await next();
    }
}
