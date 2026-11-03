# 第7关 ClampGuard 钳火机傀

2026-11-02。已完成专属脚本、固定网格切片、完整敌人预制体、共用喷焰特效池与 Level7Scene 池登记。未提交 Git；未删除或重绘现有 PNG；Health、ActorStatus、EnemyController、EnemyCombat、EnemyMovement 与本任务开始时的哈希一致。

## 文件清单

以下路径均相对项目根目录。

| 类型 | 文件 |
| --- | --- |
| 控制器 | `Assets/Projects/Scripts/Enemy/ClampGuard/ClampGuardController.cs` |
| 招式与方向护甲 | 同目录 `ClampGuardCombat.cs` |
| 移动 | 同目录 `ClampGuardMovement.cs`（直接沿用 EnemyMovement） |
| 三种命中几何及去重 | 同目录 `ClampGuardAttackBox.cs` |
| 强钳锁、拖拽和甩出 | 同目录 `ClampGuardGrab.cs` |
| 灼烧 | 同目录 `ClampGuardBurn.cs` |
| 玩家/敌人共用视觉池 | 同目录 `ClampGuardVentFlamePool.cs` |
| 编辑器内运行验收 | 同目录 `ClampGuardRuntimeValidation.cs`，仅 UNITY_EDITOR 编译，不挂正式资产 |
| 生成与资产检查 | `Assets/Projects/Editor/ClampGuardSetup.cs` |
| 隔离 Play Mode 验收入口 | `Assets/Projects/Editor/ClampGuardValidationBridge.cs` |
| 敌人完整预制体 | `Assets/Projects/Prefabs/Enemy/ClampGuard.prefab` |
| 独立喷焰预制体 | `Assets/Projects/Prefabs/SeparateEffort/ClampGuardVentFlame.prefab` |
| 切片修正 | `Assets/Art/Enemies/Level7/ClampGuard/` 内 Idle、Walk、AttackOne、AttackTwo、AttackThree、Attacked、Die 的7个 `.png.meta` |
| 场景 | `Assets/Projects/Scenes/Levels/Level7Scene.unity`，追加 ClampGuard 池及所需管理器引用 |

新增脚本、目录和预制体均附 Unity `.meta`。旧 `ClampGuardVentFlameFX.prefab` 保留，新的指定名称预制体使用相同 VentFlame 美术，不更改另外4种 Level7 特效。

## 美术切片

- 原 PNG 全部保持原字节及 SHA-256。这里只修改导入与切片元数据。
- 7组动作均为1024×1536、4×4、每格256×384，16帧，命名 `<Action>_0` 至 `<Action>_15`，PPU100。
- AttackThree：19→16，剔除13×39、12×22、12×23的碎片切片。Die：18→16，剔除25×18、24×19的碎片切片。剔除的是额外切片条目，未抹除原图像素。
- 依据 Stand 的完整双足落地姿态，对齐现有中立 Idle 的脚底线：单元格左上坐标 y=312，所有动作固定底部坐标 Pivot=(128,72)，归一化 `(0.5,0.1875)`。
- Visual使用(-1.5,1.5,1)等比缩放，使现有小幅角色画面与1.85u高的物理本体匹配。图片本身未缩放；SpriteManager原有朝向接口保持不变。

## 数值、帧窗口与结算

HP138，经验22，基础移速0.9。普通移动/慢速、重生和状态控制继续走公共 EnemyMovement；未启用怒气数值。

下表F为1起始帧，括号为C#的0起始索引。攻击动作10 FPS，共16帧；Idle/Walk为12 FPS循环。

| 招式 | 判定窗口 | 伤害与控制 | 冷却 |
| --- | --- | --- | --- |
| 钳夹 / AttackOne | F5–F8（4≤index<8），F9关箱 | 前方2.5u；每目标只扣16一次；成功扣血后通过现有ActorStun施加1.2秒普通定身，霸体可免控制 | 3.5秒 |
| 钳锁拖行 / AttackTwo | F6–F9（5≤index<9），F10关抓取箱；有目标时保持F10，释放后继续F11–F16 | 前伸4.5u，整招最多抓一个玩家；强控保持1.5秒；从命中后0.5、1.0、1.5秒各扣3，合计9，没有额外首击伤害；自然结束只向身后甩出一次 | 9秒 |
| 排气喷焰 / AttackThree | F8（index=7）姿势触发；F9关箱；跳帧时补采样一次 | 从胸部出气点起前方3u锥形，半高为0.12+0.4×前向距离；一次12；随后第1、2秒各3点灼烧，总灼烧6 | 7秒 |

