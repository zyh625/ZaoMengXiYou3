# 第六关青幡令使 GreenSummon 验收报告

日期：2026-11-01。Unity 6000.4.9f1 / 2D / C# / URP。

完整预制体：`Assets/Projects/Prefabs/Enemy/GreenSummon.prefab`。
落锤、独立可拆战旗、旗浪三个玩法预制体均已填充，并登记到 Level6Scene。后撤冷却采用用户确认的 **6 秒**。

## 数值、AI 与释放帧

HP **88**，经验 **20**，移动速度配置 **0.8**，普通受击硬直正常，受击箱常开，不使用怒气倍率。Controller / Combat / Movement 三段结构参考 RedBoom，继承既有 EnemyController 调度，没有更改公共基类或其他敌人的脚本/预制体。

| 招式 | 基础伤害 | 独立冷却 | 效果与释放帧（0 起算） |
| --- | ---: | ---: | --- |
| 召令·落锤 SummonedHammerStrike | 22 | 8 秒 | 起手时记录目标初始地面位置并显示半径 2.5 的圈；第 9 帧提交召令；从预警开始满 1.2 秒落地，Combat 只做一次圆形判定 |
| 旗浪 BannerWave | 14 | 6 秒 | 第 7 帧发射；速度 10，最大行程 12；圆形扫掠命中第一个有效玩家后消失，墙体阻挡，不穿透多名角色 |
| 战旗 WarBanner | 无直接伤害 | 16 秒 | 第 15 帧、即插旗动作最后一帧释放；HP 60，持续 8 秒，半径 5 内敌人攻击 ×1.15、受到的伤害 ×0.9 |

与目标水平距离小于 4 时后退、大于 7 时靠近、4～7 时停步施法；技能冷却中在该距离内等待。优先可用战旗，其次没有重叠预警圈的落锤，再次旗浪。三个冷却独立，出生时即可依优先级起手。

同阵营战旗在起手时预约，阻止多个青幡令使同时插出重复旗。取消起手立即释放预约。已放出的落锤、旗浪、战旗由场景级 GreenSummonEffectPool 持有，施法者死亡或回池不会提前删除；战斗结束统一回收。对象池复用会清除上一轮生命状态。

小于 3 单位时可触发 **3 单位 / 0.25 秒**短后撤，冷却 **6 秒**。整个路径进行本体碰撞体扫掠，并检查其他敌人、当前相机波次边界、落点地面及途中每 0.5 单位的同层地面。逐物理步再次扫掠，避免前方新出现的实体；冻结、眩晕、携带、受击及死亡可打断。保留原来的物理摩擦：移动指令为 0.8，默认测试地面在物理解算后读到约 0.73，没有更改移速配置。

## 落锤、战旗与来源清理

落锤预警中心在开始施法时固定。目标走动、角色切换、施法者移动均不会让圈追人。1.2 秒前没有伤害；落地首先提交“已经命中”标记，再由 Combat 非分配圆形查询并按 Health 去重。反伤杀死施法者、重复动画采样或同一角色多个碰撞箱都不造成重复扣血。未提交的召令被打断会取消；已提交的落锤继续落下。

战旗根节点释放于 `BannerHand`，偏移为朝向对应的 x=±0.18、y=0.95；旗身与光环局部下移 0.95，旗杆底部落在地面。之后保持世界坐标，不跟随施法者。根节点挂 Health、ActorStatus、EnemyMovement（复用现有玩家技能的敌人登记接口），另有受击触发箱；没有 EnemyController、EnemyCombat 或 EnemyRewards，因此不会近战、移动、贡献小怪波次击杀或掉落经验。

光环每 0.1 秒更新接收者，使用预分配集合。攻击增益调用 `ApplyAttackBuff(this, 1.15, 0.35)` 刷新同一来源，减伤调用 `SetDamageMultiplier(this, 0.9)`。离开半径、旗被拆、到期、禁用或战斗结束均只移除该旗的两个来源；独立攻击增益、原有减伤、RedBoom/WestKing 同类接口登记的其他来源保留。记录接收者 LifeVersion，避免旧生命记录污染回池重生后的对象。

同一施法者重复请求插旗只刷新自己的现有旗至 8 秒，不回血、不改变位置、不额外叠加。常规 AI 在有己方旗或预约时不会起手第二面旗。

## 动画与素材

