using Polarity.Entities;
using UnityEngine;
namespace Polarity.Model
{
    public enum DamageKind { Impact, EnemyTouch, Spike, Projectile }
    public readonly struct DamageContact
    {
        public readonly Health Target, SourceHealth;
        public readonly int SourceId;
        public readonly DamageKind Kind;
        public readonly float NormalSpeed;
        public readonly Vector2 Point;
        public DamageContact(Health target, Health source, int sourceId, DamageKind kind, float speed, Vector2 point)
        { Target = target; SourceHealth = source; SourceId = sourceId; Kind = kind; NormalSpeed = speed; Point = point; }
    }
    public readonly struct DamageResult
    {
        public readonly long Step;
        public readonly int SourceId, TargetId, Amount, RemainingHealth;
        public readonly DamageKind Kind;
        public readonly float NormalSpeed;
        public readonly Vector2 Position;
        public bool Killed => RemainingHealth == 0;
        public DamageResult(long step, DamageContact contact, int amount, int remaining)
        { Step = step; SourceId = contact.SourceId; TargetId = contact.Target.ActorId; Amount = amount;
          RemainingHealth = remaining; Kind = contact.Kind; NormalSpeed = contact.NormalSpeed; Position = contact.Target.Rigidbody.position; }
    }
    public readonly struct LevelOutcome
    {
        public readonly LevelPhase Phase;
        public readonly long Step;
        public readonly double SimulationTime;
        public readonly int PlayerHealth, RemainingEnemies, RemainingMarks;
        public LevelOutcome(LevelPhase phase, long step, double time, int health, int enemies, int marks)
        { Phase = phase; Step = step; SimulationTime = time; PlayerHealth = health; RemainingEnemies = enemies; RemainingMarks = marks; }
    }
}
