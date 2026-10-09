using System;
using System.Collections.Generic;
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

        private static SimulationWorld physicsOwner;
        private readonly List<PolarityBody> bodies = new List<PolarityBody>();
        private IReadOnlyList<PolarityBody> readOnlyBodies;
        private SimulationMode2D previousSimulationMode;
        private bool ownsPhysics;
        private bool initialized;
        private bool stepping;

        public LevelState State { get; private set; }
        public IReadOnlyList<PolarityBody> Bodies => readOnlyBodies ?? (readOnlyBodies = bodies.AsReadOnly());
        public bool OwnsPhysics => ownsPhysics;
        public long FixedUpdateCount { get; private set; }
        public long SimulationAttemptCount { get; private set; }
        public long CompletedStepCount { get; private set; }
        public long FailedStepCount { get; private set; }

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
            if (!ValidateInitialBodies())
                return;

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
                // Future stages: requests -> spawns -> snapshot -> movement/magnetism.
                SimulationAttemptCount++;
                if (!Physics2D.Simulate(Time.fixedDeltaTime))
                {
                    FailStep("Physics2D.Simulate failed; no results were committed.");
                    return;
                }
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
