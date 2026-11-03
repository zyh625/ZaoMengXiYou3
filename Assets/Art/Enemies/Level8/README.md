# Level8 万象星殿：小怪形象与招式设计

参考 Boss：九曜玄凰。生成日期：2026-11-02。

本次交付三张独立角色形象图和招式方案。尚未添加动画、碰撞体、控制器、预制体或场景敌人。图像使用内置 imagegen 生成，再整理透明边缘、尺寸与留白。

## 文件

| 小怪 | 定位 | 文件 |
| --- | --- | --- |
| 剪星羽卫 | 近身夹击与节奏变化 | StarShearGuard/Stand.png |
| 织宿星蛛 | 地面路线控制与星丝陷阱 | ConstellationWeaver/Stand.png |
| 鸣曜星鸮 | 空中音波与高低区域压制 | ResonantStarOwl/Stand.png |

三张均为 1024×1024 RGBA 透明 PNG，整体朝左，四侧至少有 64px 透明留白。无背景、地面、投影、文字或烘焙攻击特效。统一画布尺寸方便形象预览，不代表三种敌人在游戏里使用同一身高。

## 设计方向

沿用银白羽片、靛蓝层次、青色四角星和红色敌对眼睛，以双足剪卫、多足星蛛、悬空星鸮形成三种清楚轮廓。招式重点是剪击节奏、可拆星锚和分层音波，区别于现有的盾防、飞轮隐身、灯笼射线与标记、锁链、重锤和战旗召唤。

以下招式是后续制作建议；具体伤害、距离、冷却和前摇需在游戏里调试。

## 剪星羽卫

鸟首双足、银白羽领、靛蓝羽甲；双前臂是可开合的羽剪，青色星形铰点突出近身剪击的识别点。

| 招式 | 行为 | 玩家应对 |
| --- | --- | --- |
| 错拍合剪 | 张开双刃后延迟合拢，再接一次低位剪击，两次判定之间短暂停顿。 | 观察刃口高度，在收招时绕背或后撤。 |
| 挑星上剪 | 昂首并举刃成 V 形，夹击前上方低空目标，不覆盖身后。 | 昂首、举刃均为预警，退到侧后方可反击。 |
| 折羽撤剪 | 短距离后跃，落地后对追近的目标补一次短剪；不隐身，不获得无敌。 | 停顿后再接近，或远程打断其落地准备动作。 |

## 织宿星蛛

低伏多足的星蛛，羽片银甲覆盖关节；腹部的青色星点与银线组成星图，尾部发丝器对应星丝陷阱。

| 招式 | 行为 | 玩家应对 |
| --- | --- | --- |
| 双锚绊星 | 在前方放置两个星锚，连接一条低矮星丝；每只星蛛最多保持一条，且有持续时间限制。 | 可以跳过，也能攻击任一星锚破坏整条星丝。 |
| 三星缚径 | 依次点亮三个地面星结，先预警，再逐个释放小范围短暂减速脉冲，不追踪玩家。 | 沿未点亮一侧绕行，或起跳离开地面；不施加长时间定身。 |
| 牵丝换位 | 沿自己现有的星丝收丝移动到另一端，轨迹全程可见，不附带爆炸。 | 拆掉星锚即可阻止换位；收丝结束后留出反击停顿。 |

## 鸣曜星鸮

圆身短尾、银白面盘与靛蓝短翼；胸前四角星晶体是发声器官，与玄凰的长颈和长尾轮廓区分。

| 招式 | 行为 | 玩家应对 |
| --- | --- | --- |
| 递进鸣环 | 喉晶闪烁后分三拍放出扩张空心音环，伤害只在环缘，内侧留安全区。 | 可拉开距离等环缘经过，也可在前摇后贴近内侧。 |
| 高低错鸣 | 先后发出低位与高位短音波，顺序由起手的抬头或俯首动作提示。 | 按照提示先跳再落地，或先地面移动再短跳。 |
| 止羽鸣带 | 在前方半空形成一段短暂共振带，只限制局部跳跃路线，地面仍可通过。 | 从地面通过或等待消退；施法暴露胸前星晶，可打断。 |

## 组合与可读性

- 羽卫在前排施加近身压力，星蛛控制地面路径，星鸮改变空中节奏；建议先单独教学，再两两组合。
- 地面绊索与空中鸣带不要无间隙地覆盖同一段必经路线，始终保留可走、可跳或可破坏的通路。
- 前摇突出对应器官：羽剪铰点、蛛腹星点、星鸮喉晶。普通待机只保留材质上的小范围发光。
- 后续音环、星网和刃光独立制作，不烘焙进本体动画。

