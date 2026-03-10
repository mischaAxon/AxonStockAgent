---
name: docker
description: Containerize, orchestrate, and deploy applications with Docker and Docker Compose. Use this skill whenever the user asks to create Dockerfiles, docker-compose configurations, multi-stage builds, container networking, volume management, or anything related to containerization. Also trigger when the user mentions container orchestration, Docker networking, health checks, environment management across containers, development vs production Docker setups, CI/CD with Docker, optimizing image sizes, debugging container issues, or deploying multi-service applications. If the user has a project that needs to be containerized, or wants to set up a local development environment with Docker — use this skill.
---

# Docker Skill

Guides the creation of production-grade Docker configurations.

## Dockerfile Best Practices

- Always use multi-stage builds for small, secure production images
- Order layers from least-changing to most-changing for cache optimization
- Use Alpine-based images when possible
- Combine RUN commands to reduce layers
- Use `.dockerignore` to exclude unnecessary files
- Never run containers as root
- Pin base image versions
- Add health checks

## Docker Compose

- Separate dev (with hot reload, bind mounts) and prod (multi-stage, optimized) configs
- Use `depends_on` with health check conditions for proper startup ordering
- Define custom networks to isolate traffic between services
- Named volumes for persistent data (databases)
- `.env` files per environment

## Health Checks

Always define health checks:
- HTTP: `curl -f http://localhost:PORT/health`
- PostgreSQL: `pg_isready -U $POSTGRES_USER`
- Redis: `redis-cli ping`

## Common Patterns

- Run migrations via entrypoint scripts
- Use `docker compose logs -f` for debugging
- Tag images with git SHA in CI/CD
- Set resource limits for production

## Checklist

- Multi-stage build
- Non-root user
- Health checks defined
- `.dockerignore` configured
- Named volumes for persistent data
- Secrets via env vars (not baked in)
- Base images pinned
- Proper depends_on ordering
