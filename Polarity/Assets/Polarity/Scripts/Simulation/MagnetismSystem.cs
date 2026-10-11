using System.Collections.Generic;
using Polarity.Config;
using Polarity.Entities;
using UnityEngine;
using Pole = Polarity.Model.Polarity;
namespace Polarity.Simulation
{
    /// <summary>Pair rules only. World applies forces and advances physics.</summary>
    public sealed class MagnetismSystem
    {
        private readonly RaycastHit2D[] wallHits = new RaycastHit2D[1];
        private ContactFilter2D wallFilter;
        public int PairEvaluationCount { get; private set; }
        public int MagneticPairCount { get; private set; }
        public int BlockedPairCount { get; private set; }
        public void Evaluate(IReadOnlyList<BodySample> samples, GameplayConfig.Settings settings, Vector2[] forces, List<Link> links)
        {
            wallFilter=new ContactFilter2D(); wallFilter.SetLayerMask(settings.MagneticBlockingLayers); wallFilter.useTriggers=false;

            PairEvaluationCount = MagneticPairCount = BlockedPairCount = 0;
            links.Clear();
            float radiusSquared = settings.MagneticRadius * settings.MagneticRadius;
            for (int i = 0; i < samples.Count; i++)
            for (int j = i + 1; j < samples.Count; j++)
            {
                PairEvaluationCount++;
                BodySample a = samples[i], b = samples[j];
                if (a.Polarity == Pole.Neutral || b.Polarity == Pole.Neutral || settings.MagneticStrength <= 0f)
                    continue;
                Vector2 offset = b.Position - a.Position;
                float distanceSquared = offset.sqrMagnitude;
                if (!IsFinite(offset) || distanceSquared >= radiusSquared)
                    continue;
                if (Physics2D.Linecast(a.Position, b.Position, wallFilter, wallHits) > 0)
                {
                    BlockedPairCount++;
                    continue;
                }
                bool coincident = distanceSquared <= 0.00000001f;
                float distance = Mathf.Sqrt(distanceSquared);
                Vector2 direction = coincident ? StableSeparationAxis(a.Body.BodyId, b.Body.BodyId) : offset / distance;
                // At coincident centers either charge relationship uses bounded separation.
                if (coincident || a.Polarity == b.Polarity)
                    direction = -direction;
                float magnitude = settings.MagneticStrength * (1f - distance / settings.MagneticRadius);
                Vector2 force = direction * magnitude;
                forces[i] += force;
                forces[j] -= force;
                MagneticPairCount++;
                links.Add(new Link(a.Position, b.Position));
            }
        }


        private static Vector2 StableSeparationAxis(int a, int b)
        {
            uint hash = unchecked((uint)a * 73856093u ^ (uint)b * 19349663u);
            float angle = (hash % 8) * (Mathf.PI / 4f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }


        private static bool IsFinite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        public readonly struct BodySample
        {
            public readonly PolarityBody Body;
            public readonly Vector2 Position;
            public readonly Pole Polarity;
            public BodySample(PolarityBody body,Vector2 position,Pole polarity) { Body=body;Position=position;Polarity=polarity; }
        }
        public readonly struct Link
        {
            public readonly Vector2 A,B;
            public Link(Vector2 a,Vector2 b) { A=a;B=b; }
        }
    }
}
