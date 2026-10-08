using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Codely.Newtonsoft.Json.Linq;
using UnityTcp.Editor.Helpers;
using UnityEditor;

namespace UnityTcp.Editor.Tools
{
    /// <summary>
    /// Introspection tool: the live catalog of Bridge tool types, their actions, the
    /// client-side timeout budgets they require, and per-action parameter schemas
    /// (the Bridge counterpart of unity CLI's GET /api/commands). Lets a client
    /// render an always-accurate `tuanjie command` listing without maintaining a
    /// parallel static table.
    ///
    /// The catalog itself is authored in command-catalog.xml (same folder as this
    /// file) and parsed once per domain — adding a command means editing that data
    /// file, not C# code. Tool entries there must stay in sync with the dispatch
    /// switch in <see cref="UnityTcpBridge.ExecuteCommand"/> (add the new case AND
    /// the tool entry in the same change). A tool may also declare aliases —
    /// alternative names the dispatch accepts via <see cref="ResolveTypeAlias"/>
    /// (e.g. "eval" for execute_csharp_script); every type and alias must be
    /// unique across the catalog (guard-tested). Actions for tools with a `handler`
    /// attribute are reflected at runtime from that handler's ActionHandlers
    /// dictionary (zero drift); tools that dispatch otherwise — inline switches
    /// (manage_screenshot, manage_input), build-method dictionaries (manage_scene), or no action at all (script execution) — declare their
    /// actions with <action> elements, the authoritative list for those tools.
    ///
    /// Schemas are hand-authored metadata (the handlers read JObject directly, so
    /// there are no typed signatures to reflect): name/type/required/default/
    /// description per parameter, extracted from each handler's actual JObject
    /// reads. Author the <action> for a new command in the same change that adds
    /// it — ListToolsCatalogTests turns the suite red when the catalog and the
    /// wire surface drift apart. Schemas appear in the wire response only for
    /// actions that have one; clients must treat absence as "no metadata",
    /// not "no parameters".
    ///
    /// timeout_ms / action_timeouts_ms are CLIENT budget guidance: the server-side
    /// run-to-completion budget (StepJob timeout, compile/play-mode transitions) plus a
    /// safety margin. A client should hold its socket open at least this long before
    /// declaring a timeout, so slow-but-healthy transitions are not cut off client-side.
    /// </summary>
    public static class ListTools
    {
        // Internal (not private): ListToolsCatalogTests reads the registry over
        // InternalsVisibleTo to assert every handler class stays registered.
        internal sealed class ToolDescriptor
        {
            public string Type;
            public string Description;
            /// Handler class whose ActionHandlers dictionary is reflected for actions;
            /// null → <see cref="Actions"/> is authoritative.
            public Type HandlerType;
            public string[] Actions;
            /// Alternative names the dispatch accepts for this tool (CLI-style
            /// short names, e.g. "eval" for execute_csharp_script); null → none.
            public string[] Aliases;
            /// Client timeout budget in ms; 0 → client default (30 s).
            public int TimeoutMs;
            public Dictionary<string, int> ActionTimeoutsMs;
            /// Hand-authored per-action parameter schemas; null → not documented yet.
            public Dictionary<string, ActionSchema> ActionSchemas;
            /// Tool-level parameters for action-less commands (eval family, custom
            /// tools); null → not documented.
            public ParamSchema[] Params;
        }

        /// <summary>One declared parameter of an action.</summary>
        internal sealed class ParamSchema
        {
            public string Name;
            /// One of: string | int | float | bool | enum | json | vec3 | path.
            public string Type;
            public bool Required;
            /// Default value in string form (wire "default"); null = no default.
            public string Default;
            /// Enum values / alias / format hints.
            public string Note;
            public string Description;
        }

        /// <summary>Per-action metadata: what it does and the parameters it accepts.</summary>
        internal sealed class ActionSchema
        {
            public string Description;
            public ParamSchema[] Params;
        }

        // Alias → canonical type map for the dispatch pre-step
        // (ResolveTypeAlias). No initializer on purpose: LoadCatalog populates
        // it while loading the catalog, and a field initializer here would
        // run after that and wipe the map.
        private static Dictionary<string, string> m_AliasToType;

