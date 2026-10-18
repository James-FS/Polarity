using System;
using System.Collections.Generic;
using Polarity.Config;
using Polarity.Controllers;
using Polarity.Entities;
using Polarity.Model;
using UnityEngine;
using Pole = Polarity.Model.Polarity;
using MagneticSnapshot = Polarity.Simulation.MagnetismSystem.BodySample;
using MagneticLink = Polarity.Simulation.MagnetismSystem.Link;

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

        [SerializeField] private PlayerController player;
        private readonly Queue<MarkRequest> markRequests = new Queue<MarkRequest>();
        private long nextMarkSequence;
        [SerializeField] private Turret[] turrets = Array.Empty<Turret>();
        private readonly Dictionary<Rigidbody2D, Projectile> projectiles = new Dictionary<Rigidbody2D, Projectile>();
        private readonly ProjectileCollection projectileContacts = new ProjectileCollection();
        private readonly List<Projectile> consumedProjectiles = new List<Projectile>();
        private readonly Collider2D[] spawnOverlaps = new Collider2D[1];
        private int nextDynamicId;
        public int ActiveProjectileCount => projectiles.Count;
        public int SpawnedProjectileCount { get; private set; }
        public int ConsumedProjectileCount { get; private set; }
        public int ProjectileContactBufferCapacity => projectileContacts.BufferCapacity;
        public IReadOnlyList<Turret> Turrets => Array.AsReadOnly(turrets);
        private int enemyLayer, crateLayer;
        public PlayerController Player => player;
        public int PendingMarkCount => markRequests.Count;
        public long ProcessedMarkCount { get; private set; }
        public MarkResult? LastMarkResult { get; private set; }
        public event Action<MarkResult> MarkResolved;

        private static SimulationWorld physicsOwner;
        private readonly List<PolarityBody> bodies = new List<PolarityBody>();
        private readonly Dictionary<PolarityBody, EnemyMotor> enemyMotors = new Dictionary<PolarityBody, EnemyMotor>();
        private readonly Dictionary<PolarityBody, EnemyNavigator> enemyNavigators = new Dictionary<PolarityBody, EnemyNavigator>();
        public IReadOnlyDictionary<PolarityBody, EnemyNavigator> EnemyNavigators => enemyNavigators;
        private int navigationCursor;
        public int LastNavigationExpansions { get; private set; }
        public int LastEnemyDriveCount { get; private set; }
        private readonly Dictionary<Rigidbody2D, PolarityBody> bodiesByRigidbody = new Dictionary<Rigidbody2D, PolarityBody>();
        private readonly ContactCollection contacts = new ContactCollection();
        private readonly DamageCollection damageContacts = new DamageCollection();
        private readonly DamageSystem damage = new DamageSystem();
        private readonly List<SpikeHazard> touchedSpikes = new List<SpikeHazard>();
        private readonly Dictionary<Rigidbody2D,Health> healthByRigidbody = new Dictionary<Rigidbody2D,Health>();
        private readonly List<DamageContact> damagePlan = new List<DamageContact>();
        private readonly List<DamageResult> damageResults = new List<DamageResult>();
        private IReadOnlyList<DamageResult> readOnlyDamageResults;
        private LevelController levelController;
        private NavigationGridAuthoring navigation;
        public NavigationGridAuthoring Navigation => navigation;
        private float previousTimeScale;
        private bool restarting;
        public bool SlowMotionRequested { get; private set; }
        public float CurrentTimeScale { get; private set; } = 1f;
        public event Action<float> TimeScaleChanged;
        public Health PlayerHealth { get; private set; }
        public LevelController Level => levelController;
        public double SimulationTime { get; private set; }
        public IReadOnlyList<DamageContact> LastDamageContacts => damageContacts.Contacts;
        public IReadOnlyList<DamageResult> LastDamageResults => readOnlyDamageResults ?? (readOnlyDamageResults = damageResults.AsReadOnly());
        public int DamageBufferCapacity => damageContacts.BufferCapacity;
        public int DamageBufferExpansionCount => damageContacts.BufferExpansionCount;
        public event Action<DamageResult> DamageCommitted;
        private readonly PropagationSystem propagation = new PropagationSystem();
        private readonly List<BodySnapshot> propagationSnapshot = new List<BodySnapshot>();
        private readonly List<PropagationResult> propagationPlan = new List<PropagationResult>();
        private readonly List<PropagationResult> propagationResults = new List<PropagationResult>();
        private IReadOnlyList<PropagationResult> readOnlyPropagationResults;
        private bool collectingContacts;
        public IReadOnlyList<BodyContactPair> LastContactPairs => contacts.Pairs;
        public IReadOnlyList<PropagationResult> LastPropagationResults => readOnlyPropagationResults ??
            (readOnlyPropagationResults = propagationResults.AsReadOnly());
        public PropagationResult? LastPropagation { get; private set; }
        public int LastPropagationCandidateCount => propagation.CandidateCount;
        public IReadOnlyList<PropagationCandidate> LastPropagationCandidates => propagation.Candidates;
        public int ContactBufferCapacity => contacts.BufferCapacity;
        public int ContactBufferExpansionCount => contacts.BufferExpansionCount;
        public int LastContactCallbackCount => contacts.CallbackBatchCount;
        public int LastPersistentContactPointCount => contacts.PersistentPointCount;
        public int LastColliderContactPairCount => contacts.UniqueColliderPairCount;
        public event Action<PropagationResult> PropagationCommitted;
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
        private readonly MagnetismSystem magnetism = new MagnetismSystem();
        public int PlayerDamageCount { get; private set; }

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

        internal void ConfigureRoom(LevelRoom room, LevelDefinition definition)
        {
            if (ownsPhysics || State!=null) throw new InvalidOperationException("Configure a room before World initialization.");
            player=room.Player; initialBodies=room.CopyBodies(); turrets=room.CopyTurrets();
            gameplayConfig=definition.GameplayConfig; initialMarkBudget=definition.MarkBudget;
            navigation=room.GetComponentInChildren<NavigationGridAuthoring>();
        }

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
            if (player == null || (PlayerHealth = player.GetComponent<Health>()) == null ||
                (levelController = GetComponent<LevelController>()) == null)
            { Debug.LogError("World requires player Health and LevelController before starting.",this); return; }
            if (!ValidateInitialBodies() || !ValidateTurrets())
                return;

            settings = gameplayConfig.CreateRuntimeSettings();
            enemyLayer = LayerMask.NameToLayer("Enemy");
            crateLayer = LayerMask.NameToLayer("Crate");
            damageContacts.ConfigureLayers();
            State.PhaseChanged += OnPhaseChanged;

            previousTimeScale = Time.timeScale;
            SlowMotionRequested = false;
            ApplyTimeScale();
            previousSimulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            physicsOwner = this;
            ownsPhysics = true;

            PlayerHealth.Bind(this,0);
            healthByRigidbody[player.Rigidbody]=PlayerHealth;
            levelController.Bind(this);
            for (int i = 0; i < initialBodies.Length; i++)
            {
                initialBodies[i].Bind(this, i + 1);
                RegisterBody(initialBodies[i]);
            }
            nextDynamicId = initialBodies.Length + 1;
            foreach (var turret in turrets) if(turret!=null) turret.Initialize();
            navigation?.InitializeRuntime(this);
            // No Start-order dependency: all bodies are initialized and bound before Running.
            if (!initialized)
            {
                initialized = true;
                bool introduction = levelController.BeginInitialIntroduction();
                State.SetPhase(startRunning && !introduction ? LevelPhase.Running : LevelPhase.Ready);
                ApplyTimeScale();
            }
        }

        private bool ValidateTurrets()
        {
            var unique = new HashSet<Turret>();
            foreach (var turret in turrets)
            {
                if (turret == null || !unique.Add(turret) || turret.ProjectilePrefab == null ||
                    turret.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static ||
                    turret.GetComponent<Health>() != null || turret.GetComponent<PolarityBody>() != null)
                {
                    Debug.LogError("Turrets require unique bindings, a projectile prefab and an indestructible static footprint.", this);
                    return false;
                }
            }
            return true;
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
                if (body.gameObject.layer == LayerMask.NameToLayer("Enemy") && body.GetComponent<Health>() == null)
                { Debug.LogError("Every enemy requires Health before registration.",body); return false; }
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
            if(body.TryGetComponent<Projectile>(out var projectile)) projectiles[body.Rigidbody]=projectile;
            bodies.Add(body);
            bodies.Sort((a, b) => a.BodyId.CompareTo(b.BodyId));
            body.SetRegistered(true);
            bodiesByRigidbody[body.Rigidbody] = body;
            if(body.TryGetComponent<Health>(out var health)) { health.Bind(this,body.BodyId); healthByRigidbody[body.Rigidbody]=health; }
            if (body.gameObject.layer == enemyLayer && body.TryGetComponent<EnemyMotor>(out var motor))
            {
                enemyMotors[body] = motor;
                if (navigation != null) enemyNavigators[body] = new EnemyNavigator();
            }
            UpdateEnemyCount();
        }

        internal void UnregisterBody(PolarityBody body)
        {
            if (body == null || body.World != this)
                return;
            projectiles.Remove(body.Rigidbody);
            bodies.Remove(body);
            enemyMotors.Remove(body);
            if (enemyNavigators.TryGetValue(body, out var navigator)) navigator.Clear();
            enemyNavigators.Remove(body);
            bodiesByRigidbody.Remove(body.Rigidbody);
            healthByRigidbody.Remove(body.Rigidbody);
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
            if (!ownsPhysics || restarting || State == null || State.Phase == LevelPhase.Won || State.Phase == LevelPhase.Lost)
                return;
            State.SetPhase(phase);
        }

        public void SetSlowMotion(bool held)
        {
            if (!ownsPhysics || restarting) return;
            SlowMotionRequested = held;
            ApplyTimeScale();
        }
        private void ApplyTimeScale()
        {
            float value = !restarting && (State?.Phase == LevelPhase.Paused || State?.Phase == LevelPhase.Ready) ? 0f :
                !restarting && State?.Phase == LevelPhase.Running && SlowMotionRequested ? settings.SlowMotionScale : 1f;
            bool changed = !Mathf.Approximately(CurrentTimeScale,value);
            CurrentTimeScale = value;
            Time.timeScale = value;
            if (changed) TimeScaleChanged?.Invoke(value);
        }
        private void OnPhaseChanged(LevelPhase phase)
        {
            ApplyTimeScale();
            if (phase != LevelPhase.Running)
            {
                markRequests.Clear();
                if (!stepping) ClearContactState();
                player?.ClearIntent();
            }
        }

        // GM changes runtime facts here; it never creates another simulation entry.
        internal bool ApplyGM(Polarity.Debugging.GMCommand command, int value)
        {
            if (!Polarity.Debugging.GMManager.IsAvailable || !ownsPhysics || stepping || restarting ||
                levelController.IsRestarting || State == null || PlayerHealth == null) return false;
            switch (command)
            {
                case Polarity.Debugging.GMCommand.Invincible:
                    PlayerHealth.IsInvincible = value != 0;
                    return true;
                case Polarity.Debugging.GMCommand.UnlimitedMarks:
                    State.SetUnlimitedMarks(value != 0);
                    return true;
                case Polarity.Debugging.GMCommand.AddHealth:
                    return (State.Phase == LevelPhase.Running || State.Phase == LevelPhase.Paused) && PlayerHealth.AddRuntimeHealth(value);
                case Polarity.Debugging.GMCommand.AddMarks:
                    return (State.Phase == LevelPhase.Running || State.Phase == LevelPhase.Paused) && State.AddRuntimeMarks(value);
                case Polarity.Debugging.GMCommand.SetHealth:
                    return (State.Phase == LevelPhase.Running || State.Phase == LevelPhase.Paused) && PlayerHealth.SetRuntimeHealth(value);
                case Polarity.Debugging.GMCommand.SetRemainingMarks:
                    return (State.Phase == LevelPhase.Running || State.Phase == LevelPhase.Paused) && State.SetRuntimeRemainingMarks(value);
                default: return false;
            }
        }

        public long SubmitMark(int targetId, Pole polarity)
        {
            var request = new MarkRequest(targetId, polarity, ++nextMarkSequence);
            if (!ownsPhysics || State == null || State.Phase != LevelPhase.Running)
            {
                PublishMarkResult(request, MarkOutcome.NotRunning);
                return request.Sequence;
            }
            markRequests.Enqueue(request);
            return request.Sequence;
        }

        private void ConsumeMarkRequests()
        {
            while (markRequests.Count > 0)
            {
                var request = markRequests.Dequeue();
                var outcome = ValidateMark(request, out PolarityBody target);
                if (outcome == MarkOutcome.Applied)
                {
                    // Commit both facts before notifying views. Same-color requests never reach here.
                    State.TrySpendMark();
                    ApplyPolarity(target, request.Polarity);
                }
                PublishMarkResult(request, outcome);
            }
        }

        private MarkOutcome ValidateMark(MarkRequest request, out PolarityBody target)
        {
            target = bodies.Find(body => body != null && body.BodyId == request.TargetId);
            if (State.Phase != LevelPhase.Running) return MarkOutcome.NotRunning;
            if (target == null || !target.IsValid || !target.Rigidbody.simulated) return MarkOutcome.InvalidTarget;
            if (!target.CanMark || (target.gameObject.layer != enemyLayer && target.gameObject.layer != crateLayer))
                return MarkOutcome.NotMarkable;
            if (request.Polarity != Pole.Positive && request.Polarity != Pole.Negative) return MarkOutcome.InvalidPolarity;
            if (player == null || !player.isActiveAndEnabled || !player.Rigidbody.simulated) return MarkOutcome.PlayerUnavailable;
            if (!IsFinite(target.Rigidbody.position)) return MarkOutcome.InvalidTarget;
            if (target.CurrentPolarity == request.Polarity) return MarkOutcome.Unchanged;
            return State.UnlimitedMarks || State.RemainingMarks > 0 ? MarkOutcome.Applied : MarkOutcome.BudgetEmpty;
        }

        private void PublishMarkResult(MarkRequest request, MarkOutcome outcome)
        {
            var result = new MarkResult(request, outcome, State?.RemainingMarks ?? 0);
            LastMarkResult = result;
            ProcessedMarkCount++;
            MarkResolved?.Invoke(result);
        }

        private void ApplyPlayerMovement(float step)
        {
            if (player == null || !player.isActiveAndEnabled || !player.Rigidbody.simulated)
                return;
            Rigidbody2D rigidBody = player.Rigidbody;
            Vector2 velocity = IsFinite(rigidBody.velocity) ? rigidBody.velocity : Vector2.zero;
            Vector2 desired = player.MoveIntent * Mathf.Min(player.MoveSpeed, settings.MaxBodySpeed);
            Vector2 acceleration = Vector2.ClampMagnitude((desired - velocity) / step, player.MoveAcceleration);
            rigidBody.AddForce(acceleration * rigidBody.mass, ForceMode2D.Force);
        }

        private void ApplyEnemyMovement()
        {
            LastEnemyDriveCount = 0;
            LastNavigationExpansions = 0;
            if (player == null || !player.isActiveAndEnabled || !player.Rigidbody.simulated) return;
            Vector2 targetPosition = player.Rigidbody.position;
            int budget = navigation != null ? navigation.SearchBudgetPerStep : 0;
            int initialBudget = budget;
            for (int offset = 0; offset < magneticSnapshots.Count; offset++)
            {
                var snapshot = magneticSnapshots[(navigationCursor + offset) % magneticSnapshots.Count];
                if (!enemyMotors.TryGetValue(snapshot.Body, out var motor) || motor == null || !motor.isActiveAndEnabled)
                    continue;
                Vector2 acceleration;
                if (navigation?.Sampler != null && enemyNavigators.TryGetValue(snapshot.Body, out var navigator))
                {
                    Vector2 desired = navigator.Tick(navigation, snapshot.Body.Rigidbody.position, snapshot.Body.Rigidbody.velocity,
                        targetPosition, motor.ChaseSpeed, motor.MaxDriveAcceleration, SimulationTime, Time.fixedDeltaTime, ref budget);
                    acceleration = motor.CalculateNavigationDrive(snapshot.Body, desired, navigator.State != EnemyNavigationState.Following);
                }
                else acceleration = motor.CalculateDrive(snapshot.Body, targetPosition);
                snapshot.Body.Rigidbody.AddForce(acceleration * snapshot.Body.Rigidbody.mass, ForceMode2D.Force);
                LastEnemyDriveCount++;
            }
            LastNavigationExpansions = initialBudget - budget;
            navigationCursor = magneticSnapshots.Count == 0 ? 0 : (navigationCursor + 1) % magneticSnapshots.Count;
        }

        // Internal rule-layer entry shared with future propagation.
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
                ClearStepContacts();
                ConsumeMarkRequests();
                TickTurretsAndRegisterSpawns();
                CaptureMagneticSnapshot();
                navigation?.RefreshRuntime();
                ApplyPlayerMovement(Time.fixedDeltaTime);
                ApplyEnemyMovement();
                CalculateMagneticForces();
                ApplyMagneticForces();
                LastSpeedClampCount = 0;
                ClampBodySpeeds();
                SimulationAttemptCount++;
                bool simulated;
                collectingContacts = true;
                try { simulated = Physics2D.Simulate(Time.fixedDeltaTime); }
                finally { collectingContacts = false; }
                if (!simulated)
                {
                    FailStep("Physics2D.Simulate failed; no results were committed.");
                    return;
                }
                ClampBodySpeeds();
                contacts.CapturePersistent(bodies, bodiesByRigidbody);
                projectileContacts.CapturePersistent(projectiles,healthByRigidbody);
                foreach(var hit in projectileContacts.Hits)
                    if(hit.Target!=null) damageContacts.CaptureProjectile(hit.Projectile,hit.Target,hit.Point);
                touchedSpikes.RemoveAll(spike=>spike==null);
                damageContacts.CapturePersistent(healthByRigidbody,bodiesByRigidbody,touchedSpikes);
                ResolveDamage();
                ConsumeProjectiles();
                propagation.BuildPlan(CompletedStepCount + 1, propagationSnapshot, contacts.Pairs, propagationPlan);
                CommitPropagation();
                FlushDeaths();
                FlushProjectiles();
                CompletedStepCount++;
                SimulationTime += Time.fixedDeltaTime;
                levelController.ResolveOutcome();
                PublishDamage();
                PublishPropagation();
                levelController.PublishOutcome();
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

        internal void RecordCollision(Collision2D collision)
        {
            if (!collectingContacts || !ownsPhysics) return;
            contacts.CaptureCollision(collision,bodiesByRigidbody);
            projectileContacts.CaptureCollision(collision,projectiles,healthByRigidbody);
            damageContacts.CaptureCollision(collision,healthByRigidbody,bodiesByRigidbody);
        }

        internal void RecordSpike(SpikeHazard spike, Health target)
        { if (!collectingContacts || !ownsPhysics) return;
            if(!touchedSpikes.Contains(spike)) touchedSpikes.Add(spike);
            damageContacts.CaptureSpike(spike,target); }

        private void TickTurretsAndRegisterSpawns()
        {
            foreach(var turret in turrets)
                if(turret!=null && turret.Tick(Time.fixedDeltaTime,player.Rigidbody.position))
                    turret.ReportShot(SpawnProjectile(turret.ProjectilePrefab,turret.MuzzlePosition,turret.AimDirection,turret.ProjectileSpeed)!=null);
            foreach(var projectile in projectiles.Values) if(projectile.IsActive) projectile.BeginStep();
        }

        internal Projectile SpawnProjectile(Projectile prefab,Vector2 position,Vector2 direction,float speed)
        {
            if(!ownsPhysics || State.Phase!=LevelPhase.Running || prefab==null || !IsFinite(position) || !IsFinite(direction) ||
                direction.sqrMagnitude<0.000001f || float.IsNaN(speed) || float.IsInfinity(speed)) return null;
            var collider=prefab.GetComponent<CircleCollider2D>();
            if(collider==null) return null;
            float radius=collider.radius*Mathf.Max(Mathf.Abs(prefab.transform.lossyScale.x),Mathf.Abs(prefab.transform.lossyScale.y));
            var filter=new ContactFilter2D {useTriggers=false};
            filter.SetLayerMask((1<<LayerMask.NameToLayer("Player"))|(1<<enemyLayer)|(1<<crateLayer)|(1<<LayerMask.NameToLayer("Wall"))|(1<<LayerMask.NameToLayer("Turret")));
            // Reject blocked muzzles rather than spawn overlapping or teleport through cover.
            if(Physics2D.OverlapCircle(position,radius+0.03f,filter,spawnOverlaps)>0) return null;
            var shot=Instantiate(prefab,position,Quaternion.identity);
            shot.Initialize(this,nextDynamicId++,position,direction.normalized*Mathf.Clamp(speed,0f,settings.MaxBodySpeed));
            RegisterBody(shot.Body);
            SpawnedProjectileCount++;
            return shot;
        }

        private void ConsumeProjectiles()
        {
            foreach(var hit in projectileContacts.Hits) MarkConsumed(hit.Projectile);
            foreach(var shot in projectiles.Values)
                if(shot.IsActive && SimulationTime+Time.fixedDeltaTime>=shot.ExpiresAt) MarkConsumed(shot);
        }
        private void MarkConsumed(Projectile shot)
        {
            if(shot==null || shot.Consumed) return;
            shot.Consume(); consumedProjectiles.Add(shot); ConsumedProjectileCount++;
        }
        private void FlushProjectiles()
        {
            foreach(var shot in consumedProjectiles)
            {
                if(shot==null) continue;
                shot.Body.Rigidbody.simulated=false;
                UnregisterBody(shot.Body);
                shot.Body.Unbind(this);
                shot.gameObject.SetActive(false);
                Destroy(shot.gameObject);
            }
            consumedProjectiles.Clear();
        }

        private void ResolveDamage()
        {
            double time = SimulationTime + Time.fixedDeltaTime;
            damage.BuildPlan(damageContacts.Contacts,time,settings.ImpactDamageThreshold,damagePlan);
            foreach(var contact in damagePlan)
            {
                float interval = contact.Target == PlayerHealth ? settings.PlayerDamageInterval : settings.EnemyDamageInterval;
                contact.Target.ApplyDamage(settings.DamagePerHit,time,interval);
                if (contact.Target==PlayerHealth) PlayerDamageCount++;
                damageResults.Add(new DamageResult(CompletedStepCount+1,contact,settings.DamagePerHit,contact.Target.CurrentHealth));
            }
            // All life states change before propagation, but no collections are modified here.
        }

        private void FlushDeaths()
        {
            foreach(var contact in damagePlan)
            {
                var victim=contact.Target;
                if (victim.IsAlive) continue;
                victim.Rigidbody.simulated=false;
                if (victim==PlayerHealth) { player.ClearIntent(); continue; }
                var body=victim.GetComponent<PolarityBody>();
                if (body!=null) UnregisterBody(body);
                victim.gameObject.SetActive(false);
                Destroy(victim.gameObject);
            }
        }

        private void PublishDamage()
        {
            for(int i=0;i<damageResults.Count;i++)
            {
                var target=damagePlan[i].Target;
                if(target!=null) target.NotifyHealthChanged();
                DamageCommitted?.Invoke(damageResults[i]);
            }
        }

        internal void PrepareForRestart()
        {
            restarting=true;
            navigation?.ClearRuntime();
            foreach (var navigator in enemyNavigators.Values) navigator.Clear();
            touchedSpikes.Clear();
            State.SetPhase(LevelPhase.Ready);
            markRequests.Clear(); ClearContactState(); damageContacts.Clear(); damage.Clear(); damagePlan.Clear(); damageResults.Clear();
            player.ClearIntent();
            MarkResolved=null; PropagationCommitted=null; DamageCommitted=null;
            levelController.Clear();
            SlowMotionRequested=false;
            ApplyTimeScale();
            TimeScaleChanged=null;
        }

        private void CommitPropagation()
        {
            foreach (var result in propagationPlan)
            {
                var source = bodies.Find(body => body != null && body.BodyId == result.SourceId);
                var target = bodies.Find(body => body != null && body.BodyId == result.TargetId);
                if (source == null || !source.IsValid || !source.Rigidbody.simulated || target == null || !target.IsValid ||
                    !target.Rigidbody.simulated || target.CurrentPolarity != Pole.Neutral) continue;
                if (target.SetPolarity(result.Polarity, false)) propagationResults.Add(result);
            }
        }

        private void PublishPropagation()
        {
            // All target states are committed before any polarity or propagation view notification.
            foreach (var result in propagationResults)
            {
                LastPropagation = result;
                var target = bodies.Find(body => body != null && body.BodyId == result.TargetId);
                if (target != null && target.IsValid) target.NotifyPolarityChanged();
                PropagationCommitted?.Invoke(result);
            }
        }

        private void ClearStepContacts()
        {
            projectileContacts.Clear(); consumedProjectiles.Clear();
            contacts.Clear(); propagation.Clear(); propagationPlan.Clear(); propagationResults.Clear();
            damageContacts.Clear(); damage.Clear(); damagePlan.Clear(); damageResults.Clear();
        }

        private void ClearContactState()
        {
            collectingContacts = false;
            ClearStepContacts(); propagationSnapshot.Clear(); LastPropagation = null;
        }

        private void CaptureMagneticSnapshot()
        {
            magneticSnapshots.Clear();
            propagationSnapshot.Clear();
            foreach (var body in bodies)
            {
                if (body == null || !body.IsValid)
                    continue;
                Rigidbody2D rigidBody = body.Rigidbody;
                if (rigidBody.simulated)
                    propagationSnapshot.Add(new BodySnapshot(body, body.gameObject.layer == enemyLayer || body.gameObject.layer == crateLayer));
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
            magnetism.Evaluate(magneticSnapshots,settings,magneticForces,magneticLinks);
            LastPairEvaluationCount=magnetism.PairEvaluationCount;
            LastMagneticPairCount=magnetism.MagneticPairCount;
            LastBlockedPairCount=magnetism.BlockedPairCount;
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

        private void FailStep(string message)
        {
            FailedStepCount++;
            SetPhase(LevelPhase.Paused);
            Debug.LogError(message, this);
        }

        private void OnDisable() => ReleasePhysics();
        private void OnDestroy() => ReleasePhysics();

        private void ReleasePhysics()
        {
            if (!ownsPhysics)
                return;
            navigation?.ClearRuntime();
            State.PhaseChanged -= OnPhaseChanged;
            TimeScaleChanged=null;
            MarkResolved=null; PropagationCommitted=null; DamageCommitted=null;
            levelController?.Clear();
            foreach(var health in healthByRigidbody.Values) if(health!=null) health.Unbind(this);
            healthByRigidbody.Clear();
            touchedSpikes.Clear();
            markRequests.Clear();
            player?.ClearIntent();
            foreach (var body in bodies)
                if (body != null)
                    body.Unbind(this);
            bodies.Clear();
            projectiles.Clear();
            enemyMotors.Clear();
            foreach (var navigator in enemyNavigators.Values) navigator.Clear();
            enemyNavigators.Clear();
            bodiesByRigidbody.Clear();
            ClearContactState();
            magneticSnapshots.Clear();
            magneticLinks.Clear();
            State?.SetActiveEnemyCount(0);
            if (physicsOwner == this)
            {
                Physics2D.simulationMode = previousSimulationMode;
                Time.timeScale = restarting ? 1f : previousTimeScale;
                physicsOwner = null;
            }
            ownsPhysics = false;
        }
    }
}
