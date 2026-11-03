# 第7关 PressureKettle 泄压壶灵

2026-11-02。完成专属脚本、动作目录迁移、敌人及三种战斗特效预制体，并登记 Level7Scene。未执行 git commit；本任务没有修改公共 Health / EnemyController / EnemyCombat / EnemyMovement 的语义或其他敌人的实现。

## 目录与资源

- 通过 `AssetDatabase.MoveAsset` 将 `Assets/Art/Enemies/Level7/enemy3/SpriteSheets16` 整目录移动至 `Assets/Art/Enemies/Level7/PressureKettle/SpriteSheets16`。迁移时逐项检查目录和资源 GUID；重命名切片时保留 Sprite ID 与 local file ID。
- 7张原PNG像素不变：Idle、Walk、SteamJet、PressureBomb、PressureRelease、Hit、Death。每张16帧、1024×1536、4×4、每格256×384；切片命名 `<Action>_0..15`，PPU100，脚底 Pivot=(128,56)，归一化=(0.5,56/384)。README与动画清单同步新目录及命名。
- 删除前列出的重复清单：`enemy2/stand.png` 与 `RollingFurnace/Stand.png` SHA256一致；`enemy3/stand.png` 与 `PressureKettle/Stand.png` SHA256一致。扫描场景、预制体和序列化资源未发现旧副本GUID引用后，才通过AssetDatabase删除副本及空目录。`enemy1`原本不存在。保留两个正式Stand及其他美术。
- SteamCone、PressureBomb、PressureBurst特效图各有实际16帧，原有连续 `_01..16` 名称、GUID、PPU和纯播放版 `*FX.prefab` 保留。

## 脚本与预制体

脚本位于 `Assets/Projects/Scripts/Enemy/PressureKettle/`：Controller负责动画与AI接入，Movement沿用公共移动，Combat管理压力/招式/过热，Effect负责锥形判定/抛物线/单次范围爆炸，EffectPool复用项目 `PrefabPool<T>`。RuntimeValidation仅在编辑器编译，不挂正式预制体。

生成工具：`Assets/Projects/Editor/PressureKettleSetup.cs`；批量验收入口：`PressureKettleValidationBridge.RunBatch`。

| 预制体 | 用途 |
| --- | --- |
| `Assets/Projects/Prefabs/Enemy/PressureKettle.prefab` | 完整敌人，Health.beginBlood=96，EnemyRewards.experienceReward=22，移速0.8 |
| `Assets/Projects/Prefabs/SeparateEffort/PressureKettleSteam.prefab` | 蒸汽锥形判定与16帧视觉 |
| `Assets/Projects/Prefabs/SeparateEffort/PressureKettleBomb.prefab` | 飞行、落地引信、爆炸及消散 |
| `Assets/Projects/Prefabs/SeparateEffort/PressureKettleBurst.prefab` | 一次泄压范围伤害与16帧视觉 |

敌人结构对齐RedBoom：Root含Health、ActorStatus、EnemyRewards、Rigidbody2D、实体BoxCollider和专属Controller/Combat/Movement；Visual含SpriteRenderer、SpriteManager、SpriteOutlineController；Combat下有Attacked受击箱和SkillCheck，另有Muzzle。物理尺寸按现有壶灵美术匹配，脚底为Root原点。

三类特效由场景独立对象池持有，每类首次预热2个，允许扩容。射手取消施法或死亡只停止仍连接壶嘴的蒸汽；已飞出压力弹继续落地、计时和爆炸。伤害倍率在发射时快照，同时记录射手LifeVersion，射手死亡/回池/重生后不把旧弹的反伤转给新生命。

压力弹美术前6帧带有格内弧线位移，预制体按照现有美术清单中的实际target_anchor抵消此偏移，世界轨迹由物理计算；后10帧用于爆炸。没有重绘或重新裁切图像。特效缩放根据可见内容匹配3.5u、2u半径、4u半径的实际判定。

## 帧窗口和数值

下表F为1起始帧，index为SpriteManager的0起始索引。各招冷却从起手开始独立计时。

