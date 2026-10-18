using Polarity.Model;
using Polarity.Config;
using Polarity.Simulation;
using UnityEngine;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Views
{
    /// <summary>Plays feedback for committed player marks, never for contact propagation.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class MarkingAudioView : MonoBehaviour
    {
        [SerializeField] private SimulationWorld world;
        [SerializeField] private AudioClip positiveClip;
        [SerializeField] private AudioClip negativeClip;
        [SerializeField, Range(0f, 1f)] private float positiveVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float negativeVolume = 0.7f;
        private AudioSource source;
        private SimulationWorld subscribedWorld;
        private bool paused;

        public int PlayedSoundCount { get; private set; }
        public Pole LastPlayedPolarity { get; private set; }

        private void OnEnable()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            ApplyVolume();
            AudioVolumeSettings.Changed += ApplyVolume;
            paused = false;
            PlayedSoundCount = 0;
            LastPlayedPolarity = Pole.Neutral;
            subscribedWorld = world;
            if (subscribedWorld == null) return;
            subscribedWorld.MarkResolved += OnMarkResolved;
            subscribedWorld.TimeScaleChanged += OnTimeScaleChanged;
            OnTimeScaleChanged(subscribedWorld.CurrentTimeScale);
        }

        private void Start()
        {
            // World initialization can follow this component's OnEnable.
            if (subscribedWorld != null) OnTimeScaleChanged(subscribedWorld.CurrentTimeScale);
        }

        private void OnMarkResolved(MarkResult result)
        {
            if (result.Outcome != MarkOutcome.Applied || source == null) return;
            bool positive = result.Request.Polarity == Pole.Positive;
            AudioClip clip = positive ? positiveClip : negativeClip;
            if (clip == null) return;
            source.PlayOneShot(clip, positive ? positiveVolume : negativeVolume);
            PlayedSoundCount++;
            LastPlayedPolarity = result.Request.Polarity;
        }

        private void OnTimeScaleChanged(float scale)
        {
            bool shouldPause = scale <= 0f;
            if (source == null || paused == shouldPause) return;
            paused = shouldPause;
            if (paused) source.Pause();
            else source.UnPause();
        }

        private void LateUpdate()
        {
            if (subscribedWorld != null && subscribedWorld.Level != null && subscribedWorld.Level.IsRestarting)
                source.Stop();
        }

        private void OnDisable()
        {
            AudioVolumeSettings.Changed -= ApplyVolume;
            if (subscribedWorld != null)
            {
                subscribedWorld.MarkResolved -= OnMarkResolved;
                subscribedWorld.TimeScaleChanged -= OnTimeScaleChanged;
            }
            subscribedWorld = null;
            if (source != null) source.Stop();
            paused = false;
        }

        private void ApplyVolume() => source.volume = AudioVolumeSettings.Gameplay;

#if UNITY_EDITOR
        private void Reset()
        {
            world = GetComponent<SimulationWorld>();
            AssignDefaults();
            var audio = GetComponent<AudioSource>();
            if (audio != null) { audio.playOnAwake = false; audio.loop = false; audio.spatialBlend = 0f; }
        }

        private void OnValidate() => AssignDefaults();

        private void AssignDefaults()
        {
            var fallback = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Polarity/Audio/SFX/Mark.wav");
            if (positiveClip == null) positiveClip = fallback;
            if (negativeClip == null) negativeClip = fallback;
        }
#endif
    }
}
