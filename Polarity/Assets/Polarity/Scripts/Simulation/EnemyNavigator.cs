using System.Collections.Generic;
using Polarity.Config;
using UnityEngine;

namespace Polarity.Simulation
{
    public enum EnemyNavigationState { Searching, Following, Avoiding, Waiting, Recovering, EscapingHazard }

    /// <summary>Per-enemy route and movement intent. World supplies time and the shared search budget.</summary>
    public sealed class EnemyNavigator
    {
        private readonly AStarPathfinder search = new AStarPathfinder();
        private readonly List<Vector2> route = new List<Vector2>();
        private IReadOnlyList<Vector2> readOnlyRoute;
        private int waypoint, routeVersion = -1;
        private bool requested, hasResult, partial, replanPending, routeDisconnected;
        private double nextRequest;
        private Vector2 requestedTarget, lastPosition, escapeTarget;
        private float noProgressTime;
        private bool hasPosition, hasEscapeTarget;
        private bool hasLocalTarget;
        private Vector2 localTarget;
        private float avoidanceAngle;
        public EnemyNavigationState State { get; private set; } = EnemyNavigationState.Waiting;
        public IReadOnlyList<Vector2> Path => readOnlyRoute ?? (readOnlyRoute = route.AsReadOnly());
        public int WaypointIndex => waypoint;
        public PathSearchStatus SearchStatus => search.Status;
        public int LastExpansions { get; private set; }
        public int ExpandedNodes => search.ExpandedNodes;
        public int ReplanCount { get; private set; }
        public string ReplanReason { get; private set; } = "Initial";
        public Vector2 DesiredVelocity { get; private set; }
        public bool IsPartial => partial;

