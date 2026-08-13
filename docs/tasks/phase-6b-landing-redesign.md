# Landing Page Redesign — Match Supplied Designs

## Task

Rebuild the portfolio landing page (`web/app/page.tsx`) to match the two design images attached to this task (desktop and mobile). This replaces the current landing page's visual design — the design images are the source of truth for layout, spacing, typography, and visual hierarchy from here on, not the current implementation.

Keep everything the design images don't dictate unchanged: the "Amanah Drive" CTA button must still navigate to `/login`, and the CV-derived content (name, skills, experience, education, certifications, achievements) already in the page must carry over — reflow it into the new layout, don't drop it, unless the design images clearly show a reduced/different set of sections, in which case follow the images.

### Profile photo

Use `amanah-drive/pics/self pic.png` as the profile photo shown in the design. Move/copy it into `web/public/` (e.g. `web/public/profile.png`) — don't reference it from outside the `web/` app — and use Next.js's `<Image>` component for it, not a raw `<img>`, so it's optimized. Crop/frame it as needed to match the design's photo treatment (circular, rounded-square, etc.) via CSS — don't ask for a re-exported image, work with what's provided.

### Responsive behavior

The two images are the desktop and mobile breakpoints respectively — implement the actual responsive breakpoint(s) between them using Tailwind (the desktop image's layout should apply at wider viewports, the mobile image's layout at narrow viewports), not just scale one down. Match spacing/proportions reasonably; pixel-perfect isn't required, but the structural layout (what's stacked vs. side-by-side, nav/header treatment, section order) should match each image at its corresponding breakpoint.

### Constraints

- Don't touch `/login`, `/drive`, `api/`, or `ai-service/`.
- Don't change the auth flow or any non-visual behavior of the landing page.
- Re-run the existing Playwright test that checks the CTA button and heading text on `/` still pass — update the test's selectors only if the redesign changes accessible names/roles, not its intent.
- `npm run build` must pass.
- Commits are fine for completed, coherent scope per `docs/AI_RULES.md`, don't push to `main`.
- Report per `docs/AI_RULES.md`'s completion report format: what changed, files changed, key decisions (especially how you resolved any ambiguity between the two design images and the existing CV content), anything incomplete, remaining risks.
