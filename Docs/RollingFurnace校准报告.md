# RollingFurnace 预制体数值校准（2026-11-03）

本次在 `Assets/Projects/Prefabs/Enemy/RollingFurnace.prefab` 原地修改配置，保留全部对象/组件 fileID 和资源 GUID `6e53536ba74998d47a5fe27112982995`。未执行 git commit，未重建预制体，未修改技能脚本、特效预制体或场景。工作区本来已有大量其他改动；Level7Scene 在校准期间也出现了外部改动，本次只读取其池配置。

本次完成了可通过序列化数据实施的校准；**尚不能宣称完整运行验收通过**：现有技能脚本绕过通用帧窗口字段，且尚无将场景冲击波池注入池化怪物的接口。遵守“不改技能逻辑”的约束保留现状，具体限制见下文。

## 字段与引用

| 项目 | 最终配置 |
| --- | --- |
| Health.beginBlood | 144（原值已正确） |
| EnemyRewards.experienceReward | 22（原值已正确） |
| RollingFurnaceMovement.moveSpeed | 1.0（原值已正确） |
| Combat.rollCooldown / slamCooldown / ventCooldown | 6 / 8 / 12 秒（原值已正确） |
| rollBox.Attack | 20 → 17，击退开启 |
| reboundBox.Attack | 10 → 9，击退开启 |
| slamBox.Attack | 18 → 15，landingOnly=true，landingRadius=2.5 |
| rollBox Collider size / offset | (1.4,1.2) / (0.6,0.1) |
| reboundBox Collider size / offset | (1.2,1.0) / (0.4,0.1) |
| slamBox Collider size / offset | (2.6,1.4) / (0.4,-0.3) |
| 本体碰撞箱 | 保留 (1.5,1.2)、中心 (0,0.6)、edgeRadius=0.08 |
| Rigidbody2D | 保留 Dynamic、Continuous、冻结旋转、gravityScale=0.9 |

Root 的 Controller、Combat、Movement、Health、ActorStatus、EnemyRewards、Rigidbody2D、本体碰撞箱，以及 Visual/SpriteManager 均已存在，无需重复添加。3 个攻击箱、3 个 EnemyCheckAttack、AttackBoxes/CheckAttackLogic 及专属 rollBox/reboundBox/slamBox 引用均完整；受击箱保持启用，ShellVent 没有伤害箱。

17 的精确半值是 8.5，与明确指定的“第二段 9”存在差异。本次优先执行明确的 17/9/15 数值，未擅自改成 8.5。

## 动画与帧窗口

以 F1 对应索引 0，AttackShutIdx 为不包含的结束索引：

| 攻击 | 请求窗口 | AttackOpenIdx | AttackShutIdx |
| --- | --- | --- | --- |
| 滚撞 | F6–12 | 5 | 12 |
| 反弹 | F2–6 | 1 | 6 |
| 砸地 | F12–14 | 11 | 14 |

上述三个窗口已写入 Combat 的序列化数组。**它们目前只完成配置，不代表运行时生效。** Controller.ProcessSpecialState 在进入 Rolling 状态时调用 Combat.OpenRoll，之后持续 SampleRoll；基类 EnemyController 在该方法返回 true 后直接返回，不读取 AttackOpenIdx/AttackShutIdx。当前表现仍是 F1–8 起手、F9–12 循环滚动；反弹直接进入同一滚动帧，未播放 F2–6 的独立窗口。砸地仍在真实落地 F12 时仅同步采样一次，而非维持 F12–14 的触发器窗口。

此外，专属 OpenRoll 只切换攻击箱激活状态，未按朝向镜像新的非零 x 偏移。按要求填写的 (0.6,0.1)/(0.4,0.1) 已保留，但向左滚动时的攻击箱朝向需要现有逻辑支持，无法单靠常量同时满足双向效果。

