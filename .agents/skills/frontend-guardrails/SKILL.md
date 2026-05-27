---
name: frontend-guardrails
description: >
  USE THIS SKILL whenever you create, modify, or review frontend code in the
  AI-Dashboard project. Triggers on "create a component", "add a page", "style
  this", "React component", "frontend structure", "Tailwind class", "new
  section", "JSX file", or any change to files under the Frontend/ directory.
  Run this skill alongside project-context and scope-enforcer.
---

# Frontend Guardrails

Follow these rules for all work under the `Frontend/` directory.

## Directory Structure

```
Frontend/src/
├── assets/
│   ├── images/          Static images (tim.png, etc.)
│   └── css/             Global stylesheets (App.css)
├── config/
│   └── env.ts           API URL, constants, feature flags
├── features/
│   ├── components/      Small reusable components used across features
│   ├── sections/        Larger feature sections & previews
│   │   ├── Hero.jsx
│   ├── layouts/         Shells visible across multiple pages
│   │   ├── Footer.jsx
│   │   └── Header.jsx
│   ├── pages/           Page-level components (one per route)
│   │   ├── DashboardPage.jsx
│   │   └── LandingPage.jsx
│   └── routes.ts        Route path constants
├── router.ts            Application-wide routing
├── main.jsx             React entry point
└── index.html           HTML shell (lang="sv")
```

## Component Responsibilities

| Folder | Purpose | Examples |
|--------|---------|----------|
| `features/components/` | Small, reusable, no business logic | `Button`, `Reveal`, `Card`, `Sidebar` |
| `features/sections/` | Larger compositions that make up a page | `Hero`, `Pricing`, `DashboardGrid` |
| `features/layouts/` | Structural wrappers across pages | `Header`, `Footer`, `Navbar` |
| `features/pages/` | Top-level page for a route | `LandingPage`, `DashboardPage` |

## Component Structure

```jsx
import { SomeIcon } from 'lucide-react';
import ChildComponent from '../path/ChildComponent.jsx';

export default function MyComponent({ prop1, prop2 }) {
  return (
    <div className="tailwind-classes">
      <ChildComponent />
    </div>
  );
}
```

- Use `export default function ComponentName() {}`
- One component per file
- Destructure props in the function signature
- Use relative imports with explicit `.jsx` extension

### Imports Order

1. React / framework
2. lucide-react icons
3. Child components
4. Assets / config / data

## Naming

- Files & components: PascalCase (`Hero.jsx`, `Pricing.jsx`)
- Icons: destructure from lucide-react
- CSS: Tailwind utility classes exclusively
- Config files: camelCase
- Data files: camelCase
- Folders: lowercase, kebab-case for multi-word

## Styling

- Tailwind utility classes only
- Custom utilities in `assets/css/App.css` via `@layer components { ... }`
- Dark mode: `class` strategy — toggle via `<html class="dark">`
- Use semantic color tokens from `tailwind.config.js` (`bg-surface`, `text-on-primary`)
- Design tokens: `px-gutter` (24px), `max-w-container-max` (1440px)

## Routing

- `router.ts` at src root defines the routing logic
- `features/routes.ts` exports route path constants
- `/app` → `DashboardPage` (customer view)
- `/` → `LandingPage` (marketing)
- Future: React Router or TanStack Router

## Data Flow

- Static data lives in `src/data/` as JS objects/arrays
- API calls through `src/config/env.ts`
- DTOs match backend `Application/DTos/Request/` and `Application/DTos/Response/`

## Chart & Layout Libraries (future)

- Charts: ECharts / Recharts / ApexCharts via a `Chart` wrapper component
- Grid: react-grid-layout for drag & drop widget positioning
- Each widget stores: position (`x`, `y`, `w`, `h`), chart type, saved SQL

## Language

- UI text in English (matches existing sections)
- Code, variable names, comments in English
- `index.html` has `lang="sv"`

## Preserve Existing Patterns

Check existing code before adding new files. Match the established export style, import conventions, and Tailwind usage.
