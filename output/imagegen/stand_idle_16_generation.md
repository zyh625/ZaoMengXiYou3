# Wukong idle animation generation

Tool: built-in image_gen (no CLI).

Asset: Assets/levels/people/wukong/stand_idle_16_generated.png

References: stand.png for character identity; attack2.png first neutral frame for the 271px reference height, read from its existing Unity sprite slice. The selected generation uses stand.png as its image reference to reduce identity mixing.

## Inspection

- Actual PNG: 1254 x 1254, RGBA with transparent background.
- 16 full-body sprites in 4 rows and 4 columns; reading order left-to-right, top-to-bottom.
- Visible closed-eye pose: frame 8.
- At alpha >= 128, frame content height is 282-283px, versus the 271px reference target.
- The image dimensions are not divisible by 4; a strict integer-pixel equal-cell grid was not achieved by generation.
- Subtle per-frame outline/pose differences remain. Smooth looping and zero jitter have not been verified in Unity.
- This is a generated draft and does NOT yet satisfy the requested exact scale, identical per-frame geometry, and strict pixel-grid alignment.
- Existing textures, scripts, scene animation arrays and importer settings were not replaced.

## Exact prompt used for the selected generation

Use case: identity-preserve.
Create one clean transparent 16-frame idle sprite animation sheet from the attached ORIGINAL stand.png.
The sheet has EXACTLY 4 rows and 4 columns of evenly spaced equal square frame cells. Full canvas requested: 1536x1536, 384x384 per cell. Each whole sprite comfortably fits its cell with large transparent gutters, at least 40px padding on all four sides including below the last row. NO CROPPING of feet or staff.

IDENTITY LOCK: Reproduce the attached original character exactly as a single flat cutout being animated. Use its original slender proportions, angular silhouette, exact face and brown swept hairstyle, fine muzzle lines, original narrow red/yellow eyes, gold headband and square spiral ornament, yellow shirt, gray diagonal sash, black wrist wraps, cyan waistband, spotted ochre skirt, red pants, dark boots. Preserve the original single resting diagonal red-and-gold staff and original hand/arm placement. Do not improve, redraw, thicken, round, or stylize the design. No new outlines or red edge noise. No alterations to face, hand shapes, eye colors, clothing or boot shape from one frame to another.
SCALE: uniform enlargement of original to 271 pixels hair-top to boot-bottom height in every frame, matching the user's separate attack animation reference. Exactly same body height, limb/head proportions, staff length and width in all 16 frames. No perspective changes or scale variations. Fixed camera.
ALIGNMENT: fixed foot/ground contact point at cell center x and at 82% of cell height, identical cell-relative registration for all sixteen frames. Feet DO NOT move. Do not shift the whole sprite across the canvas. Gentle swaying is confined to upper body rotation around planted feet, only about +/-0.4 degrees, with one tiny cycle of chest/shoulder breathing. Keep body height stable.

ANIMATION: one seamless relaxed idle loop, no attack, no walking, no weapon swing. Motion differences between adjacent frames should be TINY and coherent. Frame1 neutral, frames2-5 gently ease to right, frames6-9 gently ease back, frames10-13 gently ease to left, frames14-16 gently ease back to seamlessly join frame1.
BLINK ONLY ONCE NEAR THE MIDDLE. Precisely follow this eye-state grid (do not render these words):
TOP ROW, frames1 2 3 4: OPEN, OPEN, OPEN, OPEN.
SECOND ROW, frames5 6 7 8: OPEN, OPEN, HALF-CLOSED, CLOSED.
THIRD ROW, frames9 10 11 12: HALF-OPEN, OPEN, OPEN, OPEN.
BOTTOM ROW, frames13 14 15 16: OPEN, OPEN, OPEN, OPEN.
Closed eyelid is a fine dark line in the original eye location, not a new red mouth or altered face. Do not add a second blink.

TRUE TRANSPARENT RGBA background with clean transparent gutters. Absolutely no backdrop, checkerboard pattern, red halos, loose pixels, shadows, labels, numbers, watermark, divider lines, motion trails, or floor. Only the sixteen complete identical-character sprites. All shoes and both ends of the staff visible in every frame.

