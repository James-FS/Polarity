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
        private static bool introductionDismissed;
        public bool IsIntroductionOpen { get; private set; }
        public bool IsFirstLevel => CurrentDefinition != null && CurrentLevelIndex == 0;
        private SimulationWorld world;
        public LevelDefinition CurrentDefinition { get; private set; }
        public LevelRoom CurrentRoom { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public int LevelCount => levelSequence.Length;
        public string GetLevelTitle(int index) => index >= 0 && index < LevelCount && levelSequence[index] != null
            ? levelSequence[index].Title : string.Empty;
        public bool HasNextLevel => CurrentDefinition != null && CurrentLevelIndex + 1 < LevelCount;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSelection() { selectedIndex=0; selectedScene=null; introductionDismissed=false; }
        public static void ResetForNewGame() => ResetSelection();
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
        public bool IsPauseSettingsOpen { get; private set; }
        public bool IsResultDismissed { get; private set; }
        public bool IsEndMenuOpen { get; private set; }
        public event Action OutcomeMenuChanged;
        public event Action<bool> PauseSettingsVisibilityChanged;
        public LevelOutcome? LastOutcome { get; private set; }
        public event Action<LevelOutcome> OutcomeResolved;
        internal void Bind(SimulationWorld owner) => world=owner;
        private void Update()
        {
            SampleControlInput(Input.GetKey(KeyCode.Space),Input.GetKeyDown(KeyCode.Escape),Input.GetKeyDown(KeyCode.R));
            if (Input.GetKeyDown(KeyCode.Return))
            { if (IsIntroductionOpen) RequestBeginLevel(); else RequestNextLevel(); }
        }
        public void SampleControlInput(bool slowHeld, bool pausePressed, bool restartPressed)
        {
            if (world == null || IsRestarting) return;
            if (restartPressed) { RequestRestart(); return; }
            if (!slowHeld) slowInputArmed = true;
            world.SetSlowMotion(slowInputArmed && slowHeld);
            if (pausePressed) { if (IsIntroductionOpen) RequestBeginLevel(); else TogglePause(); }
        }
        internal bool BeginInitialIntroduction()
        {
            IsIntroductionOpen = IsFirstLevel && !introductionDismissed;
            return IsIntroductionOpen;
        }
        public bool RequestShowIntroduction()
        {
            if (!IsFirstLevel || world == null || IsRestarting || !world.OwnsPhysics ||
                (world.State.Phase != LevelPhase.Running && world.State.Phase != LevelPhase.Paused)) return false;
            IsIntroductionOpen = true;
            world.SetSlowMotion(false);
            world.SetPhase(LevelPhase.Ready);
            return true;
        }
        public bool RequestBeginLevel()
        {
            if (!IsIntroductionOpen || world == null || IsRestarting || !world.OwnsPhysics || world.State.Phase != LevelPhase.Ready) return false;
            introductionDismissed = true;
            IsIntroductionOpen = false;
            world.Player.ClearIntent();
            world.SetSlowMotion(false);
            world.SetPhase(LevelPhase.Running);
            return true;
        }
        public bool TogglePause()
        {
            if (world == null || IsRestarting || !world.OwnsPhysics) return false;
            // Esc dismisses the settings layer before resuming the paused world.
            if (IsPauseSettingsOpen) return SetPauseSettingsVisible(false);
            if (world.State.Phase == LevelPhase.Won || world.State.Phase == LevelPhase.Lost)
            {
                IsResultDismissed = true;
                IsEndMenuOpen = !IsEndMenuOpen;
                OutcomeMenuChanged?.Invoke();
                return true;
            }
            if (world.State.Phase == LevelPhase.Running) world.SetPhase(LevelPhase.Paused);
            else if (world.State.Phase == LevelPhase.Paused) world.SetPhase(LevelPhase.Running);
            else return false;
            return true;
        }
        public bool SetPauseSettingsVisible(bool visible)
        {
            if (visible && (world == null || IsRestarting || !world.OwnsPhysics ||
                (world.State.Phase != LevelPhase.Paused && !IsEndMenuOpen))) return false;
            if (IsPauseSettingsOpen == visible) return false;
            IsPauseSettingsOpen = visible;
            PauseSettingsVisibilityChanged?.Invoke(visible);
            return true;
        }
        public void DismissOutcome()
        {
            if (world == null || IsRestarting ||
                (world.State.Phase != LevelPhase.Won && world.State.Phase != LevelPhase.Lost)) return;
            IsResultDismissed = true;
            IsEndMenuOpen = false;
            SetPauseSettingsVisible(false);
            OutcomeMenuChanged?.Invoke();
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
        internal bool RequestGMLevel(int index)
            => Polarity.Debugging.GMManager.IsAvailable && index >= 0 && index < LevelCount && RequestLoad(index);
        public bool RequestMainMenu()
        {
            const string menuPath = "Assets/Polarity/Scenes/MainMenu.scene";
            if (world == null || !world.OwnsPhysics || IsRestarting || !Application.CanStreamedLevelBeLoaded(menuPath))
                return false;
            IsRestarting = true;
            world.PrepareForRestart();
            SceneManager.LoadSceneAsync(menuPath, LoadSceneMode.Single);
            return true;
        }
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
        internal void Clear() { SetPauseSettingsVisible(false); LastOutcome=null; OutcomeResolved=null; }
        private void OnDestroy() { world=null; OutcomeResolved=null; PauseSettingsVisibilityChanged=null; OutcomeMenuChanged=null; }
    }
}
