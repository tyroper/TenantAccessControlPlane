using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenantAccessControlPlane.Abstractions;
using TenantAccessControlPlane.Core;
using TenantAccessControlPlane.Infrastructure.Caching.Redis;
using TenantAccessControlPlane.Infrastructure.Messaging.InMemory;
using TenantAccessControlPlane.Infrastructure.Persistence.EfPostgres;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IIdGenerator, GuidIdGenerator>();
builder.Services.AddSingleton<IEffectiveAccessCache, InMemoryEffectiveAccessCache>();

var conn = builder.Configuration.GetConnectionString("postgres") ?? "Host=localhost;Database=tacp;Username=postgres;Password=postgres";
builder.Services.AddEfPostgres(conn);
builder.Services.AddScoped<IEventPublisher, InMemoryEventPublisher>();
builder.Services.AddHostedService<OutboxDispatcherWorker>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization(o => o.AddPolicy("PlatformAdmin", p => p.RequireClaim("permissions", "platform:admin")));

var app = builder.Build();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var tenants = app.MapGroup("/api/tenants");
tenants.MapPost("/", async ([FromBody] Tenant dto, TacDbContext db, IOutboxRepository outbox) =>
{
    db.Tenants.Add(dto);
    var evt = IntegrationEvents.Build("TenantCreated", dto, dto.Id, null, Guid.NewGuid().ToString());
    await outbox.AddAsync(evt.ToOutbox("Tenant", dto.Id));
    await db.SaveChangesAsync();
    return Results.Created($"/api/tenants/{dto.Id}", dto);
}).RequireAuthorization("PlatformAdmin");
tenants.MapGet("/", async (TacDbContext db) => await db.Tenants.Where(x => x.DeletedAt == null).ToListAsync());

authorizeTenantRoute(app.MapGroup("/api/{tenantId:guid}"));

