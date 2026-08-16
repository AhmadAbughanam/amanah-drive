# ADR 0008 - File-Based Operational Logging

## Status

Accepted

## Context

The API emitted structured events only to the console. Those events were lost when the container was recreated, and inspecting them required host access and `docker logs`. This is a real operability gap for a self-hosted application, but the single-admin, single-API-instance workload does not justify the external aggregation, tracing, metrics, and alerting stack deferred by [ADR 0006](0006-deferring-horizontal-scaling-investment.md).

## Decision

Keep Serilog console output and add compact-JSON rolling files with daily and file-size rollover, bounded retention, and a dedicated Docker named volume.

Expose recent entries through an authenticated, read-only `GET /admin/logs` endpoint with level, text, and pagination filters. Surface that endpoint in the existing authenticated dashboard. The viewer reads the bounded local files directly; it does not introduce a logging database, external shipper, or indexing service.

Request logging remains Serilog's default completion event: method, path, status code, and elapsed time. Headers, request bodies, authorization tokens, passwords, and cookies are not captured.

## Consequences

- API logs survive container recreation and are available without host shell access.
- Retention and size limits bound disk use.
- Compact JSON keeps entries machine-readable and allows the viewer to parse them without free-text scraping.
- Log reads are local-file scans suitable for the current bounded, single-admin workload; they are not intended for high-volume querying or multiple API replicas.
- OpenTelemetry, external log shipping, Grafana/Loki/ELK, centralized indexing, metrics, and alerting remain deferred under ADR 0006.
