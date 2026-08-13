# Phase 3 — AI Processing

## Task

Implement Phase 3 (AI Processing) per `README.md`, `docs/architecture.md`, and `docs/ai-service-contract.md`. Read those, plus `docs/AI_RULES.md`, before starting.

Scope: turn an uploaded file (from Phase 2) into searchable chunks with embeddings, processed asynchronously in the background. This phase does **not** include search or chat (that's Phase 4) — it ends at "embeddings are stored in pgvector," not at "a user can query them."

### 1. Finalize `docs/ai-service-contract.md`

Fill in the three endpoints needed this phase (`/extract`, `/chunk`, `/embed` — leave `/rag/query` as still-future, Phase 4). Lock down concrete request/response JSON shapes, not just prose. Also specify: the AI service must reject requests without a valid shared service token (a header, e.g. `X-Service-Token`, checked against an env-configured secret) — it is not meant to be called by anything except the API, per the contract's existing "trusts requests only from the API" rule. Update the contract file first, then implement both sides against it.

### 2. `ai-service/` — implement for real

Replace the placeholder `app/main.py` with:
- `POST /extract` — accepts a file (multipart or the API streams bytes), detects PDF vs Markdown vs plain text by content type, returns extracted plain text. Use a standard PDF text extraction library (e.g. `pypdf`) — no OCR, no image handling, matches README's "PDF, Markdown, Plain Text" V1 scope.
- `POST /chunk` — accepts text, returns a list of chunks with character offsets. Simple fixed-size chunking with overlap is enough (e.g. ~1000 chars, ~200 overlap) — do not over-engineer semantic chunking for V1.
- `POST /embed` — accepts a list of chunk texts, returns one embedding vector per chunk using Sentence Transformers (per README's stack — pick a standard small model, e.g. `all-MiniLM-L6-v2`, and note the vector dimension in the contract doc since the API's pgvector column must match it exactly).
- Service-token middleware/dependency enforcing the shared secret on all three endpoints (not on `/health`).
- Basic tests for extraction (PDF + markdown + plain text fixtures), chunking boundaries, and embedding output shape/dimension.

### 3. `api/` — job tracking, background worker, vector storage

- `ProcessingJob` entity: file id, status (`Pending`/`Processing`/`Completed`/`Failed`), error message, timestamps. One job per uploaded file.
- `DocumentChunk` entity: file id, chunk index, text, character offsets, embedding vector column (pgvector). This requires adding the `Pgvector` .NET package (justify it in the completion report — it's the standard EF Core-compatible pgvector type support, needed because raw Npgsql doesn't map `vector` columns to a usable .NET type on its own) plus enabling it in `Npgsql`'s data source builder and `AmanahDriveDbContext`.
- On successful upload in `DriveEndpoints`, create a `ProcessingJob` in `Pending` status instead of doing any processing inline — upload must stay fast (this is the "Background Processing" principle already described in `README.md`).
- A `BackgroundService` worker that polls for `Pending` jobs, calls the AI service (`/extract` → `/chunk` → `/embed` in sequence, using `HttpClient` with the shared service token), persists the resulting `DocumentChunk` rows, and marks the job `Completed` or `Failed` with an error message. Must handle: AI service unreachable, extraction failure (e.g. corrupt PDF), and must not crash the whole worker loop on one job's failure — log and move to the next job.
- Delete a file → delete its `ProcessingJob` and `DocumentChunk` rows too (extend the existing file-delete endpoint / cascade behavior from Phase 2).

### 4. Tests

- `ai-service`: as described in section 2.
- `api`: integration tests for the upload → job creation flow (job row created in `Pending` after upload), and unit/integration tests for the worker's success and failure paths against a stubbed/fake AI service HTTP client (do not require the real Sentence Transformers model to be loaded in CI-style tests — stub the HTTP boundary).

## Constraints

- Do not touch `web/` — Phase 3 is API + AI service only.
- New dependencies expected this phase (Sentence Transformers/pypdf in `ai-service`, `Pgvector` in `api`) — justify each briefly in the completion report, but don't add anything beyond what's listed above.
- Commits are fine for completed, coherent scope (per the updated `docs/AI_RULES.md`) — do not push to `main`.
- No destructive git or database operations.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions, anything incomplete, remaining risks.
