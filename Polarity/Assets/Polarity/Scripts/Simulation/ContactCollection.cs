using System;
using System.Collections.Generic;
using Polarity.Entities;
using Polarity.Model;
using UnityEngine;

namespace Polarity.Simulation
{
    /// <summary>Reusable callback/current-contact merger. No Collision2D objects survive a callback.</summary>
    internal sealed class ContactCollection
    {
        private ContactPoint2D[] buffer = new ContactPoint2D[8];
        private readonly Dictionary<(int, int), Manifold> batch = new Dictionary<(int, int), Manifold>();
        private readonly Dictionary<(int, int), Manifold> manifolds = new Dictionary<(int, int), Manifold>();
        private readonly Dictionary<(int, int), BodyContactPair> bodyPairs = new Dictionary<(int, int), BodyContactPair>();
        private readonly List<BodyContactPair> pairs = new List<BodyContactPair>();
        private readonly IReadOnlyList<BodyContactPair> readOnlyPairs;
        public IReadOnlyList<BodyContactPair> Pairs => readOnlyPairs;
        public int BufferCapacity => buffer.Length;
        public int BufferExpansionCount { get; private set; }
        public int CallbackBatchCount { get; private set; }
        public int PersistentPointCount { get; private set; }
        public int UniqueColliderPairCount => manifolds.Count;
        public ContactCollection() => readOnlyPairs = pairs.AsReadOnly();

        public void Clear()
        {
            batch.Clear(); manifolds.Clear(); bodyPairs.Clear(); pairs.Clear();
            CallbackBatchCount = PersistentPointCount = 0;
        }

        public void CaptureCollision(Collision2D collision, IReadOnlyDictionary<Rigidbody2D, PolarityBody> registry)
        {
            batch.Clear();
            for (int i = 0; i < collision.contactCount; i++)
                AddPoint(collision.GetContact(i), registry, true);
            if (batch.Count > 0) CallbackBatchCount++;
            MergeBatch();
        }

        public void CapturePersistent(IReadOnlyList<PolarityBody> bodies, IReadOnlyDictionary<Rigidbody2D, PolarityBody> registry)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            foreach (var body in bodies)
            {
                if (body == null || !body.IsValid || !body.Rigidbody.simulated) continue;
                int count;
                // A full array may be truncated. Grow and repeat until the entire query fits.
                while ((count = body.Rigidbody.GetContacts(filter, buffer)) == buffer.Length)
                {
                    Array.Resize(ref buffer, checked(buffer.Length * 2));
                    BufferExpansionCount++;
                }
                batch.Clear();
                for (int i = 0; i < count; i++) AddPoint(buffer[i], registry, false);
                PersistentPointCount += count;
                MergeBatch();
            }
            bodyPairs.Clear(); pairs.Clear();
            foreach (var manifold in manifolds.Values)
            {
                var key = (manifold.AId, manifold.BId);
                var current = new BodyContactPair(manifold.AId, manifold.BId, manifold.Impulse, manifold.Count,
                    manifold.Point, manifold.Normal, manifold.RelativeVelocity, manifold.Callback, manifold.Persistent);
                if (bodyPairs.TryGetValue(key, out var previous))
                    current = new BodyContactPair(current.AId, current.BId, previous.NormalImpulse + current.NormalImpulse,
                        previous.PointCount + current.PointCount, current.Point, current.Normal, current.RelativeVelocity,
                        previous.FromCallback || current.FromCallback, previous.FromPersistent || current.FromPersistent);
                bodyPairs[key] = current;
            }
            pairs.AddRange(bodyPairs.Values);
            pairs.Sort((a, b) => a.AId != b.AId ? a.AId.CompareTo(b.AId) : a.BId.CompareTo(b.BId));
        }

        private void AddPoint(ContactPoint2D point, IReadOnlyDictionary<Rigidbody2D, PolarityBody> registry, bool callback)
        {
            Collider2D a = point.collider, b = point.otherCollider;
            if (a == null || b == null || a.isTrigger || b.isTrigger || a.attachedRigidbody == null || b.attachedRigidbody == null ||
                !registry.TryGetValue(a.attachedRigidbody, out var bodyA) || !registry.TryGetValue(b.attachedRigidbody, out var bodyB) ||
                !bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB) return;
            int aId = bodyA.BodyId, bId = bodyB.BodyId;
            Vector2 normal = point.normal, relativeVelocity = point.relativeVelocity;
            if (aId > bId) { int swap = aId; aId = bId; bId = swap; normal = -normal; relativeVelocity = -relativeVelocity; }
            // Collider instance IDs are ONLY local duplicate keys, never propagation conflict ordering.
            int ca = a.GetInstanceID(), cb = b.GetInstanceID();
            var key = ca < cb ? (ca, cb) : (cb, ca);
            float impulse = float.IsNaN(point.normalImpulse) || float.IsInfinity(point.normalImpulse) ? 0f : Mathf.Max(0f, point.normalImpulse);
            batch.TryGetValue(key, out var value);
            value.AId = aId; value.BId = bId; value.Impulse += impulse; value.Count++;
            value.Point = point.point; value.Normal = normal; value.RelativeVelocity = relativeVelocity;
            value.Callback = callback; value.Persistent = !callback;
            batch[key] = value;
        }

        private void MergeBatch()
        {
            foreach (var entry in batch)
            {
                Manifold value = entry.Value;
                if (manifolds.TryGetValue(entry.Key, out var previous))
                {
                    // Each batch already sums a manifold's contact points. Its mirror/query is a duplicate.
                    // Keep the largest observation, never sum copies of the same collider pair.
                    // On equal impulses retain the earlier callback numbers, not a later query copy.
                    if (previous.Impulse >= value.Impulse) value = previous;
                    value.Impulse = Mathf.Max(previous.Impulse, entry.Value.Impulse);
                    value.Count = Mathf.Max(previous.Count, entry.Value.Count);
                    value.Callback = previous.Callback || entry.Value.Callback;
                    value.Persistent = previous.Persistent || entry.Value.Persistent;
                }
                manifolds[entry.Key] = value;
            }
            batch.Clear();
        }

        private struct Manifold
        {
            public int AId, BId, Count;
            public float Impulse;
            public Vector2 Point, Normal, RelativeVelocity;
            public bool Callback, Persistent;
        }
    }
}
