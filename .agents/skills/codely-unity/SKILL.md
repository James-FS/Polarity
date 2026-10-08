---
name: codely-unity
description: Unity and Tuanjie game development through the Codely Unity Bridge, including editor workflows, gameplay scene and asset pitfalls, animation, and visual debugging. Use for Unity/Tuanjie editor operations and game development tasks; do not use for Blender or unrelated engines.
---

# codely-unity

Codely 官方 Unity 工具工作流与 Unity/Tuanjie 游戏开发经验，映射到本机 `codely-unity` MCP（Codely Unity Bridge → 团结编辑器）。

## Important — 每次操控前

1. 先 `codely-unity_bridge_status`，确认 `unity_port > 0` 且 `reason=ready`。
2. 再 `codely-unity_manage_editor action=get_state`，看 Play/编译/场景/dirty/`writeGuardInPlayMode`。
3. **Play Mode 默认写保护**（`writeGuardInPlayMode: deny`）。写场景/资产前若在 Play，先 `manage_editor action=stop`；工具返回 `write_blocked_in_play_mode` 时改只读策略或退 Play。
4. 工程根：`D:\Code\rpg\rpg`（场景扩展名 `.scene`，团结引擎）。项目踩坑记忆见 [references/project-memory.md](references/project-memory.md) 与 `rpg/CODELY.md`。

## 工具映射（Codely → 本 MCP）

| Codely | codely-unity MCP |
|---|---|
| unity_editor get_state / play / stop / refresh | `manage_editor` get_state / play / stop / refresh |
| unity_scene | `manage_scene` |
| unity_gameobject | `manage_gameobject` |
| unity_asset | `manage_asset` |
| unity_console | `read_console` |
| unity_menu | `execute_menu_item` |
| unity_screenshot / manage_screenshot | `screenshot` |
| unity_gameview | `manage_gameview` |
| exec_editor_script / exec_runtime_script | `execute_csharp`（`execution_mode`: editor / play） |
| — | `manage_job`（异步任务 status/check/list/cancel） |
| unity_script | `manage_script`（create/read/update/delete/apply_text_edits/**validate**/get_sha，带编译校验） |
| unity_shader | `manage_shader`（detect_render_pipeline / **compile** / preview / ensure_material_shader_for_srp） |
| unity_input | `manage_input`（mouse_click / key_press / mouse_drag） |
| unity_bake | `manage_bake`（bake_navmesh / bake_lighting / clear_navmesh / clear_baked_data） |
| unity_package | `manage_package`（install_package / remove_package / list_packages） |
| unity_dialog | `manage_dialog`（click——编辑器模态弹窗卡死时的救兵） |
| — | `analyze_multimedia`（引擎侧；视频 ≤8MB，大文件先 ffmpeg 压缩） |
| unity_wiki / vfs_* | **本 MCP 无**；查不到再本地搜/读文档 |

> 工具有两套命名：引擎侧 `unity_*` 与 bridge 分发名 `manage_*`（如 unity_screenshot ↔ manage_screenshot）。某个名字报「工具不存在」时，先试另一套前缀再下结论。

## 动作速查（对照 bridge 1.0.81）

| 工具 | 动作 |
|---|---|
| `manage_editor` | get_state / refresh / play / pause / resume / stop / **step**（Play 单帧步进，调时序利器）/ wait_for_idle / request_compile / get_compilation_summary / get_selection / get_windows / focus_window / ensure_tag / ensure_layer / get_tags / get_layers |
| `manage_scene` | ensure_scene_open / ensure_scene_saved / create / load / save / get_hierarchy / get_active / get_build_settings |
| `manage_gameobject` | create / create_batch / edit_batch（find 用 `captureAs` 捕 `$alias`）/ modify / delete / find / list_children / get_components / add_component / remove_component / set_component_property / ensure_component / ensure_renderer_material / ensure_mesh_collider_mesh / select |
| `manage_asset` | create（仅 Folder/Material/PhysicsMaterial/ScriptableObject）/ modify / delete / duplicate / move / **search**（按名/类型搜资产）/ get_info / get_components / ensure_has_meta / ensure_meta_integrity / import（单资产 reimport，勿当日常刷新） |
| `manage_gameview` | get_resolution / **set_resolution**（如复现 "Codely 1280x720"）/ list_resolutions |
| `manage_bake` | bake_navmesh / bake_lighting / clear_navmesh / clear_baked_data（均为一次调用即完成，勿轮询） |
| `manage_package` | install_package / remove_package / list_packages（同样一次调用即完成） |
| `screenshot` | capture_scene_view / capture_main_camera / capture_specific_camera / **capture_asset**（Edit Mode 资产预览，cardinal 七方向、正交、wireframe）/ **capture_ui_toolkit** / start_game_view_recording / finish_game_view_recording（`capture_game_view` 为禁用遗留入口） |

