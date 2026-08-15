namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Описывает одно визуальное действие, синхронизированное с transform-анимацией.
    /// </summary>
    public readonly struct SkinActionContext
    {
        public SkinVisualAction Action { get; }
        public SkinVisualVariant Variant { get; }
        public SkinVisualOutcome Outcome { get; }
        public float Duration { get; }
        public float PlaybackSpeed { get; }
        public long ActionId { get; }

        public SkinActionContext(
            SkinVisualAction action,
            SkinVisualVariant variant,
            SkinVisualOutcome outcome,
            float duration,
            long actionId,
            float playbackSpeed = 1f)
        {
            Action = action;
            Variant = variant;
            Outcome = outcome;
            Duration = duration;
            PlaybackSpeed = playbackSpeed;
            ActionId = actionId;
        }

        public bool IsLoop => Action is SkinVisualAction.GroundRun
            or SkinVisualAction.RoofRun
            or SkinVisualAction.SkateboardRideA
            or SkinVisualAction.SkateboardRideB
            or SkinVisualAction.SkateboardPush;
    }
}
