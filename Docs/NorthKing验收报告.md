# 第六关多闻天王 NorthKing 与整关流程验收报告

日期：2026-11-01。Unity 6000.4.9f1 / 2D / C# / URP。

交付：`Assets/Projects/Prefabs/Enemy/NorthKing.prefab`，以及 NorthKingRib、NorthKingRain、NorthKingShield 三个独立池化玩法预制体。所有动画、引用和第六关场景接线已填充。

## 数值与普通 AI

HP **1400**，经验 **240**，`isBoss=true`，移速 **1.0**，普通出招收招/中断后间隔 **2.2 秒**。沿用 WestKing 的 BossController / BossCombat / EnemyMovement 骨架，继承关闭怒气的规则，`UsesAnger=false`。

| 招式 | 基础伤害 | 独立冷却 | 实际行为 |
| --- | ---: | ---: | --- |
| 伞击 UmbrellaBash | 25 | 4 秒 | 身前宽 4、高 2.2 的箱形判定，击退；一招只命中同一角色一次 |
| 旋伞 UmbrellaVortex | 每次 8 | 9 秒 | 释放后持续 2 秒；6 单位范围内追加朝伞心的水平拉速，接触半径 1.5，角色接触伤害内置冷却 0.4 秒，连续贴身合计 5 次 / 40 点 |
| 伞骨飞刺 RibVolley | 每发 10，共 5 发 | 8 秒 | 正面 -24°、-12°、0°、12°、24° 扇形，直线速度 8，最大距离 14；首个有效角色命中消失，墙体阻挡 |
| 雨幕 RainCurtain | 每发落地 12，共 6 发 | 12 秒 | 动画中真实小跳；落地向场地六个分段位置释放雨弹，从上方 7 单位落下，速度 9；每发落地半径 1 的小 AOE 只结算一次 |

3～5 单位优先伞击；贴身小于 3 时优先旋伞；中距离使用伞骨弹；HP 不高于 40% 且雨幕冷却完成时优先雨幕。其余招式按有效距离和独立冷却回退选择；没有可用技能时靠近到 stopDistance=3.8。

拉拽为本 Boss 专属的速度叠加组件，最大水平拉速 2.5。每个物理步先剥离上一拉速，再让玩家输入和技能运行，最后追加本步速度，不改坐标、输入、跳跃次数、竖直速度或玩家状态机。移动速度 5 朝外时仍保有约 2.5 的外移指令，跳跃和技能位移可抵抗。冻结/融化、普通受击动画、眩晕、死亡或无敌时不强行拉拽；本体扫掠地形避免拉入墙体。没有使用 BeginCarry，没有修改 PlayerMovement 或其他公共基类。

Rib、Rain、Shield 均由场景级 NorthEffectPool 管理，使用现有 PrefabPool 预热、复用，效果结束禁用回池。已释放弹幕独立于 Boss 的启停，战斗结束统一回收。伤害在发射时快照；生命版本阻止旧投射物的反伤误伤重生后的 Boss。判定使用复用数组/集合，没有每帧创建命中列表。

## 帧窗口与动画

以下运行帧为 **0 起算**；提供的图片仍保持原来的 `_01`～`_16` 命名，运行第 5 帧即第 6 张切片。

| 招式 / 状态 | 帧窗口 / 事件 |
| --- | --- |
| UmbrellaBash | 第 5 帧开箱，第 9 帧关箱；有效区间 5～8，与 EnemyCombat 的 AttackOpenIdx / AttackShutIdx 一致 |
| UmbrellaVortex | 第 4 帧释放，持续 2 秒；主动作进入后段时循环原图第 5～12 张旋转姿势，结束接第 13～16 张收招 |
| RibVolley | 第 7 帧一次释放 5 发，后续重复采样不再生成 |
| RainCurtain | 第 3 帧给予真实向上初速；到第 11 帧尚未着地时保持该帧，实际落地后一次释放 6 发并继续收招；使用本来重力，不产生零重力残留 |
| Guard | 阈值进入时播放，16 fps 单次，播完保持 Guard_16；护伞期间禁止普通出招和自主移动 |
| 破伞 | 立即回到基类 Idle 动画姿势并占用 3 秒硬直，禁止移动/起手；这段时间受到伤害 ×1.5 |

所有角色 PNG / meta 都保留原图、原切片、GUID / Sprite ID、PPU 和脚底 pivot；根目录 2048×2304 旧版 Idle / Walk / Attacked 未使用、未修改。

| 动作 | 帧数 | fps / 循环 |
| --- | ---: | --- |
| Idle | 16 | 10，循环 |
| Walk | 16 | 14，循环 |
| UmbrellaBash | 16 | 16，单次 |
| UmbrellaVortex | 16 | 16，单次；额外旋转子片段 8 帧循环 / 收招 4 帧单次，均 16 fps |
| RibVolley | 16 | 16，单次 |
| RainCurtain | 16 | 16，单次，实际落地时续播 |
| Guard | 16 | 16，单次，末帧保持 |
| Hit（基类 Attacked） | 16 | 18，单次 |
| Death（基类 Die） | 16 | 12，单次 |
| NorthKingUmbrellaRib | 16 | 16，飞行循环 |
| NorthKingRainCurtain | 16 | 下落 16 循环，落地消散 20 单次 |
| NorthKingShield | 16 | 12，护伞期间循环，随 Boss 正面位置更新 |

