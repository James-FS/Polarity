using System;
using System.Collections.Generic;
using Polarity.Config;
using UnityEngine;

namespace Polarity.Simulation
{
    /// <summary>Queries only colliders belonging to one room, using that room's 2D physics scene.</summary>
    public sealed class NavigationGridSampler
    {
        private readonly NavigationGridAuthoring authoring;
        private readonly HashSet<Collider2D> roomColliders;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<RaycastHit2D> casts = new List<RaycastHit2D>();
        private readonly List<DynamicSample> dynamics = new List<DynamicSample>();
        private readonly PhysicsScene2D physics;
        private readonly ContactFilter2D staticFilter, hazardFilter, dynamicFilter;
        private readonly bool[] dirty;
        private readonly byte[] staticConnections;
        private readonly float radius;
        public NavigationGrid Grid { get; }
        public int LastResampledCells { get; private set; }
        public int LastRebuiltConnections { get; private set; }
        public int DynamicColliderCount => dynamics.Count;
        public float QueryRadius => radius;
        private readonly HashSet<Collider2D> startingHazards = new HashSet<Collider2D>();

        private struct DynamicSample
        {
            public Collider2D Collider;
            public bool Active;
            public Bounds Bounds;
            public Matrix4x4 Matrix;
        }

        public NavigationGridSampler(NavigationGridAuthoring source, Transform roomRoot)
        {
            if (source == null || !source.HasValidConfiguration || roomRoot == null || !source.transform.IsChildOf(roomRoot))
                throw new ArgumentException("Grid authoring must be valid and belong to the room.");
            authoring = source;
            radius = source.QueryRadius;
            Vector2 center = source.transform.TransformPoint(source.BoundsCenter);
            Grid = new NavigationGrid(center - source.BoundsSize * 0.5f, source.BoundsSize, source.CellSize);
            physics = PhysicsSceneExtensions2D.GetPhysicsScene2D(roomRoot.gameObject.scene);
            if (!physics.IsValid()) throw new ArgumentException("Room has no valid 2D physics scene.");
            roomColliders = new HashSet<Collider2D>(roomRoot.GetComponentsInChildren<Collider2D>(true));
            staticFilter = Filter(source.StaticObstacleMask, false);
            hazardFilter = Filter(source.HazardMask, true);
            dynamicFilter = Filter(source.DynamicObstacleMask, false);
            foreach (var collider in roomColliders)
                if (Matches(collider, source.DynamicObstacleMask)) dynamics.Add(Capture(collider));
            dirty = new bool[Grid.Count];
            staticConnections = new byte[Grid.Count];
            for (int i = 0; i < Grid.Count; i++)
            {
                Vector2 point = Grid.GetCenter(i);
                NavigationCell value = Inside(point) ? NavigationCell.Free : NavigationCell.Boundary;
                if (Overlaps(point, staticFilter)) value |= NavigationCell.StaticObstacle;
                if (Overlaps(point, hazardFilter)) value |= NavigationCell.Hazard;
                Grid.SetCell(i, value);
            }
            for (int i = 0; i < Grid.Count; i++) staticConnections[i] = BuildConnections(i, false);
            for (int i = 0; i < Grid.Count; i++) ResampleDynamic(i);
            for (int i = 0; i < Grid.Count; i++) Grid.SetConnections(i, BuildConnections(i, true));
            Grid.Version = 1;
            LastResampledCells = LastRebuiltConnections = Grid.Count;
        }

        /// <summary>Called before World movement, never in an independent Update/FixedUpdate.</summary>
        public bool RefreshDynamic()
        {
            LastResampledCells = LastRebuiltConnections = 0;
            Array.Clear(dirty, 0, dirty.Length);
            bool changed = false;
            for (int i = 0; i < dynamics.Count; i++)
            {
                DynamicSample before = dynamics[i], after = Capture(before.Collider);
                if (before.Active == after.Active && before.Bounds == after.Bounds && before.Matrix == after.Matrix) continue;
                if (before.Active) MarkBounds(before.Bounds);
                if (after.Active) MarkBounds(after.Bounds);
                dynamics[i] = after;
                changed = true;
            }
            if (!changed) return false;
            for (int i = 0; i < Grid.Count; i++)
                if (dirty[i]) { ResampleDynamic(i); LastResampledCells++; }
            // MarkBounds includes neighbouring origins so incident and diagonal-dependent edges are rebuilt too.
            for (int i = 0; i < Grid.Count; i++)
                if (dirty[i]) { Grid.SetConnections(i, BuildConnections(i, true)); LastRebuiltConnections++; }
            Grid.Version++;
            return true;
        }

