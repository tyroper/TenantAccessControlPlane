using TenantAccessControlPlane.Abstractions;

namespace TenantAccessControlPlane.Core;

public enum EntityStatus { Active, Suspended, Disabled }
public enum MembershipStatus { Invited, Active, Suspended, Removed }
public enum ScopeType { Tenant, Organization }
public enum Effect { Allow, Deny }
public enum ValueType { Boolean, Number, String, Json }

public abstract class AuditedEntity
{
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class Tenant : AuditedEntity { public Guid Id { get; init; } public string Key { get; init; } = ""; public string Name { get; set; } = ""; public EntityStatus Status { get; set; } = EntityStatus.Active; }
public sealed class Organization : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public string Key { get; init; } = ""; public string Name { get; set; } = ""; public Guid? ParentOrganizationId { get; set; } public string? MetadataJson { get; set; } }
public sealed class User : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public string ExternalSubject { get; init; } = ""; public string? Email { get; set; } public string? DisplayName { get; set; } public EntityStatus Status { get; set; } = EntityStatus.Active; }
public sealed class OrganizationMembership : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public Guid OrganizationId { get; init; } public Guid UserId { get; init; } public MembershipStatus Status { get; set; } = MembershipStatus.Invited; public DateTime InvitedAt { get; init; } public Guid? InvitedByUserId { get; init; } public DateTime? JoinedAt { get; set; } public DateTime? RemovedAt { get; set; } }
public sealed class Role : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public string Key { get; init; } = ""; public string Name { get; set; } = ""; public string? Description { get; set; } public bool IsSystem { get; init; } }
public sealed class ScopedRoleAssignment : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public Guid UserId { get; init; } public Guid RoleId { get; init; } public ScopeType ScopeType { get; init; } public Guid ScopeId { get; init; } }
public sealed class PermissionDefinition : AuditedEntity { public Guid Id { get; init; } public string Key { get; init; } = ""; public string Name { get; set; } = ""; public string? Description { get; set; } public Effect DefaultEffect { get; set; } public string SourceService { get; set; } = ""; public int Version { get; set; } public bool IsActive { get; set; } = true; }
public sealed class RolePermission : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public Guid RoleId { get; init; } public string PermissionKey { get; init; } = ""; public Effect Effect { get; set; } = Effect.Allow; }
public sealed class PolicyDefinition : AuditedEntity { public Guid Id { get; init; } public string Key { get; init; } = ""; public string Name { get; set; } = ""; public string? Description { get; set; } public ValueType ValueType { get; set; } public string? SchemaJson { get; set; } public string DefaultValueJson { get; set; } = "{}"; public string SourceService { get; set; } = ""; public int Version { get; set; } public bool IsActive { get; set; } = true; }
public sealed class OrganizationPolicyOverride : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public Guid OrganizationId { get; init; } public string PolicyKey { get; init; } = ""; public string ValueJson { get; set; } = "{}"; }
public sealed class DefinitionRegistration { public Guid Id { get; init; } public string DefinitionType { get; init; } = ""; public string Key { get; init; } = ""; public int Version { get; init; } public string SourceService { get; init; } = ""; public string PayloadHash { get; init; } = ""; public DateTime RegisteredAt { get; init; } }
public sealed class Invitation : AuditedEntity { public Guid Id { get; init; } public Guid TenantId { get; init; } public Guid? OrganizationId { get; init; } public string Email { get; init; } = ""; public string TokenHash { get; init; } = ""; public DateTime ExpiresAt { get; init; } public DateTime InvitedAt { get; init; } public Guid? InvitedByUserId { get; init; } public DateTime? AcceptedAt { get; set; } public Guid? AcceptedByUserId { get; set; } }

public sealed class DefinitionService
{
    public static bool CanUpdateOwner(string existingSourceService, string incomingSourceService) =>
        string.Equals(existingSourceService, incomingSourceService, StringComparison.OrdinalIgnoreCase);

    public static string ComputePayloadHash(string payload) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
}
