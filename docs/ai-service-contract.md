# AI Service Contract

This defines the boundary between the ASP.NET Core API and the Python FastAPI AI service. The API is the only caller of these endpoints; the AI service does not talk to PostgreSQL or the filesystem directly. The API passes file bytes/text/chunks to the AI service and persists the returned data.

## Authentication

All AI processing endpoints require a shared service token:

```text
X-Service-Token: <secret>
```

The token is configured through environment variables on both services. Requests without a valid token must return `401 Unauthorized`. `/health` is intentionally unauthenticated.

## Version

This is the Phase 3 V1 contract. Breaking request or response shape changes require a new versioned route in a later phase.

## Embedding Model

Model: `sentence-transformers/all-MiniLM-L6-v2`

Vector dimension: `384`

The API stores embeddings in PostgreSQL `pgvector` columns with type `vector(384)`.

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
  "embeddings": [
    [0.0123, -0.0456]
  ]
}
```

Each embedding array contains exactly `384` floating-point values.

Error responses:

* `400` for empty input.
* `401` for missing or invalid service token.

## Future Endpoint

`POST /rag/query` remains future Phase 4 work. It is intentionally not implemented in Phase 3.

## Rules

* The API owns authentication and authorization; the AI service trusts requests only from the API through the shared service token.
* The AI service is stateless with respect to business data. It does not maintain file metadata, processing jobs, vectors, or chat history.
* The API stores metadata, processing jobs, chunks, and embeddings in PostgreSQL with `pgvector`.
