using System;
using Polarity.Entities;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Debugging
{
    internal enum GMCommand { Invincible, AddHealth, AddMarks, UnlimitedMarks }

    /// <summary>Debug commands submit to World and LevelController; preferences contain no scene references.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SimulationWorld)), DefaultExecutionOrder(-75)]
    public sealed class GMManager : MonoBehaviour
    {
        private static bool invincible, unlimitedMarks, panelOpen;
        private SimulationWorld world;
        private Health health;
        private LevelState state;
        public static bool IsAvailable => Application.isPlaying && (Application.isEditor || Debug.isDebugBuild);
        public bool IsBound => state != null && world != null && world.OwnsPhysics;
        public bool Invincible => invincible;
        public bool UnlimitedMarks => unlimitedMarks;
        public bool PanelOpen => IsAvailable && panelOpen;
        public SimulationWorld World => world;
        public string Status { get; private set; } = "F1 打开 / 关闭 GM；Esc 暂停游戏。";
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { invincible = unlimitedMarks = panelOpen = false; }
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
            return Report(true, value ? "无敌已开启；碰撞仍正常，玩家不受伤。" : "无敌已关闭。");
        }
        public bool SetUnlimitedMarks(bool value)
        {
            if (!IsBound || !world.ApplyGM(GMCommand.UnlimitedMarks, value ? 1 : 0)) return Report(false);
            unlimitedMarks = value;
            return Report(true, value ? "无限标记已开启；原有次数保留。" : "无限标记已关闭；恢复原有剩余次数。");
        }
        public bool AddHealth(int amount = 5)
            => Report(IsBound && world.ApplyGM(GMCommand.AddHealth, amount), "已增加生命与生命上限；仅当前关有效。");
        public bool AddMarks(int amount = 5)
            => Report(IsBound && world.ApplyGM(GMCommand.AddMarks, amount), "已增加标记次数；仅当前关有效。");
        public bool NextLevel()
        {
            if (!IsBound) return Report(false);
            if (!world.Level.HasNextLevel) return Report(false, null, "已是最后一关，无法继续跳关。");
            return Report(world.Level.RequestGMNextLevel(), "正在进入下一关……");
        }
        public bool Retry() => Report(IsBound && world.Level.RequestRestart(), "正在重试当前关……");
        public void ResetCheats()
        {
            if (!IsBound || world.Level.IsRestarting) { Report(false); return; }
            SetInvincible(false);
            SetUnlimitedMarks(false);
            Status = "作弊开关已关闭；重试可还原本关生命与次数。";
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
