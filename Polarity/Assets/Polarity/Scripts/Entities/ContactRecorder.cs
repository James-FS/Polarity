using UnityEngine;

namespace Polarity.Entities
{
    /// <summary>Copies callback data during the World-owned physics step; never changes gameplay state.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PolarityBody))]
    public sealed class ContactRecorder : MonoBehaviour
    {
        private PolarityBody body;
        private void Awake() => body = GetComponent<PolarityBody>();
        private void OnCollisionEnter2D(Collision2D collision) => Capture(collision);
        private void OnCollisionStay2D(Collision2D collision) => Capture(collision);
        private void Capture(Collision2D collision)
        {
            if (!isActiveAndEnabled) return;
            if (body == null) body = GetComponent<PolarityBody>();
            if (body.IsValid && body.World != null)
                body.World.RecordCollision(collision);
        }
    }
}
