# 第五关赤链武卒 RedChain 验收报告

日期：2026-10-31。Unity 6000.4.9f1，2D / C# / URP。

交付预制体：`Assets/Projects/Prefabs/Enemy/RedChain.prefab`。
独立特效：`Assets/Projects/Prefabs/SeparateEffort/RedChainShockWave.prefab`。

## 数值和行为

- HP 135，经验 18，移速 0.8；普通受击、死亡继续使用 EnemyController；受击箱始终开启。
- 三段式：RedChainController / RedChainCombat / RedChainMovement。仅该敌人关闭怒气，以保持指定伤害、移速、冷却。
- 横扫：17 伤害，3.5 秒冷却，身前 4.5 × 2.2 单位，水平击退。
- 抛链：14 伤害，8 秒冷却，身前直线 9 单位。实际扣血后对命中的当前角色启动 0.35 秒强制水平速度；目的地在双方本体箱前方留 0.1 单位间隙，速度为到该位置的距离 / 0.35。
- 砸地：20 伤害，10 秒冷却，前方 6 单位、贴地 0.7 单位高；落地帧只采样一次伤害，生成一个池化 ShockWave；特效保持 1.5 秒后回池，视觉持续期间不追加伤害。
- 距离大于 6 单位优先抛链；2.5～6 单位在已冷却的横扫、砸地中随机；更近时优先横扫。无可用招式时继续靠近至 1.8 单位的停步距离。
- 起手即 `Health.SetSuperArmor(this, true)`，动画完整收招时立即撤销；强控制、死亡、停用时也撤销。普通眩晕和击退不能打断出招，伤害正常结算。
- 出招结束后 1.2 秒禁止自主移动和再次起手，期间允许正常受击反应。
- 拉拽由 RedChainPull 在玩家状态机后写入速度，保留移动、跳跃和技能输入，不调用 BeginCarry。每个物理步对 Ground 层扫掠本体，遇墙停在表面外，不把玩家送进地形；角色换出、来源回池或死亡时取消。

## 动画与切片

根据后续要求，每帧 Sprite 沿角色/特效可见像素边缘裁紧，保留 2 像素安全边距；不会把整块 256×384 透明格作为 Sprite 矩形。
8 张成品图均为 1024×1536、4×4 排版；每张恰好 16 个切片，名字为 `<Action>_0`～`<Action>_15`，PPU 100。
帧内脚底基线为从上往下第 352 像素，裁紧后重新换算脚底中心 pivot；冲击波 pivot 对齐地面。原图朝左，Visual 使用负 X 缩放适配基类朝向约定，物理箱不翻转缩放。

| 动作 | 有效切片 | fps | 循环 | 补帧数 | 裁紧减少的矩形面积 |
| --- | ---: | ---: | --- | ---: | ---: |
| Idle | 16 | 10 | 是 | 0 | 60.8% |
| Walk | 16 | 14 | 是 | 0 | 64.0% |
| Attacked | 16 | 18 | 否 | 0 | 53.7% |
| Die | 16 | 12 | 否 | 1 | 71.0% |
| AttackOne | 16 | 16 | 否 | 0 | 66.7% |
| AttackTwo | 16 | 16 | 否 | 8 | 88.4% |
| AttackThree | 16 | 16 | 否 | 0 | 73.2% |
| ShockWave | 16 | 16 | 否 | 9 | 89.8% |

保留已有有效姿态，只生成缺失的中间姿态/消散帧；未用重复帧补数量。Die 新姿态插入倒地过程，保留原有淡出帧。Attacked 使用用户提供的完整动作。Stand.png 保持单张参考立绘，不参与动作填充。

SpriteData：Idle / Walk / Attacked / Die 填在基类字段；AttackOne / AttackTwo / AttackThree 填在子类字段；ShockWave 在独立特效的 frames 字段。

| 攻击 | 开箱帧，0 起算 | 关箱帧 | 前摇 |
| --- | ---: | ---: | ---: |
| AttackOne | 6 | 9 | 0.6 秒 |
| AttackTwo | 8 | 10 | 0.8 秒 |
| AttackThree | 8，落地 | 9 | 0.9 秒 |

