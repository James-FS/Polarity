using System;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Polarity.Controllers
{
    [DisallowMultipleComponent, RequireComponent(typeof(SimulationWorld))]
    public sealed class LevelController : MonoBehaviour
    {
        private SimulationWorld world;
        public bool IsRestarting { get; private set; }
        public LevelOutcome? LastOutcome { get; private set; }
        public event Action<LevelOutcome> OutcomeResolved;
        internal void Bind(SimulationWorld owner) => world=owner;
        private void Update() => SampleRestartInput(Input.GetKeyDown(KeyCode.R));
        public void SampleRestartInput(bool pressed) { if(pressed) RequestRestart(); }
        public bool RequestRestart()
        {
            if (world==null || !world.OwnsPhysics || IsRestarting) return false;
            var scene=gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return false;
            IsRestarting=true;
            world.PrepareForRestart();
            SceneManager.LoadSceneAsync(scene.path,LoadSceneMode.Single);
            return true;
        }
        internal void ResolveOutcome()
        {
            if (world==null || world.State.Phase!=LevelPhase.Running || world.PlayerHealth==null) return;
            LevelPhase phase = !world.PlayerHealth.IsAlive ? LevelPhase.Lost :
                world.State.ActiveEnemyCount==0 ? LevelPhase.Won : LevelPhase.Running;
            if (phase==LevelPhase.Running) return;
            LastOutcome=new LevelOutcome(phase,world.CompletedStepCount,world.SimulationTime,world.PlayerHealth.CurrentHealth,
                world.State.ActiveEnemyCount,world.State.RemainingMarks);
            world.SetPhase(phase);
        }
        internal void PublishOutcome() { if (LastOutcome.HasValue) OutcomeResolved?.Invoke(LastOutcome.Value); }
        internal void Clear() { LastOutcome=null; OutcomeResolved=null; }
        private void OnDestroy() { world=null; OutcomeResolved=null; }
    }
}
