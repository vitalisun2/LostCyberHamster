using System.Collections.Generic;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Агрегированный результат ветви планировщика.
    /// Отделяет outcome (что произошло/собрано) от structure (шаги).
    /// </summary>
    public class BranchOutcome
    {
        public List<ObstacleInfo> CollectedObjects = new List<ObstacleInfo>();
        public int TotalProfit;
        public int TotalEnergyCost;
        public int NetEnergyGain;
        public DecisionRank BestRank;
        public bool AllStepsSafe = true;
    }
}
