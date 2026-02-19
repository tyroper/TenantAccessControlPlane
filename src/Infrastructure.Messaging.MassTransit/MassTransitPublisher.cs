using MassTransit;
using TenantAccessControlPlane.Abstractions;

namespace TenantAccessControlPlane.Infrastructure.Messaging.MassTransit;

public sealed class MassTransitEventPublisher(IBus bus) : IEventPublisher
{
    public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken ct = default)
        => bus.Publish(envelope, ct);
}
