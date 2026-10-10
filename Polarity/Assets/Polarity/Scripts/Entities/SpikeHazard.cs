using UnityEngine;
namespace Polarity.Entities
{
    /// <summary>Trigger adapter only. The World decides damage/cooldowns; spikes never spread.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Collider2D))]
    public sealed class SpikeHazard : MonoBehaviour
    {
        [SerializeField, Min(1)] private int hazardId=1;
        private Collider2D triggerCollider;
        public Collider2D TriggerCollider => triggerCollider != null ? triggerCollider : (triggerCollider=GetComponent<Collider2D>());
        public int HazardId => -Mathf.Max(1,hazardId);
        private void OnTriggerEnter2D(Collider2D other) => Capture(other);
        private void OnTriggerStay2D(Collider2D other) => Capture(other);
        private void Capture(Collider2D other)
        {
            if (!isActiveAndEnabled || other.attachedRigidbody==null) return;
            var health=other.attachedRigidbody.GetComponent<Health>();
            if (health!=null && health.World!=null) health.World.RecordSpike(this,health);
        }
    }
}
