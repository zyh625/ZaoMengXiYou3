# 第五关引魂灯使 RedBoom 验收报告

日期：2026-11-01。Unity 6000.4.9f1，2D / C# / URP。

交付入口：`Assets/Projects/Prefabs/Enemy/RedBoom.prefab`。HP 80，经验 18，移速 0.8。三个技能、特效及 Level5Scene 对象池均已填好。

## 数值与行为

| 招式 | 动作 | 前摇 | 伤害 | 冷却 | 释放帧，0 起算 |
| --- | --- | ---: | --- | ---: | ---: |
| 灯焰射线 | AttackOne | 0.9 秒 | 单次 12；随后 2 秒灼烧，共 6 | 5 秒 | 12 |
| 投灯 | AttackTwo | 0.5 秒 | 落地 1 秒后爆炸 18，半径 2.2；随后火区 | 7 秒 | 8 |
| 引魂标记 | AttackThree | 0.6 秒 | 无直接伤害；受伤倍率 ×1.2，持续 4 秒 | 12 秒 | 9 |

火区按用户最终确认执行：持续 3 秒，每 0.5 秒造成 5 点伤害，共六次，基础合计 30。单个灯笼爆炸与全部火区基础合计 48。
灼烧采用每 0.5 秒 1.5 点、四次结算，保持 3/秒、持续 2 秒。

- 与当前角色水平距离小于 4 时后撤，大于 7 时靠近，4～7 时停下。优先给未标记目标施加标记，其次射线，最后投灯；三招独立计时，起手时消耗冷却。
- 小于 3 单位且闪现可用时，向远离方向水平瞬移 4 单位；冷却 6 秒。检查整段本体扫掠、目的地重叠、左右脚地面及当前波相机边界；任一检查失败便放弃本次闪现，不消耗冷却。
- 沿用 EnemyController 的待机、走路、受击、死亡与 Skill 调度。RedBoom 不加霸体，普通受击能取消未释放技能；已投出的灯笼和已施加的标记独立继续。
- 射线在起手时锁定水平路径并画红线，等待完整 0.9 秒后，在释放帧只采样一次。范围为可配置的 10 单位长、0.55 单位高，遇 Ground 层墙体截断；每个 Health 去重。Ray 预制体只负责表现。
- 投灯采用抛物线与扫掠落地判定；落地计时、爆炸、火区三阶段独立运行。对象池由场景持有，射手死亡或回池不回收已飞出的灯笼；结束后自动回池并清空下一次借用状态。
- 标记以各个池化 Mark 实例作为倍率来源，可独立叠乘；移除一个来源不影响其他来源。跟随被标记的 Health，四秒到期、角色换出、死亡、驱散、效果停用及对象池整体回收均移除自己的倍率，不转移给换入角色。
- 火区与投射物使用预分配的碰撞结果数组、复用 HashSet 和结构体计时；每帧不创建伤害列表或协程。原生运行测得非结算时连续 1000 次火区更新分配 0 个托管字节。

## 动画切片与 SpriteData

七张角色图均为 1024×1536、4 列×4 行、每格 256×384；每张正好 16 个有效帧。
Unity 导入为 Multiple，PPU 100，名称连续为 `<Action>_0`～`<Action>_15`。
每个 Sprite 矩形沿可见像素裁紧并保留约 2 像素边距。脚底基线统一为格内从上向下第 352 像素；裁紧后重新计算脚底中心 pivot，保持位置稳定。

| 动作/特效 | 切片数 | fps | 播放 | 相比完整帧格减少的矩形面积 |
| --- | ---: | ---: | --- | ---: |
| Idle | 16 | 10 | 循环 | 55.8% |
| Walk | 16 | 14 | 循环 | 52.9% |
| AttackOne | 16 | 16 | 单次 | 62.4% |
| AttackTwo | 16 | 16 | 单次 | 65.1% |
| AttackThree | 16 | 16 | 单次 | 64.5% |
| Attacked | 16 | 18 | 单次 | 54.8% |
| Die | 16 | 12 | 单次 | 71.7% |
| Lantern | 16 | 14 | 循环，飞行及落地等待 | 96.3% |
| Ray | 8 | 24 | 单次，射线命中表现 | 94.3% |
| Mark | 9 | 18 | 单次出现，末帧保持至四秒 | 93.7% |

