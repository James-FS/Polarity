using Polarity.Entities;
using Polarity.Model;
using Polarity.Simulation;
using Polarity.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Controllers
{
    /// <summary>Chooses visible body faces and submits intent; never changes polarity or budget.</summary>
    [DisallowMultipleComponent]
    public sealed class MarkingController : MonoBehaviour
    {
        [SerializeField] private SimulationWorld world;
        [SerializeField] private Camera worldCamera;
        [Tooltip("Explicit wall/player body faces, excluding footprints, shadows and decoration.")]
        [SerializeField] private SpriteRenderer[] occludingSprites = new SpriteRenderer[0];
        [SerializeField] private MarkingDebugView debugView;
        public PolarityBody Candidate { get; private set; }
        public string PointerFeedback { get; private set; } = "Point at an enemy or crate";
        public Camera WorldCamera => worldCamera;

        private void Update()
        {
            Vector2 point = Input.mousePosition;
            bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (debugView != null && debugView.ContainsScreenPoint(point));
            ProcessPointerFrame(point, Input.GetMouseButtonDown(0), Input.GetMouseButtonDown(1), overUI);
        }

        // One call per input frame; button flags are press edges, never held states.
        public void ProcessPointerFrame(Vector2 point, bool positivePressed, bool negativePressed, bool overUI = false)
        {
            Candidate = null;
            if (world == null || world.State == null || world.State.Phase != LevelPhase.Running || overUI)
            {
                PointerFeedback = overUI ? "UI" : "Not running";
                return;
            }
            Candidate = FindCandidate(point, out bool visuallyBlocked);
            PointerFeedback = visuallyBlocked ? "Body hidden by wall or player" :
                Candidate != null ? Candidate.name : "No markable body";
            if (positivePressed) world.SubmitMark(Candidate != null ? Candidate.BodyId : 0, Pole.Positive);
            if (negativePressed) world.SubmitMark(Candidate != null ? Candidate.BodyId : 0, Pole.Negative);
        }

        public PolarityBody FindCandidate(Vector2 point, out bool visuallyBlocked)
        {
            visuallyBlocked = false;
            if (world == null || worldCamera == null || !worldCamera.pixelRect.Contains(point)) return null;
            PolarityBody best = null;
            SpriteRenderer bestFace = null;
            int enemy = LayerMask.NameToLayer("Enemy"), crate = LayerMask.NameToLayer("Crate");
            foreach (var body in world.Bodies)
            {
                if (body == null || !body.IsValid || !body.CanMark ||
                    (body.gameObject.layer != enemy && body.gameObject.layer != crate)) continue;
                var view = body.GetComponent<PolarityView>();
                if (view == null) continue;
                foreach (var face in view.BodySprites)
                {
                    if (!TryScreenRect(face, worldCamera, out Rect rect) || !rect.Contains(point)) continue;
                    int comparison = bestFace == null ? 1 : CompareDrawOrder(face, bestFace);
                    if (comparison > 0 || (comparison == 0 && best != null && body.BodyId < best.BodyId))
                    { best = body; bestFace = face; }
                }
            }
            if (bestFace == null) return null;
            foreach (var face in occludingSprites)
            {
                if (TryScreenRect(face, worldCamera, out Rect rect) && rect.Contains(point) && CompareDrawOrder(face, bestFace) >= 0)
                { visuallyBlocked = true; return null; }
            }
            return best;
        }

        public static bool TryScreenRect(SpriteRenderer face, Camera camera, out Rect rect)
        {
            rect = default;
            if (face == null || camera == null || !face.enabled || !face.gameObject.activeInHierarchy ||
                face.sprite == null || face.color.a <= 0.01f || (camera.cullingMask & (1 << face.gameObject.layer)) == 0)
                return false;
            Bounds bounds = face.bounds;
            Vector3 a = camera.WorldToScreenPoint(bounds.min), b = camera.WorldToScreenPoint(bounds.max);
            if (a.z <= 0 || b.z <= 0) return false;
            rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return rect.Overlaps(camera.pixelRect);
        }

        private static int CompareDrawOrder(SpriteRenderer a, SpriteRenderer b)
        {
            var ga = a.GetComponentInParent<SortingGroup>();
            var gb = b.GetComponentInParent<SortingGroup>();
            int la = SortingLayer.GetLayerValueFromID(ga != null ? ga.sortingLayerID : a.sortingLayerID);
            int lb = SortingLayer.GetLayerValueFromID(gb != null ? gb.sortingLayerID : b.sortingLayerID);
            int comparison = la.CompareTo(lb);
            if (comparison != 0) return comparison;
            comparison = (ga != null ? ga.sortingOrder : a.sortingOrder).CompareTo(gb != null ? gb.sortingOrder : b.sortingOrder);
            if (comparison != 0) return comparison;
            // Internal orders only compare sprites within the same group.
            return ga == gb ? a.sortingOrder.CompareTo(b.sortingOrder) : 0;
        }

        private void OnDisable() => Candidate = null;
    }
}
