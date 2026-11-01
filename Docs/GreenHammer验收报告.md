# 第六关青玉锤卒 GreenHammer 验收报告

日期：2026-11-01。Unity 6000.4.9f1，2D / C# / URP。

完整小怪预制体：`Assets/Projects/Prefabs/Enemy/GreenHammer.prefab`。
独立池化冲击波：`Assets/Projects/Prefabs/SeparateEffort/GreenHammerShockwave.prefab`。
已接入用户补充的 `Effects/GreenHammerShockwave.png` 专属翡翠震地冲击波。

## 数值与行为

HP 150，经验 20，移速 0.8，受击箱常开。Controller / Combat / Movement 沿用 RedChain 三段结构和 EnemyController 的待机、行走、攻击、受击、死亡调度；本敌人关闭怒气，保持指定基础伤害、移速和冷却。

| 招式 | 基础伤害 | 独立冷却 | 判定 |
| --- | ---: | ---: | --- |
| 横扫 HammerSweep | 19 | 4 秒 | 本体前方宽 4.5、高 2.2 的攻击箱，带击退 |
| 跃砸 LeapSlam | 26 | 10 秒 | 短跳实际落地后一次半径 3 的范围攻击 |
| 震地 GroundPound | 每段 18 | 9 秒 | 三段独立地面判定，中心依次在身前 2、4、6 单位，各相隔 0.15 秒 |

选招范围：横扫 2.5～6，跃砸 6 内，震地 3～8。三招都就绪时随机；其余情况优先有效横扫，再选择已就绪的跃砸/震地。各招冷却由既有 EnemyCombat 独立记录；出生时按 RedChain 的方式进入各自初始冷却。
没有可用招式时沿用基类靠近，stopDistance=3.2，保证停步后处于三招共同可选距离内；可用攻击仍由检查箱及上述选招距离决定。

起手调用 `Health.SetSuperArmor(this, true)`，只免普通硬直、击退及眩晕，正常扣血。攻击箱关闭当帧撤销本来源霸体，并从该时刻开始 **1.4 秒**输出窗口；动画余下收招帧计入这段时间。窗口内停止自主移动、禁止再次起手，普通受击照常播放，不会延长恢复时刻。
强控制、死亡、停用时取消尚未执行的攻击并撤销本来源霸体。保留其他来源的既有状态和公共控制行为。

## 攻击帧窗口与跃砸

下表全部使用 **0 起算**，与 Sprite 名称及 `AttackOpenIdx / AttackShutIdx` 一致。关箱帧不再属于持续开箱区间。

| 招式 | 起跳/开箱帧 | 关箱帧 | 额外时序 |
| --- | --- | ---: | --- |
| HammerSweep | 第 5 帧开箱 | 9 | 第 5～8 帧有效；同一招重复触发或主动采样只扣一次血 |
| LeapSlam | 第 4 帧起跳，第 9 帧起等待实际落地并开箱 | 12 | 到达落地动作但尚未着地时保持该帧，落地后继续；一次圆形采样 |
| GroundPound | 第 9 帧落地并开第一段 | 14 | 从落地时刻起，后两段分别延后 0.15、0.30 秒 |

跃砸起跳时保存原重力，把 Rigidbody2D.gravityScale 设为 0，并写入竖直/水平初速。零重力期间按固定物理步施加独立向下加速度，形成短抛物线；落地恢复原重力后才产生攻击。水平推进最多 3 单位，检查前方实体和落点地面，保留物理碰撞及连续碰撞检测。
不改变图片自带的离地偏移或固定脚底 pivot。落地攻击使用 6×6 Box 的外接范围，再按触发体最近点过滤，得到真实半径 3 的判定，矩形四角不会误命中。
冰冻、携带、死亡、对象池停用或最长飞行保护均可取消跃起；携带/吸附释放后的下落恢复原重力，避免把暂时的零重力留到之后。

震地第一段使用根预制体的 GroundPound 攻击箱，后两段分别借出独立池对象和触发体。每段生成时主动采样一次，随后关掉该段触发判定；视觉残留不持续伤害。每段分别保存命中去重记录，命中过第一段的角色仍可被后续段命中。
低帧率跳过命中帧时会补采样一次，后续段继续按时间依次释放，不在一次调用中挤成三次伤害；待所有段释放后完成收招。

## 动画绑定与资源保护

