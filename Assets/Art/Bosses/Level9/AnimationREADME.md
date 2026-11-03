# 无相帝君（WuXiangDiJun）动作与独立特效

生成日期：2026-11-03。使用内置 image_gen，以本目录原始 `level9.png` 为角色身份参考。原图与已有资源未被替换。

## 角色动作

每张 1024×1536，4 列×4 行，共 16 帧；单元格 256×384；从左到右、从上到下读取。

| 文件 | 动作 | 建议 fps | 播放 |
| --- | --- | --- | --- |
| Idle.png | 待机呼吸、珠串与衣袖微动 | 10 | 循环 |
| Walk.png | 原地缓步、交替迈步 | 14 | 循环 |
| SealSlam.png | 双手举印、下砸、收招 | 16 | 单次 |
| EdictSuppression.png | 抬手、连续下压镇令、复位 | 16 | 单次 |
| RobeSweep.png | 袍袖低扫、收招 | 16 | 单次 |
| SpaceLock.png | 展臂、前推、维持封锁姿势、收手 | 16 | 单次 |
| MaskBreak.png | 面具裂纹、展臂、日轮展开后保持 | 16 | 单次并保持末帧 |
| SunWheelSpin.png | 驱动三段实体日轮旋转、复位 | 16 | 单次 |
| MirrorSplit.png | 引手过胸、前推分身施法、复位 | 16 | 单次 |
| FinalBurst.png | 双手举印、三段日轮汇聚、缓慢收势 | 16 | 单次并保持末帧 |
| Hit.png | 向右后仰受击、站稳恢复 | 18 | 单次 |
| Death.png | 失衡、跪倒、向前伏倒、道具落下 | 12 | 单次并保持末帧 |

角色图仅画姿势与原有实体道具，保留黑白金配色、面具、冠饰、龙钮帝印与三段日轮。施法特效单独位于 Effects。

## 独立特效

每张 1024×1024，4×4、16 帧；单元格 256×256，建议 16fps、单次。

| 文件 | 用途 |
| --- | --- |
| Effects/SealMark.png | 帝印落地、黑金印记与红裂纹 |
| Effects/SuppressionPillar.png | 红圈预告、黑金镇压柱升起与收回 |
| Effects/SunWheel.png | 三段黑金日轮独立旋转与停留 |
| Effects/FinalRings.png | 三道同心冲击环依次扩张并淡出 |
| Effects/RobeSweepArc.png | 袍袖划出的低位气刃 |
| Effects/SpaceLockWall.png | 空间封锁边界形成与退去 |
| Effects/MirrorSplit.png | 象牙白与黑金镜裂、碎片及裂隙 |

镜裂特效不含另一个角色；分身本体可复用同一套角色动作。这里不包含新 Boss 脚本、碰撞判定或预制体接线。

## 尺寸、透明与 Unity 切片

- 共 19 张成品、304 个非空帧，均为真实 RGBA 透明 PNG；透明像素下的 RGB 已归零。
- 角色统一绘制比例；站立参考的连通主体统一至约 141.4px 高，以保证最宽袍袖和倒地动作也不越格。没有为单独某一帧拉伸人物。
- 角色四边实测最小留白（左/上/右/下）：41/109/41/63px；特效为 39/32/39/39px。每帧没有相邻帧内容或可见网格。
- 角色脚底/衣摆基线为单元格顶部下 320px，原点 (128,320)；Unity pivot=(0.5,0.16666666666666666)。
- 地面特效 SealMark、SuppressionPillar、SpaceLockWall 原点 (128,216)，pivot=(0.5,0.15625)；其他特效中心 pivot=(0.5,0.5)。
- 全部 PNG 的 meta 已配置 Multiple、PPU=100、16 个 FullRect 切片，名称 `<Action>_0..15`；每格尺寸固定。
- Idle/Walk 的第 16 帧与第 1 帧完全一致，便于循环；普通收招动作末帧回到开始状态，MaskBreak/Death 保持末姿势。

## 整理与验收

按用户已授权的脚本整理，对生成图进行 alpha 清理、连通区域分离、等比缩放和规则网格排版，没有脚本重绘角色。EdictSuppression/RobeSweep 的粘连稿已重新生成；MaskBreak 下半段四轮片已用 image_gen 修正为三段。

MaskBreak 早期第 4 帧原稿的最远衣摆尖端触及画布边缘，排版采用完整第 3 帧姿势短暂停留一帧，未使用残缺像素；保持姿势与首尾收招含正常的停留帧。FinalRings 的末段亮色残留已重新生成并检查透明淡出。

逐帧文件检查见 `WuXiangDiJun.Validation.txt`，最终几何与 SHA256 见 `WuXiangDiJun.ArtValidation.json`，逐张完整提示词见 `WuXiangDiJun.GenerationPrompts.json`。
生成与整理记录、诊断预览在项目外导入目录 `output/WuXiangDiJunArt/`。预览上的标签只供核对，成品 PNG 没有文字或背景。

文件检查覆盖尺寸、alpha、独立单元格、留白、名称、切片 rect、pivot、PPU、首尾循环、冲击环淡出和 GUID 唯一性。尚未执行这批新图的原生 Unity 导入或 Play Mode 观感验证。
