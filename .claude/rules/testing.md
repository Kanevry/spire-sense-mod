---
paths:
  - "**/*.test.*"
  - "**/*.spec.*"
  - tests/**
  - vitest.config.*
  - playwright.config.*
---

# Testing Rules (Path-scoped)

## Framework Standards
- **Unit/Integration:** Vitest (v4+) with `happy-dom` or `jsdom` environment.
- **E2E:** Playwright for browser testing.
- **Swift:** Swift Testing framework (NOT XCTest for new code).
- **Coverage:** V8 provider. Minimum 70% globally, higher for critical paths.
- **Default test timeout:** `testTimeout: 10_000` in every `vitest.config.ts`. Override per-test with `{ timeout }` option when needed.

## Test Patterns
- Co-locate tests with source: `my-module.ts` → `my-module.test.ts`
- Name tests descriptively: `it("should return 401 when token is expired")`
- Arrange-Act-Assert pattern. One assertion focus per test.
- Mock external services, never real APIs in unit tests.
- Use `vi.mock()` for module mocking, `vi.spyOn()` for partial mocking.

## Integration Test Patterns
- Use `supertest` for HTTP endpoint testing in Express services.
- Test the full middleware chain: auth → validation → handler → response.
- Database tests: use a dedicated test database or Supabase local dev (`supabase start`).
- Seed test data with factories (`@goetzendorfer/testing-utils` `createFactory`/`createUserFactory`).
- Clean up test data after each test (`afterEach` or transaction rollback).
- Test error responses: verify status codes, error shapes, and that internal details are not leaked.
- API contract tests: validate response bodies against Zod schemas.
- Mock external services (Stripe, Sentry, AI APIs) — never call real APIs in integration tests.
- Use `@goetzendorfer/testing-utils` `createMockSupabase` for Supabase client mocking.

## Server Action Testing

### Mocking `requireAuth()`
- Mock the auth boundary at module level: `vi.mock('@/lib/auth', () => ({ requireAuth: vi.fn() }))`.
- Default mock return: `{ user: { id: 'test-user-id', email: 'test@example.com' }, businessId: 'test-biz-id', supabase: createMockSupabase() }`.
- Test unauthenticated: `vi.mocked(requireAuth).mockRejectedValue(new Error('Unauthorized'))`.
- Reset mocks in `beforeEach` to prevent state leakage.

### Testing Response Envelopes
- Server actions return `{ success: true, data }` or `{ success: false, error }`.
- Test both paths explicitly: verify `success` boolean, `data` shape on success, `error` message on failure.
- Use Zod schema validation on response: `expect(() => ResponseSchema.parse(result)).not.toThrow()`.

### Testing Zod Validation
- Test with valid inputs (happy path).
- Test with missing required fields: expect `{ success: false, error: 'Validation failed' }`.
- Test with wrong types (string where number expected, too-short strings).
- Test boundary values (min/max length, empty strings, special characters).

### Error Boundary Integration
- Test that server action errors don't crash the page: wrap in `try/catch` at the component level.
- Verify error responses are user-friendly (no internal details leaked).
- Test concurrent action calls don't interfere with each other.

### IDOR Testing Patterns
- Test that users cannot access resources belonging to other users/businesses by manipulating IDs in requests.
- Verify every data-fetching server action scopes queries to `businessId` from `requireAuth()`, not from client params.
- Test horizontal privilege escalation: call actions with valid auth but with IDs belonging to a different tenant.
- Test vertical privilege escalation: call admin-only actions with non-admin auth tokens. Expect 403.

## What Must Be Tested
- All server actions (auth + validation + happy path + error path).
- All Zod schemas (valid + invalid inputs).
- Business logic (calculations, state transitions, permissions).
- Edge cases: empty arrays, null values, boundary conditions, Unicode.

### Testing Typed Errors (AppError)
- Test error class instantiation: `expect(new AppError('Not found', 404, 'NOT_FOUND')).toBeInstanceOf(AppError)`.
- Test status codes: `expect(error.statusCode).toBe(404)`.
- Test error codes: `expect(error.code).toBe('NOT_FOUND')`.
- Test error inheritance: `expect(error).toBeInstanceOf(Error)`.
- Test serialization: verify `toJSON()` excludes stack traces in production.
- Test that internal error details are not exposed in HTTP responses.

## What Should NOT Be Tested
- UI component rendering without logic (shadcn/ui primitives).
- Framework internals (Next.js routing, Supabase client initialization).
- Simple pass-through functions with no logic.

## CI Integration
- Tests run in CI on every push.
- Parallel sharding for large test suites (Vitest `--shard`).
- Test results reported as JUnit XML for GitLab integration (`--reporter=junit --outputFile=junit.xml`).
- Coverage reported as Cobertura XML for GitLab MR diff annotations (`vitest.config.base.ts` configures `reporter: ['text', 'cobertura']`).
- Coverage regex: `/All files[^|]*\|[^|]*\s+([\d\.]+)/` extracts percentage for MR badges.
- Failed tests block merge. No exceptions.

## E2E Best Practices
- Use data-testid attributes for stable selectors.
- Avoid `page.waitForTimeout()` — use `page.waitForSelector()` or `expect().toBeVisible()`.
- Test on multiple viewports: desktop (1280x720), mobile (375x667), tablet (768x1024).
- Screenshot on failure. Video on retry.

### E2E Timeout Management
- Set global timeout in `playwright.config.ts`: `timeout: 30_000` (30s per test).
- Navigation timeout: `navigationTimeout: 15_000`.
- Action timeout: `actionTimeout: 10_000` (clicks, fills).
- Expect timeout: `expect: { timeout: 5_000 }`.
- Override per-test for known slow operations: `test.slow()` doubles all timeouts.
- Never increase global timeouts to fix flaky tests — fix the root cause.

## Async & Timeout Patterns
- **WARNING:** Fake timers leak between tests if not restored. A leaked fake timer can cause unrelated tests to hang or timeout. Always restore in `afterEach`.
- Use `vi.useFakeTimers()` for time-dependent tests. Always call `vi.useRealTimers()` in `afterEach`.
- Prefer `vi.advanceTimersByTime(ms)` over `vi.runAllTimers()` for explicit control.
- For async assertions, use `expect(promise).resolves.toEqual(...)` or `expect(promise).rejects.toThrow(...)`.
- Set explicit test timeouts for slow integration tests: `it("slow test", { timeout: 10_000 }, async () => {...})`.
- Never use `setTimeout` in tests for waiting — use `vi.waitFor()` or Playwright's built-in waiting.
- Mock `Date.now()` via `vi.setSystemTime(new Date("2026-01-01"))` for deterministic date tests.
- For event-driven code, use `vi.waitFor(() => expect(spy).toHaveBeenCalled())` instead of arbitrary delays.
- Use `vi.waitFor()` for polling assertions: `await vi.waitFor(() => expect(spy).toHaveBeenCalledTimes(3))`.
- Async `beforeEach`: keep setup fast (< 100ms). Move slow setup to `beforeAll`.
- Test async error propagation: `await expect(asyncFn()).rejects.toThrow(AppError)`.
- For streams/iterators: collect chunks, assert on final result: `const chunks = []; for await (const c of stream) chunks.push(c);`.

## Coverage Enforcement
- Coverage thresholds enforced in `vitest.config.ts`:
  - `statements: 70`, `branches: 70`, `functions: 70`, `lines: 70`
- Critical paths (auth, payments, RLS): aim for 90%+.
- New code must maintain or improve coverage. Never reduce.
- Use `--coverage` flag in CI. Fail pipeline if thresholds not met.

## Accessibility Testing
- Use `@axe-core/playwright` for automated accessibility audits in E2E tests.
- Run `checkA11y()` on every page and major component state (open dialogs, error states, loaded data).
- CI: include accessibility checks in the E2E pipeline. Fail on critical/serious violations.
- Manual checklist: keyboard navigation, screen reader (VoiceOver), high contrast mode, zoom to 200%.
- Test focus management: after route changes, modals, and dynamic content updates.
- Validate color contrast programmatically with axe-core. Override only with documented WCAG exceptions.
- Integrate with Playwright: `import AxeBuilder from '@axe-core/playwright'; const results = await new AxeBuilder({ page }).analyze();`
- Report violations as JUnit artifacts alongside test results.

## Performance Tests
- k6 for load testing on API endpoints.
- Benchmark critical paths (invoice generation, PDF export, AI calls).
- Set baseline metrics. Alert on regression > 20%.

## See Also
development.md · security.md · security-web.md · security-compliance.md · test-quality.md · frontend.md · backend.md · backend-data.md · infrastructure.md · swift.md · mvp-scope.md · cli-design.md · parallel-sessions.md
