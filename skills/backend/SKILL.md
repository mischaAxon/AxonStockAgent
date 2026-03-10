---
name: backend
description: Design, build, and maintain backend applications and APIs with modern best practices. Use this skill whenever the user asks to create REST or GraphQL APIs, set up a server, implement authentication/authorization, design database schemas, create migrations, build middleware, implement background jobs, or work with any server-side framework (Express, NestJS, FastAPI, Django, Spring Boot, etc.). Also trigger when the user mentions microservices, API design, server architecture, ORM setup, rate limiting, caching, logging, error handling, or backend testing. If the user wants to connect a frontend to a backend, scaffold a new API, or debug server-side issues — use this skill.
---

# Backend Skill

Guides the creation of robust, scalable, and maintainable backend applications.

## Architecture

Organize with clean separation of concerns:

```
src/
├── modules/             # Feature-based modules
│   └── users/
│       ├── controller.ts  # Route handlers
│       ├── service.ts     # Business logic
│       ├── repository.ts  # Database access
│       ├── dto.ts         # Validation schemas
│       └── model.ts       # Database model
├── common/              # Middleware, guards, utils
├── config/              # App configuration
├── database/            # Migrations, seeds
└── jobs/                # Background tasks
```

Dependencies flow inward: controllers → services → repositories → database.

## API Design (REST)

- Use plural nouns for resources (`/users`)
- Version your API (`/api/v1/...`)
- Consistent response envelopes with `data` and `meta`
- Structured error responses with `code`, `message`, `details`
- Validate all input with schema-based validation (Zod, Pydantic)

## Database

- Use migrations for all schema changes
- Add indexes for frequently queried columns
- Use soft deletes for important data
- Always include `createdAt` and `updatedAt`
- Watch for N+1 queries, always paginate

## Authentication & Authorization

- JWT with short-lived access tokens (15 min) + refresh tokens
- Hash passwords with bcrypt (cost ≥ 12)
- RBAC with middleware guards

## Security Checklist

- Input validation and sanitization
- Parameterized queries
- Rate limiting on sensitive endpoints
- Explicit CORS origins
- Security headers (Helmet)
- Environment variables for secrets
- HTTPS everywhere

## Testing

- Unit: services, utilities (Jest/Vitest)
- Integration: API endpoints with test DB (Supertest)
- E2E: full user flows (Playwright)