## 核心规则（Codely 官方 1–8，已映射）

### 1. 配置类改动优先幂等 ensure

Tag、Layer、加组件、赋材质、MeshCollider、`.meta` 等：优先 `manage_gameobject action=ensure_component` 或 asset 侧 ensure/modify。**Tag/Layer 的 ensure 入口在 `manage_editor`（`action=ensure_tag` / `ensure_layer`），不在 manage_gameobject。** 已满足（`alreadyExists` / `alreadyAssigned`）时 **禁止再写**。

批量层级用 `create_batch` / `edit_batch`，用 `captureAs` + `$alias` 传引用；多字段用 `{ componentName, componentProperties }` 一次设完，不要反复单字段。

资产写入：`manage_asset create` 仅 Folder/Material/PhysicsMaterial/ScriptableObject；`move` 目标须在 `Assets/` 下；Prefab 改字段用 `modify` + `{ ComponentTypeName: { field: value } }`。

### 2. 关键边界必须 `ensure_scene_saved`

在这些时刻调用 `manage_scene action=ensure_scene_saved`（或显式 save）：

- 进 Play 前（`manage_editor action=play`）
- bake / 大批量写之后
- 向用户报告「任务完成」之前

**不要**每一步都盲目自动存场景。dirty 不可信时（见 project-memory）用文件关键字校验。

### 3. 长操作一次调用即完成，禁止轮询

`refresh` / `play` / `pause` / `resume` / `stop` / bake / package install 返回即结束。不要 `op_id`、不要二次调用「查进度」。确需更久用 `timeoutSeconds` / `timeout_ms`。异步 job 仅用 `manage_job` 查询。

### 4. Console：clear → act → read

凡错误/警告有因果关系时：

1. 动手前 `read_console action=clear`（`manage_editor refresh` 已自带 clear）
2. 执行操作
3. `read_console action=get` —— 读到的都是边界之后产生的

禁止对着未 clear 的 Console 猜哪条是刚才的操作引起的。

### 5 / 5b. Edit 与 Play 验证策略不同

**Edit Mode（已 stop）：**

1. 写项目文件  
2. 每批一次 `manage_editor action=refresh`  
3. 看 refresh 内嵌错误 / `read_console`  
4. 仅当「视觉布局」是问题本身才 `screenshot`

**Play Mode：**

- 用 `execute_csharp execution_mode=play` 做行为验证；优先 **结构化返回值** 而非截图：

```csharp
return new {
  active = go.activeInHierarchy,
  enabled = renderer.enabled,
  visible = renderer.isVisible,
  keyword = mat.IsKeywordEnabled("_X"),
  bounds = renderer.bounds
};
```

- 用游戏自身 API 驱动（施法/生成/改状态），不要模拟 Input。
- 必须点真实 UI（UGUI/TMP 按钮）时用 `manage_input`（mouse_click/key_press/mouse_drag）；行为验证本身仍优先游戏 API。
- C# 已返回 `visible=false` / `enabled=false` **就是**结论，不要再截图「确认」。
- 动态特效：一次 `execute_csharp` 内协程触发 + `ManageScreenshot` 多帧 GIF；不要「这一枪触发、下一枪截图」（RTT 会错过 1–2s 特效）。
- 跨调用 runtime 状态会累积，验证前先 `stop` 再进 Play（见 project-memory）。
- 验证后若还要编辑：先退 Play（或用 editor 模式脚本，会自动退 Play）。

### 6. Play Mode 默认写保护

写操作被拒时：改只读，或 `manage_editor action=stop` 后再写。

### 7. 禁止把 Reimport 当刷新

不要用 `execute_menu_item` 跑 `Assets/Reimport All` 当日常 refresh。Asset reimport 极重，且会回滚场景实例覆盖（见 project-memory FBX 条）。

### 7b. 截图是最后手段

默认验证：结构化 C# 返回 + Console + hierarchy/属性回读。

`screenshot` 仅用于：

- 黑屏 / 洋红（shader 错一眼能看）
- 最终视觉签收
- 用户明确要求静态画面

**禁止：**

- Play 中 `capture_main_camera` / `capture_specific_camera`（会被拒；确属静态画面时官方逃生门：`allow_static_in_play_mode: true` + `static_reason` 说明理由）；`capture_game_view` 已禁用
- 用静态帧证明「短特效没播」
- 对固定时刻 VFX 截图调用分析
- 语义验证已通过后，因构图/角度/VFX 不够美而重录——那是审美问题，报告完成或问用户

