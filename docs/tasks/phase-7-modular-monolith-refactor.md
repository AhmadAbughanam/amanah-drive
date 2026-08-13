# API Modular Monolith Refactor

## Why

The API (`api/src/AmanahDrive.Api/`) is currently organized by technical concern (`Auth/`, `Ai/`, `Data/`, `Endpoints/`, `Models/`, `Options/`, `Processing/`, `Search/`, `Storage/`), with everything sharing one flat namespace and one `AmanahDriveDbContext.OnModelCreating` configuring every entity together. The system already has four natural domain boundaries (auth, drive/file metadata, document processing, search/chat) but nothing enforces them — code in one area can freely reach into another's DbSets and internal classes.

The decision (made by the human developer, not up for reinterpretation): **do not split into microservices now.** That would add distributed-systems cost — service-to-service auth, message broker reliability, per-service migrations, observability, deployment ordering, retries, idempotency, separate data ownership — before there's a concrete need (independent scaling, separate deployment ownership, heavy processing load, or separate data ownership). Instead, restructure the API into a **modular monolith**: one deployable service, internally organized into vertical modules with enforced boundaries, so that extracting a module into its own service later is mechanical rather than a rewrite.

This is a structure-only refactor. **No behavior, endpoint route, request/response shape, or status code changes.** Every existing test must still pass with the same assertions — only namespaces/imports change. Read `docs/AI_RULES.md` and `docs/architecture.md` first.

## Target structure

```
api/src/AmanahDrive.Api/
  Modules/
    Auth/
      AuthModule.cs            # AddAuthModule(IServiceCollection, IConfiguration) + MapAuthModule(IEndpointRouteBuilder)
      Endpoints/AuthEndpoints.cs
      AuthService.cs, TokenService.cs, Argon2idPasswordHasher.cs, IAuthService.cs, ITokenService.cs, IPasswordHasher.cs, AuthResult.cs, RefreshTokenResult.cs
      Models/AdminUser.cs, RefreshToken.cs
      Options/AuthOptions.cs
      Data/AdminUserConfiguration.cs, RefreshTokenConfiguration.cs   # IEntityTypeConfiguration<T>
    Drive/
      DriveModule.cs
      Endpoints/DriveEndpoints.cs
      Models/Folder.cs, FileItem.cs
      Storage/IFileStorage.cs, LocalFileStorage.cs
      Options/DriveOptions.cs
      Data/FolderConfiguration.cs, FileItemConfiguration.cs
    Processing/
      ProcessingModule.cs
      Models/ProcessingJob.cs, DocumentChunk.cs
      DocumentProcessingWorker.cs, ProcessingJobRunner.cs
      Options/AiServiceOptions.cs (the chunking/worker-poll settings that belong to processing, not the AI HTTP client itself — see Shared below)
      Data/ProcessingJobConfiguration.cs, DocumentChunkConfiguration.cs
      IChunkSearchRepository.cs / ChunkSearchRepository.cs   # new — see "Cross-module boundaries" below
    SearchChat/
      SearchChatModule.cs
      Endpoints/SearchChatEndpoints.cs
      Search/ISemanticSearchService.cs, SemanticSearchService.cs
      Models/Conversation.cs, ChatMessage.cs
      Options/SearchOptions.cs
      Data/ConversationConfiguration.cs, ChatMessageConfiguration.cs
  Shared/
    Infrastructure/
      AmanahDriveDbContext.cs   # DbSets stay centralized (still one physical database); OnModelCreating uses modelBuilder.ApplyConfigurationsFromAssembly(...) to pick up every module's IEntityTypeConfiguration<T> automatically
      AmanahDriveDbContextFactory.cs
      Ai/IAiProcessingClient.cs, AiProcessingClient.cs, AiServiceOptions.cs (the HTTP client + its base URL/token settings — shared because both Processing and SearchChat call the AI service)
      Cors/CorsOptions.cs
      Security/ (HSTS/CSP/security-header middleware, if pulled into its own file)
  Program.cs   # becomes a thin composition root
```

Use judgment on exact file names if something reads more naturally (e.g. combining two small `Data/*Configuration.cs` files) — the goal is the module boundary, not a rigid file count. Keep each module's `*Module.cs` as the single place that registers its DI services, binds/validates its options, and exposes its endpoint-mapping method, so `Program.cs` reads like:

