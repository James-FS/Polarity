# GM 入口按钮隐藏

- 版本：2026-10-15；HUD.prefab、Game.scene、GMView.cs。
- 改动：GMOpenButton 在预制体与场景中停用；GMView.Refresh 将其保持为停用，避免状态更新时重新显示。保留内部引用，GMManager 的 F1 快捷键代码不变。
- 验证：刷新编译0错误0警告；运行时入口按钮在启动、面板打开和关闭时均隐藏；通过F1共用的TogglePanel方法打开/关闭正确，GM管理器绑定有效。Console无错误或警告，已退出Play并保存。
- 范围：仅GM入口显示；未验证物理F1按键，未运行通关测试。