**动态交付物（按优先级）：**

1. **`execute_csharp` 直接带 `record_game_view` 参数**（对象，参数同 start_game_view_recording：`durationSeconds` 1-15 默认 4 / `fps` 5-30 默认 15 / `encodeFps` 1-30 默认 2 / `startDelay` / `scale` 0.25-1）。一次调用完成「编译后自动开录 → 脚本触发特效 → 收 MP4」。bridge 源码明确警告：**分步 start→execute→finish 录制会因编译与工具调用延迟错过短特效**，所以短特效一律用这个；要求 `enable_repl:false` 且已进 Play
2. `screenshot` 的 `over_time` 参数（`"N"` 或 `"NxS"`：N 帧、帧间隔 S 个模拟步）——轻量多帧，不需要视频时优先
3. 协程 GIF（BeginCapture → CaptureFrame → EndCaptureToGif，见 csharp-examples）
4. 分步 `start_game_view_recording` / `finish_game_view_recording`（`encodeFps=2` 慢放便于逐帧看）——仅用于**输入或外部事件触发**、无法在脚本内触发的场景

录完必须看内容再声称验证通过；交 `analyze_multimedia` 前大 MP4 先压到 8MB 内（ffmpeg scale=960:540 crf32）。

### 8. 优先非视觉证据

常规验证顺序：

1. `read_console`（clear→act→get）
2. `manage_editor get_state`
3. hierarchy / 组件 / 属性回读
4. 资产元数据
5. 定点 `Debug.Log`
6. 截图 / 录屏（最后）

不要为了「看一眼中间进度」就进 Play。动态行为（移动/动画/模拟/状态切换/粒子/输入响应）在编译保存后 **不算完成**，除非用户明确跳过 runtime 验证。

## C# 脚本硬约束（`execute_csharp`）

| 约束 | 说明 |
|---|---|
| 禁止阻塞 | `Thread.Sleep` / `Task.Wait` / `Task.Result` / `SpinWait` / 忙等 —— 会被拒 |
| 顶层 `await` 无效 | 包一层 `async Task<T> RunAsync()` 再 `return RunAsync();` |
| 顶层 `yield return` 无效 | 定义本地 `IEnumerator Run()` 再 `return Run();` |
| `IEnumerator` 返回值丢弃 | 要结构化结果用 `Task<string>` / `Task<T>` |
| 协程 yield 支持 | `null` / `WaitForSeconds` / `WaitForSecondsRealtime` / `WaitUntil` / `WaitWhile` / `WaitForEndOfFrame` / `WaitForFixedUpdate` / 嵌套 IEnumerator |
| `Task.Run` 体内禁 Unity API | 仅纯计算；`await` 回主线程后再碰 Unity |
| `await` 后仍在主线程 | Unity SynchronizationContext，可直接调 Unity API |
| 验证片段不要写 .cs 落盘 | 用片段执行，避免 domain reload；**任务本身是写游戏代码时**用 `manage_script`（update + `validate`，带编译校验），不要裸写文件 |

其他参数：`timeoutSeconds`（默认 300、上限 3600，`0`=不限时）；`enable_repl:true` + `script_session_id` 开 REPL 会话（跨调用保持状态，验证完记得收尾）；`unlock_domain_reload`；`record_game_view`（一次性录 MP4，见 7b）。

完整 few-shot 见 [references/csharp-examples.md](references/csharp-examples.md)。

## 游戏场景、资产与动画开发

### 场景布局与尺度

- 放置或缩放模型时依据变换后的真实尺寸。贴地可将 `mesh.bounds` 的 8 个角点用 `TransformPoint` 转到世界坐标，再以最低世界 Y 对齐地面；同一帧的 `Renderer.bounds` 可能仍是缓存值。
- 不同资产包之间优先保持水平占地与视觉比例，单纯按高度缩放可能让树干或道具变得过细。
- 场景散布优先用确定性网格或有疏密梯度的团簇；保留路径、树干、地标、门、出生点和交互目标的通行空间。避让按树干处理，不要用树冠范围作为障碍。
- 按实际相机取景检查建筑和地标，尤其是没有相机碰撞时的出生点周边。按帧数估算等待时间前，先确认当前场景帧率。

### 模型、材质与动画

