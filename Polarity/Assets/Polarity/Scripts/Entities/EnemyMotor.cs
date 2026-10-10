using UnityEngine;

namespace Polarity.Entities
{
    /// <summary>Provides bounded steering only when called by SimulationWorld. No private simulation loop.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PolarityBody), typeof(CircleCollider2D))]
    public sealed class EnemyMotor : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float chaseSpeed = 3.2f;
        [SerializeField, Min(0.01f)] private float maxDriveAcceleration = 6f;
        [SerializeField, Min(0.01f)] private float steeringResponse = 2f;
        [SerializeField, Min(0f)] private float lookAhead = 1.5f;
        [SerializeField, Min(0f)] private float obstacleSkin = 0.06f;
        private CircleCollider2D footprint;
        private ContactFilter2D obstacleFilter;
        private bool filterReady;
        private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[1];

        public float ChaseSpeed => chaseSpeed;
        public float MaxDriveAcceleration => maxDriveAcceleration;
        public Vector2 LastDriveAcceleration { get; private set; }
        public Vector2 LastSteeringDirection { get; private set; }
        public bool LastAvoidingObstacle { get; private set; }
        public long DriveEvaluationCount { get; private set; }

        internal Vector2 CalculateDrive(PolarityBody body, Vector2 targetPosition)
        {
            DriveEvaluationCount++;
            LastDriveAcceleration = LastSteeringDirection = Vector2.zero;
            LastAvoidingObstacle = false;
            if (!isActiveAndEnabled || body == null || !body.IsValid || !IsFinite(targetPosition))
                return Vector2.zero;
            Rigidbody2D rigidBody = body.Rigidbody;
            Vector2 offset = targetPosition - rigidBody.position;
            if (!IsFinite(offset) || !IsFinite(rigidBody.velocity)) return Vector2.zero;
            float distance = offset.magnitude;
            Vector2 direction = distance > 0.0001f ? offset / distance : Vector2.zero;
            if (direction != Vector2.zero && lookAhead > 0f)
                direction = SteerAroundObstacle(body, direction, Mathf.Min(lookAhead, distance));
            LastSteeringDirection = direction;
            // Arrival eases the drive, but does not bypass physical contact with the player.
            Vector2 desired = direction * chaseSpeed * Mathf.Clamp01(distance / 0.8f);
            // Compensate the body's existing drag for walking; any correction is acceleration-limited.
            // External sideways motion and high-speed throws decay gradually, never get reset to walking speed.
            Vector2 acceleration = desired * rigidBody.drag + (desired - rigidBody.velocity) * steeringResponse;
            LastDriveAcceleration = Vector2.ClampMagnitude(acceleration, maxDriveAcceleration);
            return LastDriveAcceleration;
        }

        private Vector2 SteerAroundObstacle(PolarityBody body, Vector2 direction, float distance)
        {
            if (!filterReady)
            {
                obstacleFilter = new ContactFilter2D();
                obstacleFilter.SetLayerMask((1 << LayerMask.NameToLayer("Wall")) | (1 << LayerMask.NameToLayer("Crate")));
                obstacleFilter.useTriggers = false;
                filterReady = true;
            }
            if (footprint == null) footprint = GetComponent<CircleCollider2D>();
            float radius = footprint.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            Vector2 origin = body.Rigidbody.position + (Vector2)transform.TransformVector(footprint.offset);
            if (Physics2D.CircleCast(origin, radius + obstacleSkin, direction, obstacleFilter, obstacleHits, distance) == 0)
                return direction;
            Vector2 normal = obstacleHits[0].normal;
            if (normal.sqrMagnitude < 0.01f) normal = -direction;
            // Stable side avoids switching left/right every frame when facing a flat obstacle.
            Vector2 tangent = new Vector2(-normal.y, normal.x) * ((body.BodyId & 1) == 0 ? 1f : -1f);
            if (Mathf.Abs(Vector2.Dot(tangent, direction)) > 0.25f && Vector2.Dot(tangent, direction) < 0f)
                tangent = -tangent;
            LastAvoidingObstacle = true;
            return (tangent + normal * 0.25f).normalized;
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
    }
}
