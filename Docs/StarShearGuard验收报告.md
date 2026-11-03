# StarShearGuard 剪星羽卫验收报告

日期：2026-11-03。Unity 6000.4.9f1 / 2D / URP。

## 交付文件

新增玩法脚本（各带 `.meta`）：

- `Assets/Projects/Scripts/Enemy/StarShearGuard/StarShearGuardController.cs`：沿用 EnemyController 状态机、基类受击和控制打断，管理动画停顿及落空恢复。
- `Assets/Projects/Scripts/Enemy/StarShearGuard/StarShearGuardCombat.cs`：选招、四个独立伤害窗口、两段间隔和特效单次触发。
- `Assets/Projects/Scripts/Enemy/StarShearGuard/StarShearGuardMovement.cs`：通过 MoveAlong 入口执行后撤和补步，不写重力。
- `Assets/Projects/Scripts/Enemy/StarShearGuard/StarShearGuardAttackBox.cs`：逐段按 Health 去重、实际扣血回报、起手朝向前侧筛选。
- `Assets/Projects/Scripts/Enemy/StarShearGuard/StarShearGuardEffectPool.cs`：三招各自独立 PrefabPool 缓存；自然结束归还，打断及时回收。

新增预制体：`Assets/Projects/Prefabs/Enemy/StarShearGuard.prefab`（含 `.meta`）。

接入现有三份视觉特效，保留原有图像、切片、帧率和材质：

- `Assets/Projects/Prefabs/SeparateEffort/StarShearGuardOffbeatScissorFX.prefab`
- `Assets/Projects/Prefabs/SeparateEffort/StarShearGuardUpwardScissorFX.prefab`
- `Assets/Projects/Prefabs/SeparateEffort/StarShearGuardBackstepCutFX.prefab`

场景：`Assets/Projects/Scenes/Levels/Level8Scene.unity`。

新增验证工具（各带 `.meta`）：

- `Assets/Projects/Editor/StarShearGuardSetup.cs`
- `Assets/Projects/Editor/StarShearGuardValidationBridge.cs`
- `Assets/Projects/Editor/StarShearGuardValidation.unity`（空的隔离验收场景，不加入 Build Settings）
- `Assets/Projects/Scripts/Enemy/StarShearGuard/StarShearGuardRuntimeValidation.cs`（仅 UNITY_EDITOR 编译）

## 序列化配置

HP 170，经验 24，移动速度 1.0；常开受击箱，无自身霸体、减伤、无敌或隐身。

| 招式/段 | 伤害 | 判定箱尺寸 | 判定箱中心（朝右） | 冷却 |
| --- | --- | --- | --- | --- |
| 错拍合剪第一段 | 12 | 1.8 × 1.2 | (1.2, 0.4) | 整招 4s |
| 错拍合剪第二段 | 10 | 1.6 × 0.9 | (1.2, -0.2) | 同上 |
| 挑星上剪 | 18 | 1.8 × 1.6 | (1.1, 1.0) | 6s |
| 折羽撤剪 | 12 | 1.6 × 0.9 | (1.2, -0.2) | 7s |

补步为 0.25u / 0.15s，后撤为 2.5u / 0.4s；障碍、减速、冻结等仍由共享移动入口处理。后跃离地姿态由已有 BackstepCut 图像表现，刚体保持正常重力。

2.2u 内优先合剪；2~5u 使用上剪；0.9u 贴脸或血量比例不高于 0.35 时，优先选择已冷却的后撤（目标在 5u 内）。全部阈值、伤害、冷却、位移、窗口和动画帧率均落在序列化字段。

## 帧窗口

以下使用美术帧号 F1~F16；代码 CurFrame 从 0 开始，结束索引不包含。

| 动作 | 帧率 | 有效帧 | 代码窗口 |
| --- | --- | --- | --- |
| Idle | 10fps 循环 | — | — |
| Walk | 14fps 循环 | — | — |
| OffbeatScissor 第一段 | 16fps 单次 | F5~F8 | [4, 8) |
| OffbeatScissor 第二段 | 16fps 单次 | F11~F14 | [10, 14) |
| UpwardScissor | 16fps 单次 | F6~F10 | [5, 10) |
| BackstepCut | 16fps 单次 | F10~F13 | [9, 13) |
| Hit | 18fps 单次 | — | — |
| Death | 12fps 单次 | — | — |

第一段关箱至第二段开箱保证 0.35s 无伤害间隔；用额外动画暂停补足正常帧间隔，不改变 16fps 播放速度。第二段关箱后如未实际扣血，至少保持 0.45s 动作锁定；第一段是否命中不影响此项。期间仍可正常受击、冻结和死亡。

特效在各招首个开箱帧（F5 / F6 / F10）生成一次，合剪两段共用一次特效播放，间隔中同步暂停、第二剪恢复。播完自行禁用回池；自然播放结束会解除旧施法者引用，避免误操作已被其他实例借走的特效。

## 验收结果

- 七个动作实际导入均为 16 帧（共 112 帧）、命名 `<Action>_0..15` 连续、PPU 100、1024×1536、4×4 网格；统一脚底 pivot 像素 (128,56)。
- 三个 FX 实际导入均为 1024×1024 / 16 帧，PPU 100，引用有效，无伤害碰撞组件，单次播放后禁用。
- 运行时 39 项检查全部通过：重复采样仅结算 12/10、合计 22；第一段命中但第二段落空仍有完整恢复；绕后避开两剪；低空上剪命中 18、左右朝向均不覆盖身后；后撤 2.5u、重力不变、承受完整伤害；基础受击和冻结打断；死亡与池重生清理；三种特效各自生命周期及复用；旧施法者不能回收新租户特效。
- 原工程执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal`：**0 错误 / 7 个现有警告**，同时编译运行程序集和 Editor 程序集。
- Unity 隔离批处理退出码 0，标记 `STARSHEARGUARD_ASSETS_PASS`、`STARSHEARGUARD_RUNTIME_PASS`。

日志：`output/StarShearGuardAudit/runtime.txt`、`unity-assets.txt`、`dotnet-build.log`、`isolated-unity.log`。隔离 Unity 导入期间并行开发的其他 Boss 测试入口尚未编译完成，因此仅在验证副本中暂时排除了那两个入口；原工程后来完整 dotnet 构建已通过。日志另有 Unity Editor 搜索数据库初始化异常，不在玩法脚本堆栈内，39 项运行检查和批处理退出码均通过。

## 场景接线和范围

复用 Level8 当前唯一的 StageEnemyManager，追加 StarShearGuardController 预制体引用：poolSize=10、weight=0.34；LevelManager.enemyManager 指向该管理器。保留并行开发已经登记的其他敌人池。

生成当时与生成前快照按 Unity YAML block 比较，本次只改变 LevelManager 的 enemyManager 引用，以及 StageEnemyManager 的 enemyPools；无新增、删除其他场景 block。之后并行任务继续更新了同一场景，最终复核保留三类小怪池，并补回被并行写入覆盖的 enemyManager 引用；不回退并行任务的其他场景改动。

**当前 Level8 的原有 LevelManager 仍为禁用状态、Boss 引用为空。** 本次完成小怪登记和引用接线，没有将现有占位关卡改成完整可启动关卡；不以 Boss 资源替换或公共管理器语义变更绕过现状。

未修改其他关卡、其他敌人脚本或公共基类语义；未删除现有资源；未执行 git commit。

可在 Unity 菜单 `造梦西游/StarShearGuard/生成并验证预制体和Level8池` 重建资源；`造梦西游/StarShearGuard/运行隔离验收` 在专用空场景中复验，结束恢复原 playModeStartScene。
