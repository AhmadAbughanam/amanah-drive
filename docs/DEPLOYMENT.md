# Deployment and Maintenance

Production architecture and reasoning: [ADR 0010](decisions/0010-production-deployment-architecture.md). This document is the operational how-to.

## How deployment works

1. You push (or merge a PR) to `main`.
2. `.github/workflows/ci-cd.yml` builds and tests all three services (the `api`, `ai-service`, and `web` jobs).
3. If every one of those jobs succeeds, and only for a push to `main`, the same workflow's `publish` job builds all three Docker images and pushes them to GitHub Container Registry, tagged `main` (moving) and `sha-<short-sha>` (permanently pinned to that commit).
4. The same workflow then connects to the VPS over SSH and runs `docker compose pull` + `docker compose up -d`, which pulls the freshly-pushed `main`-tagged images and recreates only the containers whose image actually changed.
5. Postgres, its data, your uploaded files, and your logs are untouched by this — they live in named volumes that deploys never delete (see "Where persistent data is stored" below).

Nothing on the VPS ever runs `dotnet build`, `npm run build`, or installs the Python ML stack — all building happens on GitHub's runners.

## One-time setup (do this once, in order)

### 1. DNS

Point both of these at your VPS IPv4 `148.230.104.231`:
- `ahmadabughanam.com` → `A` record → `148.230.104.231`
- `www.ahmadabughanam.com` → `A` record → `148.230.104.231`

Wait for these to actually resolve before continuing — Caddy's automatic HTTPS will fail its Let's Encrypt challenge if DNS isn't live yet. Check with `nslookup ahmadabughanam.com` from your own machine.

### 2. Generate a dedicated deploy SSH key (don't reuse your personal key)

On your own machine, not the VPS:

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ./amanah-drive-deploy-key -N ""
```

This creates `amanah-drive-deploy-key` (private) and `amanah-drive-deploy-key.pub` (public) in your current directory. Keep both local; you'll paste the private one into a GitHub secret in step 5 and never store it anywhere else.

### 3. Add the public key to the VPS

```bash
ssh-copy-id -i amanah-drive-deploy-key.pub root@148.230.104.231
```

If `ssh-copy-id` isn't available, append the contents of `amanah-drive-deploy-key.pub` to `/root/.ssh/authorized_keys` on the VPS manually (over your existing SSH access). **Do not remove your existing access method or disable password authentication yet** — verify the new key works first:

```bash
ssh -i amanah-drive-deploy-key root@148.230.104.231 "echo key works"
```

Only once that succeeds should you consider tightening SSH further, and that's a separate, deliberate decision — not something this deployment requires.

### 4. Provision the VPS

SSH in with your normal access and run these, in order. Each command's effect is explained inline.

```bash
# Updates the package index and installs ufw (firewall) and git if not
# already present. Does not change any existing configuration.
apt-get update && apt-get install -y ufw git

# Docker and Docker Compose are already installed per your setup — this
# just confirms the compose plugin is present.
docker compose version

# Configure the firewall: allow only SSH, HTTP, and HTTPS. This does NOT
# touch your existing SSH session or lock you out — ufw only starts
# enforcing rules after `ufw enable`, and SSH (22) is explicitly allowed
# before that happens.
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
# Type 'y' when prompted. Verify immediately in a NEW terminal window
# (keep your current session open) that you can still SSH in before
# closing your original session:
#   ssh root@148.230.104.231
ufw status verbose

# Create the deploy directory and clone the repository.
mkdir -p /opt/amanah-drive
git clone https://github.com/AhmadAbughanam/amanah-drive.git /opt/amanah-drive
cd /opt/amanah-drive
```

### 5. Create the production `.env` file on the VPS

```bash
cd /opt/amanah-drive
cp .env.example .env
nano .env   # or vim, whichever you prefer
```

Fill in every value marked "Production:" in the file's comments with real values. See "Exact environment variables and where secrets go" below for the full list and how to generate each secret. **Set file permissions so only root can read it:**

```bash
chmod 600 .env
```

### 6. Configure GitHub Actions secrets and variables

In your GitHub repository: **Settings → Secrets and variables → Actions**.

Add these **Repository secrets**:

| Secret name | Value |
|---|---|
| `VPS_HOST` | `148.230.104.231` |
| `VPS_SSH_USER` | `root` |
| `VPS_SSH_PORT` | `22` |
| `VPS_SSH_PRIVATE_KEY` | The full contents of `amanah-drive-deploy-key` (the **private** key file from step 2) |

Add this **Repository variable** (Variables tab, not Secrets — it's not sensitive):

| Variable name | Value |
|---|---|
| `RELEASE_WEB_API_BASE_URL` | `https://ahmadabughanam.com/api` |

This last one matters more than it looks: it's compiled into the web app's browser bundle at image-build time (see `docs/RELEASING.md`). If you ever change the domain, you must update this variable and trigger a new build — changing the running container's environment variable alone will not update already-built browser JavaScript.

### 7. Make the GHCR packages pullable from the VPS

