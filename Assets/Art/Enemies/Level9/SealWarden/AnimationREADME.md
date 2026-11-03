# SealWarden 动作与独立特效

使用内置 image_gen 生成，按已授权的脚本方式整理透明轮廓、统一动作内比例和脚底线。原 Stand.png、Design.json 和 README.md 保留。

全部精灵表：RGBA真透明PNG、1024×1536、4列×4行，每格256×384，共16帧。帧序从左到右、从上到下。生产图片没有线框、编号或标签。

| 目录/文件 | 内容 | 建议FPS | 循环 |
| --- | --- | --- | --- |
| SpriteSheets16/Idle.png | 重装呼吸、肩甲和背碑轻晃 | 10 | 是 |
| SpriteSheets16/Walk.png | 左右脚交替重步行走 | 10 | 是 |
| SpriteSheets16/SealPunch.png | 左右重玺错拍、不同高度出拳 | 14 | 否 |
| SpriteSheets16/DeployBoundary.png | 双拳举高落下、双背碑前移落地 | 14 | 否 |
| SpriteSheets16/GateCompression.png | 双拳胸前内收挤压、恢复 | 14 | 否 |
| SpriteSheets16/Hit.png | 向画面右侧后仰、滑步支撑、恢复 | 16 | 否 |
| SpriteSheets16/Death.png | 双膝落地、向前倒下、保持终态 | 12 | 否 |
| Effects/SealPunchImpact.png | 两次独立的高低错拍金白拳印与碎光 | 16 | 否 |
| Effects/BoundarySeal.png | 双楔预告、立起、保持、开裂消散 | 16 | 否 |
| Effects/GateCompression.png | 两侧封界光面预警、内收一次、碎散 | 16 | 否 |

角色表只画实体装备和姿势，未烘焙拳风、落地光圈、挤压光面、尘土或外放粒子。立界角色动画中保留移动的实体双背碑；独立BoundarySeal可用于部署后的副目标表现。拳击与挤压特效不含角色或拳套。

切片已提供PNG.meta：Multiple、每格256×384、PPU100、Full Rect、Bilinear、关闭MipMap和压缩，命名为文件名_0..15。统一Pivot=(0.5,56/384)，即格内(128,56)底部坐标；脚底线为左上坐标y328。独立特效也采用同一格内锚点，运行时可按需要给视觉节点增加偏移。

每帧非透明范围宽≤178、高≤288；左右留白≥32、上下≥40，已逐格检查。动作内使用单一等比缩放，未逐帧拉伸角色。Idle、Walk和Hit的末帧复制首帧闭合；SealPunch和GateCompression末帧接共同Idle起始姿势；死亡保持倒地终态，不额外透明淡出。

GenerationPrompts.json保存完整提示词及生成源文件路径；SealWarden.AnimationNotes.json记录动作、帧矩形/边距和SHA256；Validation.json记录160格实际alpha边界。AI绘制的动作与饰纹仍建议在Unity内以实际播放速度做美术审阅。
