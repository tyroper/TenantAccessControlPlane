using TenantAccessControlPlane.Abstractions;

namespace TenantAccessControlPlane.Core;

public sealed class DefinitionRegistrationHandler(
    IDefinitionStore definitions,
    IDefinitionRegistrationStore registrations,
    IOutboxRepository outbox,
    IUnitOfWork uow,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<bool> HandleAsync(RegisterPolicyDefinitionCommand cmd, CancellationToken ct = default)
    {
        var hash = DefinitionService.ComputePayloadHash(cmd.PayloadJson);
        if (await registrations.ExistsAsync("Policy", cmd.Key, cmd.Version, cmd.SourceService, hash, ct)) return false;
        var existing = await definitions.FindPolicyAsync(cmd.Key, ct);
        if (existing is not null && !DefinitionService.CanUpdateOwner(existing.SourceService, cmd.SourceService)) throw new InvalidOperationException("Ownership violation");
        await definitions.UpsertPolicyAsync(new PolicyDefinition
        {
            Id = existing?.Id ?? ids.NewGuid(),
            Key = cmd.Key,
            Name = cmd.Name,
            Description = cmd.Description,
            ValueType = cmd.ValueType,
            SchemaJson = cmd.SchemaJson,
            DefaultValueJson = cmd.DefaultValueJson,
            SourceService = cmd.SourceService,
            Version = cmd.Version,
            IsActive = cmd.IsActive,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        }, ct);
        await registrations.AddAsync(new DefinitionRegistration { Id = ids.NewGuid(), DefinitionType = "Policy", Key = cmd.Key, Version = cmd.Version, SourceService = cmd.SourceService, PayloadHash = hash, RegisteredAt = clock.UtcNow }, ct);
        await outbox.AddAsync(IntegrationEvents.Build("PolicyDefinitionRegistered", cmd, null, null, Guid.NewGuid().ToString()).ToOutbox("PolicyDefinition", existing?.Id ?? Guid.Empty), ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }
}
