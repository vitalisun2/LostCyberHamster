using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Кандидат ветви планировщика: список шагов + агрегированный outcome.
    /// </summary>
    public class BranchCandidate
    {
        public List<BranchStep> Steps = new List<BranchStep>();
        public BranchOutcome Outcome = new BranchOutcome();
    }
}
