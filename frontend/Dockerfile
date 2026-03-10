# ---- Build stage ----
FROM node:20-alpine AS builder
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install --frozen-lockfile 2>/dev/null || npm install

COPY . .
RUN npm run build

# ---- Production stage (serve with lightweight static server) ----
FROM node:20-alpine AS production
WORKDIR /app

RUN npm install -g serve
COPY --from=builder /app/dist ./dist

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

EXPOSE 3000
CMD ["serve", "-s", "dist", "-l", "3000"]
