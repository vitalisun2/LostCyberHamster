namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Агрегированный результат ветви планировщика.
    /// </summary>
    public class BranchOutcome
    {
        public int TotalEnergyCost;
        public bool AllStepsSafe = true;
        public float FreeRunAfterFirstStep = float.PositiveInfinity;
    }
}
