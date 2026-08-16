# ADR 0004 — Local Filesystem Storage Behind an Abstraction

## Status

Accepted

## Context

File bytes have to live somewhere. Object storage (S3, Cloudflare R2, MinIO) is the standard choice for anything that might run as more than one instance, since local disk ties uploaded files to whichever specific container wrote them — a real problem the moment there's a load balancer in front of more than one API replica.

Amanah Drive V1 runs as a single API instance on a single VPS. There is no load balancer, no horizontal scaling, and (per ADR 0001) no near-term plan to add one without a concrete reason.

## Decision

V1 stores files on the local filesystem, through an `IFileStorage` interface (`Modules/Drive/Storage/`) rather than calling filesystem APIs directly from business logic. `LocalFileStorage` generates internal GUID-based storage keys rather than deriving paths from user-supplied filenames, and validates every resolved path stays within the configured storage root — both defenses against path traversal, independent of which storage backend is behind the interface.

## Consequences

- Zero infrastructure cost or external dependency for V1 — no object storage account, no additional network hop for every file read/write, no extra secret to manage.
- If the API is ever scaled to multiple instances (see the "future split candidates" note in ADR 0001 and `docs/architecture.md`), swapping in `S3FileStorage` or `MinioFileStorage` is a new implementation of `IFileStorage` and a DI registration change — not a rewrite of `Drive`, `Processing`, or any endpoint that touches files.
- Until that swap happens, the API is not safely horizontally scalable — this is a known, accepted, and load-bearing constraint of the current architecture, not an oversight. `README.md`'s Future Improvements section tracks this explicitly.
