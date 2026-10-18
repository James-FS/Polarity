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
        [SerializeField] private Text healthText, enemiesText, marksText;
        [SerializeField] private Text markFeedbackText, propagationText, budgetHintText;
        [SerializeField] private GameObject modal;
        private Text markFailureNotice;
        private float markFailureUntil;
        [SerializeField] private Text modalTitle, modalDescription;
        [SerializeField] private Button pauseButton, resumeButton, retryButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button resultCloseButton, failureCloseButton;
        [SerializeField] private GameObject pauseCard, resultCard;
        [SerializeField] private GameObject failureCard;
        [SerializeField] private Text failureLevelText, failureTimeText, failureEnemiesText, failureMarksText;
        [SerializeField] private Button failureRetryButton;
        [SerializeField] private Text pauseSummary;
        [SerializeField] private Button pauseRetryButton, pauseCloseButton;
        [SerializeField] private Button pauseHomeButton;
        [SerializeField] private Button pauseSettingsButton, settingsBackButton;
        [SerializeField] private GameObject pauseSettingsPanel;
        [SerializeField] private Text levelText, teachingText;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform statusPanel, controlsPanel;
        [SerializeField] private Polarity.Debugging.GMView gmView;
        private Rect originalCameraRect;
        private RectTransform sideLeftPanel, sideRightPanel;
        private readonly Vector3[] panelCorners = new Vector3[4];
        private readonly Image[] healthCells = new Image[3];
        private readonly Image[] healthFills = new Image[3];
        private readonly Image[] healthHighlights = new Image[3];
        private readonly float[] healthFlashUntil = new float[3];
        private int displayedHealth = -1;
        private float healthPulseTime;
        private Image markIcon;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private SimulationWorld subscribedWorld;
        private LevelState state;
        private Health health;
        public bool IsBound => state != null;
        public SimulationWorld World => world;
        public int MarkNotificationCount { get; private set; }
        public int PropagationNotificationCount { get; private set; }
        public bool ModalVisible => (modal != null && modal.activeSelf) ||
            (pauseSettingsPanel != null && pauseSettingsPanel.activeSelf);

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
                for (int i = 0; i < healthCells.Length; i++)
                {
                    if (healthCells[i] == null) continue;
                    healthFills[i] = healthCells[i].transform.Find("EnergyFill")?.GetComponent<Image>();
                    healthHighlights[i] = healthCells[i].transform.Find("EnergyFill/Highlight")?.GetComponent<Image>();
                }
                markIcon = FindStatusImage("RightStatus/MarkIcon", "MarkIcon");
            }
            pauseButton.onClick.AddListener(TogglePause);
            resumeButton.onClick.AddListener(ContinueFromMenu);
            retryButton.onClick.AddListener(Retry);
            if (failureRetryButton != null) failureRetryButton.onClick.AddListener(Retry);
            if (pauseRetryButton != null) pauseRetryButton.onClick.AddListener(Retry);
            if (pauseCloseButton != null) pauseCloseButton.onClick.AddListener(TogglePause);
            if (pauseHomeButton != null) pauseHomeButton.onClick.AddListener(ReturnHome);
            if (pauseSettingsButton != null) pauseSettingsButton.onClick.AddListener(OpenSettings);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);
            if (nextButton!=null) nextButton.onClick.AddListener(Next);
            if (resultCloseButton != null) resultCloseButton.onClick.AddListener(CloseOutcome);
            if (failureCloseButton != null) failureCloseButton.onClick.AddListener(CloseOutcome);
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
            if (levelText!=null) levelText.text=world.Level.CurrentDefinition!=null ? $"第 {world.Level.CurrentLevelIndex+1} 关  |  {world.Level.CurrentDefinition.Title}" : "极性失控";
            if (teachingText!=null) teachingText.text=world.Level.CurrentDefinition?.TeachingHint ?? "";
            RefreshCounters();
            RefreshPhase(state.Phase);
            markFeedbackText.text = world.LastMarkResult.HasValue ? Describe(world.LastMarkResult.Value) : "指向敌人或箱子进行标记。";
            propagationText.text = world.LastPropagation.HasValue ? Describe(world.LastPropagation.Value) : "接触传播  /  免费；已有极性不被覆盖。";
            state.PhaseChanged += RefreshPhase;
            state.MarksChanged += OnCounterChanged;
            state.EnemyCountChanged += OnCounterChanged;
            displayedHealth = health.CurrentHealth;
            health.HealthChanged += OnHealthChanged;
            world.MarkResolved += OnMark;
            world.PropagationCommitted += OnPropagation;
            world.Level.PauseSettingsVisibilityChanged += OnPauseSettingsChanged;
            world.Level.OutcomeMenuChanged += OnOutcomeMenuChanged;
        }
        private void OnDisable()
        {
            if (markFailureNotice != null) markFailureNotice.gameObject.SetActive(false);
            if (worldCamera != null && Application.isPlaying) worldCamera.rect = originalCameraRect;
            pauseButton.onClick.RemoveListener(TogglePause);
            resumeButton.onClick.RemoveListener(ContinueFromMenu);
            retryButton.onClick.RemoveListener(Retry);
            if (failureRetryButton != null) failureRetryButton.onClick.RemoveListener(Retry);
            if (pauseRetryButton != null) pauseRetryButton.onClick.RemoveListener(Retry);
            if (pauseCloseButton != null) pauseCloseButton.onClick.RemoveListener(TogglePause);
            if (pauseHomeButton != null) pauseHomeButton.onClick.RemoveListener(ReturnHome);
            if (pauseSettingsButton != null) pauseSettingsButton.onClick.RemoveListener(OpenSettings);
            if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(CloseSettings);
            if (nextButton!=null) nextButton.onClick.RemoveListener(Next);
            if (resultCloseButton != null) resultCloseButton.onClick.RemoveListener(CloseOutcome);
            if (failureCloseButton != null) failureCloseButton.onClick.RemoveListener(CloseOutcome);
            if (state != null)
            {
                state.PhaseChanged -= RefreshPhase;
                state.MarksChanged -= OnCounterChanged;
                state.EnemyCountChanged -= OnCounterChanged;
            }
            if (health != null) health.HealthChanged -= OnHealthChanged;
            if (subscribedWorld != null)
            {
                subscribedWorld.MarkResolved -= OnMark;
                subscribedWorld.PropagationCommitted -= OnPropagation;
                if (subscribedWorld.Level != null)
                {
                    subscribedWorld.Level.PauseSettingsVisibilityChanged -= OnPauseSettingsChanged;
                    subscribedWorld.Level.OutcomeMenuChanged -= OnOutcomeMenuChanged;
                    subscribedWorld.Level.SetPauseSettingsVisible(false);
                }
            }
            if (pauseSettingsPanel != null) pauseSettingsPanel.SetActive(false);
            state = null; health = null; subscribedWorld = null;
            displayedHealth = -1;
            healthPulseTime = 0;
            System.Array.Clear(healthFlashUntil, 0, healthFlashUntil.Length);
        }
        private void LateUpdate()
        {
            if (Application.isPlaying && health != null)
            {
                if (state.Phase == LevelPhase.Running) healthPulseTime += Time.unscaledDeltaTime;
                RefreshHealthCells();
            }
            if (markFailureNotice != null && markFailureNotice.gameObject.activeSelf &&
                (Time.unscaledTime >= markFailureUntil || state == null || state.Phase != LevelPhase.Running))
                markFailureNotice.gameObject.SetActive(false);
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
        private void ContinueFromMenu()
        {
            if (state.Phase == LevelPhase.Won) Next();
            else TogglePause();
        }
        private void CloseOutcome() => world.Level.DismissOutcome();
        private void OnOutcomeMenuChanged() => RefreshPhase(state.Phase);
        private void Retry() => world.Level.RequestRestart();
        private void ReturnHome() => world.Level.RequestMainMenu();
        private void Next()
        {
            if (world.State.Phase != LevelPhase.Won) return;
            if (world.Level.HasNextLevel) world.Level.RequestNextLevel();
            else ReturnHome();
        }
        private void OpenSettings() => world.Level.SetPauseSettingsVisible(true);
        private void CloseSettings() => world.Level.SetPauseSettingsVisible(false);
        private void OnPauseSettingsChanged(bool visible)
        {
            if (state == null) return;
            RefreshPhase(state.Phase);
            if (EventSystem.current != null)
            {
                var target = visible ? settingsBackButton : state.Phase == LevelPhase.Paused ? pauseSettingsButton : null;
                if (target != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }
        private void OnCounterChanged(int value) => RefreshCounters();
        private void OnHealthChanged(int value)
        {
            // Only newly lost cells flash; mark/enemy counter changes cannot retrigger it.
            if (displayedHealth >= 0 && value < displayedHealth)
                for (int i = Mathf.Max(0, value); i < Mathf.Min(displayedHealth, healthCells.Length); i++)
                    healthFlashUntil[i] = Time.unscaledTime + .28f;
            displayedHealth = value;
            RefreshCounters();
        }
        private void RefreshHealthCells()
        {
            float pulse = health.CurrentHealth == 1 ? .88f + .12f * Mathf.Sin(healthPulseTime * 3.2f) : 1f;
            for (int i = 0; i < healthCells.Length; i++)
            {
                bool alive = i < health.CurrentHealth;
                float flash = alive ? 0f : Mathf.Clamp01((healthFlashUntil[i] - Time.unscaledTime) / .28f);
                if (healthCells[i] != null)
                    healthCells[i].color = alive ? Color.white : Color.Lerp(new Color(.30f, .42f, .48f, .55f), new Color(1f, .48f, .15f), flash);
                if (healthFills[i] != null)
                    healthFills[i].color = alive ? new Color(.025f, .38f * pulse, .49f * pulse, .92f) :
                        Color.Lerp(new Color(.025f, .055f, .07f, .85f), new Color(1f, .20f, .055f, .95f), flash);
                if (healthHighlights[i] != null)
                    healthHighlights[i].color = alive ? new Color(.15f, .75f * pulse, .87f * pulse, .65f) :
                        Color.Lerp(new Color(.05f, .11f, .13f, .3f), new Color(1f, .52f, .16f, .8f), flash);
            }
        }
        private void RefreshCounters()
        {
            healthText.text = "生命值";
            enemiesText.text = $"剩余敌人  {state.ActiveEnemyCount}";
            marksText.text = state.UnlimitedMarks ? "标记  ∞" : $"标记  {state.RemainingMarks}/{state.MarkBudget}";
            RefreshHealthCells();
            if (markIcon != null)
                markIcon.color = state.UnlimitedMarks || state.RemainingMarks > 0
                    ? Color.white : new Color(.42f, .36f, .30f, .60f);
            budgetHintText.text = state.UnlimitedMarks ? "GM 无限标记已开启；可见点选与目标权限规则仍有效。" : state.RemainingMarks == 0 ? "标记次数已用尽。仍可移动、利用接触传播，或按 R 重试。" : "首次赋极与改色均消耗 1 次标记；接触传播免费。";
        }
        private void OnMark(MarkResult result)
        {
            MarkNotificationCount++;
            markFeedbackText.text = state.UnlimitedMarks && result.Outcome == MarkOutcome.Applied ? $"主动标记  {result.Request.TargetId}  {(result.Request.Polarity == Polarity.Model.Polarity.Positive ? "+" : "-")}  /  GM 不消耗次数" : Describe(result);
            markFeedbackText.color = result.Request.Polarity == Polarity.Model.Polarity.Positive ? new Color(1f, .65f, .25f) : new Color(.35f, .75f, 1f);
            RefreshCounters();
            ShowMarkFailure(result);
        }
        private void ShowMarkFailure(MarkResult result)
        {
            if (result.Outcome == MarkOutcome.Applied || result.Outcome == MarkOutcome.NotRunning ||
                markFeedbackText.isActiveAndEnabled)
            {
                if (markFailureNotice != null) markFailureNotice.gameObject.SetActive(false);
                return;
            }
            // The compact HUD may hide the old Controls text. Keep rejection reasons visible
            // without restoring that whole panel or adding another pointer-blocking graphic.
            if (markFailureNotice == null)
            {
                var notice = new GameObject("MarkFailureNotice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                notice.transform.SetParent(transform, false);
                markFailureNotice = notice.GetComponent<Text>();
                markFailureNotice.font = markFeedbackText.font;
                markFailureNotice.fontSize = 22;
                markFailureNotice.alignment = TextAnchor.MiddleCenter;
                markFailureNotice.color = new Color(1f, .82f, .48f);
                markFailureNotice.raycastTarget = false;
                markFailureNotice.supportRichText = false;
                var rect = markFailureNotice.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 96f);
                rect.sizeDelta = new Vector2(760f, 44f);
                var shadow = notice.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .9f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }
            markFailureNotice.text = Describe(result);
            markFailureUntil = Time.unscaledTime + 2f;
            markFailureNotice.gameObject.SetActive(true);
        }
        private void OnPropagation(PropagationResult result)
        {
            PropagationNotificationCount++;
            propagationText.text = Describe(result);
            propagationText.color = result.Polarity == Polarity.Model.Polarity.Positive ? new Color(1f, .65f, .25f) : new Color(.35f, .75f, 1f);
        }
        private void RefreshPhase(LevelPhase phase)
        {
            bool paused = phase == LevelPhase.Paused;
            bool ended = phase == LevelPhase.Won || phase == LevelPhase.Lost;
            bool menuOpen = paused || (ended && world.Level.IsEndMenuOpen);
            bool resultOpen = ended && !world.Level.IsResultDismissed;
            if (!menuOpen && world.Level.IsPauseSettingsOpen) world.Level.SetPauseSettingsVisible(false);
            bool settingsOpen = menuOpen && world.Level.IsPauseSettingsOpen;
            if (pauseSettingsPanel != null) pauseSettingsPanel.SetActive(settingsOpen);
            modal.SetActive((menuOpen && !settingsOpen) || resultOpen);
            if (pauseCard != null) pauseCard.SetActive(menuOpen && !settingsOpen);
            if (resultCard != null) resultCard.SetActive(resultOpen && (phase != LevelPhase.Lost || failureCard == null));
            if (failureCard != null) failureCard.SetActive(resultOpen && phase == LevelPhase.Lost);
            if (phase == LevelPhase.Lost && failureCard != null)
            {
                if (failureLevelText != null) failureLevelText.text = $"第 {world.Level.CurrentLevelIndex + 1} 关  ·  {world.Level.CurrentDefinition?.Title}";
                if (failureTimeText != null) failureTimeText.text = $"{world.SimulationTime:0.0} 秒";
                if (failureEnemiesText != null) failureEnemiesText.text = $"{state.ActiveEnemyCount}";
                if (failureMarksText != null) failureMarksText.text = state.UnlimitedMarks ? "∞" : $"{state.MarkBudget - state.RemainingMarks} / {state.MarkBudget}";
            }
            if (menuOpen && pauseSummary != null)
                pauseSummary.text = $"第 {world.Level.CurrentLevelIndex + 1} 关  /  {world.Level.CurrentDefinition.Title}\n" +
                    (phase == LevelPhase.Won ? "挑战成功，可重试或继续前进。" : phase == LevelPhase.Lost ? "挑战失败，可重试本关。" : "游戏已暂停，准备好后继续。");
            resumeButton.gameObject.SetActive(menuOpen);
            resumeButton.GetComponentInChildren<Text>().text = phase == LevelPhase.Won ?
                (world.Level.HasNextLevel ? "下一关" : "返回主页") : "继续";
            if (nextButton!=null)
            {
                nextButton.gameObject.SetActive(phase==LevelPhase.Won);
                nextButton.GetComponentInChildren<Text>().text = world.Level.HasNextLevel ? "通往下一关" : "返回主页";
                var retryRect = (RectTransform)retryButton.transform;
                retryRect.anchoredPosition = new Vector2(phase == LevelPhase.Won ? 180 : 0, -156);
                ((RectTransform)nextButton.transform).anchoredPosition = new Vector2(-180, -156);
            }
            pauseButton.interactable = phase == LevelPhase.Running || paused || ended;
            modalTitle.text = paused ? "已暂停" : phase == LevelPhase.Won ? (world.Level.HasNextLevel ? "本关完成" : "全部关卡完成") : "挑战失败";
            modalDescription.text = paused ? "WASD 移动 | 左键 + 橙 | 右键 - 蓝\n空格慢速 0.3 倍 | Esc 继续 | R 重试" : phase == LevelPhase.Won
                ? $"用时 {world.SimulationTime:0.0} 秒  |  受伤 {world.PlayerDamageCount} 次\n标记使用 {state.MarkBudget - state.RemainingMarks}/{state.MarkBudget} 次\n" + (world.Level.HasNextLevel ? "本关完成，准备进入下一关。" : "全部六关完成！")
                : "生命已耗尽。试试不同的标记或走位。\n\n按 R 或点击“重试”，重新挑战本关。";
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
                case MarkOutcome.BudgetEmpty: return "标记次数已用尽，仍可移动或重试。";
                case MarkOutcome.InvalidTarget: return "请指向敌人或箱子，不消耗次数。";
                case MarkOutcome.NotMarkable: return "只能标记敌人与箱子，不消耗次数。";
                case MarkOutcome.NotRunning: return "模拟已停止，不消耗次数。";
                default: return "暂时无法标记，不消耗次数。";
            }
        }
    }
}
