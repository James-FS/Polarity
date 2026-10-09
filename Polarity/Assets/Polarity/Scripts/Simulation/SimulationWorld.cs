using System;
using System.Collections.Generic;
using Polarity.Config;
using Polarity.Entities;
using Polarity.Model;
using UnityEngine;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Simulation
{
    /// <summary>The single owner of 2D simulation. IDs follow the serialized room list.</summary>
    [DisallowMultipleComponent]
    public sealed class SimulationWorld : MonoBehaviour
    {
        [Tooltip("Explicit room order. Each entry receives index + 1, including disabled entries.")]
        [SerializeField] private PolarityBody[] initialBodies = Array.Empty<PolarityBody>();
        [SerializeField, Min(0)] private int initialMarkBudget = 3;
        [SerializeField] private bool startRunning = true;
        [SerializeField] private GameplayConfig gameplayConfig;
        [SerializeField] private bool drawMagnetismGizmos = true;

        private static SimulationWorld physicsOwner;
        private readonly List<PolarityBody> bodies = new List<PolarityBody>();
        private IReadOnlyList<PolarityBody> readOnlyBodies;
        private SimulationMode2D previousSimulationMode;
        private bool ownsPhysics;
        private bool initialized;
        private bool stepping;
        private GameplayConfig.Settings settings;
        private readonly List<MagneticSnapshot> magneticSnapshots = new List<MagneticSnapshot>();
        private readonly List<MagneticLink> magneticLinks = new List<MagneticLink>();
        private Vector2[] magneticForces = Array.Empty<Vector2>();
        private Vector2[] appliedMagneticForces = Array.Empty<Vector2>();
        private readonly RaycastHit2D[] wallHits = new RaycastHit2D[1];
        private ContactFilter2D wallFilter;

        public LevelState State { get; private set; }
        public IReadOnlyList<PolarityBody> Bodies => readOnlyBodies ?? (readOnlyBodies = bodies.AsReadOnly());
        public bool OwnsPhysics => ownsPhysics;
        public long FixedUpdateCount { get; private set; }
        public long SimulationAttemptCount { get; private set; }
        public long CompletedStepCount { get; private set; }
        public long FailedStepCount { get; private set; }
        public GameplayConfig Config => gameplayConfig;
        public GameplayConfig.Settings RuntimeSettings => settings;
        public int LastPairEvaluationCount { get; private set; }
        public int LastMagneticPairCount { get; private set; }
        public int LastBlockedPairCount { get; private set; }
        public int LastAccelerationClampCount { get; private set; }
        public int LastSpeedClampCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticOwner() => physicsOwner = null;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            State = State ?? new LevelState(Mathf.Max(0, initialMarkBudget), 0);
            if (physicsOwner != null && physicsOwner != this)
            {
                Debug.LogError("Only one SimulationWorld may own Physics2D.", this);
                return;
            }
            if (gameplayConfig == null)
            {
                Debug.LogError("SimulationWorld requires a GameplayConfig before starting.", this);
                return;
            }
            if (!ValidateInitialBodies())
                return;

            settings = gameplayConfig.CreateRuntimeSettings();
            wallFilter = new ContactFilter2D();
            wallFilter.SetLayerMask(settings.MagneticBlockingLayers);
            wallFilter.useTriggers = false;

            previousSimulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            physicsOwner = this;
            ownsPhysics = true;

            for (int i = 0; i < initialBodies.Length; i++)
            {
                initialBodies[i].Bind(this, i + 1);
                RegisterBody(initialBodies[i]);
            }
            // No Start-order dependency: all bodies are initialized and bound before Running.
            if (!initialized)
            {
                initialized = true;
                State.SetPhase(startRunning ? LevelPhase.Running : LevelPhase.Ready);
            }
        }

        private bool ValidateInitialBodies()
        {
            var unique = new HashSet<PolarityBody>();
            for (int i = 0; i < initialBodies.Length; i++)
            {
                var body = initialBodies[i];
                if (body == null || !unique.Add(body) || body.Rigidbody == null ||
                    (body.World != null && body.World != this))
                {
                    Debug.LogError($"Invalid or duplicate initial body at slot {i}. World remains Ready.", this);
                    return false;
                }
                if (body.gameObject.layer == LayerMask.NameToLayer("Player"))
                {
                    Debug.LogError("Players must not have PolarityBody or enter the polarity registry.", body);
                    return false;
                }
            }
            return true;
        }

        internal void RegisterBody(PolarityBody body)
        {
            if (!ownsPhysics || body == null || body.World != this || !body.isActiveAndEnabled || bodies.Contains(body))
                return;
            if (body.BodyId <= 0 || bodies.Exists(other => other.BodyId == body.BodyId))
            {
                Debug.LogError("Duplicate or unassigned BodyId; registration rejected.", body);
                return;
            }
            bodies.Add(body);
            bodies.Sort((a, b) => a.BodyId.CompareTo(b.BodyId));
            body.SetRegistered(true);
            UpdateEnemyCount();
        }

        internal void UnregisterBody(PolarityBody body)
        {
            if (body == null || body.World != this)
                return;
            bodies.Remove(body);
            body.SetRegistered(false);
            UpdateEnemyCount();
        }

        private void UpdateEnemyCount()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            State?.SetActiveEnemyCount(bodies.FindAll(body => body != null && body.IsValid && body.gameObject.layer == enemyLayer).Count);
        }

        public void SetPhase(LevelPhase phase)
        {
            if (!ownsPhysics || State == null)
                return;
            State.SetPhase(phase);
        }

        // Rule-layer entry only. Player request validation and propagation arrive in later steps.
        internal bool ApplyPolarity(PolarityBody body, Pole polarity)
        {
            return ownsPhysics && body != null && body.World == this && body.IsValid && body.SetPolarity(polarity);
        }

        private void FixedUpdate()
        {
            FixedUpdateCount++;
            if (!ownsPhysics || State == null || State.Phase != LevelPhase.Running || stepping)
                return;
            if (physicsOwner != this || Physics2D.simulationMode != SimulationMode2D.Script)
            {
                FailStep("Physics2D ownership or simulation mode changed outside SimulationWorld.");
                return;
            }
            stepping = true;
            try
            {
                // Future requests/spawns precede this snapshot; movement joins this force stage.
                CaptureMagneticSnapshot();
                CalculateMagneticForces();
                ApplyMagneticForces();
                LastSpeedClampCount = 0;
                ClampBodySpeeds();
                SimulationAttemptCount++;
                if (!Physics2D.Simulate(Time.fixedDeltaTime))
                {
                    FailStep("Physics2D.Simulate failed; no results were committed.");
                    return;
                }
                ClampBodySpeeds();
                // Future stages: contacts -> damage -> propagation -> removals -> outcome/events.
                CompletedStepCount++;
            }
            catch (Exception exception)
            {
                FailStep($"Physics step failed; no results were committed: {exception.Message}");
            }
            finally
            {
                stepping = false;
            }
        }

        private void CaptureMagneticSnapshot()
        {
            magneticSnapshots.Clear();
            foreach (var body in bodies)
            {
                if (body == null || !body.IsValid)
                    continue;
                Rigidbody2D rigidBody = body.Rigidbody;
                if (!rigidBody.simulated || rigidBody.bodyType != RigidbodyType2D.Dynamic)
                    continue;
                magneticSnapshots.Add(new MagneticSnapshot(body, rigidBody.position, body.CurrentPolarity));
            }
            int count = magneticSnapshots.Count;
            if (magneticForces.Length < count)
            {
                magneticForces = new Vector2[count];
                appliedMagneticForces = new Vector2[count];
            }
            Array.Clear(magneticForces, 0, count);
            Array.Clear(appliedMagneticForces, 0, count);
        }

        private void CalculateMagneticForces()
        {
            LastPairEvaluationCount = LastMagneticPairCount = LastBlockedPairCount = 0;
            magneticLinks.Clear();
            float radiusSquared = settings.MagneticRadius * settings.MagneticRadius;
            for (int i = 0; i < magneticSnapshots.Count; i++)
            for (int j = i + 1; j < magneticSnapshots.Count; j++)
            {
                LastPairEvaluationCount++;
                MagneticSnapshot a = magneticSnapshots[i], b = magneticSnapshots[j];
                if (a.Polarity == Pole.Neutral || b.Polarity == Pole.Neutral || settings.MagneticStrength <= 0f)
                    continue;
                Vector2 offset = b.Position - a.Position;
                float distanceSquared = offset.sqrMagnitude;
                if (!IsFinite(offset) || distanceSquared >= radiusSquared)
                    continue;
                if (Physics2D.Linecast(a.Position, b.Position, wallFilter, wallHits) > 0)
                {
                    LastBlockedPairCount++;
                    continue;
                }
                bool coincident = distanceSquared <= 0.00000001f;
                float distance = Mathf.Sqrt(distanceSquared);
                Vector2 direction = coincident ? StableSeparationAxis(a.Body.BodyId, b.Body.BodyId) : offset / distance;
                // At coincident centers either charge relationship uses bounded separation.
                if (coincident || a.Polarity == b.Polarity)
                    direction = -direction;
                float magnitude = settings.MagneticStrength * (1f - distance / settings.MagneticRadius);
                Vector2 force = direction * magnitude;
                magneticForces[i] += force;
                magneticForces[j] -= force;
                LastMagneticPairCount++;
                magneticLinks.Add(new MagneticLink(a.Position, b.Position));
            }
        }

        private static Vector2 StableSeparationAxis(int a, int b)
        {
            uint hash = unchecked((uint)a * 73856093u ^ (uint)b * 19349663u);
            float angle = (hash % 8) * (Mathf.PI / 4f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void ApplyMagneticForces()
        {
            LastAccelerationClampCount = 0;
            for (int i = 0; i < magneticSnapshots.Count; i++)
            {
                Rigidbody2D rigidBody = magneticSnapshots[i].Body.Rigidbody;
                Vector2 force = magneticForces[i];
                float limit = rigidBody.mass * settings.MaxMagneticAcceleration;
                Vector2 applied = Vector2.ClampMagnitude(force, limit);
                if ((applied - force).sqrMagnitude > 0.000001f)
                    LastAccelerationClampCount++;
                appliedMagneticForces[i] = applied;
                if (applied.sqrMagnitude > 0f)
                    // Force mode integrates time and mass inside the engine; no extra deltaTime.
                    rigidBody.AddForce(applied, ForceMode2D.Force);
            }
        }

        private void ClampBodySpeeds()
        {
            foreach (var snapshot in magneticSnapshots)
            {
                Rigidbody2D rigidBody = snapshot.Body.Rigidbody;
                Vector2 velocity = rigidBody.velocity;
                Vector2 safeVelocity = IsFinite(velocity) ? Vector2.ClampMagnitude(velocity, settings.MaxBodySpeed) : Vector2.zero;
                if (safeVelocity != velocity)
                {
                    rigidBody.velocity = safeVelocity;
                    LastSpeedClampCount++;
                }
            }
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);

        /// <summary>Uncapped summed force for diagnostics; individual pairs are equal/opposite.</summary>
        public bool TryGetLastMagneticForce(PolarityBody body, out Vector2 force)
            => TryGetLastForce(body, magneticForces, out force);

        public bool TryGetLastAppliedMagneticForce(PolarityBody body, out Vector2 force)
            => TryGetLastForce(body, appliedMagneticForces, out force);

        private bool TryGetLastForce(PolarityBody body, Vector2[] forces, out Vector2 force)
        {
            for (int i = 0; i < magneticSnapshots.Count; i++)
            {
                if (magneticSnapshots[i].Body != body)
                    continue;
                force = forces[i];
                return true;
            }
            force = Vector2.zero;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawMagnetismGizmos || gameplayConfig == null)
                return;
            float radius = Application.isPlaying ? settings.MagneticRadius : gameplayConfig.CreateRuntimeSettings().MagneticRadius;
            if (!Application.isPlaying)
            {
                foreach (var body in initialBodies)
                    if (body != null)
                        DrawRange(body.Rigidbody.position, radius, body.InitialPolarity);
                return;
            }
            foreach (var link in magneticLinks)
            {
                Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.4f);
                Gizmos.DrawLine(link.A, link.B);
            }
            for (int i = 0; i < magneticSnapshots.Count; i++)
            {
                var snapshot = magneticSnapshots[i];
                DrawRange(snapshot.Position, radius, snapshot.Polarity);
                Vector2 arrow = Vector2.ClampMagnitude(appliedMagneticForces[i] * 0.06f, 3f);
                Gizmos.DrawLine(snapshot.Position, snapshot.Position + arrow);
                if (arrow.sqrMagnitude <= 0.000001f)
                    continue;
                Vector2 tip = snapshot.Position + arrow;
                Vector2 back = -arrow.normalized * 0.15f;
                Vector2 side = new Vector2(-back.y, back.x) * 0.5f;
                Gizmos.DrawLine(tip, tip + back + side);
                Gizmos.DrawLine(tip, tip + back - side);
            }
        }

        private static void DrawRange(Vector2 center, float radius, Pole polarity)
        {
            Gizmos.color = polarity == Pole.Positive ? new Color(1f, 0.45f, 0.12f, 0.65f) :
                polarity == Pole.Negative ? new Color(0.18f, 0.55f, 1f, 0.65f) : new Color(0.6f, 0.6f, 0.6f, 0.3f);
            const int segments = 48;
            Vector2 previous = center + Vector2.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private readonly struct MagneticSnapshot
        {
            public readonly PolarityBody Body;
            public readonly Vector2 Position;
            public readonly Pole Polarity;

            public MagneticSnapshot(PolarityBody body, Vector2 position, Pole polarity)
            {
                Body = body;
                Position = position;
                Polarity = polarity;
            }
        }

        private readonly struct MagneticLink
        {
            public readonly Vector2 A, B;
            public MagneticLink(Vector2 a, Vector2 b) { A = a; B = b; }
        }

        private void FailStep(string message)
        {
            FailedStepCount++;
            State.SetPhase(LevelPhase.Paused);
            Debug.LogError(message, this);
        }

        private void OnDisable() => ReleasePhysics();
        private void OnDestroy() => ReleasePhysics();

        private void ReleasePhysics()
        {
            if (!ownsPhysics)
                return;
            foreach (var body in initialBodies)
                if (body != null)
                    body.Unbind(this);
            bodies.Clear();
            magneticSnapshots.Clear();
            magneticLinks.Clear();
            State?.SetActiveEnemyCount(0);
            if (physicsOwner == this)
            {
                Physics2D.simulationMode = previousSimulationMode;
                physicsOwner = null;
            }
            ownsPhysics = false;
        }
    }
}
