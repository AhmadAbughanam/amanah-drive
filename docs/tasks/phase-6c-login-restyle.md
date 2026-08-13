# Login Page Restyle — Match Landing Page Design System

## Why

`web/app/login/page.tsx` was intentionally left untouched during the landing page redesign, so it still uses the original teal-accent/slate/generic-rounded-card styling from before that redesign. The landing page (`web/app/page.tsx`) now has a distinct monochrome, serif-headline, editorial design system (black `#080808`/off-white `#f7f7f5`, serif display type, thin-rule dividers, uppercase label text, the rounded-frame "device" container). Landing on `/login` after clicking the "Amanah Drive" CTA currently feels like arriving at a different site.

## Task

Restyle `web/app/login/page.tsx` to visually match the design system established in `web/app/page.tsx` — same color palette, typography (serif headline via the same font classes), spacing/label conventions (uppercase tracked labels, thin-rule dividers), and ideally the same rounded-frame container treatment used on the landing page, adapted to a centered single-card login layout. Reuse whatever shared Tailwind classes/patterns you can pull out or copy from `page.tsx` rather than inventing a third style — if there's an obvious shared piece (e.g. the frame/border treatment), factor it out, but don't over-engineer a full design-token system for two pages.

This is a **visual-only** change:
- Do not change the form's behavior: `signIn`, error handling, redirect to `/drive` on success, the "Invalid email or password." message on 401 — all of that stays exactly as-is.
- Do not change `auth-provider.tsx` or `lib/api.ts`.
- Keep the "back to portfolio" link (currently the "Ahmad Abughanam" link back to `/`).
- Keep it accessible: labels stay associated with inputs, focus states stay visible, error text keeps `role="alert"`.

## Verification

- Actually render `/login` at both desktop and mobile viewport widths and visually confirm it now reads as the same design system as `/`, not a different site. Don't rely on `npm run build` alone as proof — the mobile hero bug from the earlier redesign task wasn't caught by build/tests either.
- Existing Playwright tests (`valid login reaches drive`, `invalid login shows an error and stays on login`) must still pass — they assert on `getByLabel`, button role/name, and heading role/name, so keep those accessible names stable unless you deliberately update the tests to match an intentional label change (don't rename anything without a reason).

## Constraints

- Only touch `web/app/login/page.tsx` (and `web/app/globals.css` if a small shared style addition is genuinely useful).
- Don't touch `/`, `/drive`, `api/`, or `ai-service/`.
- Commits are fine for completed scope per `docs/AI_RULES.md`, don't push to `main`.
- Report per `docs/AI_RULES.md`'s completion report format, including how you visually verified both viewport widths.
