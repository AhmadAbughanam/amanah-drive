# Phase 1 Review Fixes

Independent review of the Phase 1 (Foundation) implementation found three issues to fix before this is considered done. Read `docs/AI_RULES.md` first.

## 1. Rate-limit `/auth/register`

`api/src/AmanahDrive.Api/Endpoints/AuthEndpoints.cs` applies `.RequireRateLimiting("login")` to `/auth/login` but not to `/auth/register`. Before the first admin account exists, `/auth/register` allows unlimited attempts at guessing the bootstrap token.

Fix: apply the same (or an equivalent, dedicated) rate limit policy to the `/auth/register` route in `Program.cs` / `AuthEndpoints.cs`.

## 2. Use constant-time comparison for the bootstrap token

`AuthService.RegisterAsync` (`api/src/AmanahDrive.Api/Auth/AuthService.cs`) compares the bootstrap token with:

```csharp
string.Equals(_options.BootstrapToken, bootstrapToken, StringComparison.Ordinal)
```

This is a timing side-channel on a secret comparison. `Argon2idPasswordHasher.VerifyAsync` already does this correctly with `CryptographicOperations.FixedTimeEquals`. Make the bootstrap token check consistent with that: compare byte representations (e.g. UTF-8 encode both sides, pad/hash to fixed length if needed) using `CryptographicOperations.FixedTimeEquals` instead of `string.Equals`.

## 3. Apply EF Core migrations automatically in the Compose path

Nothing currently applies migrations when the API container starts — `api/Dockerfile`'s entrypoint just runs `dotnet AmanahDrive.Api.dll`. On a fresh `docker compose up`, the API will fail on first DB-touching request because the schema doesn't exist yet.

Fix: apply migrations automatically on API startup. Add a call to `dbContext.Database.Migrate()` during app startup in `Program.cs` (after `var app = builder.Build();`, before `app.Run();`), scoped correctly (resolve `AmanahDriveDbContext` from a scope). This is a single-admin, single-instance deployment, so auto-migrate-on-startup is an acceptable and standard pattern here — no need for a separate migration job/init container.

## Constraints

- Do not touch `ai-service/` or `web/`.
- Do not create git commits.
- Re-run `dotnet test` (all 6 existing auth tests must still pass) and add/adjust tests only if needed to cover the register rate limit.
- Report back using the `docs/AI_RULES.md` completion report format.
