# Phase 2 — Secure Drive

## Process reminder

`docs/AI_RULES.md` says agents must not create commits unless the human developer explicitly asks for one. Commit `252c02f` ("added placeholders for /web /ai-service , /infra and /api...") was created without that ask. Going forward: leave changes in the working tree uncommitted — the human developer decides when and what to commit.

## Task

Implement Phase 2 (Secure Drive) per `README.md` and `docs/architecture.md`. Read those, plus `docs/AI_RULES.md`, before starting.

Scope — folder and file management on top of the Phase 1 foundation, still single-admin, local filesystem storage:

1. **Data model**
   - `Folder` entity: id, name, parent folder id (nullable, self-referencing for nesting), owner (the single admin user), created/updated timestamps.
   - `FileItem` entity: id, folder id, original filename, stored filename/path, content type, size in bytes, checksum, created/updated timestamps.
   - New EF Core migration for both tables, with appropriate indexes (e.g. `(FolderId, Name)` uniqueness where it makes sense, foreign keys with sensible `OnDelete` behavior — deleting a folder should cascade to its contents, deleting the admin user is out of scope for V1).

2. **Storage abstraction**
   - Introduce an `IFileStorage` interface (or similar) with methods to save, read, and delete a file by a storage key, per `docs/architecture.md`'s note that V1 uses local filesystem storage behind an abstraction so the backend can be swapped later (Cloudflare R2 / S3 / MinIO).
   - Implement `LocalFileStorage` writing under a configured root directory (env-configurable, not hardcoded). Storage keys should not be derived directly from user-supplied filenames — generate an internal key (e.g. GUID-based path) to avoid path traversal, and keep the user-facing filename only as metadata.

3. **Endpoints** (all require the existing JWT auth — reuse `[Authorize]` / the auth middleware from Phase 1, no new auth work needed)
   - Folders: create, rename, delete (recursive), list contents (folders + files) of a given folder (or root).
   - Files: upload (multipart), download, rename, move (change parent folder), delete.
   - Enforce: MIME type allow-list and file size limit (configurable via `AuthOptions`-style options class, e.g. `DriveOptions`), reject path traversal in any user-supplied name, validate that a folder/file being acted on belongs to the single admin (defense in depth even though there's only one user).

4. **Tests**
   - Integration tests (same style as `AuthEndpointTests.cs` — `WebApplicationFactory` + Testcontainers Postgres) covering: folder create/list/rename/delete, file upload/download round-trip (byte-for-byte match), rejecting oversized files, rejecting disallowed MIME types, rejecting path-traversal attempts in filenames, and that deleting a folder deletes its files from storage too (not just the DB rows).

## Constraints

- Do not touch `ai-service/` or `web/` — Phase 2 is API-only.
- Do not introduce new dependencies beyond what's needed for multipart upload handling (ASP.NET Core has this built in) unless something is genuinely missing — justify anything new in the completion report.
- Do not create git commits.
- No destructive git or database operations.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions, anything incomplete, remaining risks.
