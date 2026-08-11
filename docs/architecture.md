# Architecture Reference

This is a short navigation document for agents. The full project plan and architectural source of truth is [README.md](../README.md).

## Stack

- Backend API: ASP.NET Core REST API with JWT authentication
- AI service: Python FastAPI with LangChain, Sentence Transformers, and a RAG pipeline
- Web dashboard: Next.js, TypeScript, and Tailwind CSS
- Database: PostgreSQL with `pgvector`
- V1 file storage: local filesystem on the VPS behind a storage abstraction
- Deployment and infrastructure: Docker, Docker Compose, Nginx, and GitHub Actions

## Repository Layout

* `api/` — ASP.NET Core REST API (authentication, file service, metadata, orchestration)
* `ai-service/` — Python FastAPI service (extraction, embeddings, retrieval, RAG)
* `web/` — Next.js dashboard
* `infra/` — Docker Compose, Nginx, and other deployment config

Each service owns its own dependency manifest and does not reach into another service's directory. New top-level directories should map to one of these four areas; do not introduce a fifth without updating this file.

## Boundaries

- The ASP.NET Core REST API owns authentication, file service behavior, metadata access, and orchestration.
- The Python FastAPI AI service owns extraction, embedding generation, retrieval, summaries, and RAG behavior.
- The Next.js app owns the user dashboard.
- V1 raw file storage is local filesystem storage on the VPS.
- Metadata, processing jobs, embeddings, chat history, and sessions belong in PostgreSQL and `pgvector`.
- Cloudflare R2, S3, and MinIO are future storage-provider options behind the storage abstraction.

## Module Boundaries (`api/`)

The API is a modular monolith: one deployable ASP.NET Core service, one physical PostgreSQL database, and vertical modules under `api/src/AmanahDrive.Api/Modules`.

- `Modules/Auth` owns admin authentication, tokens, auth endpoints, auth options, and auth entity mappings.
- `Modules/Drive` owns folders, file metadata, drive endpoints, storage abstraction, drive options, and drive entity mappings.
- `Modules/Processing` owns processing jobs, document chunks, the background worker, and chunk retrieval over `pgvector`.
- `Modules/SearchChat` owns search/chat endpoints, conversations, chat messages, and semantic search orchestration.
- `Shared/Infrastructure` owns cross-cutting infrastructure: DbContext, migrations, external AI HTTP client, CORS, security headers, and host-level wiring.

Modules communicate through DI interfaces or plain IDs/DTOs, not direct cross-module data access from feature services. For example, SearchChat asks Processing's `IChunkSearchRepository` for retrieved chunks instead of querying `DocumentChunk` directly.

Future split candidates are Auth/User, Drive/File metadata, Processing worker, Search/Chat, AI service, and Web frontend. That split is not happening now; revisit it only with a concrete reason such as independent scaling, deployment ownership, heavy processing load, or separate data ownership.

Do not invent architecture beyond the README. Update this file only when it remains a short navigation aid.
