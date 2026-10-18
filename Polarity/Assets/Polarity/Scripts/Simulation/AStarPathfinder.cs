using System;
using System.Collections.Generic;
using UnityEngine;

namespace Polarity.Simulation
{
    public enum PathSearchStatus { Pending, PathFound, NoSafePath }

    /// <summary>Incremental A* over a request-local snapshot. No physics or movement authority.</summary>
    public sealed class AStarPathfinder
    {
        private NavigationGrid grid;
        private byte[] edges;
        private float[] costs;
        private int[] parents, heap, heapPositions;
        private bool[] closed;
        private int heapCount, goal, best;
        private float bestDistance;
        private readonly List<int> path = new List<int>();
        private IReadOnlyList<int> readOnlyPath;
        public PathSearchStatus Status { get; private set; } = PathSearchStatus.NoSafePath;
        public IReadOnlyList<int> Path => readOnlyPath ?? (readOnlyPath = path.AsReadOnly());
        public Vector2 Target { get; private set; }
        public int SourceVersion { get; private set; }
        public int ExpandedNodes { get; private set; }
        public int LastStepExpansions { get; private set; }
        public bool IsPartial { get; private set; }

        public void Begin(NavigationGrid source, int start, Vector2 target)
        {
            Cancel();
            if (source == null || !source.IsWalkable(start) || !Finite(target)) return;
            grid = source; Target = target; SourceVersion = source.Version;
            int count = source.Count;
            if (edges == null || edges.Length != count)
            {
                edges = new byte[count]; costs = new float[count]; parents = new int[count];
                heap = new int[count]; heapPositions = new int[count]; closed = new bool[count];
            }
            for (int i = 0; i < count; i++)
            {
                edges[i] = source.GetConnections(i); costs[i] = float.PositiveInfinity;
                parents[i] = heapPositions[i] = -1; closed[i] = false;
            }
            goal = source.TryGetCell(target, out int candidate) && source.IsWalkable(candidate) ? candidate : -1;
            best = start; bestDistance = (source.GetCenter(start) - target).sqrMagnitude;
            costs[start] = 0f; PushOrDecrease(start);
            Status = PathSearchStatus.Pending;
        }

        public PathSearchStatus Step(int expansionBudget)
        {
            LastStepExpansions = 0;
            if (Status != PathSearchStatus.Pending) return Status;
            while (heapCount > 0 && LastStepExpansions < Mathf.Max(0, expansionBudget))
            {
                int current = Pop(); closed[current] = true;
                ExpandedNodes++; LastStepExpansions++;
                float distance = (grid.GetCenter(current) - Target).sqrMagnitude;
                if (distance < bestDistance - 0.000001f ||
                    (Mathf.Abs(distance - bestDistance) <= 0.000001f &&
                     (costs[current] < costs[best] || (costs[current] == costs[best] && current < best))))
                { best = current; bestDistance = distance; }
                if (current == goal) { Finish(current, false); return Status; }
                for (int d = 0; d < NavigationGrid.Directions.Length; d++)
                {
                    if ((edges[current] & (1 << d)) == 0) continue;
                    Vector2Int direction = NavigationGrid.Directions[d];
                    int next = grid.Index(current % grid.Columns + direction.x, current / grid.Columns + direction.y);
                    if (next < 0 || closed[next]) continue;
                    float cost = costs[current] + Vector2.Distance(grid.GetCenter(current), grid.GetCenter(next));
                    if (cost >= costs[next]) continue;
                    costs[next] = cost; parents[next] = current; PushOrDecrease(next);
                }
            }
            // A disconnected/forbidden target resolves to the closest node actually reached by this search.
            if (heapCount == 0) Finish(best, true);
            return Status;
        }

        public void Cancel()
        {
            path.Clear(); heapCount = 0; grid = null;
            Status = PathSearchStatus.NoSafePath; IsPartial = false;
            ExpandedNodes = LastStepExpansions = SourceVersion = 0;
        }

        private void Finish(int end, bool partial)
        {
            path.Clear();
            for (int node = end; node >= 0; node = parents[node]) path.Add(node);
            path.Reverse(); IsPartial = partial;
            Status = path.Count > 0 ? PathSearchStatus.PathFound : PathSearchStatus.NoSafePath;
        }

        private float Heuristic(int node) => Vector2.Distance(grid.GetCenter(node), Target);
        private bool Earlier(int a, int b)
        {
            float ha = Heuristic(a), hb = Heuristic(b);
            float fa = costs[a] + ha, fb = costs[b] + hb;
            return fa < fb || (fa == fb && (ha < hb || (ha == hb && a < b)));
        }
        private void PushOrDecrease(int node)
        {
            int position = heapPositions[node];
            if (position < 0) { position = heapCount++; heap[position] = node; heapPositions[node] = position; }
            while (position > 0)
            {
                int parent = (position - 1) / 2;
                if (!Earlier(heap[position], heap[parent])) break;
                Swap(position, parent); position = parent;
            }
        }
        private int Pop()
        {
            int result = heap[0]; heapPositions[result] = -1;
            heapCount--;
            if (heapCount == 0) return result;
            heap[0] = heap[heapCount]; heapPositions[heap[0]] = 0;
            int position = 0;
            while (true)
            {
                int left = position * 2 + 1;
                if (left >= heapCount) break;
                int right = left + 1, next = right < heapCount && Earlier(heap[right], heap[left]) ? right : left;
                if (!Earlier(heap[next], heap[position])) break;
                Swap(position, next); position = next;
            }
            return result;
        }
        private void Swap(int a, int b)
        {
            int node = heap[a]; heap[a] = heap[b]; heap[b] = node;
            heapPositions[heap[a]] = a; heapPositions[heap[b]] = b;
        }
        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
