namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Агрегированный результат ветви планировщика.
    /// </summary>
    public class BranchOutcome
    {
        public int TotalEnergyCost { get; }
        public bool AllStepsSafe { get; }

        public BranchOutcome(int totalEnergyCost, bool allStepsSafe)
        {
            TotalEnergyCost = totalEnergyCost;
            AllStepsSafe = allStepsSafe;
        }
    }
}
