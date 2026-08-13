# Phase 5 — Hardening & Performance

## Task

Implement Phase 5 per `README.md`. Read `docs/AI_RULES.md`, `docs/architecture.md`, and `docs/ai-service-contract.md` first — this phase touches CI, security, and docs across the whole repo, so re-familiarize with all of it before changing anything.

This phase does not add product features. It closes gaps found by reviewing what Phases 1–4 actually shipped:

### 1. CI/CD — GitHub Actions

Add `.github/workflows/ci.yml` (there is currently no CI at all) with separate jobs, running on push and pull request:
- **api**: restore, `dotnet build`, `dotnet test` for `api/AmanahDrive.Api.slnx` (the test suite uses Testcontainers for Postgres — make sure the runner supports Docker-in-Docker, which GitHub-hosted `ubuntu-latest` runners do by default).
- **ai-service**: install `requirements.txt`, run `pytest -q`.
- **web**: `npm install` + `npm run build` (it's a placeholder today, but this job costs little and prevents regressions once it's built out).

Keep it to build+test — no deployment step in this phase (that's not in scope for Phase 5 per the README, and deploying is a sensitive operation that needs explicit human approval anyway).

### 2. Security review and hardening

Read through every endpoint added in Phases 1–4 and fix these specific known gaps:

- **No rate limiting on `/search` or `/chat`.** Unlike `/auth/login` and `/auth/register`, these are currently unlimited. They're more expensive to abuse than auth endpoints — each call hits the embedding model and, for `/chat`, the paid/quota-limited Hugging Face API. Add a dedicated rate limit policy (reuse the existing `AddRateLimiter` pattern in `Program.cs`, new configurable options alongside the existing `LoginRateLimitPermitLimit`-style settings) applied to both routes.
- **No CORS policy configured.** Add a named CORS policy allowing a configurable list of origins (env-driven, e.g. `Cors:AllowedOrigins`, defaulting to `http://localhost:3000` for local `web/` dev) restricted to what the dashboard actually needs — do not use `AllowAnyOrigin`.
- **No security headers.** `README.md`'s Security section explicitly names HTTPS/TLS/HSTS and CSP as goals; neither is wired up. Add HSTS (skip in `Development`/`Testing` environments, matching the existing `SecureCookies` pattern) and baseline headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and a reasonably strict `Content-Security-Policy` appropriate for a JSON API (this API doesn't serve HTML itself, so the policy can be tight — e.g. `default-src 'none'`).
- **Document what else was checked.** After the above, do a pass over auth, drive, and search/chat endpoints for anything else on the OWASP-Top-10-relevant list `README.md` calls out (injection, broken access control, security misconfiguration) and write a short summary of what you checked and found in the completion report, even if the answer for a given item is "already handled, no change needed."

### 3. Pagination on unbounded listings

`GET /drive/folders` (folder contents) and `GET /chat/{conversationId}` (message history) currently return every row with no limit. Add simple page/pageSize (or cursor) parameters with a sane default and max page size to both, following the existing `NormalizeTopK`-style clamping pattern already used in `SearchChatEndpoints.cs` for `/search`.

### 4. Documentation

- Add `docs/api-reference.md`: a short reference listing every implemented endpoint (method, path, auth requirement, one-line purpose) across auth, drive, search, and chat. This is new — right now the only endpoint documentation is the code itself and `docs/ai-service-contract.md` (which only covers the internal AI-service boundary, not the public API). Keep it a scannable table/list, not a duplicate of the OpenAPI spec.
- Update `README.md`'s Phase 5 status/roadmap section to reflect what's actually done once this task completes.

### 5. Tests

- Rate limit tests for `/search` and `/chat` (same style as the existing `Register_AfterRepeatedInvalidBootstrapAttempts_IsRateLimited` test).
- Pagination tests for `/drive/folders` and `/chat/{conversationId}` (default page size, max page size clamped, page navigation).
- CORS/security header tests are optional if they're awkward to assert in the current test setup — note in the report if skipped and why.

## Constraints

- Don't add new product features beyond what's listed above.
- Don't deploy anything or touch production infrastructure.
- Commits are fine for completed, coherent scope per `docs/AI_RULES.md`, don't push to `main`.
- No destructive git or database operations.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions, anything incomplete, remaining risks.
