namespace Hummingbird.API.Services;

public interface ITenantContext
{
    int TenantId { get; }
    string TenantName { get; }
    string Subdomain { get; }
    string ConnectionString { get; }
    bool IsResolved { get; }
}
