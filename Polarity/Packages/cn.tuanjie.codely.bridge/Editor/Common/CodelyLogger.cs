using System;
using UnityEditor;
using UnityEngine;

namespace UnityTcp.Editor.Helpers
{
    public enum CodelyLogMode
    {
        Off,
        WarningsAndErrors,
        All
    }

    /// <summary>
    /// Centralized logger for the Codely Bridge package.
    ///
    /// All package logging is routed through here so it can be globally
    /// enabled/disabled and consistently branded. This is the ONLY place in the
    /// package that is allowed to call <see cref="UnityEngine.Debug"/> directly;
    /// every other type should log through CodelyLogger.
    ///
    /// Select a log mode via AI/Logging or <see cref="Mode"/>.
    /// </summary>
    public static class CodelyLogger
    {
        private const string Prefix = "<b><color=#2EA3FF>Codely Bridge</color></b>:";

        private const string ModeKey = "Codely.Logging.Mode";
        // Previous on/off setting, read once when no mode has been selected yet.
        private const string EnabledKey = "Codely.Logging.Enabled";

        // Separate verbose toggle, shared with the existing bridge debug behavior.
        private const string VerboseKey = "UnityTcp.DebugLogs";

        private const string OffMenu = "AI/Logging/Off";
        private const string WarningsAndErrorsMenu = "AI/Logging/Warnings and Errors";
        private const string AllMenu = "AI/Logging/All Logs";
        private const string VerboseMenu = "AI/Logging/Enable Verbose Logs";

        // Cached copies of the persisted prefs. Logs are emitted both from background
        // threads (IPC loops, ThreadPool callbacks) and from [InitializeOnLoad] static
        // constructors during domain reload — in neither case can we rely on EditorPrefs
        // being readable: off the main thread it throws, and constructor ordering across
        // assemblies is undefined, so a separate init method may not have run yet.
        //
        // Instead we lazily read the prefs on first access (EnsureLoaded). The first
        // access happens on the main thread (the bridge's static constructor), where
        // EditorPrefs works; the result is cached in volatile fields that any thread can
        // then read. InitializeOnLoadMethod is kept only as an early best-effort warm-up.
        private static volatile CodelyLogMode s_mode = CodelyLogMode.WarningsAndErrors;
        private static volatile bool s_verbose = false;
        private static volatile bool s_loaded = false;

        [InitializeOnLoadMethod]
        private static void Init() => EnsureLoaded();

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            try
            {
                // Throws if called off the main thread; we keep defaults and retry on
                // the next access until a main-thread caller succeeds.
                int mode = EditorPrefs.HasKey(ModeKey)
                    ? EditorPrefs.GetInt(ModeKey, (int)CodelyLogMode.WarningsAndErrors)
                    : EditorPrefs.HasKey(EnabledKey)
                        ? (EditorPrefs.GetBool(EnabledKey, false)
                            ? (int)CodelyLogMode.All : (int)CodelyLogMode.Off)
                        : (int)CodelyLogMode.WarningsAndErrors;
                s_mode = mode >= (int)CodelyLogMode.Off && mode <= (int)CodelyLogMode.All
                    ? (CodelyLogMode)mode : CodelyLogMode.WarningsAndErrors;
                s_verbose = EditorPrefs.GetBool(VerboseKey, false);
                s_loaded = true;
            }
            catch { /* not on the main thread yet */ }
        }

        /// <summary>Selected output level; defaults to warnings and errors.</summary>
        public static CodelyLogMode Mode
        {
            get { EnsureLoaded(); return s_mode; }
            set
            {
                if (value < CodelyLogMode.Off || value > CodelyLogMode.All)
                    throw new ArgumentOutOfRangeException(nameof(value));
                EnsureLoaded();
                s_mode = value;
                s_loaded = true;
                try { EditorPrefs.SetInt(ModeKey, (int)value); } catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Compatibility switch: reports whether any logs are enabled. Setting true
        /// selects All, as the former on/off switch did; setting false selects Off.
        /// </summary>
        public static bool Enabled
        {
            get { return Mode != CodelyLogMode.Off; }
            set { Mode = value ? CodelyLogMode.All : CodelyLogMode.Off; }
        }

        /// <summary>
        /// Verbose/diagnostic logging. Gates <see cref="Verbose"/>; has no effect
        /// unless <see cref="Mode"/> is <see cref="CodelyLogMode.All"/>.
        /// </summary>
        public static bool VerboseEnabled
        {
            get { EnsureLoaded(); return s_verbose; }
            set
            {
                EnsureLoaded();
                s_verbose = value;
                s_loaded = true;
                try { EditorPrefs.SetBool(VerboseKey, value); } catch { /* ignore */ }
            }
        }

        /// <summary>Informational log. Emitted only in All mode.</summary>
        public static void Log(object message)
        {
            if (Mode != CodelyLogMode.All) return;
            Debug.Log($"{Prefix} {message}");
        }

        /// <summary>Warning log. Suppressed only in Off mode.</summary>
        public static void LogWarning(object message)
        {
            if (Mode == CodelyLogMode.Off) return;
            Debug.LogWarning($"<color=#cc7a00>{Prefix} {message}</color>");
        }

        /// <summary>Error log. Suppressed only in Off mode.</summary>
        public static void LogError(object message)
        {
            if (Mode == CodelyLogMode.Off) return;
            Debug.LogError($"<color=#cc3333>{Prefix} {message}</color>");
        }

        /// <summary>Exception log. Suppressed only in Off mode.</summary>
        public static void LogException(Exception exception)
        {
            if (Mode == CodelyLogMode.Off) return;
            Debug.LogException(exception);
        }

        /// <summary>
        /// Diagnostic log. Only emitted when All mode and the separate verbose
        /// switch are both enabled.
        /// </summary>
        public static void Verbose(object message)
        {
            if (Mode != CodelyLogMode.All || !VerboseEnabled) return;
            Debug.Log($"{Prefix} {message}");
        }

        // ---- Editor menu toggles ------------------------------------------------

        [MenuItem(OffMenu, priority = 1000)]
        private static void SelectOff() => Mode = CodelyLogMode.Off;

        [MenuItem(OffMenu, true)]
        private static bool SelectOffValidate()
        {
            Menu.SetChecked(OffMenu, Mode == CodelyLogMode.Off);
            return true;
        }

        [MenuItem(WarningsAndErrorsMenu, priority = 1001)]
        private static void SelectWarningsAndErrors() => Mode = CodelyLogMode.WarningsAndErrors;

        [MenuItem(WarningsAndErrorsMenu, true)]
        private static bool SelectWarningsAndErrorsValidate()
        {
            Menu.SetChecked(WarningsAndErrorsMenu, Mode == CodelyLogMode.WarningsAndErrors);
            return true;
        }

        [MenuItem(AllMenu, priority = 1002)]
        private static void SelectAll() => Mode = CodelyLogMode.All;

        [MenuItem(AllMenu, true)]
        private static bool SelectAllValidate()
        {
            Menu.SetChecked(AllMenu, Mode == CodelyLogMode.All);
            return true;
        }

        [MenuItem(VerboseMenu, priority = 1003)]
        private static void ToggleVerbose() => VerboseEnabled = !VerboseEnabled;

        [MenuItem(VerboseMenu, true)]
        private static bool ToggleVerboseValidate()
        {
            Menu.SetChecked(VerboseMenu, VerboseEnabled);
            return Mode == CodelyLogMode.All;
        }
    }
}
