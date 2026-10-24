# 角色红色外围描边

将 `CharacterRedOutline.mat` 拖到角色的 SpriteRenderer → Materials → Element 0。

- Shader Graph 类型：Universal / Sprite Unlit。
- `Outline Color`：默认红色，可调整颜色和透明度。
- `Outline Width (Pixels)`：默认 2，范围 0–8，单位为源纹理像素；0 关闭描边。
- `_MainTex` 由 SpriteRenderer 自动提供，切换动画帧时无需手动设置贴图。
- 保留 SpriteRenderer 的颜色、透明度与翻转功能。

外围描边只能绘制在 Sprite 网格覆盖的区域。导入设置建议使用 `Mesh Type = Full Rect`，并在角色四周保留不少于描边宽度的透明像素；本 Graph 不自动扩展网格。精灵表的每帧及 Sprite Atlas 也需保留足够透明间隔，避免采样到相邻图像。

已在 Unity 6000.4.9f1 / URP 17.4.0 中通过全部 4 个 Pass 编译及渲染验证：角色原色、红色外围、宽度归零关闭和 SpriteRenderer 透明度控制。
