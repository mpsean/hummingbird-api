namespace Hummingbird.API.Models;

public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;           // display name
    public string Subdomain { get; set; } = string.Empty;      // e.g. "tenant1"
    public string DatabaseName { get; set; } = string.Empty;   // e.g. "hummingbird_tenant1"
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