## 图像来源与最终提示词

方式：内置 imagegen；每个角色独立生成一张图，未使用 CLI/API 回退。
配色与阵营参考：Assets/Art/Bosses/level8.png。
描边与分色参考：Assets/Art/Enemies/Level5/RedBoom/Stand.png。
Boss 简介图用于确定低空、跳跃与闪避的关卡方向。
输出保留角色主体，清理零碎透明像素，以同一留白标准整理成 1024×1024 PNG。

### 剪星羽卫

```text
Use case: stylized-concept.
Asset type: a single production 2D side-scrolling Chinese fantasy game ENEMY character concept / ready-pose sprite, on a true transparent background.
Reference image 1 is the level boss Jiuyao Xuanhuang, for FACTION PALETTE AND MATERIAL MOTIFS ONLY: silver-white plumage/armor, deep navy and cobalt, small luminous cyan four-point stars, hostile red eyes. Do not reproduce the giant boss.
Reference image 2 is ONLY for the project's existing clean 2D enemy-sprite rendering: bold dark outlines, crisp large cel-shaded shapes, restrained painted highlights, readable fantasy details. Do not copy that enemy's hood, mask, red/gold palette, lantern, staff or equipment.
Design a NEW small elite avian foot-soldier called the Star-Shear Guard. A lean compact BIPEDAL bird-headed warrior, not the giant phoenix boss. Short hooked dark beak, two fierce red eyes, a short swept-back ivory feather crest, layered silver-white feather collar around the neck, dark indigo feather-lamellar chest armor, cyan four-point-star inlay at the sternum, short navy feather waist panels, and two sturdy bird feet with three silver claws each. Its special readable identity is a PAIR of LONG SILVER SHEARING FOREARM BLADES, one attached to each forearm like articulated folding feather shears, dark-blue bevels and small cyan star-shaped mechanical pivots. Exactly two arms and two legs. Hands/forearms remain anatomically readable. The two blades are held OPEN as a wide V in front of its chest/waist, in a poised guarded stance; the blades must not merge or be cropped. It can open and close the paired blades for high/low scissor attacks. This is an agile infantry silhouette with broad blade forearms, slim torso and short feather hip cape; no full large wings, no long phoenix tail, no shield, sword, hammer, staff, bow, banner, chain or thrown wheel. Make it visibly subordinate and simpler than the boss, appropriate as a repeatable regular enemy.
COMPOSITION: ONE complete enemy, single neutral combat-ready pose, facing IMAGE-LEFT in a side-on / shallow three-quarter view suitable for a 2D side-scroller. Not front-facing and not facing right. Square 1024 x 1024 target canvas. Keep the whole body, all claws, appendages and weapons fully visible, comfortably centered with at least 8% empty transparent margin on every side. No pose sheet, no multiple views, no duplicate creature. Consistent clean Chinese fantasy cartoon art, thick confident near-black contour, 2-3 clear cel-shading tones per material, restrained small emissive cyan cores. No photorealism, no 3D render, no painterly background.
BACKGROUND: real transparent alpha outside the entire character and within gaps between appendages. No background color or scenery, no ground texture, no pedestal, no floor, no cast shadow, no large glow cloud, no particles, no motion slash, no detached effect, no labels, no text, no numbers, no logo, no watermark. The image is a clean separated enemy body asset for future animation.
```

### 织宿星蛛

