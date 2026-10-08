using UnityEngine;
using UnityEngine.Rendering;

namespace Polarity.Views
{
    /// <summary>Sorts a visual group using its ground anchor, without changing physics.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class YSortView : MonoBehaviour
    {
        [SerializeField] private Transform footAnchor;
        [SerializeField, Range(-9, 9)] private int orderOffset;
        private SortingGroup sortingGroup;

        private void OnEnable() => UpdateSorting();
        private void OnValidate() => UpdateSorting();
        private void LateUpdate() => UpdateSorting();

        private void UpdateSorting()
        {
            if (sortingGroup == null)
                sortingGroup = GetComponent<SortingGroup>();

            Transform anchor = footAnchor != null ? footAnchor : transform.parent;
            if (anchor == null)
                anchor = transform;

            float y = anchor.position.y;
            if (float.IsNaN(y) || float.IsInfinity(y))
                return;

            float scaledOrder = Mathf.Clamp(-y * 100f, -32760f, 32760f);
            int order = Mathf.Clamp(Mathf.RoundToInt(scaledOrder) + orderOffset, -32760, 32760);
            if (sortingGroup.sortingOrder != order)
                sortingGroup.sortingOrder = order;
        }
    }
}
