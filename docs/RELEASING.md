# Releasing Amanah Drive

Releases publish versioned API, AI service, and web images to GitHub Container Registry. They do not deploy the application.

This is the deliberate, numbered-version publishing path (`vMAJOR.MINOR.PATCH` tags, `.github/workflows/release.yml`). It is separate from continuous deployment: every push to `main` is automatically built, published under the moving `main` tag and a permanent `sha-<short>` tag, and deployed to the production VPS by the `publish` and `deploy` jobs in `.github/workflows/ci-cd.yml` — see [Deployment](DEPLOYMENT.md) for that path, including rollback using the `sha-<short>` tags it publishes.

## Image Names And Tags

For a repository owned by `example-owner`, release `v1.2.3` publishes:

- `ghcr.io/example-owner/amanah-drive-api:1.2.3`
- `ghcr.io/example-owner/amanah-drive-ai-service:1.2.3`
- `ghcr.io/example-owner/amanah-drive-web:1.2.3`

The same images are also tagged `latest`. Repository-owner names are normalized to lowercase for GHCR.

Only stable semantic versions in the exact form `vMAJOR.MINOR.PATCH` are accepted. Pre-release tags such as `v1.2.3-rc.1` are not published by this workflow.

## Prepare A Release

1. Confirm the intended commit is on the release branch and CI is green.
2. Review the changelog and choose the next semantic version.
3. Configure the Actions repository variable `RELEASE_WEB_API_BASE_URL` with the browser-reachable API URL, such as `https://drive.example.com/api` or `https://drive.example.com:8080`.
4. Create and push an annotated tag:

```bash
git tag -a v1.2.3 -m "Amanah Drive v1.2.3"
git push origin v1.2.3
```

Pushing the tag starts `.github/workflows/release.yml`. The workflow validates the tag, builds all three Dockerfiles, authenticates to GHCR as `github.actor`, and publishes with the repository-provided `GITHUB_TOKEN`. Its explicit permissions are:

```yaml
permissions:
  contents: read
  packages: write
```

No additional publishing secret is required. Package visibility and access are managed in the repository or organization package settings. Private packages require consumers to authenticate to GHCR with credentials that have `read:packages` access.

## Web API URL

`NEXT_PUBLIC_API_BASE_URL` is compiled into the Next.js browser bundle. Changing the container environment variable after publication does not replace that URL. Set `RELEASE_WEB_API_BASE_URL` before creating a release tag; otherwise the workflow builds the web image with `http://localhost:8080`.

If the public API URL changes, publish a new web image. Reusing an existing web image with a different runtime environment value is not sufficient.

## Run Published Images

The local Compose file remains the source of truth for environment variables, PostgreSQL, Jaeger, volumes, and service wiring. The additive GHCR override replaces the application image names. It intentionally leaves the local `build:` declarations untouched, so use `--no-build` when starting released images.

Set the lowercase GitHub owner and desired image version:

```bash
export GHCR_OWNER=example-owner
export AMANAH_DRIVE_VERSION=1.2.3
```

Authenticate first when the packages are private:

```bash
echo "$GHCR_TOKEN" | docker login ghcr.io -u example-owner --password-stdin
```

Pull and start the stack using the existing `.env` configuration:

```bash
docker compose --env-file .env \
  -f infra/docker-compose.yml \
  -f infra/docker-compose.ghcr.yml \
  pull api ai-service web

docker compose --env-file .env \
  -f infra/docker-compose.yml \
  -f infra/docker-compose.ghcr.yml \
  up -d --no-build
```

Use `AMANAH_DRIVE_VERSION=latest` only when tracking the newest published stable release is intentional. Pin a numeric version for repeatable deployments and rollbacks.

The release workflow currently publishes Linux AMD64 images from GitHub-hosted Ubuntu runners. Multi-architecture publication is not configured.