        /// <summary>Fresh full-body check for future path simplification and local steering.</summary>
        public bool IsSegmentSafe(Vector2 from, Vector2 to)
            => Finite(from) && Finite(to) && Inside(from) && Inside(to) &&
               !Overlaps(from, staticFilter) && !Overlaps(from, hazardFilter) && !Overlaps(from, dynamicFilter) &&
               !Overlaps(to, staticFilter) && !Overlaps(to, hazardFilter) && !Overlaps(to, dynamicFilter) &&
               !Sweeps(from, to, staticFilter) && !Sweeps(from, to, hazardFilter) && !Sweeps(from, to, dynamicFilter);

        public bool IsInHazard(Vector2 point)
            => Overlaps(point, hazardFilter, PhysicalRadius);

        // Clearance is a navigation margin, not an extra physical wall. Permit only a clear physical
        // segment out of that margin, never through a solid or into a hazard.
        public bool IsClearanceRecoverySafe(Vector2 from, Vector2 to)
            => Inside(from, PhysicalRadius) && Inside(to) &&
               CanDepartTouchingSolids(from, to, staticFilter) && CanDepartTouchingSolids(from, to, dynamicFilter) &&
               !Overlaps(from, hazardFilter, PhysicalRadius) &&
               !Overlaps(to, staticFilter) && !Overlaps(to, dynamicFilter) && !Overlaps(to, hazardFilter) &&
               !Sweeps(from, to, staticFilter, PhysicalRadius) && !Sweeps(from, to, dynamicFilter, PhysicalRadius) &&
               !Sweeps(from, to, hazardFilter, PhysicalRadius);

        public bool IsHazardExitSafe(Vector2 from, Vector2 to)
        {
            if (!Finite(from) || !Finite(to) || !Inside(from, PhysicalRadius) || !Inside(to) ||
                !CanDepartTouchingSolids(from, to, staticFilter) || !CanDepartTouchingSolids(from, to, dynamicFilter) ||
                Overlaps(to, staticFilter) || Overlaps(to, dynamicFilter) || Overlaps(to, hazardFilter) ||
                Sweeps(from, to, staticFilter, PhysicalRadius) || Sweeps(from, to, dynamicFilter, PhysicalRadius)) return false;
            startingHazards.Clear();
            overlaps.Clear(); physics.OverlapCircle(from, PhysicalRadius, hazardFilter, overlaps);
            foreach (var hit in overlaps) if (hit != null && roomColliders.Contains(hit)) startingHazards.Add(hit);
            if (startingHazards.Count == 0) return false;
            casts.Clear(); Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.0000001f) return false;
            physics.CircleCast(from, PhysicalRadius, delta.normalized, delta.magnitude, hazardFilter, casts);
            foreach (var hit in casts)
                if (hit.collider != null && roomColliders.Contains(hit.collider) && !startingHazards.Contains(hit.collider)) return false;
            return true;
        }

        public bool TryFindEntry(Vector2 point, float maxDistance, bool hazardExit, out int index)
        {
            index = -1; float best = maxDistance * maxDistance;
            for (int i = 0; i < Grid.Count; i++)
            {
                if (!Grid.IsWalkable(i)) continue;
                Vector2 candidate = Grid.GetCenter(i); float distance = (candidate - point).sqrMagnitude;
                if (distance > best || (distance == best && index >= 0 && i > index)) continue;
                bool safe = hazardExit ? IsHazardExitSafe(point, candidate) :
                    IsSegmentSafe(point, candidate) || IsClearanceRecoverySafe(point, candidate);
                if (!safe) continue;
                best = distance; index = i;
            }
            return index >= 0;
        }

        private float PhysicalRadius => Mathf.Max(0.01f, radius - authoring.ClearanceMargin - 0.005f);

        private bool CanDepartTouchingSolids(Vector2 from, Vector2 to, ContactFilter2D filter)
        {
            overlaps.Clear();
            physics.OverlapCircle(from, PhysicalRadius, filter, overlaps);
            foreach (var collider in overlaps)
            {
                if (collider == null || !roomColliders.Contains(collider)) continue;
                Vector2 outward = from - collider.ClosestPoint(from);
                float distance = outward.magnitude;
                // Solver contact slop and float rounding can classify a tangent body as overlapping.
                // Accept only a shallow contact with strictly outward intent. Endpoints and the whole
                // physical sweep below still have to be clear; deep penetration never qualifies.
                if (distance < PhysicalRadius - 0.001f || distance < 0.0001f ||
                    Vector2.Dot(to - from, outward / distance) <= 0.0001f) return false;
            }
            return true;
        }