7 套角色动画均验证为 16 帧，连续名称 `<Action>_0..15`，256×384 切片，PPU=100，统一脚底 pivot=(128,56) 像素（归一化 0.5,0.14583333333333334）。Idle 10fps/循环、Walk 14fps/循环、RollCharge/BounceSlam/ShellVent 16fps/单次、Hit→Attacked 18fps/单次、Death→Die 12fps/单次，绑定及顺序正确。专属控制器部分姿态仍使用脚本内硬编码采帧速率，未改动。

LandingShockwave 的 16 切片名称为 `RollingFurnaceImpact_01..16`，引用顺序正确，256×256、统一 pivot=(0.5,0.15625)，已有 Shockwave 预制体 16fps 单次，SpriteManager 引用完整。保留既有 Shockwave 与 ImpactFX，未新建重复特效。

## 场景池与冲击波

校验时 Level7Scene 的小怪池只有一条 RollingFurnace 记录，引用原 GUID/Controller fileID，poolSize=10、weight=0.33。未重复添加或保存场景。

Combat.shockwaves 目前非空，指向预制体子物体 ShockwaveEmitter 上的 RollingFurnaceShockwavePool；其 prefab 引用已有 `RollingFurnaceShockwave.prefab`。该脚本以静态字典按特效预制体共享实例池，首次 Emit 才在运行场景创建统一根并预热 10 个，因此并非每只怪物各建 10 个特效。

**当前场景 YAML 没有独立 RollingFurnaceShockwavePool 组件，也没有生成时注入此引用的序列化入口。** Prefab 资源不能直接保存对某个场景实例的引用；在禁止修改绑定/生成逻辑的范围内，保留当前有效的子发射器→运行时共享池链路，没有添加闲置的场景池来冒充已接线。

## 逐项验收

1. **编译通过**：`dotnet build ZaoMengXiYou3.slnx --nologo -v:minimal -m:1 -nr:false`，0 错误、7 个既有警告。初次构建遇到工作区其他新建验证类的短暂编译错误，重新构建已通过；清理临时探针后再次构建仍为 0 错误、7 警告。最终日志：`output/RollingFurnaceCalibration/dotnet-build-final.txt`。
2. **去重与撞墙：代码核验，未实测。** AttackBox.OnEnable 清空命中集合，EnemyAttackBox.TryAttack 按 IDamage 去重，同段多碰撞体不重复结算；两个攻击箱分别保存集合。Controller 仅允许一次反弹，WallStun 使用 elapsed>=1 秒，滚动总路径为 8u；滚动霸体按来源设置。伤害配置为 17/9，不能将 9 宣称为 17 的精确一半。上述指定帧窗口尚未运行生效。
3. **散热倍率与清理：代码核验，未实测。** 累计实际受伤达到 40，BeginVent 清空累计并开始 12 秒内置冷却；Closed 持续 2 秒、Health 来源倍率 0.2；之后 Open 持续 1 秒、倍率 1.5；到开始后 3 秒 EndVent 移除本来源。Update 和 DamagePreparing 均推进过期清理；结束、死亡、停用均执行来源清理，不清除其他来源。闭壳期间停止移动并关闭攻击箱，保留受击箱。未执行实时物理或池化复用测试。
4. **配置核验通过**：424 项静态检查/记录，含全部原始 fileID、GUID、技能脚本与既有 Shockwave SHA256 保持不变、全部动画引用和场景池。可复查 `output/RollingFurnaceCalibration/Verify-Configuration.ps1` 与 `configuration.txt`。

为补充实际方法验证，准备了只操作未保存预制体内容的临时 Editor 探针；第一次 Unity 批处理在许可/WMI 初始化处失败，提升环境权限后第二次启动仍在执行测试入口之前以退出码 1 结束，没有生成方法测试结果。临时 Assets 下探针已清理，探针源码仅保留于 output 供复查；不能将其写成测试通过。启动日志：`output/RollingFurnaceCalibration/unity-probes.log`。

旧 `RollingFurnaceSetup.BuildAndValidate` / `RollingFurnaceValidationBridge.RunBatch` 会重新生成敌人与特效并写场景，还使用旧 20/10/18 配置；本次没有运行这些入口。旧验收报告及旧 runtime.txt 属于历史版本，不是本次数值校准的运行证据。
