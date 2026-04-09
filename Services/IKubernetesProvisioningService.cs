namespace Hummingbird.API.Services;

public interface IKubernetesProvisioningService
{
    /// <summary>
    /// Creates K8s namespace, frontend Deployment, Service, and Traefik IngressRoute
    /// for the given tenant subdomain. Never throws — returns a result object.
    /// </summary>
    Task<KubernetesProvisioningResult> ProvisionTenantAsync(string subdomain);
}