七张角色 PNG 均保留原像素、原切片、原 GUID / Sprite ID 和脚底 pivot。1024×1536、4×4、每格 256×384，PPU 100，脚底 pivot 为 (128, 56) 像素，脚底基线对应格内 y=328。原资源连续命名为 `<Action>_01`～`<Action>_16`，无需重切。

| 动作 / 特效 | 实际切片数 | SpriteData fps / 循环 |
| --- | ---: | --- |
| Idle | 16 | 10，循环 |
| Walk | 16 | 14，循环 |
| SummonedHammerStrike | 16 | 16，单次 |
| BannerWave | 16 | 16，单次 |
| WarBanner | 16 | 16，单次 |
| Hit（基类 Attacked） | 16 | 18，单次 |
| Death（基类 Die） | 16 | 12，单次 |
| GreenSummonHammer | 16 | 01～02 预警 6 fps 循环；03～07 下落 16 fps 单次；08～16 落地消散 16 fps 单次，全部 16 帧均绑定 |
| GreenSummonWarBannerAura | 16 | 12，循环，跟随独立旗的 8 秒寿命 |
| GreenSummonWave | 16 | 16，飞行循环，命名 `_0`～`_15` |
| GreenSummonBanner 旗身 | 1 | 静态 Sprite，独立旗杆脚底 pivot，透明背景，PPU 100 |

两套已提供特效仍用各自连续的 `_01`～`_16`，原图和 meta 均未修改。已有的 `GreenSummonHammerFX.prefab`、`GreenSummonWarBannerAuraFX.prefab` 视觉演示资源也保持原样；新玩法预制体单独引用相同图片。

旗浪采用 Effects 中已有 `ChatGPT 图像 2026年11月1日 18_45_30.png` 的绿色新月图。原图及其原三切片 meta 保留；新增逐字节相同的 `GreenSummonWave.png` 副本，仅为副本配置正确的 4×4 / 16 切片。特效画布为实际 1254×1254，格宽高 313/314 像素，不重采样、不重复帧。**此气浪作为占位复用，专属旗浪美术仍标注“待补美术”。**

## 场景接线

`Assets/Projects/Scenes/Levels/Level6Scene.unity` 继续使用已完成的 GreenHammerStageManager / GreenHammerStageEnemyManager。仅在 `StageEnemyManager.enemyPools` 增加以下一项：

| 预制体 | poolSize | weight |
| --- | ---: | ---: |
| GreenHammer（保留） | 10 | 0.55 |
| GreenSummon（新增） | 10 | 0.45 |

最终合入严格只有新增池的三行 YAML，保留此期间用户更新的 CameraRig、场地分段、出生点、Boss 出生点及 UI Prefab overrides。当前场景已有一套有效 EventSystem，原生测试确认只有一个启用。没有修改其他场景、公共配置或其他敌人资源。

Level6 当前没有 NorthKing Boss 预制体，沿用先前小怪流程：普通波次正常运行；最终小怪波结束时等待后续 Boss 交付，不把缺失 Boss 当作已击败，也不开放胜利出口。

## 逐项验收

| 要求 | 结果 |
| --- | --- |
| 1. Assembly-CSharp 编译 0 错误 | 通过。主工程与 Editor 工程均为 0 错误；主工程仅保留既有 Bird.startMissTrophy 未赋值警告，Editor 工程仅保留既有 RedChainSetup 过时接口警告。 |
| 2. 七动作各 16 帧；两特效连续、无空帧 | 通过。角色 112 帧；原特效 32 帧；旗浪 16 帧；新旗身 1 帧。离线 alpha/尺寸/命名与 Unity 原生导入双重检查，无越界、空 Sprite 或重复补帧。 |
| 3. 落锤只结算一次、固定初始位置 | 通过。可走出范围规避；圈不追人；圈内只扣 22；反复采样不重复，换人不伤害离场角色。 |
| 4. 旗子消失完整撤增益，其他来源保留 | 通过。出圈、被拆、8 秒到期、战斗结束均已测试；独立 ×1.3 攻击、×0.5 减伤和 ×1.2 易伤同时存在时，旗来源增删不改动它们。 |
| 5. 文件、fps、编译、接线、待补美术 | 本报告均已列明。四个完整预制体无 Missing Script/丢失对象引用，只有普通小怪血条，没有 Boss UI。 |

Unity 原生 Play Mode **64 项 PASS，ALL PASS，批处理退出码 0**。额外覆盖：三技能实际动画轮转与释放帧、旗浪首个目标/墙体/最大距离/射手死亡后继续、后撤距离与冷却、墙体/敌人/边界/缺地/途中缺口、强控制、旗子的独立寿命及刷新、对象池复用、命中反伤重入。

