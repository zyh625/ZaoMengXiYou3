# GreenHammer 翡翠震地冲击波

2026-11-01。内置 imagegen 绘制，Stand.png 只作描线、赛璐璐阴影和翡翠绿/古金色参考。

- 文件：GreenHammerShockwave.png。1024×1024 RGBA 真透明，4×4，共 16 帧，每格 256×256。
- 每帧特效包围框不超过 179×153（格宽 70%、格高 60%），四侧至少 24px 全透明；碎石与尘土同样计入。
- 所有帧使用同一绘制尺度，F1/F16 保留自然更小的占幅；不逐帧放大填满单元格。
- 固定地面线：单元格左上坐标 y=216，Pivot=(0.5,0.15625)，PPU=100；全动画不得逐帧紧边裁剪或按余烬重设 Pivot。
- 已附 Sprite/Multiple 的 16 个固定矩形切片，命名 GreenHammerShockwave_01 至 _16；建议 16 FPS、单次播放 1 秒，禁止循环。
- 顺序：首帧细裂纹与撞击点 → F2–4 扬尘隆起 → F5–8 向左扩展低矮翡翠波峰 → F9–12 拉长变薄、碎石回落 → F13–15 消散 → F16 仅余少量微弱亮屑。
- 图中只有压力波、能量边缘、尘土与小石屑；没有角色、武器、地面纹理、背景或背景投影。
- GreenHammerShockwave.Notes.json 保存最终提示词、逐帧 alpha 范围、边距、地面线和哈希。

## 接入现有工程

项目已有 Assets/Projects/Prefabs/SeparateEffort/GreenHammerShockwave.prefab，当前使用 RedChain 占位图。
本次仅新增美术和切片，不改其战斗脚本、碰撞体或预制体。接入时可将 frames.sprites 换为本图 16 个切片，保持 loop=false、fps=16。
本图最大真实宽度约 1.78 Unity 单位（PPU=100），若沿用原预制体的 width/nativeWidth 缩放，应将 nativeWidth 配合新美术宽度调整，避免按旧占位图尺寸拉伸。