        public Vector2 Tick(NavigationGridAuthoring config, Vector2 position, Vector2 velocity,
            Vector2 target, float chaseSpeed, float maxAcceleration, double time, float step, ref int sharedBudget)
        {
            LastExpansions = 0; DesiredVelocity = Vector2.zero;
            var sampler = config.Sampler; var grid = sampler?.Grid;
            if (grid == null || !Finite(position) || !Finite(velocity) || !Finite(target))
            { State = EnemyNavigationState.Waiting; return DesiredVelocity; }

            // Recover using real forces; neither position nor velocity is projected onto the grid.
            if (!sampler.IsSegmentSafe(position, position))
            {
                bool hazard = sampler.IsInHazard(position);
                bool retain = hazard && hasEscapeTarget && sampler.IsHazardExitSafe(position, escapeTarget);
                int entry = -1;
                if (retain || sampler.TryFindEntry(position, hazard ? 3f : 1.5f, hazard, out entry))
                {
                    if (!retain) escapeTarget = grid.GetCenter(entry);
                    hasEscapeTarget = hazard;
                    State = hazard ? EnemyNavigationState.EscapingHazard : EnemyNavigationState.Recovering;
                    DesiredVelocity = Toward(position, escapeTarget, chaseSpeed, 0.3f);
                }
                else State = EnemyNavigationState.Waiting;
                route.Clear(); waypoint = 0; requested = hasResult = false; search.Cancel();
                routeDisconnected = false;
                hasLocalTarget = false;
                ReplanReason = hazard ? "Hazard displacement" : "Clearance displacement";
                return DesiredVelocity;
            }
            hasEscapeTarget = false;

            bool movedTarget = requested && (target - requestedTarget).sqrMagnitude >= config.TargetMoveThreshold * config.TargetMoveThreshold;
            bool occupancyChanged = routeVersion != grid.Version;
            bool routeBlocked = false;
            if (route.Count > 0 && routeVersion != grid.Version)
            {
                for (int i = Mathf.Max(waypoint, 0); i < route.Count; i++)
                {
                    Vector2 from = i == waypoint ? position : route[i - 1];
                    if (!sampler.IsSegmentSafe(from, route[i])) { routeBlocked = true; break; }
                }
                routeVersion = grid.Version;
            }
            if (hasPosition && DesiredOrMoving(velocity))
                noProgressTime = (position - lastPosition).sqrMagnitude < 0.000004f ? noProgressTime + step : 0f;
            else noProgressTime = 0f;
            lastPosition = position; hasPosition = true;
            bool stalled = noProgressTime > 0.8f;
            bool changedWhileWaiting = hasResult && route.Count == 0 && occupancyChanged;
            bool retryPartial = hasResult && partial && occupancyChanged;
            bool needRequest = replanPending || !requested || movedTarget || routeBlocked || stalled || changedWhileWaiting || retryPartial;
            if (needRequest) replanPending = true;
            if (routeBlocked)
            {
                localTarget = route[Mathf.Min(waypoint, route.Count - 1)]; hasLocalTarget = true;
                route.Clear(); waypoint = 0;
            }
            // Finish the bounded snapshot before replacing its target; moving targets must not
            // repeatedly erase a nearly completed disconnected-region search.
            if (needRequest && time >= nextRequest && search.Status != PathSearchStatus.Pending)
            {
                ReplanReason = !requested ? "Initial" : movedTarget ? "Target moved" : routeBlocked ? "Route blocked" :
                    routeDisconnected ? "Displaced from route" :
                    stalled ? "No progress" : "Occupancy changed";
                int start;
                bool entry = grid.TryGetCell(position, out start) && grid.IsWalkable(start) && sampler.IsSegmentSafe(position, grid.GetCenter(start));
                if (!entry) entry = sampler.TryFindEntry(position, 1.5f, false, out start);
                search.Begin(grid, entry ? start : -1, target);
                requested = true; hasResult = false; requestedTarget = target;
                replanPending = false;
                routeDisconnected = false;
                if (search.Status == PathSearchStatus.NoSafePath) { hasResult = true; routeVersion = grid.Version; }
                nextRequest = time + config.ReplanInterval; noProgressTime = 0f; ReplanCount++;
            }

            if (search.Status == PathSearchStatus.Pending && sharedBudget > 0)
            {
                search.Step(Mathf.Min(sharedBudget, config.PerEnemySearchBudget));
                LastExpansions = search.LastStepExpansions; sharedBudget -= LastExpansions;
                if (search.Status == PathSearchStatus.PathFound)
                {
                    // Snapshot results are accepted only after all segments pass current geometry checks.
                    bool valid = true;
                    Vector2 from = position;
                    foreach (int index in search.Path)
                    {
                        Vector2 to = grid.GetCenter(index);
                        if (!grid.IsWalkable(index) || !sampler.IsSegmentSafe(from, to)) { valid = false; break; }
                        from = to;
                    }
                    if (valid)
                    {
                        route.Clear();
                        foreach (int index in search.Path) route.Add(grid.GetCenter(index));
                        waypoint = 0; partial = search.IsPartial; hasResult = true; routeVersion = grid.Version;
                        hasLocalTarget = false;
                    }
                    else { search.Cancel(); requested = false; ReplanReason = "Snapshot route invalidated"; }
                }
            }

            // Direct safe pursuit preserves real contact near the player rather than stopping at a cell center.
            bool direct = sampler.IsSegmentSafe(position, target);
            Vector2 destination = target;
            bool canMove = direct;
            if (!direct && route.Count > 0)
            {
                while (waypoint < route.Count && Vector2.Distance(position, route[waypoint]) < 0.12f) waypoint++;
                for (int i = route.Count - 1; i >= waypoint; i--)
                    if (sampler.IsSegmentSafe(position, route[i])) { waypoint = i; destination = route[i]; canMove = true; break; }
                bool atPartialEnd = partial && Vector2.Distance(position, route[route.Count - 1]) < 0.12f;
                if (atPartialEnd)
                    canMove = false;
                else if (!canMove || waypoint >= route.Count)
                {
                    // External motion can disconnect us from the remaining route without any
                    // target or grid change. Replan from reality, preserving the normal budget
                    // and interval; a completed partial route must still be allowed to wait.
                    localTarget = route[Mathf.Min(waypoint, route.Count - 1)];
                    hasLocalTarget = true;
                    route.Clear(); waypoint = 0;
                    routeDisconnected = replanPending = true;
                    canMove = false;
                }
            }
            if (!canMove)
            {
                if (hasLocalTarget)
                {
                    State = EnemyNavigationState.Avoiding;
                    DesiredVelocity = LocalAvoidance(sampler, position, velocity, localTarget, chaseSpeed, maxAcceleration);
                    return DesiredVelocity;
                }
                State = search.Status == PathSearchStatus.Pending ? EnemyNavigationState.Searching : EnemyNavigationState.Waiting;
                return DesiredVelocity;
            }

            Vector2 delta = destination - position;
            float distance = delta.magnitude;
            Vector2 direction = distance > 0.0001f ? delta / distance : Vector2.zero;
            float speed = chaseSpeed * Mathf.Clamp01(distance / (direct ? 0.8f : 0.3f));
            float stopDistance = velocity.sqrMagnitude / (2f * Mathf.Max(0.01f, maxAcceleration)) + 0.08f;
            if (velocity.sqrMagnitude > 0.04f && !sampler.IsSegmentSafe(position, position + velocity.normalized * stopDistance))
            { State = EnemyNavigationState.Avoiding; return DesiredVelocity; }
            float lookAhead = Mathf.Min(distance, Mathf.Max(0.15f, speed * 0.3f));
            if (!sampler.IsSegmentSafe(position, position + direction * lookAhead))
            {
                State = EnemyNavigationState.Avoiding;
                DesiredVelocity = LocalAvoidance(sampler, position, velocity, destination, speed, maxAcceleration);
                return DesiredVelocity;
            }
            State = EnemyNavigationState.Following;
            avoidanceAngle = 0f;
            DesiredVelocity = direction * speed;
            return DesiredVelocity;
        }

