using Polarity.Controllers;
using Polarity.Model;
using Polarity.Simulation;
using UnityEngine;

namespace Polarity.Views
{
    /// <summary>Temporary read-only budget/result text and candidate outline for step 4.</summary>
    [DisallowMultipleComponent]
    public sealed class MarkingDebugView : MonoBehaviour
    {
        [SerializeField] private SimulationWorld world;
        [SerializeField] private MarkingController marking;
        private static Rect Panel => new Rect(12, 12, 460, 112);
        public bool ContainsScreenPoint(Vector2 screenPoint) => isActiveAndEnabled &&
            Panel.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));

        private void OnGUI()
        {
            if (world == null || world.State == null) return;
            GUI.Box(Panel, "Step 4 - movement and limited marking");
            GUI.Label(new Rect(24, 38, 436, 22), $"Marks: {world.State.RemainingMarks}/{world.State.MarkBudget}   WASD: move   LMB: +   RMB: -");
            string result = world.LastMarkResult.HasValue ? Describe(world.LastMarkResult.Value.Outcome) : "Ready";
            GUI.Label(new Rect(24, 62, 436, 22), result);
            GUI.Label(new Rect(24, 86, 436, 22), world.State.RemainingMarks == 0 ?
                "No marks left. Movement remains available. Reopen Play to reset." : marking != null ? marking.PointerFeedback : "");
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
