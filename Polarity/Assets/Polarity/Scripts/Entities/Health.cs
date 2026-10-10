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
        public int MaxHealth => Mathf.Max(1, maxHealth);
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
        internal bool CanReceiveDamage(double time) => IsValid && Rigidbody.simulated && time + 0.0000001 >= NextDamageTime;
        internal void ApplyDamage(int amount, double time, float interval)
        { CurrentHealth = Mathf.Max(0, CurrentHealth - amount); NextDamageTime = time + interval; }
        internal void NotifyHealthChanged() => HealthChanged?.Invoke(CurrentHealth);
        private void OnDestroy() { World = null; HealthChanged = null; }
        private void OnValidate() => maxHealth = Mathf.Max(1, maxHealth);
    }
}
