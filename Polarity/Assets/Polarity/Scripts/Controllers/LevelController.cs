using System;
using Polarity.Model;
using Polarity.Config;
using Polarity.Entities;
using Polarity.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Polarity.Controllers
{
    [DisallowMultipleComponent, RequireComponent(typeof(SimulationWorld)), DefaultExecutionOrder(-100)]
    public sealed class LevelController : MonoBehaviour
    {
        [SerializeField] private LevelDefinition[] levelSequence = Array.Empty<LevelDefinition>();
        [SerializeField] private LevelRoom previewRoom;
        private static int selectedIndex;
        private static string selectedScene;
        private SimulationWorld world;
        public LevelDefinition CurrentDefinition { get; private set; }
        public LevelRoom CurrentRoom { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public int LevelCount => levelSequence.Length;
        public bool HasNextLevel => CurrentDefinition != null && CurrentLevelIndex + 1 < LevelCount;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSelection() { selectedIndex=0; selectedScene=null; }
        private void Awake()
        {
            world=GetComponent<SimulationWorld>();
            if (levelSequence.Length==0) return; // Existing single-room fixtures remain usable.
            string scenePath=gameObject.scene.path;
            CurrentLevelIndex=selectedScene==scenePath ? Mathf.Clamp(selectedIndex,0,LevelCount-1) : 0;
            CurrentDefinition=levelSequence[CurrentLevelIndex];
            if (CurrentDefinition==null || !CurrentDefinition.IsValid || !CurrentDefinition.RoomPrefab.HasValidBindings())
            { Debug.LogError("Level requires a configured room, gameplay settings and explicit valid bindings.",this); world.enabled=false; return; }
            if (previewRoom!=null) { previewRoom.gameObject.SetActive(false); Destroy(previewRoom.gameObject); }
            CurrentRoom=Instantiate(CurrentDefinition.RoomPrefab);
            CurrentRoom.name="LevelRoot";
            world.ConfigureRoom(CurrentRoom,CurrentDefinition);
            GetComponent<MarkingController>()?.ConfigureRoom(CurrentRoom);
        }
        private bool slowInputArmed;
        public bool IsRestarting { get; private set; }
        public LevelOutcome? LastOutcome { get; private set; }
        public event Action<LevelOutcome> OutcomeResolved;
        internal void Bind(SimulationWorld owner) => world=owner;
        private void Update()
        {
            SampleControlInput(Input.GetKey(KeyCode.Space),Input.GetKeyDown(KeyCode.Escape),Input.GetKeyDown(KeyCode.R));
            if (Input.GetKeyDown(KeyCode.Return)) RequestNextLevel();
        }
        public void SampleControlInput(bool slowHeld, bool pausePressed, bool restartPressed)
        {
            if (world == null || IsRestarting) return;
            if (restartPressed) { RequestRestart(); return; }
            if (!slowHeld) slowInputArmed = true;
            world.SetSlowMotion(slowInputArmed && slowHeld);
            if (pausePressed) TogglePause();
        }
        public bool TogglePause()
        {
            if (world == null || IsRestarting || !world.OwnsPhysics) return false;
            if (world.State.Phase == LevelPhase.Running) world.SetPhase(LevelPhase.Paused);
            else if (world.State.Phase == LevelPhase.Paused) world.SetPhase(LevelPhase.Running);
            else return false;
            return true;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) { slowInputArmed=false; world?.SetSlowMotion(false); } }
        public void SampleRestartInput(bool pressed) { if(pressed) RequestRestart(); }
        public bool RequestRestart() => RequestLoad(CurrentLevelIndex);
        public bool RequestNextLevel()
        {
            if (world==null || world.State?.Phase!=LevelPhase.Won || !HasNextLevel) return false;
            return RequestLoad(CurrentLevelIndex+1);
        }
        internal bool RequestGMNextLevel()
            => Polarity.Debugging.GMManager.IsAvailable && HasNextLevel && RequestLoad(CurrentLevelIndex + 1);
        private bool RequestLoad(int index)
        {
            if (world==null || !world.OwnsPhysics || IsRestarting) return false;
            var scene=gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return false;
            selectedIndex=index; selectedScene=scene.path;
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
