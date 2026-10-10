using UnityEngine;
namespace Polarity.Entities
{
    public enum TurretPhase { Cooling, Aiming, Locked }

    /// <summary>Only the World advances this simulation clock. Views read it.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Turret : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Min(0f)] private float initialDelay = 1.5f;
        [SerializeField, Min(0.02f)] private float shotInterval = 3.5f;
        [SerializeField, Min(0.02f)] private float warningDuration = 1f;
        [SerializeField, Min(0f)] private float lockDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 5f;
        [SerializeField, Min(0.01f)] private float muzzleDistance = 1.1f;
        private float remaining;
        public TurretPhase Phase { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public int ShotsFired { get; private set; }
        public int BlockedShots { get; private set; }
        public float WarningProgress => Phase == TurretPhase.Cooling ? 0f : Mathf.Clamp01(1f - remaining / Mathf.Max(0.02f, warningDuration));
        public Projectile ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public Vector2 MuzzlePosition => (Vector2)transform.position + AimDirection * muzzleDistance;
        internal void Initialize()
        {
            remaining = Mathf.Max(0f, initialDelay);
            Phase = TurretPhase.Cooling;
            ShotsFired = BlockedShots = 0;
        }
        internal bool Tick(float step, Vector2 target)
        {
            if (!isActiveAndEnabled) return false;
            remaining -= step;
            if (Phase == TurretPhase.Cooling)
            {
                if (remaining > 0f) return false;
                remaining = Mathf.Max(step, warningDuration);
                Phase = TurretPhase.Aiming;
            }
            if (Phase == TurretPhase.Aiming)
            {
                Vector2 direction = target - (Vector2)transform.position;
                if (direction.sqrMagnitude > 0.000001f) AimDirection = direction.normalized;
                if (remaining > Mathf.Clamp(lockDuration, 0f, warningDuration)) return false;
                Phase = TurretPhase.Locked;
            }
            if (remaining > 0f) return false;
            Phase = TurretPhase.Cooling;
            remaining = Mathf.Max(step, shotInterval - warningDuration);
            return true;
        }
        internal void ReportShot(bool spawned) { if (spawned) ShotsFired++; else BlockedShots++; }
    }
}
