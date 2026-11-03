# 补充独立特效（2026-11-03）

本目录新增：SeamMark.png, AnchorMark.png, Afterimage.png。

使用内置 image_gen 生成，按已授权的脚本整理进行透明杂边清理、同一动画统一比例缩放、4×4 排版与切片。每张为 1024×1024 RGBA，16 格各 256×256，按行读取，PPU100，至少24px全透明留白，无格线或文字。
原有角色图片、meta、脚本、预制体和场景没有被修改。完整提示词见 MissingEffects.GenerationPrompts.json。

建议播放16fps；生成/保持/消失阶段按需求控制：StarAnchor 的 F6–11 是保持脉冲，StarKnot 的 F7–10 是亮峰；SeamMark 的 F1–4 是金线预警、F5–12 是红色阶段；Afterimage 的 F9–12 是空心描边。
SeamMark 和 Afterimage 的第16格故意全透明，表示已经消失，不是漏帧。残影去除了生成稿的亮色污染，深蓝填色以透明度淡出，之后保留青色描边并碎散。

地面特效 StarAnchor、StarKnot、AnchorMark 的原点为每格(128,216)，meta pivot=(0.5,0.15625)；其他原点为(128,128)，pivot=(0.5,0.5)。每套动画只用一个缩放比例，各帧不独立拉伸。

SilkLine 的 PNG 仍是256×256等大网格，meta 为每格截取横向 x=28..227 的200×256区域，保留原点。铺设星丝时使用这些切片，以200px/PPU100=2u的间距连接，或按渲染缩放同比调整。两端RGBA边缘逐帧相同；直接按整个256px单元格平铺会把留白也铺进去。

文件验收涵盖RGBA、全透明边距、16个切片名称与rect、PPU100、GUID唯一性、星丝拼接边缘和原文件SHA256不变。尚未执行原生Unity导入或Play Mode验收，未接入游戏判定。
诊断预览、原稿路径和处理记录在 output/MissingSixEffects/，预览背景和标签不在成品PNG中。
