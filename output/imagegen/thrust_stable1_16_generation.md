# Staff thrust using stable1.png scale reference

Tool: built-in image_gen. No CLI or pixel edits outside image_gen.

Saved asset: Assets/levels/people/wukong/thrust_stable1_16_generated.png

References: stand.png for visual identity and stable1.png for the new requested scale. The first complete stable1.png sprite measures 263px vertically at alpha >= 128. The selected pass edits a generated thrust sheet with both original references supplied.

Actual output: 1254x1254 RGBA. The selected sheet's first complete sprite measures 269px at alpha >= 128, 6px taller than the 263px reference, so strict scale matching was not achieved. The copied workspace file was verified against the source with SHA-256.

The selected image has 16 poses in four rows/four columns, including pullback, forward drive, thrust with connected pale wind streaks, extension and recovery. Its background has alpha transparency. Image-generation variations remain in pose, grip visibility and registration; hands are not separately readable in several extended poses. Precise size matching, identical geometry and zero-jitter playback are not certified. Treat this as a generated draft rather than a calibrated production atlas. Existing sprites, game scripts and scene references were not replaced. No Unity playback verification was performed.

## Exact prompt used for selected pass

Edit the attached 16-frame thrust sprite sheet (Image1) with a targeted production cleanup. Image2 is the original character design reference; Image3 is the user's current size reference stable1.png.
Keep a single transparent 4x4 sheet with exactly sixteen complete sprites. Preserve the existing chronological thrust sequence, overall scale, palette, 2D style, face and clothing. Match the stable1.png first sprite's canonical character scale, 263px hair-to-sole height. Keep the same character-part sizes in every frame and stable foot anchors. Do not make the character smaller to fit weapon effects.

REQUIRED CORRECTIONS:
- TWO HANDS visibly and firmly grip the staff in EVERY frame, including the fully extended poses. Particularly in frames8-12, separate the hand silhouettes along the shaft so neither hand disappears behind the other: rear hand closer to the torso, front hand farther right. Both hands must be connected to their own plausible arm through elbows and wrists. Closed fingers encircle the shaft. No missing hands, floating palms or merged fists.
- SAME rigid staff in all frames: red shaft, gold end caps, invariant length and thickness. Preserve both ends, even during extension. Translate/rotate it through the two-hand attack motion; never shorten it or turn it into a projectile.
- Replace the large glowing thrust effects with small elegant narrow ivory wind streaks tightly attached to the forward part of the shaft and touching its gold tip. The effect trails along the actual weapon path in frames7-10, fades in11-12, and is absent in13-16. It never hides hands, detaches from the staff, or crosses a cell boundary. No floating arcs, yellow particle clouds, red fringes or loose noise.
- Exact regular 4-column 4-row registration. Reserve clean transparent gutters between all frames. Every foot, staff end and effect must fit inside its own equal-sized cell with at least a small transparent margin. Keep the foot baseline consistent relative to each cell. Avoid cropping the bottom row.
- Frame16 must closely match frame1's neutral combat-ready pose: same grounded feet, same waist-height approximately horizontal staff in both hands. Recovery13-15 smoothly approaches this ready pose.

Sequence stays: frames1-3 backward pullback/torso wind-up;4-6 transition and acceleration;7-10 sharp powerful RIGHTWARD horizontal thrust;11-12 maximum forward extension;13-16 controlled recovery. Stable root, natural weight shift and subtle shoulder/elbow/wrist/cloth motion. Consistent right-facing side-view game camera. Preserve Image2's exact original monkey identity; no off-model body/face changes or scale shifts.
Return only the ONE clean 16-frame RGBA sprite sheet on genuine transparent alpha, with no background, shadow, checkerboard artwork, text, labels, numbers, cell borders, watermark or other objects.
