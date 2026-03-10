---
name: frontend-react
description: Build, structure, and maintain React frontend applications with modern best practices. Use this skill whenever the user asks to create React components, pages, hooks, state management, routing, forms, API integration, or any frontend feature using React. Also trigger when the user mentions Next.js, Vite, React Router, Zustand, Redux, TanStack Query, Tailwind CSS in a React context, component architecture, or frontend testing with Jest/Vitest/React Testing Library. If the user wants to scaffold a new React project, refactor components, implement responsive layouts, add accessibility, or debug frontend issues — use this skill.
---

# Frontend React Skill

This skill guides the creation of production-grade React applications with clean architecture, modern tooling, and maintainable code.

## Project Structure

Organize React projects with a clear, scalable folder structure:

```
src/
├── components/          # Reusable UI components
│   ├── ui/              # Atomic/base components (Button, Input, Modal)
│   └── layout/          # Layout components (Header, Sidebar, Footer)
├── features/            # Feature-based modules
│   └── auth/
│       ├── components/  # Feature-specific components
│       ├── hooks/       # Feature-specific hooks
│       ├── services/    # Feature-specific API calls
│       └── index.ts     # Public API of the feature
├── hooks/               # Shared custom hooks
├── services/            # API layer / HTTP client setup
├── stores/              # Global state management
├── utils/               # Helper functions, constants, types
├── pages/               # Route-level page components
├── styles/              # Global styles, Tailwind config
└── types/               # Shared TypeScript types/interfaces
```

Group by feature, not by file type. Each feature folder is self-contained.

## Component Patterns

- Use TypeScript with explicit prop interfaces
- Keep components focused on a single responsibility
- Extract complex logic into custom hooks (`use[Purpose]`)
- Use composition over prop drilling
- Separate container (smart) from presentational (dumb) components
- Use `React.memo()` only when profiling shows a real performance issue

## State Management

| Scope | Tool | When to use |
|-------|------|-------------|
| Component-local | `useState` / `useReducer` | UI state, form inputs, toggles |
| Shared across subtree | React Context | Theme, auth, locale |
| Global client state | Zustand (preferred) | Shopping cart, UI preferences |
| Server/async state | TanStack Query | Any data fetched from an API |

## Data Fetching

Use TanStack Query as the default for server state. Set up a centralized API client in `services/api.ts`.

## Styling

Prefer Tailwind CSS. For complex variants, use `clsx` or `cva` (class-variance-authority).

## Routing

Use React Router v6+ or Next.js App Router. Organize routes by feature.

## Forms

Use React Hook Form with Zod for validation.

## Testing

Use Vitest + React Testing Library. Test behavior, not implementation.
Priorities: user-facing behavior > custom hooks > integration tests for critical flows.

## Performance

- `React.lazy()` + `Suspense` for code-splitting
- Virtualization for long lists
- Optimize images with lazy loading
- Monitor bundle size

## Accessibility

- Semantic HTML elements
- `aria-label` on icon-only buttons
- Keyboard navigation for all interactive elements
- `prefers-reduced-motion` support

## Error Handling

Implement Error Boundaries for graceful failure. Wrap each route/section in its own ErrorBoundary.
