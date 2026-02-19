using Microsoft.EntityFrameworkCore;
using TenantAccessControlPlane.Abstractions;
using TenantAccessControlPlane.Core;

namespace TenantAccessControlPlane.Infrastructure.Persistence.EfPostgres;

public sealed class TacDbContext(DbContextOptions<TacDbContext> options) : DbContext(options), IUnitOfWork, IDefinitionStore, IDefinitionRegistrationStore
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ScopedRoleAssignment> ScopedRoleAssignments => Set<ScopedRoleAssignment>();
    public DbSet<PolicyDefinition> PolicyDefinitions => Set<PolicyDefinition>();
    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();
    public DbSet<OrganizationPolicyOverride> OrganizationPolicyOverrides => Set<OrganizationPolicyOverride>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<DefinitionRegistration> DefinitionRegistrations => Set<DefinitionRegistration>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>().HasIndex(x => x.Key).IsUnique();
        b.Entity<Organization>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        b.Entity<Role>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        b.Entity<User>().HasIndex(x => new { x.TenantId, x.ExternalSubject }).IsUnique();
        b.Entity<PermissionDefinition>().HasIndex(x => x.Key).IsUnique();
        b.Entity<PolicyDefinition>().HasIndex(x => x.Key).IsUnique();
        b.Entity<OrganizationPolicyOverride>().HasIndex(x => new { x.TenantId, x.OrganizationId, x.PolicyKey }).IsUnique();
        b.Entity<DefinitionRegistration>().HasIndex(x => new { x.DefinitionType, x.Key, x.Version, x.SourceService }).IsUnique();
        b.Entity<OutboxMessage>().HasIndex(x => new { x.Status, x.NextAttemptAt });

        foreach (var et in b.Model.GetEntityTypes())
        {
            if (typeof(AuditedEntity).IsAssignableFrom(et.ClrType))
            {
                b.Entity(et.ClrType).Property<byte[]>("RowVersion").IsRowVersion();
            }
        }
    }

    public async Task<PermissionDefinition?> FindPermissionAsync(string key, CancellationToken ct = default) =>
        await PermissionDefinitions.FirstOrDefaultAsync(x => x.Key == key, ct);

    public async Task<PolicyDefinition?> FindPolicyAsync(string key, CancellationToken ct = default) =>
        await PolicyDefinitions.FirstOrDefaultAsync(x => x.Key == key, ct);

    public async Task UpsertPermissionAsync(PermissionDefinition def, CancellationToken ct = default)
    {
        var existing = await FindPermissionAsync(def.Key, ct);
        if (existing is null) await PermissionDefinitions.AddAsync(def, ct);
        else Entry(existing).CurrentValues.SetValues(def);
    }

    public async Task UpsertPolicyAsync(PolicyDefinition def, CancellationToken ct = default)
    {
        var existing = await FindPolicyAsync(def.Key, ct);
        if (existing is null) await PolicyDefinitions.AddAsync(def, ct);
        else Entry(existing).CurrentValues.SetValues(def);
    }

    public async Task<bool> ExistsAsync(string definitionType, string key, int version, string sourceService, string payloadHash, CancellationToken ct = default)
        => await DefinitionRegistrations.AnyAsync(x => x.DefinitionType == definitionType && x.Key == key && x.Version == version && x.SourceService == sourceService && x.PayloadHash == payloadHash, ct);

    public async Task AddAsync(DefinitionRegistration registration, CancellationToken ct = default) => await DefinitionRegistrations.AddAsync(registration, ct);
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Type { get; set; } = "";
    public string Version { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string? CausationId { get; set; }
    public Guid? TenantId { get; set; }
    public string AggregateType { get; set; } = "";
    public Guid AggregateId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string HeadersJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LastError { get; set; }
}
