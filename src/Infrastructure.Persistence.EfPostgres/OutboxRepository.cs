using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantAccessControlPlane.Abstractions;

namespace TenantAccessControlPlane.Infrastructure.Persistence.EfPostgres;

public sealed class EfOutboxRepository(TacDbContext db) : IOutboxRepository
{
    public Task AddAsync(OutboxMessageEntity message, CancellationToken ct = default)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = message.Id, OccurredAt = message.OccurredAt, Type = message.Type, Version = message.Version,
            CorrelationId = message.CorrelationId, CausationId = message.CausationId, TenantId = message.TenantId,
            AggregateType = message.AggregateType, AggregateId = message.AggregateId, PayloadJson = message.PayloadJson,
            HeadersJson = message.HeadersJson, Status = message.Status, Attempts = message.Attempts,
            NextAttemptAt = message.NextAttemptAt, LockedUntil = message.LockedUntil, LastError = message.LastError
        });
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<OutboxMessageEntity>> AcquirePendingAsync(int batchSize, DateTime utcNow, TimeSpan lockDuration, CancellationToken ct = default)
    {
        var rows = await db.OutboxMessages
            .Where(x => x.Status == "Pending" && x.NextAttemptAt <= utcNow && (x.LockedUntil == null || x.LockedUntil < utcNow))
            .OrderBy(x => x.OccurredAt)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var row in rows) row.LockedUntil = utcNow.Add(lockDuration);
        await db.SaveChangesAsync(ct);

        return rows.Select(x => new OutboxMessageEntity(x.Id, x.OccurredAt, x.Type, x.Version, x.CorrelationId, x.CausationId, x.TenantId, x.AggregateType, x.AggregateId, x.PayloadJson, x.HeadersJson, x.Status, x.Attempts, x.NextAttemptAt, x.LockedUntil, x.LastError)).ToList();
    }

    public async Task MarkDispatchedAsync(Guid id, DateTime utcNow, CancellationToken ct = default)
    {
        var msg = await db.OutboxMessages.FindAsync([id], ct);
        if (msg is null) return;
        msg.Status = "Dispatched";
        msg.LockedUntil = null;
        msg.NextAttemptAt = utcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, int attempts, DateTime nextAttemptAt, string? error, bool deadLetter, CancellationToken ct = default)
    {
        var msg = await db.OutboxMessages.FindAsync([id], ct);
        if (msg is null) return;
        msg.Attempts = attempts;
        msg.LastError = error;
        msg.NextAttemptAt = nextAttemptAt;
        msg.LockedUntil = null;
        msg.Status = deadLetter ? "DeadLetter" : "Pending";
        await db.SaveChangesAsync(ct);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddEfPostgres(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TacDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TacDbContext>());
        services.AddScoped<IDefinitionStore>(sp => sp.GetRequiredService<TacDbContext>());
        services.AddScoped<IDefinitionRegistrationStore>(sp => sp.GetRequiredService<TacDbContext>());
        services.AddScoped<IOutboxRepository, EfOutboxRepository>();
        return services;
    }
}
