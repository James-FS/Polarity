using System;
using System.Collections.Generic;
using Polarity.Controllers;
using UnityEngine;
namespace Polarity.Entities
{
    /// <summary>Explicit authored order; no traversal-order IDs or runtime values in assets.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelRoom : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private PolarityBody[] initialBodies = Array.Empty<PolarityBody>();
        [SerializeField] private Turret[] turrets = Array.Empty<Turret>();
        [SerializeField] private SpriteRenderer[] occludingSprites = Array.Empty<SpriteRenderer>();
        public PlayerController Player => player;
        public PolarityBody[] CopyBodies() => (PolarityBody[])initialBodies.Clone();
        public Turret[] CopyTurrets() => (Turret[])turrets.Clone();
        public SpriteRenderer[] CopyOccluders() => (SpriteRenderer[])occludingSprites.Clone();
        public bool HasValidBindings()
        {
            if (player == null || !player.transform.IsChildOf(transform)) return false;
            var unique = new HashSet<PolarityBody>();
            foreach (var body in initialBodies)
                if (body == null || !body.transform.IsChildOf(transform) || !unique.Add(body)) return false;
            var uniqueTurrets = new HashSet<Turret>();
            foreach (var turret in turrets)
                if (turret == null || !turret.transform.IsChildOf(transform) || !uniqueTurrets.Add(turret)) return false;
            return true;
        }
    }
}
