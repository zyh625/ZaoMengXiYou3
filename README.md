# 造梦西游 3 · Unity 2D 项目

基于 Unity 6、C# 和 URP 的横版动作游戏项目，包含四角色切换、技能养成、关卡波次、Boss 战、运行时训练场与轮回系统。

当前逻辑主线为第 1～9 关，轮回终点为第 9 关。第 8、9 关新增敌人与 Boss 已接线；现有构建列表仍只登记第 1～6 关，后续由场景作者安排第 7～9 关入口和构建列表。详细数值见 [Docs/数值总表.md](Docs/数值总表.md)；最新改动见 [第8至9关五预制体与轮回验收报告](Docs/第8至9关五预制体与轮回验收报告.md)。

## 开发环境

| 项目 | 版本 / 说明 |
| --- | --- |
| Unity Editor | **6000.4.9f1**，以 `ProjectSettings/ProjectVersion.txt` 为准 |
| 渲染管线 | Universal Render Pipeline **17.4.0** |
| 输入系统 | Input System **1.19.0** |
| 游戏类型 | 2D 横版动作，C# |
| UI | UGUI / TextMeshPro |

依赖由 [Packages/manifest.json](Packages/manifest.json) 管理，首次打开工程时由 Unity Package Manager 还原。

## 快速运行

1. 在 Unity Hub 中添加本仓库根目录，使用上述 Unity 版本打开，等待资源导入和脚本编译完成。
2. 打开 [MenuScene](Assets/Projects/Scenes/MenuScene.unity)，进入 Play Mode。
3. 创建或选择存档，经地图进入已解锁关卡。
4. 测试操作和敌人时，可从地图右下角的“训练场 / TRAINING”按钮进入，退出返回地图。

训练场也可以直接打开 [TrainingScene](Assets/Projects/Scenes/Training/TrainingScene.unity) 运行，无需选择正式存档。验证完整养成和关卡流程时，应从菜单开始。

构建场景列表见 [EditorBuildSettings.asset](ProjectSettings/EditorBuildSettings.asset)，当前包含菜单、地图、第 1～6 关、胜利、失败及训练场。构建前在 Unity 的 Build Profiles 中确认目标平台和 Scene List；训练场独立于正式关卡解锁链路。

## 默认操作

以下为正式角色输入资产 [PlayerActions.inputactions](Assets/Projects/Input/PlayerActions.inputactions) 的键位：

| 按键 | 操作 |
| --- | --- |
| A / D | 左右移动 |
| J | 普通攻击 |
| K | 跳跃 |
| U / I / O | 三个技能，正式战斗需先学习 |
| Q / E | 切换上一个 / 下一个已解锁角色 |
| Space | 无双 |
| W | 交互，包括开放后的关卡出口 |
| S | 空中快速下降；也用于部分角色组合技 |

组合技和资源消耗由各角色脚本决定。切换角色沿用 `PlayerManager`，训练场与正式关卡使用同一套操作。

## 关卡、伙伴与轮回

### 第一关和伙伴解锁

第一关采用通用 `PlayerSystem`、`BattleUI`、`CameraRig` 和波次管理框架，保留原关卡背景、平台美术。基础配置为 **3 波、每波 8 只狼，共 24 只，无 Boss**；清完后开放出口，交互进入胜利结算。

| 时点 | 新解锁角色 |
| --- | --- |
| 新建存档 | 孙悟空 |
| 第 1 关胜利 | 猪八戒 |
| 第 2 关胜利 | 唐僧 |
| 第 3 关胜利 | 沙僧 |

正式关卡仅创建已解锁伙伴，技能界面也检查解锁状态。训练场临时开放全部四人及技能，不改变正式进度。

第 5 关包含 RedChain、RedBoom 与 WestKing；第 6 关包含 GreenHammer、GreenSummon 与 NorthKing。对应脚本在 `Scripts/Enemy`、`Scripts/Boss`，敌人及 Boss 预制体统一位于 `Prefabs/Enemy`，独立投射物与特效位于 `Prefabs/SeparateEffort`。

### 轮回流程

第 9 关胜利结算询问“再入轮回”：

1. 通过现有存档系统保存 `rebirth + 1`。
2. 保存成功后清理本次战斗数据，进入第 1 关。
3. 保留角色等级、经验、技能、被动、金币、已解锁伙伴和关卡。