The first time the `publish` job in `ci-cd.yml` runs, it publishes three packages to `ghcr.io/ahmadabughanam/amanah-drive-{api,ai-service,web}`. By default, GHCR packages are **private** even in a public repository. Since the repo itself is public, the simplest option is to make the packages public too (no registry login needed on the VPS):

1. After the first successful deploy workflow run, go to your GitHub profile → **Packages**.
2. Open each of the three `amanah-drive-*` packages.
3. **Package settings → Danger Zone → Change visibility → Public.**

If you'd rather keep them private, generate a GitHub Personal Access Token with `read:packages` scope and run `docker login ghcr.io -u AhmadAbughanam -p <TOKEN>` once on the VPS — but public is simpler and there's no proprietary code concern here since the repository itself is already public.

### 8. First deploy

Push any commit to `main` (or just re-run the `deploy` job of the `CI/CD` workflow from the Actions tab). Watch it run in **Actions**. It will fail at the SSH step if any of the secrets above are wrong — the error will say so.

Once it succeeds, on the VPS itself, bootstrap the admin account (there is no registration UI by design — see [ADR 0003](decisions/0003-single-admin-auth-model.md)):

```bash
curl -X POST https://ahmadabughanam.com/api/auth/register \
  -H "Content-Type: application/json" \
  -H "X-Bootstrap-Token: <the AUTH_BOOTSTRAP_TOKEN value from your .env>" \
  -d '{"email":"you@example.com","password":"a-long-password"}'
```

## Exact environment variables and where secrets go

Every variable is listed in `.env.example` with an inline comment on any that needs a different value in production. Summary of what you must generate yourself (never commit these anywhere):

| Variable | How to generate | Lives in |
|---|---|---|
| `POSTGRES_PASSWORD` | `openssl rand -hex 24` | VPS `.env` only |
| `AUTH_JWT_SIGNING_KEY` | `openssl rand -hex 32` | VPS `.env` only |
| `AUTH_BOOTSTRAP_TOKEN` | `openssl rand -hex 24` | VPS `.env` only (used once, then effectively burned — the API only accepts it before an admin exists) |
| `AI_SERVICE_TOKEN` | `openssl rand -hex 24` | VPS `.env` only |
| `HF_API_TOKEN` | Create at <https://huggingface.co/settings/tokens> | VPS `.env` only |
| `VPS_SSH_PRIVATE_KEY` | Generated in one-time setup step 2 | GitHub Actions secret only |

Nothing above should ever appear in a commit, a workflow file, a log line, or a chat message. If a command in this document needs one, it's written as a placeholder like `<the AUTH_BOOTSTRAP_TOKEN value from your .env>` for you to fill in yourself, not typed out.

## How to deploy an update

Just push to `main` (directly or via a merged PR). That's the entire process — no manual step is required on the happy path. Watch progress in the repository's **Actions** tab.

## How to view logs

**Structured application logs** (recommended first stop — persisted, filterable, and viewable in the dashboard itself): log into the app at `https://ahmadabughanam.com`, open the **Logs** tab. Or query directly:

```bash
curl -s "https://ahmadabughanam.com/api/admin/logs?pageSize=50" \
  -H "Authorization: Bearer <your access token>"
```

