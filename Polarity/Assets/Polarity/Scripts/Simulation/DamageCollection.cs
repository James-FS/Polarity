using System;
using System.Collections.Generic;
using Polarity.Entities;
using Polarity.Model;
using UnityEngine;
namespace Polarity.Simulation
{
    /// <summary>Copies impact numbers and merges mirror/collider reports, separately from polarity contacts.</summary>
    internal sealed class DamageCollection
    {
        private ContactPoint2D[] buffer = new ContactPoint2D[8];
        private Collider2D[] triggerBuffer = new Collider2D[8];
        private readonly Dictionary<(int,int,DamageKind), DamageContact> observations = new Dictionary<(int,int,DamageKind), DamageContact>();
        private readonly List<DamageContact> contacts = new List<DamageContact>();
        private readonly IReadOnlyList<DamageContact> readOnlyContacts;
        private int enemyLayer,crateLayer,wallLayer,playerLayer;
        public void ConfigureLayers() { enemyLayer=LayerMask.NameToLayer("Enemy"); crateLayer=LayerMask.NameToLayer("Crate"); wallLayer=LayerMask.NameToLayer("Wall"); playerLayer=LayerMask.NameToLayer("Player"); }
        public IReadOnlyList<DamageContact> Contacts => readOnlyContacts;
        public int BufferCapacity => buffer.Length;
        public int BufferExpansionCount { get; private set; }
        public DamageCollection() => readOnlyContacts = contacts.AsReadOnly();
        public void Clear() { observations.Clear(); contacts.Clear(); }
        public void CaptureCollision(Collision2D collision, IReadOnlyDictionary<Rigidbody2D,Health> health, IReadOnlyDictionary<Rigidbody2D,PolarityBody> bodies)
        { for (int i=0;i<collision.contactCount;i++) AddPoint(collision.GetContact(i),health,bodies); }
        public void CapturePersistent(IReadOnlyDictionary<Rigidbody2D,Health> health, IReadOnlyDictionary<Rigidbody2D,PolarityBody> bodies,IReadOnlyList<SpikeHazard> spikes)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            foreach (var entry in health)
            {
                if (entry.Key == null || !entry.Value.IsValid || !entry.Key.simulated) continue;
                int count;
                while ((count = entry.Key.GetContacts(filter,buffer)) == buffer.Length)
                { Array.Resize(ref buffer, checked(buffer.Length*2)); BufferExpansionCount++; }
                for (int i=0;i<count;i++) AddPoint(buffer[i],health,bodies);
            }
            var triggerFilter = new ContactFilter2D(); triggerFilter.SetLayerMask((1<<enemyLayer)|(1<<playerLayer)); triggerFilter.useTriggers=false;
            foreach(var spike in spikes)
            {
                if(spike==null || !spike.isActiveAndEnabled || !spike.TriggerCollider.enabled || !spike.TriggerCollider.isTrigger) continue;
                int count;
                while((count=spike.TriggerCollider.OverlapCollider(triggerFilter,triggerBuffer))==triggerBuffer.Length)
                    Array.Resize(ref triggerBuffer,checked(triggerBuffer.Length*2));
                for(int i=0;i<count;i++)
                    if(triggerBuffer[i].attachedRigidbody!=null && health.TryGetValue(triggerBuffer[i].attachedRigidbody,out var target)) CaptureSpike(spike,target);
            }
            contacts.Clear(); contacts.AddRange(observations.Values);
            contacts.Sort((a,b)=>a.Target.ActorId!=b.Target.ActorId ? a.Target.ActorId.CompareTo(b.Target.ActorId) : a.SourceId!=b.SourceId ? a.SourceId.CompareTo(b.SourceId) : a.Kind.CompareTo(b.Kind));
        }
        private void AddPoint(ContactPoint2D point,IReadOnlyDictionary<Rigidbody2D,Health> health,IReadOnlyDictionary<Rigidbody2D,PolarityBody> bodies)
        {
            if (point.collider == null || point.otherCollider == null || point.collider.isTrigger || point.otherCollider.isTrigger) return;
            float speed = Mathf.Abs(Vector2.Dot(point.relativeVelocity,point.normal));
            if (float.IsNaN(speed)||float.IsInfinity(speed)) return;
            AddSide(point.collider,point.otherCollider,speed,point.point,health,bodies);
            AddSide(point.otherCollider,point.collider,speed,point.point,health,bodies);
        }
        private void AddSide(Collider2D target,Collider2D source,float speed,Vector2 point,IReadOnlyDictionary<Rigidbody2D,Health> health,IReadOnlyDictionary<Rigidbody2D,PolarityBody> bodies)
        {
            if (target.attachedRigidbody==null || !health.TryGetValue(target.attachedRigidbody,out var victim) || !victim.IsValid) return;
            int targetLayer = target.gameObject.layer, sourceLayer = source.gameObject.layer;
            Health sourceHealth = null;
            if (source.attachedRigidbody != null) health.TryGetValue(source.attachedRigidbody,out sourceHealth);
            if (sourceLayer==enemyLayer && (sourceHealth==null || !sourceHealth.IsValid)) return;
            int sourceId = sourceHealth != null ? sourceHealth.ActorId : 0;
            if (source.attachedRigidbody!=null && bodies.TryGetValue(source.attachedRigidbody,out var sourceBody)) sourceId=sourceBody.BodyId;
            if (targetLayer==playerLayer && sourceLayer==enemyLayer && sourceHealth!=null && sourceHealth.IsValid)
                Add(new DamageContact(victim,sourceHealth,sourceId,DamageKind.EnemyTouch,0f,point));
            else if (targetLayer==enemyLayer && (sourceLayer==enemyLayer || sourceLayer==crateLayer || sourceLayer==wallLayer))
                Add(new DamageContact(victim,sourceHealth,sourceId,DamageKind.Impact,speed,point));
        }
        public void CaptureSpike(SpikeHazard spike,Health target)
        {
            if (spike==null || !spike.isActiveAndEnabled || target==null || !target.IsValid) return;
            int layer = target.gameObject.layer;
            if (layer==enemyLayer || layer==playerLayer)
                Add(new DamageContact(target,null,spike.HazardId,DamageKind.Spike,0f,target.Rigidbody.position));
        }
        private void Add(DamageContact contact)
        {
            var key=(contact.Target.ActorId,contact.SourceId,contact.Kind);
            if (!observations.TryGetValue(key,out var previous) || contact.NormalSpeed>previous.NormalSpeed) observations[key]=contact;
        }
    }
}