本次不轮回时，资格以 `rebirthAvailable` 保存，之后可在地图点击“再入轮回”。一次通关资格只消费一次，下轮再次通关第 9 关后重新获得。保存失败会恢复轮回数和资格，允许重试。关卡解锁继续向第 9 关推进，轮回保留全部已解锁关卡。地图及六个存档槽均显示轮回数；未选档 / 空槽显示第 0 轮回。

令 `N = Clamp(rebirth, 0, 10)`，难度在正式第 1～9 关应用。轮回次数仍可累计，N=10 后难度不再增长。

| 项目 | 公式 | N=1 | N=10 |
| --- | --- | --- | --- |
| 敌人 HP | `1 + 0.35N` | ×1.35 | ×4.50 |
| 敌人伤害 | `1 + 0.15N` | ×1.15 | ×2.50 |
| 金币金额 | `1 + 0.25N` | ×1.25 | ×3.50 |
| 经验 | `1 + 0.20N` | ×1.20 | ×3.00 |
| 小怪数量 | `1 + 0.08N` | ×1.08 | ×1.80 |
| 攻击冷却 | `Max(0.75, 1 - 0.03N)` | ×0.97 | ×0.75 |
| 追击移速 | `1 + 0.02N` | ×1.02 | ×1.20 |
| 刷怪间隔 | `Max(0.70, 1 - 0.03N)` | ×0.97 | ×0.70 |
| 同屏小怪上限 | `+Min(1, Ceil(0.1N))` | +1 | +1 |

公式集中在 [RebirthScaling.cs](Assets/Projects/Scripts/Core/RebirthScaling.cs)。数量先按整关总数向上取整，再分配至各波左右出生侧，进度与生成使用同一份运行时数量；基础预制体不写入轮回倍率。Boss 同样接受 HP / 伤害缩放，金币仍遵循已有每档每关首杀规则。

## 运行时训练场

训练场不读写正式存档、不发金币经验、不进入正式胜败结算，也不应用轮回倍率。

| 功能 | 说明 |
| --- | --- |
| 敌人切换 | PREVIOUS / NEXT 切换目标，重置镜头目标和统计 |
| COMBAT | 完整 AI、攻击、受击与死亡 |
| DUMMY | 停止移动、转向和攻击；可设置最低 1 血，持续测伤害 |
| RESPAWN | 死亡 2 秒后复用同一实例满血复活 |
| INFINITE MP / VIGOR | 无限法力和气力，关闭后恢复正常规则 |
| NO SKILL COOLDOWN | 技能无冷却 |
| NO HP LOSS | 玩家免扣血，保留受击与命中反馈 |
| HITBOXES | 显示攻击箱与检查箱 |
| RESET STATISTICS | 清空统计，不重置目标 HP |
| RESTART TARGET | 重新生成当前目标 |

面板显示最近 3 秒 DPS、累计伤害、命中数、暴击数 / 暴击率、单次最高伤害、首次命中至击杀耗时和目标 HP。只统计玩家主动伤害，排除反伤与周期伤害。

敌人目录资产为 [TrainingEnemyCatalog.asset](Assets/Projects/Resources/Training/TrainingEnemyCatalog.asset)，目前包含 15 类敌人 / Boss。以后新增可测试敌人，直接在 Inspector 中添加目录条目和预制体引用，无需修改训练场代码。

## 目录与代码入口

```text
Assets/
  Art/                         角色、敌人、Boss、特效和 UI 美术
  Projects/
    Scenes/                    菜单、地图、结算场景
      Levels/                  正式关卡
      Training/                独立运行时训练场
    Prefabs/
      Level/                   PlayerSystem、BattleUI、CameraRig
      Enemy/                   敌人和 Boss
      SeparateEffort/          投射物、独立特效
    Scripts/
      Core/                    GameSession、BattleRun、轮回公式、暂停
      Save/                    SaveData、SaveSystem
      Level/                   波次、生成、玩家切换、教程关管理
      Player/                  四角色操作、技能与资源
      Enemy/                   小怪 Controller / Combat / Movement
      Boss/                    Boss AI、技能与特殊机制
      Combat/                  Health、伤害与状态
      Animation/               SpriteManager 与帧动画
      Training/                训练场、目录、统计与调试显示
      UI/                      战斗、地图、结算和 Boss UI
    Resources/                 训练目录、教程材质等运行时资源
    Input/                     输入动作资产
    Editor/                    资产接线与验收工具
  Level1/                      保留的第一关原始美术和旧版资源
Docs/                          数值设计与各功能验收报告
Packages/                      包依赖
ProjectSettings/               Unity 项目配置
output/                        本地验收日志、截图与独立 QA 工程（Git 忽略）
```

