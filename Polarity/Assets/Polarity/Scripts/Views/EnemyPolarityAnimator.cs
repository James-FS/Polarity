using Polarity.Entities;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Drives authored idle states from gameplay polarity without changing simulation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class EnemyPolarityAnimator : MonoBehaviour
    {
        private static readonly int PolarityParameter = Animator.StringToHash("Polarity");
        private PolarityBody body;
        private Animator animator;
        private int appliedPolarity;

        private void Awake()
        {
            body = GetComponentInParent<PolarityBody>();
            animator = GetComponent<Animator>();
        }

        private void OnEnable() => appliedPolarity = -1;

        private void Update()
        {
            if (body == null || animator == null)
                return;

            int polarity = (int)body.CurrentPolarity;
            if (polarity == appliedPolarity)
                return;

            animator.SetInteger(PolarityParameter, polarity);
            appliedPolarity = polarity;
        }
    }
}
