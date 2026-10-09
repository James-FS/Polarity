using UnityEngine;

namespace Polarity.Controllers
{
    /// <summary>Only samples intent. SimulationWorld owns all movement forces.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float moveAcceleration = 35f;
        private Rigidbody2D rigidBody;
        public Rigidbody2D Rigidbody => rigidBody != null ? rigidBody : (rigidBody = GetComponent<Rigidbody2D>());
        public Vector2 MoveIntent { get; private set; }
        public float MoveSpeed => moveSpeed;
        public float MoveAcceleration => moveAcceleration;

        private void Update() => SampleMoveInput(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.A),
            Input.GetKey(KeyCode.S), Input.GetKey(KeyCode.D));

        public void SampleMoveInput(bool w, bool a, bool s, bool d)
        {
            MoveIntent = Vector2.ClampMagnitude(new Vector2((d ? 1 : 0) - (a ? 1 : 0),
                (w ? 1 : 0) - (s ? 1 : 0)), 1f);
        }

        internal void ClearIntent() => MoveIntent = Vector2.zero;
        private void OnDisable() => ClearIntent();
        private void OnApplicationFocus(bool focused) { if (!focused) ClearIntent(); }
    }
}
