using UnityEngine;
using Polarity.Entities;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Model
{
    /// <summary>Polarity and permissions captured after marking, before this step's physics.</summary>
    public readonly struct BodySnapshot
    {
        public readonly PolarityBody Body;
        public readonly int BodyId;
        public readonly Pole Polarity;
        public readonly bool CanSpread, CanReceive, Eligible;
        public BodySnapshot(PolarityBody body, bool eligible)
        {
            Body = body; BodyId = body.BodyId; Polarity = body.CurrentPolarity;
            CanSpread = body.CanSpread; CanReceive = body.CanReceive; Eligible = eligible;
        }
        public bool IsStillValid => Body != null && Body.IsValid && Body.Rigidbody.simulated;
    }

    /// <summary>One normalized physical body pair. Double-sided reports do not double its impulse.</summary>
    public readonly struct BodyContactPair
    {
        public readonly int AId, BId, PointCount;
        public readonly float NormalImpulse;
        public readonly Vector2 Point, Normal, RelativeVelocity;
        public readonly bool FromCallback, FromPersistent;
        public BodyContactPair(int aId, int bId, float impulse, int pointCount, Vector2 point,
            Vector2 normal, Vector2 relativeVelocity, bool callback, bool persistent)
        {
            AId = aId; BId = bId; NormalImpulse = impulse; PointCount = pointCount;
            Point = point; Normal = normal; RelativeVelocity = relativeVelocity;
            FromCallback = callback; FromPersistent = persistent;
        }
    }

    public readonly struct PropagationCandidate
    {
        public readonly int SourceId, TargetId;
        public readonly Pole Polarity;
        public readonly float NormalImpulse;
        public PropagationCandidate(int sourceId, int targetId, Pole polarity, float impulse)
        { SourceId = sourceId; TargetId = targetId; Polarity = polarity; NormalImpulse = impulse; }
    }

    public readonly struct PropagationResult
    {
        public readonly long Step;
        public readonly int SourceId, TargetId, CandidateCount;
        public readonly Pole Polarity;
        public readonly float NormalImpulse;
        public readonly Vector2 SourcePosition, TargetPosition;
        public PropagationResult(long step, BodySnapshot source, BodySnapshot target, float impulse, int candidates)
        {
            Step = step; SourceId = source.BodyId; TargetId = target.BodyId; Polarity = source.Polarity;
            NormalImpulse = impulse; CandidateCount = candidates;
            SourcePosition = source.Body.Rigidbody.position; TargetPosition = target.Body.Rigidbody.position;
        }
    }
}
