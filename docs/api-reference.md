# API Reference

This is a scannable reference for the public ASP.NET Core API endpoints. The internal AI service contract is documented separately in [AI Service Contract](ai-service-contract.md).

The browsable OpenAPI/Scalar UI is available at `/docs` in Development, or when `OpenApi:Enabled=true` is configured. The generated OpenAPI JSON is available at `/openapi/v1.json` under the same condition and should be treated as the most current schema-level reference.

`/health` remains as a backward-compatible readiness alias because Docker Compose and older smoke checks originally used it.

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/health/live` | No | Liveness probe; returns whether the API process is alive without dependency checks. |
| GET | `/health/ready` | No | Readiness probe; checks PostgreSQL reachability before reporting ready. |
| GET | `/health` | No | Backward-compatible alias for `/health/ready`. |
| GET | `/admin/logs` | Bearer JWT | Return redacted persisted API logs with optional `level`, `search`, `category`, `source`, `from`, `to`, `page`, and `pageSize` filters. |
| GET | `/admin/activity` | Bearer JWT | Return recent domain activity with optional `type`, `search`, `page`, and `pageSize` filters. |
| GET | `/admin/observability` | Bearer JWT | Return request, error, AI usage/cost, and security aggregates for `range=24h`, `7d`, or `30d`. |
| POST | `/auth/register` | Bootstrap token header | Create the single admin account during bootstrap. |
| POST | `/auth/login` | No | Exchange admin credentials for a JWT access token and refresh cookie. |
| POST | `/auth/refresh` | Refresh cookie | Rotate the refresh token and issue a new access token. |
| POST | `/auth/logout` | Refresh cookie | Revoke the current refresh token and clear the refresh cookie. |
| GET | `/drive/folders` | Bearer JWT | List folders and files for a parent folder with `page` and `pageSize`. |
| POST | `/drive/folders` | Bearer JWT | Create a folder. |
| PATCH | `/drive/folders/{folderId}/rename` | Bearer JWT | Rename a folder. |
| PATCH | `/drive/folders/{folderId}/move` | Bearer JWT | Move a folder to another folder or the root; self/descendant destinations return `400`. |
| DELETE | `/drive/folders/{folderId}` | Bearer JWT | Delete a folder, descendants, metadata, and stored files. |
| POST | `/drive/files/upload` | Bearer JWT | Upload a PDF, DOCX, CSV, Markdown, plain-text, PNG, or JPEG file and create a processing job. |
| GET | `/drive/files/{fileId}/download` | Bearer JWT | Download a stored file. |
| PATCH | `/drive/files/{fileId}/rename` | Bearer JWT | Rename a file. |
| PATCH | `/drive/files/{fileId}/move` | Bearer JWT | Move a file to another folder or the root. |
| DELETE | `/drive/files/{fileId}` | Bearer JWT | Delete file metadata, stored bytes, processing job, and chunks. |
| GET | `/search` | Bearer JWT | Semantic search over processed chunks with `query` and optional `topK`. |
| POST | `/chat` | Bearer JWT | Retrieve relevant chunks, ask the AI service for a grounded answer, and persist the exchange with authoritative source metadata for valid cited markers. |
| GET | `/chat/{conversationId}` | Bearer JWT | Return conversation message history and persisted citation metadata with `page` and `pageSize`. |
| POST | `/agent/runs` | Bearer JWT | Persist a `Pending` approval-aware agent run and return immediately, optionally continuing an agent conversation. |
| GET | `/agent/runs/{runId}` | Bearer JWT | Return a run's current state and its conversation's readable ordered step history. |
| POST | `/agent/runs/{runId}/approve` | Bearer JWT | Atomically approve the pending tool call, requeue the run as `Pending`, and return immediately. |
| POST | `/agent/runs/{runId}/reject` | Bearer JWT | Atomically reject the pending tool call, requeue the run as `Pending`, and return immediately. |

`PATCH /drive/folders/{folderId}/move` accepts `{ "folderId": UUID | null }`, matching the file-move endpoint: a UUID selects the destination folder and `null` moves to the root. Missing or unowned source/destination folders return `404`, destination name collisions return `409`, and self/descendant destinations return `400` without changing the tree.

## Agent conversation continuity

`POST /agent/runs` accepts `{ "question": string, "conversationId"?: UUID | null }`. Omitting `conversationId` starts a new conversation; supplying an owned conversation ID starts a separate follow-up run. An unknown or unowned conversation returns `404`. A follow-up returns `409` while that conversation's latest run is `Pending`, `Running`, or `AwaitingApproval`; `Completed`, `Failed`, and `IterationLimitReached` runs can be followed up.

Every agent-run response includes `conversationId`. Its `steps` collection is the full conversation transcript ordered across runs, and each step includes its `runId`. Runs remain separate bounded executions: the configured iteration cap is counted only against assistant steps in the current run, even though model input includes eligible steps from prior runs.

The agent worker atomically claims `Pending` runs as `Running`. Clients observe progress by polling `GET /agent/runs/{runId}` while either status is present, and stop polling when the run reaches `AwaitingApproval` or a terminal state.
