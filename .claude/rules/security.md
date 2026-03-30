# Security Rules (Always-on)

Core security principles that apply to ALL code. Web-specific rules (CSP, rate limiting, CSRF) are in `security-web.md`. Compliance and AI/LLM rules are in `security-compliance.md`.

## SEC Rule Numbering Convention
SEC identifiers are assigned sequentially as rules are created. Gaps are intentional:
- **SEC-001 to SEC-003**: Reserved for future core authentication/authorization rules
- **SEC-004 to SEC-009**: Core security rules (auth, validation, SQL, secrets, errors)
- **SEC-010 to SEC-012**: Compliance rules (documented in security-compliance.md)
- **SEC-013 to SEC-015**: Advanced protection (XXE, SSRF, crypto)
- **SEC-016 to SEC-017**: Data integrity (CSV injection, session hardening — in security-web.md)
- **SEC-018 to SEC-019**: Reserved candidates (prototype pollution CWE-1321, unsafe deserialization CWE-502 — currently covered by SEC-006)
- **SEC-020**: Supply chain security (dependency trust, build script control)

## Authentication (SEC-004: Auth-at-Boundary)
- Every server action MUST authenticate first: `const { user, businessId, supabase } = await requireAuth()`
- Client-side: `supabase.auth.getSession()` — never trust client data server-side.
- Never pass user IDs from client. Always derive from session.

## Input Validation (SEC-006)
- Zod validation on ALL user inputs. No exceptions.
- Minimum string lengths where appropriate (prevent empty submissions).
- Sanitize before database queries. Use parameterized queries only.

## SQL/PostgREST (SEC-007)
- NEVER use template literals for SQL: `` supabase.from(`${table}`) `` is forbidden.
- Always use parameterized queries or Supabase query builder.
- RLS (Row Level Security) on every table. No `service_role` key in client code.

## Secrets Management
- All secrets in `.env` files. Never hardcode API keys, tokens, or passwords.
- `.env`, `.env.local`, `.env.production` in `.gitignore`. Always.
- `.env.example` with dummy values for every secret (documented).
- Gitleaks pre-commit hook catches accidental secret commits.
- Rotate secrets on any suspected exposure. Immediately.

## Error Exposure (SEC-009)
- Never return `error.message` directly to the client.
- Map errors to user-friendly messages. Log originals server-side.
- Stack traces: development only, never in production responses.

## XXE Prevention (SEC-013)
- Disable external entity processing: `fast-xml-parser` → `processEntities: false`. Never use `DOMParser` with untrusted XML.
- Prefer JSON over XML at all API boundaries. If XML required, validate against strict XSD first.
- Never pass user-supplied file content directly to XML parsers without sanitization.

## SSRF Prevention (SEC-014)
- Use `safeFetch()` / `safeFetchJSON()` from `@goetzendorfer/http-client` for all user-supplied URLs. These block private IP ranges (10.x, 172.16-31.x, 192.168.x, 127.x, ::1, 169.254.x) and non-HTTP schemes before requesting.
- For internal service-to-service calls (e.g., Clank → 10.0.0.x), use `fetchWithTimeout()` or pass `allowPrivateNetworks: true`.
- **DNS rebinding defense:** Set `dnsValidation: true` in `safeFetch()`/`safeFetchJSON()` options to resolve DNS and validate the resolved IP before connecting. When enabled, DNS validation is also applied on each redirect hop. **Node.js-only** — uses `dns.promises.lookup()`. For standalone use, `resolveAndValidate()` is exported from `@goetzendorfer/http-client`.
- Set explicit timeouts (5s default) and limit redirects (max 3 with re-validation) on server-side HTTP clients.
- Redirect handling: use `redirect: 'manual'` and re-validate each Location URL via `validateUrl()` before following. This prevents redirect-based SSRF attacks. The `safeFetch()` function in `@goetzendorfer/http-client` implements this pattern with `RedirectLimitError` for exceeded limits.

