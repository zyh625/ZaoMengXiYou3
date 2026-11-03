# Level8 / 九曜玄凰 · 角色与独立特效

2026-11-02。使用内置imagegen，以本目录原有`level8.png`为身份参考，生成9张角色本体表与6张独立特效表。原参考文件保持原字节与SHA-256；用户最初给出的上一级`Bosses/level8.png`已不存在，实际参考位置为本目录。

## 角色本体（9张、144帧）

| 文件 | 动作 | FPS | 播放方式 |
| --- | --- | --- | --- |
| `Idle.png` | 待机呼吸、收翼、尾扇微摆 | 12 | 循环 |
| `Walk.png` | 庄重鸟步、左右爪交替 | 14 | 循环 |
| `SkyDash.png` | 蓄势、低空水平突进、展翼制动 | 16 | 单次，回准备 |
| `FeatherFan.png` | 仰身抬翼、向前甩翼、收翼 | 14 | 单次，回准备 |
| `TailSweep.png` | 抬尾、低位扫尾、回收尾扇 | 14 | 单次，回准备 |
| `StarDive.png` | 起跳、顶点、斜向俯冲、冲击蹲伏 | 14 | 单次，回准备 |
| `Ascend.png` | 展翼、离地、升空、悬空展翼 | 12 | 单次，保持F16悬空 |
| `Hit.png` | 左侧受击、向右后仰、重新站稳 | 12 | 单次，回准备 |
| `Death.png` | 踉跄、屈腿、侧倒、星纹渐暗 | 12 | 单次，保持伏地F16 |

- 角色只包含凤凰本体、相连的羽冠/双翼/尾扇/爪足，以及羽毛内部星纹。没有冲刺尾迹、羽刃弹、扫尾刀光、光环、俯冲闪光、碎石、游离羽毛、残影、地面投影或背景。
- 每张1024×1536 RGBA真透明，4列×4行，每格256×384，恰好16帧，从左到右、从上到下。翼尖、冠羽、尾扇和爪尖包含在各自完整单元格内。
- 全部角色包围框满足不超过178×288；左右至少32px、上下至少40px空透明边距。为完整容纳凤凰宽大尾扇与展翼，角色保持小占幅，不填满单元格。
- 统一地面线为格内左上原点y=328。PPU100，Pivot=(0.5,0.145833333)，即底部像素坐标(128,56)。动作内统一等比缩放，跨组以中立眼部至落脚线距离校准，不逐帧放大填格。
- SkyDash的低空段、StarDive的跃起/俯冲段和Ascend的升空段保留格内离地偏移；根Pivot不随跳跃变化。Ascend的F13–16整幅本体保持52px离地偏移，末帧不接回站姿。
- Idle与Walk的F16精确接回F1。SkyDash、FeatherFan、TailSweep、StarDive和Hit的首尾接共同`Idle_0`准备姿势。Death保持侧倒终态。TailSweep中4个偏向右侧的源姿势已整体镜像为左向，尾扇仍完整相连。
- `.meta`已设置Sprite/Multiple、Full Rect、Bilinear、Clamp、无Mipmap/无压缩。切片名为`<Action>_0`至`<Action>_15`。重切使用Grid By Cell Size=256×384，Offset=0、Padding=0，保持统一Pivot。

## 独立招式表现（6张、96帧）

| 图像文件 | 对应表现 | FPS | 建议触发 |
| --- | --- | --- | --- |
| `Effects/SkyDashTrail.png` | 低空突进星风尾迹 | 20 | SkyDash F6；单独跟随或放置在突进路径 |
| `Effects/FeatherBlade.png` | 单枚星羽刃弹，向左飞行姿态 | 20 | FeatherFan F5–7；多枚实例组合扇形齐射 |
| `Effects/TailSweepArc.png` | 贴地星尾横扫弧光 | 20 | TailSweep F6–8；放在身前地面 |
| `Effects/StarDiveImpact.png` | 俯冲落地星闪、羽屑和小石屑 | 20 | StarDive F12落地姿势；放在实际落点 |
| `Effects/AscendAura.png` | 升空状态的独立星纹光环 | 16 | Ascend F6起飞或F9升空；可独立跟随 |
| `Effects/FeatherRainMarker.png` | 羽刃雨落点预警，亮起后碎散 | 16 | 特殊状态选定落点后；放在目标地面 |

每张特效为1024×1024 RGBA真透明、4×4、16格256×256，四侧至少32px空透明边距。只有星风、羽刃或碎屑等对应表现，没有凤凰身体、鸟形残影、背景或地面纹理。所有特效为单次起势、峰值、淡出；末帧低于本组峰值alpha面积的2.5%。

贴地三组TailSweepArc、StarDiveImpact、FeatherRainMarker采用y=216接地边缘，Pivot=(0.5,0.15625)；其他特效采用中心Pivot=(0.5,0.5)。全部PPU100，切片名为`JiuyaoXuanhuang<特效名>_0`至`_15`。

6个独立视觉预制体位于`Assets/Projects/Prefabs/SeparateEffort/`：

- `JiuyaoXuanhuangSkyDashTrailFX.prefab`
- `JiuyaoXuanhuangFeatherBladeFX.prefab`
- `JiuyaoXuanhuangTailSweepArcFX.prefab`
- `JiuyaoXuanhuangStarDiveImpactFX.prefab`
- `JiuyaoXuanhuangAscendAuraFX.prefab`
- `JiuyaoXuanhuangFeatherRainMarkerFX.prefab`

预制体复用工程现有SpriteRenderer、SpriteManager、IndependentSkillEffect及透明材质，启用播放、结束禁用，便于现有PrefabPool复用；没有伤害碰撞、阵营或Boss行为脚本。默认世界速度为0，羽刃实际飞行由调用者通过`SetVelocity`或物理弹道控制，图内尾迹不会承担世界位移。羽刃雨可复用单枚羽刃，将其旋转90°朝下并由外部控制下落；预警与羽刃实例互相独立。

若升空状态需要持续光环，应在技能逻辑中管理持续时间或单独重播光环，不把光环合并到Ascend角色帧。击中、位移、无敌和落点判定也由后续Boss脚本绑定；本次没有修改战斗脚本或场景。

## 动作时序

- **SkyDash**：F1–5准备/蓄势；F6–8低空起步；F9–12滑翔；F13–15展翼制动；F16准备。
- **FeatherFan**：F1–4后仰抬翼；F5–7向前甩翼；F8–11保持；F12–16收翼。
- **TailSweep**：F1–5扭身抬尾；F6–8低扫；F9–11随动；F12–14制动收尾；F15–16准备。
- **StarDive**：F1–5蓄势起跳；F6–8顶点瞄准；F9–11向左下俯冲；F12–14落地蹲伏；F15–16起身。
- **Ascend**：F1–5展翼蹲伏；F6–8拍翼离地；F9–12升空开尾扇；F13–16保持完整悬空姿势。

`JiuyaoXuanhuang.AnimationNotes.json`保存完整最终提示词、重做/编辑提示词、FPS、循环和保持标志、每帧位置/离地偏移、统一缩放、切片ID、预制体路径和SHA-256。
`JiuyaoXuanhuang.Validation.json`记录15张图240帧的尺寸、alpha透明边界、留白、命名、首尾与悬空终态、6个预制体YAML引用检查。审阅预览使用的深色底不在资产PNG中。
