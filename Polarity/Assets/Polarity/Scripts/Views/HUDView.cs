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
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject pauseCard, resultCard;
        [SerializeField] private GameObject failureCard;
        [SerializeField] private Text failureLevelText, failureTimeText, failureEnemiesText, failureMarksText;
        [SerializeField] private Button failureRetryButton;
        [SerializeField] private Text pauseSummary;
        [SerializeField] private Button pauseRetryButton, pauseCloseButton;
        [SerializeField] private Text levelText, teachingText;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform statusPanel, controlsPanel;
        [SerializeField] private Polarity.Debugging.GMView gmView;
        private Rect originalCameraRect;
        private RectTransform sideLeftPanel, sideRightPanel;
        private readonly Vector3[] panelCorners = new Vector3[4];
        private readonly Image[] healthCells = new Image[3];
        private Image markIcon;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private SimulationWorld subscribedWorld;
        private LevelState state;
        private Health health;
        public bool IsBound => state != null;
        public SimulationWorld World => world;
        public int MarkNotificationCount { get; private set; }
        public int PropagationNotificationCount { get; private set; }
        public bool ModalVisible => modal != null && modal.activeSelf;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (worldCamera != null) originalCameraRect = worldCamera.rect;
            sideLeftPanel = transform.Find("Frame/SideLeft") as RectTransform;
            sideRightPanel = transform.Find("Frame/SideRight") as RectTransform;
            if (statusPanel != null)
            {
                healthCells[0] = FindStatusImage("LeftStatus/HealthCells/HealthIcon1", "LeftStatus/HealthIcon", "HealthIcon");
                healthCells[1] = FindStatusImage("LeftStatus/HealthCells/HealthIcon2", "LeftStatus/HealthIcon (1)", "HealthIcon (1)");
                healthCells[2] = FindStatusImage("LeftStatus/HealthCells/HealthIcon3", "LeftStatus/HealthIcon (2)", "HealthIcon (2)");
                markIcon = FindStatusImage("RightStatus/MarkIcon", "MarkIcon");
            }
            pauseButton.onClick.AddListener(TogglePause);
            resumeButton.onClick.AddListener(TogglePause);
            retryButton.onClick.AddListener(Retry);
            if (failureRetryButton != null) failureRetryButton.onClick.AddListener(Retry);
            if (pauseRetryButton != null) pauseRetryButton.onClick.AddListener(Retry);
            if (pauseCloseButton != null) pauseCloseButton.onClick.AddListener(TogglePause);
            if (nextButton!=null) nextButton.onClick.AddListener(Next);
            Bind();
        }
        private Image FindStatusImage(params string[] paths)
        {
            foreach (string path in paths)
            {
                Transform item = statusPanel.Find(path);
                if (item != null) return item.GetComponent<Image>();
            }
            return null;
        }
        // First enable can precede World.OnEnable; Start runs after room initialization.
        private void Start() { if (!IsBound) Bind(); }
        private void Bind()
        {
            if (IsBound || world == null || world.State == null || world.PlayerHealth == null) return;
            subscribedWorld = world;
            state = world.State;
            health = world.PlayerHealth;
            if (levelText!=null) levelText.text=world.Level.CurrentDefinition!=null ? $"第 {world.Level.CurrentLevelIndex+1} / {world.Level.LevelCount} 关  |  {world.Level.CurrentDefinition.Title}" : "极性失控";
            if (teachingText!=null) teachingText.text=world.Level.CurrentDefinition?.TeachingHint ?? "";
            RefreshCounters();
            RefreshPhase(state.Phase);
            markFeedbackText.text = world.LastMarkResult.HasValue ? Describe(world.LastMarkResult.Value) : "指向敌人或箱子进行标记。";
            propagationText.text = world.LastPropagation.HasValue ? Describe(world.LastPropagation.Value) : "接触传播  /  免费；已有极性不被覆盖。";
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
            if (failureRetryButton != null) failureRetryButton.onClick.RemoveListener(Retry);
            if (pauseRetryButton != null) pauseRetryButton.onClick.RemoveListener(Retry);
            if (pauseCloseButton != null) pauseCloseButton.onClick.RemoveListener(TogglePause);
            if (nextButton!=null) nextButton.onClick.RemoveListener(Next);
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
            if (!Application.isPlaying || worldCamera == null || statusPanel == null || controlsPanel == null || Screen.width == 0 || Screen.height == 0) return;
            controlsPanel.GetWorldCorners(panelCorners);
            float bottom = Mathf.Clamp01(panelCorners[1].y / Screen.height);
            statusPanel.GetWorldCorners(panelCorners);
            float top = Mathf.Clamp01(panelCorners[0].y / Screen.height);
            float left = originalCameraRect.x, right = originalCameraRect.xMax;
            if (sideLeftPanel != null && sideRightPanel != null)
            {
                sideLeftPanel.GetWorldCorners(panelCorners);
                left = Mathf.Clamp01(panelCorners[2].x / Screen.width);
                sideRightPanel.GetWorldCorners(panelCorners);
                right = Mathf.Clamp01(panelCorners[0].x / Screen.width);
            }
            // Keep the world inside the HUD frame without changing physics coordinates.
            if (top > bottom && right > left) worldCamera.rect = new Rect(left, bottom, right-left, top-bottom);
        }
        private void TogglePause() => world.Level.TogglePause();
        private void Retry() => world.Level.RequestRestart();
        private void Next() => world.Level.RequestNextLevel();
        private void OnCounterChanged(int value) => RefreshCounters();
        private void RefreshCounters()
        {
            healthText.text = $"生命  {health.CurrentHealth}/{health.MaxHealth}";
            enemiesText.text = $"剩余敌人  {state.ActiveEnemyCount}";
            marksText.text = state.UnlimitedMarks ? "标记  ∞" : $"标记  {state.RemainingMarks}/{state.MarkBudget}";
            for (int i = 0; i < healthCells.Length; i++)
            {
                if (healthCells[i] != null)
                    healthCells[i].color = i < health.CurrentHealth ? Color.white : new Color(.30f, .42f, .48f, .55f);
            }
            if (markIcon != null)
                markIcon.color = state.UnlimitedMarks || state.RemainingMarks > 0
                    ? Color.white : new Color(.42f, .36f, .30f, .60f);
            budgetHintText.text = state.UnlimitedMarks ? "GM 无限标记已开启；范围、墙体与目标权限规则仍有效。" : state.RemainingMarks == 0 ? "标记次数已用尽。仍可移动、利用接触传播，或按 R 重试。" : "首次赋极与改色均消耗 1 次标记；接触传播免费。";
        }
        private void OnMark(MarkResult result)
        {
            MarkNotificationCount++;
            markFeedbackText.text = state.UnlimitedMarks && result.Outcome == MarkOutcome.Applied ? $"主动标记  {result.Request.TargetId}  {(result.Request.Polarity == Polarity.Model.Polarity.Positive ? "+" : "-")}  /  GM 不消耗次数" : Describe(result);
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
            speedText.text = state.Phase == LevelPhase.Ready ? "准备中" : state.Phase == LevelPhase.Paused ? "已暂停" : state.Phase == LevelPhase.Running && value < .99f ? $"慢速  {value:0.0} 倍" : "正常  1.0 倍";
        }
        private void RefreshPhase(LevelPhase phase)
        {
            bool paused = phase == LevelPhase.Paused;
            bool ended = phase == LevelPhase.Won || phase == LevelPhase.Lost;
            modal.SetActive(paused || ended);
            if (pauseCard != null) pauseCard.SetActive(paused);
            if (resultCard != null) resultCard.SetActive(ended && (phase != LevelPhase.Lost || failureCard == null));
            if (failureCard != null) failureCard.SetActive(phase == LevelPhase.Lost);
            if (phase == LevelPhase.Lost && failureCard != null)
            {
                if (failureLevelText != null) failureLevelText.text = $"第 {world.Level.CurrentLevelIndex + 1} 关  ·  {world.Level.CurrentDefinition?.Title}";
                if (failureTimeText != null) failureTimeText.text = $"{world.SimulationTime:0.0} 秒";
                if (failureEnemiesText != null) failureEnemiesText.text = $"{state.ActiveEnemyCount}";
                if (failureMarksText != null) failureMarksText.text = state.UnlimitedMarks ? "∞" : $"{state.MarkBudget - state.RemainingMarks} / {state.MarkBudget}";
            }
            if (paused && pauseSummary != null)
                pauseSummary.text = $"第 {world.Level.CurrentLevelIndex + 1} 关  /  {world.Level.CurrentDefinition.Title}\n游戏已暂停，准备好后继续。";
            resumeButton.gameObject.SetActive(paused);
            if (nextButton!=null)
            {
                nextButton.gameObject.SetActive(phase==LevelPhase.Won && world.Level.HasNextLevel);
                nextButton.GetComponentInChildren<Text>().text = "通往下一关";
                var retryRect = (RectTransform)retryButton.transform;
                retryRect.anchoredPosition = new Vector2(phase == LevelPhase.Won && world.Level.HasNextLevel ? 180 : 0, -156);
                if (world.Level.HasNextLevel)
                    ((RectTransform)nextButton.transform).anchoredPosition = new Vector2(-180, -156);
            }
            pauseButton.interactable = phase == LevelPhase.Running;
            modalTitle.text = paused ? "已暂停" : phase == LevelPhase.Won ? (world.Level.HasNextLevel ? "本关完成" : "全部关卡完成") : "挑战失败";
            modalDescription.text = paused ? "WASD 移动 | 左键 + 橙 | 右键 - 蓝\n空格慢速 0.3 倍 | Esc 继续 | R 重试" : phase == LevelPhase.Won
                ? $"用时 {world.SimulationTime:0.0} 秒  |  受伤 {world.PlayerDamageCount} 次\n标记使用 {state.MarkBudget - state.RemainingMarks}/{state.MarkBudget} 次\n" + (world.Level.HasNextLevel ? "本关完成，准备进入下一关。" : "全部六关完成！")
                : "生命已耗尽。试试不同的标记或走位。\n\n按 R 或点击“重试”，重新挑战本关。";
            OnSpeed(world.CurrentTimeScale);
        }
        // Query this frame's position, independent of EventSystem.Update ordering.
        public bool ContainsScreenPoint(Vector2 point)
        {
            if (!isActiveAndEnabled) return false;
            // Modal covers the screen immediately, even before the next Canvas rebuild.
            if (world != null && world.Level != null && world.Level.IsIntroductionOpen) return true;
            if (gmView != null && gmView.BlocksPointer) return true;
            if (ModalVisible) return new Rect(0,0,Screen.width,Screen.height).Contains(point);
            if (raycaster == null || EventSystem.current == null) return false;
            uiHits.Clear();
            raycaster.Raycast(new PointerEventData(EventSystem.current) { position = point }, uiHits);
            return uiHits.Count > 0;
        }
        private static string Describe(PropagationResult result)
            => $"接触传播  {result.SourceId} -> {result.TargetId}  {(result.Polarity == Polarity.Model.Polarity.Positive ? "+" : "-")}  /  免费";
        private static string Describe(MarkResult result)
        {
            string sign = result.Request.Polarity == Polarity.Model.Polarity.Positive ? "+" : "-";
            switch (result.Outcome)
            {
                case MarkOutcome.Applied: return $"主动标记  {result.Request.TargetId}  {sign}  /  消耗 1 次";
                case MarkOutcome.Unchanged: return "已是该极性，不消耗次数。";
                case MarkOutcome.TooFar: return "目标超出标记范围，不消耗次数。";
                case MarkOutcome.WallBlocked: return "墙体阻挡标记，不消耗次数。";
                case MarkOutcome.BudgetEmpty: return "标记次数已用尽，仍可移动或重试。";
                case MarkOutcome.InvalidTarget: return "请指向敌人或箱子，不消耗次数。";
                case MarkOutcome.NotMarkable: return "只能标记敌人与箱子，不消耗次数。";
                case MarkOutcome.NotRunning: return "模拟已停止，不消耗次数。";
                default: return "暂时无法标记，不消耗次数。";
            }
        }
    }
}
