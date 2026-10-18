using System.Collections.Generic;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Fixed world-size modules inside an existing hazard. Never writes physics.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SpikeStripView : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D hazard;
        [SerializeField] private Sprite rowSprite;
        [SerializeField] private Sprite endCapSprite;
        [Tooltip("One source pixel in world units; shared by spikes, spacing and trim.")]
        [SerializeField, Min(0.0001f)] private float unitsPerPixel = 0.0016f;
        [SerializeField] private string sortingLayer = "Ground";
        [SerializeField] private int sortingOrder = 2;

        private Mesh mesh;
        private Material material;
        private Vector2 lastSize, lastOffset, lastScale;
        private bool rebuild = true;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        private Vector2 size;
        private Vector3 center, right, up;
        private Texture texture;

        public Vector2 ModuleSize => new Vector2(165, 180) * unitsPerPixel;
        public Vector2Int ModuleCount { get; private set; }
        public int QuadCount => vertices.Count / 4;

        private void OnEnable() { rebuild = true; Rebuild(); }
        private void OnValidate() { rebuild = true; }
        private void LateUpdate()
        {
            if (hazard == null) return;
            Vector2 scale = AxisScale();
            if (rebuild || mesh == null || hazard.size != lastSize || hazard.offset != lastOffset || scale != lastScale)
                Rebuild();
        }

        private Vector2 AxisScale() => new Vector2(
            hazard.transform.TransformVector(Vector3.right).magnitude,
            hazard.transform.TransformVector(Vector3.up).magnitude);

        [ContextMenu("Rebuild fixed-size spike modules")]
        public void Rebuild()
        {
            if (hazard == null || rowSprite == null || endCapSprite == null) return;
            if (rowSprite.texture != endCapSprite.texture) return;
            var scale = AxisScale();
            if (scale.x < 0.0001f || scale.y < 0.0001f) return;
            unitsPerPixel = Mathf.Max(0.0001f, unitsPerPixel);
            lastSize = hazard.size; lastOffset = hazard.offset; lastScale = scale;
            size = Vector2.Scale(hazard.size, scale);
            center = hazard.transform.TransformPoint(hazard.offset);
            right = hazard.transform.TransformVector(Vector3.right).normalized;
            up = hazard.transform.TransformVector(Vector3.up).normalized;
            texture = rowSprite.texture;
            vertices.Clear(); uvs.Clear(); indices.Clear();

            // Source row has two spikes. Exclude its side rails and use one complete cell.
            Rect cell = new Rect(rowSprite.rect.x + 22, rowSprite.rect.y, 165, 180);
            float rail = Mathf.Min(22 * unitsPerPixel, Mathf.Min(size.x, size.y) / 4);
            float cap = Mathf.Min(endCapSprite.rect.height * unitsPerPixel, Mathf.Max(size.x, size.y) / 4);
            bool vertical = size.y > size.x;
            Rect inner = new Rect(-size.x / 2 + (vertical ? rail : cap),
                -size.y / 2 + (vertical ? cap : rail),
                size.x - 2 * (vertical ? rail : cap), size.y - 2 * (vertical ? cap : rail));
            int columns = Mathf.FloorToInt((inner.width + 0.00001f) / ModuleSize.x);
            int rows = Mathf.FloorToInt((inner.height + 0.00001f) / ModuleSize.y);
            ModuleCount = new Vector2Int(columns, rows);
            float x0 = inner.center.x - columns * ModuleSize.x / 2;
            float y0 = inner.center.y - rows * ModuleSize.y / 2;

            // Complete cells only. Remaining footprint is plate, never a cut or stretched spike.
            // Fill plate margins from the cell's plain edge, then cover its center with modules.
            AddQuad(inner, new Rect(cell.xMax - 1, cell.yMin + 1, 1, 1));
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    AddQuad(new Rect(x0 + x * ModuleSize.x, y0 + y * ModuleSize.y, ModuleSize.x, ModuleSize.y), cell);

            Rect side = new Rect(rowSprite.rect.x, rowSprite.rect.y, 22, 180);
            // A single stripe period avoids repeating the original end plate's screws/corners.
            Rect warning = new Rect(endCapSprite.rect.x + 87, endCapSprite.rect.y, 62, endCapSprite.rect.height);
            if (vertical)
            {
                Tile(new Rect(-size.x / 2, -size.y / 2 + cap, rail, size.y - 2 * cap), side, false);
                Tile(new Rect(size.x / 2 - rail, -size.y / 2 + cap, rail, size.y - 2 * cap), side, false, true);
                Tile(new Rect(-size.x / 2, size.y / 2 - cap, size.x, cap), warning, true);
                Tile(new Rect(-size.x / 2, -size.y / 2, size.x, cap), warning, true, true);
            }
            else
            {
                Tile(new Rect(-size.x / 2 + cap, -size.y / 2, size.x - 2 * cap, rail), side, true, false, true);
                Tile(new Rect(-size.x / 2 + cap, size.y / 2 - rail, size.x - 2 * cap, rail), side, true, true, true);
                Tile(new Rect(-size.x / 2, -size.y / 2, cap, size.y), warning, false, false, true);
                Tile(new Rect(size.x / 2 - cap, -size.y / 2, cap, size.y), warning, false, true, true);
            }

            if (mesh == null) mesh = new Mesh { name = "Spike modules (generated)", hideFlags = HideFlags.HideAndDontSave };
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0);
            var colors = new Color32[vertices.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = colors; mesh.RecalculateBounds();
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Lab spike modules", hideFlags = HideFlags.HideAndDontSave };
            material.mainTexture = texture;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.sortingLayerName = sortingLayer; renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            rebuild = false;
        }

        private void Tile(Rect destination, Rect source, bool horizontal, bool flip = false, bool rotate = false)
        {
            float length = horizontal ? destination.width : destination.height;
            float pitch = (horizontal != rotate ? source.width : source.height) * unitsPerPixel;
            for (float d = 0; d < length - 0.00001f; d += pitch)
            {
                float part = Mathf.Min(pitch, length - d);
                var target = horizontal ? new Rect(destination.x + d, destination.y, part, destination.height)
                    : new Rect(destination.x, destination.y + d, destination.width, part);
                var uv = source;
                if (horizontal != rotate) uv.width *= part / pitch; else uv.height *= part / pitch;
                AddQuad(target, uv, flip, rotate);
            }
        }

        private void AddQuad(Rect destination, Rect source, bool flip = false, bool rotate = false)
        {
            int start = vertices.Count;
            Vector2[] corners = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            foreach (Vector2 corner in corners)
            {
                // Preserve the visual object's Z offset; XY is the collider's exact footprint.
                Vector3 p = center + right * (destination.x + corner.x * destination.width) + up * (destination.y + corner.y * destination.height);
                Vector3 local = transform.InverseTransformPoint(p); local.z = 0;
                vertices.Add(local);
                Vector2 uv = rotate ? new Vector2(corner.y, 1 - corner.x) : corner;
                if (flip) uv = new Vector2(1 - uv.x, 1 - uv.y);
                uvs.Add(new Vector2((source.x + uv.x * source.width) / texture.width, (source.y + uv.y * source.height) / texture.height));
            }
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }

        private void OnDisable()
        {
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            mesh = null; material = null;
        }
    }
}
