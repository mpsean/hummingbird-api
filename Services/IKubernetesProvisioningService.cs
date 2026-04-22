namespace Hummingbird.API.Services;

public interface IKubernetesProvisioningService
{
    /// <summary>
    /// Creates K8s namespace, frontend Deployment, Service, and Traefik IngressRoute
    /// for the given tenant subdomain. Never throws — returns a result object.
    /// </summary>
    Task<KubernetesProvisioningResult> ProvisionTenantAsync(string subdomain);

    /// <summary>
    /// Deletes the K8s namespace for the given tenant subdomain (cascades to all
    /// resources within it). Never throws — returns a result object.
    /// </summary>
    Task<KubernetesProvisioningResult> DeprovisionTenantAsync(string subdomain);
}
