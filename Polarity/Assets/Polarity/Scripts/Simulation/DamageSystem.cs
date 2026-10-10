using System.Collections.Generic;
using Polarity.Entities;
using Polarity.Model;
namespace Polarity.Simulation
{
    /// <summary>Plans at most one hit per victim before any death is committed.</summary>
    public sealed class DamageSystem
    {
        private readonly Dictionary<Health,DamageContact> winners = new Dictionary<Health,DamageContact>();
        public void BuildPlan(IReadOnlyList<DamageContact> contacts,double time,float impactThreshold,List<DamageContact> plan)
        {
            winners.Clear(); plan.Clear();
            foreach(var contact in contacts)
            {
                if (contact.Target==null || !contact.Target.CanReceiveDamage(time) ||
                    (contact.SourceHealth!=null && !contact.SourceHealth.IsValid) ||
                    (contact.Kind==DamageKind.Impact && contact.NormalSpeed<impactThreshold)) continue;
                if (!winners.TryGetValue(contact.Target,out var previous) || contact.NormalSpeed>previous.NormalSpeed ||
                    (contact.NormalSpeed==previous.NormalSpeed && (contact.SourceId<previous.SourceId ||
                        (contact.SourceId==previous.SourceId && (int)contact.Kind<(int)previous.Kind)))) winners[contact.Target]=contact;
            }
            plan.AddRange(winners.Values); plan.Sort((a,b)=>a.Target.ActorId.CompareTo(b.Target.ActorId));
        }
        internal void Clear() => winners.Clear();
    }
}