        private void ResampleDynamic(int index)
        {
            NavigationCell value = Grid.GetCell(index) & ~NavigationCell.DynamicObstacle;
            if (Overlaps(Grid.GetCenter(index), dynamicFilter)) value |= NavigationCell.DynamicObstacle;
            Grid.SetCell(index, value);
        }

        private byte BuildConnections(int index, bool includeDynamic)
        {
            if (!Walkable(index, includeDynamic)) return 0;
            int x = index % Grid.Columns, y = index / Grid.Columns;
            byte result = 0;
            for (int d = 0; d < NavigationGrid.Directions.Length; d++)
            {
                Vector2Int direction = NavigationGrid.Directions[d];
                int next = Grid.Index(x + direction.x, y + direction.y);
                if (!Walkable(next, includeDynamic)) continue;
                if (direction.x != 0 && direction.y != 0 &&
                    (!Walkable(Grid.Index(x + direction.x, y), includeDynamic) ||
                     !Walkable(Grid.Index(x, y + direction.y), includeDynamic))) continue;
                Vector2 from = Grid.GetCenter(index), to = Grid.GetCenter(next);
                if (includeDynamic)
                {
                    if ((staticConnections[index] & (1 << d)) == 0 || Sweeps(from, to, dynamicFilter)) continue;
                }
                else if (Sweeps(from, to, staticFilter) || Sweeps(from, to, hazardFilter)) continue;
                result |= (byte)(1 << d);
            }
            return result;
        }

        private bool Walkable(int index, bool includeDynamic) => index >= 0 && index < Grid.Count &&
            (includeDynamic ? Grid.GetCell(index) == NavigationCell.Free :
             (Grid.GetCell(index) & ~NavigationCell.DynamicObstacle) == NavigationCell.Free);

        private bool Inside(Vector2 point, float checkRadius = -1f)
        {
            if (checkRadius < 0f) checkRadius = radius;
            return Finite(point) && point.x - checkRadius >= Grid.Min.x && point.y - checkRadius >= Grid.Min.y &&
                point.x + checkRadius <= Grid.Max.x && point.y + checkRadius <= Grid.Max.y;
        }

        private bool Overlaps(Vector2 point, ContactFilter2D filter, float checkRadius = -1f)
        {
            overlaps.Clear();
            physics.OverlapCircle(point, checkRadius < 0f ? radius : checkRadius, filter, overlaps);
            foreach (var hit in overlaps) if (hit != null && roomColliders.Contains(hit)) return true;
            return false;
        }

        private bool Sweeps(Vector2 from, Vector2 to, ContactFilter2D filter, float checkRadius = -1f)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.0000001f) return false;
            casts.Clear();
            physics.CircleCast(from, checkRadius < 0f ? radius : checkRadius, delta.normalized, delta.magnitude, filter, casts);
            foreach (var hit in casts) if (hit.collider != null && roomColliders.Contains(hit.collider)) return true;
            return false;
        }

        private void MarkBounds(Bounds bounds)
        {
            float padding = radius + Grid.CellSize * 2f;
            int minX = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - padding - Grid.Min.x) / Grid.CellSize), 0, Grid.Columns - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((bounds.max.x + padding - Grid.Min.x) / Grid.CellSize), 0, Grid.Columns - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((bounds.min.y - padding - Grid.Min.y) / Grid.CellSize), 0, Grid.Rows - 1);
            int maxY = Mathf.Clamp(Mathf.FloorToInt((bounds.max.y + padding - Grid.Min.y) / Grid.CellSize), 0, Grid.Rows - 1);
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) dirty[Grid.Index(x, y)] = true;
        }

        private DynamicSample Capture(Collider2D collider)
        {
            bool active = collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                !collider.isTrigger && Matches(collider, authoring.DynamicObstacleMask) &&
                (collider.attachedRigidbody == null || collider.attachedRigidbody.simulated);
            return new DynamicSample { Collider = collider, Active = active,
                Bounds = active ? collider.bounds : default, Matrix = active ? collider.transform.localToWorldMatrix : default };
        }

        private static bool Matches(Collider2D collider, LayerMask mask) => collider != null && (mask.value & (1 << collider.gameObject.layer)) != 0;
        private static ContactFilter2D Filter(LayerMask mask, bool triggers)
        {
            var filter = new ContactFilter2D(); filter.SetLayerMask(mask); filter.useTriggers = triggers; return filter;
        }
        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
