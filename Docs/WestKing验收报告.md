# 第五关广目天王 WestKing 验收报告

日期：2026-11-01。Unity 6000.4.9f1，2D / C# / URP。

完整预制体：`Assets/Projects/Prefabs/Enemy/WestKing.prefab`。HP 1100、经验 220、移速 1.0、isBoss=true、UsesAnger=false。四招、护珠、三个独立特效、Boss UI 和 Level5Scene 接线已完成。

## 四个招式

| 招式 | 动作与释放帧，0 起算 | 前摇 | 伤害 | 冷却 |
| --- | --- | ---: | --- | ---: |
| 赤龙探爪 | DragonClaw，第 11 帧开箱，第 14 帧关箱 | 0.7 秒 | 25，击退；本体前方 7×2.2 单位 | 4 秒 |
| 宝珠掷 | PearlToss，第 9 帧 | 0.6 秒 | 两次弹跳各 16，第三次最终落地爆炸 22 | 6 秒 |
| 龙息扫场 | DragonBreath，第 12 帧 | 1.0 秒 | 每 0.5 秒 10，喷吐 2 秒；随后地面火区 4 秒 | 10 秒 |
| 宝珠回旋 | PearlOrbit，第 6 帧 | 0.4 秒 | 14/次，单目标接触内置冷却 0.5 秒，持续 5 秒 | 9 秒 |

四招独立冷却，按顺序轮询当前距离内已经就绪的招式；没有合适攻击时靠近目标。爪击/龙息范围 7，投珠选择范围 14，近身 4.5 内可选回旋。停步距离 5.5。
每轮动作收招或被打断后，至少等待 2.2 秒再起手。龙息喷吐期间保持技能状态，喷吐结束后才进入这段间隔；已经投出的宝珠和残留火区独立完成结算，Boss 死亡/停用或战斗结束时统一回收。

投珠的前两次触地分别造成半径 1.2 的 16 点小范围伤害并再次弹起，最终落地产生半径 2.2 的 22 点爆炸；每次落点独立去重，飞行本身不重复产生接触伤害。宝珠使用扫掠检测，墙面反射不计入两次地面弹跳。
回旋使用一颗独立宝珠，半径 2.4、竖向幅度 0.9，跟随 Boss 环绕；每个角色 Health 独立记录半秒接触冷却。

用户确认的补充数值：护珠射线灼烧为 3/秒，持续 2 秒；残留火区每 0.5 秒 10 点，持续 4 秒。
因此持续站在龙息与残留火区内，会受到四次喷吐伤害和八次火区伤害，共 120。地面火焰长 7 单位，喷吐判定高 1.3、残留判定高 0.6，可跳离地面区域。

## 赤龙护珠时序

1. 血量首次到达 70%（770）或 30%（330）时，分别锁定一次触发；回血和再次越过同一阈值不会重置。一击跨过两个阈值时，第二次排队等待第一次护珠及其易伤窗口完成。
2. 进入护珠即取消本轮尚未释放的攻击、关闭爪箱、停止自主移动，设置 `Health.SetDamageMultiplier(this, 0.3f)` 和普通霸体。受击箱常开，正常扣血；普通受击、击退和眩晕不能中断。
3. 护珠持续 12 秒。固定三个池槽分别在场地左/中/右三段生成幻影，每颗 HP 80，距地面随机高度 2.6～4.2。幻影悬停，不追击、不近战，无经验与金币奖励；挂 EnemyMovement 与 Health，玩家现有近战和远程索敌均可攻击。
4. 护珠经过 4、8 秒时，随机一颗存活幻影锁定当前上场角色的位置并显示红线，完整预警 0.8 秒后沿该线采样一次：12 点伤害，命中后追加两秒灼烧（每半秒 1.5，共 6）。墙体截断射线，幻影被击破或护珠结束时取消尚未释放的预警。
5. 三颗全部击破：移除 0.3 减伤与普通霸体，立即进入三秒硬直和 ×1.5 受伤窗口；期间不移动、不出招。三秒后只移除本 Boss 的倍率来源，恢复正常 AI 和 2.2 秒收招间隔。
6. 12 秒内未清完：先退出护珠并回收幻影，再对结算时的当前角色造成一次 35 点伤害并请求击退；换出的角色不会代替当前角色承受惩罚。该次护珠不会再次结算超时伤害。

明确选择：**固体冰冻暂停护珠计时及对应射线预警，融化阶段继续**。三秒易伤窗口保持固定三秒，不因冰冻延长。幻影自身处于固体冰冻时也暂停自己的预警。游戏全局暂停照常遵循游戏时间。
两次护珠不会同时存在。Boss 死亡、停用、关卡结束时清理本 Boss 的减伤/易伤、霸体和全部池化效果。