        private static readonly float[] LocalAngles = { 0f, 30f, -30f, 60f, -60f };
        private Vector2 LocalAvoidance(NavigationGridSampler sampler, Vector2 position, Vector2 velocity,
            Vector2 destination, float speed, float maxAcceleration)
        {
            float stopDistance = velocity.sqrMagnitude / (2f * Mathf.Max(0.01f, maxAcceleration)) + 0.08f;
            if (velocity.sqrMagnitude > 0.04f && !sampler.IsSegmentSafe(position, position + velocity.normalized * stopDistance))
                return Vector2.zero;
            Vector2 delta = destination - position;
            float lookAhead = Mathf.Min(delta.magnitude, Mathf.Max(0.15f, speed * 0.3f));
            if (delta.sqrMagnitude < 0.0001f) return Vector2.zero;
            Vector2 direction = delta.normalized;
            // Hold a previously chosen safe side until the global route takes over or that side fails.
            if (avoidanceAngle != 0f)
            {
                Vector2 retained = Quaternion.Euler(0f, 0f, avoidanceAngle) * direction;
                if (sampler.IsSegmentSafe(position, position + retained * lookAhead)) return retained * speed * 0.5f;
            }
            foreach (float angle in LocalAngles)
            {
                Vector2 candidate = Quaternion.Euler(0f, 0f, angle) * direction;
                if (!sampler.IsSegmentSafe(position, position + candidate * lookAhead)) continue;
                avoidanceAngle = angle; return candidate * speed * 0.5f;
            }
            return Vector2.zero;
        }
        private bool DesiredOrMoving(Vector2 velocity) => State == EnemyNavigationState.Following || velocity.sqrMagnitude > 0.01f;
        private static Vector2 Toward(Vector2 from, Vector2 to, float speed, float arrival)
        {
            Vector2 delta = to - from; float distance = delta.magnitude;
            return distance > 0.0001f ? delta / distance * speed * Mathf.Clamp01(distance / arrival) : Vector2.zero;
        }
        public void Clear()
        {
            search.Cancel(); route.Clear(); requested = hasResult = hasPosition = hasEscapeTarget = replanPending = false;
            hasLocalTarget = false; avoidanceAngle = 0f;
            routeDisconnected = false;
            waypoint = 0; routeVersion = -1; nextRequest = 0; noProgressTime = 0f;
            DesiredVelocity = Vector2.zero; State = EnemyNavigationState.Waiting;
        }
        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
