---
name: scope-enforcer
description: >
  USE THIS SKILL before starting any implementation task. This is the first
  skill to activate on any feature request, bug fix, or code change. Triggers
  on "implement this", "build this task", "add this feature", "fix this bug",
  "create this", "write code for", or whenever a Kanban ticket is picked up.
  Do NOT proceed with implementation until this skill has run to completion.
---

# Scope Enforcer

Run this skill FIRST — before any implementation task.

## Step 1: Identify Scope

Read the task description and determine which layer it affects:

| Scope | Meaning |
|-------|---------|
| `frontend` | Changes under `Frontend/` only |
| `backend` | Changes under `Backend/` only |
| `both` | Changes under **both** `Frontend/` and `Backend/` |

## Step 2: Ask If Unclear

If the scope is not explicitly stated in the task, ask the user:

> "Does this task apply to frontend, backend, or both?"

Do not assume. Do not guess. If the task mentions specific files or
directories, you may infer the scope — but state your assumption before
starting.

## Step 3: Apply Correct Guardrails

- **Frontend-only** → Load `frontend-guardrails`. Follow those conventions.
- **Backend-only** → Load `backend-guardrails`. Follow those conventions.
- **Both** → Load both guardrails. Keep changes to each layer in separate
  commits or logical blocks. Do not mix frontend and backend changes in the
  same file.

## Step 4: Handle Cross-Cutting Changes

| Change | Scope |
|--------|-------|
| New NuGet package | backend |
| New npm dependency | frontend |
| Docker, CI/CD, root config | Ask the user |

## Examples

| Task | Action |
|------|--------|
| "Add a bar chart showing revenue by month" | Ask — could be both |
| "Create POST /api/connections endpoint" | Backend |
| "Style the dashboard header" | Frontend |
| "Implement dashboard sharing with UUID links" | Ask — likely both |
