using TenantAccessControlPlane.Core;

namespace TenantAccessControlPlane.Abstractions;

public interface IDefinitionRegistrationStore
{
    Task<bool> ExistsAsync(string definitionType, string key, int version, string sourceService, string payloadHash, CancellationToken ct = default);
    Task AddAsync(DefinitionRegistration registration, CancellationToken ct = default);
}

public interface IDefinitionStore
{
    Task<PermissionDefinition?> FindPermissionAsync(string key, CancellationToken ct = default);
    Task<PolicyDefinition?> FindPolicyAsync(string key, CancellationToken ct = default);
    Task UpsertPermissionAsync(PermissionDefinition def, CancellationToken ct = default);
    Task UpsertPolicyAsync(PolicyDefinition def, CancellationToken ct = default);
}
