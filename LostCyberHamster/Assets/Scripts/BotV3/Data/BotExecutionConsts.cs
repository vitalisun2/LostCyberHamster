namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Runtime-константы executor'а.
    /// Вынесены из StepExecutor, чтобы правила исполнения читались отдельно от control flow.
    /// </summary>
    internal static class BotExecutionConsts
    {
        public const float JumpMinCompletionDelay = 0.3f;
        public const float JumpLateFallbackDistance = 0.1f;
        public const float StepTooLateThreshold = -0.3f;
        public const float SwitchLaneMinElapsed = 0.1f;
        public const float SwitchLaneCompletionTolerance = 0.12f;

        public const string JumpClipName = "transform_jump";
    }
}
