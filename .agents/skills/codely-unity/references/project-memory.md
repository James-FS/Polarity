# FogHarbor 项目踩坑摘要（来自 rpg/CODELY.md）

操控本工程（`D:\Code\rpg\rpg`）时优先对照。完整版见项目内 `CODELY.md`。

## 工具链 / 验证

- refresh 每批一次；Play 下写会被拒，先 stop。
- `execute_csharp` 跨调用累积 runtime 状态 → 验证前 stop 再进。
- `IEnumerator` 返回值丢弃 → 结构化用 `Task<T>`。
- 驱动 `cc.Move` 前先 `cc.enabled=false` 传送再置回。
- Awake 生成对象（WolfSpawner 等）要轮询等待再查。
- 战斗测试先 `SetDefenseBonus(1000)`，防打死触发 LoadScene 导致脚本引用失效。
- 帧率：Town ≈190fps，Forest ≈60fps（按帧估时要换算）。
- GameView 固定分辨率 "Codely 1280x720"；`capture_game_view` 禁用；Play 中 `capture_main_camera` 会失败。
- UI 显示验证：`WaitForEndOfFrame` + `ReadPixels`。
- 动态效果：录 MP4（Windows，`encodeFps=2`）；视频分析前大文件先压到 8MB 内。

## 场景 / 对象

- dirty 不可信 → 关键节点显式 `EditorSceneManager.SaveScene` + `Select-String` 校验场景文件。
- 多场景逐个 open+save（`OpenScene(Single)` 会丢未保存改动）。
- 改动前记录完整 transform + 源 prefab 路径；`DestroyImmediate` 后勿再访问该对象（先收集再删）。
- 批量对象收进 `Environment_*` 空父组（`SetParent` 保持世界坐标）。
- 场景回滚：备份文件 `File.Copy` 回 `Assets/Scenes` + Refresh + OpenScene（比逐对象重建可靠）。

## 贴地 / 尺寸 / 分布

- 贴地：mesh.bounds 8 角点 `TransformPoint`，校验 `minY=0`（同帧 Renderer.bounds 可能是缓存）。
- 两套资产缩放：按「水平占地等比」换算，不要按高度。
- 尺寸基准：角色 2.7m、树 ≈2.2~3.5 倍人高、狼 1.67m。
- 随机撒点会扎堆 → 确定性网格或「团簇+疏密梯度」；避让针对树干/路石/交互点/出生点/门（勿用树冠包围盒）。
- 拾取物 BoxCollider `size.y=1`、`center.y=0.5` 贴地；植被无碰撞（设计如此）。
- Kenney path_stone：长轴 1.0 横铺时排距 1.0、沿街间距 0.62 才无缝。

## 相机

- `CameraFollow` offset(0, 7.5, -7.5) + LookAt → 45° 俯角、FOV60。
- 相机无碰撞；街南不放房子；`Camera.main` 可能为 null（未打标签）。
- `capture_scene_view` 只拍活动场景；大面积场景先 LookAt + 缩小 size。

## Prefab / FBX / 材质

- FBX 实例不带 Animator（手动 AddComponent）；Quaternius 目录 prefab main asset 为 null → 从 FBX `LoadAssetAtPath` 实例化。
- 替换模型先清旧灰盒子物体（Body/Marker 会劫持 Renderer 引用）。
- prefab 资产 `Renderer.activeInHierarchy` 恒 False（看 `activeSelf`）。
- 改 FBX `.meta` 会 reimport 并回滚场景实例 → 用 `Object.Instantiate` 克隆 + 手动材质/MeshCollider，勿 PrefabUtility。
- `.meta` externalObjects guid 失效 → 灰白无贴图；换成对应 `.mat` guid。
- Kenney path_stone 3 个子网格，三槽都要赋材质。
- 场景光强 ≈1.5×，`_BaseColor` 按 1/1.5 预暗。
- 切 URP 时 Converter 只转 `.mat` 资产；场景内嵌材质需手工重建 URP/Lit。
- LOD 可见性勿用 `GetComponentInChildren<Renderer>`（会命中未激活 LOD）→ `GetLODs()`。
- URP `shadowDistance=50`（Forest 远处树影会消失，已知）。

## 动画

- 新建 BlendTree 必须 `AssetDatabase.AddObjectToAsset` 注册为子资产（否则 motion=NULL）。
- 团结 Simple1D BlendTree 阈值强制 0..1（driver 输出 Clamp01）。
- Quaternius FBX clip 默认 `loop=false`；Idle/Walk/Run 设 `loopTime=true`；Attack/Death 单次。
- 非循环 clip 停在状态内时 `normalizedTime` 会继续涨（>1 ≠ 已循环）。
- 勿用 `CharacterController.velocity` 判速（重力 Move 覆盖）→ 位置差。
- 验证用 `GetCurrentAnimatorClipInfo(0)`；Attack 重播 `animator.Play`（OnAttackLanded）。

## TMP 中文

- 动态字体资产创建后必须 `AddObjectToAsset(atlas, material)`，否则磁盘 NULL。
- `TryAddCharacters` 预烘；全局回退 + LiberationSans 局部回退双配。
- 团结 TMP 3.0.9 的 `GlyphRenderMode` 在 `UnityEngine.TextCore.LowLevel`。
- 透明 Mask 不渲染子内容 → `RectMask2D`；多层 Layout 易错高 → 单 TMP 富文本 + ContentSizeFitter。

## 服务端 / PowerShell

- AIBot Server：手动注入 `.env` 的 `AIBOT_LLM_KEY`；用前查 `127.0.0.1:5000`。
- `Invoke-WebRequest` 中文 body：`[Text.Encoding]::UTF8.GetBytes` + `charset=utf-8`；JSON 用 `Get-Content -Encoding UTF8`。

## 项目事实速查

- 团结 1.10.1 / Tuanjie 2022.3.62t13 / URP HighFidelity Linear HDR；命名空间 `FogHarbor.*`。
- 场景：`Boot` / `Town` / `Forest`（`.scene`）。
- Town 120×120，三房北街，主街石路；Forest 树 202 + Flora 482 + 石路网。
- Bridge 端口以 `.com-unity-codely.json` 为准（当前会话曾见 1986；CODELY.md 记过 8655，以握手文件为准）。
- TJGenerators 生成 prefab 缩放异常 → 直接实例化 FBX 重算尺寸；积分不足会 `NotEnoughBalance`。
