# WestKing 动画初稿

共 11 张：9 张角色动作、2 张独立攻击特效。根据最新要求先交付可继续修改的初稿。

每张画布为 1024×1536，4 列×4 行，共 16 帧；单格 256×384，按从左到右、从上到下读取。PNG 保留真实透明通道。使用内置 imagegen 生成，并经授权使用脚本清理透明边缘、缩放和重新排版。各图对应的 `.prompt.txt` 保存实际生成提示词。

| 文件 | 内容 |
| --- | --- |
| Walk.png | 重型步行循环 |
| DragonClaw.png | 龙爪前突 |
| PearlToss.png | 抛珠 |
| DragonBreath.png | 龙息角色动作，不含火焰 |
| PearlOrbit.png | 红珠环绕循环 |
| Ward.png | 举珠施放护持，结束保持姿势 |
| HitReaction.png | 单次受击 |
| Death.png | 死亡倒地 |
| Idle.png | 待机呼吸循环 |
| Effects/PearlProjectile.png | 向左飞行的红珠弹体循环 |
| Effects/DragonFlame.png | 独立向左龙息火焰，起势、持续、消退 |

初稿已保留角色的主要外形、色彩与红龙/红珠元素，动作细节仍可进一步修改。特别是 PearlToss 的抛珠手部和第 9–12 帧持珠状态尚有偏差；细小装饰、循环接缝与姿态一致性也适合后续逐帧调整。

`.draft.png` 是生成器输出的原始草图，不是排版后的交付图；正式交付文件使用上表不带 `.draft` 的名称。未修改游戏脚本、预制体或现有角色资源。