| 招式 | 前摇与释放帧 | 判定 | 冷却 |
| --- | --- | --- | --- |
| 蒸汽喷口 | 16帧，8 FPS；F1–F5前摇，F6/index5开启；约F14/index13完成最后一次结算并关闭 | 从壶嘴向前3.5u、总角度60°锥形。开启后0.25/0.50/0.75/1.00秒各4伤害，共4次，带标准击退；无额外首击。每次独立去重Health | 6秒 |
| 压力弹 | 16帧，14 FPS；F1–F8前摇，F9/index8抛出 | 0.9秒预计飞行时间、重力18的抛物线；CircleCast检查地面；实际落地后0.6秒，半径2u、14伤害、只爆炸一次 | 8秒 |
| 泄压爆发 | 16帧，14 FPS；F1–F7前摇，F8/index7释放 | 本体周围半径4u，12伤害并击退，一次去重结算；压力清零，进入3秒受伤倍率1.4 | 12秒 |

Idle/Walk为10 FPS循环，Hit为16 FPS，Death为12 FPS；每个动作均16帧。跳过释放帧时通过 `frame >= releaseFrame` 补采样，提交标志防止同帧重复释放。

AI按水平距离维持中距：小于3.5u后撤，大于7u靠近，3.5–7u站定施法。蒸汽仅在壶嘴起点加3.5u能够到达的近侧中距选择；其余中距使用压力弹。满压/低血泄压优先于距离判断，可在后撤距离或失去目标时触发。冰冻、外部控制、受击和死亡仍遵守已有控制流程。

## 压力与过热

- 每次招式起手、每次实际扣血各+8压力，上限100；无敌/格挡/零伤害不会刷压力。达到100锁存泄压请求。
- 血量严格低于40%时，每次出生最多锁存一次低血请求；持续低血不会在冷却结束后反复触发。同次满压和低血条件合并为一次泄压。满压清零后重新累计仍可再次达到100。
- 未释放就被打断时保留请求和压力，等待冷却恢复；成功释放时清零。泄压伤害本身不会重复结算。
- 过热使用 `Health.SetDamageMultiplier(this,1.4f)`。3秒到期、死亡和禁用时仅 `RemoveDamageMultiplier(this)`，不改其他来源。除Update到期清理外，在Health.DamagePreparing前再次检查截止时间，避免同帧调用顺序延长易伤。
- 压力清零及过热在范围伤害回调前提交，反伤当场杀死射手时死亡清理不会被后续代码覆盖。

## Level7Scene接线

`Assets/Projects/Scenes/Levels/Level7Scene.unity` 中现有StageEnemyManager派生组件的enemyPools新增一条PressureKettle：poolSize=10，weight=0.33。相对本任务开始时的场景备份，只有这3行新增；保留现有另外两类小怪和管理器配置。实际Play Mode中预热10个PressureKettle。

## 验证

- Unity 6000.4.9f1真实导入、预制体引用检查通过：7×16动作、3×16特效、所有新预制体无Missing Script/对象引用，HP/经验/移速精确匹配。
- Play Mode使用实际玩家及敌人预制体、SpriteManager、Physics2D通过：双向蒸汽各4次共16伤害及4次击退请求；压力弹落地0.6秒后14伤害一次，射手死亡/回池重生后仍结算，复用状态重置；泄压12伤害一次；满压与低血各单次锁存、双条件合并、持续低血越过12秒冷却不重触发；过热3秒结束且保留其他来源减伤；禁用/重生和反伤致死时清理完整；实际AI后撤/靠近/停步及三个动画释放均通过。
- `dotnet build Assembly-CSharp-Editor.csproj -v minimal` 同时编译运行时及编辑器程序集，0错误，7个既有警告（Bird字段、RedChainSetup旧API、Level7EffectsValidation反序列化字段）；PressureKettle新增文件无警告。Unity退出会清理Temp中的NuGet还原文件，因此命令保留默认还原步骤。完整输出位于 `Logs/PressureKettleAudit/dotnet-build.txt`。
- 可重复运行的详细记录：`Logs/PressureKettleAudit/unity-assets.txt`、`Logs/PressureKettleAudit/runtime-validation.txt`，两者结尾均为ALL PASS。Unity批处理日志：`Logs/PressureKettle-Unity.log`。
