using Pole = Polarity.Model.Polarity;

namespace Polarity.Model
{
    public readonly struct MarkRequest
    {
        public readonly int TargetId;
        public readonly Pole Polarity;
        public readonly long Sequence;
        public MarkRequest(int targetId, Pole polarity, long sequence)
        { TargetId = targetId; Polarity = polarity; Sequence = sequence; }
    }

    public enum MarkOutcome
    {
        Applied, NotRunning, InvalidTarget, NotMarkable, InvalidPolarity,
        PlayerUnavailable, TooFar, WallBlocked, Unchanged, BudgetEmpty
    }

    public readonly struct MarkResult
    {
        public readonly MarkRequest Request;
        public readonly MarkOutcome Outcome;
        public readonly int RemainingMarks;
        public MarkResult(MarkRequest request, MarkOutcome outcome, int remainingMarks)
        { Request = request; Outcome = outcome; RemainingMarks = remainingMarks; }
    }
}
