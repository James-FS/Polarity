# codely-unity C# few-shot（Codely 官方示例，已映射到 `execute_csharp`）

`summary` 写一行目标；`execution_mode`: `editor` = Edit Mode，`play` = Play Mode（会自动进 Play 等待）。

## Edit Mode

### Log Message

```csharp
UnityEngine.Debug.Log("Hello from C#!");
```

### Get Scene Info

```csharp
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
UnityEngine.Debug.Log($"Active Scene: {scene.name}");
UnityEngine.Debug.Log($"Scene Path: {scene.path}");
UnityEngine.Debug.Log($"Root Object Count: {scene.rootCount}");
```

### List GameObjects

```csharp
var gameObjects = UnityEngine.Object.FindObjectsOfType<UnityEngine.GameObject>();
foreach (var go in gameObjects)
{
    if (go.transform.parent == null)
        UnityEngine.Debug.Log($"Root GameObject: {go.name}");
}
```

### Create GameObject

```csharp
var go = new UnityEngine.GameObject("MyObject");
go.AddComponent<UnityEngine.Light>();
UnityEngine.Debug.Log($"Created GameObject: {go.name}");
```

### Get Project Path

```csharp
UnityEngine.Debug.Log($"Data Path: {UnityEngine.Application.dataPath}");
UnityEngine.Debug.Log($"Persistent Data Path: {UnityEngine.Application.persistentDataPath}");
```

### Write Shader File

写完 `.shader` 后 `ImportAsset`；编译验证优先 `manage_shader action=compile`（结构化返回编译错误；预览用 `action=preview`），不要只靠 refresh + console 猜。

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

var path = "Assets/Shaders/CustomLit.shader";
var dir = Path.GetDirectoryName(path);
if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
File.WriteAllText(path, @"Shader ""Custom/CustomLit""
{
    SubShader { Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include ""UnityCG.cginc""
        float4 vert(float4 v:POSITION):SV_POSITION { return UnityObjectToClipPos(v); }
        fixed4 frag():SV_Target { return 1; }
        ENDCG } }
}");
AssetDatabase.ImportAsset(path);
return path;
```

## Play Mode / 跨帧

### 动态特效首选：`record_game_view` 一次性录制

`execute_csharp` 直接带 `record_game_view` 参数（JSON 对象：`durationSeconds` 1-15 默认 4 / `fps` 5-30 默认 15 / `encodeFps` 1-30 默认 2 / `startDelay` / `scale` 0.25-1），一次调用完成「编译后自动开录 → 脚本触发特效 → 收 MP4」。bridge 源码明确警告分步 start→execute→finish 会因编译与工具调用延迟错过短特效，短特效一律用这个；要求 `enable_repl:false` 且已进 Play。

```csharp
// 工具参数: execution_mode=play, record_game_view={"durationSeconds":3,"encodeFps":2}
var fx = Object.FindObjectOfType<YourVfxController>();
fx.Play("Thunder");
return new { triggered = true, fx.transform.position };
```

更轻量的多帧方案：`screenshot` 动作带 `over_time`（`"10"` 或 `"10x0.5"` = 10 帧、帧间隔 0.5 个模拟步），不需要视频时优先。

### Async / Await

顶层 `await` 会被拒。包内层 `async Task<T>`，`await` 后仍在主线程。禁止 `.Wait()` / `.Result` / `Thread.Sleep`。`Task<T>` 返回值会进入工具结果。

```csharp
using System.Threading.Tasks;
using UnityEngine;

async Task<string> RunAsync()
{
    Debug.Log("Before await (main thread)");
    await Task.Delay(500);
    var go = new GameObject("CreatedAfterAwait");
    Debug.Log($"After await, created {go.name} on the main thread");
    return go.name;
}

return RunAsync();
```

### Async with background work

只有 `Task.Run` 体内在后台线程，且 **不能** 调 Unity API。

```csharp
using System.Threading.Tasks;
using UnityEngine;

async Task<int> RunAsync()
{
    int sum = await Task.Run(() =>
    {
        int total = 0;
        for (int i = 0; i < 1_000_000; i++) total += i;
        return total;
    });
    Debug.Log($"Computed sum on background thread: {sum}");
    return sum;
}

return RunAsync();
```

### Coroutine（跨帧）

`IEnumerator` **不能** 把返回值带回工具结果；要结构化数据用 `Task<T>`。

```csharp
using System.Collections;
using UnityEngine;

IEnumerator Run()
{
    for (int i = 0; i < 3; i++)
    {
        Debug.Log($"Coroutine tick {i}");
        yield return new WaitForSeconds(0.5f);
    }
    var go = new GameObject("CoroutineDone");
    Debug.Log($"Coroutine finished, created {go.name}");
}

return Run();
```

### VFX 多帧捕获 GIF（`record_game_view` 不可用时的替代）

要点：用游戏 API 触发（不要 Input）；`BeginCapture(0.5f)` 限内存；**必须** `yield return null` 再 `CaptureFrame`；帧数上限（不要用 `Time.unscaledTime`，Play 暂停会卡死）；`finally` 里 `AbortCapture`。

```csharp
using System.Collections;
using UnityEngine;
using UnityTcp.Editor.Tools;

IEnumerator RecordCast()
{
    // Trigger the effect via YOUR game's API — not simulated keyboard / legacy Input.*.
    // e.g. FindObjectOfType<YourVfxController>().Play("Thunder");

    ManageScreenshot.BeginCapture(0.5f);
    try
    {
        const int maxFrames = 60;
        for (int frames = 0; frames < maxFrames; frames++)
        {
            yield return null;
            ManageScreenshot.CaptureFrame();
        }

        var path = ManageScreenshot.EndCaptureToGif("Temp/vfx.gif");
        Debug.Log($"VFX capture saved: {path}");
    }
    finally
    {
        ManageScreenshot.AbortCapture();
    }
}

return RecordCast();
```

## 结构化验证模板（优先于截图）

```csharp
// execution_mode=play
var go = GameObject.Find("Player");
var r = go.GetComponentInChildren<Renderer>();
return new {
    active = go.activeInHierarchy,
    enabled = r.enabled,
    visible = r.isVisible,
    bounds = r.bounds
};
```
