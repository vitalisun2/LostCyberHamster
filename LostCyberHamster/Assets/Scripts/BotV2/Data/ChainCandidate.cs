using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Кандидат planner-ветви: произвольный список шагов + агрегированный outcome.
    /// </summary>
    public class ChainCandidate
    {
        public List<ChainStep> Steps = new List<ChainStep>();
        public BranchOutcome Outcome = new BranchOutcome();
    }
}