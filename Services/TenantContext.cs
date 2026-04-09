namespace Hummingbird.API.Services;

/// <summary>
/// Scoped per-request tenant context. Populated by TenantResolutionMiddleware.
/// </summary>
public class TenantContext : ITenantContext
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
}
