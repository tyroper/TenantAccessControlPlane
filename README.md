# TenantAccessControlPlane

Production-oriented .NET 8 control-plane microservice for multi-tenant directory + RBAC + policy management with outbox-based integration events.

## What it is
- Multi-tenant entities for tenants, orgs, users, memberships, roles, permissions, policies.
- Registration endpoints/events for policy + permission definitions (idempotent + ownership-aware).
- Transactional outbox + dispatcher worker.
- Ports/adapters for persistence, caching, messaging.

## What it is not
- Not an identity provider (expects external JWT IdP).
- Not a PDP/PEP replacement in data planes; this is a management control-plane.

## Architecture

```text
[Host API + Worker]
   | uses
[Core Domain + Application]
   | depends on
[Abstractions Ports]
   | implemented by
[EF Postgres] [Redis Cache] [Messaging Adapters]
```

## Local run
1. `docker compose up -d`
2. Install .NET 8 SDK.
3. `dotnet build`
4. `dotnet run --project src/Host`
5. Open Swagger: `http://localhost:5000/swagger`

## Auth0 integration
Configure Authority/Audience in `appsettings` or env vars, map `permissions` claim to include `platform:admin` for admin APIs.

## Downstream enforcement guidance
- Query effective permissions/policies at deploy or runtime.
- Cache grants in downstream services and invalidate on integration events.

## Register definitions from another service
POST tenant route `/api/{tenantId}/definitions/permissions/register` with key, sourceService, version, payloadJson.
Idempotency uses `(DefinitionType, Key, Version, SourceService, PayloadHash)`.

## Extending scopes
Add scope enum values and update assignment/effective permission query logic in Core + Host. The ports stay stable.

## Extension points
- `IEventPublisher` for new bus providers.
- `IEffectiveAccessCache` for custom cache behavior.
- `IDefinitionStore` / `IDefinitionRegistrationStore` for alternate persistence engines.
