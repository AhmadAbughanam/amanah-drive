# ADR 0001 — Modular Monolith Over Microservices

## Status

Accepted

## Context

Amanah Drive has four natural domain boundaries — authentication, drive/file metadata, document processing, and search/chat — plus a genuinely separate AI service. It would be easy to justify splitting each of these into its own deployable service: the boundaries already exist in the code, and microservices are a familiar pattern to reach for when a system "looks" service-shaped.

But the product is a single-admin personal knowledge drive running on one VPS. A microservices split would add real, immediate costs — service-to-service authentication, message broker reliability, per-service database migrations, distributed observability, deployment ordering, retry/idempotency handling, and data ownership boundaries — none of which the system currently has a concrete need for. There is no independent scaling requirement, no separate team ownership, and no separate deployment lifecycle driving this.

## Decision

Keep the ASP.NET Core API as a single deployable service, structured internally as a **modular monolith**. The API is organized into vertical modules (`Modules/Auth`, `Modules/Drive`, `Modules/Processing`, `Modules/SearchChat`) plus `Shared/Infrastructure` for cross-cutting concerns (the DB context, the AI service HTTP client, CORS, security headers). Each module owns its own endpoints, services, models, options, and EF Core entity configuration. Modules communicate through explicit interfaces — e.g. `SearchChat` depends on `Processing`'s `IChunkSearchRepository` rather than querying `DocumentChunk` directly — never through direct cross-module database access.

The Python FastAPI AI service remains a separate deployable. It was already a clean boundary (a genuinely different language/runtime, a different workload profile — CPU/GPU-bound embedding and generation vs. lightweight HTTP request handling) and splitting it added no distributed-systems cost that the monolith split would have.

## Consequences

- One deployable to build, test, and run for the whole backend, which matches the actual operational scale of the product (single VPS, single user).
- Module boundaries are enforced by interfaces, not by physical service separation, so the discipline has to be maintained by convention and code review rather than by network boundaries forcing it.
- If a concrete reason ever appears — independent scaling, a separate team, a workload (like `Processing`) that needs to scale independently of the HTTP-facing modules — the interface boundaries already in place make extracting that module into its own service a mechanical change rather than a rewrite. The likely future split candidates, if that day comes, are Auth/User, Drive, Processing, Search/Chat, the AI service (already separate), and the web frontend (already separate).
- This decision should be revisited only given a concrete reason (measurable scaling need, deployment-ownership need, or data-ownership need) — not preemptively, and not because microservices are the more "impressive-sounding" architecture.
