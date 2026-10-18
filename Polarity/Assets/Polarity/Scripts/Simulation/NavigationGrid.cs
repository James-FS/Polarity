using System;
using UnityEngine;

namespace Polarity.Simulation
{
    [Flags]
    public enum NavigationCell : byte
    {
        Free = 0,
        Boundary = 1,
        StaticObstacle = 2,
        Hazard = 4,
        DynamicObstacle = 8
    }

    /// <summary>Room grid data only. No physics queries, scene objects or actor movement.</summary>
    public sealed class NavigationGrid
    {
        public const int MaxCells = 65536;
        private readonly NavigationCell[] cells;
        private readonly byte[] connections;
        public static readonly Vector2Int[] Directions = {
            new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1),
            new Vector2Int(-1,0), new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1,-1)
        };

        public int Columns { get; }
        public int Rows { get; }
        public int Count => cells.Length;
        public float CellSize { get; }
        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public int Version { get; internal set; }

        public NavigationGrid(Vector2 min, Vector2 size, float cellSize)
        {
            if (!Finite(min.x) || !Finite(min.y) || !Finite(size.x) || !Finite(size.y) ||
                !Finite(cellSize) || size.x <= 0f || size.y <= 0f || cellSize < 0.05f)
                throw new ArgumentException("Grid requires finite bounds and positive cell size.");
            double width = Math.Ceiling(size.x / (double)cellSize);
            double height = Math.Ceiling(size.y / (double)cellSize);
            if (width * height > MaxCells) throw new ArgumentException("Navigation grid exceeds 65536 cells.");
            Min = min; Max = min + size; CellSize = cellSize;
            Columns = (int)width; Rows = (int)height;
            cells = new NavigationCell[Columns * Rows];
            connections = new byte[cells.Length];
        }

        public int Index(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows ? y * Columns + x : -1;
        public NavigationCell GetCell(int index) => cells[index];
        public bool IsWalkable(int index) => index >= 0 && index < Count && cells[index] == NavigationCell.Free;
        public byte GetConnections(int index) => connections[index];
        internal void SetCell(int index, NavigationCell value) => cells[index] = value;
        internal void SetConnections(int index, byte value) => connections[index] = value;

        public Vector2 GetCellSize(int index)
        {
            int x = index % Columns, y = index / Columns;
            return new Vector2(Mathf.Min(CellSize, Max.x - (Min.x + x * CellSize)),
                Mathf.Min(CellSize, Max.y - (Min.y + y * CellSize)));
        }

        public Vector2 GetCenter(int index)
        {
            Vector2 size = GetCellSize(index);
            return Min + new Vector2(index % Columns * CellSize, index / Columns * CellSize) + size * 0.5f;
        }

        public bool TryGetCell(Vector2 point, out int index)
        {
            index = -1;
            if (!Finite(point.x) || !Finite(point.y) || point.x < Min.x || point.y < Min.y || point.x >= Max.x || point.y >= Max.y)
                return false;
            index = Index(Mathf.FloorToInt((point.x - Min.x) / CellSize), Mathf.FloorToInt((point.y - Min.y) / CellSize));
            return index >= 0;
        }

        public bool CanTraverse(int from, int to)
        {
            if (!IsWalkable(from) || !IsWalkable(to)) return false;
            int dx = to % Columns - from % Columns, dy = to / Columns - from / Columns;
            for (int d = 0; d < Directions.Length; d++)
                if (Directions[d].x == dx && Directions[d].y == dy) return (connections[from] & (1 << d)) != 0;
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