**Raw container logs** (useful when a container won't even start):

```bash
cd /opt/amanah-drive/infra
docker compose --env-file ../.env -f docker-compose.yml -f docker-compose.ghcr.yml -f docker-compose.prod.yml logs -f api
# swap 'api' for ai-service, web, caddy, or postgres
```

## How to restart the application

```bash
cd /opt/amanah-drive/infra
docker compose --env-file ../.env -f docker-compose.yml -f docker-compose.ghcr.yml -f docker-compose.prod.yml restart
```

This restarts containers without pulling new images or losing volume data. To restart a single service, append its name, e.g. `... restart api`.

## How to roll back

Every deploy is tagged with its commit's short SHA. To roll back to a specific prior commit:

```bash
cd /opt/amanah-drive/infra
export AMANAH_DRIVE_VERSION=sha-<the short sha you want, from the Actions run history>
docker compose --env-file ../.env -f docker-compose.yml -f docker-compose.ghcr.yml -f docker-compose.prod.yml pull
docker compose --env-file ../.env -f docker-compose.yml -f docker-compose.ghcr.yml -f docker-compose.prod.yml up -d
```

Find the SHA for any past deploy in the GitHub **Actions** tab under the `CI/CD` workflow's run history, or via `git log --oneline`. This only rolls back the application images — it does not touch the database. If the commit you're rolling back to had a different database schema, you would need a corresponding down-migration, which this project does not currently generate automatically; roll back application code only when the database schema is unaffected, or restore a database backup alongside it (see below).

## Where persistent data is stored

All of it lives in named Docker volumes, untouched by deploys:

| Volume | Contents |
|---|---|
| `amanah-drive_postgres_data` | The entire database: accounts, folders, file metadata, embeddings, chat history, activity log |
| `amanah-drive_api_storage` | The actual uploaded file bytes |
| `amanah-drive_api_logs` | Rolling structured application log files |
| `amanah-drive_caddy_data` | Let's Encrypt certificates and Caddy's own state |

List them on the VPS with `docker volume ls | grep amanah-drive`. A deploy (`up -d` after `pull`) never runs `docker compose down -v` or deletes volumes — only an explicit, deliberate `-v` flag would do that, and nothing in this deployment process ever passes it.

## How backups work

There is no automated backup configured yet — set this up before you rely on this being the only copy of anything real. The two things worth backing up:

**Database** (`pg_dump`, run from the VPS):

```bash
mkdir -p /opt/amanah-drive/backups
docker exec amanah-drive-postgres-1 pg_dump -U amanah_drive amanah_drive | gzip > /opt/amanah-drive/backups/db-$(date +%Y%m%d-%H%M%S).sql.gz
```

Wire this to a daily cron job (`crontab -e` on the VPS) and copy the resulting files off the VPS periodically (e.g. `scp` to your own machine, or a cheap object-storage bucket) — a backup that only lives on the same disk as the database doesn't protect against disk failure.

**Uploaded files**: back up the `api_storage` volume's contents:

```bash
docker run --rm -v amanah-drive_api_storage:/data -v /opt/amanah-drive/backups:/backup alpine \
  tar czf /backup/storage-$(date +%Y%m%d-%H%M%S).tar.gz -C /data .
```

**Restore** (verify this works before you need it for real):

```bash
# Database
gunzip -c /opt/amanah-drive/backups/db-<timestamp>.sql.gz | docker exec -i amanah-drive-postgres-1 psql -U amanah_drive amanah_drive

# Uploaded files
docker run --rm -v amanah-drive_api_storage:/data -v /opt/amanah-drive/backups:/backup alpine \
  sh -c "cd /data && tar xzf /backup/storage-<timestamp>.tar.gz"
```

This project intentionally does not build a more elaborate backup platform than this — see [ADR 0006](decisions/0006-deferring-horizontal-scaling-investment.md) for the same "don't build infrastructure the current scale doesn't need" reasoning applied here. A cron job and an off-box copy is the right amount of backup for a single-admin app.

## How to renew or change secrets

Rotating any secret (e.g. `AUTH_JWT_SIGNING_KEY`, `AI_SERVICE_TOKEN`, `HF_API_TOKEN`) follows the same pattern:

1. Generate the new value (see the generation commands in the table above).
2. Edit `/opt/amanah-drive/.env` on the VPS and replace the value.
3. Recreate the affected containers so they pick up the new environment:
   ```bash
   cd /opt/amanah-drive/infra
   docker compose --env-file ../.env -f docker-compose.yml -f docker-compose.ghcr.yml -f docker-compose.prod.yml up -d
   ```

Rotating `AUTH_JWT_SIGNING_KEY` invalidates every existing access token and refresh token — every logged-in session, including your own, will need to log in again. There's no user-facing impact beyond that (single-admin app).

`GITHUB_TOKEN` used by the workflows is generated automatically per-run by GitHub Actions and never needs manual rotation. `VPS_SSH_PRIVATE_KEY` should be rotated by generating a new keypair (setup step 2), adding the new public key to the VPS, updating the GitHub secret, and only then removing the old public key from `/root/.ssh/authorized_keys`.

## How to troubleshoot a failed deployment

**The GitHub Actions `CI/CD` workflow's `publish` or `deploy` job failed:**
- Check the **Actions** tab — the failing step's log tells you which stage broke (image build, GHCR push, or the SSH script).
- An SSH connection failure almost always means one of the four `VPS_*` secrets is wrong, or the deploy public key isn't in `/root/.ssh/authorized_keys` on the VPS — verify with `ssh -i amanah-drive-deploy-key root@148.230.104.231` from your own machine.
- A `docker compose pull` failure inside the SSH script usually means the GHCR packages are still private (see one-time setup step 7) or `AMANAH_DRIVE_VERSION`/`GHCR_OWNER` aren't set correctly in the VPS `.env`.

**The workflow succeeded but the site doesn't load:**
```bash
cd /opt/amanah-drive/infra
docker compose --env-file ../.env -f docker-compose.yml -f docker-compose.ghcr.yml -f docker-compose.prod.yml ps
```
Look for any container not `Up` or not `healthy`. Then check that specific container's logs (see "How to view logs" above). A `caddy` container that won't start is almost always a DNS problem (the domain doesn't yet resolve to this VPS, so the Let's Encrypt challenge fails) — recheck DNS propagation.

**A container is unhealthy specifically:**
```bash
docker inspect --format='{{json .State.Health}}' amanah-drive-api-1 | python3 -m json.tool
```
Shows the last few health-check attempts and their output — usually enough to tell you whether the process is up but not ready (e.g. still waiting on Postgres) versus not running at all.

**Something is fundamentally broken and you want to compare against a known-good state:** roll back to the last working `sha-<short>` tag (see "How to roll back" above) while you investigate — that restores service immediately without needing to fix the actual problem under time pressure.
