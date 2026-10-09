using System;

namespace Polarity.Model
{
    /// <summary>Per-room runtime facts. Configuration assets never hold these values.</summary>
    public sealed class LevelState
    {
        public LevelPhase Phase { get; private set; } = LevelPhase.Ready;
        public int MarkBudget { get; }
        public int RemainingMarks { get; private set; }
        public int ActiveEnemyCount { get; private set; }

        public event Action<LevelPhase> PhaseChanged;

        public LevelState(int markBudget, int activeEnemyCount)
        {
            if (markBudget < 0 || activeEnemyCount < 0)
                throw new ArgumentOutOfRangeException();
            MarkBudget = RemainingMarks = markBudget;
            ActiveEnemyCount = activeEnemyCount;
        }

        internal void SetPhase(LevelPhase phase)
        {
            if (!Enum.IsDefined(typeof(LevelPhase), phase))
                throw new ArgumentOutOfRangeException(nameof(phase));
            if (Phase == phase)
                return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        internal bool TrySpendMark()
        {
            if (RemainingMarks <= 0)
                return false;
            RemainingMarks--;
            return true;
        }

        internal void SetActiveEnemyCount(int count)
        {
            ActiveEnemyCount = Math.Max(0, count);
        }
    }
}
