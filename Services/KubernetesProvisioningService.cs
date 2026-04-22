using k8s;
using k8s.Models;
using k8s.Autorest;

namespace Hummingbird.API.Services;

public sealed class KubernetesProvisioningService : IKubernetesProvisioningService
{
    private readonly IKubernetes _client;
    private readonly ILogger<KubernetesProvisioningService> _logger;
    private readonly string _frontendImage;
    private readonly string _apiServiceUrl;
    private readonly string _ingressDomain;

    public KubernetesProvisioningService(
        IKubernetes client,
        IConfiguration config,
        ILogger<KubernetesProvisioningService> logger)
    {
        _client = client;
        _logger = logger;
        _frontendImage = config["Kubernetes:FrontendImage"] ?? "hummingbird-frontend:latest";
        _apiServiceUrl = config["Kubernetes:ApiServiceUrl"]
            ?? "http://hummingbird-api.hummingbird-api.svc.cluster.local:5000";
        _ingressDomain = config["Kubernetes:IngressDomain"] ?? "hmmbird.xyz";
    }

    public async Task<KubernetesProvisioningResult> ProvisionTenantAsync(string subdomain)
    {
        var ns = $"tenant-{subdomain}";
        try
        {
            await CreateNamespaceAsync(ns);
            await CreateDeploymentAsync(ns, subdomain);
            await CreateServiceAsync(ns, subdomain);
            await CreateIngressRouteAsync(ns, subdomain);

            _logger.LogInformation(
                "K8s provisioning complete for tenant {Subdomain} in namespace {Namespace}",
                subdomain, ns);

            return new KubernetesProvisioningResult(Success: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "K8s provisioning failed for tenant {Subdomain}", subdomain);
            return new KubernetesProvisioningResult(Success: false, ErrorMessage: ex.Message);
        }
    }

    public async Task<KubernetesProvisioningResult> DeprovisionTenantAsync(string subdomain)
    {
        var ns = $"tenant-{subdomain}";
        try
        {
            await _client.CoreV1.DeleteNamespaceAsync(
                ns,
                new V1DeleteOptions { PropagationPolicy = "Background" });

            _logger.LogInformation(
                "K8s deprovisioning complete for tenant {Subdomain} — namespace {Namespace} deleted",
                subdomain, ns);

            return new KubernetesProvisioningResult(Success: true);
        }
        catch (HttpOperationException ex) when ((int)ex.Response.StatusCode == 404)
        {
            _logger.LogDebug("Namespace {Namespace} not found during deprovisioning, skipping", ns);
            return new KubernetesProvisioningResult(Success: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "K8s deprovisioning failed for tenant {Subdomain}", subdomain);
            return new KubernetesProvisioningResult(Success: false, ErrorMessage: ex.Message);
        }
    }

    private async Task CreateNamespaceAsync(string ns)
    {
        try
        {
            await _client.CoreV1.CreateNamespaceAsync(new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = ns,
                    Labels = new Dictionary<string, string>
                    {
                        ["hummingbird.io/tenant"] = "true"
                    }
                }
            });
            _logger.LogDebug("Created namespace {Namespace}", ns);
        }
        catch (HttpOperationException ex) when ((int)ex.Response.StatusCode == 409)
        {
            _logger.LogDebug("Namespace {Namespace} already exists, skipping", ns);
        }
    }

    private async Task CreateDeploymentAsync(string ns, string subdomain)
    {
        var labels = new Dictionary<string, string>
        {
            ["app"] = "hummingbird-frontend",
            ["tenant"] = subdomain
        };

        try
        {
            await _client.AppsV1.CreateNamespacedDeploymentAsync(new V1Deployment
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "hummingbird-frontend",
                    NamespaceProperty = ns,
                    Labels = labels
                },
                Spec = new V1DeploymentSpec
                {
                    Replicas = 1,
                    Selector = new V1LabelSelector { MatchLabels = labels },
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta { Labels = labels },
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>
                            {
                                new V1Container
                                {
                                    Name  = "frontend",
                                    Image = _frontendImage,
                                    Ports = new List<V1ContainerPort>
                                    {
                                        new V1ContainerPort { ContainerPort = 80 }
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new V1EnvVar { Name = "API_URL", Value = _apiServiceUrl }
                                    }
                                }
                            }
                        }
                    }
                }
            }, ns);
            _logger.LogDebug("Created Deployment in namespace {Namespace}", ns);
        }
        catch (HttpOperationException ex) when ((int)ex.Response.StatusCode == 409)
        {
            _logger.LogDebug("Deployment in {Namespace} already exists, skipping", ns);
        }
    }

    private async Task CreateServiceAsync(string ns, string subdomain)
    {
        var selector = new Dictionary<string, string>
        {
            ["app"]    = "hummingbird-frontend",
            ["tenant"] = subdomain
        };

        try
        {
            await _client.CoreV1.CreateNamespacedServiceAsync(new V1Service
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "hummingbird-frontend",
                    NamespaceProperty = ns
                },
                Spec = new V1ServiceSpec
                {
                    Selector = selector,
                    Ports = new List<V1ServicePort>
                    {
                        new V1ServicePort { Port = 80, TargetPort = 80 }
                    }
                }
            }, ns);
            _logger.LogDebug("Created Service in namespace {Namespace}", ns);
        }
        catch (HttpOperationException ex) when ((int)ex.Response.StatusCode == 409)
        {
            _logger.LogDebug("Service in {Namespace} already exists, skipping", ns);
        }
    }

    private async Task CreateIngressRouteAsync(string ns, string subdomain)
    {
        var host = $"{subdomain}.{_ingressDomain}";

        var body = new Dictionary<string, object>
        {
            ["apiVersion"] = "traefik.io/v1alpha1",
            ["kind"]       = "IngressRoute",
            ["metadata"]   = new Dictionary<string, object>
            {
                ["name"]      = "hummingbird-frontend",
                ["namespace"] = ns
            },
            ["spec"] = new Dictionary<string, object>
            {
                ["entryPoints"] = new List<string> { "web" },
                ["routes"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["match"] = $"Host(`{host}`)",
                        ["kind"]  = "Rule",
                        ["services"] = new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                ["name"] = "hummingbird-frontend",
                                ["port"] = 80
                            }
                        }
                    }
                }
            }
        };

        try
        {
            await _client.CustomObjects.CreateNamespacedCustomObjectAsync(
                body: body,
                group:              "traefik.io",
                version:            "v1alpha1",
                namespaceParameter: ns,
                plural:             "ingressroutes");
            _logger.LogDebug("Created IngressRoute for {Host} in {Namespace}", host, ns);
        }
        catch (HttpOperationException ex) when ((int)ex.Response.StatusCode == 409)
        {
            _logger.LogDebug("IngressRoute for {Host} already exists, skipping", host);
        }
    }
}
