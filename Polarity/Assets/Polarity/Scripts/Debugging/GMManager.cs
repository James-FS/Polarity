using System;
using System.Linq;
using Polarity.Entities;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Debugging
{
    internal enum GMCommand { Invincible, AddHealth, AddMarks, UnlimitedMarks, SetHealth, SetRemainingMarks }

    /// <summary>Debug commands submit to World and LevelController; preferences contain no scene references.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SimulationWorld)), DefaultExecutionOrder(-75)]
    public sealed class GMManager : MonoBehaviour
    {
        private static bool invincible, unlimitedMarks, panelOpen, jumpedLevel;
        private SimulationWorld world;
        private Health health;
        private LevelState state;
        private bool resourcesEdited;
        private bool cheatUsedThisRoom;
        public static bool IsAvailable => Application.isPlaying && (Application.isEditor || Debug.isDebugBuild);
        public bool IsBound => state != null && world != null && world.OwnsPhysics;
        public bool Invincible => invincible;
        public bool UnlimitedMarks => unlimitedMarks;
        public string TestState => !invincible && !unlimitedMarks && !resourcesEdited && !jumpedLevel && !cheatUsedThisRoom ? "正常规则" :
            "GM 测试：" + string.Join("、", new[] {
                invincible ? "无敌" : null, unlimitedMarks ? "无限标记" : null,
                resourcesEdited ? "资源已调整" : null, jumpedLevel ? "已跳关" : null,
                cheatUsedThisRoom && !invincible && !unlimitedMarks ? "本关曾作弊" : null
            }.Where(x => x != null));
        public bool PanelOpen => IsAvailable && panelOpen;
        public SimulationWorld World => world;
        public string Status { get; private set; } = "F1 打开 / 关闭 GM；Esc 暂停游戏。";
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { invincible = unlimitedMarks = panelOpen = jumpedLevel = false; }
        private void OnEnable() { if (IsAvailable) Bind(); }
        private void Start() { if (IsAvailable) Bind(); }
        private void Update() { if (IsAvailable && Input.GetKeyDown(KeyCode.F1)) TogglePanel(); }
        private void Bind()
        {
            if (state != null) return;
            world = GetComponent<SimulationWorld>();
            if (world.State == null || world.PlayerHealth == null || !world.OwnsPhysics) return;
            state = world.State; health = world.PlayerHealth;
            state.MarksChanged += OnCounter;
            state.PhaseChanged += OnPhase;
            health.HealthChanged += OnCounter;
            world.ApplyGM(GMCommand.Invincible, invincible ? 1 : 0);
            world.ApplyGM(GMCommand.UnlimitedMarks, unlimitedMarks ? 1 : 0);
            Changed?.Invoke();
        }
        public void TogglePanel()
        {
            if (!IsAvailable) return;
            panelOpen = !panelOpen;
            Changed?.Invoke();
        }
        public bool SetInvincible(bool value)
        {
            if (!IsBound || !world.ApplyGM(GMCommand.Invincible, value ? 1 : 0)) return Report(false);
            invincible = value;
            if (value) cheatUsedThisRoom = true;
            return Report(true, value ? "无敌已开启；碰撞仍正常，玩家不受伤。" : "无敌已关闭。");
        }
        public bool SetUnlimitedMarks(bool value)
        {
            if (!IsBound || !world.ApplyGM(GMCommand.UnlimitedMarks, value ? 1 : 0)) return Report(false);
            unlimitedMarks = value;
            if (value) cheatUsedThisRoom = true;
            return Report(true, value ? "无限标记已开启；原有次数保留。" : "无限标记已关闭；恢复原有剩余次数。");
        }
        public bool AddHealth(int amount = 1) => ChangeResource(GMCommand.AddHealth, amount, $"生命与上限 +{amount}；仅当前关有效。");
        public bool AddMarks(int amount = 1) => ChangeResource(GMCommand.AddMarks, amount, $"剩余标记与预算 +{amount}；仅当前关有效。");
        public bool SetHealth(int target) => ChangeResource(GMCommand.SetHealth, target, $"当前生命设为 {target}；必要时提高上限，仅当前关有效。");
        public bool SetRemainingMarks(int target) => ChangeResource(GMCommand.SetRemainingMarks, target, $"剩余标记设为 {target}；已使用次数保持不变，仅当前关有效。");
        public void ReportInvalidInput() => Report(false, null, "请输入允许范围内的整数。");
        private bool ChangeResource(GMCommand command, int value, string message)
        {
            bool inRange = value >= 0 && value <= 999;
            if (IsBound && command == GMCommand.AddHealth)
                inRange = value > 0 && world.PlayerHealth.CurrentHealth <= 999 - value && world.PlayerHealth.MaxHealth <= 999 - value;
            if (IsBound && command == GMCommand.AddMarks)
                inRange = value > 0 && world.State.RemainingMarks <= 999 - value;
            bool success = IsBound && inRange && world.ApplyGM(command, value);
            if (success) resourcesEdited = true;
            return Report(success, message, "操作未执行；请检查当前阶段和数值范围。");
        }
        public bool NextLevel()
        {
            if (!IsBound) return Report(false);
            if (!world.Level.HasNextLevel) return Report(false, null, "已是最后一关，无法继续跳关。");
            bool success = world.Level.RequestGMNextLevel();
            if (success) jumpedLevel = true;
            return Report(success, "正在进入下一关……");
        }
        public bool GoToLevel(int index)
        {
            if (!IsBound || index < 0 || index >= world.Level.LevelCount)
                return Report(false, null, "关卡编号无效。");
            if (index == world.Level.CurrentLevelIndex)
                return Report(false, null, "已在这一关；可使用重试恢复初始状态。");
            bool success = world.Level.RequestGMLevel(index);
            if (success) jumpedLevel = true;
            return Report(success, $"正在进入第 {index + 1} 关……");
        }
        public bool Retry() => Report(IsBound && world.Level.RequestRestart(), "正在重试当前关……");
        public void ResetCheats()
        {
            if (!IsBound || world.Level.IsRestarting) { Report(false); return; }
            SetInvincible(false);
            SetUnlimitedMarks(false);
            Status = resourcesEdited || cheatUsedThisRoom ? "作弊开关已关闭；本关测试状态仍会提示。重试可恢复初始值。" : "作弊开关已关闭。";
            Changed?.Invoke();
        }
        private bool Report(bool success, string message = null, string failureMessage = null)
        {
            Status = success ? message ?? "操作已完成。" : failureMessage ?? "操作未执行；终局加血或加次数需先重试。";
            Changed?.Invoke();
            return success;
        }
        private void OnCounter(int value) => Changed?.Invoke();
        private void OnPhase(LevelPhase phase) => Changed?.Invoke();
        private void OnDisable()
        {
            if (state != null) { state.MarksChanged -= OnCounter; state.PhaseChanged -= OnPhase; }
            if (health != null) health.HealthChanged -= OnCounter;
            state = null; health = null; world = null;
            Changed?.Invoke();
        }
        private void OnDestroy() => Changed = null;
    }
}