        // Internal (not private): ListToolsCatalogTests reads the registry over
        // InternalsVisibleTo to assert every handler class stays registered.
        // Parsed once per domain from command-catalog.xml. The parse resolves its
        // path through PackageManager.PackageInfo — a main-thread-only editor
        // API — so the registry is warmed at domain load and the getter refuses
        // to load off the main thread: the command pump's fetch thread classifies
        // commands before any dispatch runs, and a background first touch would
        // resolve no package and poison the whole domain with an empty catalog.
        private static volatile List<ToolDescriptor> m_Tools;
        private static readonly List<ToolDescriptor> EmptyRegistry = new List<ToolDescriptor>();
        private static int m_MainThreadId = -1;

        // Throttle for the post-warm-up load retry: a failed load (missing or corrupt
        // XML, or the PackageManager lookup not ready yet during startup) keeps the
        // canonical commands working while aliases and list_tools stay degraded, so
        // retry from the main thread instead of requiring a domain reload. Fast at
        // first (a transient PackageManager race clears within seconds), then slowed
        // down so a permanently broken catalog does not spam the log every few ticks.
        private const double CatalogRetrySeconds = 5.0;
        private const double CatalogRetrySlowSeconds = 60.0;
        private const int CatalogRetryFastAttempts = 12;

        [InitializeOnLoadMethod]
        private static void WarmOnDomainLoad()
        {
            // Domain load happens on the main thread: capture it, then parse the
            // catalog before any background thread can ask for it.
            m_MainThreadId = Thread.CurrentThread.ManagedThreadId;
            EnsureLoaded();
            if (m_Tools != null) return;

            double nextAttempt = EditorApplication.timeSinceStartup;
            int fastAttempts = 0;
            EditorApplication.CallbackFunction retry = null;
            retry = () =>
            {
                if (m_Tools != null)
                {
                    EditorApplication.update -= retry;
                    return;
                }
                if (EditorApplication.timeSinceStartup < nextAttempt) return;
                bool fast = fastAttempts < CatalogRetryFastAttempts;
                fastAttempts++;
                nextAttempt = EditorApplication.timeSinceStartup +
                              (fast ? CatalogRetrySeconds : CatalogRetrySlowSeconds);
                EnsureLoaded();
                if (m_Tools != null)
                    EditorApplication.update -= retry;
            };
            EditorApplication.update += retry;
        }

        /// <summary>
        /// Loads and publishes the catalog when it is not published yet. Main thread
        /// only — the load resolves its path through a main-thread-only editor API.
        /// Anything that starts a background consumer of the alias map (the command
        /// pump's fetch thread) must call this first: [InitializeOnLoadMethod] ordering
        /// is not deterministic, so the dependency is enforced at the hand-off point.
        /// </summary>
        internal static void EnsureLoaded()
        {
            if (m_Tools != null) return;
            // Publish only a real registry: a failed parse keeps retrying on the
            // next main-thread access instead of caching a poisoned empty catalog
            // for the rest of the domain.
            var tools = LoadCatalog();
            if (tools.Count > 0) m_Tools = tools;
        }

        internal static List<ToolDescriptor> Tools
        {
            get
            {
                var tools = m_Tools;
                if (tools != null) return tools;
                if (m_MainThreadId < 0 ||
                    Thread.CurrentThread.ManagedThreadId != m_MainThreadId)
                    return EmptyRegistry;
                EnsureLoaded();
                return m_Tools ?? EmptyRegistry;
            }
        }

        // ─── catalog parsing (command-catalog.xml) ────────────────────────────

        private const string CatalogFileName = "command-catalog.xml";

        /// <summary>
        /// Parse the catalog; on a missing or corrupt file fail LOUDLY (empty
        /// registry) so the guard tests turn red and the CLI falls back to its
        /// static table instead of serving a silently wrong catalog.
        /// </summary>
        private static List<ToolDescriptor> LoadCatalog()
        {
            var catalog = ParseCatalogFile(CatalogPath());
            if (catalog.Count > 0)
            {
                // Alias map for the dispatch pre-step. A collision is impossible with a
                // green suite (AliasesAreUniqueAcrossTheCatalog asserts uniqueness);
                // last-wins otherwise so dispatch stays functional.
                m_AliasToType = new Dictionary<string, string>();
                foreach (var d in catalog)
                    foreach (var alias in d.Aliases ?? Array.Empty<string>())
                        m_AliasToType[alias] = d.Type;
            }
            return catalog;
        }

