# Level7 独立技能特效（5 张）

2026-11-02。由内置 imagegen 绘制，参考本关敌人的青铜、古金、橙红炉火配色及赛璐璐描线；按授权用脚本整理切片、统一尺度、固定锚点和透明边缘。

| 特效 | 透明精灵表（相对本目录） | 独立预制体名称 | 建议触发 |
| --- | --- | --- | --- |
| 钳火机傀·排气喷焰 | `ClampGuard/Effects/VentFlame.png` | `ClampGuardVentFlameFX` | 排气攻击开始出火时；挂胸部出气点 |
| 滚丹炉兽·砸地冲击波 | `RollingFurnace/Effects/LandingShockwave.png` | `RollingFurnaceImpactFX` | BounceSlam 落地帧；放在落点地面 |
| 泄压壶灵·蒸汽锥 | `PressureKettle/Effects/SteamCone.png` | `PressureKettleSteamFX` | SteamJet F5 开始喷射；挂壶嘴位置 |
| 泄压壶灵·压力弹 | `PressureKettle/Effects/PressureBomb.png` | `PressureKettleBombFX` | PressureBomb F9 出手时；放在发射位置 |
| 泄压壶灵·泄压爆发 | `PressureKettle/Effects/PressureBurst.png` | `PressureKettleBurstFX` | PressureRelease F5–7 开盖爆发；放在壶身中心 |

预制体均位于 `Assets/Projects/Prefabs/SeparateEffort/`，使用工程现有的 SpriteRenderer、SpriteManager、IndependentSkillEffect。默认启用即播放，16 FPS，16 帧约 1 秒，单次播放后禁用，支持对象池复用。需要更长持续喷射时，由技能逻辑控制重播或单独保持喷流阶段。

- 全部为 **1024×1024 RGBA PNG、4×4、16 格 256×256**，按左到右、上到下排序；Sprite/Multiple 已切好，名称为特效键名加 `_01` 至 `_16`。
- 每格连同烟、光晕、碎石和火星，四侧至少 24px 全透明；当前实测最小边距为 29px。没有角色、源头硬件、背景、地面贴图、网格或文字。
- 每张图全程使用同一等比缩放量，保留起始、峰值、衰减的自然占幅变化；不逐帧放大填格，不按余烬紧边裁切。
- PPU=100，Bilinear、Clamp、无 Mipmap、无纹理压缩、Full Rect；真实 alpha 中透明处 RGB 也已清零。
- 喷焰与蒸汽的出气锚点为格内 `(216,128)`，默认朝左，Pivot=`(0.84375,0.5)`。烟团和火星可在消散时脱离出气点。
- 冲击波接地边缘统一为格内 `y=216`，中心 x=128，Pivot=`(0.5,0.15625)`；扬尘向上、尘环横向扩散。
- 泄压爆发中心为 `(128,128)`，Pivot=`(0.5,0.5)`，白蒸汽环逐渐断开并消散。
- 压力弹保留约 29px 直径的青铜弹体，F1–6 的左向弧线已画进格内：中心依次为 `(169,144)、(155,113)、(140,94)、(124,94)、(110,113)、(98,144)`；F7–16 在 `(98,144)` 附近爆炸。Pivot=`(0.66015625,0.4375)`，对应起始发射点。预制体默认世界速度为零，避免额外叠加一条飞行轨迹。若后续需要物理弹道，应把飞行段 F1–6 和爆炸段 F7–16 分开使用。
- 尾帧自然淡出，不复制首帧；所有特效均为单次爆发而非循环贴图。

`Level7FX.AnimationNotes.json` 保存完整最终提示词、每张图的锚点/帧率/Pivot、逐帧透明包围框与边距、统一缩放量、切片 ID 和 SHA-256。
`Level7FX.UnityValidation.json` 记录 Unity 原生导入及 5 个预制体引用检查结果。

接入技能时可实例化对应预制体，并调用 `IndependentSkillEffect.Play` 指定朝向/跟随锚点；`Pause`、`Resume`、`StopEffect` 可独立控制。以上是视觉资源，命中、伤害、碰撞和施放时序由现有技能逻辑绑定。