通过起手短暂停帧补足设计前摇，随后以 16 fps 播放。低帧率跨过攻击窗口时补采样一次，不丢落地事件，也不逐帧重复生成。

## 预制体结构

```text
RedChain
  RedChainController / RedChainCombat / RedChainMovement
  Health / ActorStatus / EnemyRewards / Rigidbody2D / BoxCollider2D
  Visual: SpriteRenderer / SpriteManager / SpriteOutlineController
  Combat
    Attacked: 常开 BoxCollider2D
    AttackOne / AttackTwo / AttackThree: BoxCollider2D + RedChainAttackBox
    AttackOneField / AttackTwoField / AttackThreeField: BoxCollider2D + EnemyCheckAttack
  ShockWave: RedChainShockWaveEmitter，引用独立池化预制体
```

RedChainAttackBox 继承 EnemyAttackBox，沿用伤害倍率和命中去重。排序层与 BlueShield 相同（Default），角色 sortingOrder 2；复用 BlueShield 材质。没有 Boss UI 组件。普通小怪血条由既有 EnemyHealthBar 流程创建。

## Level5Scene 接线

- 将原停用、失效的 LevelManager 占位组件替换为 RedChainStageManager；登记 RedChainStageEnemyManager（StageEnemyManager 子类）。
- `StageEnemyManager.enemyPools`：RedChain，`poolSize = 10`，`weight = 0.55`。
- 引用现有 PlayerSystem、BattleUI 公共预制体，绑定该场景的玩家出生点、出口、相机与四组边界，保留旧单人实例并停用它。
- 维持四波 43 只小怪配置。实际运行已确认玩家和十个敌人池成员能够初始化。
- 本任务不实现 RedBoom、WestKing。当前尚无 WestKing 预制体，因此第五关专属子类只运行已交付的小怪波次；最后一波结束后停止刷怪，不发放 Boss 胜利。日后填好 Boss 引用即恢复基类完整 Boss 流程。
- 未修改其他关卡、其他敌人或公共基类代码。工作开始前已有的变更予以保留。未执行 git commit，未删除现有资源。

## 验收结果

1. `dotnet build Assembly-CSharp.csproj --nologo`：0 错误，1 个既有警告 `Bird.cs:44 / CS0649`。
2. 逐个检查八张成品：128 个命名连续、有效、无重复、无越界 Sprite，PPU 100；Unity 原生导入与 Python alpha 检查均通过。
3. Unity 原生预制体检查：无 Missing Script、Missing Reference；三个攻击箱均按配置帧打开/关闭。
4. Play Mode：出招期间扣血且拒绝普通眩晕；末帧立即撤销霸体；收招 1.2 秒内不能移动或起手，普通受击仍生效；死亡/回池清理状态，下次出生 135 满血。
5. Play Mode：抛链只造成一次 14 伤害，0.35 秒到达身前，保留输入；遇实际 Ground 层墙体停在表面外。左右朝向均通过；玩家攻击触发箱与墙体重叠时也不会提前卡住拉拽。
6. Play Mode：砸地只造成一次 20 伤害、生成一个 ShockWave；1.5 秒后回池，下次冲击复用同一实例。第五关实际初始化通过。

复核日志位于 `Temp/RedChainAudit/build-final.log`、`unity-assets.txt`、`runtime-validation.txt`；切片几何统计位于 `normalized-art.json`。
编辑器可通过“造梦西游 → RedChain → 运行物理与行为验收”重跑，测试对象只存在于 Play Mode，不保存到场景。

## 本次新增/修改文件

