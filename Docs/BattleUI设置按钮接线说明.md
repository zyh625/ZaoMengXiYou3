# BattleUI 设置按钮

已更新 `Assets/Projects/Prefabs/Level/BattleUI.prefab` 与 `Assets/Projects/Scripts/UI/PlayerButtonManager.cs`。

`Canvas/Images/Settings/buttons` 中每个 Button 的 OnClick 均保存一个指向本预制体 PlayerButtonManager 的持久绑定：

| 对象 | 函数 | 行为 |
| --- | --- | --- |
| backGame | BackGame | 关闭菜单，恢复进入菜单前的游戏时间倍率 |
| backMap | BackMap | 沿用原有返回地图流程 |
| backMenu | BackMenu | 沿用原有返回主菜单流程 |
| CloseAudio | CloseAudio | 全局音量归零，自己隐藏、OpenAudio 显示 |
| OpenAudio | OpenAudio | 恢复静音前音量，自己隐藏、CloseAudio 显示 |
| CloseUI | CloseUI | 隐藏 HUD，自己隐藏、OpenUI 显示 |
| OpenUI | OpenUI | 恢复 HUD，自己隐藏、CloseUI 显示 |
| Intro | OnIntro | 显示 Settings/Intro，隐藏设置按钮区和设置底图 |
| Settings/Intro/back | BackSettings | 回到设置页，继续暂停游戏 |

HUD 通过六个可编辑 CanvasGroup 隐藏：HealthBar、AngryBar、SkillBar、Head、BossBlood、BossBloodBar。隐藏时不禁用 HUD 对象或 UIManager，数据订阅继续工作；打开时恢复原来的透明度、交互和射线状态，不强行显示原本隐藏的 Boss 血条。设置页和菜单入口保留，方便重新打开 HUD。PlayerButtonManager 的 hudGroups 可在 Inspector 调整。

没有在运行时覆盖 Sprite、位置、尺寸或锚点。开关状态在打开设置页时同步，Intro 返回不调用 BackGame；重新进入设置默认显示设置页。持久 OnClick 已绑定，无需再手工添加同名回调。

验收：主工程 `dotnet build Assembly-CSharp.csproj` 0 错误（1 条原有 Bird 警告）；独立物理 QA 工程原生点击预制体按钮并验证开关互显、音量恢复、HUD 状态、Intro 返回、暂停及恢复。结果与数量见 [运行日志](../output/BattleUISettingsAudit/runtime.txt) 和 [保护范围审查](../output/BattleUISettingsAudit/audit.txt)。原始 Sprite/Image、RectTransform 及关卡场景保持不变。
