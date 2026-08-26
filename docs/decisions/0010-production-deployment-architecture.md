# ADR 0010 - Production Deployment Architecture

## Status

Accepted

## Context

Amanah Drive had been built and thoroughly verified locally through Phase 21, but `infra/nginx.conf` was a one-line placeholder that was never implemented, `docker-compose.yml` published every service's port to `0.0.0.0`, no service had a restart policy, uploaded files had no volume at all (meaning every deploy would silently destroy them), and there was no path from a `main` commit to a running production instance. Moving to a real VPS required closing these gaps without changing the application architecture itself.

## Decision

**Reverse proxy: Caddy, not Nginx or Traefik.** Caddy obtains and renews Let's Encrypt certificates automatically with no separate certbot process, and redirects HTTP to HTTPS by default. For a single-operator VPS this is meaningfully less to configure and maintain correctly than the Nginx+certbot combination the original placeholder implied.

**Single public domain, path-based routing**, not a subdomain per service. `https://ahmadabughanam.com/api/*` is proxied (with the `/api` prefix stripped) to the `api` container; everything else goes to `web`. This works because the browser calls the API directly from client-side JavaScript (`NEXT_PUBLIC_API_BASE_URL`), not through the Next.js server, so no server-side proxying inside the app itself is needed. It also means no `api.*` DNS record has to be provisioned.

**Only Caddy is publicly reachable.** `api`, `ai-service`, `web`, and `jaeger` all bind their container ports to `127.0.0.1` on the host instead of `0.0.0.0` — reachable from Caddy (which joins the same Docker network as every other service and calls them by service name) and from the operator's own SSH session on the VPS, never from the public internet. `postgres` has no host port binding at all, as it already did before this decision. Jaeger stays local-only and unauthenticated by design — see ADR 0009 — this decision does not change that.

**`UseForwardedHeaders` is now configured in the API**, trusting the proxy hop unconditionally (`KnownNetworks`/`KnownProxies` cleared). This is necessary, not cosmetic: the login and search/chat rate limiters partition by `HttpContext.Connection.RemoteIpAddress`, and refresh-token audit fields record it. Without this middleware, every request behind Caddy would appear to originate from Caddy's internal container address, silently breaking both.

**Continuous deployment builds in GitHub Actions and only pulls on the VPS**, never builds there. A new `deploy.yml` workflow triggers after the existing `CI` workflow succeeds on `main`, builds and pushes images to GHCR tagged `main` (moving) and `sha-<short>` (permanently pinned, for rollback), then deploys over SSH. This reuses the exact GHCR-publishing mechanism `release.yml` already established for numbered releases, just on a different trigger and tag scheme — the two workflows serve different, clearly documented purposes (continuous deploy vs. deliberate versioned release) rather than being merged into one.

**SSH-based deployment**, not a Hostinger-provided GitHub Action. Hostinger does not publish an official action for deploying an arbitrary Docker Compose stack to a VPS; their GitHub integrations target their website-builder/shared-hosting product, not the Docker Manager VPS product being used here. SSH deployment via `appleboy/ssh-action`, against a VPS the operator already has full root access to, gives complete visibility into exactly what commands run and does not add a third-party deployment service as a trust dependency.

**Containers run as non-root** where the base image supports it: the API uses the `.NET` 8+ base images' built-in `$APP_UID`; the AI service creates a dedicated `appuser`; the web image uses the official Node image's built-in `node` user. The log and uploaded-file directories are pre-created and `chown`'d in the API image *before* they become volume mount points, so Docker's volume-initialization-from-image-directory behavior gives the non-root process write access without any runtime `chown` step.

**Restart policies, health checks, and the missing uploaded-file volume were added directly to the base `docker-compose.yml`**, not a production-only override, because they are correct for local development too and Compose cannot remove a `ports:` entry declared in a base file from an override (list-valued fields are merged, not replaced). Only the Caddy service itself lives in the additive `docker-compose.prod.yml` override, following the same pattern already established by `docker-compose.ghcr.yml`.

## Consequences

- Deploying now means pushing to `main`; no manual build or file transfer step exists on the happy path.
- Every deployed commit is individually pinned via its `sha-<short>` image tag, so rollback is pulling a specific prior tag, not reverting a build.
- The VPS itself does no compilation and does not need the .NET SDK, Node, or Python's ML dependency stack installed — only Docker.
- Local development is unaffected: `docker compose up` from `infra/` still works exactly as before, `localhost:8080` etc. still resolve locally, because `127.0.0.1` binding behaves identically to `0.0.0.0` for same-machine access.
- This decision does not reopen ADR 0006 or ADR 0009: no load balancer, no multiple replicas, no external observability platform. It closes VPS-readiness gaps for the single instance this project has always been designed around.
