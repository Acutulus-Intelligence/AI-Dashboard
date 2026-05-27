---
name: project-context
description: >
  USE THIS SKILL whenever you need to understand the AI-Dashboard project:
  its purpose, tech stack, functional flow, or architecture. Triggers on
  "what is this project", "tell me about the app", "how does the app work",
  "what tech stack", "describe the architecture", or any onboarding query.
  Load this skill first before making architectural decisions.
---

# Project Context

Load this context before making any project-wide decisions.

## Purpose

AI-Dashboard lets users connect an external database (PostgreSQL or MySQL),
describe what they want to see in natural language, and get interactive charts
rendered on a draggable dashboard. No raw data is ever exposed during schema
inspection.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 19, Vite 6, Tailwind CSS 3.4, lucide-react |
| Backend | C# .NET 10, ASP.NET Core, Clean Architecture |
| Databases | Internal: PostgreSQL (EF Core). External: PostgreSQL or MySQL |
| Charts | ECharts / Recharts / ApexCharts (adapter pattern) |
| Layout | react-grid-layout (drag & drop) |

## Functional Flow

1. **Connection & Encryption** — User enters external DB credentials in the
   React frontend. Backend verifies, encrypts, and stores them in the internal
   PostgreSQL database.
2. **Schema Inspection** — Backend reads only the external DB structure
   (tables, columns, types, foreign keys). No user data is ingested.
3. **AI Generation** — User writes a prompt (e.g. "Show top 10 customers").
   Backend sends prompt + schema to an AI provider. Strict instructions return
   a safe SQL query + chart type inside a strict JSON schema.
4. **Safe Execution** — Backend validates SQL is SELECT-only, then executes
   against the external database. Results + chart schema return to the frontend.
5. **Presentation & Layout** — Frontend renders raw data in a table and the
   chart. Users drag, drop, and resize widgets via react-grid-layout.
6. **Dashboard Loading & Sharing** — Saved dashboards reload by running all
   saved SQL queries in parallel (Task.WhenAll). Users can generate a public
   read-only UUID link.

## Kanban Workflow

Tasks are typically split into frontend-only or backend-only tickets. Before
starting work, apply the scope-enforcer skill to determine the correct scope,
then load the corresponding guardrails skill.