倍率使用既有按来源叠乘接口，0.3 与其他 0.5 减伤得到 0.15；易伤阶段 1.5 与 0.5 得到 0.75，结束后仍保留其他来源的 0.5。本次无需修改 Health 或公共基类。

## 动画与切片

角色只使用 `Assets/Art/Bosses/WestKing/SpriteSheets16/` 下的九张成品。每张为 1024×1536、4×4、每格 256×384，恰好 16 个有效帧；Multiple、PPU 100、名称为 `<Action>_0`～`<Action>_15`。
沿可见像素裁紧并保留约 2 像素安全边距，脚底基线统一为帧格内从上向下第 352 像素，pivot 取脚底中心；切换动作时校准视觉高度，脚底原点保持一致。所有动作采用一致的原图缩放系数，未重绘或重复帧凑数。

| 动作/图集 | 切片数 | SpriteData fps | 播放方式 |
| --- | ---: | ---: | --- |
| Idle | 16 | 10 | 循环，基类 Idle |
| Walk | 16 | 14 | 循环，基类 Walk |
| DragonClaw | 16 | 16 | 单次，子类字段 |
| PearlToss | 16 | 16 | 单次，子类字段 |
| DragonBreath | 16 | 16 | 单次；喷吐未结束时保持动作 |
| PearlOrbit | 16 | 16 | 单次，子类字段 |
| Ward | 16 | 16 | 单次结束后保持姿势至护珠结束 |
| HitReaction | 16 | 18 | 单次；基类 Attacked 与破珠硬直字段 |
| Death | 16 | 12 | 单次，基类 Die |
| Effects/PearlProjectile | 16 | 16 | 投掷/回旋循环 |
| Effects/DragonFlame | 16 | 16 / 12 / 40 | 4～11 帧用于喷吐循环 16 fps、火区循环 12 fps；全部 16 帧用于落点表现单次 40 fps |
| 根目录 OrbSprites | 16 | 12 | 护珠幻影循环 |

九张角色图 + 三张效果图共 **192 个有效切片**，名字连续、帧内容唯一、无空白与越界，Unity 网格非空。角色 Idle/Walk/Attacked/Die 与六个子类动作字段均已填充。
根目录旧版 Idle/Walk/Attacked、Stand、所有 `.draft.png` 和提示词文件保留；没有混入角色动画。原始输入的本次备份位于 `output/WestKingAudit/originals/`。

## 预制体与第五关接线

| 预制体 | 已填内容 |
| --- | --- |
| `Assets/Projects/Prefabs/Enemy/WestKing.prefab` | WestController / WestCombat / WestMovement / WestEffectPool、Health 1100、ActorStatus、EnemyRewards 220 且 isBoss=true、Rigidbody2D、本体箱、Visual、常开受击箱、爪击箱、检查箱、Muzzle |
| `Assets/Projects/Prefabs/SeparateEffort/WestKingPearl.prefab` | SpriteManager、16 帧循环、两次弹跳/最终爆炸、五秒回旋与接触冷却 |
| `Assets/Projects/Prefabs/SeparateEffort/WestKingFlame.prefab` | SpriteManager、喷吐/火区/落点表现 SpriteData、半秒区域结算、自动回收 |
| `Assets/Projects/Prefabs/SeparateEffort/WestKingOrb.prefab` | Health 80、ActorStatus、悬停刚体、可攻击受击箱、EnemyMovement、空近战 Combat、OrbSprites 循环、射线发射点与预警线 |

Visual 与 EastKing 使用同一排序层和角色材质，Boss 的 Order 为 2。Boss UI 参照 EastKing 接在场景 BattleUI，使用新增 WestBossUI，血条随实际 Boss 变化；SpecialState 以剩余时间填充，护珠为红色、易伤为金色。名称/血条图使用现有 `GuangMuBlood.png`。

Level5Scene 的现有 StageEnemyManager 子类已登记 WestKing，出生点继续使用 `GuangmuTianwang_BossSpawn`（43，-1.13）。关卡与敌人管理器均保持启用。
新增 `WestKingArena/Left`、`Right`，根据末波相机边界登记约 27.111～48.889 的场地范围，供幻影左/中/右分布使用。
现有 RedChainStageManager / RedChainStageEnemyManager 在 Boss 引用存在时自动沿用标准 Boss 流程，其脚本未改。小怪池保持 RedChain 10/0.55、RedBoom 10/0.45。
只修改第五关场景实例上的 UI，不修改共享 BattleUI 预制体、其他关卡或其他 Boss。

