using System.Collections.Generic;
using Polarity.Controllers;
using Polarity.Entities;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;
using UnityEngine.UI;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Views
{
    /// <summary>First-room guidance reads committed facts; only buttons submit flow requests.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(HUDView)), DefaultExecutionOrder(150)]
    public sealed class FirstLevelTutorialView : MonoBehaviour
    {
        [SerializeField] private GameObject introduction;
        [SerializeField] private Button reviewButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text guidanceText, marksText, hoverText;
        [SerializeField] private RectTransform hoverPanel;
        private HUDView hud;
        private SimulationWorld world;
        private MarkingController marking;
        private Canvas canvas;
        private Color marksColor;
        private readonly Dictionary<int, Pole> knownPolarities = new Dictionary<int, Pole>();
        private readonly HashSet<DamageKind> explainedDamage = new HashSet<DamageKind>();
        private string notice;
        private float noticeUntil, pulseUntil;
        private bool playerWarningShown, movementHintShown;
        public bool IsGuidanceActive => world != null && world.Level != null && world.Level.IsFirstLevel;
        public bool IntroductionVisible => introduction != null && introduction.activeSelf;
        public bool HoverVisible => hoverPanel != null && hoverPanel.gameObject.activeSelf;
        public string RelationMessage { get; private set; } = "";
        public string LastDamageMessage { get; private set; } = "";
        public int MarkFeedbackCount { get; private set; }
        public int DamageFeedbackCount { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            hud = GetComponent<HUDView>();
            canvas = GetComponent<Canvas>();
            marksColor = marksText.color;
            if (closeButton != null) closeButton.onClick.AddListener(Begin);
            reviewButton.onClick.AddListener(Review);
            Bind();
        }
        private void Start() => Bind();
        private void Bind()
        {
            if (world != null || hud == null || hud.World == null || hud.World.State == null) return;
            world = hud.World;
            marking = world.GetComponent<MarkingController>();
            if (IsGuidanceActive)
            {
                foreach (var body in world.Bodies) if (body != null) knownPolarities[body.BodyId] = body.CurrentPolarity;
                world.MarkResolved += OnMark;
                world.PropagationCommitted += OnPropagation;
                world.DamageCommitted += OnDamage;
            }
            RefreshPresentation();
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (closeButton != null) closeButton.onClick.RemoveListener(Begin);
            reviewButton.onClick.RemoveListener(Review);
            if (world != null)
            {
                world.MarkResolved -= OnMark;
                world.PropagationCommitted -= OnPropagation;
                world.DamageCommitted -= OnDamage;
            }
            marksText.color = marksColor;
            introduction.SetActive(false);
            hoverPanel.gameObject.SetActive(false);
            world = null;
            knownPolarities.Clear(); explainedDamage.Clear();
            notice = null; noticeUntil = pulseUntil = 0;
            playerWarningShown = movementHintShown = false;
            MarkFeedbackCount = DamageFeedbackCount = 0;
            RelationMessage = LastDamageMessage = "";
        }
        private void Begin() { world?.Level.RequestBeginLevel(); RefreshPresentation(); }
        private void Review() { world?.Level.RequestShowIntroduction(); RefreshPresentation(); }
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (world == null) Bind();
            RefreshPresentation();
        }
        public void RefreshPresentation()
        {
            bool active = IsGuidanceActive;
            introduction.SetActive(active && world.Level.IsIntroductionOpen);
            reviewButton.gameObject.SetActive(active);
            reviewButton.interactable = active && !world.Level.IsRestarting &&
                (world.State.Phase == LevelPhase.Running || world.State.Phase == LevelPhase.Paused);
            if (!active) { hoverPanel.gameObject.SetActive(false); return; }
            UpdateGuidance();
            UpdateHover();
            marksText.color = Time.unscaledTime < pulseUntil ? new Color(1f, .85f, .3f) : marksColor;
        }
        private void UpdateGuidance()
        {
            RelationMessage = ReadRelation();
            if (world.State.Phase == LevelPhase.Ready)
            { guidanceText.text = "准备好后开始；用磁力和左侧尖刺清除敌人。"; return; }
            if (world.State.Phase == LevelPhase.Won || world.State.Phase == LevelPhase.Lost)
            {
                guidanceText.text = world.State.Phase == LevelPhase.Won ? "敌人已清除。" : "按 R 重试，尝试不同的标记或走位。";
                if (!string.IsNullOrEmpty(LastDamageMessage)) guidanceText.text += "  " + LastDamageMessage;
                return;
            }
            if (!movementHintShown && world.State.Phase == LevelPhase.Running && world.SimulationTime >= 2.5 &&
                world.Player.MoveIntent.sqrMagnitude < .01f && Time.unscaledTime >= noticeUntil)
            {
                movementHintShown = true;
                ShowNotice("按住空格慢速观察；WASD 移动会改变敌人的追击方向。", 5f);
            }
            string instruction = world.State.RemainingMarks == 0 ? "次数用尽仍可移动；按 R 重试。" :
                Time.unscaledTime < noticeUntil ? notice : "指向敌人或箱子：左键 +，右键 −。空格慢速，R 重试。";
            guidanceText.text = RelationMessage + "\n" + instruction;
        }
        private string ReadRelation()
        {
            PolarityBody enemy = null, crate = null;
            int enemyLayer = LayerMask.NameToLayer("Enemy"), crateLayer = LayerMask.NameToLayer("Crate");
            foreach (var body in world.Bodies)
            {
                if (body == null || !body.IsValid) continue;
                if (body.gameObject.layer == enemyLayer) enemy = body;
                else if (body.gameObject.layer == crateLayer) crate = body;
            }
            if (enemy == null || crate == null) return "利用磁力、撞击与左侧尖刺清除敌人。";
            if (enemy.CurrentPolarity == Pole.Neutral || crate.CurrentPolarity == Pole.Neutral)
                return "灰色表示无极性；双方都有极性时，才会相吸或相斥。";
            bool same = enemy.CurrentPolarity == crate.CurrentPolarity;
            Vector2 offset = crate.Rigidbody.position - enemy.Rigidbody.position;
            string rule = same ? "同极相斥。" : "异极相吸。";
            if (offset.sqrMagnitude >= world.RuntimeSettings.MagneticRadius * world.RuntimeSettings.MagneticRadius)
                return rule + "当前两者超出磁力范围。";
            float separation = Vector2.Dot(crate.Rigidbody.velocity - enemy.Rigidbody.velocity, offset.normalized);
            string motion = separation < -.08f ? "双方正在靠近。" : separation > .08f ? "双方正在分开。" : "观察双方的实际运动。";
            return rule + motion + (same ? "调整站位，利用排斥方向。" : "改成同极会怎样？");
        }
        private void OnMark(MarkResult result)
        {
            if (!IsGuidanceActive || result.Outcome == MarkOutcome.NotRunning) return;
            if (result.Outcome == MarkOutcome.Applied)
            {
                Pole previous = knownPolarities.TryGetValue(result.Request.TargetId, out var value) ? value : Pole.Neutral;
                knownPolarities[result.Request.TargetId] = result.Request.Polarity;
                MarkFeedbackCount++;
                pulseUntil = Time.unscaledTime + .65f;
                ShowNotice(previous == Pole.Neutral ? $"赋极消耗一次标记；还剩 {world.State.RemainingMarks} 次。" :
                    $"改色也消耗一次，只改变这个对象；还剩 {world.State.RemainingMarks} 次。", 4f);
            }
            else if (result.Outcome == MarkOutcome.Unchanged)
                ShowNotice("已是这个极性，重复标记不消耗次数。", 4f);
        }
        private void OnPropagation(PropagationResult result)
        {
            knownPolarities[result.TargetId] = result.Polarity;
        }
        private void OnDamage(DamageResult result)
        {
            if (!IsGuidanceActive) return;
            DamageFeedbackCount++;
            if (result.TargetId == 0)
            {
                LastDamageMessage = result.Kind == DamageKind.Spike ? "你碰到了尖刺。" : "敌人接触会伤害你。";
                if (!playerWarningShown)
                {
                    playerWarningShown = true;
                    ShowNotice(LastDamageMessage + "按住空格慢速观察，用 WASD 调整站位。", 5f);
                }
                return;
            }
            LastDamageMessage = result.Kind == DamageKind.Impact ? "强烈撞击伤害了敌人。" :
                result.Kind == DamageKind.Spike ? "尖刺伤害了敌人。" : "敌人受到了伤害。";
            if (explainedDamage.Add(result.Kind)) ShowNotice(LastDamageMessage + $"敌人剩余 {result.RemainingHealth} 点生命。", 4f);
        }
        private void ShowNotice(string text, float duration)
        { notice = text; noticeUntil = Time.unscaledTime + duration; }
        private void UpdateHover()
        {
            var body = marking != null ? marking.Candidate : null;
            bool visible = world.State.Phase == LevelPhase.Running && body != null && body.IsValid &&
                !hud.ContainsScreenPoint(Input.mousePosition);
            hoverPanel.gameObject.SetActive(visible);
            if (!visible) return;
            var view = body.GetComponent<PolarityView>();
            bool found = false;
            Rect bounds = default;
            if (view != null) foreach (var face in view.BodySprites)
            {
                if (!MarkingController.TryScreenRect(face, marking.WorldCamera, out Rect rect)) continue;
                bounds = found ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                    Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax)) : rect;
                found = true;
            }
            if (!found) { hoverPanel.gameObject.SetActive(false); return; }
            float factor = canvas.scaleFactor;
            Rect viewport = marking.WorldCamera.pixelRect;
            float halfWidth = hoverPanel.sizeDelta.x * factor * .5f, halfHeight = hoverPanel.sizeDelta.y * factor * .5f;
            Vector2 point = new Vector2(Mathf.Clamp(bounds.center.x, viewport.xMin + halfWidth, viewport.xMax - halfWidth),
                Mathf.Clamp(bounds.yMax + halfHeight + 12f, viewport.yMin + halfHeight, viewport.yMax - halfHeight));
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, point,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local);
            hoverPanel.anchoredPosition = local;
            hoverText.text = world.State.RemainingMarks == 0 ? "次数用尽，仍可移动" : "左键 + 橙  /  右键 − 蓝";
        }
    }
}
