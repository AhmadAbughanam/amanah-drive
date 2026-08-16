# ADR 0007 - API Documentation and Probe Hardening

## Status

Accepted

## Context

The API had a static endpoint table, but no live browsable documentation for trying authenticated requests. A single-admin app still benefits from interactive docs during local development, demos, and smoke testing, especially because most endpoints require a real bearer token.

ADR 0006 intentionally deferred broad scaling and observability work. Two items were kept out of that deferral because they close correctness and operability gaps without introducing a larger platform: health probes that distinguish process liveness from database readiness, and atomic processing-job claiming.

## Decision

Use .NET's built-in `Microsoft.AspNetCore.OpenApi` document generation and Scalar for the interactive UI at `/docs`. The OpenAPI document and UI are enabled in Development by default and can be explicitly enabled with `OpenApi:Enabled=true`; they are not exposed by default in Production.

Represent JWT bearer authentication in the generated document so authenticated endpoints can be exercised from the UI with a real access token.

Split health checks into:

- `GET /health/live` for process liveness with no dependency checks.
- `GET /health/ready` for PostgreSQL readiness.
- `GET /health` as a backward-compatible readiness alias.

Claim background processing jobs with a PostgreSQL `UPDATE ... FOR UPDATE SKIP LOCKED ... RETURNING` statement so concurrent workers cannot process the same pending job.

## Consequences

- Local and demo environments have a live API reference without adding Swashbuckle.
- Production does not expose interactive API documentation unless explicitly configured to do so.
- Readiness failures now reflect PostgreSQL reachability instead of only process uptime.
- The job runner is safe against duplicate claims even if a second worker instance is introduced later.
- This does not change the broader ADR 0006 decision: distributed tracing, external metrics, load balancing, separate worker deployments, and distributed rate limiting remain deferred until there is a concrete need.
