namespace Hummingbird.API.Services;

public sealed class NoOpKubernetesProvisioningService : IKubernetesProvisioningService
{
    public Task<KubernetesProvisioningResult> ProvisionTenantAsync(string subdomain)
        => Task.FromResult(new KubernetesProvisioningResult(Success: true));
}
