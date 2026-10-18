using System.Collections.Generic;
using Polarity.Config;
using Polarity.Controllers;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Scene-local audio for committed gameplay facts; never changes simulation state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class GameplayAudioView : MonoBehaviour
    {
        [SerializeField, Tooltip("Usually the SimulationWorld on this object. Can also be assigned explicitly.")]
        private SimulationWorld world;
        [Header("One-shot feedback")]
        [SerializeField] private AudioCue enemyHurt = new AudioCue(0.06f);
        [SerializeField] private AudioCue propagation = new AudioCue(0.08f);
        [SerializeField] private AudioCue victory = new AudioCue();
        [SerializeField] private AudioCue defeat = new AudioCue();
        [SerializeField] private AudioCue spikeDamage = new AudioCue(0.08f);
        [SerializeField] private AudioCue playerHurt = new AudioCue();
        [SerializeField] private AudioCue impact = new AudioCue(0.08f);

        private readonly HashSet<int> enemyIds = new HashSet<int>();
        private AudioSource source;
        private SimulationWorld subscribedWorld;
        private LevelController subscribedLevel;
        private bool paused, outcomePlayed;

        public int PlayedSoundCount { get; private set; }

        private void OnEnable()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            ApplyVolume();
            AudioVolumeSettings.Changed += ApplyVolume;
            paused = outcomePlayed = false;
            PlayedSoundCount = 0;
            foreach (var cue in Cues()) cue.ResetCooldown();
            TrySubscribe();
        }

        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            if (world == null) world = GetComponent<SimulationWorld>();
            if (world == null || world.State == null || subscribedWorld != null) return;
            subscribedWorld = world;
            subscribedLevel = world.Level;
            // Cache IDs before damage can unregister and destroy a killed enemy.
            enemyIds.Clear();
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            foreach (var body in world.Bodies)
                if (body != null && body.gameObject.layer == enemyLayer) enemyIds.Add(body.BodyId);
            world.PropagationCommitted += OnPropagation;
            world.DamageCommitted += OnDamage;
            world.TimeScaleChanged += OnTimeScaleChanged;
            if (subscribedLevel != null) subscribedLevel.OutcomeResolved += OnOutcome;
            OnTimeScaleChanged(world.CurrentTimeScale);
        }

        private void Play(AudioCue cue)
        {
            if (cue.Play(source)) PlayedSoundCount++;
        }

        private void OnPropagation(PropagationResult result) => Play(propagation);

        private void OnDamage(DamageResult result)
        {
            if (result.Amount <= 0) return;
            AudioCue victim = subscribedWorld.PlayerHealth != null &&
                result.TargetId == subscribedWorld.PlayerHealth.ActorId ? playerHurt :
                enemyIds.Contains(result.TargetId) ? enemyHurt : null;
            AudioCue cause = result.Kind == DamageKind.Spike ? spikeDamage :
                result.Kind == DamageKind.Impact ? impact : null;
            if (victim != null) Play(victim);
            // Different cause/character sounds may layer. A shared placeholder plays only once.
            if (cause != null && (victim == null || victim.Clip == null ||
                victim.Volume <= 0f || cause.Clip != victim.Clip)) Play(cause);
        }

        private void OnOutcome(LevelOutcome result)
        {
            if (outcomePlayed || (result.Phase != LevelPhase.Won && result.Phase != LevelPhase.Lost)) return;
            outcomePlayed = true;
            source.Stop();
            paused = false;
            source.UnPause();
            Play(result.Phase == LevelPhase.Won ? victory : defeat);
        }

        private void OnTimeScaleChanged(float scale)
        {
            bool shouldPause = scale <= 0f;
            if (paused == shouldPause) return;
            paused = shouldPause;
            if (paused) source.Pause();
            else source.UnPause();
        }

        private void LateUpdate()
        {
            if (subscribedLevel != null && subscribedLevel.IsRestarting) source.Stop();
        }

        private void OnDisable()
        {
            AudioVolumeSettings.Changed -= ApplyVolume;
            if (subscribedWorld != null)
            {
                subscribedWorld.PropagationCommitted -= OnPropagation;
                subscribedWorld.DamageCommitted -= OnDamage;
                subscribedWorld.TimeScaleChanged -= OnTimeScaleChanged;
            }
            if (subscribedLevel != null) subscribedLevel.OutcomeResolved -= OnOutcome;
            subscribedWorld = null;
            subscribedLevel = null;
            enemyIds.Clear();
            if (source != null) source.Stop();
            paused = false;
        }

        private void ApplyVolume() => source.volume = AudioVolumeSettings.Gameplay;

        private IEnumerable<AudioCue> Cues()
        {
            yield return enemyHurt; yield return propagation; yield return victory; yield return defeat;
            yield return spikeDamage; yield return playerHurt; yield return impact;
        }

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
            foreach (var cue in Cues()) cue.AssignDefaultClip();
        }
#endif
    }
}
