---
name: backend-guardrails
description: >
  USE THIS SKILL whenever you create, modify, or review backend code in the
  AI-Dashboard project. Triggers on "add a controller", "create a service",
  "write an entity", "new endpoint", "backend structure", "C# code",
  "repository pattern", "EF Core", "clean architecture", or any change to
  files under the Backend/ directory. Run this skill alongside project-context
  and scope-enforcer.
---

# Backend Guardrails

Follow these rules for all work under the `Backend/` directory.

## Directory Structure

```
Backend/
├── docker-compose.yml
├── docker-compose.override.yml
├── Presentation.slnx
├── Domain/
│   ├── Enums/
│   └── Models/
├── Application/
│   ├── DTos/Request/
│   ├── DTos/Response/
│   ├── Interfaces/          Service contracts (IConnectionService, etc.)
│   ├── Services/            Business logic implementations
│   └── Validators/          FluentValidation validators
├── Infrastructure/
│   ├── Data/                AppDbContext, EF Core configs, migrations
│   ├── Encryption/          (future) Encryption utilities
│   ├── Tunneling/           (future) SSH via Renci.SshNet
│   ├── ExternalDb/          (future) User DB connections
│   └── Ai/                  (future) AI client & prompt builder
└── Presentation/
    ├── Dockerfile
    ├── Controllers/         API endpoints
    ├── Middleware/          ExceptionHandling, SecurityHeaders, RequestLogging
    └── Program.cs           DI registration, middleware pipeline, OpenAPI
```

## Architecture Rules

### Dependency Direction

```
Domain ← Application ← Infrastructure ← Presentation
```

- **Domain** — Zero dependencies. References nothing.
- **Application** — Depends only on `Domain`.
- **Infrastructure** — Depends on `Application` and `Domain`.
- **Presentation** — Depends on `Application` and `Infrastructure`.

### Interface Contracts

- `Application/Interfaces/` — Service contracts for business operations.
  Implemented by `Application/Services/`.

### Project Reference Rules

```
Domain.csproj          → (none)
Application.csproj     → Domain
Infrastructure.csproj  → Application, Domain
Presentation.csproj    → Application, Domain, Infrastructure
```

### Naming Conventions

- Classes & interfaces: PascalCase (`ConnectionService`, `IConnectionService`)
- Parameters & variables: camelCase (`connectionString`, `schemaResult`)
- Files: match the class name exactly (`ConnectionService.cs`)
- Namespaces: match folder path (`Domain.Models`, `Application.Services`)
- Folders: PascalCase (`Application/Services/`, not `Application/services/`)

## NuGet Packages by Layer

| Layer            | Package                                                  | Version |
|------------------|----------------------------------------------------------|---------|
| **Domain**       | `Microsoft.EntityFrameworkCore`                          | 10.0.8  |
| **Domain**       | `Microsoft.Extensions.Identity.Stores`                   | 10.0.8  |
| **Application**  | `FluentValidation`                                       | 12.1.1  |
| **Application**  | `FluentValidation.DependencyInjectionExtensions`         | 12.1.1  |
| **Infrastructure**| `Microsoft.AspNetCore.Identity.EntityFrameworkCore`    | 10.0.8  |
| **Infrastructure**| `Microsoft.EntityFrameworkCore.Design`                 | 10.0.8  |
| **Infrastructure**| `Microsoft.EntityFrameworkCore.Tools`                  | 10.0.8  |
| **Infrastructure**| `Npgsql.EntityFrameworkCore.PostgreSQL`                 | 10.0.2  |
| **Presentation** | `Microsoft.AspNetCore.Authentication.JwtBearer`          | 10.0.8  |
| **Presentation** | `Microsoft.AspNetCore.OpenApi`                           | 10.0.8  |

- Never add EF Core packages to the **Presentation** layer.
- Only FluentValidation packages belong in **Application**.
- Npgsql is the only supported PostgreSQL provider — do not add other database
  providers.

## Code Conventions

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- Async for all I/O-bound public methods: `Task<T>`, `ValueTask<T>`
- Use `Task.WhenAll(...)` for parallel dashboard query execution — never
  sequential
- Map via Mapster or manual DTO mapping (no AutoMapper)
- Configuration via `IOptions<T>` pattern
- Keep controllers thin: validation in Validators, logic in Services, data in
  Infrastructure

## Security Rules

- Validate runtime SQL as SELECT-only before execution. Reject
  INSERT/UPDATE/DELETE/DROP/ALTER/TRUNCATE.
- Encrypt connection strings via Infrastructure/Encryption before storage.
  Never log or expose credentials.
- Schema inspection: metadata only (tables, columns, types, foreign keys).
  Never read actual row data.
- Shared dashboards: UUID links, read-only. No mutations via share links.

## EF Core Conventions

- Internal PostgreSQL only (via Npgsql)
- Entity configurations in `Infrastructure/Data/Configurations/` using
  `IEntityTypeConfiguration<T>`
- Table names: plural snake_case (`saved_dashboards`, `widgets`)
- Migrations in `Infrastructure/Data/Migrations/`
- No business logic in DbContext or entity configurations

## Preserve Existing Patterns

Check existing files in the relevant project before adding new ones. Match
the established style for controllers, services, and DTOs.
