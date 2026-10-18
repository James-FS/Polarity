using System;
using System.Collections.Generic;
using Polarity.Entities;
using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Config
{
    /// <summary>Room-local grid configuration and preview. World owns runtime update timing.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Polarity/Navigation Grid Authoring")]
    public sealed class NavigationGridAuthoring : MonoBehaviour
    {
        [Header("Grid Bounds (local XY, world-unit scale)")]
        [Tooltip("网格中心，相对于 Navigation 的本地 XY 坐标。")]
        [SerializeField] private Vector2 boundsCenter = Vector2.zero;
        [Tooltip("网格覆盖的房间内部范围；边缘不足一格时使用较窄的末格。")]
        [SerializeField] private Vector2 boundsSize = new Vector2(20f, 12f);
        [Tooltip("网格间距；不改变角色的连续物理运动。")]
        [SerializeField, Min(0.05f)] private float cellSize = 0.5f;

        [Header("Enemy Footprint")]
        [Tooltip("引用本关敌人的脚下圆形碰撞体，只读取尺寸，不移动它。")]
        [SerializeField] private CircleCollider2D enemyCollider;
        [Tooltip("加在敌人世界半径上的安全余量；后续占位检测只膨胀一次。")]
        [SerializeField, Min(0f)] private float clearanceMargin = 0.05f;

        [Header("Query Layers")]
        [Tooltip("实体墙和固定炮台；后续采样忽略 Trigger。")]
        [SerializeField] private LayerMask staticObstacleMask;
        [Tooltip("可移动箱子；后续单独维护动态占位。")]
        [SerializeField] private LayerMask dynamicObstacleMask;
        [Tooltip("尖刺；后续采样必须包含 Trigger，导航禁行不改变物理碰撞。")]
        [SerializeField] private LayerMask hazardMask;

        [Header("Path Search")]
        [SerializeField, Min(1)] private int searchBudgetPerStep = 512;
        [SerializeField, Min(1)] private int perEnemySearchBudget = 128;
        [SerializeField, Min(0.02f)] private float replanInterval = 0.3f;
        [SerializeField, Min(0.05f)] private float targetMoveThreshold = 0.6f;
        [Tooltip("编辑模式路径预览终点，Navigation本地XY坐标。")]
        [SerializeField] private Vector2 pathPreviewTarget = new Vector2(3f, 2f);
        [SerializeField] private bool showPaths = true;

        [Header("Grid Preview")]
        [Tooltip("选中 Navigation 时显示网格；编辑模式修改几何后需点击重建预览。")]
        [SerializeField] private bool showGrid = true;
        [Tooltip("在引用敌人脚下显示身体半径与安全余量。")]
        [SerializeField] private bool showClearance;
        [Tooltip("显示指定格子的有效相邻连线；节点索引为行优先。")]
        [SerializeField] private bool showConnections;
        [SerializeField, Min(0)] private int connectionCellIndex;

        [NonSerialized] private NavigationGridSampler previewSampler;
        [NonSerialized] private NavigationGridSampler runtimeSampler;
        [NonSerialized] private AStarPathfinder previewSearch;
        [NonSerialized] private List<Vector2> previewPath = new List<Vector2>();
        [NonSerialized] private SimulationWorld runtimeWorld;
        public NavigationGridSampler Sampler => Application.isPlaying ? runtimeSampler : previewSampler;
        public NavigationGrid Grid => Sampler?.Grid;
        public int SearchBudgetPerStep => Mathf.Max(1, searchBudgetPerStep);
        public int PerEnemySearchBudget => Mathf.Max(1, perEnemySearchBudget);
        public float ReplanInterval => replanInterval;
        public float TargetMoveThreshold => targetMoveThreshold;
        public AStarPathfinder PreviewSearch => previewSearch;
        public IReadOnlyList<Vector2> PreviewPath => previewPath;

        public void BuildPathPreview()
        {
            RebuildPreview();
            previewSearch = new AStarPathfinder();
            Vector2 start = enemyCollider.transform.TransformPoint(enemyCollider.offset);
            Vector2 target = transform.TransformPoint(pathPreviewTarget);
            int entry;
            bool found = Grid.TryGetCell(start, out entry) && Grid.IsWalkable(entry) && Sampler.IsSegmentSafe(start, Grid.GetCenter(entry));
            if (!found) found = Sampler.TryFindEntry(start, 1.5f, false, out entry);
            previewSearch.Begin(Grid, found ? entry : -1, target);
            while (previewSearch.Status == PathSearchStatus.Pending) previewSearch.Step(PerEnemySearchBudget);
            previewPath.Add(start);
            foreach (int index in previewSearch.Path) previewPath.Add(Grid.GetCenter(index));
            if (!previewSearch.IsPartial && previewPath.Count > 1 && Sampler.IsSegmentSafe(previewPath[previewPath.Count - 1], target))
                previewPath.Add(target);
        }

        public Vector2 BoundsCenter => boundsCenter;
        public Vector2 BoundsSize => boundsSize;
        public float CellSize => cellSize;
        public CircleCollider2D EnemyCollider => enemyCollider;
        public float ClearanceMargin => clearanceMargin;
        public LayerMask StaticObstacleMask => staticObstacleMask;
        public LayerMask DynamicObstacleMask => dynamicObstacleMask;
        public LayerMask HazardMask => hazardMask;
        public int Columns => Mathf.CeilToInt(boundsSize.x / cellSize);
        public int Rows => Mathf.CeilToInt(boundsSize.y / cellSize);
        public float EnemyWorldRadius => enemyCollider == null ? 0f : enemyCollider.radius *
            Mathf.Max(Mathf.Abs(enemyCollider.transform.lossyScale.x), Mathf.Abs(enemyCollider.transform.lossyScale.y));
        public float QueryRadius => EnemyWorldRadius + clearanceMargin;

        public bool HasValidConfiguration =>
            IsFinite(boundsCenter.x) && IsFinite(boundsCenter.y) &&
            IsFinite(boundsSize.x) && IsFinite(boundsSize.y) && boundsSize.x > 0f && boundsSize.y > 0f &&
            IsFinite(cellSize) && cellSize >= 0.05f &&
            Math.Ceiling(boundsSize.x / (double)cellSize) * Math.Ceiling(boundsSize.y / (double)cellSize) <= NavigationGrid.MaxCells &&
            IsFinite(clearanceMargin) && clearanceMargin >= 0f && enemyCollider != null &&
            IsFinite(EnemyWorldRadius) && EnemyWorldRadius > 0f &&
            staticObstacleMask.value != 0 && dynamicObstacleMask.value != 0 && hazardMask.value != 0 &&
            (staticObstacleMask.value & dynamicObstacleMask.value) == 0 &&
            (staticObstacleMask.value & hazardMask.value) == 0 &&
            (dynamicObstacleMask.value & hazardMask.value) == 0 &&
            Quaternion.Angle(transform.rotation, Quaternion.identity) < 0.01f &&
            (transform.lossyScale - Vector3.one).sqrMagnitude < 0.000001f;

        public void RebuildPreview()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use World-owned runtime sampling in Play mode.");
            previewSampler = null;
            previewSearch = null; previewPath.Clear();
            Physics2D.SyncTransforms();
            previewSampler = new NavigationGridSampler(this, RoomRoot());
        }

        internal void InitializeRuntime(SimulationWorld world)
        {
            previewSampler = null;
            runtimeWorld = world;
            Physics2D.SyncTransforms();
            runtimeSampler = new NavigationGridSampler(this, RoomRoot());
        }

        internal void RefreshRuntime() => runtimeSampler?.RefreshDynamic();
        internal void ClearRuntime() { runtimeSampler = null; runtimeWorld = null; }

        private Transform RoomRoot()
        {
            var room = GetComponentInParent<LevelRoom>();
            if (room == null) throw new InvalidOperationException("Navigation must belong to a LevelRoom.");
            return room.transform;
        }

        private void OnValidate()
        {
            if (!IsFinite(boundsCenter.x) || !IsFinite(boundsCenter.y)) boundsCenter = Vector2.zero;
            boundsSize.x = IsFinite(boundsSize.x) ? Mathf.Max(0.05f, boundsSize.x) : 20f;
            boundsSize.y = IsFinite(boundsSize.y) ? Mathf.Max(0.05f, boundsSize.y) : 12f;
            cellSize = IsFinite(cellSize) ? Mathf.Max(0.05f, cellSize) : 0.5f;
            clearanceMargin = IsFinite(clearanceMargin) ? Mathf.Max(0f, clearanceMargin) : 0.05f;
            connectionCellIndex = Mathf.Max(0, connectionCellIndex);
            previewSampler = null;
            previewSearch = null;
            previewPath?.Clear();
            searchBudgetPerStep = Mathf.Max(1, searchBudgetPerStep);
            perEnemySearchBudget = Mathf.Max(1, perEnemySearchBudget);
            replanInterval = IsFinite(replanInterval) ? Mathf.Max(0.02f,replanInterval) : 0.3f;
            targetMoveThreshold = IsFinite(targetMoveThreshold) ? Mathf.Max(0.05f,targetMoveThreshold) : 0.6f;
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasValidConfiguration && Grid == null) return;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.55f);
            Vector2 min = boundsCenter - boundsSize * 0.5f;
            Vector2 max = boundsCenter + boundsSize * 0.5f;
            if (showGrid)
            {
                var grid = Grid;
                if (grid != null)
                {
                    // Keep cell colors in the world XY plane, even with the game's tilted presentation.
                    Gizmos.matrix = Matrix4x4.identity;
                    for (int i = 0; i < grid.Count; i++)
                    {
                        NavigationCell state = grid.GetCell(i);
                        Gizmos.color = (state & (NavigationCell.Boundary | NavigationCell.StaticObstacle)) != 0 ? new Color(0.5f,0.5f,0.5f,0.6f) :
                            (state & NavigationCell.Hazard) != 0 ? new Color(1f,0.2f,0.2f,0.65f) :
                            (state & NavigationCell.DynamicObstacle) != 0 ? new Color(1f,0.6f,0.1f,0.65f) : new Color(0.2f,0.9f,0.45f,0.35f);
                        Vector2 size = grid.GetCellSize(i) * 0.88f;
                        Gizmos.DrawWireCube(grid.GetCenter(i), new Vector3(size.x,size.y,0f));
                    }
                    if (showConnections && connectionCellIndex < grid.Count)
                    {
                        Gizmos.color = Color.cyan;
                        Vector2 start = grid.GetCenter(connectionCellIndex);
                        Gizmos.DrawWireSphere(start, 0.1f);
                        int x = connectionCellIndex % grid.Columns, y = connectionCellIndex / grid.Columns;
                        for (int d = 0; d < NavigationGrid.Directions.Length; d++)
                        {
                            Vector2Int offset = NavigationGrid.Directions[d];
                            int next = grid.Index(x + offset.x, y + offset.y);
                            if (grid.CanTraverse(connectionCellIndex, next)) Gizmos.DrawLine(start, grid.GetCenter(next));
                        }
                    }
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.color = new Color(0.25f,0.8f,1f,0.55f);
                }
                Gizmos.DrawWireCube(boundsCenter, new Vector3(boundsSize.x, boundsSize.y, 0f));
                // Keep accidental tiny cells / large bounds from freezing the editor preview.
                if (grid == null && (long)Columns + Rows <= 4096)
                {
                    for (int x = 1; x < Columns; x++)
                    {
                        float position = min.x + x * cellSize;
                        Gizmos.DrawLine(new Vector3(position, min.y, 0f), new Vector3(position, max.y, 0f));
                    }
                    for (int y = 1; y < Rows; y++)
                    {
                        float position = min.y + y * cellSize;
                        Gizmos.DrawLine(new Vector3(min.x, position, 0f), new Vector3(max.x, position, 0f));
                    }
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
            if (showPaths)
            {
                Gizmos.color = Color.magenta;
                if (!Application.isPlaying && previewPath != null) DrawPath(previewPath);
                if (Application.isPlaying && runtimeWorld != null)
                    foreach (var entry in runtimeWorld.EnemyNavigators) DrawPath(entry.Value.Path);
            }
            if (showClearance && enemyCollider != null)
            {
                Vector3 center = enemyCollider.transform.TransformPoint(enemyCollider.offset);
                Gizmos.color = Color.yellow;
                Vector3 previous = center + Vector3.right * QueryRadius;
                for (int i = 1; i <= 48; i++)
                {
                    float angle = i * Mathf.PI * 2f / 48;
                    Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * QueryRadius;
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }
            }
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static void DrawPath(IReadOnlyList<Vector2> points)
        {
            for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1], points[i]);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
