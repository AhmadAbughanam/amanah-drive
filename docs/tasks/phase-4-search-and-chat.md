# Phase 4 — Search & Chat

## Task

Implement Phase 4 (Search & Chat) per `README.md`, `docs/architecture.md`, and `docs/ai-service-contract.md`. Read those, plus `docs/AI_RULES.md`, before starting.

LLM provider decision (already made, not up for reinterpretation): use the **Hugging Face Inference API** with a free-tier-friendly instruction-tuned model, e.g. `HuggingFaceH4/zephyr-7b-beta` as the default (configurable via env — if that specific model is unavailable/gated on the free tier when you check, pick another comparable free instruct model on Hugging Face Inference API and record the actual model used in `docs/ai-service-contract.md` and the completion report). Auth via a Hugging Face API token in an env var (e.g. `HF_API_TOKEN`), never hardcoded or committed.

Scope split, matching the MVP cut-line already in `README.md`'s Phase 4 roadmap:
- **Search**: semantic search over existing chunks (no generation).
- **Chat v1**: single-turn retrieval + grounded answer + citations. This is the demoable milestone.
- **Chat v2**: multi-turn conversation history layered on top of v1.

### Architecture note — keep the AI service stateless

Per `docs/ai-service-contract.md`'s existing rule, the AI service does not talk to Postgres. So vector similarity search itself happens in the **API** (it already has the `pgvector`-backed `DocumentChunk` table via `Pgvector.EntityFrameworkCore` from Phase 3). The AI service's job in this phase is: (a) embed a query string (reuse the existing `/embed` endpoint — no new endpoint needed for that), and (b) given a question plus already-retrieved chunk texts, generate a grounded answer with citations back to those chunks (new endpoint — this is the LLM call). The API does retrieval; the AI service does generation.

### 1. Update `docs/ai-service-contract.md`

Add a `POST /rag/answer` endpoint (replacing the old placeholder `/rag/query` description, which implied retrieval — retrieval stays in the API per the note above). Define concrete request/response shapes:
- Request: the question, and a list of retrieved chunks (each with at least an id/reference, source file name, and text) already selected by the API.
- Response: the generated answer text, plus a list of citations mapping parts of the answer back to the input chunk references (a simple approach — e.g. every input chunk actually used, not necessarily precise inline span citations — is fine for V1).
Require the existing `X-Service-Token` auth on this endpoint too. Document the chosen Hugging Face model and how failures/timeouts from the Hugging Face API surface (e.g. `502`/`504` with a clear error body) so the API can handle them.

### 2. `ai-service/` — RAG generation

- Add a service module for the LLM call (calling Hugging Face's Inference API over HTTP with the configured model and token), and a thin router for `POST /rag/answer`, following the existing `services/` + `routers/` structure from the Phase 3 restructure.
- Prompt construction: build a grounded prompt instructing the model to answer only from the provided chunks and to say it doesn't know if the chunks don't contain the answer — this is the hallucination-reduction behavior `README.md` calls for.
- Handle Hugging Face API errors/timeouts gracefully (don't let an unhandled exception 500 with a stack trace — return a clear error response).
- Support stubbing the LLM call in tests the same way `embed`'s model override works (`app.state`), so tests don't make real network calls to Hugging Face.
- Tests: prompt construction includes the question and all provided chunks; a stubbed-LLM happy path returns the expected shape; a simulated upstream failure returns a clean error, not a crash.

### 3. `api/` — search and chat endpoints

- **`GET /search`** — accepts a query string, embeds it via `IAiProcessingClient`/`EmbedAsync`, runs a `pgvector` cosine-distance top-K query against `DocumentChunk` (K configurable, small default e.g. 5), returns results with file name, chunk snippet, and similarity score. Reuse existing ownership scoping patterns from Phase 2 (`DriveEndpoints`) even though there's only one user.
- **`POST /chat`** (v1 behavior) — accepts a question, does the same retrieval as `/search`, calls the AI service's new `/rag/answer` with the question and retrieved chunks, returns the generated answer plus citations (chunk id, file name, snippet). No persistence yet at this point in the build — but see below, v2 extends this same endpoint rather than adding a separate one.
- **Conversation persistence (v2)**: add `Conversation` (id, created/updated timestamps) and `ChatMessage` (conversation id, role [`user`/`assistant`], content, citations, created timestamp) entities + migration. Extend `POST /chat` to accept an optional `conversationId` — if omitted, create a new conversation; if provided, load recent prior messages and include them as extra context in the prompt sent to the AI service (simple recency-based inclusion, e.g. last N turns — do not build query rewriting or summarization for V1). Persist both the user question and assistant answer as `ChatMessage` rows. Add a `GET /chat/{conversationId}` to fetch a conversation's message history.
- New `IAiProcessingClient` method for calling `/rag/answer`, following the existing pattern in `Ai/IAiProcessingClient.cs` / `Ai/AiProcessingClient.cs`.

### 4. Tests

- API integration tests (same style as existing `DriveEndpointTests`/auth tests, stubbing `IAiProcessingClient` so tests don't need the real embedding model or Hugging Face): search returns the most relevant seeded chunk first; chat v1 returns an answer with citations from a stubbed AI response; chat v2 persists messages and a second call with the same `conversationId` includes prior turns in what's sent to the (stubbed) AI service; `GET /chat/{conversationId}` returns the stored history in order.
- `ai-service` tests as described in section 2.

## Constraints

- Do not touch `web/` — Phase 4 is API + AI service only.
- New dependency expected: an HTTP client call to Hugging Face's Inference API from `ai-service` (likely just `httpx`/`requests`, already available via FastAPI's stack — avoid adding a heavy new SDK unless there's a good reason, and justify it in the report if you do).
- `HF_API_TOKEN` must be env-configured, added to `.env.example` as a placeholder, and wired through `infra/docker-compose.yml` — never hardcoded or committed with a real value.
- Commits are fine for completed, coherent scope per `docs/AI_RULES.md`, don't push to `main`.
- No destructive git or database operations.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions, anything incomplete, remaining risks.
