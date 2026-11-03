# ConstellationWeaver 织宿星蛛验收报告

完成日期：2026-11-03。范围为星蛛专属脚本、预制体、占位和 Level8Scene 池登记。未提交 git commit，未改其他敌人或公共基类语义。

## 数值与行为

- 本体 HP 138、经验 24、移动速度 0.9；不设置霸体或护盾。小于 3u 后撤，大于 6u 靠近，3～6u 选技能；施法停止普通移动，换位除外。
- 双锚冷却 12 秒；每锚 30 HP；星丝 10 秒。预告期间即预留场景唯一星丝名额，多个星蛛不会同时生成多条。
- 星丝接触伤害 8，40% 减速 1 秒。按 Health 去重，多碰撞箱只结算一次；持续接触不反复结算，重新进入还需满足 0.5 秒间隔。
- 任一星锚死亡立即回收两锚和整条星丝，并施加蜘蛛 0.8 秒普通硬直；自然到期不触发拆锚硬直。
- 三星冷却 10 秒，起手锁定三个地面点；每点一次 6 伤害、40% 减速 1.5 秒，节点间隔 0.6 秒。低位判定允许跳过。
- 换位冷却 8 秒，沿可见星丝连续物理位移，抵达远端后硬直 1 秒；受击、冻结、外部控制、断丝均中断。断丝后不能起手。
- 星锚和区域按实例预热复用；本体死亡、停用时回收，减速在玩家身上单独计时，陷阱回池不会提前取消，驱散/角色停用会清除自己的减速。

## 动画与帧窗口

所有动作是 1024×1536 图集、每动作 16 帧。Unity 资源验证检查连续的 Action_0～Action_15、有效矩形/非空像素和网格。七套精灵导入改为自定义底部居中 pivot (0.5, 0)，保留原切片矩形和 PPU=100。

| 动作 | fps | 循环 | 窗口（从 1 计数） |
| --- | --- | --- | --- |
| Idle | 10 | 是 | 全部 |
| Walk | 14 | 是 | 全部 |
| TwinAnchor | 16 | 否 | 起手显示双点预告，第 5 帧生成两锚和星丝，在 5～9 帧窗口内 |
| ThreeStarBind | 16 | 否 | 第 6、8、11 帧脉冲；后两点对应帧等待，保证实际间隔至少 0.6 秒 |
| SilkReposition | 16 | 否 | 第 5～10 帧连续换位，物理到达后 1 秒硬直 |
| Attacked | 18 | 否 | 使用 Attacked，无 Hit 引用 |
| Die | 12 | 否 | 使用 Die，无 Death 引用 |

三星帧等待是为同时满足 16fps、6～11 帧窗口和 0.6 秒节点间隔；因此该招总播放时间大于普通 16 帧单次动画的 1 秒。

## 文件清单

新增脚本目录 `Assets/Projects/Scripts/Enemy/ConstellationWeaver/`：

- ConstellationWeaverController.cs：中距调度、动作切换、暂停帧与打断。
- ConstellationWeaverCombat.cs：技能、冷却、全场星丝名额与专属效果池。
- ConstellationWeaverMovement.cs：保持距离、沿线位移与地形阻挡。
- StarAnchor.cs：独立 Health 可攻击锚与断丝通知。
- ConstellationWeaverArea.cs：星丝/地面节点占位和去重伤害。
- ConstellationWeaverSlow.cs：玩家来源化定时减速。
- ConstellationWeaverRuntimeValidation.cs：仅编辑器编译的隔离 Play Mode 验收。

新增资源：

- `Assets/Projects/Prefabs/Enemy/ConstellationWeaver.prefab`
- `Assets/Projects/Prefabs/SeparateEffort/StarAnchor.prefab`
- `Assets/Projects/Prefabs/SeparateEffort/ConstellationWeaverSilkLine.prefab`
- `Assets/Projects/Prefabs/SeparateEffort/ConstellationWeaverStarKnot.prefab`
- `Assets/Projects/Prefabs/SeparateEffort/ConstellationWeaverPlaceholder.mat`
- `Assets/Projects/Prefabs/SeparateEffort/StarAnchorPlaceholder.png`：程序生成的 8×8 白色占位 Sprite。
- `Assets/Projects/Editor/ConstellationWeaverSetup.cs`：资源生成/校验菜单。
- `Assets/Projects/Editor/ConstellationWeaverValidationBridge.cs`：隔离 Play Mode 与批处理入口。
- `Assets/Projects/Editor/ConstellationWeaverValidation.unity`：空白验收场景，不启动其他关卡逻辑或存档流程。
- `Docs/ConstellationWeaver-runtime-validation.txt`：30 项成功验收记录。

以上资源均带相应 meta。修改七套动作 png.meta 的 pivot；修改 Level8Scene 的 StageEnemyManager 登记。未改角色图像像素。

## 场景接线

Level8Scene 原先没有 StageEnemyManager，因此在现有 Level8_WanxiangStarHall_Layout 上新增该组件，enemyPools 中登记星蛛 Controller，poolSize=10、weight=0.33。预制体包含 Rigidbody2D、本体 BoxCollider2D、Health、单个 ActorStatus、EnemyRewards、三个专属组件和 Visual/SpriteManager，Combat/Attacked 为可受击触发箱。三个效果预制体引用全部接好。

场景现有 LevelManager 原本处于禁用状态，Boss 引用为空；本次保留这一布局状态。星蛛池登记已完成，这不等于整个第 8 关已具备完整启动配置。

## 验收结果

1. `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:q`（同时编译运行时工程）：最终 0 警告、0 错误。Unity 6.4 的独立缓存批处理资源生成成功；7×16=112 帧通过校验。
2. 真实 PlayerAttackBox 触发碰撞可扣完锚的 30 HP；当帧断丝/回收另一锚，蜘蛛硬直到 0.8 秒。通过。
3. 星丝 8 伤害、持续接触去重、0.5 秒重入保护、重入后单次伤害及 1 秒减速到期；星结单次 6 伤害、起手锁位、0.6 秒等待及 1.5 秒减速到期。通过。
4. 拆锚后换位起手失败；多物理步位移可见、到达另一端、受击打断和 1 秒收招硬直。通过。
5. 额外检查多星蛛同屏单丝、星丝 10 秒到期、真实控制器自动出招和受击中断。共 30 项 Play Mode 检查 ALL PASS，批处理返回 0；未出现星蛛空引用或销毁引用异常。

编辑器入口：`造梦西游/ConstellationWeaver/生成并验证织宿星蛛`、`造梦西游/ConstellationWeaver/运行隔离验收`。批处理入口为 `ConstellationWeaverSetup.BuildAndValidate` 与 `ConstellationWeaverValidationBridge.RunBatch`；运行时入口不带 -quit，由验收完成后自行退出。

## 待补美术：星锚、星丝、星结

当前星锚为低透明青蓝方块 Sprite，星丝为低透明细 LineRenderer，星结为低透明地面椭圆圈。均为独立占位，没有使用角色帧或其他关卡特效代替。