关卡通常由 `StageManager / StageEnemyManager` 管理；第一关通过 `TutorialStageManager / TutorialEnemyManager` 复用通用框架并跳过 Boss 阶段。`GameSession` 保存当前选档和会话，`BattleRun` 记录本次战斗，`VictoryControl` 负责胜利结算、伙伴加入和轮回入口。

## 存档与资源编辑

存档由 [SaveSystem.cs](Assets/Projects/Scripts/Save/SaveSystem.cs) 统一读写，默认位于 `Application.persistentDataPath` 下的 `save<槽位号>.json`。具体目录取决于运行平台及 Player Settings，不保存在工程的 `Assets` 下。旧存档缺少 `rebirth` 时按 0 处理，伙伴解锁根据已有进度和养成记录兼容补齐。

既有 UI 保留可编辑的场景对象；新的地图轮回入口提供未放置的 [RebirthEntry.prefab](Assets/Projects/Prefabs/UI/RebirthEntry.prefab)，由场景作者拖入 Canvas，自行替换 Image 的 Sprite 和调整 RectTransform。可选确认面板及文字引用在 `RebirthEntry` 的 Inspector 中指定，不指定确认面板时点击按钮直接轮回。运行时不覆盖 Sprite 或布局。既有 UI 编辑位置如下：

| 场景 / Hierarchy 名称 | 编辑位置 |
| --- | --- |
| VictoryScene / `RebirthButton` | `Image.Source Image`、Color、Type；RectTransform 锚点、尺寸、位置 |
| VictoryScene / `RebirthTitle`、`RebirthMessage` | 标题 / 提示样式、字号、位置；保留结算脚本引用 |
| MapScene / `RebirthLabel` | 轮回文字位置、字号、尺寸 |
| MenuScene / 各存档槽 `RebirthLabel` | 各标签布局及 `MenuButton.rebirthLabels` 数组引用 |
| Level1Scene / `TutorialHelpPanel`、`TutorialNotice` | 提示底图、文字和 RectTransform |

这些 UI 的运行时代码更新内容与显隐，不覆盖手工设置的 Sprite 和布局。修改当前关卡 HUD 时，优先编辑场景实例，避免把局部调整应用到所有关卡共用的预制体。

`Editor` 下的 Setup 工具用于手动生成和接线；普通运行无需执行。已有场景或美术调整后，应先检查工具行为再运行生成菜单，以免重新生成资产覆盖手工编辑。

## 编译与验收

在根目录执行：

```powershell
dotnet build Assembly-CSharp.csproj
dotnet build Assembly-CSharp-Editor.csproj
```

`.csproj` 由 Unity 生成且不纳入版本控制；首次检出若缺少工程文件，先由 Unity 生成 C# 工程。上述命令检查脚本编译，资源引用、动画和玩法仍需在 Unity 中验证。

截至 **2026-11-02** 的既有验收记录：运行时及 Editor 程序集均 0 错误，轮回 / 第一关定向验收 **357 项 PASS**，训练场回归 **65 项 PASS**。详细数据和已知警告见对应报告；这些是已完成的定向验收记录。

| 文档 | 内容 |
| --- | --- |
| [数值总表](Docs/数值总表.md) | 关卡、角色、敌人和轮回设计依据 |
| [第8至9关五预制体与轮回验收报告](Docs/第8至9关五预制体与轮回验收报告.md) | 五个新预制体、84 项原生运行检查、第 9 关轮回与地图按钮接线；覆盖旧报告中的第 6 关轮回规则 |
| [轮回与第一关验收报告](Docs/轮回与第一关验收报告.md) | 解锁、公式、应用点、实测数值、UI 接线与证据 |
| [运行时训练场验收报告](Docs/运行时训练场验收报告.md) | 模式、统计口径、存档隔离与回归结果 |
| [RedChain](Docs/RedChain验收报告.md) / [RedBoom](Docs/RedBoom验收报告.md) / [WestKing](Docs/WestKing验收报告.md) | 第 5 关新增敌人与 Boss |
| [GreenHammer](Docs/GreenHammer验收报告.md) / [GreenSummon](Docs/GreenSummon验收报告.md) / [NorthKing](Docs/NorthKing验收报告.md) | 第 6 关新增敌人与 Boss |

本地证据位于 `output/RebirthAudit/`：包含 `runtime-validation.txt`、`training-regression.txt`、编译日志，以及第一关、结算、地图、存档槽的实测截图。`output` 已被 Git 忽略，其他检出环境应通过验收工具自行生成日志。
