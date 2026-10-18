using System;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Inspector-configurable feedback with an optional real-time playback cooldown.</summary>
    [Serializable]
    public sealed class AudioCue
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField, Min(0f)] private float cooldown;
        [NonSerialized] private float nextAllowedTime;

        public AudioClip Clip => clip;
        public float Volume => volume;

        public AudioCue(float cooldown = 0f) { this.cooldown = cooldown; }

        public bool Play(AudioSource source)
        {
            if (source == null || clip == null || volume <= 0f || Time.unscaledTime < nextAllowedTime)
                return false;
            source.PlayOneShot(clip, volume);
            nextAllowedTime = Time.unscaledTime + Mathf.Max(0f, cooldown);
            return true;
        }

        public void ResetCooldown() => nextAllowedTime = 0f;

#if UNITY_EDITOR
        public void AssignDefaultClip()
        {
            if (clip == null)
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Polarity/Audio/SFX/Mark.wav");
        }
#endif
    }
}