        /// <summary>
        /// Parses the catalog XML at <paramref name="path"/>. Internal for the guard
        /// tests, which drive the failure modes (unresolved package path, missing file,
        /// corrupt XML) without touching the published registry: any failure logs and
        /// yields an empty list — "nothing to publish" — never a partial catalog.
        /// </summary>
        internal static List<ToolDescriptor> ParseCatalogFile(string path)
        {
            try
            {
                if (path == null)
                {
                    CodelyLogger.LogError("[ListTools] The Bridge assembly does not resolve to a " +
                                          "package folder — command-catalog.xml cannot be located; " +
                                          "the tool catalog is unavailable.");
                    return new List<ToolDescriptor>();
                }
                if (!File.Exists(path))
                {
                    CodelyLogger.LogError($"[ListTools] command-catalog.xml not found at {path} — " +
                                          "the tool catalog is unavailable.");
                    return new List<ToolDescriptor>();
                }

                var result = new List<ToolDescriptor>();
                var doc = new XmlDocument();
                doc.Load(path);
                foreach (XmlElement toolEl in doc.SelectNodes("/catalog/tool"))
                    result.Add(ParseTool(toolEl));
                return result;
            }
            catch (Exception ex)
            {
                CodelyLogger.LogError($"[ListTools] Failed to load command-catalog.xml: {ex.Message}");
                return new List<ToolDescriptor>();
            }
        }

