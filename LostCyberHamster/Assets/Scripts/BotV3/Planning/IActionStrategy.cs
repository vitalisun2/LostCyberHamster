namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Стратегия конкретного действия: умеет строить шаг для проблемы и проецировать его исход.
    /// </summary>
    internal interface IActionStrategy
    {
        BotAction Action { get; }

        bool TryBuildStep(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            ProjectedWorld projectedWorld,
            out BranchStep step,
            out string rejectReason);

        StepProjectionResult Project(
            BotSceneSnapshot snapshot,
            BranchStep step,
            ProjectedWorld projectedWorld);
    }
}
