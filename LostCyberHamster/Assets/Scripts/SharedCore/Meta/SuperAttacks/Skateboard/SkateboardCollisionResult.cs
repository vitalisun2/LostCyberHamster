namespace Vues.GameCore
{
    /// <summary>
    /// Описывает одно Skateboard collision-решение без выполнения production outcome.
    /// </summary>
    public readonly struct SkateboardCollisionResult
    {
        public SkateboardCollisionResult(
            SkateboardInteractionPolicy.Outcome outcome,
            bool wasJumpCollisionActive)
        {
            Outcome = outcome;
            WasJumpCollisionActive = wasJumpCollisionActive;
        }

        public SkateboardInteractionPolicy.Outcome Outcome { get; }
        public bool WasJumpCollisionActive { get; }
    }
}
