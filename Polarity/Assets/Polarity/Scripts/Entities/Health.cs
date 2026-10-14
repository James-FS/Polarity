using System;
using Polarity.Simulation;
using UnityEngine;
namespace Polarity.Entities
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 2;
        private Rigidbody2D rigidBody;
        private bool initialized;
        private int bonusHealth;
        public int MaxHealth => Mathf.Max(1, maxHealth) + bonusHealth;
        public bool IsInvincible { get; internal set; }
        public int CurrentHealth { get; private set; }
        public bool IsAlive => !initialized || CurrentHealth > 0;
        public bool IsValid => World != null && isActiveAndEnabled && IsAlive;
        public int ActorId { get; private set; }
        public SimulationWorld World { get; private set; }
        public Rigidbody2D Rigidbody => rigidBody != null ? rigidBody : (rigidBody = GetComponent<Rigidbody2D>());
        public double NextDamageTime { get; private set; }
        public event Action<int> HealthChanged;
        private void Awake() => Initialize();
        private void Initialize() { if (initialized) return; CurrentHealth = MaxHealth; initialized = true; }
        internal void Bind(SimulationWorld world, int id) { Initialize(); World = world; ActorId = id; }
        internal void Unbind(SimulationWorld world) { if (World == world) World = null; HealthChanged = null; }
        internal bool CanReceiveDamage(double time) => IsValid && !IsInvincible && Rigidbody.simulated && time + 0.0000001 >= NextDamageTime;
        internal void ApplyDamage(int amount, double time, float interval)
        { CurrentHealth = Mathf.Max(0, CurrentHealth - amount); NextDamageTime = time + interval; }
        internal bool AddRuntimeHealth(int amount)
        {
            if (!IsAlive || amount <= 0 || MaxHealth == int.MaxValue) return false;
            int added = Math.Min(amount, int.MaxValue - MaxHealth);
            bonusHealth += added;
            CurrentHealth += added;
            NotifyHealthChanged();
            return true;
        }
        internal bool SetRuntimeHealth(int target)
        {
            if (!IsAlive || target < 1 || target > 999) return false;
            if (target > MaxHealth) bonusHealth += target - MaxHealth;
            CurrentHealth = target;
            NotifyHealthChanged();
            return true;
        }
        internal void NotifyHealthChanged() => HealthChanged?.Invoke(CurrentHealth);
        private void OnDestroy() { World = null; HealthChanged = null; }
        private void OnValidate() => maxHealth = Mathf.Max(1, maxHealth);
    }
}
