---
name: communication
description: Set up and manage real-time communication between frontend and backend services. Use this skill whenever the user asks about API integration, WebSockets, Server-Sent Events (SSE), REST client setup, GraphQL subscriptions, message queues, event-driven architecture, or any form of client-server communication. Also trigger when the user mentions CORS configuration, request/response interceptors, authentication token flow, webhook handling, retry strategies, error propagation between services, API versioning, or inter-service communication in microservices. If the user wants to connect their React frontend to an API, implement real-time features, set up pub/sub messaging, or design a communication layer — use this skill.
---

# Communication Skill

Covers all patterns for communication between services: frontend-to-backend, backend-to-backend, and real-time.

## REST Communication

- Centralized, type-safe API client as single point of contact
- Custom error class with status helpers (isUnauthorized, isNotFound, etc.)
- Integration with TanStack Query for caching and revalidation
- Shared types between frontend and backend (monorepo, OpenAPI codegen, or tRPC)

## Authentication Flow

- Automatic token refresh in the API client
- Redirect to login on session expiry
- Store tokens securely (HTTP-only cookies preferred)

## Real-Time Communication

| Pattern | Direction | Use case |
|---------|-----------|----------|
| REST | Request-Response | CRUD operations |
| WebSocket | Bidirectional | Chat, collaboration, gaming |
| SSE | Server to Client | Notifications, live feeds |
| Webhooks | Server to Server | Third-party integrations |

- WebSockets: implement reconnection with exponential backoff
- SSE: use EventSource API for one-way server-to-client streaming

## CORS Configuration

- Explicit allowed origins (never `*` in production)
- Enable credentials for auth
- In production, use reverse proxy to eliminate CORS entirely

## Inter-Service Communication

- Synchronous: HTTP/gRPC with circuit breakers and retry logic
- Asynchronous: message queues (Redis Pub/Sub, RabbitMQ, NATS)
- Use async when operations can happen in background

## Error Propagation

- Structured errors from backend to frontend
- User-friendly messages for expected errors
- Generic messages for unexpected errors
- Full error logging on backend
