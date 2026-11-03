# StarShearGuard / 剪星羽卫

2026-11-02。内置 imagegen 按本目录原有 Stand.png 绘制。最终交付7张角色本体表、3张独立技能特效表，以及3个独立视觉预制体。Stand.png保留原文件。

## 角色本体表

| 文件 | 动作 | 建议FPS | 循环 |
| --- | --- | --- | --- |
| `Idle.png` | 戒备呼吸、双刃微开合 | 12 | 是 |
| `Walk.png` | 轻捷鸟步、左右腿交替 | 16 | 是 |
| `OffbeatScissor.png` | 错拍双剪：胸前第一剪、停顿、腰部第二剪 | 12 | 否 |
| `UpwardScissor.png` | 向前上方合剪 | 12 | 否 |
| `BackstepCut.png` | 后跳退步、落地前刺反剪 | 14 | 否 |
| `Hit.png` | 从左侧受击，向右后仰并恢复 | 12 | 否 |
| `Death.png` | 失力、屈膝、伏倒，单刃从护臂挂点滑落 | 12 | 否 |

- 每张1024×1536 RGBA真透明PNG，4列×4行，每格256×384，16帧，共112帧。顺序从左到右、从上到下。
- 每格包括羽冠、双臂实体长刃、双足与所有爪尖。角色与实体双刃保留，挥斩弧光、粒子、刀光、残影、扬尘均不画进角色图。
- 完整包围框不超过178×288；当前最大实际宽度168px、最大高度185px。横向至少32px、纵向至少40px透明留白；全部通过逐帧alpha边界检查，无跨格和画布裁切。
- 各动作内全程使用同一等比缩放量，不逐帧放大填格；以中立姿势眼部至落脚线距离校准各组身体尺度。
- 固定格内地面线y=328（左上原点），PPU100，统一Pivot=(0.5,0.145833333)，即底部像素坐标(128,56)。后跳只改变格内离地偏移，不改变Pivot。
- BackstepCut的F5–F8分别保留12、24、28、12px离地偏移；角色始终朝左，微量后退在格内向右移动，随后向左回切。
- Idle、Walk的F16与F1像素一致。三招和Hit的F1/F16使用共同Idle_0准备姿势；Death的F16保持伏倒终态，不回站姿。死亡时保留完整双臂与两片完整刀刃，滑落的是装备而非肢体。
- `.meta`已配置Sprite/Multiple、固定网格、Full Rect、Bilinear、Clamp、无Mip Maps、无压缩。命名`<Action>_0`至`<Action>_15`。重切时使用Grid By Cell Size=256×384，Offset与Padding均为0，保持统一Pivot。

## 三招动作分段

- **OffbeatScissor**：F1准备；F2–4双刃张开；F5–7保持预警；F8–9胸前第一剪；F10–11停住；F12低位重新张开、F13腰部第二剪；F14–16回准备。
- **UpwardScissor**：F1准备；F2–5抬头、双刃在胸前上方张开；F6–8保持；F9–11朝左前上方合剪；F12–14后仰随动；F15–16恢复。
- **BackstepCut**：F1准备；F2–4下蹲收刃；F5–8向画面右侧短跳，双足离地；F9–11落地低姿、向左突刺；F12–14短斩延伸；F15–16恢复。刀刃、爪尖始终在本格内。

## 独立技能特效

特效只有银白/冰青/靛蓝剪切弧光和少量光屑，没有角色、身体残影、实体武器、地面或背景。均为1024×1024、4×4、16格256×256、真透明、单次播放；四侧至少32px透明边距。

| 特效图（本目录下） | 独立预制体名称 | FPS | 建议触发 |
| --- | --- | --- | --- |
| `Effects/OffbeatScissorFX.png` | `StarShearGuardOffbeatScissorFX` | 24 | 角色F8开始；先高位一剪，F9–10光效帧短暂停顿，F11–13低位第二剪 |
| `Effects/UpwardScissorFX.png` | `StarShearGuardUpwardScissorFX` | 20 | 角色F8–9，由低右向前上方升起，F9–11合剪时达到亮部 |
| `Effects/BackstepCutFX.png` | `StarShearGuardBackstepCutFX` | 20 | 角色F9落地反击时；只显示向左短斩，后跳阶段不产生残影或尘土 |

预制体均位于 `Assets/Projects/Prefabs/SeparateEffort/`，使用工程现有SpriteRenderer、SpriteManager、IndependentSkillEffect与透明材质。启用即播放，播放完自动禁用，可交给现有PrefabPool复用，位置、朝向、跟随和结束回调独立可控。预制体不包含伤害、碰撞或敌人AI。

OffbeatScissorFX以第一剪中心作为挂点：格内(128,96)，Pivot=(0.5,0.625)，第二剪下降到(128,164)。另外两张使用中心Pivot=(0.5,0.5)。所有特效PPU100。切片命名`StarShearGuard<招式名>_0`至`_15`。

退步短斩特效已做整体水平镜像以统一朝左；上剪原始末格为空，使用同组F15已绘制光屑继续降低透明度得到F16，原始生成图保留。角色与特效均由imagegen绘制，后处理只整理切片、统一比例、锚点、透明边缘和首尾连接。

## 检查与记录

`StarShearGuard.AnimationNotes.json`保存最终完整提示词及重做提示词、建议FPS、循环标志、逐帧位置、包围框、统一缩放量、切片ID、特效预制体路径和SHA-256。
`StarShearGuard.Validation.json`记录10张图的160帧、透明留白、分辨率、连续命名、首尾连接和3个预制体的YAML引用检查结果。预览底色只用于审阅，不存在于资产PNG中。

本次未修改玩法脚本、敌人预制体或任何场景；新增的是角色美术、独立特效和3个视觉特效预制体。