```text
Use case: stylized-concept.
Asset type: a single production 2D side-scrolling Chinese fantasy game ENEMY character concept / ready-pose sprite, on a true transparent background.
Reference image 1 is the level boss Jiuyao Xuanhuang, for FACTION PALETTE AND MATERIAL MOTIFS ONLY: silver-white plumage/armor, deep navy and cobalt, small luminous cyan four-point stars, hostile red eyes. Do not reproduce the giant boss.
Reference image 2 is ONLY for the project's existing clean 2D enemy-sprite rendering: bold dark outlines, crisp large cel-shaded shapes, restrained painted highlights, readable fantasy details. Do not copy that enemy's hood, mask, red/gold palette, lantern, staff or equipment.
Design a NEW small constellation-weaver SPIDER demon, a low WIDE ground skirmisher, not a humanoid and not a phoenix. Exactly EIGHT clearly separated articulated legs, four on each side, with dark indigo joints and narrow silver-white tapered feather-like armor segments. A small silver-white armored face at the LEFT with two narrow hostile red eyes, compact closed black mandibles. A rounded dark-indigo abdomen toward the RIGHT with a small crown/ridge of ivory feather-like plates. On the abdomen, four cyan four-point-star studs are linked by very fine engraved silver constellation lines, all physically on its shell, no floating star map. Two tiny cyan silk-spool organs tucked on the rear abdomen visually explain its thread-trap attacks. Its body, legs and shell match the boss's navy, cobalt, silver-white and cyan star palette. Ready stance with a low raised body and evenly planted pointed feet; all eight legs readable and separated against transparency. No displayed web, no silk line stretched across the image, no attack effect, no external trap props, no egg swarm, no skulls, no humanoid torso, no shield/hammer/banner/bow. The spider's own constellation carapace and silk-spool anatomy are the unique silhouette.
COMPOSITION: ONE complete enemy, single neutral combat-ready pose, facing IMAGE-LEFT in a side-on / shallow three-quarter view suitable for a 2D side-scroller. Not front-facing and not facing right. Square 1024 x 1024 target canvas. Keep the whole body, all claws, appendages and weapons fully visible, comfortably centered with at least 8% empty transparent margin on every side. No pose sheet, no multiple views, no duplicate creature. Consistent clean Chinese fantasy cartoon art, thick confident near-black contour, 2-3 clear cel-shading tones per material, restrained small emissive cyan cores. No photorealism, no 3D render, no painterly background.
BACKGROUND: real transparent alpha outside the entire character and within gaps between appendages. No background color or scenery, no ground texture, no pedestal, no floor, no cast shadow, no large glow cloud, no particles, no motion slash, no detached effect, no labels, no text, no numbers, no logo, no watermark. The image is a clean separated enemy body asset for future animation.
```

### 鸣曜星鸮

```text
Use case: stylized-concept.
Asset type: a single production 2D side-scrolling Chinese fantasy game ENEMY character concept / ready-pose sprite, on a true transparent background.
Reference image 1 is the level boss Jiuyao Xuanhuang, for FACTION PALETTE AND MATERIAL MOTIFS ONLY: silver-white plumage/armor, deep navy and cobalt, small luminous cyan four-point stars, hostile red eyes. Do not reproduce the giant boss.
Reference image 2 is ONLY for the project's existing clean 2D enemy-sprite rendering: bold dark outlines, crisp large cel-shaded shapes, restrained painted highlights, readable fantasy details. Do not copy that enemy's hood, mask, red/gold palette, lantern, staff or equipment.
Design a NEW small floating RESONANCE STAR OWL demon, a compact round hovering owl spirit. It is obviously an owl, NOT a mini phoenix, not a humanoid and not a spider. A broad silver-white heart-shaped owl facial disc around two sharp red eyes, short dark beak, two short ivory ear tufts, a round deep-indigo feathered torso, short layered tail feathers. Its throat/chest bears ONE prominent faceted cyan FOUR-POINT-STAR resonance crystal surrounded by a collar of ivory feather plates, visibly an organ for producing sound pulses. Two broad SHORT wings are half-open in a balanced hovering ready pose, layered navy/cobalt feathers with small cyan star-eye markings near the outer panels, inspired by the boss without copying its long wings and enormous tail. Two small tucked talons beneath its body. The wings should have clean, readable overlapping feather groups, not hundreds of tiny strands. Full body visible with no ground or shadow. Distinct round caster silhouette, clearly smaller and simpler than the boss. No held weapon, staff, lantern, bell prop, floating accessory, orb ring, summoned minion, enormous tail fan, environment or sound-wave effects baked into the image.
COMPOSITION: ONE complete enemy, single neutral combat-ready pose, facing IMAGE-LEFT in a side-on / shallow three-quarter view suitable for a 2D side-scroller. Not front-facing and not facing right. Square 1024 x 1024 target canvas. Keep the whole body, all claws, appendages and weapons fully visible, comfortably centered with at least 8% empty transparent margin on every side. No pose sheet, no multiple views, no duplicate creature. Consistent clean Chinese fantasy cartoon art, thick confident near-black contour, 2-3 clear cel-shading tones per material, restrained small emissive cyan cores. No photorealism, no 3D render, no painterly background.
BACKGROUND: real transparent alpha outside the entire character and within gaps between appendages. No background color or scenery, no ground texture, no pedestal, no floor, no cast shadow, no large glow cloud, no particles, no motion slash, no detached effect, no labels, no text, no numbers, no logo, no watermark. The image is a clean separated enemy body asset for future animation.
```