角色图为 1024×1536、4×4、每格 256×384，PPU 100，pivot=(128,56) 像素，格内脚底基线 y=328。特效为 1024×1024、4×4、256×256，保留各自现成 pivot。共 **192 个有效切片**，命名连续，无空帧或越界；没有重切、重采样或额外用重复切片补数量。提供的图片适用，本次没有新增/重生成美术，也没有待补美术占位。

## 宝伞时序与来源处理

1. **触发**：本次生命首次 HP≤70%、首次 HP≤40% 各锁存一次。回血或退出护伞不会重置。如果一击跨过两个阈值，先进入第一轮，第二轮等待前一轮护伞及其破伞硬直结束，绝不叠加。
2. **展开**：停止普通技能和移动，按进入时当前角色的位置固定护伞朝向，普通霸体开启，耐久设为 150，计时 6 秒；玩家绕后不会让伞自动转向。
3. **正面命中**：Health.DamagePreparing 按来源坐标判断正面，登记本 Boss 来源的 ×0.1 伤害倍率和 0.2 反伤。伞面扣除减伤前原始伤害；Boss 本身只承受该击的 10%，其他独立减伤/易伤仍照常叠乘。反伤经 Health 的现有接口结算，DamageInfo.Source 为 Boss 根 Transform，反射伤害不会再反弹成循环。
4. **背面命中**：不登记本 Boss 的减伤或反伤，不扣伞面耐久，Boss 正常受伤。无方向信息的周期伤害沿用现有状态伤害规则，不当作正面伞击。
5. **弹幕**：护伞有效计时第 2 / 4 / 6 秒各从正面发射一波 3 发、每发 10 的伞骨弹，固定朝护伞方向，玩家可绕后。
6. **破伞**：耐久归零的这一击仍按 10% 扣 Boss 血，并完成该击的 20% 反伤。等 Health.DamageResolved 后才撤销护伞来源、清空耐久、关闭护盾表现和普通霸体，切 Idle 硬直并登记 ×1.5 易伤，持续整 3 秒。结束只移除本 Boss 的易伤来源，恢复 AI，普通攻击仍受 2.2 秒间隔限制。
7. **超时**：完整 6 秒未破伞则收起，不进入硬直；耐久、护盾、减伤、反伤和普通霸体立即清理，恢复正常 AI。
8. **强控 / 结束**：明确采用暂停方案，固体冰冻与携带/吸附等外控同时暂停护伞与弹幕计时，代码中已有注释；破伞易伤按游戏时间固定 3 秒。死亡、禁用和新生命复用会清理本来源。所有移除操作都指定本组件来源，不清空其他增益字典。

NorthBossUI 继承 BossUI，通过现有 SpecialState 图显示护伞耐久 /150，破伞后显示三秒窗口剩余比例；Boss 血条直接绑定关卡创建的实际 Health，死亡后保持零血。场景中使用现有 DuoWenBlood 头像，没有新增 UI 美术或修改共享 BattleUI 预制体。

## Level6 整体接线与流程

保留当前相机、布局、EnemyBornRig 和四波配置；沿用已启用的 GreenHammerStageManager / GreenHammerStageEnemyManager。填入 NorthKing 后，这两个组件直接走既有通用 Boss 流程，没有修改它们的脚本。

| 项目 | 结果 |
| --- | --- |
| 小怪池 | GreenHammer 10 / 0.55，GreenSummon 10 / 0.45，保持不变 |
| 四波数量 | 左 [3,4,7,7]，右 [3,5,6,8]，总共 43 只；每波 [6,9,13,15] |
| 节奏 | 正式配置仍为首波/换区等待 5 秒、刷新间隔 10 秒、同屏上限 6；验收探针仅在运行期缩短等待以测试全流程 |
| Boss 引用 | NorthKing.prefab，既有 DuoWenBorn 对象保留，世界坐标约 (46.47,-3.29,0)，脚底对齐地面 |
| 玩家出生 | 保留 PlayerSpawn 对象与 x=-5.26，y 调整至约 -2.39，按玩家物理本体脚底对齐地面 |
| Boss 场地 | 新增本场景 NorthKingArena 左右 Transform，约 27.11111～50.88889，来自最终波相机边界及 16:9 视口半宽，供六发雨幕分段落点使用 |
| UI | 当前 BattleUI 场景实例的 BossUI 替换为 NorthBossUI，HP / SpecialState 引用齐全；共享 UI 资产保持不变 |
| 出口 | 保留原对象与 x=46，y 对齐约 -2.17；Boss 死亡动画结束才启用；角色实际步行进入触发范围已通过，沿用既有交互和 VictoryScene 结算逻辑 |
| EventSystem | 一套有效启用；没有重复输入系统 |