## Dependencies
- `pnpm audit --prod` in CI pipeline. Block deploys on critical vulnerabilities.
- Gitleaks (37 rules) + Semgrep SAST (45 custom rules in `.semgrep.yml` + 4 managed rulesets) in CI.
- Review `node_modules` additions in PRs (supply chain awareness).

## Supply Chain Security (SEC-020)
- Use `block-exotic-subdeps=true` in `.npmrc` to prevent transitive dependencies from using git or tarball sources. Mitigates PackageGate-class attacks (CVE-2026-xxxx).
- Set `minimum-release-age=1440` (24 hours) to delay package updates, giving security vendors time to detect malicious releases.
- Use `trust-policy=no-downgrade` to reject packages with lower trust signals than previously installed versions.
- Restrict build script execution: `only-built-dependencies-of[]='@goetzendorfer/*'` — only internal packages may run postinstall/prepare scripts.
- Never use `git+ssh://` or `git+https://` as dependency specifiers in `package.json`. Always use npm registry versions.
- Audit all new dependencies before adding: check npm download trends, last publish date, maintainer count. Minimum 1000 weekly downloads unless justified.
- In CI: always use `pnpm install --frozen-lockfile` to prevent lockfile tampering.

## OWASP Top 10 2021 Mapping

| OWASP ID | Risk | Baseline Coverage |
|---|---|---|
| A01 | Broken Access Control | SEC-004 (Auth-at-Boundary), SEC-007 (RLS), security-web.md (CSRF), open-redirect (CWE-601), path-traversal (CWE-22) |
| A02 | Cryptographic Failures | SEC-015 (below), weak-hash (CWE-328), TLS validation (CWE-295), Semgrep rules #37-38, #42-43 |
| A03 | Injection | SEC-006 (Zod), SEC-007 (parameterized queries), SEC-013 (XXE), prototype-pollution (CWE-1321), ReDoS (CWE-1333) |
| A04 | Insecure Design | MVP scope rules (mvp-scope.md), threat modeling at design phase |
| A05 | Security Misconfiguration | security-web.md (CSP, headers, CORS), CORS wildcard (CWE-942), infrastructure.md (Docker hardening) |
| A06 | Vulnerable Components | Dependencies section (pnpm audit), CI Semgrep (45 custom rules) + Gitleaks (37 rules) |
| A07 | Auth Failures | SEC-004 (requireAuth), SEC-017 (session hardening in security-web.md) |
| A08 | Data Integrity Failures | CI/CD pipeline integrity, Gitleaks, pnpm lockfile, SEC-020 (supply chain), json-parse-untrusted (CWE-502) |
| A09 | Logging Failures | backend.md (structured logging), @goetzendorfer/logger |
| A10 | SSRF | SEC-014 (safeFetch/safeFetchJSON, redirect re-validation) |

## Cryptographic Failures (SEC-015)
- TLS 1.3 minimum for all external connections. Never allow TLS 1.0/1.1.
- Password hashing: bcrypt (cost 12+) or scrypt. Never MD5, SHA1, or plain SHA256 for passwords.
- Use `crypto.subtle` or Node.js `crypto` module for cryptographic operations. Never custom crypto.
- Secrets at rest: encrypt with AES-256-GCM. Never store secrets in plaintext outside `.env` files.
- Random values: `crypto.getRandomValues()` or `crypto.randomUUID()`. Never `Math.random()` for security-sensitive values (tokens, keys, nonces, session IDs). `Math.random()` is acceptable for non-security purposes like retry jitter, UI randomization, and shuffling display order.
- JWT: RS256 or ES256 for signing. Never HS256 with weak secrets. Verify `alg` header to prevent algorithm confusion.

## See Also
development.md · security-web.md · security-compliance.md · testing.md · test-quality.md · frontend.md · backend.md · backend-data.md · infrastructure.md · swift.md · mvp-scope.md · cli-design.md · parallel-sessions.md
