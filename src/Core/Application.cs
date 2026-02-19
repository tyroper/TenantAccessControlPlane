using System.Text.Json;
using TenantAccessControlPlane.Abstractions;

namespace TenantAccessControlPlane.Core;

public static class IntegrationEvents
{
    public static IntegrationEventEnvelope Build(string eventType, object payload, Guid? tenantId, Guid? orgId, string correlationId, string? actor = null, string? causationId = null)
        => new(Guid.NewGuid(), DateTime.UtcNow, "1.0", correlationId, causationId, actor, tenantId, orgId, eventType, payload);

    public static OutboxMessageEntity ToOutbox(this IntegrationEventEnvelope evt, string aggregateType, Guid aggregateId)
        => new(Guid.NewGuid(), evt.TimestampUtc, evt.EventType, evt.Version, evt.CorrelationId, evt.CausationId, evt.TenantId, aggregateType, aggregateId,
            JsonSerializer.Serialize(evt.Payload), "{}", "Pending", 0, evt.TimestampUtc, null, null);
}

public record RegisterPermissionDefinitionCommand(string Key, string Name, string? Description, Effect DefaultEffect, string SourceService, int Version, bool IsActive, string PayloadJson);
public record RegisterPolicyDefinitionCommand(string Key, string Name, string? Description, ValueType ValueType, string? SchemaJson, string DefaultValueJson, string SourceService, int Version, bool IsActive, string PayloadJson);
