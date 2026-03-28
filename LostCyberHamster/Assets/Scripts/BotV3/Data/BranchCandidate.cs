using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Кандидат ветви планировщика: список шагов + агрегированный outcome.
    /// </summary>
    public class BranchCandidate
    {
        public List<BranchStep> Steps { get; }
        public BranchOutcome Outcome { get; }

        public BranchCandidate(List<BranchStep> steps, BranchOutcome outcome)
        {
            Steps = steps ?? new List<BranchStep>();
            Outcome = outcome ?? new BranchOutcome(0, true);
        }
    }
}
