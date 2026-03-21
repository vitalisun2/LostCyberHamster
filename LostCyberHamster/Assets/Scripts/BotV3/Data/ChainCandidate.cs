using System.Collections.Generic;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Кандидат ветви планировщика: список шагов + агрегированный outcome.
    /// </summary>
    public class ChainCandidate
    {
        public List<ChainStep> Steps = new List<ChainStep>();
        public BranchOutcome Outcome = new BranchOutcome();
    }
}
