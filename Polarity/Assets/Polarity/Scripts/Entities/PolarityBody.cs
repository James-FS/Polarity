using System;
using Polarity.Simulation;
using UnityEngine;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Entities
{
    /// <summary>Physical authority and polarity state; it never advances physics or spreads.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PolarityBody : MonoBehaviour
    {
        [SerializeField] private Pole initialPolarity = Pole.Neutral;
        [SerializeField] private bool canMark = true;
        [SerializeField] private bool canSpread = true;
        [SerializeField] private bool canReceive = true;

        private Rigidbody2D rigidBody;
        private Pole currentPolarity;
        private bool initialized;

        public int BodyId { get; private set; }
        public SimulationWorld World { get; private set; }
        public bool IsRegistered { get; private set; }
        public bool IsValid => IsRegistered && isActiveAndEnabled && rigidBody != null;
        public Rigidbody2D Rigidbody => rigidBody != null ? rigidBody : (rigidBody = GetComponent<Rigidbody2D>());
        public Pole InitialPolarity => initialPolarity;
        public Pole CurrentPolarity => initialized ? currentPolarity : initialPolarity;
        public bool CanMark => canMark;
        public bool CanSpread => canSpread;
        public bool CanReceive => canReceive;

        public event Action<Pole> PolarityChanged;

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            EnsureInitialized();
            if (World != null)
                World.RegisterBody(this);
        }

        private void OnDisable()
        {
            if (World != null)
                World.UnregisterBody(this);
        }

        private void OnDestroy()
        {
            if (World != null)
                World.UnregisterBody(this);
            World = null;
            PolarityChanged = null;
        }

        internal void EnsureInitialized()
        {
            if (initialized)
                return;
            rigidBody = GetComponent<Rigidbody2D>();
            currentPolarity = initialPolarity;
            initialized = true;
        }

        internal void Bind(SimulationWorld world, int bodyId)
        {
            EnsureInitialized();
            World = world;
            BodyId = bodyId;
        }

        internal void SetRegistered(bool registered) => IsRegistered = registered;

        internal void Unbind(SimulationWorld world)
        {
            if (World != world)
                return;
            IsRegistered = false;
            World = null;
        }

        internal bool SetPolarity(Pole polarity)
        {
            if (!Enum.IsDefined(typeof(Pole), polarity))
                throw new ArgumentOutOfRangeException(nameof(polarity));
            EnsureInitialized();
            if (currentPolarity == polarity)
                return false;
            currentPolarity = polarity;
            PolarityChanged?.Invoke(polarity);
            return true;
        }
    }
}
