using System.Collections.Generic;
using Polarity.Model;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Simulation
{
    /// <summary>Plans a single hop from an immutable step snapshot; the World commits afterwards.</summary>
    public sealed class PropagationSystem
    {
        public const float ImpulseTieTolerance = 0.0001f;
        private readonly Dictionary<int, BodySnapshot> byId = new Dictionary<int, BodySnapshot>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly Dictionary<int, float> maxima = new Dictionary<int, float>();
        private readonly Dictionary<int, Candidate> winners = new Dictionary<int, Candidate>();
        private readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        private readonly List<PropagationCandidate> candidateDetails = new List<PropagationCandidate>();
        private IReadOnlyList<PropagationCandidate> readOnlyCandidates;
        public IReadOnlyList<PropagationCandidate> Candidates => readOnlyCandidates ?? (readOnlyCandidates = candidateDetails.AsReadOnly());
        public int CandidateCount => candidates.Count;

        public void BuildPlan(long step, IReadOnlyList<BodySnapshot> snapshot, IReadOnlyList<BodyContactPair> contacts, List<PropagationResult> plan)
        {
            Clear(); plan.Clear();
            foreach (var body in snapshot) if (body.Eligible && body.IsStillValid) byId[body.BodyId] = body;
            foreach (var pair in contacts)
            {
                if (!byId.TryGetValue(pair.AId, out var a) || !byId.TryGetValue(pair.BId, out var b)) continue;
                AddCandidate(a, b, pair.NormalImpulse);
                AddCandidate(b, a, pair.NormalImpulse);
            }
            // Compute a global maximum first: epsilon ties must not depend on candidate traversal order.
            foreach (var candidate in candidates)
            {
                int target = candidate.Target.BodyId;
                if (candidate.Impulse < maxima[target] - ImpulseTieTolerance) continue;
                if (!winners.TryGetValue(target, out var previous) || candidate.Source.BodyId < previous.Source.BodyId)
                    winners[target] = candidate;
            }
            foreach (var candidate in winners.Values)
                plan.Add(new PropagationResult(step, candidate.Source, candidate.Target, candidate.Impulse, counts[candidate.Target.BodyId]));
            plan.Sort((a, b) => a.TargetId.CompareTo(b.TargetId));
        }

        private void AddCandidate(BodySnapshot source, BodySnapshot target, float impulse)
        {
            if (!source.CanSpread || !target.CanReceive || source.Polarity == Pole.Neutral || target.Polarity != Pole.Neutral) return;
            if (float.IsNaN(impulse) || float.IsInfinity(impulse) || impulse < 0f) impulse = 0f;
            candidates.Add(new Candidate(source, target, impulse));
            candidateDetails.Add(new PropagationCandidate(source.BodyId, target.BodyId, source.Polarity, impulse));
            if (!maxima.TryGetValue(target.BodyId, out float maximum) || impulse > maximum) maxima[target.BodyId] = impulse;
            counts.TryGetValue(target.BodyId, out int count); counts[target.BodyId] = count + 1;
        }

        internal void Clear()
        {
            byId.Clear(); candidates.Clear(); maxima.Clear(); winners.Clear(); counts.Clear(); candidateDetails.Clear();
        }

        private readonly struct Candidate
        {
            public readonly BodySnapshot Source, Target;
            public readonly float Impulse;
            public Candidate(BodySnapshot source, BodySnapshot target, float impulse)
            { Source = source; Target = target; Impulse = impulse; }
        }
    }
}
