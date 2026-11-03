# GreenSummon 角色本体动画精灵表

生成日期：2026-11-01。参考：本目录原有 `Stand.png`；原图及原 `.meta` 保留。
使用内置 imagegen 绘制，按用户授权用脚本整理透明边缘、单元格留白与落地对齐。

## 文件与播放建议

| 文件 | 动作 | 循环 | 建议 FPS |
| --- | --- | --- | --- |
| `Idle.png` | 待机呼吸 | 是 | 10 |
| `Walk.png` | 滑步行走 | 是 | 10 |
| `SummonedHammerStrike.png` | 召唤锤击施法 | 否 | 14 |
| `BannerWave.png` | 挥旗横扫 | 否 | 14 |
| `WarBanner.png` | 战旗插地 | 否 | 12 |
| `Hit.png` | 受击后仰 | 否 | 16 |
| `Death.png` | 死亡倒地 | 否 | 12 |

7 张 PNG 均为 1024×1536、RGBA 真透明、4 列×4 行，每张 16 帧，共 112 帧。
每格 256×384，帧序从左到右、从上到下。角色与整面旗帜均保留在本格内，四侧留白，无网格、文字或特效。
每帧 alpha 包围框不超过 178×288，左右透明边距至少 32px、上下至少 40px。

## Unity 导入

- 每张 PNG 已附 `Sprite / Multiple` 的 `.meta`，每张含 16 个 256×384 固定矩形切片。
- 子精灵按时间命名为 `动作名_01` 至 `动作名_16`，例如 `BannerWave_01`。
- 统一 PPU=100、Full Rect、Bilinear、Clamp；关闭 Mip Maps 与纹理压缩，最大纹理尺寸 2048，无 NPOT 缩放。
- 脚底基线为单元格左上坐标 `y=328`；统一自定义 Pivot 为 `(0.5, 0.14583333)`，即距单元格底部 56px。
- 手动重切时选择 `Grid By Cell Size = 256×384`，Offset / Padding 均为 0，保持上述 Pivot，不要自动紧边裁剪。
- `Idle` 和 `Walk` 循环播放，末帧与首帧像素一致；其余播放一次。`Hit` 末帧回到其首帧。
- `SummonedHammerStrike`、`BannerWave` 末帧接公共待机姿势；`WarBanner` 保持插旗，`Death` 保持倒地，二者不会被替换成待机。
- 本次交付为精灵表、切片与备注，不改动控制器、动画片段、碰撞体或游戏脚本。

## 动作分段

### Idle / 待机呼吸

F01 准备姿势；F02–05 吸气、旗面和流苏轻摆；F06–09 呼气、右手与袍摆微动；F10–13 旗面回落；F14–16 回到 F01。双脚不迈步，旗杆保持竖持。

### Walk / 滑步行走

F01 左脚在前；F02–05 重心前移、衣带后拖；F06–09 右脚经过身下；F10–13 右脚在前、旗面前摆；F14–16 左脚回摆并闭合循环。

### SummonedHammerStrike / 召唤锤击施法

F01–05 抬右手、抬头；F06–09 指尖在空中划圆；F10–12 向下发令；F13–14 保持下指；F15–16 收回准备姿势。只绘角色施法姿势，无幽灵锤、法阵或冲击特效。

### BannerWave / 挥旗横扫

F01–04 将旗杆拉至身后；F05–08 旗杆横向挥扫、旗布展开；F09–11 随势收尾；F12–16 将旗杆恢复竖持准备姿势。旗面、横梁与旗杆保持连接。

### WarBanner / 战旗插地

F01–04 双手抬起旗杆；F05–08 向下插旗；F09–11 双手支撑已插地的旗杆；F12–16 保持插旗站姿，旗面和衣带随风摆动。结尾保持双手撑旗，不返回张手待机。

### Hit / 受击后仰

F01 准备；F02 骤然受击；F03–06 向画面右侧后仰、旗杆晃动；F07–09 吸收冲击；F10–13 站直；F14–16 恢复初始姿势。没有攻击或受击特效。

### Death / 死亡倒地

F01–03 后退失衡、旗杆倾斜；F04–07 屈膝；F08–11 下沉并松开旗杆；F12–15 倒在袍服上，旗帜落下；F16 保持倒地终态。播完停在末帧。

## 附加资料与校验

- `GreenSummon.AnimationNotes.json`：动作信息、循环与帧率建议、112 个 Unity 切片坐标和最终生成提示词。
- `GreenSummon.Validation.json`：每帧 alpha 包围框与透明边距、最终文件哈希和参考图哈希。
- 已执行离线像素与切片数据检查；未启动 Unity Editor 做实时导入或游戏内播放验证。
- 聊天中的动态预览为便于比较会循环播放所有动作，游戏中死亡与插旗应按上方规则播放。

## 独立特效（2026-11-01 补充）

- 落锤：`Assets/Projects/Prefabs/SeparateEffort/GreenSummonHammerFX.prefab`。
- 战旗光环：`Assets/Projects/Prefabs/SeparateEffort/GreenSummonWarBannerAuraFX.prefab`。
- 特效表位于本目录的 `Effects` 子目录，不烘焙进角色动作；角色 PNG 保持原样。
- 两个特效预制体均已通过 Unity 6000.4.9f1 原生导入、16 帧引用、独立播放/停止与池化复用检查。
- 详细接口与触发建议见 `Assets/Projects/Prefabs/SeparateEffort/README_GreenSummon_NorthKing_FX.md`。
