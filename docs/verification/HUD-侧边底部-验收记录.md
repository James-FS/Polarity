# HUD 侧边与底部验收记录

- 版本：2026-10-14。六张 PNG 已复制到 `Assets/Polarity/Art/UI`，用 Sprite 导入矩形裁掉透明留白，原始图片内容未改。
- `HUD.prefab` 新增 `Frame/SideLeft`、`Frame/SideRight`、`Controls/BottomPanel` 和 `Controls/ControlPrompts`；底条显示左键正极、右键负极、Space 减速、Esc 暂停，原有教学与反馈文字保留在其上方。
- [1280×720 实际运行预览](HUD-侧边底部-最终预览.png)：边框接缝、顶部文字、底部提示和辅助按钮均可见。地图中心点击未被 UI 拦截，底部 UI 点击被拦截；暂停按钮可使 Running → Paused → Running。Console 0 错误、0 警告。
- 范围：此轮只接入 HUD 美术和调整底部文字位置；地图布局、相机取景和玩法规则未改。其他宽高比未逐一验证。
