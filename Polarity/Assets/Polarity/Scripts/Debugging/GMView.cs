using UnityEngine;
using UnityEngine.UI;
using Polarity.Model;

namespace Polarity.Debugging
{
    /// <summary>Chinese GM panel. Buttons only submit commands; no physics or rule mutations here.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(110)]
    public sealed class GMView : MonoBehaviour
    {
        [SerializeField] private GMManager manager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text factsText, statusText, invincibleLabel, unlimitedLabel;
        [SerializeField] private Button openButton, closeButton, invincibleButton, healthButton, marksButton;
        [SerializeField] private Button unlimitedButton, nextButton, retryButton, resetButton;
        public bool BlocksPointer => GMManager.IsAvailable && manager != null && manager.PanelOpen && isActiveAndEnabled;
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            openButton.onClick.AddListener(TogglePanel); closeButton.onClick.AddListener(TogglePanel);
            invincibleButton.onClick.AddListener(ToggleInvincible); unlimitedButton.onClick.AddListener(ToggleUnlimited);
            healthButton.onClick.AddListener(AddHealth); marksButton.onClick.AddListener(AddMarks);
            nextButton.onClick.AddListener(Next); retryButton.onClick.AddListener(Retry); resetButton.onClick.AddListener(ResetCheats);
            if (manager != null) manager.Changed += Refresh;
            Refresh();
        }
        private void Start() => Refresh();
        private void Refresh()
        {
            bool ready = GMManager.IsAvailable && manager != null && manager.IsBound;
            openButton.gameObject.SetActive(ready);
            panel.SetActive(ready && manager.PanelOpen);
            if (!ready) return;
            var w = manager.World;
            factsText.text = $"第 {w.Level.CurrentLevelIndex + 1} / {w.Level.LevelCount} 关 | {w.State.Phase}\n生命 {w.PlayerHealth.CurrentHealth} / {w.PlayerHealth.MaxHealth} | 标记 " +
                (w.State.UnlimitedMarks ? $"∞（保留 {w.State.RemainingMarks} / {w.State.MarkBudget}）" : $"{w.State.RemainingMarks} / {w.State.MarkBudget}") +
                "\n开关随重试与切关保留；增加的生命与次数仅本关有效。";
            statusText.text = manager.Status;
            invincibleLabel.text = manager.Invincible ? "无敌：开启" : "无敌：关闭";
            unlimitedLabel.text = manager.UnlimitedMarks ? "无限标记：开启" : "无限标记：关闭";
            bool mutable = !w.Level.IsRestarting && (w.State.Phase == LevelPhase.Running || w.State.Phase == LevelPhase.Paused);
            healthButton.interactable = marksButton.interactable = mutable;
            nextButton.interactable = !w.Level.IsRestarting && w.Level.HasNextLevel;
            retryButton.interactable = !w.Level.IsRestarting;
        }
        private void TogglePanel() => manager.TogglePanel();
        private void ToggleInvincible() => manager.SetInvincible(!manager.Invincible);
        private void ToggleUnlimited() => manager.SetUnlimitedMarks(!manager.UnlimitedMarks);
        private void AddHealth() => manager.AddHealth();
        private void AddMarks() => manager.AddMarks();
        private void Next() => manager.NextLevel();
        private void Retry() => manager.Retry();
        private void ResetCheats() => manager.ResetCheats();
        private void OnDisable()
        {
            if (manager != null) manager.Changed -= Refresh;
            openButton.onClick.RemoveListener(TogglePanel); closeButton.onClick.RemoveListener(TogglePanel);
            invincibleButton.onClick.RemoveListener(ToggleInvincible); unlimitedButton.onClick.RemoveListener(ToggleUnlimited);
            healthButton.onClick.RemoveListener(AddHealth); marksButton.onClick.RemoveListener(AddMarks);
            nextButton.onClick.RemoveListener(Next); retryButton.onClick.RemoveListener(Retry); resetButton.onClick.RemoveListener(ResetCheats);
            panel.SetActive(false);
        }
    }
}
