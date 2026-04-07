namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Стратегия конкретного действия: умеет строить шаг для проблемы и проецировать его исход.
    /// </summary>
    internal interface IActionStrategy
    {
        BotAction Action { get; }

        bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ObstacleInfo target,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason);

        StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld);
    }
}
