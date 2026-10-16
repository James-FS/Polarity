using Polarity.Entities;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Visual-only hit overlay; leaves body tints, symbols and shadows untouched.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class DamageFlashView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] bodySprites = new SpriteRenderer[0];
        [SerializeField, Min(0.01f)] private float duration = 0.15f;
        private static readonly int AmountId = Shader.PropertyToID("_DamageFlashAmount");
        private Health health;
        private SimulationWorld subscribedWorld;
        private MaterialPropertyBlock properties;
        private float remaining;
        public float FlashAmount { get; private set; }
        public int FlashCount { get; private set; }

        private void OnEnable()
        {
            health = GetComponent<Health>();
            if (properties == null) properties = new MaterialPropertyBlock();
            remaining = 0f;
            SetAmount(0f);
            TrySubscribe();
        }

        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            var world = health != null ? health.World : null;
            if (world == subscribedWorld) return;
            if (subscribedWorld != null) subscribedWorld.DamageCommitted -= OnDamage;
            subscribedWorld = world;
            if (subscribedWorld != null) subscribedWorld.DamageCommitted += OnDamage;
        }

        private void OnDamage(DamageResult result)
        {
            if (health == null || result.TargetId != health.ActorId || result.Amount <= 0) return;
            remaining = duration;
            FlashCount++;
            SetAmount(1f);
        }

        private void LateUpdate()
        {
            TrySubscribe();
            if (remaining <= 0f) return;
            // Match slow motion/pause, but let the final hit fade after Won/Lost freezes simulation.
            float delta = subscribedWorld != null && subscribedWorld.State != null &&
                (subscribedWorld.State.Phase == LevelPhase.Won || subscribedWorld.State.Phase == LevelPhase.Lost)
                ? Time.unscaledDeltaTime : Time.deltaTime;
            remaining = Mathf.Max(0f, remaining - delta);
            // Hold a clear red hit for half the interval, then fade back to the current body tint.
            SetAmount(Mathf.Clamp01(remaining / Mathf.Max(0.005f, duration * 0.5f)));
        }

        private void SetAmount(float amount)
        {
            FlashAmount = amount;
            if (properties == null) return;
            foreach (var sprite in bodySprites)
            {
                if (sprite == null) continue;
                sprite.GetPropertyBlock(properties);
                properties.SetFloat(AmountId, amount);
                sprite.SetPropertyBlock(properties);
            }
        }

        private void OnDisable()
        {
            if (subscribedWorld != null) subscribedWorld.DamageCommitted -= OnDamage;
            subscribedWorld = null;
            remaining = 0f;
            SetAmount(0f);
        }
    }
}
