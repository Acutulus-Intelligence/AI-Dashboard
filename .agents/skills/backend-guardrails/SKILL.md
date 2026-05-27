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
├── Domain/
│   ├── Entities/          SavedDashboard, Widget, ConnectionInfo
│   ├── Enums/             DatabaseType, GraphType
│   └── Interfaces/        Repository contracts (IWidgetRepository, etc.)
├── Application/
│   ├── DTos/Request/      Input DTOs
│   ├── DTos/Response/     Output DTOs
│   ├── Interfaces/        Service contracts (IConnectionService, etc.)
│   ├── Services/          Business logic implementations
│   └── Validator/         FluentValidation validators
├── Infrastructure/
│   ├── Data/              AppDbContext, EF Core configs, migrations
│   ├── Repositories/      Domain.Interfaces implementations
│   ├── Encryption/        (future) Encryption utilities
│   ├── Tunneling/         (future) SSH via Renci.SshNet
│   ├── ExternalDb/        (future) User DB connections
│   └── Ai/                (future) AI client & prompt builder
└── Presentation/
    ├── Controllers/       API endpoints
    ├── Middleware/         ExceptionHandling, SecurityHeaders, RequestLogging
    └── Program.cs         DI registration, middleware pipeline, OpenAPI
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

### Interface Ownership

- `Domain/Interfaces/` — Repository contracts for data access. Implemented by
  `Infrastructure/Repositories/`.
- `Application/Interfaces/` — Service contracts for business operations.
  Implemented by `Application/Services/`.

### Naming Conventions

- Classes & interfaces: PascalCase (`ConnectionService`, `IWidgetRepository`)
- Parameters & variables: camelCase (`connectionString`, `schemaResult`)
- Files: match the class name exactly (`ConnectionService.cs`)
- Namespaces: match folder path (`Domain.Entities`, `Application.Services`)
- Folders: PascalCase (`Domain/Entities/`, not `Domain/entities/`)

## Code Conventions

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- Async for all I/O-bound public methods: `Task<T>`, `ValueTask<T>`
- Use `Task.WhenAll(...)` for parallel dashboard query execution — never
  sequential
- Map via Mapster or manual DTO mapping (no AutoMapper)
- Configuration via `IOptions<T>` pattern
- Keep controllers thin: validation in Validator, logic in Services, data in
  Repositories

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
