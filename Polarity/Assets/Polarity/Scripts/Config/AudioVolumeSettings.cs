using System;
using UnityEngine;

namespace Polarity.Config
{
    /// <summary>Player volume preferences, independent of authored clip levels.</summary>
    public static class AudioVolumeSettings
    {
        private const string MasterKey = "Polarity.Audio.Master";
        private const string ButtonKey = "Polarity.Audio.Button";
        private const string GameplayKey = "Polarity.Audio.Gameplay";
        private static bool loaded, dirty;
        private static float master = 1f, button = 1f, gameplay = 1f;
        public static event Action Changed;

        public static float Master { get { EnsureLoaded(); return master; } }
        public static float Button { get { EnsureLoaded(); return button; } }
        public static float Gameplay { get { EnsureLoaded(); return gameplay; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            loaded = dirty = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => EnsureLoaded();

        private static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);

        private static void EnsureLoaded()
        {
            if (loaded) return;
            master = Clamp(PlayerPrefs.GetFloat(MasterKey, 1f));
            button = Clamp(PlayerPrefs.GetFloat(ButtonKey, 1f));
            gameplay = Clamp(PlayerPrefs.GetFloat(GameplayKey, 1f));
            loaded = true;
            AudioListener.volume = master;
        }

        public static void SetMaster(float value)
        {
            EnsureLoaded();
            Set(ref master, value, MasterKey);
        }

        public static void SetButton(float value)
        {
            EnsureLoaded();
            Set(ref button, value, ButtonKey);
        }

        public static void SetGameplay(float value)
        {
            EnsureLoaded();
            Set(ref gameplay, value, GameplayKey);
        }

        private static void Set(ref float field, float value, string key)
        {
            value = Clamp(value);
            if (Mathf.Approximately(field, value)) return;
            field = value;
            PlayerPrefs.SetFloat(key, value);
            dirty = true;
            AudioListener.volume = master;
            Changed?.Invoke();
        }

        public static void RestoreDefaults()
        {
            SetMaster(1f);
            SetButton(1f);
            SetGameplay(1f);
        }

        public static void Save()
        {
            if (!dirty) return;
            PlayerPrefs.Save();
            dirty = false;
        }
    }
}
