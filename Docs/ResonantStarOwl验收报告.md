# ResonantStarOwl 鸣曜星鸮验收报告

本任务新增星鸮专属脚本、一个敌人预制体、三个音波预制体，并向 Level8Scene 的 StageEnemyManager.enemyPools 追加星鸮。未修改其他敌人或公共基类源码，未删除现有资源，未执行 git commit。工作区中原有及并行任务产生的其他更改保留。

## 文件清单

- `Assets/Projects/Scripts/Enemy/ResonantStarOwl/`：ResonantStarOwlController.cs、ResonantStarOwlCombat.cs、ResonantStarOwlMovement.cs、ResonantStarOwlEffect.cs、ResonantStarOwlEffectPool.cs、ResonantStarOwlRuntimeValidation.cs，以及对应 meta / 文件夹 meta。最后一个仅在 UNITY_EDITOR 下编译。
- `Assets/Projects/Editor/ResonantStarOwlSetup.cs`、`ResonantStarOwlValidationBridge.cs` 及 meta：生成、引用校验与隔离批处理验收入口。
- `Assets/Projects/Prefabs/Enemy/ResonantStarOwl.prefab` 及 meta。
- `Assets/Projects/Prefabs/SeparateEffort/ResonantStarOwlSoundRings.prefab`、`ResonantStarOwlHighLowChime.prefab`、`ResonantStarOwlResonanceBand.prefab` 及 meta。
- `Assets/Projects/Scenes/Levels/Level8Scene.unity`：仅追加本任务的星鸮敌人池条目，保留其他任务登记的敌人。
- 本报告。

## 动画与帧窗口

下表 F 从 1 开始；脚本索引从 0 开始。七个动作各 16 帧，连续命名，PPU 100。实际正式美术包括三个 Effects 文件均为 1024×1536；沿用既有紧裁切与补偿 pivot，未按暂定的 1024×1024 重新裁切，也未使用 GeneratedDrafts。

| 动作 | 播放 | 窗口 / 数值 |
| --- | --- | --- |
| Idle | 10fps 循环 | 固定基准高度上下浮动 |
| Walk | 14fps 循环 | 水平漂浮，基础速度 0.9 |
| SoundRings | 16fps 单次，释放姿势停帧 | F6、F9、F12 / index5、8、11；起手后 0.3125、0.7125、1.1125 秒释放；每环 8，冷却 7 秒 |
| HighLowChime | 16fps 单次，低鸣姿势停帧 | F6 低层、F11 高层 / index5、10；起手后 0.3125、0.8125 秒；各 10，冷却 6 秒 |
| ResonanceBand | 16fps 单次，F12 停帧维持 | F6～F12 判定，起手后 0.3125～2.3125 秒，共 2 秒；6 点并打断上升，冷却 9 秒 |
| Hit | 18fps 单次 | 受击箱始终开启 |
| Death | 12fps 单次 | 切换 Dynamic 并启用重力下落 |

16fps 的指定帧间距短于数值要求，因此通过专属控制器暂停 SpriteManager 并按施法时钟驱动帧，保持释放帧与真实间隔同时成立；低帧率跳帧时保留释放姿势并补采样，段号先提交再生成特效，避免重复释放。冷却从起手计时。

高低错鸣采用固定“先低后高”，与现有图中的先俯首、后抬头对应。没有随机反转而失去预警提示。

## 判定与行为

鸣环从半径 1.6u 扩张至 7u，环缘半宽 0.18u；查询候选后按最近点 / 最远角排除整个内圈，按上一物理步至当前步的环缘扫掠防止漏判。每个环分别记录玩家 Health 与 LifeVersion，同一环对同一生命只结算一次，三个环合计最多 24 点。

高低短音波是前方 0.8～7u、厚度 0.55u 的分层矩形，中心分别在地面上方 0.55u / 2.6u，每段独立一次伤害。

鸣带在前方 4u、地面上方 2.5u 形成宽 0.55u、高 1.6u 的有限条带。只接收 HasJumped>0 且当前物理检测未落地的玩家；地面穿行及未跳跃的空中目标不受伤。命中清除待处理跳跃输入，将竖直速度限制为向下，单条带不对同一生命重复结算。施法被打断时回收所属鸣带，死亡或池生命变更时旧鸣带失效。

三张特效各以独立 prefab 为键池化，首次施法才创建对应缓存，之后归还 / 扩容；未将特效常驻在敌人身上。鸣环和已释放的短音波可以独立结束，来源记录生命版本，避免回池后归属到新生命。

AI 优先观察跳跃反复起落，其次按空中目标优先错鸣、地面目标优先鸣环，再选择其他已冷却招式。施法起手限于 4～7u；水平距离小于 4u 后退、大于 7u 靠近，小于 3u 同时向上拉开。正常悬停基准为出生点下方地面 +2.2u，加 0.12u 浮动；飞行使用 Kinematic、重力 0，以 BoxCast 预扫地形阻止进入墙体。击飞 / KnockDown 与死亡切换 Dynamic，下落后存活目标恢复悬停；对象池新生命重新初始化高度及重力。

## 预制体与场景接线

Root 包含 ResonantStarOwlController、ResonantStarOwlCombat、ResonantStarOwlMovement、Health、ActorStatus、EnemyRewards、Rigidbody2D、BoxCollider2D。Visual 包含 SpriteRenderer、SpriteManager、SpriteOutlineController；Combat/Attacked 为始终开启的触发受击箱。Health.beginBlood=100，EnemyRewards.experienceReward=24，moveSpeed=0.9，Rigidbody2D.gravityScale=0。

Level8Scene 的 StageEnemyManager.enemyPools 中星鸮条目：prefab GUID `640175fdb81b4c44f9c270d48d25a506`，Controller fileID `1591944423024096039`，poolSize=10，weight=0.33。未改变其他池的配置。

## 验证与重现

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal`：运行时与编辑器程序集编译，0 错误；完整结果见 `Logs/ResonantStarOwlAudit/dotnet-build.txt`。
- Unity 6000.4.9f1 原生导入校验七套动作、三套效果，各 16 帧连续命名、PPU / 悬停 pivot 一致；四个预制体无 Missing Script 或丢失对象引用，HP / 经验 / 速度通过。记录见 `Logs/ResonantStarOwlAudit/unity-assets.txt`。
- 真实玩家和星鸮预制体、真实 Physics2D 的隔离 Play Mode：内圈安全，三环各一次共 24，高低分别 10，鸣带地面无伤 / 只对跳跃者 6 点并截断上升 / 不重复命中 / 两秒结束，三环 0.4 秒与高低 0.5 秒时序及指定释放帧，悬停追击、近身向后上退、地形扫掠、死亡下落、复用恢复零重力。详细结果见 `Logs/ResonantStarOwlAudit/runtime-validation.txt`。
- 批处理入口 `ResonantStarOwlValidationBridge.RunBatch` 必须在独立工程中运行；它打开 Level8Scene、生成并验证资源，再进入空场景 Play Mode 验收。实际 QA 副本在 `Logs/ResonantStarOwlQA/`；未切换主编辑器的当前场景。QA 仅排除了另一并行任务当时尚未能编译的 RedFurnaceLordRuntimeValidation / ValidationBridge 副本，未修改主工程中这两个文件。
- 三张特效与人物正式 PNG / meta 均沿用原资源，没有生成新图片、移动或删除美术。
