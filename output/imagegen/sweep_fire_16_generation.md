# Horizontal fire staff sweep: 16 frames

Generation: built-in image_gen, with stand.png as character identity reference and stable1.png as scale reference. The pasted brief requests a forward semicircular horizontal sweep, not a thrust or full spinning attack.

## Deliverables

- Assets/levels/people/wukong/sweep_fire_16_generated.png: transparent RGBA sprite sheet, 1448x1086, sixteen chronological poses in four rows and four columns.
- Assets/levels/people/wukong/sweep_fire_16_generated.png.meta: Unity Multiple-sprite importer settings with SweepFire_01 through SweepFire_16, individual boot-centered pivots, Full Rect meshes, no mipmaps, and uncompressed texture import.
- output/imagegen/sweep_fire_16_slices.json: measured slice rectangles, pivots, file IDs and size calibration.

## Import and verification

Slices follow the actual transparent gaps. Several column boundaries were adjusted slightly so the stronger flame pixels remain in their own frames; do not overwrite these custom rectangles with an automatic equal-grid slice. No pixels at alpha >= 128 intersect the saved slice boundaries. Very faint edge noise below this threshold can remain. All sixteen rectangles/pivots lie within the source image. Names and internal ID mappings were checked. The copied image matches the generator output SHA-256; raster pixels were not changed outside image_gen.

The reference first sprite measures 263px high at 100 PPU, or 2.63 Unity units. The generated first sprite is 222px high. Setting PPU to 84.41064639 makes its first-frame displayed height 2.63 units at the same Transform scale. This is a Unity display-scale calibration, not a claim that the raw image pixel heights match. Per-frame poses still have drawing/proportion differences; the importer does not eliminate those differences. Root pivots use measured boot positions to reduce positioning jumps.

The image depicts preparation, a forward waist-height fiery air-cutting crescent, follow-through and recovery. It still has some hand occlusion, small pose inconsistencies and a few residual effects during late recovery. Frame16 is not pixel-identical to frame1. Exact anatomy consistency and a seamless loop are not certified. Unity import/playback was not run. Existing source sprites, gameplay scripts and scene animation arrays were not changed or replaced.

## Exact prompt for selected image

Re-layout and clean up Image1, the existing sixteen-frame FIRE STAFF SWEEP sprite sheet. Image2 is the exact original character identity; Image3 stable1.png is the character size reference.

Keep EXACTLY the same SIXTEEN chronological action stages and preserve the good wide forward semicircular horizontal sweep, the clean 2D character design and outfit, and the integrated ivory/orange fire-air crescent attached to the staff. Do NOT turn it into a thrust or a full spin/tornado.

MAIN CORRECTION: MAKE THE SIXTEEN FRAMES SEPARATE AND FULLY CONTAINED.
Create a regular four-column/four-row grid with IDENTICAL spacious rectangular cells. Each entire pose INCLUDING all fire/sparks/weapon must fit in the INNER 70% of its cell, surrounded by broad EMPTY TRANSPARENT SPACE on all sides. Enlarge the sheet canvas if necessary to add padding while retaining the reference character scale. Leave at least 15% clear space at every cell edge, and use the SAME generous padding at the outer sheet boundaries. Nothing should touch or cross a cell boundary. No VFX from one pose may connect to the next. Character feet and top of hair must be fully visible in every cell. No cropped staff ends. All sixteen cells must be spaced absolutely regularly so equal-grid slicing works.

Keep the same full-sized character in all frames, stable root/foot registration, constant physical staff length and thickness, clear two-hand contact and plausible arms. Preserve Image2's original face, hair, gold headband, yellow clothes, gray sash, cyan belt, spotted waistcloth, red trousers and dark boots; no body-size or design drift. Staff ends remain gold and shaft red. Fixed side-view game camera.
The sweep is a broad WAIST-HEIGHT FRONT HALF-CIRCLE, visibly changing staff angle across main frames, body torso/shoulder rotation supporting the swing without completing a full turn. Natural anticipation and follow-through. Two hands grip the solid staff throughout; keep hands legible.

Timing stays:
1-2 neutral horizontal staff at waist, no effects.
3-4 load backward and twist torso, no effects.
5-7 accelerate across the front, attached air trail starts small.
8-11 full semicircular sweep with strongest attached ivory air crescent and orange/gold flame ribbons; shrink only excessive effect spread to fit the safe cell interior. Keep face/arms/weapon readable.
12-14 follow-through and gradually fading air/flames.
15-16 ready stance again, absolutely no effects. Make frame16 match frame1's pose, staff placement, foot registration and size.

Only the clean sixteen complete sprites on TRUE transparent RGBA, no scenery, opaque background, checkerboard artwork, shadows, labels, numbers, borders, text or watermark. No blurry silhouette, stray red noise or detached effects. The broad transparent gutters between all frames are essential.

