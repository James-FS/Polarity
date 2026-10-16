# GM 面板绑定恢复

- 版本：2026-10-15；仅 Game.scene 的 GMView.manager 场景引用。
- 原因：HUD/GMView.manager 为空，Refresh 判定未就绪，关闭面板和入口；GMManager 的 F1 状态切换代码仍存在。
- 修复：绑定到 SimulationWorld 上的 GMManager，记录场景实例覆盖并保存。未修改代码、HUD样式或关卡。
- 验证：Play 中 Manager 已绑定且可用；TogglePanel（F1 使用的同一方法）打开/关闭均正确；入口按钮回调打开/关闭均正确；BlocksPointer随之切换。面板卡片屏幕范围(370,110)..(910,610)，Scale=(1,1,1)。Console无警告或错误，退出Play并保存。
- 限制：未模拟物理F1键；本次验证限定面板显示链路与按钮，不运行通关测试。
