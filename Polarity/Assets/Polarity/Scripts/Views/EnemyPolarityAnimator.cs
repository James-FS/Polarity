using Polarity.Entities;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Drives polarity and facing from gameplay state without changing simulation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class EnemyPolarityAnimator : MonoBehaviour
    {
        private static readonly int PolarityParameter = Animator.StringToHash("Polarity");
        private static readonly int DirectionParameter = Animator.StringToHash("Direction");
        // Switching axes requires a clear lead, so near-diagonal steering keeps its facing.
        private const float AxisSwitchRatio = 1.15f;
        [SerializeField] private Rect sideSelectionRect = new Rect(12f, 14f, 40f, 36f);
        private SpriteSelectionBounds selectionBounds;
        private PolarityBody body;
        private EnemyMotor motor;
        private Animator animator;
        private int facing;
        private int appliedPolarity;
        private int appliedDirection;

        private void Awake()
        {
            body = GetComponentInParent<PolarityBody>();
            motor = GetComponentInParent<EnemyMotor>();
            animator = GetComponent<Animator>();
            selectionBounds = GetComponent<SpriteSelectionBounds>();
        }

        private void OnEnable()
        {
            facing = 0;
            appliedPolarity = appliedDirection = -1;
        }

        private void Update()
        {
            if (body == null || animator == null)
                return;

            // Steering expresses intended facing even when magnetic forces push backwards.
            if (motor != null)
                UpdateFacing(motor.LastSteeringDirection);

            int polarity = (int)body.CurrentPolarity;
            if (polarity != appliedPolarity)
            {
                animator.SetInteger(PolarityParameter, polarity);
                appliedPolarity = polarity;
            }
            if (facing != appliedDirection)
            {
                animator.SetInteger(DirectionParameter, facing);
                if (selectionBounds != null)
                    selectionBounds.SetPixelRectOverride(facing >= 2 ? sideSelectionRect : (Rect?)null);
                appliedDirection = facing;
            }
        }

        private void OnDisable()
        {
            if (selectionBounds != null)
                selectionBounds.SetPixelRectOverride(null);
        }

        private void UpdateFacing(Vector2 steering)
        {
            if (steering.sqrMagnitude <= 0.0001f)
                return;

            float x = Mathf.Abs(steering.x);
            float y = Mathf.Abs(steering.y);
            bool horizontal = facing == 2 || facing == 3;
            if (horizontal && y > x * AxisSwitchRatio)
                horizontal = false;
            else if (!horizontal && x > y * AxisSwitchRatio)
                horizontal = true;

            // 0 = front/down, 1 = back/up, 2 = left, 3 = right.
            facing = horizontal ? (steering.x > 0f ? 3 : 2) : (steering.y > 0f ? 1 : 0);
        }
    }
}
