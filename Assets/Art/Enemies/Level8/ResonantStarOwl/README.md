# ResonantStarOwl 鸣曜星鸮动画美术

身份参考：`Stand.png`，保持原文件不变。本次仅生成美术，不修改第 8 关玩法、脚本或预制体。

## 当前状态

根目录的七套 PNG 与 `Effects/` 中三套 PNG 为规范化交付图。`GeneratedDrafts/` 保留原始生成稿，正式使用时请选择根目录和 `Effects/` 中的图。

已生成 7 套角色动作及 3 套独立特效，各为 1024×1536、4×4、16 个非空帧、RGBA PNG。角色图不包含招式声环、音波或墙体；独立特效不包含角色。

经用户明确授权，已用 Python 清理透明杂点、分离相邻帧的低透明度边缘、等比缩小并居中排版，没有重绘角色。七套角色动作使用同一缩放系数 0.6285714286，各特效按本套最大范围统一缩放。死亡动作在单元格内逐步下降，其余帧保持统一悬停中心。

160 帧逐帧验证：每格非空，透明水平留白至少 32px、垂直留白至少 40px，可见范围不超过格宽 70% / 格高 70%，无邻格越界。所有成品为 1024×1536 的真实 RGBA PNG，4×4 网格、单元格 256×384。原始 PNG 和 Stand.png 均通过哈希核对，未改动。

实测最小左 / 上 / 右 / 下透明留白为 **40 / 68 / 40 / 68 px**；最大宽 / 高占比为 **68.75% / 64.58%**。`validation.txt` 保存最终文件级检查结果。

已生成 Unity `.png.meta`：Multiple、PPU 100、每套 16 个连续命名的切片（`<文件名>_0` 至 `_15`）。切片 rect 沿可见轮廓收紧；custom pivot 补偿为原格内 `(128,192)` 悬停中心，避免紧裁切导致播放位置跳动。死亡末段保留容纳悬停原点所需的少量透明区。纹理不启用压缩或 mipmap。

## 文件

| 交付路径 | 动作 | 建议播放 |
| --- | --- | --- |
| Idle.png | 悬停 | 10 fps，循环 |
| Walk.png | 原地滑翔 | 14 fps，循环 |
| SoundRings.png | 声环施法姿势 | 16 fps，单次 |
| HighLowChime.png | 高低双鸣姿势 | 16 fps，单次 |
| ResonanceBand.png | 共鸣带施法姿势 | 16 fps，单次 |
| Hit.png | 单次受击后恢复 | 18 fps，单次 |
| Death.png | 坠落死亡 | 12 fps，单次 |
| Effects/SoundRings.png | 独立声环 | 16 fps，单次 |
| Effects/HighLowChime.png | 独立高低双鸣 | 16 fps，单次 |
| Effects/ResonanceBand.png | 独立共鸣带 | 16 fps，单次 |

逐行从左向右读取。fps / loop 已写入 meta 的 userData 和 `animation-manifest.json`，供后续 SpriteData 绑定参考；本次不创建角色脚本或预制体。独立特效图只含表现，伤害和生成时机应由后续 Combat 单独控制。

## 生成与检查

使用内置 image_gen 工具，每套图独立调用，以 Stand.png 为身份参考。最终提示词集保存在 `generation-prompts.json`。

本地原始生成来源记录：`output/ResonantStarOwl/manifest.json`。原始稿检查：`validation.json`；成品逐帧检查：`normalized-validation.json`；规范化脚本：`normalize.py`。这些文件位于 `output/ResonantStarOwl/`，不作为游戏运行依赖。

已在独立 QA 工程尝试原生 Unity 导入验收，但本次运行因许可客户端初始化超时而在资产验收前崩溃，因此不宣称 Unity 原生导入已通过。日志在 `output/ResonantStarOwl/unity-import.log`。文件级检查已验证全部切片命名、rect 边界、PPU、透明留白以及 pivot 对齐；未操作主工程编辑器或正式存档。
