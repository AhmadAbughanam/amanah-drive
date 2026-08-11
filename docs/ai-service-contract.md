# AI Service Contract

This defines the boundary between the ASP.NET Core API and the Python FastAPI AI service. The API is the only caller of these endpoints; the AI service does not talk to PostgreSQL or the filesystem directly — the API passes it whatever it needs and persists whatever it returns.

This file should be filled in during Phase 3 (AI Processing) before implementation starts, so both services are built against an agreed contract instead of an assumed one.

## Endpoints (to be finalized in Phase 3)

* `POST /extract` — accepts a file reference, returns extracted text
* `POST /chunk` — accepts text, returns chunks with offsets
* `POST /embed` — accepts chunks, returns vector embeddings
* `POST /rag/query` — accepts a question and retrieval scope, returns an answer with cited source chunks

## Rules

* The API owns authentication and authorization; the AI service trusts requests only from the API (internal network / service token, not exposed publicly).
* Request/response shapes are versioned once the first real implementation lands — breaking changes require a version bump, not a silent shape change.
* The AI service is stateless with respect to business data — it does not maintain its own copy of file metadata or job state.