```csharp
builder.Services
    .AddAuthModule(builder.Configuration)
    .AddDriveModule(builder.Configuration)
    .AddProcessingModule(builder.Configuration)
    .AddSearchChatModule(builder.Configuration);
// ...
app.MapAuthModule();
app.MapDriveModule();
app.MapProcessingModule();
app.MapSearchChatModule();
```

Shared, genuinely cross-cutting concerns stay in `Program.cs`/`Shared/Infrastructure`: Serilog setup, the DbContext/Npgsql+pgvector data source, JWT authentication scheme registration (owned conceptually by Auth but wired centrally since `UseAuthentication()`/`UseAuthorization()` ordering is host-level), CORS policy, security headers, Kestrel body-size limit, and the startup migration call. Rate limiter policies should be registered from the module that owns them (`AddAuthModule` adds the `"login"` policy, `AddSearchChatModule` adds the `"ai"` policy) — `AddRateLimiter` can be called multiple times across modules and the policies compose via the options pattern; don't collapse them all into `Program.cs`.

## Cross-module boundaries — the part that actually matters

Splitting folders is easy; the point of this refactor is stopping modules from reaching into each other's data. Specifically:

- **`SemanticSearchService` (SearchChat) currently queries `DocumentChunk` directly**, including `chunk.FileItem.UserId` and `chunk.FileItem.OriginalFileName` — `DocumentChunk` belongs to Processing, `FileItem` belongs to Drive. Move the pgvector similarity query itself into the **Processing** module (it owns `DocumentChunk`), exposed as a small interface — e.g. `IChunkSearchRepository.SearchAsync(userId, queryVector, topK)` returning a plain DTO (chunk id, file id, file name, chunk index, text, distance/score). `SearchChatModule` depends on that interface via DI, not on `DocumentChunk`/EF navigation directly. This is the one piece of real logic movement in this refactor, not just a file move.
- **`IAiProcessingClient` is used by both Processing (extract/chunk/embed) and SearchChat (embed for query, rag/answer)** — this is why it belongs in `Shared/Infrastructure`, not inside either module. It's an external service boundary, not domain logic of either module.
- **`UserId` foreign keys** (on `Folder`, `FileItem`, `Conversation`) referencing `AdminUser` are fine to keep as-is — none of the other modules currently join back to `AdminUsers`, they only filter by the `Guid userId` extracted from JWT claims. That's already the right pattern (reference by id, not by joining into another module's table) — don't change it, just confirm it stays that way.
- **Cascade deletes across module-owned tables** (e.g. deleting a `FileItem` cascading to `ProcessingJob`/`DocumentChunk`) can stay as database-level FK cascades — that's a data-integrity concern, not an in-process method call, and it's fine for a single shared database at this stage. Don't try to replace this with application-level orchestration; that would be over-engineering for what the user asked for.
- Do **not** introduce an in-process event bus/mediator for this refactor. The user's ask is "communicate through interfaces, not random method calls" — plain DI-injected interfaces between modules satisfy that. A domain-event system is more machinery than this step needs and can be added later without breaking the module boundaries this refactor establishes.

## Documentation

Update `docs/architecture.md`: add a short "Module Boundaries (api/)" section describing the `Modules/*` + `Shared/Infrastructure` layout and the rule that modules communicate through interfaces, not direct cross-module data access. Include the likely future split candidates as a short list (Auth/User service, Drive/File metadata service, Processing worker service, Search/Chat service, AI service, Web frontend), explicitly framed as *not happening now* — only revisited given a concrete reason (independent scaling, deployment ownership, heavy processing load, separate data ownership). Keep it as short as the rest of that file — it's a navigation aid, not a design doc.

## Constraints

- No behavior changes. Every existing test (auth, drive, search/chat, rate limiting, pagination, etc.) must pass with unchanged assertions — only imports/namespaces change to match the new structure.
- Don't touch `ai-service/` or `web/`.
- Migrations: don't generate a new EF migration for this refactor if the entity shapes/table names/columns are unchanged — moving `IEntityTypeConfiguration` classes into module folders and using `ApplyConfigurationsFromAssembly` should produce an identical model. If EF's model snapshot ends up byte-different in a harmless way, that's fine; if it wants to generate an actual schema-changing migration, stop and report that rather than forcing it through.
- Commits are fine for completed, coherent scope per `docs/AI_RULES.md`, don't push to `main`.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions (especially how you handled the `SemanticSearchService`/`IChunkSearchRepository` boundary), anything incomplete, remaining risks.
