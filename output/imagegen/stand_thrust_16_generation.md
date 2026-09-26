# Wukong 16-frame staff thrust

Tool: built-in image_gen. No CLI or manual pixel editing.

Selected asset: Assets/levels/people/wukong/stand_thrust_16_generated.png

References: stand.png for character identity; attack2.png for the requested scale. The selected pass corrects an earlier generated sheet using stand.png as the character reference.

## Inspection

The selected PNG contains sixteen full-body right-facing poses in four rows and four columns, including anticipation, forward drive, a horizontal thrust with attached wind streaks, extended poses, and recovery. Background alpha transparency is present. Both hands are visible on the staff in the selected version.

The generator did not strictly enforce the requested pixel dimensions and registration. Character pose/proportion and grip-spacing differences remain, and the final ready pose is not pixel-identical to the first. Exact matching to the attack2.png scale, uniform frame rectangles, fixed foot anchors, and playback continuity are not certified. This is a generated animation draft requiring sprite cleanup/alignment before production use. Existing assets, scene animation references and source code were not replaced.

## Exact prompt for selected pass

Use case: precise-object-edit / identity-preserve.
Image 1 is an existing 16-frame horizontal staff-thrust sprite sheet to CORRECT. Image 2 is the original stand.png character identity reference.
Return ONE improved transparent RGBA sheet of exactly 16 frames in a four-column by four-row regular grid. Output canvas exactly 1272x1236 pixels, equal 318x309 cells. Maintain the overall attack sequence from Image1: frames1-3 pullback, 4-6 forward drive, 7-10 quick straight rightward thrust with a small attached wind streak, 11-12 full extension, 13-16 controlled return.
The height of the monkey character must be 271 pixels in each cell, matching the user's separate attack2.png reference, without changing the proportions. Local boot-sole baseline y=294, hair-top y=23. Fixed registration of feet/root across all cells and consistent body-part sizes. Keep staff, effect and all feet fully within their cell. No clipping. Same scale in every pose, no resizing due to weapon bounds.

Make these corrections:
1. Preserve the exact slender angular original monkey design from Image2, including original face geometry, brown swept hair, gold squared-spiral headband, cream muzzle/face, narrow red-and-yellow eye, yellow sleeveless torso, gray sash, black wrist wraps, cyan belt, spotted ochre waistcloth, red legs, dark shoes. No new design and no changes in head size between frames. Remove stray red halo pixels; keep clean colored line-art edges.
2. In EVERY frame, clearly show TWO anatomically connected arms and TWO hands firmly holding the SAME red staff with gold ends. The hands wrap the shaft, separated by a consistent comfortable grip distance. Keep staff length/thickness identical throughout. No staff extension/shortening, no hidden missing hand, no floating palm. Use elbows/shoulders and torso turn to drive the forward thrust. The shaft remains approximately horizontal at waist level throughout, no overhead swing.
3. Keep the wind effect narrow and elegant, only hugging and touching the moving staff's forward shaft and tip in frames7-10. Tiny pale-ivory/translucent-gold speed streaks, diminishing in frames11-12, absent in frames13-16. Effects must not obscure either hand, must not float away, must not cross into adjacent cells, must not look like a separate projectile.
4. Frame16 returns to the SAME waist-height neutral ready pose and foot/hand placement as frame1. Frames13-15 interpolate smoothly back to it. Correct any sudden stance or scale jump near the end.

Keep the chronological attack readable: slow coiled anticipation, fast powerful FORWARD STAB, full extension beat, balanced recovery. Secondary motion only from plausible articulation of torso, shoulders, elbows, wrists and waistcloth, no stretchy body parts. All sprites face RIGHT from a consistent side-view game camera.
No extra characters, no labels, no text, no grid lines, no watermark, no floor shadow, no background; true transparent RGBA with clean gutters. Exactly sixteen complete coherent sprites.

