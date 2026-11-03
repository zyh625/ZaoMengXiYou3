# 赤炉灵尊 / RedFurnaceLord 动作与独立特效

本批使用内置 imagegen，根据本目录 `level7.png` 生成，随后按已授权的脚本整理方式分离各帧、统一留白和帧格。原始参考图保留。

## 角色动作

8 张透明 PNG 均为 **1024×1536、4×4、16 帧、每格 256×384**，按从左到右、从上到下播放。

| 文件 | 动作 |
| --- | --- |
| Idle.png | 待机呼吸 |
| Walk.png | 原地沉重行走 |
| PestleSmash.png | 双手举杵后下砸 |
| FurnaceBreath.png | 蓄力、前倾喷吐姿势 |
| ChainSweep.png | 扭身、附着链条横扫 |
| FurnaceOpen.png | 炉门打开、最终保持露芯 |
| Hit.png | 单次后仰受击 |
| Death.png | 踉跄、跪下、侧倒，最终炉芯熄灭 |

角色表只含姿势、武器/链条等本体部件及炉芯/嘴部内部亮度，不叠加外部攻击特效。所有可见内容均处于独立帧格，最大宽度 178px、最大高度 288px；透明留白至少左右32px、上下40px。

角色脚底基线设为格内从上数 **y=312px**；对应 Unity FullRect 切片 pivot 为 `(128,72)` 像素，即归一化 `(0.5,0.1875)`，PPU 100。六组回到准备姿势的动作最后一帧与第一帧一致；开炉最终保持打开，死亡最终保持倒地。

## 独立特效

`Effects/` 中 4 张透明 PNG 均为 **1024×1024、4×4、16 帧、每格 256×256**。逐格至少24px透明留白。PPU100、FullRect、中心 pivot。

- `PestleImpact.png`：落地闪光、裂纹形亮痕、低矮铜尘、碎石与余烬。
- `FurnaceBreath.png`：朝图像左侧的白热火芯、橙火和烟边，消退至余烬。
- `CoreBurst.png`：开炉核心闪光、环形余烬/热浪、最终微弱稳态光芒。
- `ChainSweepArc.png`：独立链扫空气划痕，仅空气弧线与微尘；实体链条属于角色。最终透明消失。

## 校验记录

`RedFurnaceLord.ValidationSummary.json` 记录 12 张表、192 格的尺寸、透明留白、切片帧数和 PPU 检查。

`RedFurnaceLord.ArtValidation.json` 保留逐帧 alpha 边界、源图路径及输出 SHA256；`RedFurnaceLord.GenerationPrompts.json` 保留最终各资产使用的提示词。为修正生成稿边缘裁切，受击和喷吐各有一帧使用邻近完整姿势保持。所有生产图不含网格线、编号或文字。