灯笼爆炸复用 Ray 的 8 帧，以 20 fps 单次播放；火区复用这组火焰帧，以 12 fps 循环，配合独立地面范围环显示实际宽度。
Ray 从原图实际变化中取 8 帧，Mark 使用前 9 个出现帧，均未重复帧凑数。特效使用中心 pivot，三张成品图也为 1024×1536。

Idle / Walk / Attacked / Die 已填入基类字段；AttackOne / AttackTwo / AttackThree 已填入子类字段。三个特效各自拥有独立 SpriteData。
Stand.png 是参考立绘，保持原样，不参与动画播放。

Die 原图有 15 个有效姿态；按用户“缺失自己补上”的授权，使用内置 `imagegen` 补出一个独立倒地过渡姿态，加入为 Die_6。其余有效像素仅做缩放、居中与透明补边。全部 145 个切片均已检查：非空、无越界、命名连续、帧内容不重复、Unity 网格非空。

## 预制体与场景接线

RedBoom 根节点挂 RedBoomController / Combat / Movement、Health、ActorStatus、EnemyRewards、Rigidbody2D 和本体 BoxCollider2D。
Visual 子物体挂 SpriteRenderer、SpriteManager、SpriteOutlineController；排序层 0、Order 2、材质与 PurpleRanged 一致。原图朝左，Visual 的负 X 缩放适配基类朝向。
Combat/Attacked 是常开受击触发箱；Combat/SkillCheck 为 EnemyCheckAttack 检查箱；Muzzle 为发射点。
参考 PurpleRanged 的远程模式，EnemyCombat 的近战数组为空，三招在子类按动画帧采样；不会生成重复的近战伤害。没有 Boss UI 组件。

| 预制体 | 责任 |
| --- | --- |
| `Assets/Projects/Prefabs/Enemy/RedBoom.prefab` | 完整小怪与三个特效引用 |
| `Assets/Projects/Prefabs/SeparateEffort/RedBoomLantern.prefab` | 抛物线、落地计时、爆炸、三秒火区 |
| `Assets/Projects/Prefabs/SeparateEffort/RedBoomRay.prefab` | 红线预警与一次命中动画 |
| `Assets/Projects/Prefabs/SeparateEffort/RedBoomMark.prefab` | 跟随目标、四秒倍率与清理 |

Level5Scene 复用已有启用的 RedChainStageEnemyManager（继承 StageEnemyManager）与 StageManager 接线，在 `enemyPools` 增加 RedBoom：poolSize 10、weight 0.45。现有 RedChain：10、0.55 保留。
原生加载场景已确认 LevelManager 初始化成功，并真实预热 10 个 RedBoom 与 10 个 RedChain。没有修改其他场景、其他敌人或原有第五关管理器脚本。
第五关此前的 Boss 引用仍为空；本次按既有小怪阶段接线完成，不扩展广目天王内容。

公共代码只按授权扩展 `Health.SetDamageMultiplier` 的一行：`Clamp01(multiplier)` 改为 `Max(0, multiplier)`，允许 ×1.2 易伤。既有 0～1 减伤、按来源叠乘、护盾和伤害结算顺序保持原样，原生运行已验证。

## 逐项验收

| 要求 | 结果与证据 |
| --- | --- |
| 1. `dotnet build Assembly-CSharp.csproj` 零错误 | 通过；0 错误，1 个既有 Bird.startMissTrophy 未赋值警告。Editor 工程也为 0 错误，仅有既有 RedChainSetup 过时接口警告。 |
| 2. 每张角色图恰好 16 帧、命名连续、无空帧 | 通过；7×16 角色帧 +16 Lantern +8 Ray +9 Mark，共 145。PNG/Meta 审计检查 alpha、唯一帧、越界和脚底 pivot；Unity 原生导入检查非空像素及网格。 |
| 3. 射线一次、火区 0.5 秒、无每帧 GC | 通过；重复采样释放帧 100 次仍仅一次 12 伤害；完整灼烧总计 6；火区六次 5 伤害；非结算更新 1000 次托管分配为 0。 |
| 4. 标记四秒移除、换人不串 | 通过；四秒后倍率恢复，独立双来源叠乘正确，移除一源保留另一源；角色换出后旧标记消失，新角色伤害倍率正常；池整体回收也清除标记。 |
| 5. 闪现不出场地、不进地形 | 通过；同水平线准确位移 4；六秒冷却且无伤害；墙体、其他敌人重叠、边界之外、无地面落点均拒绝。 |
| 6. 文件、动画、编译、场景说明 | 已列于本报告。四个 Prefab 无 Missing Script 或丢失对象引用，HP/经验正确。 |

