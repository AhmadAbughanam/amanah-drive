# Changelog

## Current V1

### Phase 1 - Foundation

- Created the four-service repository layout: API, AI service, web, and infrastructure.
- Added Docker Compose with PostgreSQL and pgvector.
- Built the ASP.NET Core API foundation with EF Core, migrations, structured logging, and health checks.
- Implemented single-admin authentication with Argon2id, JWT access tokens, refresh-token rotation, secure refresh cookies, login rate limiting, and account lockout.
- Added integration coverage for login, refresh rotation, lockout, and invalid/reused refresh-token handling.

### Phase 2 - Secure Drive

- Added folder and file metadata models with nested folders and cascade behavior.
- Added local filesystem storage behind `IFileStorage`, using internal GUID storage keys instead of user-supplied paths.
- Implemented folder CRUD and file upload, download, rename, move, and delete endpoints.
- Enforced configured upload limits, MIME allow-listing, and path traversal protection.
- Added integration tests for folder operations, byte-for-byte file round trips, upload validation, and storage deletion.

### Phase 3 - AI Processing

- Defined the API-to-AI-service contract for extraction, chunking, and embeddings.
- Implemented FastAPI endpoints for PDF extraction, text/Markdown passthrough, fixed-size chunking, and Sentence Transformers embeddings.
- Added processing jobs and document chunks with pgvector embeddings.
- Added the background processing worker that turns uploaded files into searchable chunks.
- Added API and AI service tests for job creation, worker success/failure paths, extraction, chunking, and embedding shape.

### Phase 4 - Search and Chat

- Added API-side pgvector semantic search.
- Added grounded answer generation through the AI service and Hugging Face Inference API.
- Added conversations and chat messages with follow-up context.
- Implemented search, chat, and chat-history endpoints with citations.
- Added integration coverage for relevance, citations, conversation persistence, and prompt history.

### Phase 5 - Hardening and Performance

- Added GitHub Actions CI jobs for API, AI service, and web.
- Added rate limiting for search and chat.
- Added explicit CORS allow-listing and baseline security headers.
- Added pagination to folder listing and chat history.
- Added the static API endpoint reference.

### Phase 6 - Web Frontend

- Built the portfolio landing page from the provided CV.
- Added browser login with in-memory access tokens and refresh-cookie support.
- Added the authenticated drive dashboard with folder and file management.
- Restyled the landing and login pages into a shared monochrome editorial design.
- Added Playwright coverage for the landing CTA, login flow, and auth guard.

### Phase 7 - Modular Monolith Refactor

- Reorganized the API into vertical modules: `Auth`, `Drive`, `Processing`, and `SearchChat`.
- Moved cross-cutting infrastructure into `Shared/Infrastructure`.
- Moved entity mappings into module-owned EF Core configurations.
- Replaced direct SearchChat access to processing entities with `IChunkSearchRepository`.
- Updated architecture documentation for module boundaries and future split candidates.

### Phase 8 - Embedding Runtime Fix

- Pinned `torch`, `transformers`, and `accelerate` for compatibility with `sentence-transformers`.
- Added a real-model smoke path so embedding model startup can be verified outside the fast stubbed test suite.
- Verified the rebuilt AI service returns real 384-dimension embeddings.

### Phase 9 - Search and Chat UI

- Added an authenticated dashboard switch between file management and search/chat.
- Added semantic search UI with result snippets, scores, and source downloads.
- Added chat UI with citations, follow-up conversation state, loading states, and error handling.
- Added Playwright coverage for search results, empty states, chat citations, and chat failures.

### Phase 10 - OpenAPI Documentation

- Added built-in OpenAPI document generation.
- Added Scalar UI at `/docs` with bearer-token support.
- Kept interactive docs enabled by default in Development and configurable elsewhere with `OpenApi:Enabled`.

### Phase 11 - Health Probes and Job Claiming

- Split API health checks into `/health/live` and `/health/ready`.
- Kept `/health` as a readiness alias for compatibility.
- Added PostgreSQL readiness checking.
- Reworked processing-job claims with PostgreSQL `FOR UPDATE SKIP LOCKED`.
- Added a real PostgreSQL concurrency test proving jobs are not claimed twice.

### Phase 12 - Documentation Overhaul

- Replaced internal task handoff documents with this changelog.
- Updated README, architecture, API reference, AI service contract, and ADR index to match the implemented system.
- Added ADR 0007 for the OpenAPI, health-probe, and job-claiming decisions.
- Duplicated the system architecture, document processing, and RAG chat flow diagrams into `docs/architecture.md` so the reference is self-contained.

### Phase 13 - Drive Dashboard Redesign

- Rebuilt the authenticated `/drive` dashboard (file management and Search & Chat views) to match a supplied visual design, replacing the interim styling carried over from Phase 6.
- Kept all existing behavior unchanged: folder/file CRUD, upload validation, pagination, semantic search, chat with citations, and conversation state.
- Extended the same visual language to mobile with stacked panels rather than shrinking the desktop layout.

### Phase 14 - Durable Logging and Admin Viewer

- Added daily and size-based rolling API log files in compact JSON format while retaining console logging.
- Added bounded retention and a dedicated Docker volume so logs survive API container recreation.
- Added the authenticated `/admin/logs` endpoint with level, text, and pagination filters.
- Added a Logs view to the authenticated dashboard with loading, empty, error, filtering, and pagination states.
- Recorded the deliberate file-based logging decision in ADR 0008 while keeping external observability infrastructure deferred.

### Phase 15 - CI Security Scanning

- Added a CodeQL workflow for C#, JavaScript/TypeScript, and Python on pushes and pull requests.
- Added NuGet, npm, and Python dependency vulnerability scans to the existing service CI jobs.
- Set NuGet to fail on Critical findings, npm to fail on High or Critical findings, and Python auditing to report without blocking while known upstream advisories remain unresolved.
- Updated `pypdf` to an available fixed release after auditing the current Python dependency graph.

### Phase 16 - Outbound HTTP Resilience

- Added retries with exponential backoff and jitter around API-to-AI-service HTTP calls.
- Added per-attempt and overall request timeouts plus circuit breaking to the API AI client.
- Added transient-only Hugging Face retries with Tenacity while leaving configuration and other client errors non-retryable.
- Added focused tests for recovery, circuit opening, fail-fast behavior, and non-retryable responses.

### Phase 17 - Local Distributed Tracing

- Instrumented ASP.NET Core, `HttpClient`, FastAPI, and HTTPX with OpenTelemetry tracing.
- Added W3C trace-context propagation across API-to-AI-service calls.
- Added Jaeger all-in-one to Docker Compose with in-memory storage and OTLP receivers.
- Documented the deliberately narrow tracing scope in ADR 0009 while leaving metrics, alerting, external retention, and multi-replica observability deferred.

### Phase 18 - Domain Activity Feed

- Added lightweight in-process domain notifications for uploads, processing outcomes, and answered chats.
- Added the Admin-owned `activity_entries` projection and authenticated, filterable `/admin/activity` endpoint.
- Added a paginated Activity dashboard view with periodic refresh.
- Kept activity handlers non-critical so notification failures cannot fail the originating operation.