原生流程已经覆盖 **四波全部刷出/清场 → Boss 激活并显示血条 → Boss 死亡动画 → 出口启用 → 角色行走进入出口触发器**。实际场景与原生验证版本逐字节一致。

构建入口：全局 `ProjectSettings/EditorBuildSettings.asset` 尚未登记 Level6Scene。用户已明确选择 **“不改全局配置，保留编辑器运行”**，因此全局列表保持原样，交付入口为直接打开 Level6Scene 后运行 Play Mode。场景内部四波、Boss、UI 与出口流程均已完成。预备的三行补丁 `output/NorthKingAudit/build-settings-proposed.patch` **未应用**；本次不包含打包后的第六关入口登记。

## 逐项验收结果

| 验收要求 | 结果 |
| --- | --- |
| 1. dotnet build Assembly-CSharp.csproj 0 错误 | 通过，0 错误；仅既有 Bird.startMissTrophy 未赋值警告。Editor 工程也为 0 错误，仅既有 RedChainSetup 过时接口警告。两工程的编译列表均包含本次全部新脚本。 |
| 2. 九动作各 16 帧、三特效连续 | 通过，共 192 帧，离线 alpha / 尺寸 / 命名检查与 Unity 原生导入均通过；24 个 PNG/meta 文件与输入哈希一致。 |
| 3. 两阈值、正背面、150 耐久、破伞三秒 | 通过，包含回血不重置、单击跨两阈值顺序触发、Guard_16 末帧保持、背面不扣伞面、破伞这一击的 10% 及 20% 反伤、完整三秒移动/起手禁止、+50% 易伤。 |
| 4. 结束清理且不影响其他来源 | 通过，独立 ×0.5 减伤和 0.1 反伤同时存在的情况下，护伞/易伤来源加入、离开和超时均保留它们；玩家自身反伤不会产生递归反弹。 |
| 5. Prefab 引用和 Boss UI | 通过，四预制体全部本地对象、外部对象及 Sprite 引用已检查；场景无 Missing Script，血条/护伞填充图在 UI 层级实际启用，数值及死亡零血更新正确。 |
| 6. 文件、窗口、接线、时序 | 见本报告。 |

**Unity 原生 Play Mode 共 78 项 PASS，ALL PASS，批处理退出码 0。** 还验证了伞击去重/开关帧、两秒旋伞五段伤害和可抵抗拉速、冻结/受击不拉、五发弹幕和墙体阻挡、真实起跳落地六发雨幕、一次落地伤害、射手停用后弹幕继续、对象池复用与战斗结束回收。

原生验收在复用的独立验证项目 `output/GreenSummonUnityValidation/` 进行，使用独立产品名与持久化目录，避免写入用户游戏存档，也没有关闭用户当前打开的场景。模式为 batchmode / nographics；SpriteManager、动画事件、Physics2D、UI 绑定/层级和整关调度均实际运行，另有素材拼图目检；不包含 GPU 游戏画面截图。UnityEditor 的 SearchDatabase 启动索引异常记录保留在日志中，未修改全局搜索配置去规避；玩法测试全部通过。

证据目录：`output/NorthKingAudit/`，包含 runtime-validation.txt、unity-runtime.log、unity-assets.txt、scene-wiring.txt、dotnet-build.txt、dotnet-editor-build.txt、art-audit.json、contact-sheet.png、preservation-validation.json、delivery-validation.json、scene-merge.diff、native-exit.txt。

## 新增 / 修改文件清单

所有新 Unity 资产均有对应 `.meta`，脚本目录有目录 `.meta`。

新增运行脚本（`Assets/Projects/Scripts/Boss/NorthKing/`）：

- NorthController.cs
- NorthCombat.cs
- NorthMovement.cs
- NorthBashBox.cs
- NorthEffects.cs
- NorthEffectPool.cs
- NorthHitUtility.cs
- NorthVortexPull.cs
- NorthKingRib.cs
- NorthKingRain.cs
- NorthKingShield.cs
- NorthKingRuntimeValidation.cs（仅 UNITY_EDITOR）

新增 UI / 编辑器脚本：

- Assets/Projects/Scripts/UI/Common/NorthBossUI.cs
- Assets/Projects/Editor/NorthKingSetup.cs
- Assets/Projects/Editor/NorthKingValidationBridge.cs

新增完整预制体：

- Assets/Projects/Prefabs/Enemy/NorthKing.prefab
- Assets/Projects/Prefabs/SeparateEffort/NorthKingRib.prefab
- Assets/Projects/Prefabs/SeparateEffort/NorthKingRain.prefab
- Assets/Projects/Prefabs/SeparateEffort/NorthKingShield.prefab

修改：`Assets/Projects/Scenes/Levels/Level6Scene.unity`；同步本地生成的 Assembly-CSharp / Editor 工程编译列表。新增本报告与 output/NorthKingAudit 审计资料。

未修改其他 Boss、小怪、公共基类/Health 语义、其他关卡场景或全局配置；输入美术及 meta 全部保留。95 个受保护的既有脚本/预制体/其他关卡场景哈希一致。没有 git commit，没有删除既有资源。构建场景列表按用户确认保持原样，仅交付编辑器运行。
