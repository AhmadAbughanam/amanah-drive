# ADR 0006 — Deferring Horizontal Scaling and Observability Investment

## Status

Accepted

## Context

It's straightforward to describe a production-grade target architecture for Amanah Drive: multiple stateless API replicas behind a load balancer, a separately deployed processing worker, OpenTelemetry-based distributed tracing feeding Prometheus/Grafana, formal SLOs (latency percentiles, availability targets), distributed rate limiting, and load/chaos testing. All of that is standard, well-understood practice for a system serving real concurrent multi-user traffic.

None of it is required for Amanah Drive V1. It is a single-admin personal application designed around one API instance and one Postgres database. Building that infrastructure now would be solving a problem the system does not have, at a real cost in complexity and time that could otherwise go toward the product itself.

## Decision

Defer horizontal-scaling and full-observability investment until there is a concrete, measured reason to need it - not a theoretical one. Two narrow items were identified as worth doing regardless of scale because they close real correctness and operability gaps without starting the broader scaling epic: splitting `/health` into liveness and readiness probes, and adding atomic job-claiming safety to the processing worker. Those two exceptions are recorded in [ADR 0007](0007-api-documentation-and-probe-hardening.md). Everything else from that target picture - externalized object storage, a load balancer, multiple API/worker replicas, OpenTelemetry/Prometheus/Grafana, distributed rate limiting, formal SLOs, and load/chaos testing - is recorded in `README.md`'s Future Improvements as a deliberate "not now."

## Consequences

- Engineering time stays focused on the product's actual current scale and actual current gaps (see ADR 0004's storage constraint) rather than speculative infrastructure.
- The architecture is still deliberately built so this work is additive later, not a rewrite: `IFileStorage` is already an abstraction (ADR 0004), the API is already close to stateless aside from local file storage, and the modular monolith (ADR 0001) already has the boundaries a future extraction would use.
- This decision itself — knowing when *not* to build something — is treated as part of the engineering work being demonstrated by this project, not as a gap in it.

## Subsequent Decision

[ADR 0009](0009-narrow-local-distributed-tracing.md) adds one further narrow exception: correctly propagated API-to-AI-service traces exported to an in-memory local Jaeger instance. It does not introduce the production observability platform deferred here; metrics, alerting, external retention, collector infrastructure, and multi-replica operations remain deferred.
