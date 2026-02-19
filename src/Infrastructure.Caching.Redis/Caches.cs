using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using TenantAccessControlPlane.Abstractions;

namespace TenantAccessControlPlane.Infrastructure.Caching.Redis;

public sealed class InMemoryEffectiveAccessCache(IMemoryCache cache) : IEffectiveAccessCache
{
    public Task<IReadOnlyCollection<string>?> GetPermissionsAsync(Guid tenantId, Guid userId, string scopeType, Guid scopeId, CancellationToken ct = default)
        => Task.FromResult(cache.Get<IReadOnlyCollection<string>>(Key(tenantId, userId, scopeType, scopeId)));

    public Task SetPermissionsAsync(Guid tenantId, Guid userId, string scopeType, Guid scopeId, IReadOnlyCollection<string> permissions, TimeSpan ttl, CancellationToken ct = default)
    {
        cache.Set(Key(tenantId, userId, scopeType, scopeId), permissions, ttl);
        return Task.CompletedTask;
    }

    public Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default) { return Task.CompletedTask; }
    private static string Key(Guid t, Guid u, string st, Guid s) => $"perm:{t}:{u}:{st}:{s}";
}

public sealed class RedisEffectiveAccessCache(IDistributedCache cache) : IEffectiveAccessCache
{
    public async Task<IReadOnlyCollection<string>?> GetPermissionsAsync(Guid tenantId, Guid userId, string scopeType, Guid scopeId, CancellationToken ct = default)
    {
        var raw = await cache.GetStringAsync(Key(tenantId, userId, scopeType, scopeId), ct);
        return raw is null ? null : JsonSerializer.Deserialize<string[]>(raw);
    }

    public async Task SetPermissionsAsync(Guid tenantId, Guid userId, string scopeType, Guid scopeId, IReadOnlyCollection<string> permissions, TimeSpan ttl, CancellationToken ct = default)
        => await cache.SetStringAsync(Key(tenantId, userId, scopeType, scopeId), JsonSerializer.Serialize(permissions), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);

    public Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
    private static string Key(Guid t, Guid u, string st, Guid s) => $"perm:{t}:{u}:{st}:{s}";
}