        /// Absolute path of the catalog next to this script, resolved through the
        /// package the assembly was loaded from (works for registry, local and
        /// embedded packages alike).
        private static string CatalogPath()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ListTools).Assembly);
            return package == null
                ? null
                : Path.Combine(package.resolvedPath, "Editor/Bridge/Tools", CatalogFileName);
        }

        /// <summary>
        /// Canonical wire type for `type`, resolving catalog aliases (e.g.
        /// "eval" → "execute_csharp_script"). Unknown names pass through
        /// unchanged so the dispatch switch reports them as unknown types.
        /// </summary>
        public static string ResolveTypeAlias(string type)
        {
            if (string.IsNullOrEmpty(type)) return type;
            var map = m_AliasToType;
            return map != null && map.TryGetValue(type, out var canonical) ? canonical : type;
        }

        private static ToolDescriptor ParseTool(XmlElement el)
        {
            var d = new ToolDescriptor
            {
                Type = Required(el, "type"),
                Description = Required(el, "description"),
                TimeoutMs = OptionalInt(el, "timeoutMs"),
                Aliases = ParseCsv(el, "aliases"),
            };

            var handler = el.GetAttribute("handler");
            if (!string.IsNullOrEmpty(handler))
                d.HandlerType = ResolveHandlerType(handler, d.Type);

            var timeoutsEl = (XmlElement)el.SelectSingleNode("actionTimeouts");
            if (timeoutsEl != null)
            {
                d.ActionTimeoutsMs = new Dictionary<string, int>();
                foreach (XmlElement t in timeoutsEl.SelectNodes("timeout"))
                    d.ActionTimeoutsMs[Required(t, "action")] = OptionalInt(t, "ms");
            }

            // <action> elements carry the per-action schemas (every tool that has
            // them) and, for tools without a reflected ActionHandlers table, the
            // authoritative action list in document order.
            var actionNames = new List<string>();
            var schemas = new Dictionary<string, ActionSchema>();
            foreach (XmlElement actionEl in el.SelectNodes("action"))
            {
                var name = Required(actionEl, "name");
                schemas[name] = new ActionSchema
                {
                    Description = Text(actionEl, "description"),
                    Params = ParseParams(actionEl),
                };
                actionNames.Add(name);
            }
            if (schemas.Count > 0) d.ActionSchemas = schemas;
            if (d.HandlerType == null) d.Actions = actionNames.ToArray();

            // Tool-level params (action-less commands: eval family, custom tools).
            var toolParams = ParseParams(el);
            if (toolParams.Length > 0) d.Params = toolParams;

            return d;
        }

        private static ParamSchema[] ParseParams(XmlElement parent)
        {
            var result = new List<ParamSchema>();
            foreach (XmlElement p in parent.SelectNodes("param"))
            {
                result.Add(new ParamSchema
                {
                    Name = Required(p, "name"),
                    Type = Required(p, "type"),
                    Required = p.GetAttribute("required") == "true",
                    Default = Optional(p, "default"),
                    Note = Optional(p, "note"),
                    Description = string.IsNullOrWhiteSpace(p.InnerText) ? null : p.InnerText.Trim(),
                });
            }
            return result.ToArray();
        }

        /// <summary>Resolve a `handler` attribute to its class in this assembly
        /// (bare names get the UnityTcp.Editor.Tools prefix).</summary>
        private static Type ResolveHandlerType(string name, string toolType)
        {
            var fullName = name.Contains(".") ? name : "UnityTcp.Editor.Tools." + name;
            var resolved = typeof(ListTools).Assembly.GetType(fullName, false);
            if (resolved == null)
                throw new Exception($"tool '{toolType}': handler class '{fullName}' does not exist");
            return resolved;
        }

        // ─── small XML attribute helpers ──────────────────────────────────────

        private static string Required(XmlElement el, string attribute)
        {
            var value = el.GetAttribute(attribute);
            if (string.IsNullOrEmpty(value))
                throw new Exception($"<{el.Name}> element is missing the required '{attribute}' attribute");
            return value;
        }

        /// <summary>Text of a direct child element, or null when absent.</summary>
        private static string Text(XmlElement parent, string childName)
        {
            var child = (XmlElement)parent.SelectSingleNode(childName);
            return child == null ? null : child.InnerText.Trim();
        }

        private static string Optional(XmlElement el, string attribute)
            => el.HasAttribute(attribute) ? el.GetAttribute(attribute) : null;

        private static int OptionalInt(XmlElement el, string attribute)
        {
            var value = Optional(el, attribute);
            if (value == null) return 0;
            if (!int.TryParse(value, out var parsed))
                throw new Exception($"<{el.Name}>: '{attribute}' must be an integer, got '{value}'");
            return parsed;
        }

        /// <summary>Comma-separated attribute as a trimmed array; null when the
        /// attribute is absent or empty (wire form omits the field).</summary>
        private static string[] ParseCsv(XmlElement el, string attribute)
        {
            var value = Optional(el, attribute);
            if (value == null) return null;
            var parts = value.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            return parts.Length > 0 ? parts : null;
        }

        // Built-in tool surface only changes via domain reload, so build once per domain.
        private static List<object> m_CachedToolJson;

        public static object HandleCommand(JObject @params)
        {
            var tools = Tools;
            if (tools.Count == 0)
            {
                // Never answer list_tools with success and an empty listing: the
                // catalog failed to load (missing or corrupt XML) or has not been
                // published yet, and callers would cache the empty answer as truth.
                // The main-thread retry in the Tools getter has already run, so this
                // means the load genuinely failed. Throw so the dispatch's unified
                // error path answers with a single failure envelope — a Response.Error
                // returned here would be wrapped into an outer success by
                // ExecuteCommand and disguise the failure on the wire.
                throw new InvalidOperationException(
                    "The tool catalog is unavailable: command-catalog.xml is missing, " +
                    "corrupt, or not loaded yet. Check the editor log for a [ListTools] " +
                    "error and retry once the issue is fixed.");
            }

            if (m_CachedToolJson == null)
                m_CachedToolJson = tools.Select(BuildToolJson).ToList();

            // Custom tools change without a domain reload (RegisterTool) — read fresh.
            List<string> customTools;
            try
            {
                customTools = ExecuteCustomTool.GetRegisteredTools().ToList();
            }
            catch (Exception ex)
            {
                CodelyLogger.LogWarning($"[ListTools] Custom tool enumeration failed: {ex.Message}");
                customTools = new List<string>();
            }
            // The registration order is an implementation detail; pin the wire order
            // like the reflected action lists so catalog consumers see a stable listing.
            customTools.Sort(StringComparer.Ordinal);

            return Response.Success($"Listed {tools.Count} tool types.", new Dictionary<string, object>
            {
                ["bridge_version"] = BridgeVersion(),
                ["tool_count"] = tools.Count,
                ["custom_tools"] = customTools,
                ["tools"] = m_CachedToolJson,
            });
        }

        /// Comma-separated tool type list for the unknown-type error in the
        /// dispatch switch, with each tool's aliases in parentheses so clients
        /// discover the alternative names they may also send.
        public static string ToolTypeList()
            => string.Join(", ", Tools.Select(t =>
                t.Aliases == null || t.Aliases.Length == 0
                    ? t.Type
                    : $"{t.Type} ({string.Join("|", t.Aliases)})"));

        private static object BuildToolJson(ToolDescriptor d)
        {
            var actions = EnumerateActions(d);
            return new Dictionary<string, object>
            {
                ["type"] = d.Type,
                ["description"] = d.Description,
                ["aliases"] = d.Aliases,
                ["actions"] = actions,
                ["action_count"] = actions.Count,
                ["timeout_ms"] = d.TimeoutMs > 0 ? (object)d.TimeoutMs : null,
                ["action_timeouts_ms"] = d.ActionTimeoutsMs,
                ["action_schemas"] = BuildActionSchemasJson(d),
                ["params"] = BuildParamsJson(d.Params),
            };
        }

        /// Wire form of a tool-level param list (action-less commands); null when absent.
        private static object BuildParamsJson(ParamSchema[] @params)
        {
            if (@params == null) return null;

            var paramList = new List<Dictionary<string, object>>();
            foreach (var p in @params)
            {
                var param = new Dictionary<string, object>
                {
                    ["name"] = p.Name,
                    ["type"] = p.Type,
                    ["required"] = p.Required,
                };
                if (p.Default != null) param["default"] = p.Default;
                if (p.Note != null) param["note"] = p.Note;
                if (p.Description != null) param["description"] = p.Description;
                paramList.Add(param);
            }
            return paramList;
        }

        /// Wire form of the hand-authored schemas: {action: {description, params: [...]}}.
        /// Absent metadata is omitted per action (clients treat missing as "not documented").
        private static object BuildActionSchemasJson(ToolDescriptor d)
        {
            if (d.ActionSchemas == null) return null;

            var result = new Dictionary<string, object>();
            foreach (var kvp in d.ActionSchemas)
            {
                var paramList = new List<Dictionary<string, object>>();
                foreach (var p in kvp.Value.Params ?? Array.Empty<ParamSchema>())
                {
                    var param = new Dictionary<string, object>
                    {
                        ["name"] = p.Name,
                        ["type"] = p.Type,
                        ["required"] = p.Required,
                    };
                    if (p.Default != null) param["default"] = p.Default;
                    if (p.Note != null) param["note"] = p.Note;
                    if (p.Description != null) param["description"] = p.Description;
                    paramList.Add(param);
                }
                result[kvp.Key] = new Dictionary<string, object>
                {
                    ["description"] = kvp.Value.Description,
                    ["params"] = paramList,
                };
            }
            return result;
        }

        /// Reflect the handler's private static ActionHandlers dictionary (the same table
        /// ActionRouter.Route dispatches through), falling back to the declared list.
        private static List<string> EnumerateActions(ToolDescriptor d)
        {
            if (d.HandlerType != null)
            {
                var field = d.HandlerType.GetField(
                    "ActionHandlers", System.Reflection.BindingFlags.Static |
                                       System.Reflection.BindingFlags.NonPublic |
                                       System.Reflection.BindingFlags.Public);
                if (field != null && field.GetValue(null) is IDictionary dict)
                {
                    var names = new List<string>();
                    foreach (var key in dict.Keys)
                        if (key is string name) names.Add(name);
                    if (names.Count > 0)
                    {
                        // Dictionary order is an implementation detail; pin the wire
                        // order so consumers — and any future model-tool cache built
                        // on this listing — see a stable sequence across reloads.
                        names.Sort(StringComparer.Ordinal);
                        return names;
                    }
                }
                CodelyLogger.LogWarning(
                    $"[ListTools] No ActionHandlers table found on {d.HandlerType.Name}; falling back to the declared action list.");
            }
            return (d.Actions ?? new string[0]).ToList();
        }

        private static string BridgeVersion()
        {
            try
            {
                return UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(UnityTcpBridge).Assembly)?.version;
            }
            catch
            {
                return null;
            }
        }
    }
}
