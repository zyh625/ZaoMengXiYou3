# PhaseWeaver 相位织使动画

恢复日期：2026-11-03。本文依据保留的动画清单、生成记录和验收日志重建；图片、切片与原始参考没有重绘。

角色动画（各 16 帧）：Idle、Walk、LowPhaseCast、HighPhaseCast、InvertField、AllyExchange、Hit、Death。
独立特效（各 16 帧）：Effects/LowPhase、Effects/HighPhase、Effects/InvertField、Effects/AllyExchange。
建议帧率：Idle 10、Walk 14、Hit 18、Death 12，其余 16；仅 Idle/Walk 循环。

每张成品 PNG 为真实透明 RGBA、1024×1536、4×4、单元格 256×384；192 帧全部非空。
保留统一角色缩放、原点 (128,192)、PPU 100、自定义 pivot 与紧凑切片，名称为 Action_0..15。
原验证的四边最小留白为 48/74/48/73 像素，最大占宽 62.5%、占高 61.72%。

角色图仅表现动作和实体道具，招式特效单独存放在 Effects。
Stand.png、Design.json、GeneratedDrafts 及其中的修正原稿均保留；HighPhaseCast 第 4 帧使用保存的双手姿势修正图。
完整逐帧数据见 animation-manifest.json，提示词见 generation-prompts.json，验收见 validation.txt。
本次文件恢复未接入新的敌人玩法或修改 C# 行为，也不将历史静态检查等同于 Play Mode 实测。