- FBX 实例可能没有 Animator；有些模型目录的 prefab/main asset 为空，需要直接加载 FBX 再实例化。替换模型时清理旧占位子物体，避免 `GetComponentInChildren<Renderer>()` 先命中占位物。
- 修改 FBX `.meta` 可能触发重导入并丢失场景实例覆盖；检查 `externalObjects` 材质 GUID，灰白模型常由引用失效或多子网格材质槽未全部赋值造成。
- URP 转换器可能只处理 `.mat` 资产，场景内嵌材质需单独检查并重建为 URP/Lit。检查 LOD 时读 `LODGroup.GetLODs()`，不要依赖可能命中未激活 LOD 的 `GetComponentInChildren<Renderer>()`。
- 作为子资产新建 BlendTree 时调用 `AssetDatabase.AddObjectToAsset`；否则控制器可能保存出空 motion。Tuanjie 的 Simple1D 阈值可能被归一化到 0..1，driver 应对应限幅。
- 按用途检查 FBX 动画的 loop 设置：Idle/Walk/Run 通常循环，Attack/Death 通常单次。用当前位置差估算移动速度；不要依赖每帧重力移动会覆盖的 `CharacterController.velocity`。用当前 clip 信息和权重检查 Animator；`normalizedTime > 1` 本身不表示单次动画循环了。
- TMP 动态字体生成后，把 atlas 和 material 持久化为子资产，并预烘常用字形；否则新会话可能读到空引用。Tuanjie TMP 3.0.9 的 `GlyphRenderMode` 位于 `UnityEngine.TextCore.LowLevel`。矩形裁切优先 `RectMask2D`；复杂嵌套 LayoutGroup/ContentSizeFitter 易产生错误高度，可改用单个富文本 TMP。

### 游戏运行验证

- 验证动态行为时在真实运行场景检查。由 `Awake` 或 spawner 创建的对象要等其出现后再查询；传送 `CharacterController` 前先禁用组件，设置位置后再启用。
- 如果战斗验证可能导致死亡并重载场景，先保护测试玩家，避免场景切换使当前脚本引用失效。
- UI 像素验证应在 `WaitForEndOfFrame` 后读取画面。需要证明动画时序时录制短片；截图只用于静态外观确认。
- 通过 shell 将资源复制到 `Assets` 后，先执行 AssetDatabase refresh/import 再检查资源。

详细工具规则、C# 约束和桥接流程仍以上文为准；项目级踩坑细节见 [references/project-memory.md](references/project-memory.md)。

## 推荐操作序

```text
bridge_status → get_state
  → [若 Play 且需写] stop
  → clear console
  → 改（ensure_* / batch / execute_csharp）
  → refresh（每批一次）
  → read_console
  → ensure_scene_saved（关键边界）
  → [动态行为] play / execute_csharp(play) 结构化验证
  → [最终签收才 screenshot / 录屏]
```

## Examples

- 「在 Town 场景放一棵树」→ 规则 1/2：ensure 或 batch 创建 → refresh → console → 保存；静态布局可 `capture_scene_view`。
- 「验证玩家受击掉血」→ 退 Play 改代码（若需要）→ save → `execute_csharp` play 模式驱动 TakeDamage，返回 HP 数值；不截图。
- 「做个爆炸特效看看」→ 一次协程：触发 API → BeginCapture(0.5f) → 每帧 CaptureFrame → EndCaptureToGif → finally AbortCapture。

## Troubleshooting

遇到 `SendDataChannelMessage REJECTED` / `dataChannel=null`，先读 [原生串流排障](references/native-streaming.md)：区分浏览器串流与 MCP/TCP，按实际日志和是否需要浏览器预览选择处理方式。Polarity 的项目修复记录见 `docs/桥接串流修复.md`。

| 症状 | 处理 |
|---|---|
| `unity_port` 为 0 / 连不上 | 团结编辑器需打开并加载 bridge；查 `Temp/.com-unity-codely.json` |
| `write_blocked_in_play_mode` | `manage_editor action=stop` 后再写 |
| `refresh_blocked_in_play_mode` | refresh 在 Play/Paused 下也会被拒（不只写操作被拒）；先 stop |
| 脚本被拒 | 查是否顶层 await/yield、阻塞 API、Play 写操作 |
| `IEnumerator` 无结果 | 改 `Task<T>` |
| 截图失败 | Play 中勿用 camera 捕获（或 `allow_static_in_play_mode`+`static_reason`）；改结构化返回或录屏 |
| shader 编译错误 | `manage_shader action=compile` 结构化返回，别只靠 refresh + console 猜 |
| 编辑器模态弹窗卡死 | `manage_dialog action=click` |
| FBX 灰白/位移丢失 | 勿用 reimport；见 [references/project-memory.md](references/project-memory.md) |
| 场景存了但磁盘仍旧 | dirty 不可靠；显式 SaveScene + 校验文件关键字 |
