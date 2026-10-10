using UnityEngine;
namespace Polarity.Entities
{
    /// <summary>Copies numeric collision records only, for polarity entities or the nonmagnetic player.</summary>
    [DisallowMultipleComponent]
    public sealed class ContactRecorder : MonoBehaviour
    {
        private PolarityBody body;
        private Health health;
        private void Awake() { body=GetComponent<PolarityBody>(); health=GetComponent<Health>(); }
        private void OnCollisionEnter2D(Collision2D collision) => Capture(collision);
        private void OnCollisionStay2D(Collision2D collision) => Capture(collision);
        private void Capture(Collision2D collision)
        {
            if (!isActiveAndEnabled) return;
            var world=body!=null ? body.World : health!=null ? health.World : null;
            if (world!=null) world.RecordCollision(collision);
        }
    }
}
