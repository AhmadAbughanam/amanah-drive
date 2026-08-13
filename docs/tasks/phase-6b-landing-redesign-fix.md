# Landing Redesign Fix — Mobile Hero Overlap

## Bug

On mobile viewport widths (verified at 390px), the hero portrait image (`web/app/page.tsx`, the `<Image src="/profile.png" .../>` in the hero `<section>`) overlaps the intro paragraph and "Explore Work" link. The image is `absolute bottom-0 right-[-42px]` sized `h-[320px] w-[310px]` on mobile, and the text block above it is only `max-w-[290px]` — at a 390px viewport with `px-9` (36px) side padding, the available content width is ~318px, so the 310px-wide image (offset -42px right) extends left far enough to sit on top of the paragraph text. The word "intelligent" is legible only around the image, and the paragraph reads as broken. Confirmed visually via screenshot at 390×844.

## Fix

Adjust the mobile hero layout so the portrait image doesn't overlap the text block — either:
- give the image a fixed position/size that sits below the text in normal document flow on mobile (not `absolute`), matching how the rest of the page stacks content on mobile, or
- keep it absolutely positioned but reduce its size/reposition it so it doesn't intersect the text block's bounding box (e.g. constrain the text block's `max-w` further and/or move the image down so it starts below the paragraph and CTA, only overlapping empty space).

Use your judgment on which matches the original mobile design image most closely — the constraint is just that no text may be visually obscured by the image at any viewport width from ~360px up. Verify by actually rendering the page at a mobile viewport (e.g. Playwright or a browser dev tools device emulation) and confirming the paragraph and "Explore Work" link are fully legible with no overlap — don't rely on `npm run build` succeeding as proof, it doesn't catch this.

## Constraints

- Only touch the hero section's layout/positioning in `web/app/page.tsx` (and `globals.css` if a fix needs a small addition there) — don't otherwise change content, other sections, or non-visual behavior.
- Desktop layout must remain unaffected (re-check both breakpoints after the fix).
- Existing Playwright tests must still pass.
- Commits are fine for completed scope per `docs/AI_RULES.md`, don't push to `main`.
- Report per `docs/AI_RULES.md`'s completion report format, and include how you verified the overlap is actually gone (e.g. describe or attach a screenshot at mobile width).