RouteGroupBuilder authorizeTenantRoute(RouteGroupBuilder g)
{
    g.AddEndpointFilter(async (ctx, next) =>
    {
        if (!ctx.HttpContext.Request.RouteValues.TryGetValue("tenantId", out var tv) || tv is null) return Results.BadRequest("tenantId missing");
        if (ctx.HttpContext.User.HasClaim("permissions", "platform:admin")) return await next(ctx);
        var tenantClaim = ctx.HttpContext.User.FindFirst("tenant_id")?.Value;
        return tenantClaim == tv.ToString() ? await next(ctx) : Results.Forbid();
    });

    g.MapGet("/organizations", async (Guid tenantId, TacDbContext db) => await db.Organizations.Where(x => x.TenantId == tenantId && x.DeletedAt == null).ToListAsync());
    g.MapPost("/organizations", async (Guid tenantId, Organization org, TacDbContext db, IOutboxRepository outbox) =>
    {
        if (org.TenantId != tenantId) return Results.BadRequest("Cross-tenant reference");
        db.Organizations.Add(org);
        await outbox.AddAsync(IntegrationEvents.Build("OrganizationCreated", org, tenantId, org.Id, Guid.NewGuid().ToString()).ToOutbox("Organization", org.Id));
        await db.SaveChangesAsync();
        return Results.Created($"/api/{tenantId}/organizations/{org.Id}", org);
    });

    g.MapPut("/users/upsert", async (Guid tenantId, User user, TacDbContext db, IOutboxRepository outbox) =>
    {
        if (tenantId != user.TenantId) return Results.BadRequest();
        var existing = await db.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalSubject == user.ExternalSubject);
        if (existing is null) db.Users.Add(user);
        else { existing.DisplayName = user.DisplayName; existing.Email = user.Email; existing.Status = user.Status; existing.UpdatedAt = DateTime.UtcNow; }
        await outbox.AddAsync(IntegrationEvents.Build("UserUpserted", user, tenantId, null, Guid.NewGuid().ToString()).ToOutbox("User", user.Id));
        await db.SaveChangesAsync();
        return Results.Ok(user);
    });

    g.MapPost("/definitions/permissions/register", async (Guid tenantId, RegisterPermissionDefinitionCommand cmd, IDefinitionStore defs, IDefinitionRegistrationStore regs, IUnitOfWork uow, IIdGenerator ids, IOutboxRepository outbox) =>
    {
        var hash = DefinitionService.ComputePayloadHash(cmd.PayloadJson);
        if (await regs.ExistsAsync("Permission", cmd.Key, cmd.Version, cmd.SourceService, hash)) return Results.Ok(new { idempotent = true });
        var existing = await defs.FindPermissionAsync(cmd.Key);
        if (existing is not null && !DefinitionService.CanUpdateOwner(existing.SourceService, cmd.SourceService)) return Results.Conflict("Ownership violation");
        var entity = new PermissionDefinition { Id = existing?.Id ?? ids.NewGuid(), Key = cmd.Key, Name = cmd.Name, Description = cmd.Description, DefaultEffect = cmd.DefaultEffect, SourceService = cmd.SourceService, Version = cmd.Version, IsActive = cmd.IsActive, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        await defs.UpsertPermissionAsync(entity);
        await regs.AddAsync(new DefinitionRegistration { Id = ids.NewGuid(), DefinitionType = "Permission", Key = cmd.Key, Version = cmd.Version, SourceService = cmd.SourceService, PayloadHash = hash, RegisteredAt = DateTime.UtcNow });
        await outbox.AddAsync(IntegrationEvents.Build("PermissionDefinitionRegistered", entity, tenantId, null, Guid.NewGuid().ToString()).ToOutbox("PermissionDefinition", entity.Id));
        await uow.SaveChangesAsync();
        return Results.Ok(entity);
    });

    g.MapGet("/effective-permissions", async (Guid tenantId, Guid userId, string scopeType, Guid scopeId, TacDbContext db, IEffectiveAccessCache cache) =>
    {
        var cached = await cache.GetPermissionsAsync(tenantId, userId, scopeType, scopeId);
        if (cached is not null) return Results.Ok(cached);
        var roleIds = await db.ScopedRoleAssignments.Where(x => x.TenantId == tenantId && x.UserId == userId && x.ScopeType.ToString() == scopeType && x.ScopeId == scopeId && x.DeletedAt == null).Select(x => x.RoleId).ToListAsync();
        var perms = await db.RolePermissions.Where(x => x.TenantId == tenantId && roleIds.Contains(x.RoleId) && x.Effect == Effect.Allow && x.DeletedAt == null).Select(x => x.PermissionKey).Distinct().ToListAsync();
        await cache.SetPermissionsAsync(tenantId, userId, scopeType, scopeId, perms, TimeSpan.FromMinutes(2));
        return Results.Ok(perms);
    });

    return g;
}

app.Run();

public partial class Program { }

public sealed class OutboxDispatcherWorker(IServiceProvider sp, ILogger<OutboxDispatcherWorker> logger, IClock clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = sp.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var pub = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
            var messages = await repo.AcquirePendingAsync(20, clock.UtcNow, TimeSpan.FromSeconds(30), stoppingToken);
            foreach (var m in messages)
            {
                try
                {
                    var env = new IntegrationEventEnvelope(Guid.NewGuid(), clock.UtcNow, m.Version, m.CorrelationId, m.CausationId, null, m.TenantId, null, m.Type, JsonSerializer.Deserialize<object>(m.PayloadJson)!);
                    await pub.PublishAsync(env, stoppingToken);
                    await repo.MarkDispatchedAsync(m.Id, clock.UtcNow, stoppingToken);
                }
                catch (Exception ex)
                {
                    var attempts = m.Attempts + 1;
                    var dead = attempts >= 10;
                    await repo.MarkFailedAsync(m.Id, attempts, clock.UtcNow.AddSeconds(Math.Pow(2, Math.Min(attempts, 6))), ex.Message, dead, stoppingToken);
                    logger.LogError(ex, "Failed outbox message {Id}", m.Id);
                }
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
