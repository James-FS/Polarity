using System.Collections.Generic;
using Polarity.Controllers;
using Polarity.Entities;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Visual contour only: reads sprite outline data, never creates a collider.</summary>
    [DisallowMultipleComponent]
    public sealed class SpriteSelectionOutline : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodySprite;
        [Tooltip("Shared silhouette keeps the border stable across polarity states.")]
        [SerializeField] private Sprite outlineSprite;
        [SerializeField, Min(1f)] private float widthPixels = 2f;
        [SerializeField] private Color color = Color.yellow;
        private PolarityBody body;
        private MarkingController marking;
        private LineRenderer line;
        private Material material;
        private readonly List<Vector2> points = new List<Vector2>();
        public bool IsVisible => line != null && line.enabled;
        public int PointCount => line != null ? line.positionCount : 0;

        private void OnEnable()
        {
            body = GetComponent<PolarityBody>();
            marking = FindObjectOfType<MarkingController>();
        }

        private void LateUpdate() => RefreshOutline();

        public void RefreshOutline()
        {
            bool visible = marking != null && marking.Candidate == body && body != null && body.IsValid &&
                bodySprite != null && bodySprite.enabled && bodySprite.gameObject.activeInHierarchy;
            if (!visible) { if (line != null) line.enabled = false; return; }
            if (line == null && !CreateLine()) return;
            line.enabled = true;
            line.sortingLayerID = bodySprite.sortingLayerID;
            line.sortingOrder = bodySprite.sortingOrder + 1;
            line.startColor = line.endColor = color;
            Camera camera = marking.WorldCamera;
            if (camera != null && camera.pixelHeight > 0)
            {
                float worldHeight = camera.orthographic ? 2f * camera.orthographicSize :
                    2f * Vector3.Distance(camera.transform.position, bodySprite.transform.position) *
                    Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
                line.widthMultiplier = worldHeight * widthPixels / camera.pixelHeight;
            }
        }

        private bool CreateLine()
        {
            Sprite sprite = outlineSprite != null ? outlineSprite : bodySprite.sprite;
            if (sprite == null || sprite.GetPhysicsShapeCount() == 0) return false;
            sprite.GetPhysicsShape(0, points);
            if (points.Count < 3) return false;
            var child = new GameObject("SelectionContour");
            child.transform.SetParent(bodySprite.transform, false);
            line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.View;
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0));
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            material = new Material(Shader.Find("Sprites/Default")) { name = "Selection contour (runtime)" };
            line.sharedMaterial = material;
            return true;
        }

        private void OnDisable()
        {
            if (line != null) Destroy(line.gameObject);
            if (material != null) Destroy(material);
            line = null;
            material = null;
        }
    }
}
