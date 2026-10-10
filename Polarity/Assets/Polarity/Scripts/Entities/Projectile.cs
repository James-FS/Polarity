using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Entities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PolarityBody), typeof(CircleCollider2D), typeof(ContactRecorder))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 6f;
        public PolarityBody Body { get; private set; }
        public bool Consumed { get; private set; }
        public double ExpiresAt { get; private set; }
        public Vector2 StepStartPosition { get; private set; }
        public bool IsActive => !Consumed && Body != null && Body.IsValid && Body.Rigidbody.simulated;
        private void Awake() => Body = GetComponent<PolarityBody>();
        internal void Initialize(SimulationWorld world, int id, Vector2 position, Vector2 velocity)
        {
            Body = GetComponent<PolarityBody>();
            Consumed = false;
            Body.InitializeProjectile();
            Body.Bind(world, id);
            Body.Rigidbody.position = position;
            Body.Rigidbody.velocity = velocity;
            Body.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
            Body.Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.Rigidbody.simulated = true;
            ExpiresAt = world.SimulationTime + Mathf.Max(0.1f, lifetime);
            StepStartPosition = position;
        }
        internal void BeginStep() => StepStartPosition = Body.Rigidbody.position;
        internal void Consume() => Consumed = true;
    }
}
