---
paths:
  - src/app/actions/**
  - src/app/api/**
  - src/routes/**
  - src/lib/errors/**
  - src/lib/http/**
  - src/lib/logging/**
  - src/lib/responses/**
  - src/services/**
  - api/**
  - services/**
  - server/**
  - src/index.*
---
# Backend Rules (Path-scoped)

## Server Actions (Next.js)
- File pattern: `src/app/actions/*.actions.ts`
- Always `"use server"` at top of file.
- Auth first: `const { user, supabase } = await requireAuth()`
- For tenant-specific data, look up `businessId` from the database after auth (do not destructure from `requireAuth`).
- Cache `businessId` per request via `React.cache()` wrapping a `getBusinessId(userId, supabase)` helper to avoid redundant DB lookups.
- Zod validation on all inputs before any DB operation.
- Return typed results using the canonical API response envelope (see below).
- Never return raw DB errors to client.

### Canonical API Response Envelope (SEC-009)
All API responses and server action returns MUST use the canonical envelope from `@goetzendorfer/zod-schemas`:
- **Success:** `{ success: true, data: T }` | **Error:** `{ success: false, error: { code, message, details? } }`
- **Standard codes:** `VALIDATION_ERROR` (400), `UNAUTHORIZED` (401), `FORBIDDEN` (403), `NOT_FOUND` (404), `CONFLICT` (409), `RATE_LIMITED` (429), `INTERNAL_ERROR` (500). Do not invent new codes without documenting.
- Never return raw error objects or `error.message` to client (SEC-009). Map to standard code + user-friendly message.
- `details` field: only for validation errors (Zod issues array). Never include stack traces.
- Import: `import { ApiErrorSchema, apiSuccess } from '@goetzendorfer/zod-schemas'`

## API Routes (Next.js)
- Use for webhooks, external API endpoints, cron jobs only.
- Server Actions preferred over API routes for internal mutations.
- Validate webhook signatures (HMAC, timing-safe comparison).
- Rate limit all public endpoints.

## Express Services
- ESM modules (`"type": "module"` in package.json).
- Helmet for security headers.
- Prom-client for Prometheus metrics.
- Structured logging with Pino.
- Health check endpoint: `GET /health` (returns 200 + uptime + version).

### Graceful Shutdown
- Handle `SIGTERM` and `SIGINT` signals to drain connections before exiting.
- Call `server.close()` to stop accepting new connections, then wait for in-flight requests to complete.
- Set a forced exit timeout (10s) to prevent hanging: `setTimeout(() => process.exit(1), 10_000).unref()`.
- Return 503 during shutdown drain if a load balancer continues sending traffic.
- Close database connections and flush logs after the HTTP server is closed.
- Wrap `app.listen()` in `if (process.env.NODE_ENV !== 'test')` so test imports don't start the server.

### Logging & Error Tracking
- Use `@goetzendorfer/logger` (Pino-based) for structured logging. Initialize with `new Logger()`, set level via `LOG_LEVEL` env var.
- Never log PII (emails, names, IBANs, Steuernummer). Use UUIDs and correlation IDs instead.
- Use `req.id` or `x-request-id` header for request correlation. Inject into logger context.
- Log levels: `error` (failures), `warn` (degraded), `info` (request lifecycle), `debug` (dev only).
- Cross-references: structured logging in `infrastructure.md`, DSGVO PII rules in `security-compliance.md`.

### Sentry Integration
- **SDK selection:** Next.js App Router → `@sentry/nextjs` | Express/Docker → `@sentry/node` | Swift → `sentry-cocoa`.
- **Init order (Express):** `initSentry()` → `express()` → security middleware → `express.json()` → routes → `Sentry.setupExpressErrorHandler(app)` (last, before error handler).
- **PII redaction:** Configure `beforeSend` to strip `authorization`/`cookie` headers and redact `request.data`. Configure `beforeBreadcrumb` to scrub emails/IBANs from URLs and messages using a regex redactor. Extend to `beforeSendTransaction` and `beforeSendSpan` (Sentry v8+).
- **User context:** `Sentry.setUser({ id: user.id })` after auth — never set email or name.
- **Config:** Set `environment` from `NODE_ENV`, `release` from `package.json` version. Never log `SENTRY_DSN` — it's a secret URL.

## Streaming / SSE Patterns
- Use SSE for real-time one-way data (AI streaming, live updates). Set `Content-Type: text/event-stream`.
- Send `data: [DONE]\n\n` as final event. Heartbeat every 30s. Max 3 concurrent SSE connections per user.
- Client: `EventSource` or Vercel AI SDK `useChat`/`useCompletion`.
- Clean up on disconnect: `req.on('close')` (Express) or `signal.aborted` (Next.js).

## Health Check Response Schema
- **Liveness** (`GET /health`): `{ status: "ok", uptime, version }` — HTTP 200, no external calls.
- **Readiness** (`GET /ready`): `{ status: "ok"|"degraded"|"unhealthy", checks: { database, redis, ... } }` — 2s timeout per check, 503 if critical check fails.
- Both endpoints excluded from rate limiting, auth, and access logging. Use template from `templates/express-service/src/routes/health.ts`.

## Retry with Exponential Backoff
- Config: 3 attempts, base 1s, max 30s, jitter enabled. Formula: `min(base * 2^attempt + random(0, base), max)`.
- Only retry transient errors (5xx, network timeouts, ECONNRESET). Never retry 4xx.
- Circuit breaker: 5 consecutive failures in 60s → open for 30s → return 503.
- Log every retry attempt with attempt number, delay, and error reason.
- Implement as a generic `withRetry<T>(fn, opts)` wrapper. See `@goetzendorfer/http-client` for `fetchWithRetry()`.

## API Response Shapes
Uses the canonical envelope from the [Canonical API Response Envelope](#canonical-api-response-envelope-sec-009) section above.
- **List variant:** `{ success: true, data: T[], count: number }` — always include total count for pagination.
- **Status codes:** 200 (success), 201 (created), 204 (deleted, no body), 400/401/403/404/409/429/500 per standard codes above.

## API Design Patterns

### Pagination (Cursor-based)
- Use `?cursor=<opaque_id>&limit=20`. Return `{ data: T[], nextCursor: string | null, hasMore: boolean }`.
- Default limit: 20, max: 100. Cursor = opaque base64-encoded `id` or `created_at`. Never use offset pagination for large datasets.

### Versioning
- URL prefix: `/api/v1/`, `/api/v2/`. Max 2 concurrent versions. Deprecate with `Sunset` header.
- Breaking changes (removed fields, renamed endpoints) → new version. Non-breaking → current version.

### Filtering & Sorting
- Convention: `?filter[status]=active&sort=-created_at&limit=20`. `-` prefix for descending.
- Validate all filter/sort fields against an allowlist. Reject unknown fields with 400.

## Error Handling Patterns
- Use typed error classes: base `AppError(statusCode, message, code)` with subclasses like `NotFoundError`, `ValidationError`.
- Distinguish operational errors (expected: 4xx, recoverable) from programmer errors (unexpected: 5xx).
- Express: centralize in a single `(err, req, res, next)` handler mapping `AppError` subclasses to responses.
- Next.js Server Actions: try/catch → return canonical error envelope. Never throw (triggers error boundary).

## External API Integration
- User-supplied URLs: `safeFetch()`/`safeFetchJSON()` from `@goetzendorfer/http-client` (SEC-014). Trusted URLs: `fetchWithTimeout()`/`fetchWithRetry()`.
- Wrap in service classes. Apply retry + circuit breaker (see sections above). Timeout: 30s default.
- Log request/response (minus sensitive data) for debugging.

## See Also
development.md · security.md · security-web.md · security-compliance.md · testing.md · test-quality.md · frontend.md · backend-data.md · infrastructure.md · swift.md · mvp-scope.md · cli-design.md · parallel-sessions.md
