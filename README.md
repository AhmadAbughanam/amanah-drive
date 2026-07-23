# Amanah Drive

`Amanah Drive` is a single-user personal AI drive: a private cloud storage app with AI search, chat, summaries, and tagging.

The product is designed as a practical capstone project with:

- `Go` for the core backend API
- `Python FastAPI` for ingestion and AI workflows
- `PostgreSQL` + `pgvector` for metadata and embeddings
- `Cloudflare R2` for object storage
- `Next.js` for the web dashboard
- `Docker Compose` + `Nginx` for VPS deployment

## Product Goals

- Upload and organize files in folders
- Store raw file objects in `Cloudflare R2`
- Index file content for semantic search and chat
- Ask questions over your files with citations
- Generate summaries and tag suggestions
- Keep the first version single-user and operationally simple

## v1 Scope

### Included

- Single-user auth
- Folder and file CRUD
- Upload, download, move, rename, and soft delete
- Support for `PDF`, `text`, and `markdown`
- Text extraction and chunking
- Embeddings stored in `pgvector`
- Semantic search
- AI chat with citations
- Suggested tags with confirmation
- Docker-based deployment to one VPS

### Excluded

- Multi-user sharing
- End-to-end encryption
- OCR-heavy image workflows
- `DOCX` / `XLSX` ingestion
- Fully agentic file actions

## Architecture

### Applications

- `drive-api/` → Go modular monolith for auth, files, folders, tags, search metadata, and orchestration
- `ai-service/` → Python FastAPI service for extraction, embeddings, retrieval, summaries, and suggestions
- `web/` → Next.js dashboard

### Storage

- `Cloudflare R2` → raw files and derived assets
- `PostgreSQL` → users, folders, files, jobs, chat history, tags, extracted text, chunk metadata
- `pgvector` → embeddings for semantic retrieval

## Deployment

- one VPS
- one domain
- `Nginx` reverse proxy
- `Docker Compose` for local and VPS orchestration
- environment-variable based secrets

## Suggested Repository Layout

```txt
amanah-drive/
  drive-api/
  ai-service/
  web/
  infra/
  docs/
```

## Initial Build Order

1. Build single-user auth in the Go API.
2. Add folders, file metadata, and R2 upload flow.
3. Add Python ingestion for PDF, text, and markdown.
4. Store chunks and embeddings in PostgreSQL + `pgvector`.
5. Add semantic search and AI chat with citations.
6. Build the Next.js dashboard.
7. Add Docker Compose and Nginx for VPS deployment.

## Git

This directory is intended to be its own git repository so it can be pushed independently from the learning workspace.
