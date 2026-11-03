# 第七关 RollingFurnace 滚丹炉兽

> 本文保留旧版 20/10/18 配置和旧测试记录。2026-11-03 的 17/9/15 原地校准、帧窗口与现有逻辑限制见 [RollingFurnace校准报告](RollingFurnace校准报告.md)。旧生成/验收入口会重新生成并回写旧配置，本次未运行。

## 交付资源

- 本体：`Assets/Projects/Prefabs/Enemy/RollingFurnace.prefab`。
- 池化落地特效：`Assets/Projects/Prefabs/SeparateEffort/RollingFurnaceShockwave.prefab`，只负责表现，不重复造成伤害；共享场景池预热 10 个，播放结束归还。
- 专属脚本：`Assets/Projects/Scripts/Enemy/RollingFurnace/`。
- 生成工具：Unity 菜单 `造梦西游/RollingFurnace/生成预制体并接入第七关`，实现位于 `Assets/Projects/Editor/RollingFurnaceSetup.cs`。

## 切片

Idle、Walk、RollCharge、BounceSlam、ShellVent、Hit、Death 共 7 张、112 帧。每张统一 4×4、256×384 矩形，名称 `<Action>_0..15`，PPU 100，脚底 pivot 像素坐标 `(128,56)`，归一化 `(0.5,0.1458333333)`。

保留 PNG 原图、资源 GUID 和切片内部 ID；仅修正 importer 的矩形、pivot 和 FullRect 网格设置。已逐格检查 alpha 边界，所有内容独立，未进入相邻格。导入前后的七张 PNG SHA256 完全相同，记录见 `output/RollingFurnaceAudit/art.json`。

## 数值与动作窗口

表中 F1 为索引 0。角色基础 HP 144、经验 22、行走速度 1.0，无通用怒气增幅。

| 动作 | 帧窗口与时间 | 结算 |
| --- | --- | --- |
| 缩壳滚撞 | F1–8 起手 0.5 秒；F9–12 循环滚动；F13–16 恢复 0.25 秒 | 冷却 6 秒，恒速 6u/s、滚动重力 0，总路径 8u；去程 20，反弹段 10，均击退，各段只结算一次 |
| 撞墙弱点 | 墙体或当前可见相机边缘触发，定格 Hit 姿势 1 秒 | 立即关闭攻击箱、撤销霸体；满 1 秒后首次反向，第二次撞墙仍眩晕但不再反弹；未消耗的路程交给回程 |
| 弹跳砸地 | F1–5 蓄力，F6–11 空中；未落地时保持下降帧；真实落地切 F12，F12–16 恢复 | 冷却 8 秒，落地帧仅做一次半径 2.5 判定，伤害 18；同帧生成 LandingShockwave |
| 壳缝散热 | 累计实际受伤 40 后触发；F1–12 收壳并保持至 2 秒；F13–16 开缝并保持 1 秒 | 冷却 12 秒；闭壳来源倍率 0.2，开缝来源倍率 1.5；3 秒结束完全移除。本动作无攻击箱 |
| 普通状态 | Idle 10fps 循环，Walk 14fps 循环，Hit 18fps 单次，Death 12fps 单次 | 死亡播放完第 16 帧后停用、归池 |

3–8u 且冷却完成时优先滚撞；2–4u 使用砸地（重叠区先滚撞）。接近和近距离拉开均使用 Walk。起手和滚动霸体不等于无敌；强制携带、吸附、冻结可打断，恢复时不残留零重力。缩壳及开缝期间禁止自主移动和攻击，普通受击不延长阶段计时。

伤害倍率通过 `Health.SetDamageMultiplier(this, ...)` 管理，结束、强控中断、死亡、停用都用 `RemoveDamageMultiplier(this)` 清除；不会清掉其他来源的倍率。滚撞霸体也使用独立来源，不改公共基类语义。

## 场景接线

`Assets/Projects/Scenes/Levels/Level7Scene.unity` 中的 `StageEnemyManager` 子类池登记 RollingFurnace：`poolSize=10`、`weight=0.33`。保留其余已有池条目。

原场景只有停用的旧关卡组件，未配置可运行的小怪管理器和 Boss。本次补齐第七关局部流程、PlayerSystem/BattleUI 引用及相机/出生点接线，保留旧停用组件和已有资源。使用 `RollingFurnaceStageManager`、`RollingFurnaceStageEnemyManager` 两个局部适配类；未配置 Boss 时清完末波等待，不自动判定胜利；以后填入 Boss 时使用原有 Boss 流程。

## 已执行验收

1. Unity 6.4 / 6000.4.9f1 批处理导入和生成成功；七个动作各 16 帧，预制体无缺失脚本。
2. Level7Scene 实际进入 Play Mode、完成初始化，预热 10 只 RollingFurnace。
3. 真实物理场地验证 20/10 两段各一次、一次反弹、1 秒撞墙硬直、8u 总路径、冷却及重力归还；相机边缘同样触发。
4. 真实 Health 验证累计 40 触发、闭壳减伤 80% 持续 2 秒、开缝易伤 50% 持续 1 秒、独立倍率叠乘及结束时仅移除本来源；池化复用无残留。
5. 真实跳跃/接触验证 F12 落地判定仅一次 18 伤害、冲击波生成及对象复用；特效存续不再次扣血。
6. 补查减速领域中滚撞恒速、强携带打断/释放后重力恢复、冻结中断散热、死亡清理全部攻击箱/倍率/霸体并归池。
7. `dotnet build ZaoMengXiYou3.slnx --nologo -v:minimal -m:1 -nr:false`：成功，**0 错误、7 条已有警告**，均位于 Bird、RedChainSetup、Level7EffectsValidation，未修改这些文件。

批处理入口为 `RollingFurnaceValidationBridge.RunBatch`。原始结果位于 `output/RollingFurnaceAudit/runtime.txt`（`ALL PASS`）、`validation.log`、`assets.txt`、`dotnet-build.txt`。测试场地、测试伤害和测试角色数值仅存在于未保存的 Play Mode 会话，未写回资源。未执行 git commit。
