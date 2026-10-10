using Polarity.Controllers;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Temporary read-only budget/result text and candidate outline for the prototype.</summary>
    [DisallowMultipleComponent]
    public sealed class MarkingDebugView : MonoBehaviour
    {
        [SerializeField] private SimulationWorld world;
        [SerializeField] private MarkingController marking;
        private static Rect Panel => new Rect(12, 12, 460, 330);
        public bool ContainsScreenPoint(Vector2 screenPoint) => isActiveAndEnabled &&
            Panel.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));

        private void OnGUI()
        {
            if (world == null || world.State == null) return;
            GUI.Box(Panel, "Prototype - turret fire and polarity");
            GUI.Label(new Rect(24, 38, 436, 22), $"Marks: {world.State.RemainingMarks}/{world.State.MarkBudget}   WASD: move   LMB: +   RMB: -");
            string markText = world.LastMarkResult.HasValue ? Describe(world.LastMarkResult.Value.Outcome) : "Ready";
            GUI.Label(new Rect(24, 62, 436, 22), markText);
            GUI.Label(new Rect(24, 86, 436, 22), world.State.RemainingMarks == 0 ?
                "No marks left. Movement remains available. R: retry." : marking != null ? marking.PointerFeedback : "");
            GUI.Label(new Rect(24, 110, 436, 22), $"Step {world.CompletedStepCount} | contacts {world.LastContactPairs.Count} | candidates {world.LastPropagationCandidateCount} | transfers {world.LastPropagationResults.Count}");
            string contactText = "Physical pairs:";
            for (int i = 0; i < Mathf.Min(4, world.LastContactPairs.Count); i++)
            {
                var pair = world.LastContactPairs[i];
                contactText += $" {pair.AId}-{pair.BId}({pair.NormalImpulse:0.###})";
            }
            GUI.Label(new Rect(24, 134, 436, 22), contactText);
            string transfer = "Propagation: none yet (free, one hop per step).";
            if (world.LastPropagation.HasValue)
            {
                var result = world.LastPropagation.Value;
                transfer = $"Propagation step {result.Step}: {result.SourceId} -> {result.TargetId} {result.Polarity} (free)";
                GUI.Label(new Rect(24, 206, 436, 22), $"Chosen impulse {result.NormalImpulse:0.####} | source candidates {result.CandidateCount}");
            }
            GUI.Label(new Rect(24, 158, 436, 22), transfer);
            string candidateText = "Candidates:";
            for (int i = 0; i < Mathf.Min(4, world.LastPropagationCandidates.Count); i++)
            {
                var candidate = world.LastPropagationCandidates[i];
                candidateText += $" {candidate.SourceId}>{candidate.TargetId} {(candidate.Polarity == Polarity.Model.Polarity.Positive ? "+" : "-")}({candidate.NormalImpulse:0.###})";
            }
            GUI.Label(new Rect(24, 182, 436, 22), candidateText);
            GUI.Label(new Rect(24,230,436,22), $"HP: {world.PlayerHealth?.CurrentHealth}/{world.PlayerHealth?.MaxHealth} | enemies: {world.State.ActiveEnemyCount} | {world.State.Phase} | R: retry");
            string hit = world.LastDamageResults.Count>0 ? $"Hit: {world.LastDamageResults[0].Kind} -> {world.LastDamageResults[0].TargetId} (HP {world.LastDamageResults[0].RemainingHealth})" : "Low-speed enemy contacts do not damage enemies.";
            GUI.Label(new Rect(24,254,436,22),hit);
            GUI.Label(new Rect(24,278,436,22),world.State.Phase==LevelPhase.Won ? "ROOM CLEARED - R: play again" : world.State.Phase==LevelPhase.Lost ? "PLAYER DOWN - R: retry" : "Enemy touch hurts player; fast impacts hurt enemies.");
            GUI.Label(new Rect(24,302,436,22),$"Shots: {world.ActiveProjectileCount} | negative, cannot mark or spread.");
            if (marking == null || marking.Candidate == null || world.State.Phase != LevelPhase.Running) return;
            var view = marking.Candidate.GetComponent<PolarityView>();
            if (view == null) return;
            bool found = false;
            Rect outline = default;
            foreach (var face in view.BodySprites)
            {
                if (!MarkingController.TryScreenRect(face, marking.WorldCamera, out Rect rect)) continue;
                outline = found ? Rect.MinMaxRect(Mathf.Min(outline.xMin, rect.xMin), Mathf.Min(outline.yMin, rect.yMin),
                    Mathf.Max(outline.xMax, rect.xMax), Mathf.Max(outline.yMax, rect.yMax)) : rect;
                found = true;
            }
            if (!found) return;
            outline.y = Screen.height - outline.yMax;
            Color previous = GUI.color;
            GUI.color = Color.yellow;
            GUI.DrawTexture(new Rect(outline.x - 3, outline.y - 3, outline.width + 6, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.x - 3, outline.yMax + 1, outline.width + 6, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.x - 3, outline.y - 3, 2, outline.height + 6), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.xMax + 1, outline.y - 3, 2, outline.height + 6), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string Describe(MarkOutcome outcome)
        {
            switch (outcome)
            {
                case MarkOutcome.Applied: return "Polarity changed. Used 1 mark.";
                case MarkOutcome.Unchanged: return "Same polarity: no mark used.";
                case MarkOutcome.TooFar: return "Out of marking range: no mark used.";
                case MarkOutcome.WallBlocked: return "Wall blocks marking: no mark used.";
                case MarkOutcome.BudgetEmpty: return "No marks remaining.";
                case MarkOutcome.InvalidTarget: return "No valid target: no mark used.";
                case MarkOutcome.NotMarkable: return "Target cannot be marked: no mark used.";
                case MarkOutcome.NotRunning: return "Simulation is not running.";
                default: return "Request rejected: no mark used.";
            }
        }
    }
}
