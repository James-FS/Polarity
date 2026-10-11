using System.Collections.Generic;
using Polarity.Controllers;
using Polarity.Entities;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Polarity.Views
{
    /// <summary>Reads committed facts. Buttons submit flow intent to LevelController.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class HUDView : MonoBehaviour
    {
        [SerializeField] private SimulationWorld world;
        [SerializeField] private Text healthText, enemiesText, marksText, speedText;
        [SerializeField] private Text markFeedbackText, propagationText, budgetHintText;
        [SerializeField] private GameObject modal;
        [SerializeField] private Text modalTitle, modalDescription;
        [SerializeField] private Button pauseButton, resumeButton, retryButton;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform statusPanel, controlsPanel;
        private Rect originalCameraRect;
        private readonly Vector3[] panelCorners = new Vector3[4];
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private SimulationWorld subscribedWorld;
        private LevelState state;
        private Health health;
        public bool IsBound => state != null;
        public int MarkNotificationCount { get; private set; }
        public int PropagationNotificationCount { get; private set; }
        public bool ModalVisible => modal != null && modal.activeSelf;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (worldCamera != null) originalCameraRect = worldCamera.rect;
            pauseButton.onClick.AddListener(TogglePause);
            resumeButton.onClick.AddListener(TogglePause);
            retryButton.onClick.AddListener(Retry);
            Bind();
        }
        // First enable can precede World.OnEnable; Start runs after room initialization.
        private void Start() { if (!IsBound) Bind(); }
        private void Bind()
        {
            if (IsBound || world == null || world.State == null || world.PlayerHealth == null) return;
            subscribedWorld = world;
            state = world.State;
            health = world.PlayerHealth;
            RefreshCounters();
            RefreshPhase(state.Phase);
            markFeedbackText.text = world.LastMarkResult.HasValue ? Describe(world.LastMarkResult.Value) : "Point at an enemy or crate to mark it.";
            propagationText.text = world.LastPropagation.HasValue ? Describe(world.LastPropagation.Value) : "CONTACT SPREAD  /  Free; existing polarity stays.";
            state.PhaseChanged += RefreshPhase;
            state.MarksChanged += OnCounterChanged;
            state.EnemyCountChanged += OnCounterChanged;
            health.HealthChanged += OnCounterChanged;
            world.MarkResolved += OnMark;
            world.PropagationCommitted += OnPropagation;
            world.TimeScaleChanged += OnSpeed;
        }
        private void OnDisable()
        {
            if (worldCamera != null && Application.isPlaying) worldCamera.rect = originalCameraRect;
            pauseButton.onClick.RemoveListener(TogglePause);
            resumeButton.onClick.RemoveListener(TogglePause);
            retryButton.onClick.RemoveListener(Retry);
            if (state != null)
            {
                state.PhaseChanged -= RefreshPhase;
                state.MarksChanged -= OnCounterChanged;
                state.EnemyCountChanged -= OnCounterChanged;
            }
            if (health != null) health.HealthChanged -= OnCounterChanged;
            if (subscribedWorld != null)
            {
                subscribedWorld.MarkResolved -= OnMark;
                subscribedWorld.PropagationCommitted -= OnPropagation;
                subscribedWorld.TimeScaleChanged -= OnSpeed;
            }
            state = null; health = null; subscribedWorld = null;
        }
        private void LateUpdate()
        {
            if (!Application.isPlaying || worldCamera == null || statusPanel == null || controlsPanel == null || Screen.height == 0) return;
            controlsPanel.GetWorldCorners(panelCorners);
            float bottom = Mathf.Clamp01(panelCorners[1].y / Screen.height);
            statusPanel.GetWorldCorners(panelCorners);
            float top = Mathf.Clamp01(panelCorners[0].y / Screen.height);
            // Reserve HUD space without moving or scaling any physics object.
            if (top > bottom) worldCamera.rect = new Rect(originalCameraRect.x,bottom,originalCameraRect.width,top-bottom);
        }
        private void TogglePause() => world.Level.TogglePause();
        private void Retry() => world.Level.RequestRestart();
        private void OnCounterChanged(int value) => RefreshCounters();
        private void RefreshCounters()
        {
            healthText.text = $"LIFE  {health.CurrentHealth} / {health.MaxHealth}";
            enemiesText.text = $"ENEMIES  {state.ActiveEnemyCount}";
            marksText.text = $"MARKS  {state.RemainingMarks} / {state.MarkBudget}";
            budgetHintText.text = state.RemainingMarks == 0 ? "No marks left. You can still move, use contact spread, or press R to retry." : "First mark and recolor each use 1 mark. Contact spread is free.";
        }
        private void OnMark(MarkResult result)
        {
            MarkNotificationCount++;
            markFeedbackText.text = Describe(result);
            markFeedbackText.color = result.Request.Polarity == Polarity.Model.Polarity.Positive ? new Color(1f, .65f, .25f) : new Color(.35f, .75f, 1f);
            RefreshCounters();
        }
        private void OnPropagation(PropagationResult result)
        {
            PropagationNotificationCount++;
            propagationText.text = Describe(result);
            propagationText.color = result.Polarity == Polarity.Model.Polarity.Positive ? new Color(1f, .65f, .25f) : new Color(.35f, .75f, 1f);
        }
        private void OnSpeed(float value)
        {
            speedText.text = state.Phase == LevelPhase.Paused ? "PAUSED" : state.Phase == LevelPhase.Running && value < .99f ? $"SLOW  {value:0.0}x" : "NORMAL  1.0x";
        }
        private void RefreshPhase(LevelPhase phase)
        {
            bool paused = phase == LevelPhase.Paused;
            bool ended = phase == LevelPhase.Won || phase == LevelPhase.Lost;
            modal.SetActive(paused || ended);
            resumeButton.gameObject.SetActive(paused);
            pauseButton.interactable = phase == LevelPhase.Running;
            modalTitle.text = paused ? "PAUSED" : phase == LevelPhase.Won ? "ROOM CLEARED" : "PLAYER DOWN";
            modalDescription.text = paused ? "WASD Move | Left + orange | Right - blue\nHold Space 0.3x | Esc Resume | R Retry\n\nOnly enemies and crates can be marked.\nShots: negative, no marking or spreading.\n\nCONTACT SPREAD (free)\n+ touches neutral -> both +\n- touches neutral -> both -\nExisting signs stay; source keeps its sign." : phase == LevelPhase.Won ? "All enemies cleared. Dangerous simulation has stopped.\n\nPress R or Retry to play this room again." : "Life depleted. Try a different mark or movement route.\n\nPress R or Retry for a fresh room.";
            OnSpeed(world.CurrentTimeScale);
        }
        // Query this frame's position, independent of EventSystem.Update ordering.
        public bool ContainsScreenPoint(Vector2 point)
        {
            if (!isActiveAndEnabled) return false;
            // Modal covers the screen immediately, even before the next Canvas rebuild.
            if (ModalVisible) return new Rect(0,0,Screen.width,Screen.height).Contains(point);
            if (raycaster == null || EventSystem.current == null) return false;
            uiHits.Clear();
            raycaster.Raycast(new PointerEventData(EventSystem.current) { position = point }, uiHits);
            return uiHits.Count > 0;
        }
        private static string Describe(PropagationResult result)
            => $"CONTACT SPREAD  {result.SourceId} -> {result.TargetId}  {(result.Polarity == Polarity.Model.Polarity.Positive ? "+" : "-")}  /  FREE";
        private static string Describe(MarkResult result)
        {
            string sign = result.Request.Polarity == Polarity.Model.Polarity.Positive ? "+" : "-";
            switch (result.Outcome)
            {
                case MarkOutcome.Applied: return $"MANUAL MARK  {result.Request.TargetId}  {sign}  /  Used 1 mark";
                case MarkOutcome.Unchanged: return "Already this polarity. No mark used.";
                case MarkOutcome.TooFar: return "Out of marking range. No mark used.";
                case MarkOutcome.WallBlocked: return "Wall blocks marking. No mark used.";
                case MarkOutcome.BudgetEmpty: return "No marks remaining. You can still move or retry.";
                case MarkOutcome.InvalidTarget: return "Point at an enemy or crate. No mark used.";
                case MarkOutcome.NotMarkable: return "Only enemies and crates can be marked. No mark used.";
                case MarkOutcome.NotRunning: return "Simulation is stopped. No mark used.";
                default: return "Mark unavailable. No mark used.";
            }
        }
    }
}
