namespace TenantAccessControlPlane.Abstractions;

public interface IClock { DateTime UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }

public interface IIdGenerator { Guid NewGuid(); }
public sealed class GuidIdGenerator : IIdGenerator { public Guid NewGuid() => Guid.NewGuid(); }

public interface IUnitOfWork { Task<int> SaveChangesAsync(CancellationToken ct = default); }

public interface IEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken ct = default);
}

public interface IEffectiveAccessCache
{
    Task<IReadOnlyCollection<string>?> GetPermissionsAsync(Guid tenantId, Guid userId, string scopeType, Guid scopeId, CancellationToken ct = default);
    Task SetPermissionsAsync(Guid tenantId, Guid userId, string scopeType, Guid scopeId, IReadOnlyCollection<string> permissions, TimeSpan ttl, CancellationToken ct = default);
    Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessageEntity message, CancellationToken ct = default);
    Task<IReadOnlyCollection<OutboxMessageEntity>> AcquirePendingAsync(int batchSize, DateTime utcNow, TimeSpan lockDuration, CancellationToken ct = default);
    Task MarkDispatchedAsync(Guid id, DateTime utcNow, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, int attempts, DateTime nextAttemptAt, string? error, bool deadLetter, CancellationToken ct = default);
}

public record IntegrationEventEnvelope(
    Guid EventId,
    DateTime TimestampUtc,
    string Version,
    string CorrelationId,
    string? CausationId,
    string? ActorSubject,
    Guid? TenantId,
    Guid? OrganizationId,
    string EventType,
    object Payload);

public sealed record OutboxMessageEntity(
    Guid Id,
    DateTime OccurredAt,
    string Type,
    string Version,
    string CorrelationId,
    string? CausationId,
    Guid? TenantId,
    string AggregateType,
    Guid AggregateId,
    string PayloadJson,
    string HeadersJson,
    string Status,
    int Attempts,
    DateTime NextAttemptAt,
    DateTime? LockedUntil,
    string? LastError);
