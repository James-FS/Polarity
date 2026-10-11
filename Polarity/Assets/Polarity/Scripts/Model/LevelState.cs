using System;

namespace Polarity.Model
{
    /// <summary>Per-room runtime facts. Configuration assets never hold these values.</summary>
    public sealed class LevelState
    {
        public LevelPhase Phase { get; private set; } = LevelPhase.Ready;
        public int MarkBudget { get; private set; }
        public bool UnlimitedMarks { get; private set; }
        public int RemainingMarks { get; private set; }
        public int ActiveEnemyCount { get; private set; }

        public event Action<LevelPhase> PhaseChanged;
        public event Action<int> MarksChanged;
        public event Action<int> EnemyCountChanged;

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
            if (UnlimitedMarks) return true;
            if (RemainingMarks <= 0)
                return false;
            RemainingMarks--;
            MarksChanged?.Invoke(RemainingMarks);
            return true;
        }

        internal bool AddRuntimeMarks(int amount)
        {
            if (amount <= 0 || MarkBudget == int.MaxValue) return false;
            int added = Math.Min(amount, int.MaxValue - MarkBudget);
            MarkBudget += added;
            RemainingMarks += added;
            MarksChanged?.Invoke(RemainingMarks);
            return true;
        }

        internal void SetUnlimitedMarks(bool value)
        {
            if (UnlimitedMarks == value) return;
            UnlimitedMarks = value;
            MarksChanged?.Invoke(RemainingMarks);
        }

        internal void SetActiveEnemyCount(int count)
        {
            int next = Math.Max(0, count);
            if (next == ActiveEnemyCount) return;
            ActiveEnemyCount = next;
            EnemyCountChanged?.Invoke(next);
        }
    }
}
