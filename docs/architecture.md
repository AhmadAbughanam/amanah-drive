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

The current README defines the services and architecture but does not lock in directory names yet. Follow existing repository structure as it is introduced.

## Boundaries

- The ASP.NET Core REST API owns authentication, file service behavior, metadata access, and orchestration.
- The Python FastAPI AI service owns extraction, embedding generation, retrieval, summaries, and RAG behavior.
- The Next.js app owns the user dashboard.
- V1 raw file storage is local filesystem storage on the VPS.
- Metadata, processing jobs, embeddings, chat history, and sessions belong in PostgreSQL and `pgvector`.
- Cloudflare R2, S3, and MinIO are future storage-provider options behind the storage abstraction.

Do not invent architecture beyond the README. Update this file only when it remains a short navigation aid.