七张现成 PNG 均已验证为 1024×1536、4×4，每张 16 个有效 Sprite，PPU 100。保持现有 256×384 固定切片、Full Rect、统一 pivot `(0.5, 0.14583333)`，对应格内脚底基线 y=328。

实际输入 meta 使用了 `_01`～`_16`，本次仅改为用户要求的 `_0`～`_15`。纹理 GUID、Sprite 内部 ID、切片矩形、pivot、导入设置均保留。逐文件比较确认七份 meta 的差异只有名称替换；七张 PNG 的 SHA-256 与接入前相同。

| 动作 | 帧数 | fps | 循环 | 预制体字段 |
| --- | ---: | ---: | --- | --- |
| Idle | 16 | 10 | 是 | EnemyController.Idle |
| Walk | 16 | 14 | 是 | EnemyController.Walk |
| HammerSweep | 16 | 16 | 否 | GreenHammerController.HammerSweep |
| LeapSlam | 16 | 16 | 否 | GreenHammerController.LeapSlam |
| GroundPound | 16 | 16 | 否 | GreenHammerController.GroundPound |
| Hit | 16 | 18 | 否 | EnemyController.Attacked |
| Death | 16 | 12 | 否 | EnemyController.Die |
| 专属翡翠冲击波 | 16 | 16 | 否 | GreenHammerShockwave.frames，生命周期 1 秒 |

美术附带备注中的旧帧率和一基名称保留作原始交付记录；运行绑定使用本轮指定配置。Stand.png、原始 JSON 与 README 未改。

专属冲击波来自 `Effects/GreenHammerShockwave.png`：1024×1024、4×4、每格 256×256，原有 `_01`～`_16` 连续命名、PPU 100、pivot `(0.5,0.15625)` 与 y=216 地面线均保留。原图向左，向右出招时翻转表现；池复用时重新设置方向。按实际 alpha 峰值宽度 1.77u 配置 nativeWidth，震地段峰值显示为 2u，跃砸复用时显示为 6u；不把透明留白计入范围，不逐帧缩放。PNG 与 meta 哈希均与收到时一致。

## 预制体与第六关接线

GreenHammer 根节点包含 GreenHammerController / Combat / Movement、Health、ActorStatus、EnemyRewards、Rigidbody2D 和本体 BoxCollider2D。
Visual 子物体挂 SpriteRenderer、SpriteManager、SpriteOutlineController，材质及排序与 RedChain 一致；物理箱保持正常缩放，Visual 适配原图朝左的方向。
Combat 下有常开 Attacked 受击箱、三个独立攻击箱（BoxCollider2D + GreenHammerAttackBox，继承 EnemyAttackBox）、三个 EnemyCheckAttack 检查箱。Shockwave 子物体保存独立冲击波预制体引用。没有 Boss UI。

GreenHammerShockwavePool 为场景级简单池，使用既有 `PrefabPool<T>`，预热三个实例并自动复用。接口接受 EnemyCombat，BossCombat 子类和其他小怪也可调用；表现结束自动停用回池，战斗结束统一回收。池对象使用世界位置，独立于射手移动。

Level6Scene 原管理器为禁用的旧占位组件。参照 RedChain 的接线方式，新增本关 GreenHammerStageManager / GreenHammerStageEnemyManager，继承现有 StageManager / StageEnemyManager，启用本关小怪流程并接好 PlayerSystem、BattleUI、相机四波边界、玩家出生点和出口引用。
`StageEnemyManager.enemyPools` 已登记 GreenHammer：**poolSize 10、weight 0.55**。保留原四波数量及关卡布局。原生加载已确认管理器初始化成功并预热十个 GreenHammer。
目前没有 NorthKing Boss 预制体，本次只运行已交付小怪波次；最后一波结束后停止本次演示，不开放击败 Boss 的胜利出口。未来填入 Boss 引用后，这两个管理器自动沿用标准 Boss 流程。

公共 EnemyMovement 仅新增一个 `protected` **只读**查询 `IsSuctionActive`，用于区分主动吸附与释放后的 Falling 阶段，使跃砸在携带释放后及时恢复重力。没有更改任何既有方法、分支、字段写入或其他敌人的行为。

## 逐项验收

