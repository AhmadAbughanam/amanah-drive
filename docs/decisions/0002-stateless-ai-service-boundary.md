# ADR 0002 — Stateless AI Service; Retrieval Stays in the API

## Status

Accepted

## Context

The RAG chat flow needs two things from the AI service: a query embedding, and a generated answer given retrieved context. The vector similarity search itself (the pgvector cosine-distance query over `DocumentChunk`) could plausibly live in either the API or the AI service — the AI service already handles embeddings, and `README.md`'s original tech-stack description mentioned the AI service as owning "retrieval" in a loose sense.

Putting the vector search in the AI service would mean giving it direct PostgreSQL access, which conflicts with keeping it a clean, swappable, stateless boundary — and would mean two services independently reasoning about the same schema.

## Decision

The AI service (`ai-service/`, documented in `docs/ai-service-contract.md`) is stateless with respect to business data. It does not connect to PostgreSQL and does not maintain file metadata, processing jobs, vectors, or chat history. Its job is limited to: text extraction (`/extract`), chunking (`/chunk`), embedding (`/embed`), and grounded answer generation from already-retrieved context (`/rag/answer`).

The API (specifically the `SearchChat` module's `SemanticSearchService`, backed by `Processing`'s `IChunkSearchRepository`) owns retrieval: it calls the AI service to embed the query, then runs the pgvector similarity query itself and passes the resulting chunks to `/rag/answer` for generation.

## Consequences

- The AI service can be redeployed, rescaled, or even replaced (a different embedding model, a different LLM provider) without touching how retrieval works, since it never touched the database in the first place.
- All authorization and data-ownership enforcement (a user can only search/chat over their own files) happens in one place — the API — rather than being duplicated or, worse, only partially enforced across two services.
- The AI service can be tested and reasoned about as a pure function of its inputs: given a question and a set of chunks, produce an answer. It has no implicit dependency on database state.
- Every request between the two services is authenticated with a shared service token (`X-Service-Token`), since the AI service trusts requests only from the API, not from any other caller.
