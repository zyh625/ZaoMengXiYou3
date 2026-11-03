# 第七关 Boss 赤炉灵尊 RedFurnaceLord

## 文件清单

- `Assets/Projects/Scripts/Boss/RedFurnaceLord/`：Controller、Combat、Movement、UI、AttackBox、Effect、EffectPool 和仅编辑器编译的 RuntimeValidation，各自带 `.meta`。
- `Assets/Projects/Editor/RedFurnaceLordSetup.cs`：生成、切片检查、引用检查和 Level7 接线；`RedFurnaceLordValidationBridge.cs`：Play Mode 验收入口。
- `Assets/Projects/Prefabs/Enemy/RedFurnaceLord.prefab`。
- `Assets/Projects/Prefabs/SeparateEffort/RedFurnaceLord{PestleImpact,FurnaceBreath,ChainSweepArc,CoreBurst}.prefab`：四个独立表现预制体；Boss 下各一个池，世界空间播放，停用回收，表现本身不造成伤害。
- `Assets/Projects/Scenes/Levels/Level7Scene.unity`：只替换 Boss 引用、专属 UI 组件及其引用。

## 数值与帧窗口

F1 对应切片索引 0。HP 1310、经验 260、速度 0.9、普通出招间隔 2.2 秒，无怒气。Combat 的 Inspector 集中保存招式伤害、距离、冷却、窗口、丹心阈值、计时与效果引用。Health、EnemyRewards、Movement 同时保存对应基础数值，关卡原有轮回缩放仍由关卡创建流程处理。

| 动作 | 帧窗口 | 结算 |
| --- | --- | --- |
| PestleSmash | F8–F11，索引 7–10；F12 关闭 | 34 伤害、半径 2.6，低矮宽箱；F8 生成 PestleImpact，同一目标一招一次；冷却 6 秒 |
| FurnaceBreath | F6–F12，索引 5–11；F12 保持至喷吐满 2 秒，再播恢复帧 | 前方 5u、半角 35° 锥形；每 0.5 秒 8 伤害，共四次 32；F6 生成 FurnaceBreath；冷却 9 秒 |
| ChainSweep | F7–F11，索引 6–10；F12 关闭 | 前方 6u，24 伤害，一招一次；命中后向 Boss 身前拉最多 2u，避免穿越实体墙；F7 生成 ChainSweepArc；冷却 8 秒 |
| FurnaceOpen | 进入即占用动作，F8 开始允许核心光环判定；非循环播放后保持末姿势 | 首次 HP≤70%、首次 HP≤40% 各一次；跨过两阈值的大伤害保留两个触发 |

Idle 10fps 循环，Walk 14fps 循环，三招及 FurnaceOpen 16fps 单次，Hit 18fps，Death 12fps。8 张动作均为 1024×1536、4×4、16 切片，名称 `<Action>_0..15`，PPU100、脚底 pivot 像素 `(128,72)`。四张效果为 1024×1024、16 切片、PPU100、中心 pivot。未修改美术 PNG。

## 丹心时序

1. 伤害及附加效果结算后检测 70%/40%，记录已触发阈值，打断普通招式；多阈值触发按队列保留。
2. 立即锁定自主移动和普通出招，计时 6 秒，以 Combat 实例作为独立来源设置 `Health.SetDamageMultiplier(this,1.5)` 与普通受击霸体。当前 Health 已支持大于 1 的来源，本次不修改 Health 或公共基类。
3. F8 起核心光环生效，累加时钟每 0.5 秒结算半径 3、伤害 8；正常 6 秒过程结算 12 次。保持动画末帧不会延长计时。
4. 到期或冻结、吸附、携带等强控打断，先退出状态并清理本来源，再释放一次半径 4、35 伤害且击退的 CoreBurst。重复结束调用不重复释放。其他组件的独立倍率来源保留。
5. 死亡或停用关闭所有攻击箱、取消光环、清除本来源倍率及霸体、回收效果；死亡不追加攻击。对象池下次启用重置两阈值与全部计时。
6. UI 订阅 CoreChanged，特殊槽显示 `CoreRemaining/CoreDuration`，默认即除以 6，颜色橙红；死亡和清理后归零。

## 场景接线

`RollingFurnaceStageEnemyManager.boss` 指向 RedFurnaceLord 根 Transform；`bossBornLocation` 保留 `ChiluLingzun_BossSpawn`。因此 HasBoss=true，RequiresBoss=true，沿用现有关卡 Boss 出生、死亡和出口流程。现有 BattleUI 场景实例以组件覆盖移除 TitanBossUI，添加 RedFurnaceLordUI，绑定原 bloodFull 与 specialState，并设置 RollingFurnaceStageManager.bossUI。未修改公共 BattleUI 预制体。

保留三池：RollingFurnace 10/0.33，ClampGuard 10/0.34，PressureKettle 10/0.33。五波左侧 `3,4,7,7,8`、右侧 `3,5,6,8,9`；未改变波次数量、刷怪间隔或其它配置。

## 验证记录

2026-11-03：Unity 6.4 / 6000.4.9f1 的隔离项目副本完整执行成功，退出码 0，运行时 **47 条 PASS / ALL PASS**。通过的内容包括真实 Level7 Boss 阶段出生、血条及丹心槽；三招帧开关与伤害；喷吐四次 tick、背面拒绝；70%/40% 各一次及双阈值排队；12 次光环、35 伤害单次爆发；独立倍率共存、强控打断、死亡及池复用清理；真实 Boss 死亡动画后普通关卡完成钩子开启出口。五个预制体无缺失脚本或引用，192 张切片的数量、名称、矩形、PPU 和 pivot 全部通过。

`dotnet build Assembly-CSharp.csproj --nologo -v:minimal -m:1 -nr:false`：**0 错误，1 条已有 Bird.cs 警告**。Editor 工程也通过，0 错误，7 条已有警告（Bird、RedChainSetup、Level7EffectsValidation）。未修改这些文件。

已核对交付预制体、`.meta` 和 Level7Scene 与通过验收的副本 SHA256 一致；与开始前场景快照对比，仅有 Boss 引用与 UI 覆盖接线的差异。原始记录：`runtime.txt`、`assets.txt`、`scene.txt`、`isolated-validation.log`、`dotnet-build.txt`、`dotnet-editor-build.txt`。关卡验证止于出口开启，不触发测试存档或胜利场景跳转。

最终结果以 `output/RedFurnaceLordAudit/` 的编译、资源和运行时原始记录为准。Unity 菜单入口为 `造梦西游/RedFurnaceLord/运行完整验收`；批处理入口为 `RedFurnaceLordValidationBridge.RunBatch`。运行时验收仅修改未保存的 Play Mode 实例，不保存测试数值或测试场地。

未执行 git commit，未删除已有资源，未修改其他 Boss、其他关卡或公共基类语义。
