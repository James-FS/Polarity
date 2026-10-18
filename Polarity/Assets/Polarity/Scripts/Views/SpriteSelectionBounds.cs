using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Authored visual selection area; does not change rendering or physics.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteSelectionBounds : MonoBehaviour
    {
        [Tooltip("Selection rectangle in sprite pixels, measured from its bottom-left corner. Exclude shadow and decorative particles.")]
        [SerializeField] private Rect pixelRect = new Rect(14f, 12f, 37f, 37f);
        private SpriteRenderer spriteRenderer;

        public Rect PixelRect => pixelRect;

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null || pixelRect.width <= 0f || pixelRect.height <= 0f)
                return false;

            float left = Mathf.Clamp(pixelRect.xMin, 0f, sprite.rect.width);
            float right = Mathf.Clamp(pixelRect.xMax, 0f, sprite.rect.width);
            float bottom = Mathf.Clamp(pixelRect.yMin, 0f, sprite.rect.height);
            float top = Mathf.Clamp(pixelRect.yMax, 0f, sprite.rect.height);
            if (right <= left || top <= bottom)
                return false;

            for (int i = 0; i < 4; i++)
            {
                Vector2 pixel = new Vector2((i & 1) == 0 ? left : right,
                    (i & 2) == 0 ? bottom : top);
                Vector2 local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
                if (spriteRenderer.flipX) local.x = -local.x;
                if (spriteRenderer.flipY) local.y = -local.y;
                Vector3 point = transform.TransformPoint(new Vector3(local.x, local.y, 0f));
                if (i == 0) bounds = new Bounds(point, Vector3.zero);
                else bounds.Encapsulate(point);
            }
            return true;
        }
    }
}