当前用户编辑器打开 Level5Scene，验证在 `output/GreenSummonUnityValidation/` 的独立 Unity 副本完成；回填资产与通过原生验收的资产逐字节相同，脚本、meta 与输入美术亦核对一致。场景只合入池登记，没有覆盖当前布局。批处理为 nographics，SpriteManager 实际播放与帧事件已测，图片有拼图目检；没有 GPU 游戏画面截图。Unity 编辑器启动时有自身 SearchDatabase 索引异常记录，未进入玩法代码，不影响 64 项运行验收；未修改搜索或全局设置去规避它。

证据：`output/GreenSummonAudit/runtime-validation.txt`、`unity-runtime.log`、`unity-assets.txt`、`dotnet-build.txt`、`dotnet-editor-build.txt`、`art-audit.json`、`contact-sheet.png`、`preservation-validation.json`、`scene-merge.diff`。保留校验确认 12 张输入 PNG、10 份既有角色/特效 meta、23 个已完成 GreenHammer 相关文件与开始本任务时一致。

## 新增 / 修改文件

以下 Unity 新资产均附带 `.meta`，脚本目录有对应目录 `.meta`。

新增运行脚本（`Assets/Projects/Scripts/Enemy/GreenSummon/`）：

- GreenSummonController.cs
- GreenSummonCombat.cs
- GreenSummonMovement.cs
- GreenSummonEffectPool.cs
- GreenSummonHammer.cs
- GreenSummonWave.cs
- GreenSummonWarBanner.cs
- GreenSummonHitUtility.cs
- GreenSummonRuntimeValidation.cs（仅 UNITY_EDITOR 编译）

新增编辑器脚本：

- Assets/Projects/Editor/GreenSummonSetup.cs
- Assets/Projects/Editor/GreenSummonValidationBridge.cs

新增预制体与预警材质：

- Assets/Projects/Prefabs/Enemy/GreenSummon.prefab
- Assets/Projects/Prefabs/SeparateEffort/GreenSummonHammer.prefab
- Assets/Projects/Prefabs/SeparateEffort/GreenSummonWarBanner.prefab
- Assets/Projects/Prefabs/SeparateEffort/GreenSummonWave.prefab
- Assets/Projects/Prefabs/SeparateEffort/GreenSummonWarning.mat

新增素材：

- Assets/Art/Enemies/Level6/GreenSummon/Effects/GreenSummonBanner.png（生成独立旗身）
- Assets/Art/Enemies/Level6/GreenSummon/Effects/GreenSummonWave.png（现有气浪图片的无损副本与正确切片）

修改：`Assets/Projects/Scenes/Levels/Level6Scene.unity`（仅三行池登记）。新增本报告。辅助审计脚本与结果位于 `output/GreenSummonAudit/`，验证副本位于 `output/GreenSummonUnityValidation/`，不属于 Unity 导入资源。

本任务没有修改 GreenHammer、RedBoom、其他敌人/关卡或公共基类。没有 git commit、删除既有资源。

## 战旗旗身生成记录

使用 **内置 image_gen 工具**，按照 `Stand.png` 参考提取并补齐独立旗杆、旗布和金色装饰，去除人物，输出真实透明背景。已目检并存入项目：

`Assets/Art/Enemies/Level6/GreenSummon/Effects/GreenSummonBanner.png`

完整提示词另存 `output/GreenSummonAudit/banner-generation-prompt.txt`：

> Use case: precise-object-edit. Asset: a single transparent 2D Unity game war-banner prop. Edit target/reference: the supplied GreenSummon Stand.png. Extract and complete ONLY the exact jade-green and antique-gold cloth banner and its entire upright staff. Preserve its dark green pole, gold fittings, jade diamond finial, crossbar, green/gold tassels, forked cloth end and cloud-scroll ornament, bold dark outlines and polished cel shading. Remove the person, face, hands, armor, ribbons from the person and all other objects. Fill the tiny sections of staff previously hidden by hands with the same green pole and gold bands. One complete vertical staff with the cloth hanging to screen RIGHT, slight natural billow; staff base fully visible and touching an imaginary horizontal baseline, no drawn ground. Center the vertical pole near the horizontal center of a square canvas, leave generous transparent margins around the complete banner, including finial, cloth, tassels and bottom tip. No aura, particles, UI, lettering, scenery, pedestal, cast shadow or second banner. This is one isolated full prop, not a sprite sheet. True transparent alpha background.
