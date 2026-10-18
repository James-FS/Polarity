using Polarity.Controllers;
using UnityEngine;

namespace Polarity.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerFacingView : MonoBehaviour
    {
        private PlayerController player;
        private Animator animator;
        private int facing; // 默认0，正面

        private static readonly int Direction =
            Animator.StringToHash("Direction");

        private void Awake()
        {
            player = GetComponentInParent<PlayerController>();
            animator = GetComponent<Animator>();
        }

        private void LateUpdate()
        {
            if (player == null)
                return;

            Vector2 move = player.MoveIntent;

            // 没有输入时保留最后朝向。
            if (move.sqrMagnitude > 0.0001f)
            {
                if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
                    facing = move.x > 0f ? 3 : 2;
                else
                    facing = move.y > 0f ? 1 : 0;
            }

            animator.SetInteger(Direction, facing);
        }
    }
}