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
├── assets/images/         Static images
├── assets/css/            (optional) Additional CSS files
├── config/env.ts          API URL, constants, feature flags
├── data/                  Mock data & static content
│   ├── homePage.js
│   └── customerDashboard.js
├── styles/index.css       Tailwind directives + @layer custom utilities
├── components/
│   ├── ui/                Primitives (Button, Reveal — zero business logic)
│   ├── layout/            Header, Footer (visible across pages)
│   ├── sections/          Marketing sections (HeroSection, FeaturesSection, …)
│   ├── customer/          Post-login dashboard (DashboardsPage + parts/)
│   └── dashboard/         Cross-context preview (DashboardPreview)
├── router.ts              (future) Route definitions
├── main.jsx               React entry point
└── index.html             HTML shell, lang="sv"
```

## Component Organization

| Folder | Purpose | Example |
|--------|---------|---------|
| `ui/` | Generic reusable primitives | `Button`, `Reveal`, `Modal` |
| `layout/` | Structural wrappers across pages | `Header`, `Footer`, `Sidebar` |
| `sections/` | Marketing/content sections | `HeroSection`, `FeaturesSection` |
| `customer/` | Authenticated app experience | `DashboardsPage`, `parts/*` |
| `dashboard/` | Cross-context previews | `DashboardPreview` |

When adding a new feature:
1. Create a folder under `components/` named after the feature
   (e.g. `components/connections/`)
2. For complex features, group sub-components in a `parts/` folder
3. Keep page-level orchestration in a `*Page.jsx` file

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
4. Assets / data

## Naming

- Files & components: PascalCase (`HeroSection.jsx`)
- Icons: destructure from lucide-react
- CSS: Tailwind utility classes exclusively
- Data files: camelCase (`homePage.js`)
- Folders: lowercase, kebab-case for multi-word (`dashboard-preview/`)

## Styling

- Tailwind utility classes only
- Custom utilities in `index.css` via `@layer components { ... }`
- Dark mode: `class` strategy — toggle via `<html class="dark">`
- Use semantic color tokens from `tailwind.config.js` (`bg-surface`,
  `text-on-primary`)
- Design tokens: `px-gutter` (24px), `max-w-container-max` (1440px)

## Routing

- Current: `window.location.pathname` check in `App.jsx`
- `/app` → `DashboardsPage` (customer view)
- Other → Marketing landing page
- Future: React Router or TanStack Router

## Data Flow

- Static data in `src/data/` as JS objects/arrays
- API calls through `src/config/env.ts`
- DTOs match backend `Application/DTos/Request/` and `Application/DTos/Response/`

## Chart & Layout Libraries (future)

- Charts: ECharts / Recharts / ApexCharts via `components/chart/ChartAdapter.jsx`
- Grid: react-grid-layout for drag & drop widget positioning
- Each widget stores: position (`x`, `y`, `w`, `h`), chart type, saved SQL

## Language

- UI text in Swedish (matches existing sections)
- Code, variable names, comments in English
- `index.html` has `lang="sv"`

## Preserve Existing Patterns

Check existing code before adding new files. Match the established export
style, import conventions, and Tailwind usage.