## 逐项验收

| 验收项 | 结果 |
| --- | --- |
| 1. `dotnet build Assembly-CSharp.csproj` 零错误 | 通过：0 错误，仅既有 Bird.startMissTrophy 未赋值警告。Editor 工程也为 0 错误，仅既有 RedChainSetup 过时接口警告。 |
| 2. 角色每动作 16 帧，特效按内容，名字连续 | 通过：12 个图集各 16 帧，共 192；PNG/Meta 逐帧审计及 Unity 原生导入检查均通过。 |
| 3. 阈值各一次、破珠三秒易伤、超时只结算一次 | 通过：测试回血再次越阈值、一击跨两阈值排队、三珠击破、三秒恢复、12 秒超时、换人承伤及后续帧不重复惩罚。 |
| 4. 减伤正确并只清除自己的来源 | 通过：10 点输入变 3；叠加 0.5 后为 1.5；破珠阶段为 7.5；易伤结束后保留其他来源，仍为 5。超时后输入 10 恢复 10。 |
| 5. Prefab 与 UI | 通过：四个 Prefab 无 Missing Script/丢失对象引用；实际 Level5Scene 创建 Boss，WestBossUI 可见，血条 70% 与护珠倒计时正确刷新。 |
| 6. 文件、帧率、编译、场景与时序说明 | 已在本报告完整列出。 |

Unity 原生批处理 Play Mode 共 **55 项 PASS，ALL PASS，退出码 0**。测试首先真实加载 Level5Scene，再使用专用地面和玩家验证机制、物理、伤害及对象池；最后启用完整 AI 与 SpriteManager，实际播放四招，确认龙息正常收招不会丢失第四次伤害。
还验证了爪箱按帧开关和重复采样去重、投珠 16+16+22、回旋 14 与半秒内置冷却/五秒回收、喷吐四跳/火区八跳、非结算时 1000 次火焰更新分配 0 个托管字节。
批处理为 nographics：UI 检查组件可见性、绑定和 Image 数据；美术通过拼图目检，未做 GPU 屏幕截图验收。

验收证据保存在 `output/WestKingAudit/`：`runtime-validation.txt`、`unity-runtime.log`、`unity-assets.txt`、`dotnet-build.txt`、`dotnet-editor-build.txt`、`art-validation.txt`、`final-sprites.json`、`contact-sheet.png`。

复查菜单：`造梦西游/WestKing/生成并验证广目天王`；打开 Level5Scene 后运行 `造梦西游/WestKing/运行护珠与物理验收`。验证对象只在 Play Mode 中存在，不保存测试场地。

## 文件清单

新增运行脚本，均含 `.meta`（目录 `Assets/Projects/Scripts/Boss/WestKing/`）：

- `WestController.cs`
- `WestCombat.cs`
- `WestMovement.cs`
- `WestClawBox.cs`
- `WestEffectPool.cs`
- `WestHitUtility.cs`
- `WestKingBurn.cs`
- `WestKingPearl.cs`
- `WestKingFlame.cs`
- `WestKingOrb.cs`
- `WestKingRuntimeValidation.cs`，仅 `UNITY_EDITOR` 编译

其他新增文件，均含相应 Unity `.meta`：

- `Assets/Projects/Scripts/Boss/WestKing.meta`
- `Assets/Projects/Scripts/UI/Common/WestBossUI.cs`
- `Assets/Projects/Editor/WestKingSetup.cs`
- `Assets/Projects/Editor/WestKingValidationBridge.cs`
- 上表四个完整 `.prefab`
- `Assets/Projects/Prefabs/SeparateEffort/WestKingWarning.mat`

修改的资源：

- `Assets/Art/Bosses/WestKing/SpriteSheets16/` 下的 Idle、Walk、DragonClaw、PearlToss、DragonBreath、PearlOrbit、Ward、HitReaction、Death：各 PNG 与 PNG.meta。
- `Assets/Art/Bosses/WestKing/SpriteSheets16/Effects/DragonFlame.png`、`PearlProjectile.png` 及两份 PNG.meta。
- `Assets/Art/Bosses/WestKing/OrbSprites.png` 与 PNG.meta。
- `Assets/Projects/Scenes/Levels/Level5Scene.unity`。
- `Docs/WestKing验收报告.md`。

本次未修改公共基类或 Health；未修改其他 Boss/敌人脚本、其他场景或共享 BattleUI；保留会话此前已有的 RedChain/RedBoom 工作。未删除现有资源，未执行 git commit。
