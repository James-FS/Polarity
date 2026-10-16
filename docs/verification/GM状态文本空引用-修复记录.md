# GM 状态文本空引用修复

- 版本：2026-10-16；当前工作区，团结 1.10.1。
- 原因：HUD 中的 Status 文本已移除，GMView.statusText 为空，Refresh 仍直接访问其 text。启动时 GMManager.Bind 与 GMView.Start 各触发一次异常。
- 改动：GMView.cs 仅在 statusText 非空时更新文本，兼容当前不显示状态文本的布局。
- 验证：Bridge refresh 编译 0 错误、0 警告；一次实际 Play 中确认 statusText 为空、GMManager 已绑定、面板初始关闭，调用 TogglePanel 打开/关闭、切换无敌开关触发刷新均成功；Console 0 条日志。退出 Play，场景 dirty=false。
- 范围：通过 GM API 验证共用刷新路径；未测试物理 F1 输入及完整通关路线。未修改预制体或场景布局。
