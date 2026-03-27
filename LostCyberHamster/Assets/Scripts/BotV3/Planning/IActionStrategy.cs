namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Контракт action-specific логики planner'а:
    /// построение шага и его проекция в будущее.
    /// </summary>
    public interface IActionStrategy
    {
        BotAction Action { get; }

        bool CanSolve(ProblemDescriptor problem);

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
