# GreenSummon / NorthKing 独立特效预制体

2026-11-01。角色图只包含姿势、盔甲、袍服和实体道具；以下 5 类效果均为独立 Unity 预制体，带各自 16 帧透明特效表。
特效由内置 imagegen 绘制，未含人物；按用户授权整理透明、羽化光晕边缘和固定单格坐标。

| 预制体（本目录） | 类型 | 默认播放/移动 | 建议触发 |
| --- | --- | --- | --- |
| `GreenSummonHammerFX.prefab` | 独立落锤与冲击 | 单次，16 FPS，约 1 秒，完成后禁用 | 召唤锤击施法 F10；落点放在目标地面 |
| `GreenSummonWarBannerAuraFX.prefab` | 战旗光环 | 循环，12 FPS，持续到 StopEffect | 插旗 F08；放在战旗地面锚点 |
| `NorthKingUmbrellaRibFX.prefab` | 单枚伞骨弹 | 循环，16 FPS，默认向左 6 单位/秒，2 秒禁用 | RibVolley F05–08；多枚弹分别实例化 |
| `NorthKingRainCurtainFX.prefab` | 独立雨幕体积 | 循环，16 FPS，默认 2 秒禁用 | RainCurtain F09–12；放在目标区域地面 |
| `NorthKingShieldFX.prefab` | 独立护罩 | 循环，12 FPS，持续到 StopEffect | Guard F05；退出防御时停止 |

## 使用方式

每个预制体都自带 SpriteRenderer、项目现有 SpriteManager 和新组件 IndependentSkillEffect。
帧序、透明材质、方向、循环与生命期已经绑定；默认朝左，自动启用播放。结束后禁用而不是销毁，便于外部对象池复用。
SpriteManager 复用项目的精灵透明/描边处理，不修改它的源码。

```csharp
// 在外部技能事件或帧事件中生成。角色动画与特效动画完全独立。
var go = Instantiate(effectPrefab, groundPosition, Quaternion.identity);
var fx = go.GetComponent<IndependentSkillEffect>();
fx.Play(null, Vector3.zero, faceRight: false, seconds: 2f);

// 护罩/光环可跟随角色、战旗或其他独立锚点；0 表示循环到显式停止。
fx.Play(anchorTransform, Vector3.zero, faceRight: false, seconds: 0f);

// 伞骨弹可使用默认弹速，或由调用方赋予世界空间速度。
fx.SetVelocity(Vector2.left * 8f);
fx.Pause();
fx.Resume();
fx.StopEffect();
```

- `Play()` 重新开始；带参数的 Play 指定跟随目标、世界偏移、朝右镜像和持续秒数。负持续时间使用预制体默认值，0 让循环持续到停止；非循环落锤仍会自然完成。
- `SetFollow` / `SetVelocity` 可在播放中独立调整。跟随目标有效时优先跟随，不叠加弹速。位置与缩放也可直接由调用方控制 Transform。
- `Pause` 同时冻结帧动画、运动与生命期；`Resume` 恢复。`Completed` 在自然结束或 StopEffect 时触发一次，允许监听者立即复用/重播该实例。
- 落锤、雨幕和护罩使用底部锚点；光环使用椭圆地面锚点；伞骨弹使用中心锚点。纹理均为 1024×1024、4×4、每格 256×256、PPU=100，四侧至少 32px 空透明边距。
- Aura 默认排序为 2，弹/雨/盾为较高排序，可在 SpriteRenderer 调整以配合场景。素材复用项目现有透明材质。
- 这些预制体只负责视觉、独立移动/跟随和生命周期，不内置伤害、命中、减伤或 Boss AI；未修改现有敌人/场景。技能脚本可在上述建议帧触发，并单独绑定战斗判定。

`GreenSummon_NorthKing_FX.Notes.json` 包含最终提示词、纹理与预制体路径、帧率、Pivot、逐帧 alpha 包围框和校验哈希。
Unity 原生导入验证报告见 `GreenSummon_NorthKing_FX.UnityValidation.json`（成功验证后生成）。
