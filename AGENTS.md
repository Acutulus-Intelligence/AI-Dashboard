# AGENTS.md

Guidance for coding agents working in this repository.

## Purpose

AI-Dashboard lets users connect an external database (PostgreSQL or MySQL),
describe what they want to see in natural language, and get interactive charts
on a draggable dashboard. Schema inspection sends metadata only — not row data —
to the AI provider.

## Tech stack

| Layer | Technology |
|-------|------------|
| Frontend | React 19, Vite, Tailwind CSS 3.4, lucide-react, react-grid-layout, Recharts |
| Backend | C# .NET 10, ASP.NET Core, Clean Architecture |
| Internal DB | PostgreSQL (EF Core / Npgsql) |
| External DBs | PostgreSQL or MySQL (user-connected) |
| Billing / AI | Stripe, OpenRouter |

## Repo layout

```
Frontend/          React app (Vite)
Backend/
  Domain/          Entities & enums (no dependencies)
  Application/     Services, DTOs, validators, interfaces
  Infrastructure/  EF Core, encryption, external DB, AI, Stripe
  Presentation/    API controllers, middleware, Program.cs
  docker-compose.yml
.agents/skills/    Project skills (context, guardrails, scope)
```

Dependency direction: **Domain ← Application ← Infrastructure ← Presentation**.

## How to run locally

| Service | Directory / notes | Command | Port |
|---------|-------------------|---------|------|
| PostgreSQL | Docker service `db` in `Backend/docker-compose.yml`, or local Postgres | `docker compose up db` (from `Backend/`) | 5432 |
| Backend API | `Backend/Presentation` | `dotnet run` (set `ASPNETCORE_ENVIRONMENT=Development`) | 8080 |
| Frontend | `Frontend/` | `npm run dev` | 5173 |

- Start Postgres before the API. On startup the API runs EF migrations and seeds data (`SeedData`).
- Vite proxies `/api` → `http://localhost:8080` (`Frontend/vite.config.ts`). Keep the API on 8080 for the proxy to work.
- Full stack via Docker: `docker compose up` in `Backend/` (needs a local `Backend/.env` — see Config).

Useful scripts:

- Frontend: `npm run dev`, `npm run build`, `npm run lint` (`Frontend/package.json`)
- Backend: `dotnet restore` / `dotnet build` / `dotnet test` on `Backend/Presentation.slnx` (see `.github/workflows/`)

### Backend tests

Route-level integration tests live in `Backend/tests/Presentation.IntegrationTests/`.
They use Testcontainers (Docker required) and cover Auth, Company, Subscription, Admin, and product APIs (connections/schema/graphs/charts/dashboards). Stripe and OpenRouter are faked in the test host.

```bash
dotnet test Backend/Presentation.slnx
# or
dotnet test Backend/tests/Presentation.IntegrationTests
```

## Config (do not commit secrets)

Secrets and environment-specific values are **not** in Git:

| How you run | Config source | Git status |
|-------------|---------------|------------|
| `dotnet run` locally | `Backend/Presentation/appsettings.Development.json` | gitignored |
| Docker Compose (`presentation`) | `Backend/.env` via `env_file` | gitignored |

ASP.NET Core maps env vars with `__` nesting (e.g. `ConnectionStrings__Default`, `Cors__AllowedOrigins__0`, `Jwt__Secret`). Compose expects sections used in `Program.cs`: connection string, Jwt, Encryption, Stripe, Ai, Cors, ExternalDb.

Never commit real keys, connection strings, or JWT/encryption secrets.

## Important gotchas

- Controllers for connections, schema, charts, graphs, and dashboards use `[Authorize]` plus `[RequireActiveSubscription]`. Registration/login work without Stripe; those product flows need an active subscription (and OpenRouter for AI generation).
- Seeded admin user is created in `Backend/Infrastructure/Data/SeedData.cs` on first boot.
- Backend integration tests require Docker (Testcontainers). Frontend CI builds with `npm run build`; `npm run lint` may report existing hook-rule findings.

## Project skills

Before larger changes, load the relevant skill under `.agents/skills/`:

- `project-context` — purpose, stack, functional flow
- `scope-enforcer` — frontend-only vs backend-only scope
- `frontend-guardrails` / `backend-guardrails` — layer and coding conventions
