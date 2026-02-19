using Microsoft.Extensions.Logging;
using TenantAccessControlPlane.Abstractions;

namespace TenantAccessControlPlane.Infrastructure.Messaging.InMemory;

public sealed class InMemoryEventPublisher(ILogger<InMemoryEventPublisher> logger) : IEventPublisher
{
    public List<IntegrationEventEnvelope> Published { get; } = [];
    public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken ct = default)
    {
        Published.Add(envelope);
        logger.LogInformation("Published {EventType} {EventId}", envelope.EventType, envelope.EventId);
        return Task.CompletedTask;
    }
}