- `Assets/Art/Enemies/Level5/RedChain/{Idle,Walk,Attacked,Die,AttackOne,AttackTwo,AttackThree,ShockWave}.png`，以及各自 `.png.meta`。
- 新增 `Assets/Projects/Prefabs/Enemy/RedChain.prefab` 及 `.meta`。
- 新增 `Assets/Projects/Prefabs/SeparateEffort/RedChainShockWave.prefab` 及 `.meta`。
- 新增 `Assets/Projects/Scripts/Enemy/RedChain/` 目录及目录 `.meta`，包含下列脚本及各自 `.cs.meta`：
  - RedChainController.cs
  - RedChainCombat.cs
  - RedChainMovement.cs
  - RedChainAttackBox.cs
  - RedChainPull.cs
  - RedChainShockWave.cs
  - RedChainShockWavePool.cs
  - RedChainShockWaveEmitter.cs
  - RedChainStageManager.cs
  - RedChainStageEnemyManager.cs
  - RedChainRuntimeValidation.cs（仅编辑器编译的验收探针）
- 新增 `Assets/Projects/Editor/RedChainSetup.cs`、`RedChainPlayValidation.cs` 及各自 `.meta`。
- 修改 `Assets/Projects/Scenes/Levels/Level5Scene.unity`。
- 新增本报告 `Docs/RedChain验收报告.md`。

## 补帧生成记录

使用内置 imagegen，参考原图，只补缺失内容。最终消费的素材已写入上述项目 PNG，未引用工作区外的生成文件。以下为最终提交的提示词。

### AttackTwo：8 个中间帧

```text
Use case: identity-preserve. Game production supplementary sprite frames. Reference is the existing 8-keyframe chain-throw animation for RedChain, a red-and-gold armored Chinese warrior carrying a dragon-head chain mace. Generate ONLY the 8 missing in-between frames, in exactly FOUR columns by TWO rows, each equal-sized cell, on true transparent background. No text, no gridlines, no background, no labels. Keep identical detailed 2D painted/cel game-art style, identical helmet, armor, physique, scale and left-facing 3/4 view as reference. Each cell contains the whole character, feet at the same baseline and body at the same scale, generous clear gap to neighboring cells. In row-major order: one new distinct intermediate pose BETWEEN each successive existing keyframe, with the eighth frame settling into idle after the last keyframe. First pose winding up with chain mace near left hand, then extending the dragon head progressively to the left, peak fully extended, then withdrawing chain. Exactly 8 complete distinct characters, no clipped feet, no extra limbs, no empty cells. The warrior remains planted while the chain moves.
```

### ShockWave：9 个扩散/消散帧

```text
Use case: precise-object-edit. Supplement missing frames in this exact 2D side-view fire ground shock-wave effect. Generate exactly NINE distinct full frames arranged in THREE columns by THREE rows, equal cells, isolated on genuine transparent background. No character, no text, no gridlines, no ground plane. Match reference: yellow-white hot cores, vivid red-orange flames, mirrored outward-curling ground tongues, scattered tiny embers. Each whole effect centered horizontally with the same bottom ground baseline in its cell, safe transparent gaps on all four sides, never cross cell boundaries. Read row-major. Frames 1-6 are new intermediate expansion shapes between successive pairs of the 7 reference keyframes: compact explosion, double curling lobes becoming successively wider, energy ripples pushing out. Frames 7-9 continue after the final keyframe: fading detached orange-red low flames and ground embers, then thin separated diminishing trails, then small residual red embers. Each cell must contain a distinct nonempty complete effect. Keep all flames visible and uncropped. Width grows then fades; height remains ground-hugging. Use the reference only for exact effect style; provide new intermediate frames, do not repeat any reference frame.
```

### Die：1 个倒地过渡姿态

```text
Use case: identity-preserve. Create exactly ONE missing intermediate death-animation sprite for the armored red-and-gold warrior in this reference sheet. Match precisely this 2D cel-painted game sprite style, black outlines, golden dragon helmet, red heavy armor, dark chain and dragon-head mace. Pose: warrior is collapsing, torso leaning back toward the RIGHT, left knee lowering, right hand bracing on ground, between the kneeling and lying-flat poses in reference row 2. Feet point toward left, helmet at right, face view and armor identical to reference. Whole character fully visible, fully opaque painted body, no duplicates, no text, no cast ground, no background, no extra props, no aura. SINGLE isolated complete sprite on genuine transparent background with at least 24 pixels clear margin. This fills a missing distinct pose; do not copy a pose already in reference.
```
