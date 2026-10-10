using System;
using System.Collections.Generic;
using Polarity.Entities;
using UnityEngine;
namespace Polarity.Simulation
{
    /// <summary>Merge callback and persistent hits; choose exactly one contact for each shot.</summary>
    internal sealed class ProjectileCollection
    {
        internal readonly struct Hit
        {
            public readonly Projectile Projectile;
            public readonly Health Target;
            public readonly Vector2 Point;
            public readonly float Distance;
            public readonly int TieId;
            public Hit(Projectile projectile, Health target, Vector2 point, int tieId)
            { Projectile=projectile; Target=target; Point=point; TieId=tieId; Distance=(point-projectile.StepStartPosition).sqrMagnitude; }
        }
        private readonly Dictionary<Projectile,Hit> hits = new Dictionary<Projectile,Hit>();
        private ContactPoint2D[] buffer = new ContactPoint2D[8];
        public IEnumerable<Hit> Hits => hits.Values;
        public int BufferCapacity => buffer.Length;
        public void Clear() => hits.Clear();
        public void CaptureCollision(Collision2D collision, IReadOnlyDictionary<Rigidbody2D,Projectile> projectiles, IReadOnlyDictionary<Rigidbody2D,Health> health)
        { for(int i=0;i<collision.contactCount;i++) AddPoint(collision.GetContact(i), projectiles, health); }
        public void CapturePersistent(IReadOnlyDictionary<Rigidbody2D,Projectile> projectiles, IReadOnlyDictionary<Rigidbody2D,Health> health)
        {
            var filter=new ContactFilter2D {useTriggers=false};
            foreach(var entry in projectiles)
            {
                if(entry.Key==null || !entry.Value.IsActive) continue;
                int count;
                while((count=entry.Key.GetContacts(filter,buffer))==buffer.Length) Array.Resize(ref buffer,checked(buffer.Length*2));
                for(int i=0;i<count;i++) AddPoint(buffer[i],projectiles,health);
            }
        }
        private void AddPoint(ContactPoint2D point,IReadOnlyDictionary<Rigidbody2D,Projectile> projectiles,IReadOnlyDictionary<Rigidbody2D,Health> health)
        {
            if(point.collider==null || point.otherCollider==null || point.collider.isTrigger || point.otherCollider.isTrigger) return;
            AddSide(point.collider,point.otherCollider,point.point,projectiles,health);
            AddSide(point.otherCollider,point.collider,point.point,projectiles,health);
        }
        private void AddSide(Collider2D source,Collider2D other,Vector2 point,IReadOnlyDictionary<Rigidbody2D,Projectile> projectiles,IReadOnlyDictionary<Rigidbody2D,Health> health)
        {
            if(source.attachedRigidbody==null || !projectiles.TryGetValue(source.attachedRigidbody,out var projectile) || !projectile.IsActive) return;
            if(other.attachedRigidbody!=null && projectiles.ContainsKey(other.attachedRigidbody)) return;
            Health target=null;
            if(other.attachedRigidbody!=null) health.TryGetValue(other.attachedRigidbody,out target);
            // Every non-trigger solid is a blocker. Only living registered actors take damage.
            if(target!=null && !target.IsValid) target=null;
            var hit=new Hit(projectile,target,point,target!=null ? target.ActorId : 0);
            if(!hits.TryGetValue(projectile,out var previous) || hit.Distance<previous.Distance-0.000001f ||
                (Mathf.Abs(hit.Distance-previous.Distance)<=0.000001f &&
                    ((hit.Target==null && previous.Target!=null) ||
                     ((hit.Target==null)==(previous.Target==null) && hit.TieId<previous.TieId)))) hits[projectile]=hit;
        }
    }
}
