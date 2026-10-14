using UnityEngine;
using UnityEngine.UI;
using Polarity.Model;

namespace Polarity.Debugging
{
    /// <summary>GM controls submit commands; gameplay changes remain in World and LevelController.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(110)]
    public sealed class GMView : MonoBehaviour
    {
        [SerializeField] private GMManager manager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text factsText, statusText, invincibleLabel, unlimitedLabel;
        [SerializeField] private Button openButton, closeButton, invincibleButton, healthButton, marksButton;
        [SerializeField] private Button unlimitedButton, nextButton, retryButton, resetButton;
        private GameObject subPanel;
        private Text subTitle;
        private InputField valueInput;
        private Button[] levelButtons;
        private Text openLabel, healthLabel, marksLabel, nextLabel, resetLabel;
        private bool editingHealth;
        public bool BlocksPointer => GMManager.IsAvailable && manager != null && manager.PanelOpen && isActiveAndEnabled;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            BuildSubPanel();
            openButton.onClick.AddListener(TogglePanel); closeButton.onClick.AddListener(TogglePanel);
            invincibleButton.onClick.AddListener(ToggleInvincible); unlimitedButton.onClick.AddListener(ToggleUnlimited);
            healthButton.onClick.AddListener(OpenHealth); marksButton.onClick.AddListener(OpenMarks);
            nextButton.onClick.AddListener(OpenLevels); retryButton.onClick.AddListener(Retry); resetButton.onClick.AddListener(ResetCheats);
            if (manager != null) manager.Changed += Refresh;
            Refresh();
        }
        private void Start() => Refresh();
        private void BuildSubPanel()
        {
            if (subPanel != null) return;
            openLabel = openButton.GetComponentInChildren<Text>();
            healthLabel = healthButton.GetComponentInChildren<Text>();
            marksLabel = marksButton.GetComponentInChildren<Text>();
            nextLabel = nextButton.GetComponentInChildren<Text>();
            resetLabel = resetButton.GetComponentInChildren<Text>();
            var card = nextButton.transform.parent;
            subPanel = new GameObject("GMSubPanel", typeof(RectTransform), typeof(Image));
            subPanel.transform.SetParent(card, false);
            var rect = (RectTransform)subPanel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(500, 350);
            rect.anchoredPosition = new Vector2(0, -25);
            subPanel.GetComponent<Image>().color = new Color(.055f, .065f, .075f, .98f);
            subTitle = MakeText("Title", new Vector2(0, 145), new Vector2(470, 34), 21);
            levelButtons = new Button[6];
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int index = i;
                levelButtons[i] = MakeButton("Level" + (i + 1), new Vector2(i % 2 == 0 ? -123 : 123, 79 - i / 2 * 56),
                    new Vector2(225, 44), () => manager.GoToLevel(index));
            }
            MakeButton("AddOne", new Vector2(-123, 75), new Vector2(225, 44), () => AddResource(1));
            MakeButton("AddFive", new Vector2(123, 75), new Vector2(225, 44), () => AddResource(5));
            MakeInput();
            MakeButton("SetValue", new Vector2(0, -80), new Vector2(225, 44), SetResource);
            MakeButton("Back", new Vector2(0, -143), new Vector2(225, 44), () => subPanel.SetActive(false));
            subPanel.SetActive(false);
        }
        private Text MakeText(string name, Vector2 position, Vector2 size, int fontSize)
        {
            var copy = Instantiate(factsText, subPanel.transform);
            copy.name = name;
            var rect = copy.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            copy.fontSize = fontSize;
            copy.alignment = TextAnchor.MiddleCenter;
            copy.color = Color.white;
            copy.raycastTarget = false;
            return copy;
        }
        private Button MakeButton(string name, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var copy = Instantiate(nextButton, subPanel.transform);
            copy.name = name;
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            copy.onClick = new Button.ButtonClickedEvent();
            copy.onClick.AddListener(action);
            copy.GetComponentInChildren<Text>().text = name == "AddOne" ? "+1" : name == "AddFive" ? "+5" :
                name == "SetValue" ? "设为输入值" : name == "Back" ? "返回" : "";
            return copy;
        }
        private void MakeInput()
        {
            var container = new GameObject("ValueInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            container.transform.SetParent(subPanel.transform, false);
            var rect = (RectTransform)container.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -20);
            rect.sizeDelta = new Vector2(225, 42);
            container.GetComponent<Image>().color = new Color(.16f, .22f, .27f, 1f);
            var valueText = MakeText("Value", Vector2.zero, new Vector2(205, 40), 19);
            valueText.transform.SetParent(container.transform, false);
            valueText.rectTransform.anchorMin = Vector2.zero;
            valueText.rectTransform.anchorMax = Vector2.one;
            valueText.rectTransform.offsetMin = new Vector2(10, 0);
            valueText.rectTransform.offsetMax = new Vector2(-10, 0);
            valueInput = container.GetComponent<InputField>();
            valueInput.textComponent = valueText;
            valueInput.contentType = InputField.ContentType.IntegerNumber;
            valueInput.characterLimit = 3;
        }
        private static string PhaseName(LevelPhase phase)
        {
            switch (phase)
            {
                case LevelPhase.Ready: return "准备中";
                case LevelPhase.Running: return "运行中";
                case LevelPhase.Paused: return "已暂停";
                case LevelPhase.Won: return "已通关";
                case LevelPhase.Lost: return "已失败";
                default: return phase.ToString();
            }
        }
        private void Refresh()
        {
            bool ready = GMManager.IsAvailable && manager != null && manager.IsBound;
            openButton.gameObject.SetActive(ready);
            panel.SetActive(ready && manager.PanelOpen);
            if (!ready) return;
            var w = manager.World;
            if (!manager.PanelOpen) subPanel.SetActive(false);
            factsText.text = $"第 {w.Level.CurrentLevelIndex + 1} / {w.Level.LevelCount} 关 | {PhaseName(w.State.Phase)}\n生命 {w.PlayerHealth.CurrentHealth} / {w.PlayerHealth.MaxHealth} | 标记 " +
                (w.State.UnlimitedMarks ? $"∞（保留 {w.State.RemainingMarks} / {w.State.MarkBudget}）" : $"{w.State.RemainingMarks} / {w.State.MarkBudget}") +
                "\n开关随重试与切关保留；调整的资源仅本关有效。";
            statusText.text = manager.Status;
            invincibleLabel.text = manager.Invincible ? "无敌：开启" : "无敌：关闭";
            unlimitedLabel.text = manager.UnlimitedMarks ? "无限标记：开启" : "无限标记：关闭";
            string tags = manager.TestState == "正常规则" ? "正常规则" : manager.TestState
                .Replace("GM 测试：", "").Replace("无限标记", "无限")
                .Replace("资源已调整", "资源").Replace("已跳关", "跳关")
                .Replace("本关曾作弊", "曾作弊").Replace("、", "·");
            openLabel.text = "GM [F1]\n" + tags;
            openLabel.fontSize = 12;
            openButton.GetComponent<Image>().color = manager.TestState == "正常规则" ? new Color(.13f, .27f, .38f) : new Color(.55f, .22f, .16f);
            healthLabel.text = "调整生命";
            marksLabel.text = "调整标记次数";
            nextLabel.text = "选择关卡";
            resetLabel.text = "关闭作弊开关";
            bool mutable = !w.Level.IsRestarting && (w.State.Phase == LevelPhase.Running || w.State.Phase == LevelPhase.Paused);
            healthButton.interactable = marksButton.interactable = mutable;
            nextButton.interactable = !w.Level.IsRestarting;
            retryButton.interactable = !w.Level.IsRestarting;
        }
        private void OpenLevels()
        {
            var level = manager.World.Level;
            subTitle.text = "选择关卡（重新加载房间）";
            valueInput.gameObject.SetActive(false);
            SetResourceControls(false);
            for (int i = 0; i < levelButtons.Length; i++)
            {
                levelButtons[i].gameObject.SetActive(i < level.LevelCount);
                if (i < level.LevelCount)
                {
                    levelButtons[i].interactable = i != level.CurrentLevelIndex;
                    levelButtons[i].GetComponentInChildren<Text>().text = $"第 {i + 1} 关  {level.GetLevelTitle(i)}";
                }
            }
            subPanel.SetActive(true);
        }
        private void OpenHealth() => OpenResource(true);
        private void OpenMarks() => OpenResource(false);
        private void OpenResource(bool health)
        {
            editingHealth = health;
            subTitle.text = health ? "调整当前生命（1–999）" : "调整剩余标记（0–999）";
            valueInput.gameObject.SetActive(true);
            valueInput.text = health ? manager.World.PlayerHealth.CurrentHealth.ToString() : manager.World.State.RemainingMarks.ToString();
            SetResourceControls(true);
            foreach (var button in levelButtons) button.gameObject.SetActive(false);
            subPanel.SetActive(true);
        }
        private void SetResourceControls(bool visible)
        {
            foreach (Transform child in subPanel.transform)
                if (child.name == "AddOne" || child.name == "AddFive" || child.name == "SetValue") child.gameObject.SetActive(visible);
        }
        private void AddResource(int amount)
        {
            if (editingHealth) manager.AddHealth(amount); else manager.AddMarks(amount);
            valueInput.text = editingHealth ? manager.World.PlayerHealth.CurrentHealth.ToString() : manager.World.State.RemainingMarks.ToString();
        }
        private void SetResource()
        {
            if (!int.TryParse(valueInput.text, out int target))
            {
                manager.ReportInvalidInput();
                return;
            }
            if (editingHealth) manager.SetHealth(target); else manager.SetRemainingMarks(target);
        }
        private void TogglePanel() => manager.TogglePanel();
        private void ToggleInvincible() => manager.SetInvincible(!manager.Invincible);
        private void ToggleUnlimited() => manager.SetUnlimitedMarks(!manager.UnlimitedMarks);
        private void Retry() => manager.Retry();
        private void ResetCheats() => manager.ResetCheats();
        private void OnDisable()
        {
            if (manager != null) manager.Changed -= Refresh;
            openButton.onClick.RemoveListener(TogglePanel); closeButton.onClick.RemoveListener(TogglePanel);
            invincibleButton.onClick.RemoveListener(ToggleInvincible); unlimitedButton.onClick.RemoveListener(ToggleUnlimited);
            healthButton.onClick.RemoveListener(OpenHealth); marksButton.onClick.RemoveListener(OpenMarks);
            nextButton.onClick.RemoveListener(OpenLevels); retryButton.onClick.RemoveListener(Retry); resetButton.onClick.RemoveListener(ResetCheats);
            if (subPanel != null) subPanel.SetActive(false);
            panel.SetActive(false);
        }
    }
}
