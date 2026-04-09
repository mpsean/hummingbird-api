namespace Hummingbird.API.Services;

public sealed record KubernetesProvisioningResult(bool Success, string? ErrorMessage = null);
