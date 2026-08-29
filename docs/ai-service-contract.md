# AI Service Contract

This defines the boundary between the ASP.NET Core API and the Python FastAPI AI service. The API is the only caller of these endpoints; the AI service does not talk to PostgreSQL or the filesystem directly. The API passes file bytes/text/chunks to the AI service and persists the returned data.

## Authentication

All AI processing endpoints require a shared service token:

```text
X-Service-Token: <secret>
```

The token is configured through environment variables on both services. Requests without a valid token must return `401 Unauthorized`. `/health` is intentionally unauthenticated.

## Version

This is the V1 contract implemented by the API and AI service. Breaking request or response shape changes require a new versioned route.

## Health

`GET /health` is unauthenticated and returns:

```json
{
  "status": "ok"
}
```

## Embedding Model

Model: `sentence-transformers/all-MiniLM-L6-v2`

Vector dimension: `384`

The API stores embeddings in PostgreSQL `pgvector` columns with type `vector(384)`.

## Generation Model

Provider: Hugging Face Inference API

Endpoint: `https://router.huggingface.co/v1/chat/completions`

Default model: `openai/gpt-oss-20b`

The Hugging Face API token is configured through `HF_API_TOKEN`. The model can be overridden through `HF_MODEL`.

## `POST /extract`

Accepts multipart form data with a single file part named `file`.

Supported content types:

* `application/pdf`
* `text/markdown`
* `text/plain`

Request:

```http
POST /extract
X-Service-Token: <secret>
Content-Type: multipart/form-data

file=<uploaded file bytes>
```

Response `200`:

```json
{
  "text": "Extracted plain text...",
  "contentType": "application/pdf",
  "characterCount": 1234
}
```

Error responses:

* `400` for unsupported content type or unreadable input.
* `401` for missing or invalid service token.

## `POST /chunk`

Splits text into fixed-size overlapping chunks. Offsets are zero-based character offsets into the submitted text; `endOffset` is exclusive.

Request:

```json
{
  "text": "Full extracted text...",
  "chunkSize": 1000,
  "overlap": 200
}
```

`chunkSize` and `overlap` are optional. Defaults are `1000` and `200`. `overlap` must be smaller than `chunkSize`.

Response `200`:

```json
{
  "chunks": [
    {
      "index": 0,
      "text": "Chunk text...",
      "startOffset": 0,
      "endOffset": 1000
    }
  ]
}
```

Error responses:

* `400` for invalid chunk settings.
* `401` for missing or invalid service token.

## `POST /embed`

Generates one embedding vector per chunk text. The order of returned embeddings must match the order of submitted texts.

Request:

```json
{
  "texts": [
    "First chunk text",
    "Second chunk text"
  ]
}
```

Response `200`:

```json
{
  "model": "sentence-transformers/all-MiniLM-L6-v2",
  "dimension": 384,
  "usage": {
    "provider": "local",
    "inputTokens": 12,
    "outputTokens": 0
  },
  "embeddings": [
    [0.0123, -0.0456]
  ]
}
```

Each embedding array contains exactly `384` floating-point values.

Error responses:

* `400` for empty input.
* `401` for missing or invalid service token.

## `POST /rag/answer`

Generates a grounded answer from chunks already retrieved by the API. The AI service does not perform vector search or access PostgreSQL.

Request:

```json
{
  "question": "What does the lease say about renewal?",
  "chunks": [
    {
      "fileName": "lease.pdf",
      "text": "The tenant may renew the lease..."
    }
  ],
  "history": [
    {
      "role": "user",
      "content": "What is this document about?"
    },
    {
      "role": "assistant",
      "content": "It describes a lease agreement."
    }
  ]
}
```

`history` is optional and contains recent prior conversation turns selected by the API.

Response `200`:

```json
{
  "answer": "The lease allows renewal under the conditions in the first retrieved chunk. [1]",
  "model": "openai/gpt-oss-20b",
  "usage": {
    "provider": "huggingface",
    "inputTokens": 342,
    "outputTokens": 86
  },
  "citations": [
    {
      "reference": "1",
      "fileName": "lease.pdf",
      "snippet": "The tenant may renew the lease..."
    }
  ]
}
```

Citation granularity in V1 is chunk-level. Chunks are assigned one-based references from their request order, so the first chunk is cited as `[1]`, the second as `[2]`, and so on. The AI service returns the same number as the citation `reference`; the API maps that ordinal back to its internal chunk and file identifiers. Internal chunk GUIDs are not sent as citation references or injected into the retrieved-chunk prompt. The response includes citation metadata for every supplied chunk; inline numeric markers identify the chunks cited by the generated answer. Character-level citation spans are not returned.

`usage` contains measured tokenizer counts when the model/provider exposes them. A count may be `null` when an upstream response omits usage data; callers must not estimate missing token counts from character length.

Error responses:

* `400` for an empty question or invalid input.
* `401` for missing or invalid service token.
* `502` when Hugging Face returns an upstream error or an invalid response.
* `503` when `HF_API_TOKEN` is not configured.
* `504` when the Hugging Face request times out.

## Rules

* The API owns authentication and authorization; the AI service trusts requests only from the API through the shared service token.
* The AI service is stateless with respect to business data. It does not maintain file metadata, processing jobs, vectors, or chat history.
* The API stores metadata, processing jobs, chunks, embeddings, conversations, and chat messages in PostgreSQL with `pgvector`.