Unity 批处理 Play Mode 原生测试共 44 项通过，退出码 0。测试先真实加载 Level5Scene 检查初始化和两个池，再使用专用地面、墙体、敌人、边界和玩家验证物理及技能。最后启用完整 RedBoomController 与 SpriteManager，验证 AttackThree→AttackOne→AttackTwo 的实际动画释放及标记后的 14.4 点射线伤害。
Walk_13 最终扩框后另跑一次 Unity 原生导入检查，退出码 0。美术拼图已人工检查；批处理使用 nographics，未做屏幕渲染截图验收。

验收产物：

- `output/RedBoomAudit/runtime-validation.txt`：44 项 PASS 与 ALL PASS。
- `output/RedBoomAudit/unity-assets.txt`：145 Sprite、四个 Prefab、引用及数值检查。
- `output/RedBoomAudit/unity-runtime.log`、`unity-sprites-final.log`：Unity 原生运行与最终导入日志。
- `output/RedBoomAudit/dotnet-build.txt`、`dotnet-editor-build.txt`：编译日志。
- `output/RedBoomAudit/art-audit.json`、`contact-sheet.png`、`audit_art.py`：逐帧矩形/pivot 数据、预览和只读复查脚本。

可重复运行编辑器菜单 `造梦西游/RedBoom/生成并验证引魂灯使`，重新生成本次四个 Prefab 并接线；打开 Level5Scene 后运行菜单 `造梦西游/RedBoom/运行物理与效果验收`，只在 Play Mode 创建验证对象，不保存测试场地或修改存档。

## 新增/修改文件清单

新增脚本（均包含对应 `.meta`，目录 `RedBoom.meta` 一并交付）：

- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomController.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomCombat.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomMovement.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomHitUtility.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomBurn.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomLantern.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomRay.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomMark.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomEffectPool.cs`
- `Assets/Projects/Scripts/Enemy/RedBoom/RedBoomRuntimeValidation.cs`（仅 UNITY_EDITOR 生效）
- `Assets/Projects/Editor/RedBoomSetup.cs`
- `Assets/Projects/Editor/RedBoomValidationBridge.cs`

新增预制体为上表四个文件及其 `.meta`；新增 `Assets/Projects/Prefabs/SeparateEffort/RedBoomWarning.mat` 及 `.meta`，用于 URP 红线/地面环。

重排和切片的图片（每项含 PNG 与 PNG.meta，路径均在 `Assets/Art/Enemies/Level5/RedBoom/`）：Idle、Walk、AttackOne、AttackTwo、AttackThree、Attacked、Die、Lantern、Ray、Mark。

其他修改：

- `Assets/Projects/Scripts/Combat/Health.cs`：仅上述倍率接口扩展。
- `Assets/Projects/Scenes/Levels/Level5Scene.unity`：登记 RedBoom 池。
- `Docs/RedBoom验收报告.md`：本报告。

未执行 git commit；未删除现有资源。

## 补帧生成记录

工具：内置 `imagegen`。补帧落在 `Assets/Art/Enemies/Level5/RedBoom/Die.png` 的 Die_6，原始透明输出另存 `output/RedBoomAudit/generated-die-source.png`。

生成提示词：

```text
Use case: identity-preserve. Create exactly ONE new intermediate death-animation sprite of this red-and-gold hooded Chinese spirit-lantern mage. Match the reference sheet's 2D cel-painted game style, gold-trimmed dark crimson robes, gold hood with shadowed face, gray hands, hooked gold staff and eye-pattern glowing lantern. Pose between reference row 2's kneeling and lying positions: seated with one knee collapsed, torso leaning backward toward the right, left hand holding the lowered staff, lantern resting just above the ground on the left, right hand bracing on the floor. Whole mage, both feet, staff and lantern fully visible, generous clear transparent margins. Single complete isolated sprite on true transparent background. No text, grid, extra people, scenery, ground plane or aura. Preserve character identity and costume exactly, add a distinct in-between pose rather than copying a frame.
```
