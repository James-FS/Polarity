using Polarity.Config;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Short-lived UI audio host so closing a panel or loading a scene does not cut clicks.</summary>
    [AddComponentMenu("")]
    public sealed class ButtonAudioPlayer : MonoBehaviour
    {
        private static ButtonAudioPlayer instance;
        private static ButtonAudioConfig settings;
        private static bool warnedMissingSettings;
        private AudioSource source;
        private float remaining;

        public AudioClip LastPlayedClip { get; private set; }
        public int PlayedSoundCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            instance = null;
            settings = null;
            warnedMissingSettings = false;
        }

        public static void PlayClick()
        {
            if (settings == null) settings = Resources.Load<ButtonAudioConfig>(ButtonAudioConfig.ResourcePath);
            if (settings == null)
            {
                if (!warnedMissingSettings)
                {
                    Debug.LogWarning("Button audio settings missing: Resources/Audio/ButtonAudioConfig.asset");
                    warnedMissingSettings = true;
                }
                return;
            }
            var clip = settings.ClickClip;
            float volume = Mathf.Clamp01(settings.Volume);
            if (clip == null || volume <= 0f) return;
            if (instance == null)
            {
                var host = new GameObject("ButtonAudioTail");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<ButtonAudioPlayer>();
                instance.source = host.AddComponent<AudioSource>();
                instance.source.playOnAwake = false;
                instance.source.loop = false;
                instance.source.spatialBlend = 0f;
                instance.source.ignoreListenerPause = true;
                instance.source.volume = AudioVolumeSettings.Button;
            }
            instance.source.PlayOneShot(clip, volume);
            instance.LastPlayedClip = clip;
            instance.PlayedSoundCount++;
            instance.remaining = Mathf.Max(instance.remaining, clip.length + 0.1f);
        }

        private void Update()
        {
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f) Destroy(gameObject);
        }

        private void OnEnable() => AudioVolumeSettings.Changed += ApplyVolume;

        private void ApplyVolume()
        {
            if (source != null) source.volume = AudioVolumeSettings.Button;
        }

        private void OnDestroy()
        {
            AudioVolumeSettings.Changed -= ApplyVolume;
            if (instance == this) instance = null;
        }
    }
}