| 要求 | 结果 |
| --- | --- |
| 1. `dotnet build Assembly-CSharp.csproj` 零错误 | 通过，0 错误，仅既有 Bird.startMissTrophy 未赋值警告。Editor 工程也为 0 错误，既有 RedChainSetup 过时接口警告保留。 |
| 2. 七动作各 16 帧、名称连续、无空 Sprite | 通过，共 112 帧；PNG/Meta 离线检查和 Unity 原生导入均通过，PNG 像素未改。 |
| 3. Prefab 引用、三招开关、霸体 | 通过，两个 Prefab 无 Missing Script/丢失对象引用；横扫、跃砸、震地按窗口开关；普通伤害扣血但不打断出招，关箱撤霸体；恢复期普通受击正常。 |
| 4. 震地三段各一次 | 通过，移动角色依次承受 18+18+18=54；三段时间差约 0.15 秒；重复采样、停留和重新进入可见波纹不再扣血，实例自动回池并可复用。 |
| 5. 文件、窗口、编译、场景、待补美术 | 已列于本报告。 |

Unity 原生批处理 Play Mode **48 项 PASS，ALL PASS，退出码 0**。除上述项目，还验证了三招选择与独立冷却、1.4 秒禁止移动/再次起手、跃砸零重力初速与真实圆形范围、冰冻/携带/死亡/复用后的重力恢复、跳帧情况下的三段时序，以及专属特效引用/方向/复用。反伤在命中回调中击杀施法者时也会安全取消，不继续访问旧攻击索引。
批处理使用 nographics；动画通过 SpriteManager 实际播放与帧事件检查，图片另有拼图目检，不包含 GPU 游戏画面截图验收。

证据文件位于 `output/GreenHammerAudit/`：`runtime-validation.txt`、`unity-runtime.log`、`unity-assets.txt`、`dotnet-build.txt`、`dotnet-editor-build.txt`、`art-validation.txt`、`art-audit.json`、`preservation-validation.json`、`contact-sheet.png`。
复查入口：编辑器菜单 `造梦西游/GreenHammer/生成并验证青玉锤卒`；打开 Level6Scene 后运行 `造梦西游/GreenHammer/运行三招与重力验收`。

## 待补美术

**震地专属美术已补齐并接入。** 三段均播放用户提供的翡翠压力波与碎石消散动画。
跃砸目前复用这套翡翠冲击波作为 6u 落地提示；独立环形落地爆发仍列为“待补美术”，不影响已经完成的半径 3u 判定。GreenHammerShockwave 已完全解除对 RedChain 占位图片的引用。

## 新增/修改文件清单

新增脚本（目录 `Assets/Projects/Scripts/Enemy/GreenHammer/`，均含 `.meta`）：

- `GreenHammerController.cs`
- `GreenHammerCombat.cs`
- `GreenHammerMovement.cs`
- `GreenHammerAttackBox.cs`
- `GreenHammerShockwave.cs`
- `GreenHammerShockwavePool.cs`
- `GreenHammerShockwaveEmitter.cs`
- `GreenHammerStageManager.cs`
- `GreenHammerStageEnemyManager.cs`
- `GreenHammerRuntimeValidation.cs`，仅 UNITY_EDITOR 编译

其他新增资产：

- `Assets/Projects/Scripts/Enemy/GreenHammer.meta`
- `Assets/Projects/Editor/GreenHammerSetup.cs` 及 `.meta`
- `Assets/Projects/Editor/GreenHammerValidationBridge.cs` 及 `.meta`
- `Assets/Projects/Prefabs/Enemy/GreenHammer.prefab` 及 `.meta`
- `Assets/Projects/Prefabs/SeparateEffort/GreenHammerShockwave.prefab` 及 `.meta`
- `Docs/GreenHammer验收报告.md`

接入已有新增素材：`Assets/Art/Enemies/Level6/GreenHammer/Effects/GreenHammerShockwave.png` 及原 `.meta`，保留用户原图与切片，附带说明/Notes 不变。

修改文件：

- `Assets/Art/Enemies/Level6/GreenHammer/` 下 Idle、Walk、HammerSweep、LeapSlam、GroundPound、Hit、Death 的七份 PNG.meta：只修正 Sprite 名称。
- `Assets/Projects/Scenes/Levels/Level6Scene.unity`：本关接线。
- `Assets/Projects/Scripts/Enemy/EnemyMovement.cs`：新增上述一行只读查询，既有语义不变。

本次保留其他关卡、敌人、Boss 和已有未提交工作；编辑器自动产生的最近场景列表变更已还原。未删除现有资源，未执行 git commit。
