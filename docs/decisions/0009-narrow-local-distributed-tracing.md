# ADR 0009 - Narrow Local Distributed Tracing

## Status

Accepted

## Context

[ADR 0006](0006-deferring-horizontal-scaling-investment.md) deferred a full observability and horizontal-scaling program because Amanah Drive is a single-admin application running one API instance on one VPS. That remains the correct scope. However, requests already cross a real process boundary between the API and AI service, and logs alone do not show the connected timing and parent-child relationships across that boundary.

Implementing trace-context propagation and viewing those traces locally closes that specific diagnostic gap without introducing a production observability platform.

## Decision

Instrument the ASP.NET Core API and FastAPI AI service with OpenTelemetry tracing. Use W3C `traceparent` propagation for API-to-AI-service HTTP calls and OTLP exporters in both services.

Run Jaeger all-in-one in Docker Compose with in-memory storage. Keep its UI and query API loopback/private-network only. Expose a narrow authenticated `/admin/traces` read model from the API for the application's Observability workspace; normalize only trace hierarchy, services, operations, timings, and error state rather than forwarding raw tags. Use parent-based always-on sampling because the current workload is low volume and local. Export happens in background batches, Jaeger is not part of either service's health checks or startup dependencies, and tracing can be disabled through configuration.

Capture framework HTTP server spans, API HTTP client spans, and AI-service HTTPX client spans. Do not capture request or response bodies, authorization headers, cookies, tokens, or file contents.

## Consequences

- One request can be followed across the API and AI service under a single trace ID.
- The admin can inspect common trace timelines in the same authenticated workspace as logs and derived metrics, while Jaeger remains publicly inaccessible.
- Jaeger's in-memory traces are intentionally ephemeral and disappear when its container restarts.
- An unavailable OTLP or query endpoint may produce diagnostics and an isolated trace-view warning, but it does not fail application requests, readiness checks, logs, or metrics.
- This is not a reversal of ADR 0006. Metrics, Prometheus, Grafana, alerting, external trace retention, collector infrastructure, multi-replica trace operations, formal SLOs, and broader scaling work remain deferred.