距离按目标水平距离筛选：≤2.5u优先钳夹；3–5u优先钳锁；5u内喷焰冷却好时可用。各招独立冷却。施法前摇和有效窗口拥有自身霸体来源；关箱且无钳锁后移除此来源，恢复普通受击；强控依然能打断。

方向护甲在 `Health.DamagePreparing` 内按本次来源相对实际朝向判断：正面只设置自己的 `SetDamageMultiplier(this,0.8)`，背面移除自己的正面倍率并挂本次一次性+25%易伤。结算后清掉自己的临时正面来源。无来源/周期伤害不继承上一次方向。其他来源减伤或易伤不被清空。

钳锁不禁用玩家输入，普通移动/跳跃会被保持；玩家仍能施放强控攻击或位移技能。施法者被冰冻、携带、吸附、死亡、退场，或目标位移、驱散、无敌、死亡、换人时，立即解除该来源。现有Wukong烈焰闪的速度位移和空斩的直接位置位移均能逃脱；解除不覆盖已经产生的位移速度。Boss及非玩家敌人不是钳锁目标。

拖拽使用本体扫掠，停止在身前安全间距。甩出只发生一次，并检查墙体及身后落点；被地形阻挡时安全释放，避免穿墙。所有状态保存双方 LifeVersion，池成员重生后不沿用旧锁或旧灼烧。

## 预制体与特效

敌人Root包含 ClampGuardController、ClampGuardCombat、ClampGuardMovement、Health、ActorStatus、EnemyRewards、Rigidbody2D、BoxCollider2D；Visual包含 SpriteRenderer、SpriteManager、SpriteOutlineController。Combat下有受击箱、3个ClampGuardAttackBox与3个EnemyCheckAttack。

喷焰预制体仅含视觉组件，无伤害碰撞体，16 FPS单次播放；由 `ClampGuardVentFlamePool` 复用项目现有 `PrefabPool<IndependentSkillEffect>`，结束禁用即回池。每次租出重置位置、缩放、朝向和播放状态。3u视觉长度与判定长度匹配。

```csharp
// 玩家或敌人均可调用；伤害与阵营由调用者自己的判定管理。
ClampGuardVentFlamePool.Emit(flamePrefab, worldPosition, faceRight);
```

## 场景接线

Level7Scene中现有StageEnemyManager派生管理器的enemyPools已追加ClampGuard：poolSize=10，weight=0.34，且只登记一条。场景控制器enemyManager指向该管理器。

实施期间检测到并行任务补入RollingFurnace的Level7管理器及池，已合并ClampGuard条目、保留对方配置，未修改其脚本或预制体。本任务不补Boss：当前Boss仍为空，由场景现有专属管理器处理占位逻辑；验收覆盖ClampGuard本体和池接线，不声称完成Boss关卡。

## 验证结果与复查

- `dotnet build ZaoMengXiYou3.slnx --nologo -v:minimal`：成功，0错误、6条工程既有警告。
- Unity6.4(6000.4.9f1)原生资产导入、切片、两预制体和场景登记验证通过。
- 隔离Play Mode共35项检查全部通过，覆盖8/12.5方向伤害、其他来源叠乘、普通定身/霸体、强钳锁3次周期伤害、单次甩出、冰冻打断、实际Wukong位移逃脱、驱散、池复用、非玩家拒绝、喷焰12+6、锥形角落排除、特效回池/复用、独立冷却、自然F10停帧恢复、自然F8触发以及跳过整个窗口的单次补采样。
- 原始PNG哈希与公共基类哈希检查通过。

日志：`output/ClampGuardAudit/dotnet-build.log`、`unity-assets.txt`、`runtime.txt`、`runtime-unity.log`、`slices.json`、`final-checks.json`。
编辑器菜单：`造梦西游/ClampGuard/生成并验证预制体和Level7池`。完整批处理入口：`ClampGuardValidationBridge.RunBatch`；运行于临时空场景，不在正式场景生成测试碰撞体。
