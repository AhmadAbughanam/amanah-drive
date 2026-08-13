# Phase 6 — Web: Portfolio Landing, Auth Gate, Drive Shell

## Task

Build out `web/` (currently a default Next.js scaffold — App Router, React 19, Tailwind 4, nothing custom yet). Read `README.md`, `docs/architecture.md`, and `docs/api-reference.md` first — the API surface you're integrating against is fully documented in `docs/api-reference.md`.

This app now serves two purposes on one Next.js site:
1. A **personal portfolio landing page** at `/` — this is the human developer's CV/portfolio, with Amanah Drive as the flagship project.
2. The **Amanah Drive product** behind a login gate, reached via a CTA button on the landing page.

### 1. Portfolio landing page (`/`)

The human developer will attach their actual CV in the next message — use its real content (name, title, summary, skills, experience, projects) for the copy. Do not invent facts, credentials, or experience that isn't in the attached CV. If something needed for a polished page isn't in the CV (e.g. a profile photo, specific contact links), leave a clearly-marked placeholder rather than fabricating it.

Build a clean, modern single-page portfolio: hero/intro, skills, experience/projects section (with Amanah Drive featured prominently, linking to its GitHub repo if useful), and a clear call-to-action button — **"Amanah Drive"** or similar — that navigates to `/login`. Keep it Tailwind-only, no heavy UI kit dependency, responsive, matching the engineering-quality bar the rest of this project has been held to.

### 2. Auth flow

- `/login` — email/password form calling the API's `POST /auth/login` (see `docs/api-reference.md`). On success, the API returns a JSON access token and sets an HttpOnly refresh cookie (`credentials: "include"` is required on all API calls for the refresh cookie to work). Store the access token in memory (e.g. React context), not `localStorage`, since it's short-lived and refresh is cookie-driven.
- On 401 from an API call (access token expired), call `POST /auth/refresh` (cookie-based, no body needed) to get a new access token and retry the original request once. If refresh also fails, treat the user as logged out and redirect to `/login`.
- A logout action calling `POST /auth/logout` and clearing local auth state.
- Route protection: everything under `/drive` (see below) requires a valid session. Redirect to `/login` if there's no valid access token and refresh also fails. Use Next.js middleware or a client-side auth guard — whichever fits the App Router auth state approach you set up; either is fine, but be consistent.
- Since there's only ever one admin account, don't build a registration UI — `/auth/register` is a one-time bootstrap step done outside the browser (e.g. via curl/http file), not a page.

### 3. Drive shell (`/drive`, post-login)

This is "entering the project" after login. Scope for this task is the **file management shell only** — semantic search and AI chat panels are a separate follow-up task, not in scope here.

Build against the existing `/drive/*` endpoints (`docs/api-reference.md`):
- Folder browser: navigate folders (breadcrumb or tree), list contents with pagination (`page`/`pageSize` already supported server-side).
- Create folder, rename, delete (with a confirmation for delete since it cascades).
- File upload (multipart, respecting the API's MIME allow-list and size limit — surface the API's error responses clearly, e.g. "file type not supported" / "file too large").
- File download, rename, move (to another folder), delete.
- Loading and error states for all of the above — don't leave the user looking at a blank screen or a raw error on failure.

### 4. Config

- Add `NEXT_PUBLIC_API_BASE_URL` (the API's browser-reachable URL, e.g. `http://localhost:8080` — the browser talks to the API directly, not through the Docker-internal `api` hostname). Add it to `.env.example` and wire it through `infra/docker-compose.yml`'s `web` service environment, and through `web/`'s own build (Next.js public env vars are baked in at build time — document that if `NEXT_PUBLIC_API_BASE_URL` differs per environment, the image needs rebuilding, or switch to a runtime-configurable approach if that's a real concern for this deployment — use your judgment, don't over-engineer this for a single-VPS single-user app).

### 5. Tests

- Playwright end-to-end tests (per `README.md`'s stated stack) covering: portfolio page renders and the CTA navigates to `/login`; login with valid credentials reaches `/drive`; login with invalid credentials shows an error and stays on `/login`; an unauthenticated visit to `/drive` redirects to `/login`.
- Run against a real running API + Postgres (docker compose) or a mocked API layer — use your judgment on what's practical to wire into CI later; note the choice and tradeoffs in the completion report.

## Constraints

- Don't touch `api/` or `ai-service/` — this is a `web/`-only task, aside from the `.env.example`/`docker-compose.yml` config wiring in section 4.
- Don't invent portfolio content beyond what's in the attached CV.
- Don't build the search/chat UI yet — that's the next task.
- Commits are fine for completed, coherent scope per `docs/AI_RULES.md`, don't push to `main`.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions, anything incomplete, remaining risks.
